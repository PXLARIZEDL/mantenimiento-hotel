// PROPÓSITO: única puerta de acceso a la base PostgreSQL del servicio ordenes.
//   Mapea el agregado Orden a tablas y concentra la configuración de
//   persistencia.
//
// DEBE CONTENER:
//   1. La clase OrdenesDbContext heredando de DbContext.
//   2. Un DbSet<Orden>.
//   3. Configuración en OnModelCreating: tabla, clave primaria, conversión de
//      los enums a texto, longitudes y campos obligatorios.
//   4. Índices sobre estado y sobre número de habitación, porque la UI lista por
//      estado y habitaciones se consulta por número.
//   5. Una tabla o conjunto de eventos ya procesados (por eventoId) para
//      garantizar la IDEMPOTENCIA al consumir orden.asignada dos veces.
//   6. Manejo de concurrencia optimista sobre el estado de la orden.
//
// NO DEBE CONTENER:
//   - Reglas de transición de estado; viven en Modelos/Orden.cs.
//   - Ninguna tabla de habitaciones ni de técnicos; esos datos se piden por HTTP
//     o llegan copiados dentro de los eventos.
//   - La cadena de conexión literal.
//
// RELACIONADO:
//   - Modelos/Orden.cs
//   - Eventos/ConsumidorOrdenAsignada.cs (usa el registro de idempotencia)
//   - docker-compose.yml → servicio db-ordenes
