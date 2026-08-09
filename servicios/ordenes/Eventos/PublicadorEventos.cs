using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RabbitMQ.Client;

namespace Ordenes.Eventos;

/// <summary>Opciones de conexión al broker. Se llenan desde la sección RabbitMq.</summary>
public sealed class OpcionesRabbitMq
{
    public const string Seccion = "RabbitMq";

    public string Host { get; set; } = "rabbitmq";

    public int Puerto { get; set; } = 5672;

    public string Usuario { get; set; } = "guest";

    public string Contrasena { get; set; } = "guest";

    public string Exchange { get; set; } = "hotel.eventos";

    public string ColaOrdenAsignada { get; set; } = "ordenes.orden-asignada";

    public int SegundosEsperaReconexion { get; set; } = 5;
}

/// <summary>
/// Publica los eventos de los que ordenes es productor: orden.creada y
/// orden.resuelta. Aísla al resto del servicio de la API de RabbitMQ.
/// </summary>
/// <remarks>
/// Aquí NO se decide cuándo publicar — eso lo decide
/// Endpoints/OrdenesEndpoints.cs. Aquí tampoco se consume nada: eso vive en
/// Eventos/ConsumidorOrdenAsignada.cs.
/// </remarks>
public interface IPublicadorEventos
{
    /// <summary>
    /// Publica un evento. <paramref name="evento"/> es a la vez la routing key y
    /// el tipoEvento del sobre: el catálogo define que son el mismo nombre.
    /// </summary>
    Task PublicarAsync(string evento, object cuerpo, CancellationToken ct = default);
}

public sealed class PublicadorEventos : IPublicadorEventos, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        // camelCase en el cable: es lo que leen los consumidores Python.
        // Cambiarlo rompe los contratos (contratos/*.json).
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly OpcionesRabbitMq _opciones;
    private readonly ILogger<PublicadorEventos> _log;
    private readonly ConnectionFactory _fabrica;
    private readonly SemaphoreSlim _cerrojo = new(1, 1);

    private IConnection? _conexion;
    private IModel? _canal;
    private bool _desechado;

    public PublicadorEventos(OpcionesRabbitMq opciones, ILogger<PublicadorEventos> log)
    {
        _opciones = opciones;
        _log = log;

        _fabrica = new ConnectionFactory
        {
            HostName = opciones.Host,
            Port = opciones.Puerto,
            UserName = opciones.Usuario,
            Password = opciones.Contrasena,
            // Reconexión ante caída del broker, con espera entre intentos. El
            // servicio debe arrancar y seguir vivo aunque RabbitMQ no esté.
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(opciones.SegundosEsperaReconexion),
            ClientProvidedName = "ordenes.publicador"
        };
    }

    public async Task PublicarAsync(string evento, object cuerpo, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_desechado, this);

        var mensaje = ArmarSobre(evento, cuerpo);
        var bytes = Encoding.UTF8.GetBytes(mensaje.ToJsonString(Json));

        // IModel no es seguro entre hilos; se serializa el acceso al canal.
        await _cerrojo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var canal = AsegurarCanal();

            var propiedades = canal.CreateBasicProperties();
            // Persistente: el mensaje sobrevive a un reinicio del broker.
            propiedades.Persistent = true;
            propiedades.ContentType = "application/json";
            propiedades.Type = evento;
            propiedades.MessageId = mensaje["eventoId"]!.GetValue<string>();
            propiedades.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            canal.BasicPublish(
                exchange: _opciones.Exchange,
                routingKey: evento,
                mandatory: false,
                basicProperties: propiedades,
                body: bytes);

            // Espera la confirmación del broker: sin esto, "publicado" solo
            // significa "escrito en el socket".
            canal.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));

            _log.LogInformation(
                "Publicado {Evento} (eventoId {EventoId}) en el exchange {Exchange}.",
                evento, propiedades.MessageId, _opciones.Exchange);
        }
        finally
        {
            _cerrojo.Release();
        }
    }

    /// <summary>
    /// Genera el sobre común de TODO evento y le pega encima los campos de
    /// negocio. Centralizarlo aquí es lo que garantiza que los tres contratos
    /// lleven eventoId, tipoEvento, version y ocurridoEn con la misma forma.
    /// </summary>
    private static JsonObject ArmarSobre(string evento, object cuerpo)
    {
        var sobre = new JsonObject
        {
            ["eventoId"] = Guid.NewGuid().ToString(),
            ["tipoEvento"] = evento,
            ["version"] = 1,
            // UTC con Z, nunca hora local del hotel (contratos/*.json).
            ["ocurridoEn"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'")
        };

        var campos = JsonSerializer.SerializeToNode(cuerpo, cuerpo.GetType(), Json)?.AsObject();

        if (campos is not null)
        {
            foreach (var campo in campos)
            {
                sobre[campo.Key] = campo.Value?.DeepClone();
            }
        }

        return sobre;
    }

    private IModel AsegurarCanal()
    {
        if (_canal is { IsOpen: true })
        {
            return _canal;
        }

        _canal?.Dispose();
        _conexion?.Dispose();

        _conexion = _fabrica.CreateConnection();
        _canal = _conexion.CreateModel();

        // Declaración idempotente: el servicio no asume que otro ya creó el
        // exchange. Cualquiera de los cinco puede arrancar primero.
        _canal.ExchangeDeclare(
            exchange: _opciones.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        _canal.ConfirmSelect();

        _log.LogInformation(
            "Conectado a RabbitMQ en {Host}:{Puerto}; exchange {Exchange} declarado.",
            _opciones.Host, _opciones.Puerto, _opciones.Exchange);

        return _canal;
    }

    public void Dispose()
    {
        if (_desechado)
        {
            return;
        }

        _desechado = true;
        _canal?.Dispose();
        _conexion?.Dispose();
        _cerrojo.Dispose();
    }
}
