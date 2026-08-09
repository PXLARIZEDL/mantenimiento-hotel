namespace Ordenes.Clientes;

/// <summary>
/// Configuración de la única llamada sincrónica del sistema. Los valores reales
/// llegan por variables de entorno; los de aquí son solo el defecto razonable.
/// </summary>
public sealed class OpcionesHabitaciones
{
    public const string Seccion = "Habitaciones";

    /// <summary>Dirección de habitaciones dentro de la red de Docker.</summary>
    public string UrlBase { get; set; } = "http://habitaciones:8080";

    public OpcionesResiliencia Resiliencia { get; set; } = new();
}

/// <summary>
/// Los tres mecanismos exigidos por docs/adr/003-estrategia-comunicacion.md.
/// Program.cs los traduce en las políticas del HttpClient tipado; ni el cliente
/// ni los endpoints conocen estos números.
/// </summary>
public sealed class OpcionesResiliencia
{
    /// <summary>
    /// Tope por intento. Bloquear una habitación es una escritura simple contra
    /// PostgreSQL: si tarda más de esto, algo está mal y conviene reintentar.
    /// </summary>
    public int TimeoutSegundos { get; set; } = 3;

    /// <summary>Reintentos ADICIONALES al primer intento.</summary>
    public int Reintentos { get; set; } = 3;

    /// <summary>Espera base; crece exponencialmente y lleva jitter.</summary>
    public int EsperaBaseMilisegundos { get; set; } = 200;

    /// <summary>Proporción de fallos (0 a 1) que abre el circuito.</summary>
    public double UmbralFallosCircuito { get; set; } = 0.5;

    /// <summary>Cuánto permanece abierto antes de dejar pasar una prueba.</summary>
    public int SegundosCircuitoAbierto { get; set; } = 30;

    /// <summary>Ventana sobre la que se mide el umbral de fallos.</summary>
    public int SegundosVentanaMuestreo { get; set; } = 30;

    /// <summary>Llamadas mínimas en la ventana antes de que el umbral cuente.</summary>
    public int MinimoLlamadasCircuito { get; set; } = 8;
}
