using Microsoft.EntityFrameworkCore;
using Habitaciones.Modelos;

namespace Habitaciones.Datos;

/// <summary>Parámetros del inventario. Vienen de appsettings → sección Habitaciones.</summary>
public sealed class OpcionesInventario
{
    public const string Seccion = "Habitaciones";

    public int Total { get; set; } = 400;

    public int Pisos { get; set; } = 10;
}

/// <summary>
/// Siembra el inventario inicial del hotel. Sin las 400 habitaciones no hay
/// caso de uso que demostrar: ninguna orden se puede crear.
/// </summary>
public static class SembradorHabitaciones
{
    /// <summary>
    /// Crea las habitaciones que falten. Es idempotente: si ya están, no hace
    /// nada, así que reiniciar el contenedor no duplica ni pisa estados.
    /// </summary>
    public static async Task SembrarAsync(
        HabitacionesDbContext bd, OpcionesInventario opciones, ILogger registro)
    {
        if (await bd.Habitaciones.AnyAsync())
        {
            registro.LogInformation("El inventario ya estaba sembrado; no se toca.");
            return;
        }

        var porPiso = opciones.Total / opciones.Pisos;
        var habitaciones = new List<Habitacion>(opciones.Total);

        for (var numero = 1; numero <= opciones.Total; numero++)
        {
            var piso = ((numero - 1) / porPiso) + 1;

            // Reparto de tipos fijo y predecible, no aleatorio: la siembra debe
            // dar el mismo hotel en cada máquina para que la demo sea igual
            // para todos.
            var tipo = (numero % 10) switch
            {
                0 => TipoHabitacion.SUITE,
                1 or 2 or 3 => TipoHabitacion.SENCILLA,
                _ => TipoHabitacion.DOBLE
            };

            // Algunas nacen OCUPADA para que la demo no sea un hotel vacío. No
            // hay flujo de check-in en la v1 (ver pregunta 3 del README), así
            // que este es el único momento en que aparece ese estado.
            var estado = numero % 4 == 0
                ? EstadoHabitacion.OCUPADA
                : EstadoHabitacion.DISPONIBLE;

            habitaciones.Add(Habitacion.Crear(numero, piso, tipo, estado));
        }

        bd.Habitaciones.AddRange(habitaciones);
        await bd.SaveChangesAsync();

        registro.LogInformation(
            "Inventario sembrado: {Total} habitaciones en {Pisos} pisos.",
            opciones.Total, opciones.Pisos);
    }
}
