# PROPÓSITO: escuchar el evento orden.creada en RabbitMQ y disparar la asignación
#   automática de un técnico. Es la frontera entre la cola y el dominio: aquí
#   entra un mensaje y sale, si corresponde, el evento orden.asignada.
#
# DEBE CONTENER:
#   1. La conexión al broker leyendo credenciales de variables de entorno, con
#      reintento y espera creciente si RabbitMQ todavía no está listo.
#   2. La declaración del exchange "hotel.eventos" (topic, durable) de forma
#      idempotente, sin asumir que otro servicio ya lo creó.
#   3. La declaración de la cola "tecnicos.orden-creada", durable, con binding a
#      la routing key "orden.creada".
#   4. El bucle de consumo corriendo en segundo plano sin bloquear a FastAPI.
#   5. La deserialización del mensaje según el esquema de entrada de modelos.py.
#   6. La verificación de IDEMPOTENCIA por eventoId ANTES de asignar: recibir el
#      mismo evento dos veces no puede producir dos técnicos asignados.
#   7. La llamada a asignador.py y, si devuelve un técnico, la persistencia de la
#      asignación y la publicación de orden.asignada en camelCase.
#   8. La confirmación manual del mensaje (ack) solo después de guardar.
#   9. Qué hacer cuando NO hay técnico disponible: registrar el caso y decidir si
#      se descarta, se reencola o se pospone (queda documentado en el ADR 003).
#  10. Qué hacer con un mensaje que falla siempre: límite de reintentos y
#      descarte con log, para no atascar la cola.
#
# NO DEBE CONTENER:
#   1. La regla de elección del técnico; solo se invoca a asignador.py.
#   2. Consultas SQL directas; se piden a base_datos.py.
#   3. Consumo de orden.asignada ni de orden.resuelta: este servicio solo
#      escucha orden.creada.
#   4. Envío de avisos a recepción; eso es del servicio notificaciones.
#   5. Llamadas HTTP a habitaciones o a ordenes.
#
# RELACIONADO:
#   - Entrada: contratos/orden.creada.v1.json (productor: servicios/ordenes)
#   - Salida: contratos/orden.asignada.v1.json (lo consumen ordenes y
#     notificaciones)
#   - asignador.py, base_datos.py, modelos.py
#   - docs/catalogo-eventos.md
