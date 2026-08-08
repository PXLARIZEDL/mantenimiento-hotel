# Catálogo de eventos

Todos los eventos viajan por el exchange **`hotel.eventos`** de tipo **topic** en
RabbitMQ. La forma exacta de cada payload está en `contratos/*.json`; este
documento explica el **significado** de cada uno.

---

## Convenciones

- **Routing key** = nombre del evento, en minúsculas y separado por puntos.
- Cada evento lleva versión en el nombre del contrato (`.v1.json`).
- Un evento describe **algo que ya pasó**; nunca es una orden de hacer algo.

Preguntas guía:

- ¿Qué se hace cuando un contrato tenga que cambiar? ¿`v2` o campo opcional?
- ¿Los eventos llevan todos los datos que el consumidor necesita, o el consumidor
  vuelve a preguntar por HTTP? (decidir y justificar)

---

## `orden.creada`

| | |
|---|---|
| **Productor** | `ordenes` |
| **Consumidores** | `tecnicos`, `notificaciones` |
| **Contrato** | `contratos/orden.creada.v1.json` |
| **Se publica cuando** | la orden quedó persistida en estado `ABIERTA` y la habitación ya está `FUERA_DE_SERVICIO` |

Qué hace cada consumidor:

- `tecnicos`: busca un técnico de la especialidad requerida que esté en turno y
  publica `orden.asignada`.
- `notificaciones`: avisa a recepción que la habitación quedó bloqueada.

Pregunta guía: ¿qué pasa si **no hay** ningún técnico disponible?

---

## `orden.asignada`

| | |
|---|---|
| **Productor** | `tecnicos` |
| **Consumidores** | `notificaciones` (y `ordenes`, para mover el estado) |
| **Contrato** | `contratos/orden.asignada.v1.json` |
| **Se publica cuando** | el asignador eligió un técnico concreto |

Pregunta guía: ¿por qué la asignación la decide `tecnicos` y no `ordenes`, si
`ordenes` es quien orquesta el caso de uso?

---

## `orden.resuelta`

| | |
|---|---|
| **Productor** | `ordenes` |
| **Consumidores** | `notificaciones` |
| **Contrato** | `contratos/orden.resuelta.v1.json` |
| **Se publica cuando** | la orden pasó a `RESUELTA` y la habitación volvió a `DISPONIBLE` |

Pregunta guía: ¿la habitación se libera antes o después de publicar el evento?

---

## Colas y bindings

| Cola | Binding (routing key) | Servicio |
|---|---|---|
| `tecnicos.orden-creada` | `orden.creada` | `tecnicos` |
| `notificaciones.eventos` | `orden.*` | `notificaciones` |
| `ordenes.orden-asignada` | `orden.asignada` | `ordenes` |

Pregunta guía: `notificaciones` usa el comodín `orden.*` — ¿qué gana y qué riesgo
corre cuando se agregue un evento nuevo?

---

## Entrega e idempotencia

Responder aquí:

1. ¿Qué garantía se asume: *at-least-once* o *at-most-once*?
2. Si `tecnicos` recibe dos veces `orden.creada`, ¿asigna dos técnicos? ¿Qué campo
   se usa como clave para descartar el duplicado?
3. ¿Qué pasa con un mensaje que falla siempre? (política de reintento / descarte)
