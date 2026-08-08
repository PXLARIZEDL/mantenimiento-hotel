// PROPÓSITO: punto de entrada del servicio ordenes. Arma la aplicación, registra
//   las dependencias (base de datos, cliente HTTP resiliente hacia habitaciones,
//   conexión a RabbitMQ) y engancha endpoints y consumidores. Es el único
//   archivo que ve el cableado completo del servicio que orquesta el caso de uso.
//
// DEBE CONTENER:
//   1. Creación del WebApplicationBuilder y serialización JSON en camelCase.
//   2. Registro del OrdenesDbContext contra su propia base PostgreSQL.
//   3. Registro del HttpClient tipado HabitacionesClient con su URL base y sus
//      políticas de resiliencia (timeout, reintento, circuit breaker). Los
//      valores salen de configuración, NO se escriben aquí a mano.
//   4. Registro del publicador de eventos de RabbitMQ (Eventos/).
//   5. Registro del consumidor en segundo plano de orden.asignada, como
//      BackgroundService/HostedService.
//   6. Health checks: servicio, base de datos y conexión a RabbitMQ.
//   7. Mapeo de los endpoints mediante el método de extensión de Endpoints/.
//   8. app.Run().
//
// NO DEBE CONTENER:
//   - Las políticas de Polly escritas en línea con números mágicos; se definen a
//     partir de la sección de configuración y se aplican al cliente tipado.
//   - Rutas ni handlers; van en Endpoints/OrdenesEndpoints.cs.
//   - La regla de qué técnico asignar; esa vive en el servicio tecnicos.
//   - Acceso a la base de habitaciones; solo se habla con ella por HTTP.
//
// RELACIONADO:
//   - Clientes/HabitacionesClient.cs (se registra aquí con sus políticas)
//   - Eventos/PublicadorEventos.cs y Eventos/ConsumidorOrdenAsignada.cs
//   - Endpoints/OrdenesEndpoints.cs
//   - docs/adr/003-estrategia-comunicacion.md (decide los valores de resiliencia)
