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

# Los valores son los de contratos/orden.creada.v1.json. Si se agrega uno nuevo
# al contrato, cae en el .lower() de más abajo en vez de romper.
_ESPECIALIDADES_LEGIBLES = {
    "AIRE_ACONDICIONADO": "aire acondicionado",
    "PLOMERIA": "plomería",
    "CERRADURA": "cerradura",
    "ELECTRICIDAD": "electricidad",
}

# El turno ya no viaja en orden.asignada, así que no hay nada que traducir:
# _TURNOS_LEGIBLES se eliminó junto con la mención al turno en el aviso.

# Cuando no se conoce el número de habitación. Es la convención que ya usaba
# aviso_por_defecto.
HABITACION_DESCONOCIDA = 0


def _texto_habitacion(numero_habitacion: int) -> str:
    """Cómo nombrar la habitación en el título y el cuerpo del aviso."""
    if numero_habitacion == HABITACION_DESCONOCIDA:
        return "Habitación (sin identificar)"
    return f"Habitación {numero_habitacion}"


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


def aviso_orden_creada(evento: Dict[str, Any], numero_habitacion: int) -> Dict[str, Any]:
    """Construye el aviso para el evento orden.creada."""
    tipo_falla = evento["tipoFalla"]
    descripcion = evento["descripcion"]
    prioridad = evento["prioridad"]

    falla_legible = _ESPECIALIDADES_LEGIBLES.get(tipo_falla, tipo_falla.lower())
    nivel_info = _NIVELES_POR_PRIORIDAD.get(prioridad, _NIVEL_INFORMATIVO)

    habitacion = _texto_habitacion(numero_habitacion)

    titulo = f"{habitacion} fuera de servicio"
    cuerpo = (
        f"{habitacion} fuera de servicio: falla de "
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


def aviso_orden_asignada(evento: Dict[str, Any], numero_habitacion: int) -> Dict[str, Any]:
    """Construye el aviso para el evento orden.asignada.

    El contrato de este evento NO trae el número de habitación ni el turno: solo
    ordenId, tecnicoId, tecnicoNombre y especialidad. El número lo resuelve
    consumidor.py correlacionando por ordenId con el orden.creada previo.
    """
    nombre_tecnico = evento["tecnicoNombre"]
    especialidad = evento["especialidad"]

    especialidad_legible = _ESPECIALIDADES_LEGIBLES.get(
        especialidad, especialidad.lower()
    )

    habitacion = _texto_habitacion(numero_habitacion)

    titulo = f"{habitacion}: técnico asignado"
    cuerpo = (
        f"{habitacion}: asignado {nombre_tecnico} ({especialidad_legible})."
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


def aviso_orden_resuelta(evento: Dict[str, Any], numero_habitacion: int) -> Dict[str, Any]:
    """Construye el aviso para el evento orden.resuelta.

    El diseño original distinguía con `habitacionLiberada` entre "ya está
    disponible" y "sigue bloqueada por otra orden". Ese campo NO está en
    contratos/orden.resuelta.v1.json, así que el aviso no puede afirmar
    disponibilidad: lo único que el evento garantiza es que ESTA orden se cerró.

    Se redacta en consecuencia. Decir "disponible de nuevo" sin poder
    verificarlo haría que recepción ofreciera un cuarto que quizá sigue roto por
    otra falla abierta — justo el error que el campo evitaba.

    Recuperar esa distinción requiere volver a poner el campo en el contrato, y
    eso obliga a una v2 porque v1 ya está publicado. Queda anotado en el README.
    """
    nota_cierre = evento["notaCierre"]

    habitacion = _texto_habitacion(numero_habitacion)

    titulo = f"{habitacion}: orden resuelta"
    cuerpo = (
        f"{habitacion}: se resolvió una orden de mantenimiento. "
        f"Verificá el estado del cuarto antes de asignarlo. Nota: {nota_cierre}"
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


def aviso_por_defecto(
    evento: Dict[str, Any], numero_habitacion: int = HABITACION_DESCONOCIDA
) -> Dict[str, Any]:
    """Aviso genérico para un tipoEvento reconocido pero sin plantilla propia.

    No debe fallar nunca por campos ausentes: usa .get() con valores neutros.
    """
    tipo_evento = evento.get("tipoEvento", "desconocido")

    habitacion = _texto_habitacion(numero_habitacion)

    titulo = f"{habitacion}: actualización"
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