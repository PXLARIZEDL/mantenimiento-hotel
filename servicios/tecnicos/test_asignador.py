import uuid
from datetime import datetime, timezone
from asignador import CandidatoTecnico, elegir_tecnico

def test_asigna_tecnico_con_menos_ordenes():
    candidatos = [
        CandidatoTecnico(id=uuid.uuid4(), nombre="Juan", especialidad="PLOMERIA", turno="MAÑANA", ordenes_abiertas=3),
        CandidatoTecnico(id=uuid.uuid4(), nombre="Ana", especialidad="PLOMERIA", turno="MAÑANA", ordenes_abiertas=1),
    ]
    momento_utc = datetime(2026, 8, 10, 14, 0, tzinfo=timezone.utc)
    
    decision = elegir_tecnico(tipo_falla="PLOMERIA", momento_utc=momento_utc, candidatos=candidatos, offset_horas=-4)
    
    assert decision.hubo_asignacion is True
    assert decision.tecnico.nombre == "Ana"