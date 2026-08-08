// PROPÓSITO: representar una habitación del hotel. Es la entidad central del
//   servicio y la única fuente de verdad sobre el estado físico de un cuarto.
//
// DEBE CONTENER:
//   1. La clase Habitacion con sus propiedades: número (clave, 1..400), piso,
//      tipo de habitación, estado actual y fecha de última actualización.
//   2. Un campo que explique POR QUÉ está fuera de servicio (por ejemplo, el
//      identificador de la orden que la bloqueó), para poder liberarla bien.
//   3. Los métodos de transición de estado que protegen la invariante: por
//      ejemplo MarcarFueraDeServicio(ordenId) y Liberar(ordenId).
//   4. La regla de qué transiciones son válidas: una habitación OCUPADA puede
//      pasar a FUERA_DE_SERVICIO, pero una FUERA_DE_SERVICIO no puede pasar a
//      OCUPADA sin liberarse primero.
//   5. Un contador o colección de órdenes activas, si se decide que una
//      habitación con dos fallas abiertas no se libera al resolver solo una.
//
// NO DEBE CONTENER:
//   - Atributos de mapeo de Entity Framework ni nombres de tabla; la
//     configuración de persistencia va en Datos/HabitacionesDbContext.cs.
//   - Datos de la orden (tipo de falla, descripción, técnico); eso pertenece a
//     los servicios ordenes y tecnicos.
//   - Serialización ni DTOs de respuesta HTTP; esos van en Endpoints/.
//
// RELACIONADO:
//   - Modelos/EstadoHabitacion.cs (el estado que esta entidad guarda)
//   - Datos/HabitacionesDbContext.cs (cómo se persiste)
//   - contratos/orden.creada.v1.json → campo numeroHabitacion
