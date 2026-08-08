// PROPÓSITO: exponer por HTTP lo que el servicio habitaciones sabe hacer.
//   Es la frontera pública del servicio: lo que no esté aquí, no existe para el
//   resto del sistema.
//
// DEBE CONTENER:
//   1. Un método de extensión MapHabitacionesEndpoints(this WebApplication app)
//      que agrupe todas las rutas bajo /habitaciones.
//   2. GET /habitaciones — lista con filtro opcional por estado y por piso.
//      Lo consume el componente Habitaciones de la UI.
//   3. GET /habitaciones/{numero} — detalle de una habitación.
//   4. PUT /habitaciones/{numero}/fuera-de-servicio — ES EL ENDPOINT CRÍTICO:
//      lo llama el servicio ordenes de forma sincrónica. Recibe el ordenId,
//      debe ser IDEMPOTENTE (llamarlo dos veces con el mismo ordenId deja el
//      mismo resultado) y debe responder rápido.
//   5. PUT /habitaciones/{numero}/disponible — libera la habitación al
//      resolverse la orden. También idempotente.
//   6. Los DTOs de petición y respuesta de estos endpoints, en camelCase.
//   7. Los códigos de respuesta bien elegidos: 404 si la habitación no existe,
//      409 si la transición no es válida, 200/204 si ya estaba en ese estado.
//   8. GET /salud — estado del servicio y de su base de datos, para PanelSalud.
//
// NO DEBE CONTENER:
//   - Consultas escritas directamente contra el DbContext con lógica compleja;
//     las reglas de transición viven en Modelos/Habitacion.cs.
//   - Ningún endpoint sobre órdenes o técnicos; este servicio no los conoce.
//   - Llamadas salientes a otros servicios ni publicación de eventos.
//   - Autenticación; en la versión 1 el gateway es la única frontera de entrada.
//
// RELACIONADO:
//   - Consumido por servicios/ordenes/Clientes/HabitacionesClient.cs
//   - Enrutado por servicios/gateway/appsettings.json
//   - Consumido por servicios/ui/src/componentes/Habitaciones.jsx
//   - Modelos/Habitacion.cs (reglas que estos endpoints invocan)
