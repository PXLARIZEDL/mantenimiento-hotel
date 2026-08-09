namespace Ordenes.Modelos;

/// <summary>
/// Conjunto cerrado de estados por los que pasa una orden de mantenimiento.
/// Es el vocabulario que la UI muestra y que los eventos comunican al resto
/// del sistema.
/// </summary>
/// <remarks>
/// Se persiste como TEXTO en PostgreSQL (ver Datos/OrdenesDbContext.cs), no como
/// entero: la base queda legible a simple vista y agregar un estado no corre los
/// valores ya guardados.
/// </remarks>
public enum EstadoOrden
{
    ABIERTA,
    ASIGNADA,
    RESUELTA
}

/// <summary>
/// Declara qué transiciones de estado son válidas. Modelos/Orden.cs es quien la
/// consulta; nadie más debería cambiar un estado sin pasar por aquí.
/// </summary>
public static class TransicionesOrden
{
    private static readonly Dictionary<EstadoOrden, EstadoOrden[]> Permitidas = new()
    {
        // ABIERTA puede ir directo a RESUELTA: el contrato orden.resuelta.v1.json
        // admite resueltoPor nulo "si la orden se cerró sin haber sido asignada
        // nunca". Es el caso del técnico que ya estaba en el piso.
        [EstadoOrden.ABIERTA] = new[] { EstadoOrden.ASIGNADA, EstadoOrden.RESUELTA },
        [EstadoOrden.ASIGNADA] = new[] { EstadoOrden.RESUELTA },
        // Estado terminal: una orden resuelta no se reabre. Reabrirla sería una
        // orden nueva sobre la misma habitación.
        [EstadoOrden.RESUELTA] = Array.Empty<EstadoOrden>()
    };

    public static bool Permite(EstadoOrden desde, EstadoOrden hacia) =>
        Permitidas.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);
}
