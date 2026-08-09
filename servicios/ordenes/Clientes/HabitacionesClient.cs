using System.Net;
using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Ordenes.Clientes;

/// <summary>Cómo terminó una llamada al servicio habitaciones, en términos de dominio.</summary>
public enum ResultadoHabitacion
{
    /// <summary>La habitación quedó en el estado pedido (o ya estaba en él).</summary>
    Exito,

    /// <summary>No existe una habitación con ese número.</summary>
    NoExiste,

    /// <summary>La habitación no admite esa transición ahora mismo (p. ej. está OCUPADA).</summary>
    TransicionInvalida,

    /// <summary>
    /// habitaciones no respondió: timeout, error del servidor, fallo de red o
    /// circuito abierto. Es el único caso REINTENTABLE desde fuera.
    /// </summary>
    NoDisponible
}

/// <param name="Resultado">Cómo terminó la llamada.</param>
/// <param name="HabitacionId">
/// Identificador interno de la habitación. Solo viene con <see cref="ResultadoHabitacion.Exito"/>.
/// Es la razón por la que ordenes puede publicar habitacionId sin tener su propia
/// tabla de habitaciones.
/// </param>
/// <param name="Detalle">Texto para el log y para el cuerpo de error de la API.</param>
public sealed record RespuestaHabitaciones(
    ResultadoHabitacion Resultado,
    Guid? HabitacionId,
    string? Detalle);

/// <summary>
/// Encapsula la ÚNICA llamada sincrónica del sistema (ordenes → habitaciones).
/// </summary>
/// <remarks>
/// Este archivo define QUÉ se llama y cómo se traduce la respuesta a dominio.
/// CON QUÉ garantías se llama (timeout, reintento, circuit breaker) lo define
/// Program.cs a partir de configuración — por eso aquí no hay ni una política de
/// Polly ni un número mágico.
/// </remarks>
public interface IHabitacionesClient
{
    Task<RespuestaHabitaciones> MarcarFueraDeServicioAsync(
        int numeroHabitacion, Guid ordenId, CancellationToken ct = default);

    Task<RespuestaHabitaciones> LiberarAsync(
        int numeroHabitacion, Guid ordenId, CancellationToken ct = default);
}

public sealed class HabitacionesClient : IHabitacionesClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HabitacionesClient> _log;

    public HabitacionesClient(HttpClient http, ILogger<HabitacionesClient> log)
    {
        _http = http;
        _log = log;
    }

    public Task<RespuestaHabitaciones> MarcarFueraDeServicioAsync(
        int numeroHabitacion, Guid ordenId, CancellationToken ct = default) =>
        LlamarAsync(numeroHabitacion, ordenId, "fuera-de-servicio", ct);

    public Task<RespuestaHabitaciones> LiberarAsync(
        int numeroHabitacion, Guid ordenId, CancellationToken ct = default) =>
        LlamarAsync(numeroHabitacion, ordenId, "disponible", ct);

    private async Task<RespuestaHabitaciones> LlamarAsync(
        int numeroHabitacion, Guid ordenId, string transicion, CancellationToken ct)
    {
        var ruta = $"habitaciones/{numeroHabitacion}/{transicion}";

        try
        {
            // El ordenId viaja siempre: el endpoint del otro lado es idempotente
            // y lo necesita para reconocer un reintento como la MISMA operación
            // en vez de como una nueva.
            using var respuesta = await _http.PutAsJsonAsync(
                ruta, new { ordenId }, ct);

            return await TraducirAsync(respuesta, numeroHabitacion, ordenId, transicion, ct);
        }
        catch (BrokenCircuitException)
        {
            // No se llegó a intentar: el breaker está abierto porque habitaciones
            // viene fallando. Insistir solo empeora la recuperación.
            _log.LogError(
                "Circuito ABIERTO hacia habitaciones; no se intentó {Transicion} en la {Numero} (orden {OrdenId}).",
                transicion, numeroHabitacion, ordenId);

            return new RespuestaHabitaciones(
                ResultadoHabitacion.NoDisponible,
                null,
                "El servicio de habitaciones no está disponible (circuito abierto).");
        }
        catch (TimeoutRejectedException)
        {
            _log.LogError(
                "TIMEOUT hacia habitaciones en {Transicion} de la {Numero} (orden {OrdenId}).",
                transicion, numeroHabitacion, ordenId);

            return new RespuestaHabitaciones(
                ResultadoHabitacion.NoDisponible,
                null,
                "El servicio de habitaciones no respondió a tiempo.");
        }
        catch (HttpRequestException ex)
        {
            _log.LogError(
                ex,
                "Fallo de RED hacia habitaciones en {Transicion} de la {Numero} (orden {OrdenId}).",
                transicion, numeroHabitacion, ordenId);

            return new RespuestaHabitaciones(
                ResultadoHabitacion.NoDisponible,
                null,
                "No se pudo contactar al servicio de habitaciones.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Cancelación provocada por el timeout de la política, no por el
            // cliente que se fue.
            _log.LogError(
                "Llamada CANCELADA por timeout en {Transicion} de la {Numero} (orden {OrdenId}).",
                transicion, numeroHabitacion, ordenId);

            return new RespuestaHabitaciones(
                ResultadoHabitacion.NoDisponible,
                null,
                "El servicio de habitaciones no respondió a tiempo.");
        }
    }

    private async Task<RespuestaHabitaciones> TraducirAsync(
        HttpResponseMessage respuesta,
        int numeroHabitacion,
        Guid ordenId,
        string transicion,
        CancellationToken ct)
    {
        // 200 y 204 son ambos éxito: 204 es "ya estaba en ese estado", que para
        // un endpoint idempotente es un resultado correcto, no un error.
        if (respuesta.IsSuccessStatusCode)
        {
            Guid? habitacionId = null;

            if (respuesta.StatusCode != HttpStatusCode.NoContent)
            {
                var cuerpo = await respuesta.Content
                    .ReadFromJsonAsync<HabitacionDto>(ct)
                    .ConfigureAwait(false);

                habitacionId = cuerpo?.Id;
            }

            return new RespuestaHabitaciones(ResultadoHabitacion.Exito, habitacionId, null);
        }

        // --- Errores NO reintentables ---------------------------------------
        // Reintentar un 404 o un 409 es un bug: la respuesta no va a cambiar por
        // insistir. Por eso se traducen a dominio aquí y las políticas de
        // Program.cs no los consideran fallo transitorio.
        switch (respuesta.StatusCode)
        {
            case HttpStatusCode.NotFound:
                _log.LogWarning(
                    "habitaciones respondió 404: la habitación {Numero} no existe (orden {OrdenId}).",
                    numeroHabitacion, ordenId);

                return new RespuestaHabitaciones(
                    ResultadoHabitacion.NoExiste,
                    null,
                    $"La habitación {numeroHabitacion} no existe.");

            case HttpStatusCode.Conflict:
                _log.LogWarning(
                    "habitaciones respondió 409: transición {Transicion} inválida para la {Numero} (orden {OrdenId}).",
                    transicion, numeroHabitacion, ordenId);

                return new RespuestaHabitaciones(
                    ResultadoHabitacion.TransicionInvalida,
                    null,
                    $"La habitación {numeroHabitacion} no admite pasar a {transicion} en su estado actual.");

            case HttpStatusCode.BadRequest:
                _log.LogWarning(
                    "habitaciones respondió 400 para la {Numero} (orden {OrdenId}).",
                    numeroHabitacion, ordenId);

                return new RespuestaHabitaciones(
                    ResultadoHabitacion.TransicionInvalida,
                    null,
                    "El servicio de habitaciones rechazó la petición.");
        }

        // --- 5xx que sobrevivió a los reintentos ------------------------------
        _log.LogError(
            "habitaciones respondió {Codigo} en {Transicion} de la {Numero} (orden {OrdenId}) tras agotar los reintentos.",
            (int)respuesta.StatusCode, transicion, numeroHabitacion, ordenId);

        return new RespuestaHabitaciones(
            ResultadoHabitacion.NoDisponible,
            null,
            "El servicio de habitaciones devolvió un error.");
    }

    /// <summary>Forma mínima de la respuesta de habitaciones que ordenes necesita.</summary>
    /// <remarks>
    /// Se leen solo los campos que hacen falta: si habitaciones agrega campos,
    /// este cliente no se rompe.
    /// </remarks>
    private sealed record HabitacionDto(Guid Id, int Numero, string Estado);
}
