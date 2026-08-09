"""LA REGLA DE NEGOCIO de este servicio: dado el tipo de falla de una orden,
decidir qué técnico se encarga.

Está aislado de la cola y de la base a propósito: `elegir_tecnico` recibe los
candidatos como parámetro y no importa nada de SQLAlchemy ni de aio-pika, así
que se puede probar sin levantar PostgreSQL ni RabbitMQ.

Por qué esta regla vive aquí y no en el servicio ordenes: depende de
especialidad y turno, datos que solo este servicio posee
(docs/adr/002-limites-contextos.md).
"""

from __future__ import annotations

import logging
import uuid
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone

from modelos import Especialidad, Turno

registro = logging.getLogger(__name__)


@dataclass(frozen=True)
class CandidatoTecnico:
    """Un técnico como lo ve la regla de asignación: sin ORM de por medio."""

    id: uuid.UUID
    nombre: str
    especialidad: str
    turno: str
    ordenes_abiertas: int


@dataclass(frozen=True)
class Decision:
    """Resultado de la decisión.

    El caso "no hay técnico" se devuelve de forma EXPLÍCITA, no como excepción
    ni como valor vacío ambiguo: `tecnico is None` con un `motivo` que explica
    por qué. El llamador tiene que mirarlo, no adivinarlo.
    """

    tecnico: CandidatoTecnico | None
    motivo: str
    descartados: tuple[tuple[str, str], ...] = ()

    @property
    def hubo_asignacion(self) -> bool:
        return self.tecnico is not None


def especialidad_para(tipo_falla: str) -> Especialidad | None:
    """Traduce el tipo de falla de la orden a la especialidad que la atiende.

    Hoy es una correspondencia uno a uno porque los valores permitidos de
    `tipoFalla` en el contrato son exactamente los de `Especialidad`. Se deja
    como función y no como acceso directo porque es el punto donde habría que
    tocar si algún día una falla la atendieran dos especialidades.
    """
    try:
        return Especialidad(tipo_falla)
    except ValueError:
        return None


def turno_vigente(momento_utc: datetime, offset_horas: int) -> Turno:
    """Turno del hotel en el que cae un instante.

    El evento viaja en UTC; los turnos son horarios LOCALES. Sin la conversión,
    una falla reportada a las 20:00 locales llegaría como 00:00 UTC y se
    asignaría al turno de noche cuando corresponde el de tarde.
    """
    if momento_utc.tzinfo is None:
        momento_utc = momento_utc.replace(tzinfo=timezone.utc)

    hora_local = (momento_utc.astimezone(timezone.utc) + timedelta(hours=offset_horas)).hour

    if 6 <= hora_local < 14:
        return Turno.MANANA
    if 14 <= hora_local < 22:
        return Turno.TARDE
    return Turno.NOCHE


def elegir_tecnico(
    tipo_falla: str,
    momento_utc: datetime,
    candidatos: list[CandidatoTecnico],
    offset_horas: int,
) -> Decision:
    """Decide qué técnico atiende la falla.

    `candidatos` son todos los técnicos activos; el filtrado por especialidad y
    turno se hace aquí para que la regla completa sea visible y probable en un
    solo lugar.
    """
    especialidad = especialidad_para(tipo_falla)

    if especialidad is None:
        # Respuesta a la pregunta 3 del README: un tipoFalla desconocido no es
        # un error transitorio. Reintentarlo daría el mismo resultado siempre.
        motivo = f"El tipo de falla '{tipo_falla}' no corresponde a ninguna especialidad."
        registro.error(motivo)
        return Decision(None, motivo)

    turno = turno_vigente(momento_utc, offset_horas)

    descartados: list[tuple[str, str]] = []
    elegibles: list[CandidatoTecnico] = []

    for candidato in candidatos:
        if candidato.especialidad != especialidad.value:
            descartados.append((candidato.nombre, f"especialidad {candidato.especialidad}"))
        elif candidato.turno != turno.value:
            descartados.append((candidato.nombre, f"turno {candidato.turno}"))
        else:
            elegibles.append(candidato)

    if not elegibles:
        motivo = (
            f"No hay técnico de {especialidad.value} en el turno {turno.value}."
        )
        registro.warning("%s Descartados: %s", motivo, descartados or "ninguno")
        return Decision(None, motivo, tuple(descartados))

    # DESEMPATE: el que tenga menos órdenes abiertas.
    #
    # Se elige repartir carga y no antigüedad ni azar por una razón operativa:
    # es la única de las tres que evita que un técnico acumule la cola mientras
    # otro del mismo turno está libre. La antigüedad concentraría el trabajo en
    # una persona y el azar no garantiza nada a corto plazo.
    #
    # A igualdad de carga se ordena por nombre para que la decisión sea
    # DETERMINISTA: la misma entrada da siempre la misma salida, que es lo que
    # permite probar esta función y reproducir un caso en la defensa.
    elegibles.sort(key=lambda t: (t.ordenes_abiertas, t.nombre))
    elegido = elegibles[0]

    for otro in elegibles[1:]:
        descartados.append((otro.nombre, f"{otro.ordenes_abiertas} órdenes abiertas"))

    motivo = (
        f"{elegido.nombre} atiende {especialidad.value} en turno {turno.value} "
        f"y tiene {elegido.ordenes_abiertas} orden(es) abierta(s)."
    )
    registro.info("Asignado: %s | Descartados: %s", motivo, descartados or "ninguno")

    return Decision(elegido, motivo, tuple(descartados))
