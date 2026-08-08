# ADR 003 — Estrategia de comunicación

- **Estado:** propuesto
- **Fecha:** _(completar)_

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

Preguntas guía:

- ¿Qué código HTTP devuelve `ordenes` al usuario si `habitaciones` no responde?
- ¿Se crea la orden "pendiente" y se reintenta después, o se rechaza? Justificar.

### Todo lo demás por eventos

Exchange `hotel.eventos` (topic). Ver `../catalogo-eventos.md`.

Pregunta guía: ¿por qué la asignación de técnico **sí** puede ser asincrónica, si
una orden sin técnico no sirve de nada?

---

## Resiliencia de la llamada sincrónica

Los tres mecanismos exigidos, con los valores a definir:

| Mecanismo | Qué evita | Valor propuesto |
|---|---|---|
| **Timeout** | quedarse colgado esperando | _(definir, en segundos)_ |
| **Reintento** | fallo transitorio de red | _(definir cuántos y con qué espera)_ |
| **Circuit breaker** | insistir contra un servicio caído | _(definir umbral y tiempo abierto)_ |

Preguntas guía:

- ¿Se reintenta un `409 Conflict`? ¿Y un `500`? ¿Cuáles son *reintentables*?
- Con el circuito **abierto**, ¿qué responde `ordenes`?
- ¿El reintento va por dentro o por fuera del circuit breaker? (el orden importa)
- Si se reintenta el bloqueo de habitación, ¿la operación es idempotente? Si no lo
  es, ¿qué pasa al aplicarla dos veces?

---

## Consistencia sin transacciones distribuidas

No hay transacción que abarque `ordenes` + `habitaciones` + `tecnicos`.

Responder:

1. ¿Qué se hace si la habitación se bloqueó pero la orden no se pudo guardar?
2. ¿Qué se hace si la orden se guardó pero el evento no se pudo publicar?
3. ¿Se acepta una ventana de inconsistencia? ¿De cuánto y quién la nota?

---

## Alternativas descartadas

- **Todo sincrónico (REST entre todos):** *responder por qué no*.
- **Todo asincrónico (incluida la habitación):** *responder por qué no*.
- **Transacción distribuida / two-phase commit:** *responder por qué no*.

---

## Relacionado

- `../catalogo-eventos.md`
- `../../contratos/*.json`
- `../../servicios/ordenes/Clientes/HabitacionesClient.cs` (implementa esta decisión)
