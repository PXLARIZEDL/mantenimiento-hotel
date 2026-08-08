# PROPÓSITO: escuchar TODOS los eventos del sistema y convertirlos en avisos para
#   recepción. Es el único consumidor que se suscribe con comodín: le interesa
#   todo lo que le pase a una orden.
#
# DEBE CONTENER:
#   1. La conexión al broker leyendo credenciales de variables de entorno, con
#      reintento y espera creciente si RabbitMQ aún no está listo.
#   2. La declaración del exchange "hotel.eventos" (topic, durable) de forma
#      idempotente.
#   3. La declaración de la cola "notificaciones.eventos" con binding a la
#      routing key comodín "orden.*", que cubre los tres eventos de una vez.
#   4. El bucle de consumo en segundo plano, sin bloquear a FastAPI.
#   5. El despacho por tipo de evento: orden.creada, orden.asignada y
#      orden.resuelta, cada uno a su plantilla correspondiente.
#   6. El manejo de un evento DESCONOCIDO que entre por el comodín: registrarlo y
#      confirmarlo, nunca reventar. Es el riesgo que se acepta al usar "orden.*".
#   7. La verificación de idempotencia por eventoId, para no mostrar el mismo
#      aviso dos veces en la bandeja.
#   8. La escritura del aviso en el almacén en memoria de main.py.
#   9. La confirmación manual del mensaje (ack) después de guardarlo.
#
# NO DEBE CONTENER:
#   1. La redacción de los textos; se delega a plantillas.py.
#   2. Publicación de eventos: este servicio solo consume, nunca produce.
#   3. Llamadas HTTP a otros servicios para "completar" datos que falten en el
#      evento; si un dato falta, el que está mal es el contrato.
#   4. Persistencia en base de datos.
#   5. Reglas de negocio sobre órdenes, habitaciones o técnicos.
#
# RELACIONADO:
#   - contratos/orden.creada.v1.json, orden.asignada.v1.json, orden.resuelta.v1.json
#   - docs/catalogo-eventos.md (tabla de colas y bindings)
#   - plantillas.py, main.py (almacén en memoria)
