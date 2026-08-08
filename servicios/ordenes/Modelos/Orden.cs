// PROPÓSITO: representar una orden de mantenimiento y proteger las reglas de su
//   ciclo de vida. Es el agregado central del sistema: casi todo el caso de uso
//   gira alrededor del estado de esta entidad.
//
// DEBE CONTENER:
//   1. La clase Orden con: identificador, número de habitación, tipo de falla,
//      descripción, prioridad, quién la reportó, estado actual y las fechas
//      (creada, asignada, resuelta).
//   2. El identificador del técnico asignado y su nombre, COPIADOS del evento
//      orden.asignada — no se consulta a tecnicos para mostrarlos.
//   3. Los métodos de transición que protegen la máquina de estados:
//      ABIERTA → ASIGNADA → RESUELTA, y nada más. Intentar saltar o retroceder
//      un estado debe fallar de forma explícita, no en silencio.
//   4. La nota de cierre que el técnico deja al resolver.
//   5. Un método de fábrica que cree la orden ya en estado ABIERTA con sus
//      campos obligatorios validados.
//
// NO DEBE CONTENER:
//   - La regla de qué técnico corresponde según la falla; eso es del servicio
//     tecnicos (docs/adr/002-limites-contextos.md).
//   - El estado de la habitación; aquí solo se guarda su NÚMERO. El estado lo
//     posee el servicio habitaciones.
//   - Llamadas HTTP ni publicación de eventos; la entidad no conoce la red.
//   - Atributos de Entity Framework; el mapeo va en Datos/OrdenesDbContext.cs.
//
// RELACIONADO:
//   - Modelos/EstadoOrden.cs
//   - contratos/orden.creada.v1.json y contratos/orden.resuelta.v1.json
//     (los campos de esta entidad alimentan esos eventos)
