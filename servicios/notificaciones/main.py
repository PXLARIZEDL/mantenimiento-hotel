# servicios/notificaciones/main.py

# PROPÓSITO: punto de entrada del servicio notificaciones. Crea la aplicación
#   FastAPI, arranca el consumidor de eventos y expone la bandeja de avisos que
#   la UI muestra a recepción. Es el único servicio SIN base de datos: todo vive
#   en memoria.
#
# DEBE CONTENER:
#   1. La instancia de FastAPI con título y versión.
#   2. El almacén EN MEMORIA de avisos: una estructura acotada (por ejemplo, los
#      últimos N avisos) para que el proceso no crezca sin límite. Debe ser
#      segura ante accesos concurrentes, porque escribe el consumidor y lee HTTP.
#   3. El ciclo de vida (lifespan): al arrancar, lanzar el consumidor de
#      RabbitMQ; al apagar, cerrarlo de forma ordenada.
#   4. GET /notificaciones — bandeja completa, más reciente primero, con filtro
#      opcional por tipo de evento y por habitación.
#   5. GET /notificaciones/{id} — detalle de un aviso.
#   6. POST /notificaciones/{id}/leida — marcar como leída desde la UI.
#   7. GET /salud — estado del servicio y de la conexión a RabbitMQ, y cuántos
#      avisos hay en memoria; lo consume el PanelSalud.
#   8. Una advertencia visible en la documentación de la API: los avisos se
#      PIERDEN al reiniciar el contenedor. Es una decisión, no un descuido.
#
# NO DEBE CONTENER:
#   1. Base de datos, ORM ni persistencia en disco; si algún día hace falta, es
#      un cambio de ADR, no un parche aquí.
#   2. El bucle de consumo de mensajes; vive en consumidor.py.
#   3. La redacción de los textos de los avisos; vive en plantillas.py.
#   4. Un endpoint para CREAR un aviso a mano: un aviso solo nace de un evento.
#   5. Llamadas HTTP a ordenes, tecnicos o habitaciones: todo lo que necesita
#      llega dentro del payload del evento.
#   6. Envío real de correo o SMS; en la versión 1 se "envía" guardando en
#      memoria.
#
# RELACIONADO:
#   - consumidor.py (llena el almacén de aquí)
#   - plantillas.py (arma el texto de cada aviso)
#   - servicios/ui/src/componentes/BandejaNotificaciones.jsx (consume estos
#     endpoints a través del gateway)

import asyncio
import logging
import threading
from collections import OrderedDict
from contextlib import asynccontextmanager
from typing import Optional

from fastapi import FastAPI, HTTPException, Query

logger = logging.getLogger("notificaciones.main")

# --- Almacén en memoria ---------------------------------------------------
# OrderedDict indexado por eventoId (== id del aviso, ver plantillas.py).
# Permite: verificación de idempotencia O(1), lookup por id O(1), y recorte
# al máximo de avisos en O(1) amortizado (popitem del más antiguo).
#
# Protegido con threading.Lock (no asyncio.Lock) a propósito: las funciones
# que lo tocan (existe_evento_id, agregar_aviso, marcar_leida) son SÍNCRONAS
# porque consumidor.py las llama sin await. En un único event loop no habría
# problema de por sí, pero el Lock deja la estructura defendida ante
# cualquier forma futura de invocación sin depender de esa suposición.
MAX_AVISOS = 50

_lock = threading.Lock()
_avisos: "OrderedDict[str, dict]" = OrderedDict()


def existe_evento_id(evento_id: str) -> bool:
    """Usado por consumidor.py para la verificación de idempotencia."""
    with _lock:
        return evento_id in _avisos


def agregar_aviso(aviso: dict) -> None:
    """Usado por consumidor.py para guardar un aviso ya construido.

    Recorta al máximo definido, descartando el aviso más antiguo primero.
    """
    with _lock:
        aviso_id = aviso["id"]
        _avisos[aviso_id] = aviso
        _avisos.move_to_end(aviso_id)
        while len(_avisos) > MAX_AVISOS:
            aviso_id_descartado, _ = _avisos.popitem(last=False)
            logger.debug(
                "Aviso %s descartado del almacén en memoria por límite de %s.",
                aviso_id_descartado,
                MAX_AVISOS,
            )


def _listar_avisos() -> list:
    with _lock:
        # copia superficial: evita exponer la estructura interna
        return list(_avisos.values())


def _obtener_aviso(aviso_id: str) -> Optional[dict]:
    with _lock:
        return _avisos.get(aviso_id)


def _marcar_leida(aviso_id: str) -> Optional[dict]:
    with _lock:
        aviso = _avisos.get(aviso_id)
        if aviso is None:
            return None
        aviso["leido"] = True
        return aviso


# --- Ciclo de vida ----------------------------------------------------------
_consumidor_task: Optional[asyncio.Task] = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _consumidor_task

    # Import diferido a propósito: consumidor.py hace
    # "from main import agregar_aviso, existe_evento_id" a nivel de módulo.
    # Si este import estuviera arriba del archivo, se produciría un import
    # circular al arrancar. Para cuando el lifespan corre, este módulo ya
    # está completamente cargado, así que el import de consumidor.py se
    # resuelve sin problema.
    from consumidor import iniciar_consumidor

    _consumidor_task = asyncio.create_task(iniciar_consumidor())
    logger.info("Consumidor de RabbitMQ lanzado en segundo plano.")

    try:
        yield
    finally:
        if _consumidor_task is not None:
            # main.py solo cancela y espera. NO cierra canal ni conexión de
            # RabbitMQ directamente: esos recursos son propiedad de
            # consumidor.py, que ya los cierra dentro de su propio
            # "except asyncio.CancelledError" en iniciar_consumidor(). Cerrar
            # algo aquí que main.py no posee duplicaría esa responsabilidad.
            _consumidor_task.cancel()
            try:
                await _consumidor_task
            except asyncio.CancelledError:
                pass
            except Exception:
                logger.exception(
                    "El consumidor terminó con error durante el apagado."
                )
            logger.info("Consumidor de RabbitMQ detenido.")


app = FastAPI(
    title="Notificaciones",
    version="1.0.0",
    description=(
        "Servicio de notificaciones para recepción del hotel.\n\n"
        "**Aviso importante:** los avisos se almacenan ÚNICAMENTE EN MEMORIA "
        "(máximo " + str(MAX_AVISOS) + " avisos) y se PIERDEN al reiniciar el "
        "contenedor. Esto es una decisión intencional del proyecto, no un "
        "descuido: este es el servicio que se apaga durante la defensa para "
        "demostrar que el resto del sistema sigue funcionando sin él. Los "
        "eventos pendientes quedan acumulados en la cola durable de "
        "RabbitMQ y se procesan solos al volver a levantar el servicio."
    ),
    lifespan=lifespan,
)


# --- Endpoints ---------------------------------------------------------------

@app.get("/notificaciones")
async def listar_notificaciones(
    tipoEvento: Optional[str] = Query(
        None, description="Filtra por tipoEvento exacto (orden.creada, orden.asignada, orden.resuelta)."
    ),
    numeroHabitacion: Optional[int] = Query(
        None, description="Filtra por número de habitación."
    ),
):
    """Bandeja completa de avisos, más reciente primero, con filtros opcionales."""
    avisos = list(reversed(_listar_avisos()))  # más reciente primero

    if tipoEvento is not None:
        avisos = [a for a in avisos if a["tipoEvento"] == tipoEvento]
    if numeroHabitacion is not None:
        avisos = [a for a in avisos if a["numeroHabitacion"] == numeroHabitacion]

    return avisos


@app.get("/notificaciones/{aviso_id}")
async def obtener_notificacion(aviso_id: str):
    """Detalle de un aviso puntual."""
    aviso = _obtener_aviso(aviso_id)
    if aviso is None:
        raise HTTPException(status_code=404, detail="Aviso no encontrado")
    return aviso


@app.post("/notificaciones/{aviso_id}/leida")
async def marcar_notificacion_leida(aviso_id: str):
    """Marca un aviso como leído desde la UI."""
    aviso = _marcar_leida(aviso_id)
    if aviso is None:
        raise HTTPException(status_code=404, detail="Aviso no encontrado")
    return aviso


@app.get("/salud")
async def salud():
    """Estado del servicio para el PanelSalud.

    IMPORTANTE: consumidor.py no expone ningún estado interno de la conexión
    (ni una bandera, ni un evento, ni la conexión misma), solo la tarea de
    fondo que la ejecuta. Por eso "rabbitmq" NO puede afirmar "conectado":
    la tarea permanece viva tanto mientras está conectada y consumiendo
    como mientras está reintentando conectarse (o reconectando tras una
    caída, vía connect_robust). Sin modificar consumidor.py no hay forma
    fiable de distinguir esos dos casos, así que este campo solo reporta si
    el proceso de consumo sigue en ejecución ("activo") o si terminó o fue
    cancelado ("detenido") — no si RabbitMQ está realmente conectado.
    """
    consumidor_vivo = _consumidor_task is not None and not _consumidor_task.done()

    return {
        "estado": "ok",
        "rabbitmq": "activo" if consumidor_vivo else "detenido",
        "avisosEnMemoria": len(_listar_avisos()),
    }