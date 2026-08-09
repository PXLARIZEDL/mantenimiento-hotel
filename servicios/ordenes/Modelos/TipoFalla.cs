namespace Ordenes.Modelos;

/// <summary>
/// Tipos de falla que se pueden reportar. Los nombres son EXACTAMENTE los
/// valores permitidos de contratos/orden.creada.v1.json: se serializan tal cual
/// al publicar el evento, y el servicio tecnicos los usa para elegir la
/// especialidad.
/// </summary>
/// <remarks>
/// Cambiar un nombre aquí cambia el cable y obliga a subir el contrato a v2.
/// </remarks>
public enum TipoFalla
{
    AIRE_ACONDICIONADO,
    PLOMERIA,
    CERRADURA,
    ELECTRICIDAD
}

/// <summary>
/// Urgencia con la que recepción reporta la falla. La decide quien reporta, no
/// el sistema: no hay regla que la derive del tipo de falla.
/// </summary>
public enum Prioridad
{
    BAJA,
    MEDIA,
    ALTA
}
