using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Habitaciones.Datos;
using Habitaciones.Endpoints;

// Punto de entrada del servicio habitaciones. Es el único archivo que ve el
// cableado completo.
//
// Este servicio NO llama a nadie: no hay cliente HTTP saliente ni conexión a
// RabbitMQ. Solo responde.

var constructor = WebApplication.CreateBuilder(args);

// --- 1. Serialización -------------------------------------------------------
// camelCase y enums como texto: convención del proyecto. ordenes lee el campo
// "id" de esta respuesta para guardarlo como habitacionId.
constructor.Services.ConfigureHttpJsonOptions(opciones =>
{
    opciones.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// --- 2. Base de datos propia ------------------------------------------------
var cadenaPostgres = constructor.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Postgres.");

constructor.Services.AddDbContext<HabitacionesDbContext>(opciones =>
    opciones.UseNpgsql(cadenaPostgres, npgsql => npgsql.EnableRetryOnFailure(3)));

// --- 3. Parámetros del inventario -------------------------------------------
var opcionesInventario = constructor.Configuration
    .GetSection(OpcionesInventario.Seccion)
    .Get<OpcionesInventario>() ?? new OpcionesInventario();

constructor.Services.AddSingleton(opcionesInventario);

// --- 4. Health checks -------------------------------------------------------
constructor.Services.AddHealthChecks()
    .AddNpgSql(cadenaPostgres, name: "postgres", tags: new[] { "listo" });

constructor.Services.AddEndpointsApiExplorer();
constructor.Services.AddSwaggerGen();

var app = constructor.Build();

// --- 5. Esquema y siembra ---------------------------------------------------
// La siembra vive en Datos/, no escrita en línea aquí.
using (var ambito = app.Services.CreateScope())
{
    var bd = ambito.ServiceProvider.GetRequiredService<HabitacionesDbContext>();
    await bd.Database.MigrateAsync();

    await SembradorHabitaciones.SembrarAsync(
        bd, opcionesInventario, app.Logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- 6. Rutas ---------------------------------------------------------------
app.MapHabitacionesEndpoints();

// GET /salud para el PanelSalud de la UI y para los health checks activos del
// gateway.
app.MapHealthChecks("/salud");

app.Run();
