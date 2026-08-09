"""Única puerta de acceso a la base PostgreSQL del servicio tecnicos.

Concentra conexión, sesiones y consultas para que ni el asignador ni el
consumidor sepan cómo se guardan las cosas. Aquí se BUSCA; en asignador.py se
DECIDE.
"""

from __future__ import annotations

import logging
import uuid
from datetime import datetime, timezone

from sqlalchemy import create_engine, func, select
from sqlalchemy.orm import Session, sessionmaker

from asignador import CandidatoTecnico
from configuracion import configuracion
from modelos import Asignacion, Base, Especialidad, EventoProcesado, Tecnico, Turno

registro = logging.getLogger(__name__)

# pool_pre_ping descarta conexiones muertas antes de usarlas: si PostgreSQL se
# reinicia, la siguiente consulta reconecta en vez de fallar.
motor = create_engine(
    configuracion.database_url,
    pool_pre_ping=True,
    pool_size=5,
    max_overflow=5,
)

FabricaSesiones = sessionmaker(bind=motor, expire_on_commit=False)


def obtener_sesion():
    """Dependencia de FastAPI: entrega una sesión y la cierra siempre."""
    sesion = FabricaSesiones()
    try:
        yield sesion
    finally:
        sesion.close()


# ---------------------------------------------------------------------------
# Arranque
# ---------------------------------------------------------------------------


def crear_tablas() -> None:
    Base.metadata.create_all(motor)


# Al menos uno por especialidad y por turno: sin ellos no hay caso de uso que
# demostrar, porque ninguna orden llegaría a asignarse.
_NOMBRES = {
    (Especialidad.AIRE_ACONDICIONADO, Turno.MANANA): "Luis Ramírez",
    (Especialidad.AIRE_ACONDICIONADO, Turno.TARDE): "Marta Peña",
    (Especialidad.AIRE_ACONDICIONADO, Turno.NOCHE): "Iván Guzmán",
    (Especialidad.PLOMERIA, Turno.MANANA): "Pedro Solano",
    (Especialidad.PLOMERIA, Turno.TARDE): "Rosa Vargas",
    (Especialidad.PLOMERIA, Turno.NOCHE): "Julio Cepeda",
    (Especialidad.CERRADURA, Turno.MANANA): "Ana Beltrán",
    (Especialidad.CERRADURA, Turno.TARDE): "Diego Matos",
    (Especialidad.CERRADURA, Turno.NOCHE): "Sofía Núñez",
    (Especialidad.ELECTRICIDAD, Turno.MANANA): "Carlos Objío",
    (Especialidad.ELECTRICIDAD, Turno.TARDE): "Elena Cruz",
    (Especialidad.ELECTRICIDAD, Turno.NOCHE): "Miguel Adames",
}


def sembrar_tecnicos() -> None:
    """Siembra la plantilla de prueba. Idempotente: si ya hay técnicos, no toca nada."""
    with FabricaSesiones() as sesion:
        if sesion.scalar(select(func.count()).select_from(Tecnico)):
            registro.info("La plantilla ya estaba sembrada; no se toca.")
            return

        for (especialidad, turno), nombre in _NOMBRES.items():
            sesion.add(
                Tecnico(
                    id=uuid.uuid4(),
                    nombre=nombre,
                    especialidad=especialidad.value,
                    turno=turno.value,
                    activo=True,
                )
            )

        sesion.commit()
        registro.info("Plantilla sembrada: %s técnicos.", len(_NOMBRES))


# ---------------------------------------------------------------------------
# Consultas
# ---------------------------------------------------------------------------


def listar_tecnicos(
    sesion: Session,
    especialidad: str | None = None,
    turno: str | None = None,
) -> list[Tecnico]:
    consulta = select(Tecnico)

    if especialidad:
        consulta = consulta.where(Tecnico.especialidad == especialidad)
    if turno:
        consulta = consulta.where(Tecnico.turno == turno)

    return list(sesion.scalars(consulta.order_by(Tecnico.nombre)))


def obtener_tecnico(sesion: Session, tecnico_id: uuid.UUID) -> Tecnico | None:
    return sesion.get(Tecnico, tecnico_id)


def listar_asignaciones(sesion: Session) -> list[Asignacion]:
    return list(
        sesion.scalars(select(Asignacion).order_by(Asignacion.asignada_en.desc()))
    )


def candidatos_activos(sesion: Session) -> list[CandidatoTecnico]:
    """Todos los técnicos activos, con su carga actual.

    Devuelve `CandidatoTecnico` y no entidades del ORM porque quien consume esto
    es asignador.py, que no debe conocer SQLAlchemy.

    El filtrado por especialidad y turno NO se hace aquí a propósito: es parte
    de la regla de negocio y vive en asignador.py, donde se puede probar y donde
    queda registrado por qué se descartó cada candidato.
    """
    carga = (
        select(Asignacion.tecnico_id, func.count().label("abiertas"))
        .group_by(Asignacion.tecnico_id)
        .subquery()
    )

    filas = sesion.execute(
        select(Tecnico, func.coalesce(carga.c.abiertas, 0))
        .outerjoin(carga, carga.c.tecnico_id == Tecnico.id)
        .where(Tecnico.activo.is_(True))
    ).all()

    return [
        CandidatoTecnico(
            id=tecnico.id,
            nombre=tecnico.nombre,
            especialidad=tecnico.especialidad,
            turno=tecnico.turno,
            ordenes_abiertas=int(abiertas),
        )
        for tecnico, abiertas in filas
    ]


def guardar_asignacion(
    sesion: Session,
    orden_id: uuid.UUID,
    tecnico_id: uuid.UUID,
    habitacion_numero: int,
    asignada_en: datetime,
) -> None:
    sesion.add(
        Asignacion(
            orden_id=orden_id,
            tecnico_id=tecnico_id,
            habitacion_numero=habitacion_numero,
            asignada_en=asignada_en,
        )
    )


# ---------------------------------------------------------------------------
# Idempotencia
# ---------------------------------------------------------------------------


def evento_ya_procesado(sesion: Session, evento_id: uuid.UUID) -> bool:
    return sesion.get(EventoProcesado, evento_id) is not None


def registrar_evento(sesion: Session, evento_id: uuid.UUID, tipo_evento: str) -> None:
    """Deja constancia de que el evento se manejó.

    Se guarda en la MISMA transacción que el efecto (la asignación); si se
    guardaran por separado, un fallo entre ambos reabriría la ventana del
    duplicado.
    """
    sesion.add(
        EventoProcesado(
            evento_id=evento_id,
            tipo_evento=tipo_evento,
            procesado_en=datetime.now(timezone.utc),
        )
    )


def base_responde() -> bool:
    """Para GET /salud."""
    try:
        with FabricaSesiones() as sesion:
            sesion.execute(select(1))
        return True
    except Exception:
        registro.exception("La base no responde.")
        return False
