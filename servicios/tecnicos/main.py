"""Punto de entrada del servicio tecnicos.

Crea la aplicación FastAPI, expone la API de CONSULTA y arranca el consumidor de
eventos en segundo plano. Es el único archivo que ve el cableado completo.

No hay ningún endpoint que asigne un técnico: asignar es consecuencia del evento
`orden.creada`, no de una petición del usuario.
"""

from __future__ import annotations

import logging
import uuid
from contextlib import asynccontextmanager
from datetime import datetime, timezone

from fastapi import Depends, FastAPI, HTTPException
from fastapi.responses import JSONResponse
from sqlalchemy.orm import Session

import base_datos
from asignador import turno_vigente
from configuracion import configuracion
from consumidor import consumidor
from modelos import AsignacionRespuesta, TecnicoRespuesta

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)-8s %(name)s | %(message)s",
)

registro = logging.getLogger(__name__)


@asynccontextmanager
async def ciclo_de_vida(_: FastAPI):
    # Al arrancar: esquema, siembra y consumidor.
    base_datos.crear_tablas()
    base_datos.sembrar_tecnicos()
    consumidor.arrancar()

    registro.info("Servicio tecnicos listo.")
    yield

    # Al apagar: cerrar la conexión al broker de forma ordenada.
    await consumidor.detener()


app = FastAPI(
    title="tecnicos",
    version="1.0.0",
    description=(
        "Conoce al personal de mantenimiento y decide quién atiende cada falla. "
        "La asignación se dispara por el evento orden.creada."
    ),
    lifespan=ciclo_de_vida,
)


@app.get("/tecnicos", response_model=list[TecnicoRespuesta])
def listar_tecnicos(
    especialidad: str | None = None,
    turno: str | None = None,
    sesion: Session = Depends(base_datos.obtener_sesion),
):
    return base_datos.listar_tecnicos(sesion, especialidad, turno)


@app.get("/tecnicos/disponibles", response_model=list[TecnicoRespuesta])
def tecnicos_disponibles(sesion: Session = Depends(base_datos.obtener_sesion)):
    """Quiénes están en turno AHORA MISMO.

    Sirve para depurar por qué una orden no se asignó: si esto viene vacío para
    la especialidad que hacía falta, ya está la respuesta.
    """
    turno = turno_vigente(datetime.now(timezone.utc), configuracion.hotel_utc_offset)
    return base_datos.listar_tecnicos(sesion, turno=turno.value)


@app.get("/tecnicos/{tecnico_id}", response_model=TecnicoRespuesta)
def obtener_tecnico(
    tecnico_id: uuid.UUID, sesion: Session = Depends(base_datos.obtener_sesion)
):
    tecnico = base_datos.obtener_tecnico(sesion, tecnico_id)

    if tecnico is None:
        raise HTTPException(status_code=404, detail=f"No existe el técnico {tecnico_id}.")

    return tecnico


@app.get("/asignaciones", response_model=list[AsignacionRespuesta])
def listar_asignaciones(sesion: Session = Depends(base_datos.obtener_sesion)):
    """Qué órdenes se le asignaron a quién. Es la traza de lo que este servicio decidió."""
    return base_datos.listar_asignaciones(sesion)


@app.get("/salud")
def salud():
    """Estado del servicio, de la base y de la conexión a RabbitMQ.

    Lo consultan el PanelSalud de la UI y los health checks activos del gateway,
    que dejan de enrutar tráfico si esto no responde 200.
    """
    base_ok = base_datos.base_responde()
    broker_ok = consumidor.conectado

    detalle = {
        "estado": "sano" if (base_ok and broker_ok) else "degradado",
        "base": "sana" if base_ok else "caida",
        "rabbitmq": "conectado" if broker_ok else "desconectado",
    }

    # 503 si algo esencial falta: el gateway lo usa para sacar este servicio del
    # balanceo en vez de mandarle tráfico que va a fallar.
    return JSONResponse(detalle, status_code=200 if base_ok and broker_ok else 503)
