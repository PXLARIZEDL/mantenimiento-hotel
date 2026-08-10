using Ordenes.Modelos;

namespace Ordenes.Pruebas;

/// <summary>
/// Pruebas de la máquina de estados y del alta de una orden.
///
/// Ninguna necesita PostgreSQL, RabbitMQ ni levantar la aplicación: la entidad
/// no conoce la red. Que se pueda probar así es consecuencia del diseño.
/// </summary>
public class OrdenPruebas
{
    private static readonly Guid HabitacionId = Guid.Parse("c8d3a5b1-6666-4a7b-9d42-5e6f708a9005");
    private static readonly Guid TecnicoId = Guid.Parse("4d5e6f70-4444-4e6f-b123-3c4d5e6f7003");

    private static Orden CrearOrden(Guid? id = null) => Orden.Crear(
        id ?? Guid.NewGuid(),
        HabitacionId,
        habitacionNumero: 314,
        TipoFalla.AIRE_ACONDICIONADO,
        descripcion: "No enfría y gotea sobre la alfombra.",
        Prioridad.ALTA,
        reportadoPor: "recepcion.turno.noche");

    // -- Alta ---------------------------------------------------------------

    [Fact]
    public void UnaOrdenNaceAbiertaYSinTecnico()
    {
        var orden = CrearOrden();

        Assert.Equal(EstadoOrden.ABIERTA, orden.Estado);
        Assert.Null(orden.TecnicoId);
        Assert.Null(orden.AsignadaEn);
        Assert.Null(orden.ResueltaEn);
    }

    [Fact]
    public void LaOrdenConservaElIdentificadorQueRecibe()
    {
        // Es EL bug que se encontró al ejecutar el sistema por primera vez: la
        // entidad acuñaba un Guid propio, así que la habitación quedaba
        // bloqueada con un identificador y la orden nacía con otro. Al
        // resolverla, el cuarto no se liberaba nunca.
        var id = Guid.NewGuid();

        var orden = CrearOrden(id);

        Assert.Equal(id, orden.Id);
    }

    [Fact]
    public void SinIdentificadorNoSePuedeCrear()
    {
        // Ese id nace ANTES que la orden, en el paso que bloquea la habitación.
        // Si llegara vacío significaría que alguien se saltó ese paso.
        Assert.Throws<ArgumentException>(() => CrearOrden(Guid.Empty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(401)]
    [InlineData(-3)]
    public void LaHabitacionTieneQueEstarEnElHotel(int numero)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Orden.Crear(
            Guid.NewGuid(), HabitacionId, numero, TipoFalla.PLOMERIA,
            "Fuga.", Prioridad.MEDIA, "recepcion"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HayQueContarQueEstaFallando(string descripcion)
    {
        Assert.Throws<ArgumentException>(() => Orden.Crear(
            Guid.NewGuid(), HabitacionId, 314, TipoFalla.PLOMERIA,
            descripcion, Prioridad.MEDIA, "recepcion"));
    }

    // -- Transiciones válidas -----------------------------------------------

    [Fact]
    public void AsignarMueveAAsignadaYCopiaAlTecnico()
    {
        var orden = CrearOrden();
        var cuando = DateTimeOffset.UtcNow;

        orden.Asignar(TecnicoId, "Luis Ramírez", "AIRE_ACONDICIONADO", cuando);

        Assert.Equal(EstadoOrden.ASIGNADA, orden.Estado);
        Assert.Equal(TecnicoId, orden.TecnicoId);
        // El nombre se COPIA dentro de la orden a propósito, para que la UI no
        // tenga que preguntarle nada al servicio tecnicos.
        Assert.Equal("Luis Ramírez", orden.TecnicoNombre);
        Assert.Equal(cuando, orden.AsignadaEn);
    }

    [Fact]
    public void UnaOrdenAsignadaSePuedeResolver()
    {
        var orden = CrearOrden();
        orden.Asignar(TecnicoId, "Luis Ramírez", "AIRE_ACONDICIONADO", DateTimeOffset.UtcNow);

        orden.Resolver(TecnicoId, "Se limpió el filtro.");

        Assert.Equal(EstadoOrden.RESUELTA, orden.Estado);
        Assert.Equal(TecnicoId, orden.ResueltoPor);
        Assert.Equal("Se limpió el filtro.", orden.NotaCierre);
        Assert.NotNull(orden.ResueltaEn);
    }

    [Fact]
    public void UnaOrdenAbiertaSePuedeResolverSinHaberSidoAsignada()
    {
        // Es el caso real del técnico que ya estaba en el piso. El contrato lo
        // respalda: orden.resuelta.v1.json admite resueltoPor nulo.
        var orden = CrearOrden();

        orden.Resolver(resueltoPor: null, "Lo arregló el técnico que pasaba.");

        Assert.Equal(EstadoOrden.RESUELTA, orden.Estado);
        Assert.Null(orden.ResueltoPor);
    }

    [Fact]
    public void SiNoSeDiceQuienResolvioSeAsumeElTecnicoAsignado()
    {
        var orden = CrearOrden();
        orden.Asignar(TecnicoId, "Luis Ramírez", "AIRE_ACONDICIONADO", DateTimeOffset.UtcNow);

        orden.Resolver(resueltoPor: null, "Listo.");

        Assert.Equal(TecnicoId, orden.ResueltoPor);
    }

    // -- Transiciones inválidas ---------------------------------------------

    [Fact]
    public void UnaOrdenResueltaNoSeReabre()
    {
        // RESUELTA es terminal. Volver al mismo cuarto es una orden NUEVA.
        var orden = CrearOrden();
        orden.Resolver(TecnicoId, "Listo.");

        var error = Assert.Throws<TransicionInvalidaException>(
            () => orden.Asignar(TecnicoId, "Otro", "PLOMERIA", DateTimeOffset.UtcNow));

        Assert.Equal(EstadoOrden.RESUELTA, error.Desde);
        Assert.Equal(EstadoOrden.ASIGNADA, error.Hacia);
    }

    [Fact]
    public void UnaOrdenResueltaNoSeResuelveDosVeces()
    {
        var orden = CrearOrden();
        orden.Resolver(TecnicoId, "Listo.");

        Assert.Throws<TransicionInvalidaException>(() => orden.Resolver(TecnicoId, "Otra vez."));
    }

    [Fact]
    public void UnaOrdenAsignadaNoSeReasigna()
    {
        // v1 no contempla reasignar. Se falla explícito en vez de pisar la
        // asignación anterior en silencio.
        var orden = CrearOrden();
        orden.Asignar(TecnicoId, "Luis Ramírez", "AIRE_ACONDICIONADO", DateTimeOffset.UtcNow);

        Assert.Throws<TransicionInvalidaException>(
            () => orden.Asignar(Guid.NewGuid(), "Otro", "PLOMERIA", DateTimeOffset.UtcNow));
    }

    // -- La tabla de transiciones -------------------------------------------

    [Theory]
    [InlineData(EstadoOrden.ABIERTA, EstadoOrden.ASIGNADA, true)]
    [InlineData(EstadoOrden.ABIERTA, EstadoOrden.RESUELTA, true)]
    [InlineData(EstadoOrden.ASIGNADA, EstadoOrden.RESUELTA, true)]
    [InlineData(EstadoOrden.ASIGNADA, EstadoOrden.ABIERTA, false)]
    [InlineData(EstadoOrden.RESUELTA, EstadoOrden.ABIERTA, false)]
    [InlineData(EstadoOrden.RESUELTA, EstadoOrden.ASIGNADA, false)]
    [InlineData(EstadoOrden.ABIERTA, EstadoOrden.ABIERTA, false)]
    public void LaTablaDeTransicionesEsLaEsperada(
        EstadoOrden desde, EstadoOrden hacia, bool permitida)
    {
        Assert.Equal(permitida, TransicionesOrden.Permite(desde, hacia));
    }

    // -- Higiene ------------------------------------------------------------

    [Fact]
    public void LaDescripcionYQuienReportaSeGuardanSinEspaciosDeMas()
    {
        var orden = Orden.Crear(
            Guid.NewGuid(), HabitacionId, 314, TipoFalla.CERRADURA,
            "  La cerradura no abre.  ", Prioridad.ALTA, "  recepcion  ");

        Assert.Equal("La cerradura no abre.", orden.Descripcion);
        Assert.Equal("recepcion", orden.ReportadoPor);
    }

    [Fact]
    public void UnaNotaDeCierreVaciaSeGuardaComoNula()
    {
        // Para que la UI y notificaciones no tengan que distinguir entre "no
        // dejó nota" y "dejó una nota en blanco".
        var orden = CrearOrden();

        orden.Resolver(TecnicoId, "   ");

        Assert.Null(orden.NotaCierre);
    }
}
