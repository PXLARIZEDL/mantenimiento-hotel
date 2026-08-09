# servicios/notificaciones/plantillas.py

# PROPÓSITO: traducir un evento técnico en un mensaje que una persona de
#   recepción entienda. Está aislado del consumidor para poder cambiar la
#   redacción sin tocar nada de RabbitMQ, y para poder probarlo con solo pasarle
#   un diccionario.
#
# DEBE CONTENER:
#   1. Una función por tipo de evento, cada una devolviendo un aviso ya armado:
#        - orden.creada   → "Habitación 314 fuera de servicio: falla de aire
#                            reportada por recepción."
#        - orden.asignada → "Habitación 314: asignado Luis Ramírez (aire),
#                            turno noche."
#        - orden.resuelta → "Habitación 314 disponible de nuevo. Nota: ..."
#   2. La estructura del aviso: id, tipo de evento, número de habitación,
#      título corto, cuerpo, marca de tiempo, destinatario y si fue leído.
#   3. El uso del campo habitacionLiberada de orden.resuelta para NO decir
#      "habitación disponible" cuando sigue bloqueada por otra orden.
#   4. Un nivel o color por prioridad de la orden, para que la bandeja de la UI
#      distinga lo urgente.
#   5. Un texto por defecto para un evento reconocido pero sin plantilla, en vez
#      de fallar.
#
# NO DEBE CONTENER:
#   1. Lógica de RabbitMQ ni acceso al almacén en memoria.
#   2. Endpoints HTTP.
#   3. Decisiones de negocio: aquí no se decide SI se notifica, solo CÓMO se
#      redacta. El "si" lo decide consumidor.py.
#   4. Envío real por correo o SMS; ese canal no existe en la versión 1
#      (ver docs/limites-descartados.md, punto 2).
#   5. Traducciones a otros idiomas ni plantillas HTML complejas.
#
# RELACIONADO:
#   - Los tres contratos de contratos/*.json (de ahí salen los campos que se
#     interpolan en cada texto)
#   - consumidor.py (único llamador)
#   - servicios/ui/src/componentes/BandejaNotificaciones.jsx (muestra estos textos)

from typing import Any, Dict

# Destinatario fijo: en la v1 no existe segmentación de destinatarios ni
# canales (ver NO DEBE CONTENER punto 4). Todo aviso es para recepción.
DESTINATARIO_RECEPCION = "recepcion"

# Traducción de la prioridad de la orden (solo presente en orden.creada) a un
# nivel/color que la bandeja de la UI pueda usar para resaltar lo urgente.
_NIVELES_POR_PRIORIDAD = {
    "BAJA": {"nivel": "BAJA", "color": "gris"},
    "MEDIA": {"nivel": "MEDIA", "color": "amarillo"},
    "ALTA": {"nivel": "ALTA", "color": "rojo"},
}

# Nivel por defecto para eventos que no traen prioridad en su contrato
# (orden.asignada, orden.resuelta) y para el texto por defecto. No es una
# prioridad de negocio: es solo el color neutro con el que la UI los pinta.
_NIVEL_INFORMATIVO = {"nivel": "INFORMATIVO", "color": "azul"}

_ESPECIALIDADES_LEGIBLES = {
    "AIRE": "aire",
    "PLOMERIA": "plomería",
    "CERRADURA": "cerradura",
}

_TURNOS_LEGIBLES = {
    "MAÑANA": "mañana",
    "TARDE": "tarde",
    "NOCHE": "noche",
}


def _construir_aviso(
    *,
    aviso_id: str,
    tipo_evento: str,
    numero_habitacion: int,
    titulo: str,
    cuerpo: str,
    marca_de_tiempo: str,
    nivel_info: Dict[str, str],
) -> Dict[str, Any]:
    """Arma la estructura común de un aviso. Uso interno de este archivo."""
    return {
        "id": aviso_id,
        "tipoEvento": tipo_evento,
        "numeroHabitacion": numero_habitacion,
        "titulo": titulo,
        "cuerpo": cuerpo,
        "marcaDeTiempo": marca_de_tiempo,
        "destinatario": DESTINATARIO_RECEPCION,
        "leido": False,
        "nivel": nivel_info["nivel"],
        "color": nivel_info["color"],
    }


def aviso_orden_creada(evento: Dict[str, Any]) -> Dict[str, Any]:
    """Construye el aviso para el evento orden.creada."""
    numero_habitacion = evento["numeroHabitacion"]
    tipo_falla = evento["tipoFalla"]
    descripcion = evento["descripcion"]
    prioridad = evento["prioridad"]

    falla_legible = _ESPECIALIDADES_LEGIBLES.get(tipo_falla, tipo_falla.lower())
    nivel_info = _NIVELES_POR_PRIORIDAD.get(prioridad, _NIVEL_INFORMATIVO)

    titulo = f"Habitación {numero_habitacion} fuera de servicio"
    cuerpo = (
        f"Habitación {numero_habitacion} fuera de servicio: falla de "
        f"{falla_legible} reportada por recepción. {descripcion}"
    )

    return _construir_aviso(
        aviso_id=evento["eventoId"],
        tipo_evento=evento["tipoEvento"],
        numero_habitacion=numero_habitacion,
        titulo=titulo,
        cuerpo=cuerpo,
        marca_de_tiempo=evento["ocurridoEn"],
        nivel_info=nivel_info,
    )


def aviso_orden_asignada(evento: Dict[str, Any]) -> Dict[str, Any]:
    """Construye el aviso para el evento orden.asignada."""
    numero_habitacion = evento["numeroHabitacion"]
    nombre_tecnico = evento["nombreTecnico"]
    especialidad = evento["especialidad"]
    turno = evento["turno"]

    especialidad_legible = _ESPECIALIDADES_LEGIBLES.get(
        especialidad, especialidad.lower()
    )
    turno_legible = _TURNOS_LEGIBLES.get(turno, turno.lower())

    titulo = f"Habitación {numero_habitacion}: técnico asignado"
    cuerpo = (
        f"Habitación {numero_habitacion}: asignado {nombre_tecnico} "
        f"({especialidad_legible}), turno {turno_legible}."
    )

    return _construir_aviso(
        aviso_id=evento["eventoId"],
        tipo_evento=evento["tipoEvento"],
        numero_habitacion=numero_habitacion,
        titulo=titulo,
        cuerpo=cuerpo,
        marca_de_tiempo=evento["ocurridoEn"],
        nivel_info=_NIVEL_INFORMATIVO,
    )


def aviso_orden_resuelta(evento: Dict[str, Any]) -> Dict[str, Any]:
    """Construye el aviso para el evento orden.resuelta.

    Respeta habitacionLiberada: si es False, la habitación sigue bloqueada
    por otra orden y el aviso NO debe decir que está disponible.
    """
    numero_habitacion = evento["numeroHabitacion"]
    nota_cierre = evento["notaCierre"]
    habitacion_liberada = evento["habitacionLiberada"]

    titulo = f"Habitación {numero_habitacion}: orden resuelta"

    if habitacion_liberada:
        cuerpo = (
            f"Habitación {numero_habitacion} disponible de nuevo. "
            f"Nota: {nota_cierre}"
        )
    else:
        cuerpo = (
            f"Habitación {numero_habitacion}: esta orden fue resuelta, pero "
            f"la habitación sigue fuera de servicio por otra orden abierta. "
            f"Nota: {nota_cierre}"
        )

    return _construir_aviso(
        aviso_id=evento["eventoId"],
        tipo_evento=evento["tipoEvento"],
        numero_habitacion=numero_habitacion,
        titulo=titulo,
        cuerpo=cuerpo,
        marca_de_tiempo=evento["ocurridoEn"],
        nivel_info=_NIVEL_INFORMATIVO,
    )


def aviso_por_defecto(evento: Dict[str, Any]) -> Dict[str, Any]:
    """Aviso genérico para un tipoEvento reconocido pero sin plantilla propia.

    No debe fallar nunca por campos ausentes: usa .get() con valores neutros.
    """
    numero_habitacion = evento.get("numeroHabitacion", 0)
    tipo_evento = evento.get("tipoEvento", "desconocido")

    titulo = f"Habitación {numero_habitacion}: actualización"
    cuerpo = f"Se recibió el evento '{tipo_evento}' para esta habitación."

    return _construir_aviso(
        aviso_id=evento.get("eventoId", "sin-id"),
        tipo_evento=tipo_evento,
        numero_habitacion=numero_habitacion,
        titulo=titulo,
        cuerpo=cuerpo,
        marca_de_tiempo=evento.get("ocurridoEn", ""),
        nivel_info=_NIVEL_INFORMATIVO,
    )


# Mapa de despacho por tipoEvento. consumidor.py puede usar esto para elegir
# la plantilla correcta sin un if/elif propio; si el tipo no está aquí, debe
# usar aviso_por_defecto.
PLANTILLAS_POR_TIPO_EVENTO = {
    "orden.creada": aviso_orden_creada,
    "orden.asignada": aviso_orden_asignada,
    "orden.resuelta": aviso_orden_resuelta,
}