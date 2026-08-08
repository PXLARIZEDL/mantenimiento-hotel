# ADR 001 — Estilo arquitectónico: microservicios

- **Estado:** propuesto
- **Fecha:** _(completar)_
- **Decide:** el equipo de arquitectura del proyecto

---

## Contexto

Preguntas guía a responder:

- ¿Qué problema concreto del hotel resuelve el sistema?
- ¿Cuántas personas lo van a mantener?
- ¿Hay partes que cambien a ritmos muy distintos entre sí?
- ¿Hay partes que necesiten escalar de forma independiente?
- ¿Qué restricciones impone que el proyecto sea universitario (tiempo, equipo,
  máquina de una sola persona)?

---

## Decisión

Se adopta un estilo de **microservicios** con cinco servicios y una UI, donde cada
servicio es dueño de su base de datos y se comunican por HTTP (una sola llamada) y
por eventos en RabbitMQ.

---

## Alternativas consideradas

### A. Monolito modular

- A favor: *responder*.
- En contra: *responder*.
- Por qué no se eligió: *responder*.

### B. Monolito con módulos y una sola base

- A favor: *responder*.
- En contra: *responder*.

### C. Microservicios (elegida)

- A favor: *responder*.
- En contra: *responder* — incluir el costo real: red, latencia, consistencia
  eventual, depuración distribuida.

---

## Consecuencias

Responder honestamente:

- ¿Qué se volvió más difícil con esta decisión?
- ¿Qué garantía se perdió respecto de un monolito? (transacciones)
- ¿Qué se gana concretamente en este dominio?
- ¿Cuándo habría que revisar esta decisión?

---

## Relacionado

- `002-limites-contextos.md` (dónde se cortó)
- `003-estrategia-comunicacion.md` (cómo se hablan)
- `../limites-descartados.md` (qué particiones se evaluaron)
