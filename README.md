# mantenimiento-hotel

Sistema de gestión de órdenes de mantenimiento para un hotel de 400 habitaciones,
construido como un conjunto de microservicios.

> **Estado:** esqueleto. Todos los archivos de código están vacíos y solo contienen
> un bloque de comentario que describe qué debe implementarse en ellos.

---

## 1. El caso de uso que da sentido al sistema

Un empleado reporta una falla (aire acondicionado, plomería, cerradura).
A partir de ese reporte:

1. Se crea una **orden de mantenimiento** en estado `ABIERTA`.
2. La **habitación se bloquea** automáticamente (`FUERA_DE_SERVICIO`).
3. Se **asigna un técnico** según su especialidad y su turno.
4. Se **notifica a recepción** de cada cambio relevante.

> **Pregunta guía a responder:** ¿por qué este caso de uso justifica microservicios y
> no un monolito? Responder en `docs/adr/001-estilo-arquitectonico.md`.

---

## 2. Servicios

| Servicio | Stack | Base de datos | Responsabilidad |
|---|---|---|---|
| `habitaciones` | C# / .NET 8 | PostgreSQL | Inventario y estado de las 400 habitaciones |
| `ordenes` | C# / .NET 8 | PostgreSQL | Ciclo de vida de la orden; orquesta el caso de uso |
| `tecnicos` | Python 3.12 / FastAPI | PostgreSQL | Técnicos, especialidades, turnos y asignación |
| `notificaciones` | Python 3.12 / FastAPI | — (memoria) | Consume eventos y "envía" avisos |
| `gateway` | C# / .NET 8 + YARP | — | Único punto de entrada al sistema |
| `ui` | React + Vite | — | Interfaz de recepción y mantenimiento |

---

## 3. Comunicación

### Sincrónica (una sola, a propósito)

```
ordenes  ──HTTP──▶  habitaciones     PUT /habitaciones/{numero}/fuera-de-servicio
```

Es la única llamada sincrónica del sistema porque bloquear la habitación es
**condición para que la orden exista**. Debe llevar **timeout**, **reintento** y
**circuit breaker**.

> **Pregunta guía:** ¿qué pasa con la orden si `habitaciones` está caído?
> Responder en `docs/adr/003-estrategia-comunicacion.md`.

### Asincrónica (RabbitMQ)

Exchange `hotel.eventos`, tipo **topic**.

| Evento | Productor | Consumidores |
|---|---|---|
| `orden.creada` | `ordenes` | `tecnicos`, `notificaciones` |
| `orden.asignada` | `tecnicos` | `notificaciones` |
| `orden.resuelta` | `ordenes` | `notificaciones` |

El detalle de cada evento vive en `docs/catalogo-eventos.md` y su forma exacta en
`contratos/*.json`.

---

## 4. Estructura del repositorio

```
mantenimiento-hotel/
├── docs/            decisiones de arquitectura y catálogo de eventos
├── contratos/       forma JSON de cada evento (contrato entre C# y Python)
└── servicios/       un directorio por servicio, cada uno con su README
```

---

## 5. Cómo se levanta

> **Pendiente de escribir** cuando `docker-compose.yml` esté implementado.

Pasos previstos:

1. Copiar `.env.example` a `.env` y ajustar valores.
2. `docker compose up --build`.
3. Entrar por el gateway (la UI y las APIs se acceden solo por ahí).

---

## 6. Preguntas guía del proyecto

1. ¿Cuáles son los límites de contexto y por qué se trazaron ahí?
   → `docs/adr/002-limites-contextos.md`
2. ¿Qué límites se consideraron y se descartaron?
   → `docs/limites-descartados.md`
3. ¿Cómo se mantiene la consistencia si no hay transacciones distribuidas?
   → `docs/adr/003-estrategia-comunicacion.md`
4. ¿Qué pasa si un evento se procesa dos veces?
   → `docs/catalogo-eventos.md`
