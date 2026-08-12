using Microsoft.EntityFrameworkCore;
using Habitaciones.Datos;
using Habitaciones.Modelos;

namespace Habitaciones.Endpoints;

/// <summary>Cuerpo de los dos PUT que llama ordenes.</summary>
/// <remarks>
/// El <c>ordenId</c> es obligatorio y es lo que hace idempotentes a estos
/// endpoints: permite reconocer un reintento como la MISMA operación.
/// </remarks>
public sealed record TransicionPeticion(Guid OrdenId);

/// <summary>
/// Respuesta de una habitación. El campo <c>id</c> es el que ordenes guarda
/// como <c>habitacionId</c> para publicarlo en los eventos — quitarlo rompe
/// contratos/orden.creada.v1.json.
/// </summary>
public sealed record HabitacionRespuesta(
    Guid Id,
    int Numero,
    int Piso,
    TipoHabitacion Tipo,
    EstadoHabitacion Estado,
    DateTimeOffset ActualizadaEn,
    IReadOnlyCollection<Guid> OrdenesActivas)
{
    public static HabitacionRespuesta De(Habitacion h) =>
        new(h.Id, h.Numero, h.Piso, h.Tipo, h.Estado, h.ActualizadaEn, h.OrdenesActivas);
}


public static class HabitacionesEndpoints
{
    public static WebApplication MapHabitacionesEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/habitaciones").WithTags("Habitaciones");

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarHabitaciones")
            .WithSummary("Lista habitaciones con filtro opcional por estado y por piso.");

        grupo.MapGet("/{numero:int}", ObtenerAsync)
            .WithName("ObtenerHabitacion");

        grupo.MapPut("/{numero:int}/fuera-de-servicio", BloquearAsync)
            .WithName("BloquearHabitacion")
            .WithSummary("Bloquea el cuarto por una orden. Idempotente: lo llama ordenes y lo reintenta.");

        grupo.MapPut("/{numero:int}/disponible", LiberarAsync)
            .WithName("LiberarHabitacion")
            .WithSummary("Cierra una orden sobre el cuarto y lo libera si no queda ninguna. Idempotente.");

        return app;
    }

    private static async Task<IResult> ListarAsync(
        HabitacionesDbContext bd,
        CancellationToken ct,
        EstadoHabitacion? estado = null,
        int? piso = null)
    {
        var consulta = bd.Habitaciones.AsNoTracking().AsQueryable();

        if (estado is { } filtroEstado)
        {
            consulta = consulta.Where(h => h.Estado == filtroEstado);
        }

        if (piso is { } filtroPiso)
        {
            consulta = consulta.Where(h => h.Piso == filtroPiso);
        }

        var habitaciones = await consulta
            .OrderBy(h => h.Numero)
            .Select(h => HabitacionRespuesta.De(h))
            .ToListAsync(ct);

        return Results.Ok(habitaciones);
    }
    .
    private static async Task<IResult> ObtenerAsync(
        int numero, HabitacionesDbContext bd, CancellationToken ct)
    {
        var habitacion = await bd.Habitaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Numero == numero, ct);

        return habitacion is null
            ? Results.NotFound(new { mensaje = $"La habitación {numero} no existe." })
            : Results.Ok(HabitacionRespuesta.De(habitacion));
    }

    /// <summary>
    /// EL ENDPOINT CRÍTICO: lo llama ordenes de forma sincrónica antes de crear
    /// la orden. Debe responder rápido y ser idempotente.
    /// </summary>
    private static Task<IResult> BloquearAsync(
        int numero,
        TransicionPeticion peticion,
        HabitacionesDbContext bd,
        ILoggerFactory registros,
        CancellationToken ct) =>
        AplicarTransicionAsync(
            numero, peticion, bd, registros.CreateLogger("Habitaciones.Bloquear"), ct,
            aplicar: (h, ordenId) => h.MarcarFueraDeServicio(ordenId),
            accion: "bloquear");

    private static Task<IResult> LiberarAsync(
        int numero,
        TransicionPeticion peticion,
        HabitacionesDbContext bd,
        ILoggerFactory registros,
        CancellationToken ct) =>
        AplicarTransicionAsync(
            numero, peticion, bd, registros.CreateLogger("Habitaciones.Liberar"), ct,
            aplicar: (h, ordenId) => h.Liberar(ordenId),
            accion: "liberar");

    /// <summary>
    /// Tronco común de los dos PUT: ambos validan igual, manejan la concurrencia
    /// igual y distinguen "cambió algo" de "ya estaba así" igual. Solo cambia la
    /// transición que aplican.
    /// </summary>
    private static async Task<IResult> AplicarTransicionAsync(
        int numero,
        TransicionPeticion peticion,
        HabitacionesDbContext bd,
        ILogger registro,
        CancellationToken ct,
        Func<Habitacion, Guid, bool> aplicar,
        string accion)
    {
        if (peticion.OrdenId == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ordenId"] = new[] { "El ordenId es obligatorio: es lo que hace idempotente esta llamada." }
            });
        }

        var habitacion = await bd.Habitaciones.FirstOrDefaultAsync(h => h.Numero == numero, ct);

        if (habitacion is null)
        {
            return Results.NotFound(new { mensaje = $"La habitación {numero} no existe." });
        }

        bool cambio;

        try
        {
            cambio = aplicar(habitacion, peticion.OrdenId);
        }
        catch (TransicionInvalidaException ex)
        {
            return Results.Problem(
                title: "Transición inválida",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!cambio)
        {
            // Ya estaba en ese estado por esta misma orden: reintento de ordenes.
            // 200 y no 409 — para un endpoint idempotente esto es éxito, y
            // devolver 409 haría que ordenes tratara su propio reintento como
            // un fallo de negocio.
            registro.LogInformation(
                "Reintento de {Accion} sobre la {Numero} (orden {OrdenId}): sin cambios.",
                accion, numero, peticion.OrdenId);

            return Results.Ok(HabitacionRespuesta.De(habitacion));
        }

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Dos órdenes tocando el mismo cuarto a la vez.
            registro.LogWarning(
                "Conflicto de concurrencia al {Accion} la {Numero} (orden {OrdenId}).",
                accion, numero, peticion.OrdenId);

            return Results.Problem(
                title: "La habitación cambió mientras se actualizaba",
                detail: "Otra orden modificó el cuarto al mismo tiempo. Reintentá.",
                statusCode: StatusCodes.Status409Conflict);
        }

        registro.LogInformation(
            "Habitación {Numero} -> {Estado} (orden {OrdenId}, {Activas} orden(es) activa(s)).",
            numero, habitacion.Estado, peticion.OrdenId, habitacion.OrdenesActivas.Count);

        return Results.Ok(HabitacionRespuesta.De(habitacion));
    }
}
