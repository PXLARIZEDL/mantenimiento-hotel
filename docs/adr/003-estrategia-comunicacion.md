# ADR 003 — Estrategia de comunicación

- **Estado:** aceptado
- **Fecha:** 2026-08-09

---

## Contexto

Definidos los cinco contextos (ADR 002), hay que decidir **cómo hablan**. La regla
que se adopta es: *asincrónico por defecto, sincrónico solo cuando la respuesta es
condición para continuar*.

---

## Decisión

### Una sola llamada sincrónica

```
ordenes ──HTTP──▶ habitaciones
```

Bloquear la habitación es condición para que la orden exista: si no se puede
bloquear, la orden **no debe crearse**.

- **¿Qué código HTTP devuelve `ordenes` al usuario si `habitaciones` no responde?:** Devuelve un código HTTP `503 Service Unavailable`.
- **¿Se crea la orden "pendiente" y se reintenta después, o se rechaza?:** Se rechaza inmediatamente. Justificación: el negocio requiere la garantía estricta de que la habitación esté bloqueada como `FUERA_DE_SERVICIO` en `habitaciones` antes de confirmar la orden; crear una orden pendiente permitiría que la habitación sea ocupada por un huésped por error.

### Todo lo demás por eventos

Exchange `hotel.eventos` (topic). Ver `../catalogo-eventos.md`.

- **¿Por qué la asignación de técnico sí puede ser asincrónica?:** Porque una vez bloqueada la habitación y registrada la orden `ABIERTA`, la operación crítica del negocio está protegida (la habitación no se comercializará). La búsqueda y asignación del técnico puede demorar unos segundos sin comprometer la seguridad ni el inventario del hotel.

---

## Resiliencia de la llamada sincrónica

Los tres mecanismos exigidos. Estos son los valores **implementados**: viven en
`servicios/ordenes/appsettings.json` → `Habitaciones:Resiliencia`, y `Program.cs`
los traduce en las políticas del `HttpClient` tipado. Ninguno está escrito en el
código.

| Mecanismo | Qué evita | Valor | Clave de configuración |
|---|---|---|---|
| **Timeout** | quedarse colgado esperando | 3 s por intento | `TimeoutSegundos` |
| **Reintento** | fallo transitorio de red | 3 reintentos, espera exponencial con jitter desde 200 ms | `Reintentos`, `EsperaBaseMilisegundos` |
| **Circuit breaker** | insistir contra un servicio caído | abre con 50 % de fallos, mínimo 8 llamadas en 30 s; 30 s abierto | `UmbralFallosCircuito`, `MinimoLlamadasCircuito`, `SegundosVentanaMuestreo`, `SegundosCircuitoAbierto` |

Dos detalles que importan y no son obvios:

- **Jitter en la espera.** Sin él, todas las instancias de `ordenes` reintentarían
  en bloque y volverían a tumbar a `habitaciones` justo cuando se recupera.
- **El breaker mide proporción, no fallos consecutivos.** Se abre con el 50 % de
  fallos sobre una ventana de 30 s, exigiendo un mínimo de 8 llamadas. El mínimo
  existe para que dos fallos aislados en un momento de poco tráfico no abran el
  circuito. Contar fallos *consecutivos* se descartó porque una llamada exitosa
  suelta entre fallos reiniciaría el contador y el circuito nunca abriría.

- **¿Se reintenta un `409 Conflict`? ¿Y un `500`?:** Un `409 Conflict` no se reintenta (es un error de negocio o de estado no reintentable). Un `500 Internal Server Error` o fallo de conexión sí se reintenta por ser potencialmente transitorio. Tampoco se reintentan `400` ni `404`, por lo mismo: la respuesta no cambia por insistir.
- **Con el circuito abierto, ¿qué responde `ordenes`?:** Responde de inmediato un `503 Service Unavailable` sin intentar realizar la solicitud HTTP a `habitaciones`. Se elige `503` y no `500` porque comunica "la dependencia no está disponible, reintentá" en vez de "algo se rompió de este lado".
- **¿El reintento va por dentro o por fuera del circuit breaker?:** El reintento va **por FUERA**, envolviendo al breaker. El orden efectivo es `reintento → circuit breaker → timeout por intento`, de modo que **cada intento individual atraviesa el breaker y alimenta su contador**. Si fuera al revés, el breaker vería un único fallo por cada tanda completa de reintentos y prácticamente nunca llegaría a abrirse.
- **Idempotencia en el reintento de bloqueo:** `PUT /habitaciones/{numero}/fuera-de-servicio` es una operación **idempotente**. Ejecutarla múltiples veces produce exactamente el mismo resultado final. Por eso `ordenes` genera el `ordenId` **antes** de la primera llamada y lo envía en todos los intentos: es lo que permite al otro lado reconocer un reintento como la misma operación y no como una nueva.

---

## Consistencia sin transacciones distribuidas

No hay transacción que abarque `ordenes` + `habitaciones` + `tecnicos`.

1. **Si la habitación se bloqueó pero la orden no se pudo guardar:** `ordenes` realiza una llamada HTTP de compensación a `habitaciones` para restaurar o liberar el estado de la habitación, retornando un error al usuario.
2. **Si la orden se guardó pero el evento no se pudo publicar:** la orden ya está guardada y es válida, así que **no se deshace**. Se registra un log `Critical` y se responde `201`. Lo que se pierde es el disparo de la asignación: la orden queda `ABIERTA` sin técnico hasta que alguien la reintente.

   Es el problema clásico de la **doble escritura** (base + broker), y la solución correcta es un **outbox**: guardar el evento en la misma transacción que la orden y despacharlo con un proceso aparte. **Todavía NO está implementado** — está anotado como pendiente en `servicios/ordenes/README.md`. Documentarlo como si funcionara sería mentir sobre el estado del sistema.
3. **Ventana de inconsistencia:** Se acepta una ventana de inconsistencia eventual de pocos milisegundos a segundos entre la publicación de eventos y la reacción de `tecnicos` y `notificaciones`.

---

## Alternativas descartadas

- **Todo sincrónico (REST entre todos):** Descartado por el acoplamiento temporal y la cascada de fallos: si `notificaciones` o `tecnicos` fallaran, impedirían crear órdenes de mantenimiento.
- **Todo asincrónico (incluida la habitación):** Descartado porque la habitación debe quedar bloqueada sincrónicamente antes de responder éxito al usuario.
- **Transacción distribuida / two-phase commit (2PC):** Descartado por su alta complejidad, baja latencia, bloqueo de recursos y falta de soporte sencillo en arquitecturas políglotas.

---

## Relacionado

- `../catalogo-eventos.md`
- `../../contratos/*.json`
- `../../servicios/ordenes/Clientes/HabitacionesClient.cs` (implementa esta decisión)

