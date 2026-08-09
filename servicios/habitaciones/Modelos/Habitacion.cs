namespace Habitaciones.Modelos;

/// <summary>Tipos de cuarto del hotel. Es dato de inventario, no de estado.</summary>
public enum TipoHabitacion
{
    SENCILLA,
    DOBLE,
    SUITE
}

/// <summary>
/// Se lanza cuando se pide una transición que la habitación no admite.
/// Endpoints/HabitacionesEndpoints.cs la traduce a 409 Conflict.
/// </summary>
public class TransicionInvalidaException : InvalidOperationException
{
    public TransicionInvalidaException(string mensaje) : base(mensaje)
    {
    }
}

/// <summary>
/// Una habitación del hotel. Es la única fuente de verdad sobre el estado
/// físico de un cuarto: nadie más escribe este dato.
/// </summary>
public class Habitacion
{
    private Habitacion() { }

    /// <summary>
    /// Identificador estable de la habitación. Es lo que viaja como
    /// <c>habitacionId</c> en contratos/orden.creada.v1.json.
    /// </summary>
    /// <remarks>
    /// El esqueleto proponía usar el NÚMERO como clave primaria. Se usa un GUID
    /// porque el contrato de eventos exige un habitacionId estable e
    /// independiente de la numeración: si el hotel renumera un piso, el número
    /// cambia y las órdenes viejas quedarían apuntando a otro cuarto. El número
    /// sigue siendo único y es lo que se usa en las rutas HTTP.
    /// </remarks>
    public Guid Id { get; private set; }

    /// <summary>Número visible del cuarto, 1 a 400. Único.</summary>
    public int Numero { get; private set; }

    public int Piso { get; private set; }

    public TipoHabitacion Tipo { get; private set; }

    public EstadoHabitacion Estado { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    /// <summary>
    /// Órdenes de mantenimiento abiertas que mantienen bloqueado el cuarto.
    /// </summary>
    /// <remarks>
    /// Es una COLECCIÓN, no un solo id, y esa es la respuesta a la pregunta 1
    /// del README: si una habitación tiene dos fallas abiertas y se resuelve
    /// una, el cuarto NO se libera. Solo vuelve a DISPONIBLE cuando la lista
    /// queda vacía. Liberarlo antes devolvería al inventario un cuarto que
    /// sigue roto.
    /// </remarks>
    public List<Guid> OrdenesActivas { get; private set; } = new();

    public static Habitacion Crear(int numero, int piso, TipoHabitacion tipo, EstadoHabitacion estado)
    {
        if (numero is < 1 or > 400)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numero), numero, "El hotel tiene 400 habitaciones.");
        }

        return new Habitacion
        {
            Id = Guid.NewGuid(),
            Numero = numero,
            Piso = piso,
            Tipo = tipo,
            Estado = estado,
            ActualizadaEn = DateTimeOffset.UtcNow,
            OrdenesActivas = new List<Guid>()
        };
    }

    /// <summary>
    /// Bloquea el cuarto por una orden de mantenimiento. IDEMPOTENTE: llamarlo
    /// dos veces con el mismo ordenId deja exactamente el mismo resultado,
    /// porque ordenes reintenta esta llamada.
    /// </summary>
    /// <returns>
    /// <c>true</c> si esta llamada cambió algo; <c>false</c> si era un reintento
    /// o la orden ya estaba registrada.
    /// </returns>
    public bool MarcarFueraDeServicio(Guid ordenId)
    {
        if (ordenId == Guid.Empty)
        {
            throw new ArgumentException("Hay que decir qué orden bloquea el cuarto.", nameof(ordenId));
        }

        // Reintento de ordenes, o segunda falla ya registrada: nada que hacer.
        if (OrdenesActivas.Contains(ordenId))
        {
            return false;
        }

        // Respuesta a la pregunta 2 del README: si el cuarto ya está bloqueado
        // por OTRA orden, no es un 409 — es una segunda falla legítima sobre el
        // mismo cuarto. Se registra y se responde 200. Rechazarla dejaría una
        // orden viva sin que el cuarto sepa que debe seguir bloqueado.
        OrdenesActivas.Add(ordenId);
        Estado = EstadoHabitacion.FUERA_DE_SERVICIO;
        ActualizadaEn = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>
    /// Quita una orden del cuarto y lo libera si no queda ninguna. También
    /// IDEMPOTENTE.
    /// </summary>
    /// <returns><c>true</c> si esta llamada cambió algo.</returns>
    public bool Liberar(Guid ordenId)
    {
        if (ordenId == Guid.Empty)
        {
            throw new ArgumentException("Hay que decir qué orden se cerró.", nameof(ordenId));
        }

        var quitada = OrdenesActivas.Remove(ordenId);

        if (!quitada && Estado != EstadoHabitacion.FUERA_DE_SERVICIO)
        {
            // Reintento de una liberación que ya se aplicó. No es error.
            return false;
        }

        if (OrdenesActivas.Count > 0)
        {
            // Quedan fallas abiertas: el cuarto sigue bloqueado.
            ActualizadaEn = DateTimeOffset.UtcNow;
            return quitada;
        }

        // Vuelve a DISPONIBLE, no a OCUPADA: el servicio no sabe si había
        // huésped, y devolver un cuarto a OCUPADA por su cuenta sería inventar
        // un dato que no posee.
        Estado = EstadoHabitacion.DISPONIBLE;
        ActualizadaEn = DateTimeOffset.UtcNow;
        return true;
    }
}
