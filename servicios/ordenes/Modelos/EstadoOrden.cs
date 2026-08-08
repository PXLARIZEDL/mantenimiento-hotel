// PROPÓSITO: definir el conjunto cerrado de estados por los que pasa una orden
//   de mantenimiento. Es el vocabulario que la UI muestra y que los eventos
//   comunican al resto del sistema.
//
// DEBE CONTENER:
//   1. La enumeración EstadoOrden con exactamente tres valores:
//      ABIERTA, ASIGNADA, RESUELTA.
//   2. La decisión de guardarlo como texto en PostgreSQL, no como entero, para
//      que la base sea legible y para que agregar un estado no corra los índices.
//   3. Opcionalmente, una tabla o método que declare las transiciones válidas y
//      que Modelos/Orden.cs consulte.
//
// NO DEBE CONTENER:
//   - Estados de la HABITACIÓN (DISPONIBLE, OCUPADA, FUERA_DE_SERVICIO); esos
//     viven en servicios/habitaciones/Modelos/EstadoHabitacion.cs.
//   - Estados extra no pedidos (CANCELADA, EN_PROGRESO); agregarlos rompe a los
//     consumidores de los eventos y obliga a subir la versión del contrato.
//
// RELACIONADO:
//   - Modelos/Orden.cs
//   - Los tres contratos de contratos/*.json, cuyo campo estado usa este mismo
//     vocabulario
