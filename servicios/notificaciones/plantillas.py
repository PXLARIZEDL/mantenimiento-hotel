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
