# Catálogo de eventos

Todos los eventos viajan por el exchange **`hotel.eventos`** de tipo **topic** en
RabbitMQ. La forma exacta de cada payload está en `contratos/*.json`; este
documento explica el **significado** de cada uno.

---

## Convenciones

- **Routing key** = nombre del evento, en minúsculas y separado por puntos.
- Cada evento lleva versión en el nombre del contrato (`.v1.json`).
- Un evento describe **algo que ya pasó**; nunca es una orden de hacer algo.

- **¿Qué se hace cuando un contrato tenga que cambiar?:** Si el cambio es retrocompatible (ej. agregar un campo opcional), se mantiene la versión actual `v1`. Si es un cambio de ruptura (breaking change), se publica una nueva versión del contrato `v2` (ej. `orden.creada.v2.json`) y se migran los productores y consumidores.
- **¿Los eventos llevan todos los datos necesarios o el consumidor pregunta por HTTP?:** Los eventos llevan todos los datos necesarios en su payload (*event-carried state transfer*). Esto evita llamadas HTTP síncronas de retorno (*query back*) y acoplamiento temporal entre servicios.

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

- **¿Qué pasa si no hay ningún técnico disponible?:** No se publica el evento `orden.asignada`. La orden permanece en estado `ABIERTA` en el servicio `ordenes` hasta que se registre/incorpore un técnico del turno correspondiente o se ejecute un reintento manual/periódico.

---

## `orden.asignada`

| | |
|---|---|
| **Productor** | `tecnicos` |
| **Consumidores** | `notificaciones` (y `ordenes`, para mover el estado) |
| **Contrato** | `contratos/orden.asignada.v1.json` |
| **Se publica cuando** | el asignador eligió un técnico concreto |

- **¿Por qué la asignación la decide `tecnicos` y no `ordenes`?:** Porque las reglas sobre turnos, disponibilidad y especialidades pertenecen exclusivamente al contexto de `tecnicos`. Si `ordenes` tomara la decisión, violaría la propiedad de los datos y tendría que duplicar la lógica de gestión de personal.

---

## `orden.resuelta`

| | |
|---|---|
| **Productor** | `ordenes` |
| **Consumidores** | `notificaciones` |
| **Contrato** | `contratos/orden.resuelta.v1.json` |
| **Se publica cuando** | la orden pasó a `RESUELTA` y la habitación volvió a `DISPONIBLE` |

- **¿La habitación se libera antes o después de publicar el evento?:** La habitación se libera ANTES de publicar el evento, de forma que al recibir `orden.resuelta`, el consumidor `notificaciones` asuma con certeza que la habitación ya está liberada en `habitaciones`.

---

## Colas y bindings

| Cola | Binding (routing key) | Servicio |
|---|---|---|
| `tecnicos.orden-creada` | `orden.creada` | `tecnicos` |
| `notificaciones.eventos` | `orden.*` | `notificaciones` |
| `ordenes.orden-asignada` | `orden.asignada` | `ordenes` |

- **`notificaciones` usa el comodín `orden.*` — ¿qué gana y qué riesgo corre?:** Gana la capacidad de suscribirse automáticamente a cualquier evento relativo a órdenes sin reconfigurar bindings. El riesgo es recibir eventos nuevos cuyo esquema no conoce y fallar en tiempo de ejecución si no cuenta con un manejador defensivo.

---

## Entrega e idempotencia

1. **Garantía asumida:** *At-least-once* (al menos una vez). La red o el broker pueden entregar mensajes duplicados.
2. **Idempotencia en `tecnicos`:** Si recibe dos veces `orden.creada`, no asigna dos técnicos. Se utiliza el campo `eventoId` como clave de idempotencia (junto con la comprobación de si `ordenId` ya está asignada) para ignorar el duplicado.
3. **Mensajes con fallos persistentes:** Se reintentan un número finito de veces. Si continúan fallando, se envían a una cola de mensajes muertos (Dead Letter Queue - DLQ) para no bloquear el consumo de la cola principal.

