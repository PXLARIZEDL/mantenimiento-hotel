namespace Ordenes.Modelos;

/// <summary>
/// Se lanza cuando se intenta un cambio de estado que la máquina de estados no
/// admite. Existe para que saltarse una transición falle de forma EXPLÍCITA y no
/// en silencio; Endpoints/OrdenesEndpoints.cs la traduce a 409 Conflict.
/// </summary>
public class TransicionInvalidaException : InvalidOperationException
{
    public TransicionInvalidaException(EstadoOrden desde, EstadoOrden hacia)
        : base($"Una orden en estado {desde} no puede pasar a {hacia}.")
    {
        Desde = desde;
        Hacia = hacia;
    }

    public EstadoOrden Desde { get; }

    public EstadoOrden Hacia { get; }
}

/// <summary>
/// Orden de mantenimiento: el agregado central del sistema. Protege las reglas
/// de su ciclo de vida ABIERTA → ASIGNADA → RESUELTA.
/// </summary>
/// <remarks>
/// La entidad no conoce la red: no hace HTTP ni publica eventos. Tampoco decide
/// qué técnico corresponde — esa regla es del servicio tecnicos
/// (docs/adr/002-limites-contextos.md).
/// </remarks>
public class Orden
{
    // EF Core materializa por aquí; el resto del código usa Crear().
    private Orden() { }

    public Guid Id { get; private set; }

    /// <summary>
    /// Identificador de la habitación en el servicio habitaciones. Llega en la
    /// respuesta al bloquearla y se guarda para poder publicarlo en los eventos
    /// sin volver a preguntar.
    /// </summary>
    public Guid HabitacionId { get; private set; }

    /// <summary>
    /// Número visible de la habitación. Es lo único que ordenes sabe del cuarto:
    /// su ESTADO pertenece al servicio habitaciones.
    /// </summary>
    public int HabitacionNumero { get; private set; }

    public TipoFalla TipoFalla { get; private set; }

    public string Descripcion { get; private set; } = string.Empty;

    public Prioridad Prioridad { get; private set; }

    public string ReportadoPor { get; private set; } = string.Empty;

    public EstadoOrden Estado { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    public DateTimeOffset? AsignadaEn { get; private set; }

    public DateTimeOffset? ResueltaEn { get; private set; }

    // --- Datos COPIADOS del evento orden.asignada -------------------------
    // Se guardan a propósito para que la UI pinte la lista sin consultar a
    // tecnicos. Son el valor en el momento del evento, no la fuente de verdad:
    // si el técnico cambia de nombre después, esta copia no se corrige.

    public Guid? TecnicoId { get; private set; }

    public string? TecnicoNombre { get; private set; }

    public string? Especialidad { get; private set; }

    // --- Cierre ------------------------------------------------------------

    /// <summary>Quién cerró la orden. Nulo si se cerró sin haber sido asignada.</summary>
    public Guid? ResueltoPor { get; private set; }

    public string? NotaCierre { get; private set; }

    /// <summary>
    /// Crea la orden ya validada y en estado ABIERTA. Es el único camino de alta.
    /// </summary>
    public static Orden Crear(
        Guid habitacionId,
        int habitacionNumero,
        TipoFalla tipoFalla,
        string descripcion,
        Prioridad prioridad,
        string reportadoPor)
    {
        if (habitacionId == Guid.Empty)
        {
            throw new ArgumentException("La habitación es obligatoria.", nameof(habitacionId));
        }

        if (habitacionNumero is < 1 or > 400)
        {
            throw new ArgumentOutOfRangeException(
                nameof(habitacionNumero), habitacionNumero, "El hotel tiene 400 habitaciones.");
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            throw new ArgumentException("La descripción es obligatoria.", nameof(descripcion));
        }

        if (string.IsNullOrWhiteSpace(reportadoPor))
        {
            throw new ArgumentException("Hay que registrar quién reporta.", nameof(reportadoPor));
        }

        return new Orden
        {
            Id = Guid.NewGuid(),
            HabitacionId = habitacionId,
            HabitacionNumero = habitacionNumero,
            TipoFalla = tipoFalla,
            Descripcion = descripcion.Trim(),
            Prioridad = prioridad,
            ReportadoPor = reportadoPor.Trim(),
            Estado = EstadoOrden.ABIERTA,
            CreadaEn = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Aplica la asignación decidida por el servicio tecnicos. Se invoca solo
    /// desde el consumidor de orden.asignada; no hay endpoint HTTP equivalente.
    /// </summary>
    public void Asignar(Guid tecnicoId, string tecnicoNombre, string especialidad, DateTimeOffset asignadaEn)
    {
        if (!TransicionesOrden.Permite(Estado, EstadoOrden.ASIGNADA))
        {
            throw new TransicionInvalidaException(Estado, EstadoOrden.ASIGNADA);
        }

        TecnicoId = tecnicoId;
        TecnicoNombre = tecnicoNombre;
        Especialidad = especialidad;
        AsignadaEn = asignadaEn;
        Estado = EstadoOrden.ASIGNADA;
    }

    /// <summary>
    /// Cierra la orden. <paramref name="resueltoPor"/> puede ser nulo si nunca
    /// llegó a asignarse (contratos/orden.resuelta.v1.json lo contempla).
    /// </summary>
    public void Resolver(Guid? resueltoPor, string? notaCierre)
    {
        if (!TransicionesOrden.Permite(Estado, EstadoOrden.RESUELTA))
        {
            throw new TransicionInvalidaException(Estado, EstadoOrden.RESUELTA);
        }

        // Si no viene quién la resolvió, se asume el técnico asignado.
        ResueltoPor = resueltoPor ?? TecnicoId;
        NotaCierre = string.IsNullOrWhiteSpace(notaCierre) ? null : notaCierre.Trim();
        ResueltaEn = DateTimeOffset.UtcNow;
        Estado = EstadoOrden.RESUELTA;
    }
}
