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

using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks();

// Para consultar el /salud de cada servicio en el endpoint agregado.
builder.Services.AddHttpClient("salud", cliente =>
{
    // Corto a propósito: /salud no debe quedarse colgado esperando a un
    // servicio caído; que no responda en 3s ya es la respuesta.
    cliente.Timeout = TimeSpan.FromSeconds(3);
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors();

// Logging de cada petición reenviada: sin esto, depurar el sistema
// distribuido es a ciegas. Se registra después de que YARP eligió destino.
app.Use(async (contexto, siguiente) =>
{
    await siguiente();

    var reenvio = contexto.Features.Get<IReverseProxyFeature>();
    var destino = reenvio?.ProxiedDestination?.Model?.Config?.Address;

    if (destino is not null)
    {
        app.Logger.LogInformation(
            "{Metodo} {Ruta} -> {Destino} : {Codigo}",
            contexto.Request.Method,
            contexto.Request.Path,
            destino,
            contexto.Response.StatusCode);
    }
});

// Liveness del gateway en sí: responde mientras el proceso esté vivo.
app.MapHealthChecks("/health");

// Endpoint AGREGADO que consume PanelSalud: consulta el /salud de los cuatro
// servicios y devuelve un resumen.
//
// Las direcciones y la ruta de sondeo salen de la sección ReverseProxy, nunca
// escritas a mano: el gateway ya declara ahí el mapa del sistema.
//
// Devuelve 200 aunque algún servicio esté caído. Un servicio abajo se REPORTA,
// pero no impide que el gateway responda (servicios/gateway/README.md).
app.MapGet("/salud", async (
    IHttpClientFactory fabrica,
    IConfiguration configuracion,
    CancellationToken ct) =>
{
    var clusteres = configuracion.GetSection("ReverseProxy:Clusters").GetChildren();
    var cliente = fabrica.CreateClient("salud");

    var consultas = clusteres.Select(async cluster =>
    {
        var direccion = cluster.GetSection("Destinations").GetChildren()
            .FirstOrDefault()?["Address"];
        var ruta = cluster["HealthCheck:Active:Path"] ?? "/salud";

        if (string.IsNullOrWhiteSpace(direccion))
        {
            return new { nombre = cluster.Key, estado = "desconocido", detalle = "sin destino configurado" };
        }

        try
        {
            using var respuesta = await cliente.GetAsync(
                $"{direccion.TrimEnd('/')}{ruta}", ct);

            return new
            {
                nombre = cluster.Key,
                estado = respuesta.IsSuccessStatusCode ? "sano" : "caido",
                detalle = $"HTTP {(int)respuesta.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new { nombre = cluster.Key, estado = "caido", detalle = ex.GetType().Name };
        }
    });

    var servicios = await Task.WhenAll(consultas);

    return Results.Ok(new
    {
        estado = servicios.All(s => s.estado == "sano") ? "sano" : "degradado",
        servicios
    });
});

app.MapReverseProxy();

app.Run();

