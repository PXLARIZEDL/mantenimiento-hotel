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

Los tres mecanismos exigidos, con los valores a definir:

| Mecanismo | Qué evita | Valor propuesto |
|---|---|---|
| **Timeout** | quedarse colgado esperando | 3 segundos |
| **Reintento** | fallo transitorio de red | 3 reintentos (espera de 1s, 2s, 4s) |
| **Circuit breaker** | insistir contra un servicio caído | Umbral: 5 fallos consecutivos; Abierto durante: 30 segundos |

- **¿Se reintenta un `409 Conflict`? ¿Y un `500`?:** Un `409 Conflict` no se reintenta (es un error de negocio o de estado no reintentable). Un `500 Internal Server Error` o fallo de conexión sí se reintenta por ser potencialmente transitorio.
- **Con el circuito abierto, ¿qué responde `ordenes`?:** Responde de inmediato un `503 Service Unavailable` sin intentar realizar la solicitud HTTP a `habitaciones`.
- **¿El reintento va por dentro o por fuera del circuit breaker?:** El reintento se ejecuta por DENTRO del circuit breaker (las peticiones individuales alimentan el contador del circuito).
- **Idempotencia en el reintento de bloqueo:** `PUT /habitaciones/{numero}/fuera-de-servicio` es una operación **idempotente**. Ejecutarla múltiples veces produce exactamente el mismo resultado final.

---

## Consistencia sin transacciones distribuidas

No hay transacción que abarque `ordenes` + `habitaciones` + `tecnicos`.

1. **Si la habitación se bloqueó pero la orden no se pudo guardar:** `ordenes` realiza una llamada HTTP de compensación a `habitaciones` para restaurar o liberar el estado de la habitación, retornando un error al usuario.
2. **Si la orden se guardó pero el evento no se pudo publicar:** `ordenes` utiliza un mecanismo de reintento en segundo plano o patrón outbox para publicar el evento `orden.creada` en RabbitMQ de forma eventual.
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

