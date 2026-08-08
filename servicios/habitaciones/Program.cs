// PROPÓSITO: punto de entrada del servicio habitaciones. Arma la aplicación
//   mínima de ASP.NET, registra dependencias y engancha los endpoints. Es el
//   único archivo que conoce el "cableado" completo del servicio.
//
// DEBE CONTENER:
//   1. Creación del WebApplicationBuilder.
//   2. Registro del DbContext contra PostgreSQL leyendo la cadena de conexión
//      desde configuración (appsettings.json + variables de entorno).
//   3. Registro del health check del servicio y de la base de datos.
//   4. Registro de la serialización JSON en camelCase (convención del proyecto).
//   5. Llamada al método de extensión que mapea los endpoints
//      (Endpoints/HabitacionesEndpoints.cs), sin declarar rutas aquí.
//   6. Aplicación de migraciones o siembra inicial de las 400 habitaciones al
//      arrancar, delegada a Datos/, no escrita en línea aquí.
//   7. app.Run().
//
// NO DEBE CONTENER:
//   - Definiciones de rutas ni handlers; van en Endpoints/.
//   - Consultas a la base de datos ni lógica de estados; van en Datos/ y Modelos/.
//   - Cliente HTTP hacia otros servicios: habitaciones NO llama a nadie, solo
//     responde. Los clientes salientes viven en ordenes/Clientes/.
//   - Publicación ni consumo de eventos de RabbitMQ; este servicio no habla con
//     la cola en la versión 1.
//
// RELACIONADO:
//   - Endpoints/HabitacionesEndpoints.cs (rutas que se mapean aquí)
//   - Datos/HabitacionesDbContext.cs (contexto que se registra aquí)
//   - appsettings.json (cadena de conexión y logging)
//   - Endpoint consumido por ordenes: PUT /habitaciones/{numero}/fuera-de-servicio
