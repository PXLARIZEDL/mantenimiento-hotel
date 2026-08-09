using System.Net;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ordenes.Clientes;
using Ordenes.Datos;
using Ordenes.Endpoints;
using Ordenes.Eventos;
using Polly;
using Polly.Timeout;

// Punto de entrada del servicio ordenes: el único archivo que ve el cableado
// completo del servicio que orquesta el caso de uso.

var constructor = WebApplication.CreateBuilder(args);

// --- 1. Serialización -------------------------------------------------------
// camelCase y enums como texto: es lo que fijan contratos/*.json y lo que espera
// la UI. Aplica tanto a la API HTTP como a los DTOs que se publican.
constructor.Services.ConfigureHttpJsonOptions(opciones =>
{
    opciones.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// --- 2. Base de datos propia ------------------------------------------------
var cadenaPostgres = constructor.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Postgres.");

constructor.Services.AddDbContext<OrdenesDbContext>(opciones =>
    opciones.UseNpgsql(cadenaPostgres, npgsql => npgsql.EnableRetryOnFailure(3)));

// --- 3. Cliente HTTP resiliente hacia habitaciones --------------------------
var opcionesHabitaciones = constructor.Configuration
    .GetSection(OpcionesHabitaciones.Seccion)
    .Get<OpcionesHabitaciones>() ?? new OpcionesHabitaciones();

constructor.Services.AddSingleton(opcionesHabitaciones);

var r = opcionesHabitaciones.Resiliencia;

constructor.Services
    .AddHttpClient<IHabitacionesClient, HabitacionesClient>(cliente =>
    {
        cliente.BaseAddress = new Uri(opcionesHabitaciones.UrlBase.TrimEnd('/') + "/");
        // Sin timeout del HttpClient: el tiempo lo gobierna la política, que es
        // quien sabe distinguir un intento de la operación completa.
        cliente.Timeout = Timeout.InfiniteTimeSpan;
    })
    .AddResilienceHandler("habitaciones", pipeline =>
    {
        // El ORDEN importa y es la respuesta a la pregunta de ADR 003:
        // el reintento va POR FUERA del circuit breaker. Así cada intento pasa
        // por el breaker y lo alimenta; al revés, el breaker vería un solo
        // "fallo" por cada tanda de reintentos y nunca llegaría a abrirse.
        //
        //   reintento  →  circuit breaker  →  timeout por intento
        pipeline
            .AddRetry(new Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions
            {
                MaxRetryAttempts = r.Reintentos,
                Delay = TimeSpan.FromMilliseconds(r.EsperaBaseMilisegundos),
                BackoffType = DelayBackoffType.Exponential,
                // Jitter: evita que todas las instancias reintenten a la vez y
                // vuelvan a tumbar a habitaciones justo cuando se recupera.
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    // Solo lo REINTENTABLE. Un 404 o un 409 no cambian por
                    // insistir: reintentarlos sería un bug.
                    .HandleResult(respuesta =>
                        (int)respuesta.StatusCode >= 500 ||
                        respuesta.StatusCode == HttpStatusCode.RequestTimeout)
            })
            .AddCircuitBreaker(new Microsoft.Extensions.Http.Resilience.HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = r.UmbralFallosCircuito,
                MinimumThroughput = r.MinimoLlamadasCircuito,
                SamplingDuration = TimeSpan.FromSeconds(r.SegundosVentanaMuestreo),
                BreakDuration = TimeSpan.FromSeconds(r.SegundosCircuitoAbierto),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(respuesta => (int)respuesta.StatusCode >= 500)
            })
            .AddTimeout(TimeSpan.FromSeconds(r.TimeoutSegundos));
    });

// --- 4. RabbitMQ: publicador y consumidor -----------------------------------
var opcionesRabbit = constructor.Configuration
    .GetSection(OpcionesRabbitMq.Seccion)
    .Get<OpcionesRabbitMq>() ?? new OpcionesRabbitMq();

constructor.Services.AddSingleton(opcionesRabbit);
constructor.Services.AddSingleton<IPublicadorEventos, PublicadorEventos>();

// El consumidor de orden.asignada corre en segundo plano mientras viva el
// servicio. Es el único camino por el que una orden recibe técnico.
constructor.Services.AddHostedService<ConsumidorOrdenAsignada>();

// --- 5. Health checks -------------------------------------------------------
constructor.Services.AddHealthChecks()
    .AddNpgSql(cadenaPostgres, name: "postgres", tags: new[] { "listo" })
    .AddRabbitMQ(
        new Uri($"amqp://{opcionesRabbit.Usuario}:{opcionesRabbit.Contrasena}@{opcionesRabbit.Host}:{opcionesRabbit.Puerto}/"),
        name: "rabbitmq",
        tags: new[] { "listo" });

constructor.Services.AddEndpointsApiExplorer();
constructor.Services.AddSwaggerGen();

var app = constructor.Build();

// --- 6. Esquema de la base --------------------------------------------------
// Se aplica al arrancar. El servicio depende de db-ordenes con healthcheck en
// compose, así que la base ya está lista en este punto.
using (var ambito = app.Services.CreateScope())
{
    var bd = ambito.ServiceProvider.GetRequiredService<OrdenesDbContext>();
    await bd.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- 7. Rutas ---------------------------------------------------------------
app.MapOrdenesEndpoints();

// GET /salud para el PanelSalud de la UI.
app.MapHealthChecks("/salud");

app.Run();
