// PROPÓSITO: exponer por HTTP el ciclo de vida de las órdenes. Aquí vive la
//   ORQUESTACIÓN del caso de uso principal: reportar una falla, bloquear la
//   habitación y publicar el evento que dispara todo lo demás.
//
// DEBE CONTENER:
//   1. Un método de extensión MapOrdenesEndpoints(this WebApplication app) que
//      agrupe las rutas bajo /ordenes.
//   2. POST /ordenes — el endpoint más importante del sistema. Su secuencia:
//        a. validar la petición (habitación 1..400, tipo de falla conocido);
//        b. llamar a HabitacionesClient para dejar la habitación
//           FUERA_DE_SERVICIO — si esto falla, la orden NO se crea;
//        c. persistir la orden en estado ABIERTA;
//        d. publicar el evento orden.creada.
//      Debe quedar escrito qué se hace si (c) o (d) fallan después de (b).
//   3. GET /ordenes — listado con filtro por estado y por habitación, para el
//      componente ListaOrdenes de la UI.
//   4. GET /ordenes/{id} — detalle de una orden.
//   5. PUT /ordenes/{id}/resolver — pasa a RESUELTA, pide a habitaciones que
//      libere la habitación y publica orden.resuelta.
//   6. Los DTOs de petición y respuesta en camelCase.
//   7. Códigos de respuesta bien elegidos: 400 por datos inválidos, 404 si la
//      orden no existe, 409 si el estado no admite la transición, y un 503 con
//      mensaje claro cuando el circuito hacia habitaciones esté abierto.
//   8. GET /salud para el PanelSalud.
//
// NO DEBE CONTENER:
//   - La elección del técnico; el servicio tecnicos la hace al consumir el
//     evento orden.creada.
//   - Un endpoint para asignar técnico manualmente desde aquí; la asignación
//     entra por el consumidor de orden.asignada.
//   - Las políticas de reintento y circuit breaker; están configuradas sobre el
//     cliente en Program.cs, no repartidas por los handlers.
//   - Acceso directo a la base de habitaciones.
//
// RELACIONADO:
//   - Clientes/HabitacionesClient.cs (paso b)
//   - Eventos/PublicadorEventos.cs (pasos d y 5)
//   - contratos/orden.creada.v1.json, contratos/orden.resuelta.v1.json
//   - servicios/ui/src/componentes/NuevaOrden.jsx y ListaOrdenes.jsx
