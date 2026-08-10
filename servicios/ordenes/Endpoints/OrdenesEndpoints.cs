using Microsoft.EntityFrameworkCore;
using Ordenes.Clientes;
using Ordenes.Datos;
using Ordenes.Eventos;
using Ordenes.Modelos;

namespace Ordenes.Endpoints;

// --- DTOs de entrada -------------------------------------------------------
// En camelCase por la política de serialización de Program.cs.

/// <summary>Lo que recepción envía al reportar una falla.</summary>
public sealed record CrearOrdenPeticion(
    int HabitacionNumero,
    TipoFalla TipoFalla,
    string Descripcion,
    Prioridad Prioridad,
    string ReportadoPor);

/// <summary>Lo que el técnico envía al cerrar la orden.</summary>
public sealed record ResolverOrdenPeticion(Guid? ResueltoPor, string? NotaCierre);

// --- DTOs de salida --------------------------------------------------------

public sealed record OrdenRespuesta(
    Guid Id,
    Guid HabitacionId,
    int HabitacionNumero,
    TipoFalla TipoFalla,
    string Descripcion,
    Prioridad Prioridad,
    string ReportadoPor,
    EstadoOrden Estado,
    DateTimeOffset CreadaEn,
    DateTimeOffset? AsignadaEn,
    DateTimeOffset? ResueltaEn,
    Guid? TecnicoId,
    string? TecnicoNombre,
    string? Especialidad,
    Guid? ResueltoPor,
    string? NotaCierre)
{
    public static OrdenRespuesta De(Orden o) => new(
        o.Id, o.HabitacionId, o.HabitacionNumero, o.TipoFalla, o.Descripcion, o.Prioridad,
        o.ReportadoPor, o.Estado, o.CreadaEn, o.AsignadaEn, o.ResueltaEn,
        o.TecnicoId, o.TecnicoNombre, o.Especialidad, o.ResueltoPor, o.NotaCierre);
}

// --- Cuerpos de los eventos publicados -------------------------------------
// Solo los campos de NEGOCIO: el sobre (eventoId, tipoEvento, version,
// ocurridoEn) lo agrega Eventos/PublicadorEventos.cs.

/// <summary>Cuerpo de contratos/orden.creada.v1.json.</summary>
public sealed record EventoOrdenCreada(
    Guid OrdenId,
    Guid HabitacionId,
    int HabitacionNumero,
    TipoFalla TipoFalla,
    string Descripcion,
    Prioridad Prioridad,
    string ReportadoPor);

/// <summary>Cuerpo de contratos/orden.resuelta.v1.json.</summary>
public sealed record EventoOrdenResuelta(
    Guid OrdenId,
    Guid HabitacionId,
    Guid? ResueltoPor,
    string? NotaCierre);

public static class OrdenesEndpoints
{
    public const string EventoCreada = "orden.creada";
    public const string EventoResuelta = "orden.resuelta";

    public static WebApplication MapOrdenesEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/ordenes").WithTags("Ordenes");

        grupo.MapPost("/", CrearAsync)
            .WithName("CrearOrden")
            .WithSummary("Reporta una falla: bloquea la habitación, guarda la orden y publica orden.creada.");

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarOrdenes")
            .WithSummary("Lista órdenes, con filtro opcional por estado y por habitación.");

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .WithName("ObtenerOrden");

        grupo.MapPut("/{id:guid}/resolver", ResolverAsync)
            .WithName("ResolverOrden")
            .WithSummary("Cierra la orden, libera la habitación y publica orden.resuelta.");

        return app;
    }

    /// <summary>
    /// El endpoint más importante del sistema. Secuencia:
    ///   a. validar;
    ///   b. bloquear la habitación por HTTP — si falla, la orden NO se crea;
    ///   c. persistir en ABIERTA;
    ///   d. publicar orden.creada.
    /// </summary>
    private static async Task<IResult> CrearAsync(
        CrearOrdenPeticion peticion,
        OrdenesDbContext bd,
        IHabitacionesClient habitaciones,
        IPublicadorEventos publicador,
        ILoggerFactory registros,
        CancellationToken ct)
    {
        var log = registros.CreateLogger("Ordenes.Crear");

        // --- a. Validación ---------------------------------------------------
        if (peticion.HabitacionNumero is < 1 or > 400)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["habitacionNumero"] = new[] { "El hotel tiene 400 habitaciones (1 a 400)." }
            });
        }

        if (!Enum.IsDefined(peticion.TipoFalla))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["tipoFalla"] = new[] { "Tipo de falla desconocido." }
            });
        }

        if (string.IsNullOrWhiteSpace(peticion.Descripcion))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["descripcion"] = new[] { "La descripción es obligatoria." }
            });
        }

        if (string.IsNullOrWhiteSpace(peticion.ReportadoPor))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["reportadoPor"] = new[] { "Hay que registrar quién reporta la falla." }
            });
        }

        // El ordenId se genera ANTES de llamar a habitaciones: es lo que hace
        // idempotente al endpoint del otro lado si el reintento llega dos veces.
        var ordenId = Guid.NewGuid();

        // --- b. Bloquear la habitación ---------------------------------------
        // Es condición para que la orden exista (docs/adr/003): si no se puede
        // bloquear, no se crea nada.
        var bloqueo = await habitaciones.MarcarFueraDeServicioAsync(
            peticion.HabitacionNumero, ordenId, ct);

        switch (bloqueo.Resultado)
        {
            case ResultadoHabitacion.NoExiste:
                return Results.Problem(
                    title: "La habitación no existe",
                    detail: bloqueo.Detalle,
                    statusCode: StatusCodes.Status404NotFound);

            case ResultadoHabitacion.TransicionInvalida:
                return Results.Problem(
                    title: "La habitación no se puede bloquear",
                    detail: bloqueo.Detalle,
                    statusCode: StatusCodes.Status409Conflict);

            case ResultadoHabitacion.NoDisponible:
                // Respuesta a la pregunta guía del README: con el circuito
                // abierto se devuelve 503, no 500. 503 dice "vuelve a intentar";
                // 500 dice "algo se rompió acá". El cliente puede reintentar.
                return Results.Problem(
                    title: "Servicio de habitaciones no disponible",
                    detail: $"{bloqueo.Detalle} La orden no se creó; vuelve a intentarlo.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (bloqueo.HabitacionId is not { } habitacionId)
        {
            log.LogError(
                "habitaciones confirmó el bloqueo de la {Numero} pero no devolvió su id.",
                peticion.HabitacionNumero);

            return Results.Problem(
                title: "Respuesta inesperada de habitaciones",
                detail: "No se pudo identificar la habitación bloqueada.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        // --- c. Persistir ------------------------------------------------------
        Orden orden;

        try
        {
            // El MISMO ordenId con el que se bloqueó la habitación en el paso b.
            // Si aquí naciera otro, el cuarto quedaría bloqueado con un
            // identificador y la orden con otro, y al resolverla no se liberaría.
            orden = Orden.Crear(
                ordenId,
                habitacionId,
                peticion.HabitacionNumero,
                peticion.TipoFalla,
                peticion.Descripcion,
                peticion.Prioridad,
                peticion.ReportadoPor);

            bd.Ordenes.Add(orden);
            await bd.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // La habitación quedó bloqueada y la orden no existe. Se COMPENSA
            // liberándola: dejarla fuera de servicio sin orden que la explique
            // saca una habitación del inventario sin que nadie sepa por qué.
            log.LogError(
                ex,
                "No se pudo guardar la orden {OrdenId}; se compensa liberando la habitación {Numero}.",
                ordenId, peticion.HabitacionNumero);

            var compensacion = await habitaciones.LiberarAsync(peticion.HabitacionNumero, ordenId, ct);

            if (compensacion.Resultado != ResultadoHabitacion.Exito)
            {
                // La compensación también falló: queda inconsistencia real que
                // alguien tiene que limpiar. Se registra como crítico.
                log.LogCritical(
                    "La habitación {Numero} quedó FUERA_DE_SERVICIO sin orden asociada: {Detalle}",
                    peticion.HabitacionNumero, compensacion.Detalle);
            }

            return Results.Problem(
                title: "No se pudo registrar la orden",
                detail: "La habitación no quedó bloqueada. Volvé a intentarlo.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        // --- d. Publicar -------------------------------------------------------
        try
        {
            await publicador.PublicarAsync(
                EventoCreada,
                new EventoOrdenCreada(
                    orden.Id,
                    orden.HabitacionId,
                    orden.HabitacionNumero,
                    orden.TipoFalla,
                    orden.Descripcion,
                    orden.Prioridad,
                    orden.ReportadoPor),
                ct);
        }
        catch (Exception ex)
        {
            // La orden YA está guardada y no se deshace: existe y es válida. Lo
            // que se perdió es el disparo de la asignación, así que la orden se
            // queda ABIERTA sin técnico hasta que alguien la reintente.
            //
            // Es el problema clásico de la doble escritura (base + broker). La
            // solución real es un OUTBOX: guardar el evento en la misma
            // transacción que la orden y despacharlo aparte. Queda pendiente,
            // anotado en el README de este servicio.
            log.LogCritical(
                ex,
                "La orden {OrdenId} se guardó pero NO se publicó orden.creada: no se le asignará técnico.",
                orden.Id);
        }

        return Results.Created($"/ordenes/{orden.Id}", OrdenRespuesta.De(orden));
    }

    private static async Task<IResult> ListarAsync(
        OrdenesDbContext bd,
        CancellationToken ct,
        EstadoOrden? estado = null,
        int? habitacion = null)
    {
        var consulta = bd.Ordenes.AsNoTracking().AsQueryable();

        if (estado is { } filtroEstado)
        {
            consulta = consulta.Where(o => o.Estado == filtroEstado);
        }

        if (habitacion is { } numero)
        {
            consulta = consulta.Where(o => o.HabitacionNumero == numero);
        }

        var ordenes = await consulta
            .OrderByDescending(o => o.CreadaEn)
            .Select(o => OrdenRespuesta.De(o))
            .ToListAsync(ct);

        return Results.Ok(ordenes);
    }

    private static async Task<IResult> ObtenerAsync(
        Guid id, OrdenesDbContext bd, CancellationToken ct)
    {
        var orden = await bd.Ordenes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

        return orden is null
            ? Results.NotFound(new { mensaje = $"No existe la orden {id}." })
            : Results.Ok(OrdenRespuesta.De(orden));
    }

    /// <summary>
    /// Cierra la orden. El orden de los pasos es deliberado: primero se persiste
    /// RESUELTA y después se libera la habitación.
    /// </summary>
    /// <remarks>
    /// Si se liberara primero y fallara el guardado, la habitación volvería al
    /// inventario con la falla sin resolver — se podría alojar a alguien en un
    /// cuarto roto. Al revés, el peor caso es una habitación bloqueada de más:
    /// cuesta dinero, pero no afecta a ningún huésped y se arregla reintentando.
    ///
    /// Por eso el endpoint es IDEMPOTENTE: reintentarlo sobre una orden ya
    /// RESUELTA vuelve a intentar liberar y publicar.
    /// </remarks>
    private static async Task<IResult> ResolverAsync(
        Guid id,
        ResolverOrdenPeticion peticion,
        OrdenesDbContext bd,
        IHabitacionesClient habitaciones,
        IPublicadorEventos publicador,
        ILoggerFactory registros,
        CancellationToken ct)
    {
        var log = registros.CreateLogger("Ordenes.Resolver");

        var orden = await bd.Ordenes.FirstOrDefaultAsync(o => o.Id == id, ct);

        if (orden is null)
        {
            return Results.NotFound(new { mensaje = $"No existe la orden {id}." });
        }

        // Si ya estaba RESUELTA no es un conflicto: es un reintento de un cierre
        // cuya liberación o publicación había fallado. Se reanuda desde ahí.
        if (orden.Estado != EstadoOrden.RESUELTA)
        {
            try
            {
                orden.Resolver(peticion.ResueltoPor, peticion.NotaCierre);
                await bd.SaveChangesAsync(ct);
            }
            catch (TransicionInvalidaException ex)
            {
                return Results.Problem(
                    title: "Transición inválida",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Otro proceso tocó la orden entre la lectura y el guardado.
                return Results.Problem(
                    title: "La orden cambió mientras se cerraba",
                    detail: "Volvé a cargarla e intentá de nuevo.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        // --- Liberar la habitación --------------------------------------------
        var liberacion = await habitaciones.LiberarAsync(orden.HabitacionNumero, orden.Id, ct);

        if (liberacion.Resultado == ResultadoHabitacion.NoDisponible)
        {
            log.LogError(
                "La orden {OrdenId} quedó RESUELTA pero la habitación {Numero} sigue bloqueada: {Detalle}",
                orden.Id, orden.HabitacionNumero, liberacion.Detalle);

            return Results.Problem(
                title: "Servicio de habitaciones no disponible",
                detail: "La orden quedó cerrada, pero la habitación sigue bloqueada. Reintentá esta misma llamada.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (liberacion.Resultado != ResultadoHabitacion.Exito)
        {
            // 404 o 409 del otro lado: no se arregla reintentando, pero la orden
            // sí está cerrada. Se deja constancia y se sigue.
            log.LogWarning(
                "No se pudo liberar la habitación {Numero} de la orden {OrdenId}: {Detalle}",
                orden.HabitacionNumero, orden.Id, liberacion.Detalle);
        }

        // --- Publicar ----------------------------------------------------------
        // Va último: el contrato dice que quien recibe orden.resuelta puede
        // asumir que la habitación YA está disponible.
        try
        {
            await publicador.PublicarAsync(
                EventoResuelta,
                new EventoOrdenResuelta(orden.Id, orden.HabitacionId, orden.ResueltoPor, orden.NotaCierre),
                ct);
        }
        catch (Exception ex)
        {
            log.LogCritical(
                ex,
                "La orden {OrdenId} se cerró pero NO se publicó orden.resuelta: recepción no recibirá el aviso.",
                orden.Id);
        }

        return Results.Ok(OrdenRespuesta.De(orden));
    }
}
