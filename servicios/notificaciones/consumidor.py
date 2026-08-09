# servicios/notificaciones/consumidor.py

# PROPÓSITO: escuchar TODOS los eventos del sistema y convertirlos en avisos para
#   recepción. Es el único consumidor que se suscribe con comodín: le interesa
#   todo lo que le pase a una orden.
#
# DEBE CONTENER:
#   1. La conexión al broker leyendo credenciales de variables de entorno, con
#      reintento y espera creciente si RabbitMQ aún no está listo.
#   2. La declaración del exchange "hotel.eventos" (topic, durable) de forma
#      idempotente.
#   3. La declaración de la cola "notificaciones.eventos" con binding a la
#      routing key comodín "orden.*", que cubre los tres eventos de una vez.
#   4. El bucle de consumo en segundo plano, sin bloquear a FastAPI.
#   5. El despacho por tipo de evento: orden.creada, orden.asignada y
#      orden.resuelta, cada uno a su plantilla correspondiente.
#   6. El manejo de un evento DESCONOCIDO que entre por el comodín: registrarlo y
#      confirmarlo, nunca reventar. Es el riesgo que se acepta al usar "orden.*".
#   7. La verificación de idempotencia por eventoId, para no mostrar el mismo
#      aviso dos veces en la bandeja.
#   8. La escritura del aviso en el almacén en memoria de main.py.
#   9. La confirmación manual del mensaje (ack) después de guardarlo.
#
# NO DEBE CONTENER:
#   1. La redacción de los textos; se delega a plantillas.py.
#   2. Publicación de eventos: este servicio solo consume, nunca produce.
#   3. Llamadas HTTP a otros servicios para "completar" datos que falten en el
#      evento; si un dato falta, el que está mal es el contrato.
#   4. Persistencia en base de datos.
#   5. Reglas de negocio sobre órdenes, habitaciones o técnicos.
#
# RELACIONADO:
#   - contratos/orden.creada.v1.json, orden.asignada.v1.json, orden.resuelta.v1.json
#   - docs/catalogo-eventos.md (tabla de colas y bindings)
#   - plantillas.py, main.py (almacén en memoria)

import asyncio
import json
import logging
import os

import aio_pika
from aio_pika.abc import AbstractIncomingMessage

from plantillas import PLANTILLAS_POR_TIPO_EVENTO

# NOTA: main.py aún no está implementado. Se asume que expone estas dos
# funciones sobre el almacén en memoria (máx. 50 avisos):
#   existe_evento_id(evento_id: str) -> bool
#   agregar_aviso(aviso: dict) -> None
# Si main.py termina usando otros nombres, este import debe ajustarse
# cuando se implemente ese archivo (no se toca aquí de antemano).
from main import (
    agregar_aviso,
    existe_evento_id,
    habitacion_de_orden,
    recordar_habitacion,
)
from plantillas import HABITACION_DESCONOCIDA, aviso_por_defecto

logger = logging.getLogger("notificaciones.consumidor")

EXCHANGE_NOMBRE = os.environ.get("EXCHANGE", "hotel.eventos")
COLA_NOMBRE = "notificaciones.eventos"
ROUTING_KEY_COMODIN = "orden.*"

# Backoff creciente para la conexión inicial a RabbitMQ.
ESPERA_INICIAL_SEGUNDOS = 1
ESPERA_MAXIMA_SEGUNDOS = 30

PREFETCH_COUNT = 10

# Conexión viva, para que main.py pueda reportar en /salud si el broker está
# realmente conectado y no solo si la tarea de fondo sigue corriendo. La nota de
# salud() en main.py describía justamente esta carencia.
_conexion: "aio_pika.abc.AbstractRobustConnection | None" = None


def conexion_activa() -> bool:
    """Si hay conexión abierta al broker ahora mismo."""
    return _conexion is not None and not _conexion.is_closed


def _url_rabbitmq() -> str:
    """Arma la URL de conexión desde variables de entorno.

    Acepta RABBITMQ_URL completa si está definida. Si no, la construye a partir
    de las variables separadas (RABBITMQ_HOST, RABBITMQ_USUARIO, ...), que es lo
    que docker-compose.yml ya le pasa al resto de los servicios.
    """
    url = os.environ.get("RABBITMQ_URL")
    if url:
        return url

    host = os.environ.get("RABBITMQ_HOST")
    if not host:
        raise RuntimeError(
            "Falta RABBITMQ_URL o RABBITMQ_HOST para conectar a RabbitMQ."
        )

    puerto = os.environ.get("RABBITMQ_PUERTO", "5672")
    usuario = os.environ.get("RABBITMQ_USUARIO", "guest")
    contrasena = os.environ.get("RABBITMQ_CONTRASENA", "guest")

    return f"amqp://{usuario}:{contrasena}@{host}:{puerto}/"


async def _conectar_con_reintentos() -> aio_pika.RobustConnection:
    """Reintenta la conexión a RabbitMQ con espera creciente hasta lograrla."""
    espera = ESPERA_INICIAL_SEGUNDOS
    url = _url_rabbitmq()

    while True:
        try:
            conexion = await aio_pika.connect_robust(url)
            logger.info("Conectado a RabbitMQ.")
            return conexion
        except Exception as error:  # RabbitMQ aún no está listo
            logger.warning(
                "No se pudo conectar a RabbitMQ (%s). Reintentando en %ss.",
                error,
                espera,
            )
            await asyncio.sleep(espera)
            espera = min(espera * 2, ESPERA_MAXIMA_SEGUNDOS)


def _procesar_evento(cuerpo_bytes: bytes) -> None:
    """Decodifica, valida idempotencia, despacha a la plantilla y guarda.

    Deja que cualquier excepción se propague: quien llama decide qué hacer
    con el ack/nack (ver on_mensaje).
    """
    evento = json.loads(cuerpo_bytes.decode("utf-8"))

    evento_id = evento.get("eventoId")
    tipo_evento = evento.get("tipoEvento")

    if not evento_id or not tipo_evento:
        logger.error(
            "Evento sin eventoId o tipoEvento, se descarta sin generar aviso: %s",
            evento,
        )
        return

    if existe_evento_id(evento_id):
        logger.info(
            "Evento %s (%s) ya procesado antes; se ignora para no duplicar el aviso.",
            evento_id,
            tipo_evento,
        )
        return

    # ¿De qué habitación habla este evento?
    #
    # Solo orden.creada trae habitacionNumero. Los otros dos contratos traen
    # ordenId pero no el cuarto, así que se resuelve correlacionando: se recuerda
    # el número al ver la creación y se busca al ver los siguientes.
    orden_id = evento.get("ordenId")
    numero_habitacion = HABITACION_DESCONOCIDA

    if tipo_evento == "orden.creada":
        numero_habitacion = evento.get("habitacionNumero", HABITACION_DESCONOCIDA)
        if orden_id and numero_habitacion != HABITACION_DESCONOCIDA:
            recordar_habitacion(orden_id, numero_habitacion)
    elif orden_id:
        numero_habitacion = habitacion_de_orden(orden_id) or HABITACION_DESCONOCIDA

    plantilla = PLANTILLAS_POR_TIPO_EVENTO.get(tipo_evento)
    if plantilla is None:
        # Riesgo aceptado del binding "orden.*": puede llegar un tipoEvento
        # futuro sin plantilla propia. Se registra un aviso genérico en vez de
        # perderlo, que es justo lo que exige el comodín para no fallar.
        logger.info(
            "Evento de tipo desconocido '%s' (eventoId=%s) recibido por el "
            "binding comodín orden.*; se usa la plantilla por defecto.",
            tipo_evento,
            evento_id,
        )
        plantilla = aviso_por_defecto

    aviso = plantilla(evento, numero_habitacion)
    agregar_aviso(aviso)
    logger.info("Aviso generado para eventoId=%s (%s).", evento_id, tipo_evento)


async def _on_mensaje(mensaje: AbstractIncomingMessage) -> None:
    """Callback de consumo. Ack manual: solo se confirma tras guardar el aviso.

    Un tipoEvento desconocido o un evento ya procesado también terminan en
    ack (no hay nada que reintentar). Un fallo real al parsear/despachar un
    evento reconocido se nackea sin reencolar, para no entrar en un bucle
    infinito de reintentos contra un mensaje que no va a sanar solo.
    """
    try:
        _procesar_evento(mensaje.body)
        await mensaje.ack()
    except Exception:
        logger.exception(
            "Error procesando mensaje con routing_key=%s; se descarta sin "
            "reencolar (el contrato/payload es el que está mal, no este "
            "consumidor).",
            mensaje.routing_key,
        )
        await mensaje.nack(requeue=False)


async def iniciar_consumidor() -> None:
    """Punto de entrada del consumidor. Pensado para lanzarse como tarea de
    fondo desde el evento de arranque de FastAPI en main.py, por ejemplo:

        @app.on_event("startup")
        async def _startup():
            asyncio.create_task(iniciar_consumidor())

    No bloquea el event loop de FastAPI: aio_pika entrega los mensajes vía
    callback sobre el mismo loop, sin necesitar un hilo o proceso aparte.
    """
    global _conexion

    conexion = await _conectar_con_reintentos()
    _conexion = conexion
    canal = await conexion.channel()
    await canal.set_qos(prefetch_count=PREFETCH_COUNT)

    exchange = await canal.declare_exchange(
        EXCHANGE_NOMBRE,
        aio_pika.ExchangeType.TOPIC,
        durable=True,
    )

    cola = await canal.declare_queue(COLA_NOMBRE, durable=True)
    await cola.bind(exchange, routing_key=ROUTING_KEY_COMODIN)

    logger.info(
        "Cola '%s' escuchando '%s' en exchange '%s'.",
        COLA_NOMBRE,
        ROUTING_KEY_COMODIN,
        EXCHANGE_NOMBRE,
    )

    await cola.consume(_on_mensaje, no_ack=False)

    # cola.consume() registra el callback y retorna de inmediato; no bloquea.
    # Sin esto, esta tarea terminaría aquí mismo justo después de registrar
    # el consumidor, lo que rompe dos cosas que dependen de que la tarea
    # siga "viva" mientras el consumidor está activo:
    #   - GET /salud en main.py, que usa el estado de la tarea (task.done())
    #     como aproximación de si el consumidor sigue conectado.
    #   - el apagado ordenado desde el lifespan de main.py: cancelar una
    #     tarea que ya terminó no cierra nada.
    # Se espera indefinidamente hasta que main.py cancele esta tarea al
    # apagar el servicio; en ese momento se cierra el canal y la conexión.
    try:
        await asyncio.Future()
    except asyncio.CancelledError:
        logger.info("Cerrando conexión a RabbitMQ de forma ordenada.")
        try:
            await canal.close()
        except Exception:
            logger.exception("Error cerrando el canal de RabbitMQ durante el apagado.")
        try:
            await conexion.close()
        except Exception:
            logger.exception("Error cerrando la conexión de RabbitMQ durante el apagado.")
        raise