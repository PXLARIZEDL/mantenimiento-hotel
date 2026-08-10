"""Pruebas de la regla de asignación.

Estas pruebas NO levantan PostgreSQL ni RabbitMQ, y esa es justamente la razón
de que asignador.py reciba los candidatos como parámetro en vez de buscarlos él
mismo. Si algún día hiciera falta una base para correr esto, sería la señal de
que la separación se rompió.

    pip install -r requirements.txt -r requirements-dev.txt
    pytest
"""

import uuid
from datetime import datetime, timezone

from asignador import CandidatoTecnico, elegir_tecnico

# El hotel está en UTC-4, igual que el valor por defecto de HOTEL_UTC_OFFSET.
OFFSET = -4

# 14:00 UTC son las 10:00 locales -> turno MAÑANA (06:00 a 14:00).
# Elegir una hora que cambie de turno al convertir no es casualidad: si alguien
# borra la conversión de huso, estas pruebas fallan.
MOMENTO_MANANA = datetime(2026, 8, 10, 14, 0, tzinfo=timezone.utc)


def tecnico(nombre, especialidad, turno, ordenes_abiertas=0):
    """Atajo para no repetir el uuid en cada candidato."""
    return CandidatoTecnico(
        id=uuid.uuid4(),
        nombre=nombre,
        especialidad=especialidad,
        turno=turno,
        ordenes_abiertas=ordenes_abiertas,
    )


def test_asigna_tecnico_con_menos_ordenes():
    """El desempate reparte carga: gana quien menos órdenes abiertas tiene."""
    candidatos = [
        tecnico("Juan", "PLOMERIA", "MAÑANA", ordenes_abiertas=3),
        tecnico("Ana", "PLOMERIA", "MAÑANA", ordenes_abiertas=1),
    ]

    decision = elegir_tecnico(
        tipo_falla="PLOMERIA",
        momento_utc=MOMENTO_MANANA,
        candidatos=candidatos,
        offset_horas=OFFSET,
    )

    assert decision.hubo_asignacion is True
    assert decision.tecnico.nombre == "Ana"


def test_descarta_a_quien_no_esta_en_turno():
    """Un técnico del turno de noche no atiende una falla de la mañana."""
    candidatos = [
        tecnico("Nocturno", "PLOMERIA", "NOCHE", ordenes_abiertas=0),
        tecnico("Diurno", "PLOMERIA", "MAÑANA", ordenes_abiertas=9),
    ]

    decision = elegir_tecnico(
        tipo_falla="PLOMERIA",
        momento_utc=MOMENTO_MANANA,
        candidatos=candidatos,
        offset_horas=OFFSET,
    )

    # Gana el de la mañana AUNQUE tenga muchísima más carga: el turno se filtra
    # antes de desempatar, no compite con la carga.
    assert decision.tecnico.nombre == "Diurno"


def test_sin_tecnico_de_esa_especialidad():
    """No se inventa una asignación: se devuelve el caso explícitamente."""
    candidatos = [tecnico("Ana", "PLOMERIA", "MAÑANA")]

    decision = elegir_tecnico(
        tipo_falla="ELECTRICIDAD",
        momento_utc=MOMENTO_MANANA,
        candidatos=candidatos,
        offset_horas=OFFSET,
    )

    assert decision.hubo_asignacion is False
    assert decision.tecnico is None
    # El motivo se registra para poder depurar por qué una orden quedó ABIERTA.
    assert "ELECTRICIDAD" in decision.motivo


def test_tipo_de_falla_desconocido():
    """Un tipoFalla fuera del contrato no revienta: se rechaza con motivo."""
    candidatos = [tecnico("Ana", "PLOMERIA", "MAÑANA")]

    decision = elegir_tecnico(
        tipo_falla="METEORITO",
        momento_utc=MOMENTO_MANANA,
        candidatos=candidatos,
        offset_horas=OFFSET,
    )

    assert decision.hubo_asignacion is False
    assert "METEORITO" in decision.motivo


def test_el_desempate_es_determinista():
    """A igualdad de carga gana el primero por nombre, siempre el mismo.

    Si el desempate fuera al azar, este test no se podria escribir.
    """
    candidatos = [
        tecnico("Zoe", "CERRADURA", "MAÑANA", ordenes_abiertas=2),
        tecnico("Abel", "CERRADURA", "MAÑANA", ordenes_abiertas=2),
    ]

    elegidos = {
        elegir_tecnico(
            tipo_falla="CERRADURA",
            momento_utc=MOMENTO_MANANA,
            candidatos=list(candidatos),
            offset_horas=OFFSET,
        ).tecnico.nombre
        for _ in range(5)
    }

    assert elegidos == {"Abel"}


def test_sin_candidatos():
    """La plantilla vacía es un caso normal, no un error."""
    decision = elegir_tecnico(
        tipo_falla="PLOMERIA",
        momento_utc=MOMENTO_MANANA,
        candidatos=[],
        offset_horas=OFFSET,
    )

    assert decision.hubo_asignacion is False
