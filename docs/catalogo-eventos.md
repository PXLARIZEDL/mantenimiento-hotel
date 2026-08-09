# Catálogo de eventos

Todos los eventos viajan por el exchange **`hotel.eventos`** de tipo **topic** en
RabbitMQ. La forma exacta de cada payload está en `contratos/*.json`; este
documento explica el **significado** de cada uno.

---

## Resumen

Los tres eventos, de un vistazo. La columna *payload* lista solo los campos de
negocio: los tres llevan además el sobre común `eventoId`, `tipoEvento`,
`version` y `ocurridoEn`.

| Evento | Productor | Consumidores | Payload |
|---|---|---|---|
| `orden.creada` | `ordenes` | `tecnicos`, `notificaciones` | `ordenId`, `habitacionId`, `habitacionNumero`, `tipoFalla`, `descripcion`, `prioridad`, `reportadoPor` |
| `orden.asignada` | `tecnicos` | `notificaciones`, `ordenes` | `ordenId`, `tecnicoId`, `tecnicoNombre`, `especialidad` |
| `orden.resuelta` | `ordenes` | `notificaciones` | `ordenId`, `habitacionId`, `resueltoPor`, `notaCierre` |

Los tres archivos de `contratos/` describen **el mismo flujo**: la orden
`b7e2f4a0-…5d60` sobre la habitación `314` (`c8d3a5b1-…9005`), reportada a las
`14:32:10Z`, asignada a Luis Ramírez a las `14:32:12Z` y cerrada a las `17:05:44Z`.

---

## Convenciones

- **Routing key** = nombre del evento, en minúsculas y separado por puntos.
- Cada evento lleva versión en el nombre del contrato (`.v1.json`).
- Un evento describe **algo que ya pasó**; nunca es una orden de hacer algo.
- **camelCase en el cable**, sin excepciones. C# ya serializa así; los consumidores
  Python declaran alias en vez de usar sus nombres `snake_case` internos.

### ¿Qué se hace cuando un contrato tenga que cambiar?

Depende de si el cambio rompe a los consumidores existentes:

| Tipo de cambio | Qué se hace |
|---|---|
| Agregar un campo que el consumidor puede ignorar | **Campo opcional dentro de `v1`.** No se sube versión. |
| Renombrar o quitar un campo | **`v2`:** archivo `.v2.json` nuevo y routing key `orden.creada.v2`. |
| Cambiar el tipo de un campo o su lista de valores permitidos | **`v2`.** |

Regla que lo hace funcionar: **el consumidor ignora los campos que no conoce** en
vez de fallar. Sin eso, hasta agregar un campo opcional rompe a alguien.

Durante una migración a `v2` el productor publica **las dos versiones** hasta que
todos los consumidores se hayan movido; recién ahí se retira `v1`. `contratos/`
está bajo CODEOWNERS: cualquier cambio pasa por revisión antes de mezclarse.

### ¿Los eventos llevan todos los datos, o el consumidor vuelve a preguntar por HTTP?

**Los eventos son autocontenidos.** La única llamada sincrónica del sistema es
`ordenes → habitaciones` para bloquear la habitación (ADR 003); ningún consumidor
de eventos hace HTTP para completar un payload.

Por qué: `notificaciones` tiene que poder redactar el aviso aunque `tecnicos` y
`habitaciones` estén caídos. Si tuviera que preguntar, un evento asincrónico se
volvería dependiente en tiempo real de dos servicios más, y se pierde justamente
lo que se buscaba al desacoplarlos.

El costo aceptado es **duplicación deliberada**: `tecnicoNombre` viaja dentro de
`orden.asignada` y `habitacionNumero` dentro de `orden.creada`, aunque el dueño
del dato sea otro servicio. Son copias del valor *en el momento del evento*, no
la fuente de verdad — si el técnico luego cambia de nombre, el aviso ya emitido
no se corrige.

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

**¿Qué pasa si no hay ningún técnico disponible?** El contrato es claro en lo
inmediato: `tecnicos` **no publica** `orden.asignada`, y la orden se queda
`ABIERTA` — no se inventa una asignación ni se falla el mensaje, porque no hay
nada que reintentar (que no haya técnico en turno no es un error transitorio de
infraestructura).

Lo que sigue **abierto** es qué pasa después. Las dos opciones sobre la mesa están
anotadas en el contrato y deben decidirse en ADR 003:

1. un evento `orden.sin-tecnico` en `v2`, que `notificaciones` use para escalar a
   recepción; o
2. reintentar la asignación al iniciar el próximo turno.

Mientras no se decida, una orden sin técnico queda visible solo porque sigue
`ABIERTA` en la UI. **Es la laguna conocida de `v1`.**

---

## `orden.asignada`

| | |
|---|---|
| **Productor** | `tecnicos` |
| **Consumidores** | `notificaciones` (y `ordenes`, para mover el estado) |
| **Contrato** | `contratos/orden.asignada.v1.json` |
| **Se publica cuando** | el asignador eligió un técnico concreto |

Es el **único evento producido por Python y consumido por C#**, y por eso el que
más riesgo tiene de romperse por serialización.

**¿Por qué la asignación la decide `tecnicos` y no `ordenes`, si `ordenes` orquesta
el caso de uso?** Porque la regla de asignación depende de **especialidad y turno**,
y esos datos viven en `tecnicos` (ADR 002 lo dice explícitamente: `ordenes` *no*
conoce especialidades ni turnos).

Si la decidiera `ordenes`, tendría que replicar el catálogo de especialidades y el
calendario de turnos, y volver a desplegarse cada vez que esas reglas cambien —
que es exactamente el acoplamiento que ADR 002 intenta evitar.

La distinción que sostiene el diseño: `ordenes` orquesta el **ciclo de vida** de la
orden (abierta → asignada → resuelta), no la **regla de negocio** de a quién le
toca. Recibe el resultado como un hecho ya ocurrido y actualiza su estado.

---

## `orden.resuelta`

| | |
|---|---|
| **Productor** | `ordenes` |
| **Consumidores** | `notificaciones` |
| **Contrato** | `contratos/orden.resuelta.v1.json` |
| **Se publica cuando** | la orden pasó a `RESUELTA` y la habitación volvió a `DISPONIBLE` |

**¿La habitación se libera antes o después de publicar el evento?** **Antes.** El
contrato define el evento como el anuncio de un hecho ya consumado: quien lo
recibe puede asumir que la habitación **ya** está `DISPONIBLE`. Publicar primero y
liberar después permitiría que recepción reciba "habitación lista" y encuentre la
habitación todavía bloqueada — un aviso que miente.

El costo de este orden es el inverso, y hay que asumirlo: si el proceso muere
entre liberar la habitación y publicar el evento, la habitación queda libre pero
**nadie se entera**. Es preferible a lo contrario: una habitación libre sin aviso
se detecta al mirar la UI; un aviso falso ya llegó a recepción.

> Esta decisión debe quedar **formalizada en ADR 003**, que sigue en estado
> *propuesto* — el contrato la asume, el ADR todavía no la registra. Ver también
> la pregunta 2 de "Consistencia sin transacciones distribuidas" en ese ADR.

---

## Colas y bindings

| Cola | Binding (routing key) | Servicio |
|---|---|---|
| `tecnicos.orden-creada` | `orden.creada` | `tecnicos` |
| `notificaciones.eventos` | `orden.*` | `notificaciones` |
| `ordenes.orden-asignada` | `orden.asignada` | `ordenes` |

**`notificaciones` usa el comodín `orden.*` — ¿qué gana y qué riesgo corre?**

Gana que **un evento nuevo le llega sin tocar nada**: ni binding, ni configuración,
ni redespliegue. Es coherente con su rol, porque `notificaciones` es el consumidor
que quiere enterarse de *todo* lo que le pasa a una orden.

Arriesga exactamente lo mismo por el otro lado: cuando se agregue `orden.sin-tecnico`
o cualquier `orden.X`, **empieza a recibirlo aunque no sepa manejarlo**. Sin
protección, ese mensaje falla, se reencola, vuelve a fallar y termina bloqueando
la cola con un evento que nadie pidió.

Mitigación exigida al consumidor: ante un `tipoEvento` desconocido debe hacer
**ack y descartar con un log**, nunca lanzar excepción. Es decir, el comodín es
aceptable *solo* si el consumidor trata "no sé qué es esto" como un caso normal y
no como un error.

---

## Entrega e idempotencia

**1. ¿Qué garantía se asume?**

**At-least-once.** Los consumidores usan ack manual: si el consumidor muere después
de procesar pero antes del ack, RabbitMQ reentrega el mensaje. Se prefiere sobre
at-most-once porque perder un `orden.creada` significa una orden que nunca se
asigna a nadie — un mensaje duplicado es un problema que se puede resolver en el
consumidor; uno perdido, no.

La contrapartida es obligatoria: **todo consumidor debe ser idempotente.**

**2. Si `tecnicos` recibe dos veces `orden.creada`, ¿asigna dos técnicos?**

No. La clave de descarte es **`eventoId`**.

`tecnicos` mantiene una tabla de eventos ya procesados (`servicios/tecnicos/base_datos.py`)
y comprueba `eventoId` **antes** de asignar: si ya está registrado, hace ack y no
hace nada más. `ordenes` y `notificaciones` siguen el mismo patrón.

`ordenId` **no** sirve como clave, y por eso los contratos separan los dos campos:
una misma orden produce `orden.creada`, `orden.asignada` y `orden.resuelta`, todos
con el mismo `ordenId`. Filtrar por `ordenId` descartaría eventos legítimos.

El registro del `eventoId` procesado y el efecto (la asignación) deben guardarse
**en la misma transacción local**; si no, un fallo entre ambos reabre la ventana
del duplicado.

**3. ¿Qué pasa con un mensaje que falla siempre?**

Hay que distinguir dos casos, porque el reintento solo ayuda en uno:

| Caso | Ejemplo | Qué se hace |
|---|---|---|
| **Transitorio** | la base de datos no responde | Reintentar con espera creciente, hasta un máximo. |
| **Permanente** | `tipoFalla` no existe, JSON inválido, campo faltante | **No se reintenta.** Va directo a la DLQ. |

Agotados los reintentos, el mensaje pasa a una **cola de mensajes muertos** (DLQ)
y se emite una alerta. Nunca se reintenta indefinidamente: un mensaje envenenado
en reintento infinito bloquea la cola y detiene el procesamiento de todos los
mensajes válidos que vienen detrás.

Falta definir (junto con los valores de resiliencia de ADR 003): **cuántos
reintentos, con qué espera, y quién revisa la DLQ**.

---

## Pendientes que este catálogo deja abiertos

Todos requieren decisión del equipo, no solo redacción:

1. **Caso sin técnico** — `orden.sin-tecnico` en `v2` vs. reintento por turno.
   Decidir en ADR 003.
2. **Orden liberar/publicar en `orden.resuelta`** — decidido aquí y en el
   contrato, falta registrarlo en ADR 003 (que sigue *propuesto*).
3. **Política de reintentos y DLQ** — número, espera y responsable.
4. **Desalineación de valores de dominio** — `contratos/` ya usa
   `AIRE_ACONDICIONADO`, `PLOMERIA`, `CERRADURA`, `ELECTRICIDAD`, pero
   `servicios/tecnicos/` y `servicios/ui/` todavía documentan
   `AIRE | PLOMERIA | CERRADURA`. **Hay que alinearlos antes de implementar**;
   el contrato manda.
