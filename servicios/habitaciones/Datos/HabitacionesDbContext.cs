// PROPÓSITO: única puerta de acceso a la base de datos PostgreSQL del servicio
//   habitaciones. Traduce las entidades del dominio a tablas y concentra toda la
//   configuración de persistencia.
//
// DEBE CONTENER:
//   1. La clase HabitacionesDbContext heredando de DbContext.
//   2. Un DbSet<Habitacion>.
//   3. La configuración del modelo en OnModelCreating: nombre de tabla, clave
//      primaria (número de habitación), conversión del enum a texto, longitudes
//      y campos obligatorios.
//   4. Un índice sobre el estado, porque la UI lista habitaciones filtrando por
//      estado.
//   5. La siembra inicial de las 400 habitaciones (número, piso, tipo) o el
//      método que la ejecuta al arrancar.
//   6. El manejo de concurrencia optimista, para que dos órdenes simultáneas
//      sobre la misma habitación no se pisen.
//
// NO DEBE CONTENER:
//   - Reglas de negocio ni validación de transiciones; eso vive en
//     Modelos/Habitacion.cs.
//   - La cadena de conexión literal; se inyecta desde Program.cs con lo que
//     venga de appsettings.json y de las variables de entorno.
//   - Acceso a tablas de otros servicios; ordenes y tecnicos tienen su propia
//     base y nadie cruza esa frontera (docs/adr/002-limites-contextos.md).
//
// RELACIONADO:
//   - Modelos/Habitacion.cs, Modelos/EstadoHabitacion.cs
//   - Program.cs (aquí se registra el contexto)
//   - docker-compose.yml → servicio db-habitaciones
