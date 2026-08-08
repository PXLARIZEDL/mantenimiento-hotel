// PROPÓSITO: punto de entrada del gateway. Es el ÚNICO puerto abierto hacia
//   afuera: todo lo que entra al sistema pasa por aquí y se reenvía al servicio
//   que corresponda mediante YARP. Existe para que la UI conozca una sola
//   dirección y no la topología interna.
//
// DEBE CONTENER:
//   1. Creación del WebApplicationBuilder.
//   2. Registro del proxy inverso de YARP cargando rutas y clústeres desde la
//      sección ReverseProxy de appsettings.json — la configuración es datos, no
//      código.
//   3. Registro de CORS, para que la UI servida por nginx pueda llamar al
//      gateway desde el navegador.
//   4. Registro de health checks propios y del endpoint agregado /salud que
//      consulta el /salud de los cuatro servicios y devuelve un resumen. Es lo
//      que pinta el PanelSalud de la UI.
//   5. Mapeo del proxy con app.MapReverseProxy().
//   6. Logging de cada petición reenviada: ruta entrante, destino y código de
//      respuesta. Sin esto, depurar el sistema distribuido es a ciegas.
//   7. app.Run().
//
// NO DEBE CONTENER:
//   1. Lógica de negocio de ningún dominio: el gateway no sabe qué es una orden,
//      una habitación ni un técnico. Solo sabe reenviar.
//   2. Acceso a bases de datos.
//   3. Conexión a RabbitMQ: el gateway no publica ni consume eventos.
//   4. Transformación ni agregación de respuestas de varios servicios; si eso
//      hiciera falta, sería un BFF, que se descartó a propósito
//      (docs/limites-descartados.md, punto 5).
//   5. Las URLs de los servicios escritas a mano; van en appsettings.json.
//
// RELACIONADO:
//   - appsettings.json → sección ReverseProxy (rutas y clústeres)
//   - Destinos: habitaciones, ordenes, tecnicos, notificaciones
//   - servicios/ui/src/api.js (todo lo que la UI pide entra por aquí)
//   - servicios/ui/src/componentes/PanelSalud.jsx (consume /salud)
