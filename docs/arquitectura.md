# Arquitectura de mantenimiento-hotel

Documento maestro. Explica **qué** es el sistema y **cómo** están dispuestas sus
piezas. Las decisiones y sus alternativas descartadas viven en `adr/`.

---

## 1. Contexto del negocio

Hotel de 400 habitaciones. Preguntas guía a responder en esta sección:

- ¿Quién reporta una falla y desde dónde?
- ¿Qué se pierde si una habitación queda ocupada teniendo una falla activa?
- ¿Cuántas órdenes por día se esperan? (dimensiona si esto necesita colas o no)
- ¿Quién consulta el sistema: recepción, mantenimiento, gerencia?

---

## 2. Vista de contenedores

```
                         ┌──────────────┐
                         │      ui      │  React + Vite
                         └──────┬───────┘
                                │ HTTP
                         ┌──────▼───────┐
                         │   gateway    │  .NET 8 + YARP  ◀── único punto de entrada
                         └──┬───┬───┬───┘
              ┌─────────────┘   │   └─────────────┐
              │                 │                 │
       ┌──────▼──────┐   ┌──────▼──────┐   ┌──────▼─────────┐
       │ habitaciones│◀──│   ordenes   │   │    tecnicos    │
       │   .NET 8    │HTTP│   .NET 8   │   │    FastAPI     │
       └──────┬──────┘   └──────┬──────┘   └───────┬────────┘
              │                 │                  │
        ┌─────▼─────┐     ┌─────▼─────┐      ┌─────▼─────┐
        │ postgres  │     │ postgres  │      │ postgres  │
        └───────────┘     └───────────┘      └───────────┘
                                │                  │
                          ┌─────▼──────────────────▼─────┐
                          │  RabbitMQ  exchange topic    │
                          │       "hotel.eventos"        │
                          └──────────────┬───────────────┘
                                         │
                                 ┌───────▼────────┐
                                 │ notificaciones │  FastAPI, sin BD
                                 └────────────────┘
```

Preguntas guía:

- ¿Por qué el gateway no habla con `notificaciones` por cola sino por HTTP?
- ¿Por qué `notificaciones` no tiene base de datos y qué se pierde con eso?

---

## 3. Propiedad de los datos

Cada servicio es dueño exclusivo de su esquema. Nadie lee la tabla de otro.

| Dato | Dueño | Quién más lo necesita | Cómo lo obtiene |
|---|---|---|---|
| Habitación, estado | `habitaciones` | `ordenes` | HTTP sincrónico |
| Orden, estado | `ordenes` | `tecnicos`, `notificaciones` | evento `orden.creada` |
| Técnico, especialidad, turno | `tecnicos` | `notificaciones` | evento `orden.asignada` |
| Aviso enviado | `notificaciones` | nadie | — |

Pregunta guía: ¿qué datos se **duplican** a propósito en el payload de los eventos
y por qué eso no es un error?

---

## 4. Flujo completo del caso de uso

Describir paso a paso, indicando en cada paso si es sincrónico o asincrónico:

1. `POST /ordenes` llega al gateway y se enruta a `ordenes`.
2. `ordenes` llama a `habitaciones` para marcarla `FUERA_DE_SERVICIO`. **(sincrónico)**
3. `ordenes` persiste la orden en `ABIERTA` y publica `orden.creada`. **(asincrónico)**
4. `tecnicos` consume `orden.creada`, elige técnico por especialidad y publica
   `orden.asignada`.
5. `ordenes` consume `orden.asignada` y pasa la orden a `ASIGNADA`.
6. `notificaciones` consume los tres eventos y arma el aviso a recepción.
7. Al resolver, `ordenes` publica `orden.resuelta` y libera la habitación.

Preguntas guía:

- ¿En qué orden se hace el paso 2 y el paso 3, y qué pasa si falla el segundo?
- ¿Quién devuelve la habitación a `DISPONIBLE` y en qué paso exacto?

---

## 5. Resiliencia

- Única llamada sincrónica: timeout, reintento con espera creciente y circuit
  breaker. Detalle en `adr/003-estrategia-comunicacion.md`.
- Consumidores: qué pasa con un mensaje que falla (¿se reintenta?, ¿se descarta?).
- Idempotencia: cómo se evita asignar dos técnicos a la misma orden.

---

## 6. Observabilidad mínima

- Endpoint de salud en cada servicio.
- `PanelSalud` en la UI los consulta a través del gateway.

Pregunta guía: ¿qué debe reportar "sano" un servicio que depende de RabbitMQ?
