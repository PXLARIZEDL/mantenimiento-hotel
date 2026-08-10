"""Pruebas del consumidor de orden.creada.

Cubren las dos cosas que más se preguntan y que hasta ahora solo se podían
demostrar a mano: la **idempotencia** y la **política de ack/nack**.

Sin infraestructura:

  - La base es **SQLite en memoria**. Se sustituye la fábrica de sesiones de
    base_datos por una que apunta ahí. Las entidades son las mismas de
    producción, así que si el modelo cambia, estas pruebas se enteran.
  - RabbitMQ no aparece: `_recibir` recibe un mensaje falso que anota si le
    hicieron ack o nack, que es exactamente lo que hay que verificar.
  - Las corrutinas se ejecutan con `asyncio.run` para no depender de
    pytest-asyncio.

    pip install -r requirements.txt -r requirements-dev.txt
    pytest
"""

import asyncio
import json
import uuid
from datetime import datetime, timezone

import pytest
from sqlalchemy import create_engine, func, select
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import StaticPool

import base_datos
from configuracion import Configuracion
from consumidor import ConsumidorOrdenCreada
from modelos import Asignacion, Base, Especialidad, EventoProcesado, Tecnico, Turno

# 14:00 UTC son las 10:00 locales con offset -4 -> turno MAÑANA.
MOMENTO = datetime(2026, 8, 10, 14, 0, tzinfo=timezone.utc)


# ---------------------------------------------------------------------------
# Andamiaje
# ---------------------------------------------------------------------------


@pytest.fixture
def sesiones(monkeypatch):
    """Base SQLite en memoria, en lugar de la PostgreSQL de producción.

    StaticPool y check_same_thread son necesarios, no decorativos: el consumidor
    hace el trabajo de base en otro hilo (`asyncio.to_thread`), y SQLite en
    memoria da una base VACÍA por conexión. Sin esto, el hilo de trabajo abre su
    propia base sin tablas y falla con "no such table".
    """
    motor = create_engine(
        "sqlite://",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(motor)
    fabrica = sessionmaker(bind=motor, expire_on_commit=False)

    # El consumidor llama a base_datos.FabricaSesiones(); basta con sustituirla.
    monkeypatch.setattr(base_datos, "FabricaSesiones", fabrica)
    return fabrica


def sembrar(fabrica, *, especialidad=Especialidad.PLOMERIA, turno=Turno.MANANA, nombre="Ana"):
    with fabrica() as sesion:
        tecnico = Tecnico(
            id=uuid.uuid4(),
            nombre=nombre,
            especialidad=especialidad.value,
            turno=turno.value,
            activo=True,
        )
        sesion.add(tecnico)
        sesion.commit()
        return tecnico.id


def evento_creada(evento_id=None, orden_id=None, tipo_falla="PLOMERIA", habitacion=314):
    """Un orden.creada con la forma del contrato, en camelCase."""
    return {
        "eventoId": str(evento_id or uuid.uuid4()),
        "tipoEvento": "orden.creada",
        "version": 1,
        "ocurridoEn": MOMENTO.isoformat(),
        "ordenId": str(orden_id or uuid.uuid4()),
        "habitacionId": str(uuid.uuid4()),
        "habitacionNumero": habitacion,
        "tipoFalla": tipo_falla,
        "descripcion": "Fuga en el lavamanos.",
        "prioridad": "MEDIA",
        "reportadoPor": "recepcion",
    }


class MensajeFalso:
    """Lo mínimo de un mensaje de aio-pika: el cuerpo y si ya se reentregó.

    Anota qué se hizo con él para poder afirmar sobre la política de ack/nack.
    """

    def __init__(self, cuerpo, redelivered=False):
        if isinstance(cuerpo, (dict, list)):
            cuerpo = json.dumps(cuerpo)
        if isinstance(cuerpo, str):
            cuerpo = cuerpo.encode("utf-8")

        self.body = cuerpo
        self.redelivered = redelivered
        self.acciones = []

    async def ack(self):
        self.acciones.append(("ack", None))

    async def nack(self, requeue=False):
        self.acciones.append(("nack", requeue))


def contar(fabrica, entidad):
    with fabrica() as sesion:
        return sesion.scalar(select(func.count()).select_from(entidad))


# ---------------------------------------------------------------------------
# Idempotencia
# ---------------------------------------------------------------------------


def test_el_mismo_evento_dos_veces_no_asigna_dos_veces(sesiones):
    """Es la garantía que sostiene la entrega at-least-once."""
    sembrar(sesiones)
    consumidor = ConsumidorOrdenCreada()

    from modelos import EventoOrdenCreada

    evento = EventoOrdenCreada.model_validate(evento_creada())

    primera = consumidor._aplicar_sincrono(evento)
    segunda = consumidor._aplicar_sincrono(evento)

    assert primera is not None, "la primera vez sí debe asignar"
    assert segunda is None, "la segunda no debe volver a asignar ni publicar"
    assert contar(sesiones, Asignacion) == 1


def test_el_registro_de_idempotencia_se_guarda_por_evento(sesiones):
    """Lo que se guarda es el eventoId, no el ordenId.

    Importa porque una misma orden produce orden.creada, orden.asignada y
    orden.resuelta con el MISMO ordenId: si la clave fuera esa, el segundo y el
    tercero se descartarían como duplicados.
    """
    sembrar(sesiones)
    consumidor = ConsumidorOrdenCreada()

    from modelos import EventoOrdenCreada

    crudo = evento_creada()
    consumidor._aplicar_sincrono(EventoOrdenCreada.model_validate(crudo))

    with sesiones() as sesion:
        registrado = sesion.scalars(select(EventoProcesado)).one()

    assert str(registrado.evento_id) == crudo["eventoId"]
    assert str(registrado.evento_id) != crudo["ordenId"]
    assert registrado.tipo_evento == "orden.creada"


def test_ordenes_distintas_se_procesan_las_dos(sesiones):
    """El filtro de idempotencia no puede ser tan ancho que bloquee lo legítimo."""
    sembrar(sesiones)
    consumidor = ConsumidorOrdenCreada()

    from modelos import EventoOrdenCreada

    primera = EventoOrdenCreada.model_validate(evento_creada(habitacion=101))
    segunda = EventoOrdenCreada.model_validate(evento_creada(habitacion=202))

    assert consumidor._aplicar_sincrono(primera) is not None
    assert consumidor._aplicar_sincrono(segunda) is not None
    assert contar(sesiones, Asignacion) == 2


# ---------------------------------------------------------------------------
# Sin técnico
# ---------------------------------------------------------------------------


def test_sin_tecnico_no_publica_pero_deja_constancia(sesiones):
    """No se publica orden.asignada, pero el evento SÍ queda como procesado.

    El evento se manejó: la decisión fue "ninguno". Reintentarlo daría lo mismo
    mientras no cambie la plantilla.
    """
    # Plantilla vacía a propósito: no se siembra ningún técnico.
    consumidor = ConsumidorOrdenCreada()

    from modelos import EventoOrdenCreada

    evento = EventoOrdenCreada.model_validate(evento_creada())
    resultado = consumidor._aplicar_sincrono(evento)

    assert resultado is None
    assert contar(sesiones, Asignacion) == 0
    assert contar(sesiones, EventoProcesado) == 1


# ---------------------------------------------------------------------------
# La forma de lo que se publica
# ---------------------------------------------------------------------------


def test_lo_publicado_tiene_los_campos_del_contrato(sesiones):
    """Si esto cambia, se rompe el consumidor C# del otro lado."""
    tecnico_id = sembrar(sesiones, nombre="Rosa Vargas")
    consumidor = ConsumidorOrdenCreada()

    from modelos import EventoOrdenCreada

    evento = EventoOrdenCreada.model_validate(evento_creada())
    salida = consumidor._aplicar_sincrono(evento)

    cuerpo = salida.model_dump(by_alias=True, mode="json")

    assert set(cuerpo) == {
        "eventoId",
        "tipoEvento",
        "version",
        "ocurridoEn",
        "ordenId",
        "tecnicoId",
        "tecnicoNombre",
        "especialidad",
    }
    assert cuerpo["tipoEvento"] == "orden.asignada"
    assert cuerpo["tecnicoId"] == str(tecnico_id)
    assert cuerpo["tecnicoNombre"] == "Rosa Vargas"


def test_se_serializa_en_camel_case(sesiones):
    """Sin by_alias saldría snake_case y C# no entendería nada."""
    sembrar(sesiones)
    consumidor = ConsumidorOrdenCreada()

    from modelos import EventoOrdenCreada

    salida = consumidor._aplicar_sincrono(
        EventoOrdenCreada.model_validate(evento_creada())
    )
    cuerpo = salida.model_dump(by_alias=True, mode="json")

    assert "tecnicoNombre" in cuerpo
    assert "tecnico_nombre" not in cuerpo


# ---------------------------------------------------------------------------
# Política de ack / nack
# ---------------------------------------------------------------------------


def test_mensaje_ilegible_se_descarta_sin_reencolar(sesiones):
    """Un mensaje envenenado reencolado atascaría la cola para siempre."""
    consumidor = ConsumidorOrdenCreada()
    mensaje = MensajeFalso("esto no es json")

    asyncio.run(consumidor._recibir(mensaje))

    assert mensaje.acciones == [("nack", False)]


def test_mensaje_al_que_le_falta_un_campo_obligatorio_se_descarta(sesiones):
    """Sin eventoId no hay forma de garantizar idempotencia: pydantic lo rechaza.

    Es la otra cara de la tolerancia a campos DESCONOCIDOS: sobran los que no se
    conocen, pero no puede faltar uno obligatorio.
    """
    consumidor = ConsumidorOrdenCreada()
    cuerpo = evento_creada()
    del cuerpo["eventoId"]

    mensaje = MensajeFalso(cuerpo)
    asyncio.run(consumidor._recibir(mensaje))

    assert mensaje.acciones == [("nack", False)]


def test_un_campo_nuevo_desconocido_no_rompe_el_consumidor(sesiones):
    """Es la regla de versionado del catálogo: agregar un campo opcional en el
    contrato no debe tumbar a quien no lo conoce."""
    sembrar(sesiones)
    consumidor = ConsumidorOrdenCreada()

    cuerpo = evento_creada()
    cuerpo["campoQueNadieConoce"] = "valor futuro"

    mensaje = MensajeFalso(cuerpo)
    asyncio.run(consumidor._recibir(mensaje))

    assert mensaje.acciones == [("ack", None)]
    assert contar(sesiones, Asignacion) == 1


def test_el_exito_confirma_el_mensaje(sesiones):
    sembrar(sesiones)
    consumidor = ConsumidorOrdenCreada()

    mensaje = MensajeFalso(evento_creada())
    asyncio.run(consumidor._recibir(mensaje))

    assert mensaje.acciones == [("ack", None)]
    assert contar(sesiones, Asignacion) == 1


def test_fallo_transitorio_se_reencola_una_sola_vez(sesiones, monkeypatch):
    """Primera entrega: se reencola, por si la base solo tuvo un mal momento."""
    consumidor = ConsumidorOrdenCreada()

    def revienta(_evento):
        raise RuntimeError("la base no responde")

    monkeypatch.setattr(consumidor, "_aplicar_sincrono", revienta)

    mensaje = MensajeFalso(evento_creada(), redelivered=False)
    asyncio.run(consumidor._recibir(mensaje))

    assert mensaje.acciones == [("nack", True)]


def test_si_ya_venia_reentregado_se_descarta(sesiones, monkeypatch):
    """Segunda vez que falla: se descarta, para no atascar la cola."""
    consumidor = ConsumidorOrdenCreada()

    def revienta(_evento):
        raise RuntimeError("la base no responde")

    monkeypatch.setattr(consumidor, "_aplicar_sincrono", revienta)

    mensaje = MensajeFalso(evento_creada(), redelivered=True)
    asyncio.run(consumidor._recibir(mensaje))

    assert mensaje.acciones == [("nack", False)]


# ---------------------------------------------------------------------------
# Configuración de la conexión
# ---------------------------------------------------------------------------


def test_la_url_del_broker_se_arma_con_las_variables_separadas():
    """Son las que docker-compose.yml le pasa al contenedor."""
    configuracion = Configuracion(
        rabbitmq_host="rabbitmq",
        rabbitmq_puerto=5672,
        rabbitmq_usuario="guest",
        rabbitmq_contrasena="guest",
    )

    assert configuracion.url_amqp == "amqp://guest:guest@rabbitmq:5672/"


def test_la_url_respeta_credenciales_distintas():
    """En producción no sería guest/guest, y la contraseña no puede quedar fija."""
    configuracion = Configuracion(
        rabbitmq_host="broker.interno",
        rabbitmq_puerto=5673,
        rabbitmq_usuario="hotel",
        rabbitmq_contrasena="secreta",
    )

    assert configuracion.url_amqp == "amqp://hotel:secreta@broker.interno:5673/"


def test_el_offset_del_hotel_tiene_valor_por_defecto():
    """Si falta la variable, no se cae: usa el huso del hotel.

    Es el valor que convierte la hora UTC del evento al turno local, y el que
    más fácil se olvida al desplegar.
    """
    assert Configuracion().hotel_utc_offset == -4
