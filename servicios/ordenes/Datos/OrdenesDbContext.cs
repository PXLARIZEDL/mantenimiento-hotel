using Microsoft.EntityFrameworkCore;
using Ordenes.Modelos;

namespace Ordenes.Datos;

/// <summary>
/// Única puerta de acceso a la base PostgreSQL del servicio ordenes.
/// </summary>
/// <remarks>
/// No hay ninguna tabla de habitaciones ni de técnicos: esos datos se piden por
/// HTTP o llegan copiados dentro de los eventos
/// (docs/adr/002-limites-contextos.md).
/// </remarks>
public class OrdenesDbContext : DbContext
{
    public OrdenesDbContext(DbContextOptions<OrdenesDbContext> opciones)
        : base(opciones)
    {
    }

    public DbSet<Orden> Ordenes => Set<Orden>();

    /// <summary>Eventos ya consumidos; sostiene la idempotencia del consumidor.</summary>
    public DbSet<EventoProcesado> EventosProcesados => Set<EventoProcesado>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Orden>(orden =>
        {
            orden.ToTable("ordenes");
            orden.HasKey(o => o.Id);

            orden.Property(o => o.HabitacionId).IsRequired();
            orden.Property(o => o.HabitacionNumero).IsRequired();

            // Los enums se guardan como TEXTO: la base queda auditable a simple
            // vista y el valor coincide con el que viaja en los contratos.
            orden.Property(o => o.TipoFalla)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();

            orden.Property(o => o.Prioridad)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            orden.Property(o => o.Estado)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            orden.Property(o => o.Descripcion).HasMaxLength(1000).IsRequired();
            orden.Property(o => o.ReportadoPor).HasMaxLength(120).IsRequired();
            orden.Property(o => o.TecnicoNombre).HasMaxLength(120);
            orden.Property(o => o.Especialidad).HasMaxLength(40);
            orden.Property(o => o.NotaCierre).HasMaxLength(1000);

            orden.Property(o => o.CreadaEn).IsRequired();

            // La UI lista por estado y habitaciones se consulta por número.
            orden.HasIndex(o => o.Estado);
            orden.HasIndex(o => o.HabitacionNumero);

            // Concurrencia optimista sobre la fila completa usando el xmin de
            // PostgreSQL: si el consumidor de orden.asignada y un PUT /resolver
            // tocan la misma orden a la vez, el segundo falla en vez de pisar al
            // primero en silencio.
            orden.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        modelo.Entity<EventoProcesado>(evento =>
        {
            evento.ToTable("eventos_procesados");

            // La clave primaria ES el eventoId: insertar un duplicado viola la
            // restricción, que es la última línea de defensa de la idempotencia
            // si dos instancias del consumidor corren a la vez.
            evento.HasKey(e => e.EventoId);

            evento.Property(e => e.TipoEvento).HasMaxLength(60).IsRequired();
            evento.Property(e => e.ProcesadoEn).IsRequired();
        });
    }
}
