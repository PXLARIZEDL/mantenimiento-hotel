using Microsoft.EntityFrameworkCore;
using Habitaciones.Modelos;

namespace Habitaciones.Datos;

/// <summary>
/// Única puerta de acceso a la base PostgreSQL del servicio habitaciones.
/// </summary>
/// <remarks>
/// No hay ninguna tabla de órdenes ni de técnicos: esos servicios tienen su
/// propia base y nadie cruza esa frontera
/// (docs/adr/002-limites-contextos.md).
/// </remarks>
public class HabitacionesDbContext : DbContext
{
    public HabitacionesDbContext(DbContextOptions<HabitacionesDbContext> opciones)
        : base(opciones)
    {
    }

    public DbSet<Habitacion> Habitaciones => Set<Habitacion>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Habitacion>(habitacion =>
        {
            habitacion.ToTable("habitaciones");
            habitacion.HasKey(h => h.Id);

            // El número es único aunque no sea la clave: es lo que se usa en las
            // rutas HTTP y lo que ordenes conoce.
            habitacion.HasIndex(h => h.Numero).IsUnique();

            habitacion.Property(h => h.Numero).IsRequired();
            habitacion.Property(h => h.Piso).IsRequired();

            // Enums como TEXTO: la base queda auditable a simple vista.
            habitacion.Property(h => h.Tipo)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            habitacion.Property(h => h.Estado)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            habitacion.Property(h => h.ActualizadaEn).IsRequired();

            // Postgres guarda la lista como uuid[]. Es una colección pequeña y
            // acotada (las fallas abiertas de UN cuarto), siempre se lee junto
            // con la habitación y nadie la consulta por separado: no merece una
            // tabla aparte.
            habitacion.Property(h => h.OrdenesActivas)
                .HasColumnType("uuid[]")
                .IsRequired();

            // La UI lista habitaciones filtrando por estado, y también por piso.
            habitacion.HasIndex(h => h.Estado);
            habitacion.HasIndex(h => h.Piso);

            // Concurrencia optimista con el xmin de PostgreSQL: si dos órdenes
            // simultáneas tocan el mismo cuarto, la segunda falla en vez de
            // pisar a la primera en silencio. Es exactamente el caso de dos
            // fallas reportadas a la vez sobre la misma habitación.
            habitacion.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });
    }
}
