// PROPÓSITO: definir el conjunto cerrado de estados en los que puede estar una
//   habitación. Existe como archivo aparte porque este vocabulario es el que
//   toda la organización usa para hablar del cuarto.
//
// DEBE CONTENER:
//   1. La enumeración EstadoHabitacion con exactamente tres valores:
//      DISPONIBLE, OCUPADA, FUERA_DE_SERVICIO.
//   2. La decisión de cómo se guarda en PostgreSQL: como texto legible, no como
//      entero, para que la base sea auditable a simple vista.
//   3. Si hace falta, un helper que diga si un estado admite reservas.
//
// NO DEBE CONTENER:
//   - Estados de la ORDEN (ABIERTA, ASIGNADA, RESUELTA); esos viven en
//     servicios/ordenes/Modelos/EstadoOrden.cs y no deben mezclarse.
//   - Estados intermedios inventados (EN_LIMPIEZA, RESERVADA) que no pidió el
//     dominio; agregar uno obliga a revisar todos los consumidores.
//   - Lógica de transición; esa vive en Modelos/Habitacion.cs.
//
// RELACIONADO:
//   - Modelos/Habitacion.cs
//   - docs/adr/002-limites-contextos.md (por qué este estado es dueño de este
//     servicio y no de ordenes)
