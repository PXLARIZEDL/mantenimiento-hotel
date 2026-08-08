# ADR 002 — Límites de los contextos

- **Estado:** propuesto
- **Fecha:** _(completar)_

---

## Contexto

Aceptado el estilo de microservicios (ADR 001), hay que decidir **dónde se corta**.
El corte define qué datos posee cada servicio y, por lo tanto, qué conversaciones
son inevitables entre ellos.

Preguntas guía:

- ¿Qué sustantivos del dominio tienen ciclo de vida propio?
- ¿Cuáles cambian por razones distintas?
- ¿Cuáles se consultan siempre juntos?

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

*Responder.* Pista: la regla de asignación depende de especialidad y turno, datos
que solo `tecnicos` posee. Si viviera en `ordenes`, `ordenes` tendría que leer o
copiar esos datos.

### ¿Por qué el estado de la habitación no vive en `ordenes`?

*Responder.* Pista: una habitación cambia de estado por razones ajenas al
mantenimiento (check-in, check-out).

### ¿Por qué `notificaciones` no tiene base de datos?

*Responder.* Pista: ¿qué se pierde al reiniciar el contenedor y por qué se acepta?

---

## Consecuencias

- La orden guarda el **número de habitación**, no una referencia con integridad
  referencial. Responder: ¿qué pasa si esa habitación se elimina?
- Consultar "todas las órdenes con el nombre del técnico" obliga a juntar datos de
  dos servicios. Responder: ¿dónde se hace ese *join* y quién lo paga?

---

## Relacionado

- `../limites-descartados.md`
- `003-estrategia-comunicacion.md`
