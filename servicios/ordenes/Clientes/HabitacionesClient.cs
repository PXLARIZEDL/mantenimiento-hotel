// PROPÓSITO: encapsular la ÚNICA llamada sincrónica del sistema
//   (ordenes → habitaciones). Existe para que el resto del servicio no sepa que
//   del otro lado hay una red que puede fallar: aquí se concentran el timeout,
//   el reintento y el circuit breaker exigidos por docs/adr/003.
//
// DEBE CONTENER:
//   1. Una interfaz IHabitacionesClient con dos operaciones:
//      MarcarFueraDeServicioAsync(numero, ordenId) y
//      LiberarAsync(numero, ordenId).
//   2. La implementación como HttpClient TIPADO, con la URL base inyectada
//      desde configuración (nunca escrita a mano).
//   3. La traducción de códigos HTTP a un resultado de dominio propio: éxito,
//      "habitación no existe", "transición inválida", "servicio no disponible".
//      El resto del servicio no debe ver HttpResponseMessage.
//   4. El envío del ordenId en cada llamada, porque el endpoint del otro lado es
//      idempotente y lo necesita para reconocer un reintento.
//   5. Logging de cada intento fallido, indicando si fue timeout, error del
//      servidor o circuito abierto — es lo que se depura en la defensa.
//   6. La distinción entre errores REINTENTABLES (timeout, 5xx, fallo de red) y
//      NO reintentables (400, 404, 409): reintentar un 409 es un bug.
//
// NO DEBE CONTENER:
//   - Las políticas de Polly definidas aquí dentro; se registran sobre este
//     cliente en Program.cs a partir de la configuración. Este archivo define
//     QUÉ se llama; Program.cs define CON QUÉ garantías.
//   - Lógica de negocio de la orden; eso es de Modelos/Orden.cs y Endpoints/.
//   - Llamadas a tecnicos ni a notificaciones: con esos dos NO se habla por
//     HTTP, solo por eventos (docs/adr/003-estrategia-comunicacion.md).
//   - Caché del estado de las habitaciones; ordenes no es dueño de ese dato.
//
// RELACIONADO:
//   - Endpoint destino: PUT /habitaciones/{numero}/fuera-de-servicio y
//     PUT /habitaciones/{numero}/disponible
//   - servicios/habitaciones/Endpoints/HabitacionesEndpoints.cs
//   - docs/adr/003-estrategia-comunicacion.md (valores de timeout, reintentos y
//     umbral del circuit breaker)
//   - appsettings.json → sección Habitaciones (UrlBase y resiliencia)
