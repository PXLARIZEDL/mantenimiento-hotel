using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ordenes.Datos;
using Ordenes.Modelos;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Ordenes.Eventos;

/// <summary>
/// Forma de contratos/orden.asignada.v1.json tal como la necesita ordenes.
/// </summary>
/// <remarks>
/// Solo se declaran los campos que se usan. System.Text.Json ignora los
/// desconocidos por defecto, así que agregar un campo opcional al contrato no
/// rompe este consumidor — que es justamente la regla de versionado que fija
/// docs/catalogo-eventos.md.
/// </remarks>
public sealed record MensajeOrdenAsignada
{
    public Guid EventoId { get; init; }

    public string TipoEvento { get; init; } = string.Empty;

    public DateTimeOffset OcurridoEn { get; init; }

    public Guid OrdenId { get; init; }

    public Guid TecnicoId { get; init; }

    public string TecnicoNombre { get; init; } = string.Empty;

    public string Especialidad { get; init; } = string.Empty;
}

/// <summary>
/// Escucha orden.asignada (que produce el servicio tecnicos) y mueve la orden de
/// ABIERTA a ASIGNADA. Es el ÚNICO punto por el que una orden recibe técnico: no
/// existe endpoint HTTP equivalente, a propósito.
/// </summary>
public sealed class ConsumidorOrdenAsignada : BackgroundService
{
    private const string Evento = "orden.asignada";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly OpcionesRabbitMq _opciones;
    private readonly IServiceScopeFactory _ambitos;
    private readonly ILogger<ConsumidorOrdenAsignada> _log;

    private IConnection? _conexion;
    private IModel? _canal;

    public ConsumidorOrdenAsignada(
        OpcionesRabbitMq opciones,
        IServiceScopeFactory ambitos,
        ILogger<ConsumidorOrdenAsignada> log)
    {
        _opciones = opciones;
        _ambitos = ambitos;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Bucle de reconexión: el servicio debe arrancar aunque RabbitMQ todavía
        // no esté listo, y recuperarse si se cae después.
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Conectar();
                // Conectado: se queda escuchando hasta que caiga o se apague el
                // servicio. El consumidor trabaja por callbacks, no por bucle.
                while (!ct.IsCancellationRequested && _canal is { IsOpen: true })
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "No se pudo escuchar {Cola}; reintentando en {Segundos}s.",
                    _opciones.ColaOrdenAsignada, _opciones.SegundosEsperaReconexion);

                Desconectar();

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_opciones.SegundosEsperaReconexion), ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        Desconectar();
    }

    private void Conectar()
    {
        var fabrica = new ConnectionFactory
        {
            HostName = _opciones.Host,
            Port = _opciones.Puerto,
            UserName = _opciones.Usuario,
            Password = _opciones.Contrasena,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(_opciones.SegundosEsperaReconexion),
            DispatchConsumersAsync = true,
            ClientProvidedName = "ordenes.consumidor"
        };

        _conexion = fabrica.CreateConnection();
        _canal = _conexion.CreateModel();

        // Topología declarada de forma idempotente: ordenes no asume que tecnicos
        // ya creó el exchange, ni al revés.
        _canal.ExchangeDeclare(_opciones.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        _canal.QueueDeclare(_opciones.ColaOrdenAsignada, durable: true, exclusive: false, autoDelete: false);
        _canal.QueueBind(_opciones.ColaOrdenAsignada, _opciones.Exchange, Evento);

        // Un mensaje a la vez: la orden es un agregado y procesarlas en paralelo
        // solo generaría conflictos de concurrencia sobre la misma fila.
        _canal.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.Received += RecibirAsync;

        // autoAck en false: el mensaje se confirma SOLO después de guardar.
        _canal.BasicConsume(_opciones.ColaOrdenAsignada, autoAck: false, consumer: consumidor);

        _log.LogInformation(
            "Escuchando {Cola} con binding {RoutingKey} sobre {Exchange}.",
            _opciones.ColaOrdenAsignada, Evento, _opciones.Exchange);
    }

    private async Task RecibirAsync(object emisor, BasicDeliverEventArgs entrega)
    {
        var canal = _canal;

        if (canal is null)
        {
            return;
        }

        MensajeOrdenAsignada? mensaje;

        // --- 1. Deserialización ------------------------------------------------
        try
        {
            var texto = Encoding.UTF8.GetString(entrega.Body.Span);
            mensaje = JsonSerializer.Deserialize<MensajeOrdenAsignada>(texto, Json);
        }
        catch (JsonException ex)
        {
            // Mensaje envenenado: reintentarlo daría exactamente el mismo error.
            // Se descarta con log en vez de bloquear la cola para siempre.
            _log.LogError(ex, "Mensaje ilegible en {Cola}; se descarta.", _opciones.ColaOrdenAsignada);
            canal.BasicNack(entrega.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        if (mensaje is null || mensaje.EventoId == Guid.Empty || mensaje.OrdenId == Guid.Empty)
        {
            _log.LogError("Mensaje sin eventoId u ordenId en {Cola}; se descarta.", _opciones.ColaOrdenAsignada);
            canal.BasicNack(entrega.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        // --- 2. Aplicación ------------------------------------------------------
        try
        {
            await AplicarAsync(mensaje).ConfigureAwait(false);
            canal.BasicAck(entrega.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            // Fallo probablemente transitorio (la base no responde). Reintento
            // ACOTADO: se reencola una vez; si vuelve a fallar ya viene marcado
            // como redelivered y se descarta, para no bloquear la cola.
            if (entrega.Redelivered)
            {
                _log.LogError(
                    ex,
                    "El evento {EventoId} falló dos veces; se DESCARTA (orden {OrdenId}).",
                    mensaje.EventoId, mensaje.OrdenId);

                canal.BasicNack(entrega.DeliveryTag, multiple: false, requeue: false);
            }
            else
            {
                _log.LogWarning(
                    ex,
                    "El evento {EventoId} falló; se reencola una vez (orden {OrdenId}).",
                    mensaje.EventoId, mensaje.OrdenId);

                canal.BasicNack(entrega.DeliveryTag, multiple: false, requeue: true);
            }
        }
    }

    private async Task AplicarAsync(MensajeOrdenAsignada mensaje)
    {
        using var ambito = _ambitos.CreateScope();
        var bd = ambito.ServiceProvider.GetRequiredService<OrdenesDbContext>();

        // --- IDEMPOTENCIA -------------------------------------------------------
        // Se comprueba ANTES de aplicar nada. La entrega es at-least-once: recibir
        // el mismo eventoId dos veces NO debe asignar dos veces.
        var yaProcesado = await bd.EventosProcesados
            .AnyAsync(e => e.EventoId == mensaje.EventoId)
            .ConfigureAwait(false);

        if (yaProcesado)
        {
            _log.LogInformation(
                "Evento {EventoId} ya procesado; se confirma sin hacer nada.", mensaje.EventoId);
            return;
        }

        var orden = await bd.Ordenes
            .FirstOrDefaultAsync(o => o.Id == mensaje.OrdenId)
            .ConfigureAwait(false);

        if (orden is null)
        {
            // No hay nada que reintentar: la orden no existe y no va a aparecer.
            // Se registra el evento como procesado para no volver a evaluarlo.
            _log.LogWarning(
                "Llegó una asignación para la orden {OrdenId}, que no existe; se descarta.",
                mensaje.OrdenId);

            bd.EventosProcesados.Add(EventoProcesado.Registrar(mensaje.EventoId, Evento));
            await bd.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        if (orden.Estado == EstadoOrden.RESUELTA)
        {
            // Carrera normal: el técnico ya la cerró antes de que llegara la
            // asignación. Asignar ahora sería retroceder el estado.
            _log.LogWarning(
                "La orden {OrdenId} ya está RESUELTA; se ignora la asignación del técnico {TecnicoId}.",
                mensaje.OrdenId, mensaje.TecnicoId);

            bd.EventosProcesados.Add(EventoProcesado.Registrar(mensaje.EventoId, Evento));
            await bd.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        if (orden.Estado == EstadoOrden.ASIGNADA)
        {
            // Otro eventoId distinto para la misma orden: tecnicos reasignó. No es
            // un duplicado, pero v1 no define reasignación, así que se deja
            // constancia y se conserva la primera.
            _log.LogWarning(
                "La orden {OrdenId} ya tenía técnico ({TecnicoActual}); se ignora la reasignación a {TecnicoNuevo}.",
                mensaje.OrdenId, orden.TecnicoId, mensaje.TecnicoId);

            bd.EventosProcesados.Add(EventoProcesado.Registrar(mensaje.EventoId, Evento));
            await bd.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        // El nombre y la especialidad se COPIAN dentro de la orden para que la UI
        // no tenga que preguntarle nada a tecnicos.
        orden.Asignar(mensaje.TecnicoId, mensaje.TecnicoNombre, mensaje.Especialidad, mensaje.OcurridoEn);

        // El efecto y el registro de idempotencia, en la MISMA transacción: si se
        // guardaran por separado, un fallo entre ambos reabriría la ventana del
        // duplicado.
        bd.EventosProcesados.Add(EventoProcesado.Registrar(mensaje.EventoId, Evento));
        await bd.SaveChangesAsync().ConfigureAwait(false);

        _log.LogInformation(
            "Orden {OrdenId} ASIGNADA a {TecnicoNombre} ({Especialidad}).",
            orden.Id, mensaje.TecnicoNombre, mensaje.Especialidad);
    }

    private void Desconectar()
    {
        try
        {
            _canal?.Close();
            _conexion?.Close();
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Error al cerrar la conexión del consumidor; se ignora.");
        }
        finally
        {
            _canal?.Dispose();
            _conexion?.Dispose();
            _canal = null;
            _conexion = null;
        }
    }

    public override void Dispose()
    {
        Desconectar();
        base.Dispose();
    }
}
