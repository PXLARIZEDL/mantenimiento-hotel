namespace Habitaciones.Modelos;

/// <summary>
/// Conjunto cerrado de estados en los que puede estar una habitación. Es el
/// vocabulario que toda la organización usa para hablar del cuarto.
/// </summary>
/// <remarks>
/// Se persiste como TEXTO en PostgreSQL (ver Datos/HabitacionesDbContext.cs)
/// para que la base sea auditable a simple vista.
/// </remarks>
public enum EstadoHabitacion
{
    DISPONIBLE,
    OCUPADA,
    FUERA_DE_SERVICIO
}

public static class EstadosHabitacion
{
    /// <summary>
    /// Si el cuarto se le puede vender a un huésped. Solo DISPONIBLE admite
    /// reservas: OCUPADA ya tiene huésped y FUERA_DE_SERVICIO está roto.
    /// </summary>
    public static bool AdmiteReservas(this EstadoHabitacion estado) =>
        estado == EstadoHabitacion.DISPONIBLE;
}
