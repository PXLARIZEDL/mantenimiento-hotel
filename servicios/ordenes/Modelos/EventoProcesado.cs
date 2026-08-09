namespace Ordenes.Modelos;

/// <summary>
/// Registro de un evento de integración ya consumido. Es lo que hace idempotente
/// a Eventos/ConsumidorOrdenAsignada.cs: la entrega de RabbitMQ es
/// <em>at-least-once</em>, así que el mismo mensaje puede llegar dos veces.
/// </summary>
/// <remarks>
/// La clave es <c>eventoId</c> (el del sobre del mensaje), NO <c>ordenId</c>:
/// una misma orden genera varios eventos distintos y filtrar por ordenId
/// descartaría mensajes legítimos. Ver docs/catalogo-eventos.md.
///
/// El alta de esta fila y el efecto del evento se guardan en la MISMA
/// transacción; si no, un fallo entre ambos reabre la ventana del duplicado.
/// </remarks>
public class EventoProcesado
{
    public Guid EventoId { get; set; }

    public string TipoEvento { get; set; } = string.Empty;

    public DateTimeOffset ProcesadoEn { get; set; }

    public static EventoProcesado Registrar(Guid eventoId, string tipoEvento) => new()
    {
        EventoId = eventoId,
        TipoEvento = tipoEvento,
        ProcesadoEn = DateTimeOffset.UtcNow
    };
}
