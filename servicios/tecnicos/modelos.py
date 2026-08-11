"""Formas de datos del servicio tecnicos.

Dos familias que no hay que mezclar:

  - Entidades SQLAlchemy: lo que se guarda en PostgreSQL.
  - Esquemas Pydantic: los eventos que entran y salen, y las respuestas HTTP.

Este archivo es el que garantiza que Python y C# se entiendan: los eventos que
salen se serializan en camelCase mediante alias, porque el consumidor del otro
lado es C# y espera camelCase, no snake_case.
"""

from __future__ import annotations

import uuid
from datetime import datetime
from enum import Enum

from pydantic import BaseModel, ConfigDict, Field
from sqlalchemy import Boolean, DateTime, ForeignKey, Index, Integer, String
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column


# ---------------------------------------------------------------------------
# Enumeraciones del dominio
# ---------------------------------------------------------------------------


class Especialidad(str, Enum):
    """Especialidades del personal de mantenimiento.

    Los valores coinciden EXACTAMENTE con el campo tipoFalla de
    contratos/orden.creada.v1.json. Esa coincidencia es lo que permite mapear
    falla a especialidad sin una tabla de traducción; si alguno se renombra,
    hay que subir el contrato a v2.
    """

    AIRE_ACONDICIONADO = "AIRE_ACONDICIONADO"
    PLOMERIA = "PLOMERIA"
    CERRADURA = "CERRADURA"
    ELECTRICIDAD = "ELECTRICIDAD"


class Turno(str, Enum):
    """Turnos del hotel.

    El identificador de Python va sin eñe para que sea ASCII, pero el VALOR
    guardado y mostrado es el del dominio: MAÑANA.
    """

    MANANA = "MAÑANA"
    TARDE = "TARDE"
    NOCHE = "NOCHE"


# ---------------------------------------------------------------------------
# Entidades persistidas
# ---------------------------------------------------------------------------


class Base(DeclarativeBase):
    pass


class Tecnico(Base):
    __tablename__ = "tecnicos"

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    nombre: Mapped[str] = mapped_column(String(120), nullable=False)

    # Se guardan como texto para que la base sea legible a simple vista, igual
    # que en los servicios C#.
    especialidad: Mapped[str] = mapped_column(String(40), nullable=False)
    turno: Mapped[str] = mapped_column(String(10), nullable=False)

    activo: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)

    # La búsqueda de candidatos filtra siempre por estas tres columnas juntas.
    __table_args__ = (
        Index("ix_tecnicos_especialidad_turno_activo", "especialidad", "turno", "activo"),
    )


class Asignacion(Base):
    """Traza de lo que este servicio decidió: qué técnico quedó a cargo de qué orden."""

    __tablename__ = "asignaciones"

    orden_id: Mapped[uuid.UUID] = mapped_column(primary_key=True)
    tecnico_id: Mapped[uuid.UUID] = mapped_column(
        ForeignKey("tecnicos.id"), nullable=False
    )
    habitacion_numero: Mapped[int] = mapped_column(Integer, nullable=False)
    asignada_en: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)

    # Se cuenta la carga de cada técnico por esta columna, así que conviene
    # indexarla.
    __table_args__ = (Index("ix_asignaciones_tecnico", "tecnico_id"),)


class EventoProcesado(Base):
    """Eventos ya consumidos. Es lo que hace IDEMPOTENTE el consumo de orden.creada.

    La clave es eventoId, no ordenId: una misma orden genera varios eventos
    distintos y filtrar por ordenId descartaría mensajes legítimos.
    """

    __tablename__ = "eventos_procesados"

    evento_id: Mapped[uuid.UUID] = mapped_column(primary_key=True)
    tipo_evento: Mapped[str] = mapped_column(String(60), nullable=False)
    procesado_en: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), nullable=False
    )


# ---------------------------------------------------------------------------
# Esquemas de eventos
# ---------------------------------------------------------------------------


def a_camel(texto: str) -> str:
    primera, *resto = texto.split("_")
    return primera + "".join(palabra.capitalize() for palabra in resto)


class EsquemaEvento(BaseModel):
    """Base de los eventos: camelCase en el cable, snake_case adentro."""

    model_config = ConfigDict(
        alias_generator=a_camel,
        populate_by_name=True,
        # Tolerancia a campos desconocidos: un campo nuevo opcional en el
        # contrato NO debe tumbar el consumidor. Es la regla de versionado que
        # fija docs/catalogo-eventos.md.
        extra="ignore",
    )


class EventoOrdenCreada(EsquemaEvento):
    """Entrada: contratos/orden.creada.v1.json.

    Solo se declaran los campos que este servicio usa.
    """

    evento_id: uuid.UUID
    tipo_evento: str = ""
    ocurrido_en: datetime

    orden_id: uuid.UUID
    habitacion_numero: int
    tipo_falla: str
    prioridad: str = "MEDIA"


class EventoOrdenAsignada(EsquemaEvento):
    """Salida: contratos/orden.asignada.v1.json.

    Se serializa SIEMPRE con by_alias=True; si se serializa por nombre de campo
    saldría snake_case y el consumidor C# no entendería nada.
    """

    evento_id: uuid.UUID = Field(default_factory=uuid.uuid4)
    tipo_evento: str = "orden.asignada"
    version: int = 1
    ocurrido_en: datetime

    orden_id: uuid.UUID
    tecnico_id: uuid.UUID
    tecnico_nombre: str
    especialidad: str


# ---------------------------------------------------------------------------
# Respuestas HTTP
# ---------------------------------------------------------------------------


class TecnicoRespuesta(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    nombre: str
    especialidad: str
    turno: str
    activo: bool


class TecnicoNuevo(BaseModel):
    """Alta de un técnico.

    `especialidad` y `turno` se declaran con los enums del dominio, así que un
    valor fuera del catálogo lo rechaza pydantic antes de tocar la base. Sin
    eso se podría dar de alta a alguien con una especialidad que ninguna falla
    va a pedir nunca.
    """

    nombre: str = Field(min_length=1, max_length=120)
    especialidad: Especialidad
    turno: Turno
    activo: bool = True


class TecnicoCambio(BaseModel):
    """Edición. Van todos los campos: es un reemplazo, no un parche."""

    nombre: str = Field(min_length=1, max_length=120)
    especialidad: Especialidad
    turno: Turno
    activo: bool


class AsignacionRespuesta(BaseModel):
    model_config = ConfigDict(
        from_attributes=True, alias_generator=a_camel, populate_by_name=True
    )

    orden_id: uuid.UUID
    tecnico_id: uuid.UUID
    habitacion_numero: int
    asignada_en: datetime
