"""Frontera entre la cola y el dominio.

Escucha `orden.creada` y, si corresponde, publica `orden.asignada`. Es el único
punto por el que se dispara una asignación: no hay endpoint HTTP que asigne.

Aquí NO vive la regla de elección del técnico (está en asignador.py) ni las
consultas SQL (están en base_datos.py).
"""

from __future__ import annotations

import asyncio
import json
import logging
from datetime import datetime, timezone

import aio_pika
from aio_pika.abc import AbstractIncomingMessage, AbstractRobustConnection

import base_datos
from asignador import Decision, elegir_tecnico
from configuracion import configuracion
from modelos import EventoOrdenAsignada, EventoOrdenCreada

registro = logging.getLogger(__name__)

EVENTO_ENTRADA = "orden.creada"
EVENTO_SALIDA = "orden.asignada"


class ConsumidorOrdenCreada:
    """Mantiene la conexión al broker y procesa los mensajes de la cola."""

    def __init__(self) -> None:
        self._conexion: AbstractRobustConnection | None = None
        self._canal: aio_pika.abc.AbstractChannel | None = None
        self._exchange: aio_pika.abc.AbstractExchange | None = None
        self._tarea: asyncio.Task[None] | None = None

    # -- ciclo de vida -------------------------------------------------------

    def arrancar(self) -> None:
        """Lanza el consumo en segundo plano, sin bloquear a FastAPI."""
        self._tarea = asyncio.create_task(self._correr())

    async def detener(self) -> None:
        if self._tarea is not None:
            self._tarea.cancel()
            try:
                await self._tarea
            except asyncio.CancelledError:
                pass

        if self._conexion is not None and not self._conexion.is_closed:
            await self._conexion.close()

        registro.info("Consumidor detenido.")

    @property
    def conectado(self) -> bool:
        return self._conexion is not None and not self._conexion.is_closed

    # -- bucle ---------------------------------------------------------------

    async def _correr(self) -> None:
        """Conecta y consume, reintentando si RabbitMQ todavía no está listo.

        El servicio debe arrancar aunque el broker no esté: por eso el reintento
        vive aquí y no en un script de espera del Dockerfile.
        """
        while True:
            try:
                await self._conectar()
                await asyncio.Future()  # queda escuchando hasta que se cancele
            except asyncio.CancelledError:
                raise
            except Exception:
                registro.exception(
                    "No se pudo consumir %s; reintento en %ss.",
                    configuracion.cola_orden_creada,
                    configuracion.segundos_espera_reconexion,
                )
                await asyncio.sleep(configuracion.segundos_espera_reconexion)

    async def _conectar(self) -> None:
        self._conexion = await aio_pika.connect_robust(
            configuracion.url_amqp,
            client_properties={"connection_name": "tecnicos.consumidor"},
        )
        self._canal = await self._conexion.channel()

        # Un mensaje a la vez: dos asignaciones en paralelo competirían por los
        # mismos técnicos y podrían repartir mal la carga.
        await self._canal.set_qos(prefetch_count=1)

        # Declaración idempotente: este servicio no asume que ordenes ya creó el
        # exchange. Cualquiera de los cinco puede arrancar primero.
        self._exchange = await self._canal.declare_exchange(
            configuracion.exchange, aio_pika.ExchangeType.TOPIC, durable=True
        )

        cola = await self._canal.declare_queue(
            configuracion.cola_orden_creada, durable=True
        )
        await cola.bind(self._exchange, routing_key=EVENTO_ENTRADA)

        await cola.consume(self._recibir, no_ack=False)

        registro.info(
            "Escuchando %s con binding %s sobre %s.",
            configuracion.cola_orden_creada,
            EVENTO_ENTRADA,
            configuracion.exchange,
        )

    # -- procesamiento -------------------------------------------------------

    async def _recibir(self, mensaje: AbstractIncomingMessage) -> None:
        try:
            evento = EventoOrdenCreada.model_validate_json(mensaje.body)
        except Exception:
            # Mensaje envenenado: reintentarlo daría el mismo error para
            # siempre. Se descarta con log en vez de bloquear la cola.
            registro.exception("Mensaje ilegible en %s; se descarta.", configuracion.cola_orden_creada)
            await mensaje.nack(requeue=False)
            return

        try:
            por_publicar = await asyncio.to_thread(self._aplicar_sincrono, evento)
            await mensaje.ack()
        except Exception:
            # Fallo probablemente transitorio (la base no responde). Reintento
            # ACOTADO: se reencola una vez; si vuelve a fallar ya viene marcado
            # como redelivered y se descarta, para no atascar la cola.
            if mensaje.redelivered:
                registro.exception(
                    "El evento %s falló dos veces; se DESCARTA (orden %s).",
                    evento.evento_id,
                    evento.orden_id,
                )
                await mensaje.nack(requeue=False)
            else:
                registro.exception(
                    "El evento %s falló; se reencola una vez (orden %s).",
                    evento.evento_id,
                    evento.orden_id,
                )
                await mensaje.nack(requeue=True)
            return

        # La publicación va DESPUÉS de confirmar en base, y fuera de la
        # transacción: publicar antes de guardar podría anunciar una asignación
        # que después no existe.
        if por_publicar is not None:
            await self._publicar(por_publicar)

    def _aplicar_sincrono(
        self, evento: EventoOrdenCreada
    ) -> EventoOrdenAsignada | None:
        """Todo el trabajo contra la base, en una sola transacción.

        Devuelve el evento a publicar, o `None` si no hubo asignación. Se
        devuelve en vez de guardarse en el objeto para que no haya estado
        compartido entre mensajes.

        Corre en un hilo aparte (`asyncio.to_thread`) porque SQLAlchemy aquí es
        síncrono y no debe bloquear el bucle de eventos que atiende a FastAPI.
        """
        with base_datos.FabricaSesiones() as sesion:
            # --- IDEMPOTENCIA ---------------------------------------------
            # Se comprueba ANTES de asignar. La entrega es at-least-once:
            # recibir el mismo eventoId dos veces NO puede producir dos
            # técnicos asignados.
            if base_datos.evento_ya_procesado(sesion, evento.evento_id):
                registro.info(
                    "Evento %s ya procesado; se confirma sin hacer nada.", evento.evento_id
                )
                return None

            candidatos = base_datos.candidatos_activos(sesion)

            decision: Decision = elegir_tecnico(
                tipo_falla=evento.tipo_falla,
                momento_utc=evento.ocurrido_en,
                candidatos=candidatos,
                offset_horas=configuracion.hotel_utc_offset,
            )

            tecnico = decision.tecnico

            if tecnico is None:
                # NO se publica orden.asignada: así lo fija
                # contratos/orden.asignada.v1.json. La orden se queda ABIERTA.
                #
                # Se registra el evento como procesado igual, porque el evento
                # SÍ se manejó: la decisión fue "ninguno". Reintentarlo daría lo
                # mismo mientras no cambie la plantilla.
                registro.warning(
                    "Orden %s sin técnico: %s", evento.orden_id, decision.motivo
                )
                base_datos.registrar_evento(sesion, evento.evento_id, EVENTO_ENTRADA)
                sesion.commit()
                return None

            ahora = datetime.now(timezone.utc)

            # El efecto y el registro de idempotencia, en la MISMA transacción.
            base_datos.guardar_asignacion(
                sesion,
                orden_id=evento.orden_id,
                tecnico_id=tecnico.id,
                habitacion_numero=evento.habitacion_numero,
                asignada_en=ahora,
            )
            base_datos.registrar_evento(sesion, evento.evento_id, EVENTO_ENTRADA)
            sesion.commit()

            return EventoOrdenAsignada(
                ocurrido_en=ahora,
                orden_id=evento.orden_id,
                tecnico_id=tecnico.id,
                tecnico_nombre=tecnico.nombre,
                especialidad=tecnico.especialidad,
            )

    async def _publicar(self, evento: EventoOrdenAsignada) -> None:
        if self._exchange is None:
            registro.critical(
                "Orden %s asignada pero SIN canal para publicar orden.asignada.",
                evento.orden_id,
            )
            return

        # by_alias=True es obligatorio: sin él saldría snake_case y el
        # consumidor C# no entendería el mensaje.
        cuerpo = evento.model_dump(by_alias=True, mode="json")

        try:
            await self._exchange.publish(
                aio_pika.Message(
                    body=json.dumps(cuerpo, ensure_ascii=False).encode("utf-8"),
                    content_type="application/json",
                    # Persistente: sobrevive a un reinicio del broker.
                    delivery_mode=aio_pika.DeliveryMode.PERSISTENT,
                    message_id=str(evento.evento_id),
                    type=EVENTO_SALIDA,
                ),
                routing_key=EVENTO_SALIDA,
            )
            registro.info(
                "Publicado %s (orden %s -> %s).",
                EVENTO_SALIDA,
                evento.orden_id,
                evento.tecnico_nombre,
            )
        except Exception:
            # La asignación ya está guardada y es válida. Lo que se pierde es el
            # aviso: ordenes no moverá la orden a ASIGNADA y notificaciones no
            # avisará. Mismo problema de doble escritura que en ordenes; la
            # solución real es un outbox y está anotada como pendiente.
            registro.critical(
                "Orden %s quedó asignada pero NO se publicó %s.",
                evento.orden_id,
                EVENTO_SALIDA,
                exc_info=True,
            )


consumidor = ConsumidorOrdenCreada()
