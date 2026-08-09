# ADR 002 — Límites de los contextos

- **Estado:** aceptado
- **Fecha:** 2026-08-09

---

## Contexto

Aceptado el estilo de microservicios (ADR 001), hay que decidir **dónde se corta**.
El corte define qué datos posee cada servicio y, por lo tanto, qué conversaciones
son inevitables entre ellos.

- **¿Qué sustantivos del dominio tienen ciclo de vida propio?:** Habitación (inventario y estados), Orden (ciclo de vida de la falla), Técnico (datos de personal, turno y especialidad) y Notificación (avisos emitidos).
- **¿Cuáles cambian por razones distintas?:** Las habitaciones cambian por recepción/limpieza u órdenes; las órdenes cambian por la atención técnica; los técnicos cambian por turnos de trabajo; las notificaciones son efímeras por eventos.
- **¿Cuáles se consultan siempre juntos?:** Se consulta la disponibilidad de la habitación junto a la creación de la orden.

---

## Decisión

Cinco contextos:

| Servicio | Es dueño de | No sabe nada de |
|---|---|---|
| `habitaciones` | número, piso, tipo, estado | órdenes, técnicos |
| `ordenes` | orden, falla reportada, estado, fechas | especialidades, turnos |
| `tecnicos` | técnico, especialidad, turno, asignación | estado de la habitación |
| `notificaciones` | avisos emitidos | reglas de negocio de nadie |
| `gateway` | rutas | nada del dominio |

---

## Justificación de los cortes difíciles

### ¿Por qué la asignación vive en `tecnicos` y no en `ordenes`?

La regla de asignación depende de qué técnicos están en turno y de sus especialidades, información de la que solo `tecnicos` es dueño. Si viviera en `ordenes`, `ordenes` violaría los límites del contexto al tener que consultar o duplicar los cuadrantes y turnos de personal.

### ¿Por me el estado de la habitación no vive en `ordenes`?

Porque el estado de una habitación responde a múltiples operaciones del hotel ajenas a mantenimiento (como check-in, check-out, limpieza diaria). `habitaciones` debe ser la única fuente de verdad para el inventario del hotel.

### ¿Por qué `notificaciones` no tiene base de datos?

Se pierde el historial acumulado de notificaciones ante reinicios del contenedor. Se acepta porque las notificaciones sirven como un panel de avisos en tiempo real para el turno actual de recepción, manteniendo el microservicio simple y sin estado.

---

## Consecuencias

- **La orden guarda el número de habitación sin integridad referencial:** Si una habitación se elimina del catálogo en `habitaciones`, las órdenes conservan el número como un dato histórico desnormalizado. El dominio de órdenes no falla por claves foráneas inexistentes.
- **Consultar órdenes con el nombre del técnico obliga a unir datos de dos servicios:** Para evitar JOINs en tiempo de ejecución o llamadas HTTP de consulta (*query back*), el nombre del técnico se incluye explícitamente en el evento `orden.asignada` (*event-carried state transfer*). Así, el consumidor o vista dispone del dato sin costo adicional.

---

## Relacionado

- `../limites-descartados.md`
- `003-estrategia-comunicacion.md`

