// PROPÓSITO: escuchar el evento orden.asignada que produce el servicio tecnicos
//   y mover la orden de ABIERTA a ASIGNADA. Es el único punto por el que una
//   orden recibe técnico; no hay endpoint HTTP equivalente.
//
// DEBE CONTENER:
//   1. Una clase que herede de BackgroundService y corra mientras vive el
//      servicio.
//   2. Declaración de la cola "ordenes.orden-asignada", durable, con binding al
//      exchange "hotel.eventos" y routing key "orden.asignada".
//   3. Deserialización del mensaje según contratos/orden.asignada.v1.json,
//      tolerando campos nuevos desconocidos sin romperse.
//   4. Comprobación de IDEMPOTENCIA por eventoId antes de aplicar el cambio: si
//      ya se procesó, se confirma el mensaje y no se hace nada más.
//   5. Copia del tecnicoId y del nombre del técnico dentro de la orden, para que
//      la UI no tenga que preguntarle a tecnicos.
//   6. Confirmación manual del mensaje (ack) SOLO después de guardar en base.
//   7. Qué hacer si el mensaje falla: reintento acotado y luego descarte con
//      log, para no bloquear la cola con un mensaje envenenado.
//   8. Qué hacer si llega una asignación para una orden que ya está RESUELTA o
//      que no existe.
//
// NO DEBE CONTENER:
//   - La regla de elección del técnico; eso ya lo decidió tecnicos/asignador.py.
//   - Publicación de eventos; para eso está Eventos/PublicadorEventos.cs.
//   - Llamadas a habitaciones: asignar un técnico no cambia el estado del cuarto.
//   - Envío de avisos; de eso se encarga el servicio notificaciones.
//
// RELACIONADO:
//   - contratos/orden.asignada.v1.json
//   - Productor: servicios/tecnicos/consumidor.py y asignador.py
//   - Modelos/Orden.cs (método de transición a ASIGNADA)
//   - Datos/OrdenesDbContext.cs (registro de eventos ya procesados)
