# Servicio `ordenes`

C# / .NET 8 + PostgreSQL + RabbitMQ

---

## Qué hace

Es el **orquestador** del caso de uso. Recibe el reporte de la falla, se asegura
de que la habitación quede bloqueada, guarda la orden y avisa al resto del sistema
publicando eventos.

Gestiona el ciclo de vida: `ABIERTA` → `ASIGNADA` → `RESUELTA`.

---

## De qué datos es dueño

| Dato | Detalle |
|---|---|
| Orden | id, tipo de falla, descripción, prioridad, quién reportó |
| Estado | `ABIERTA` / `ASIGNADA` / `RESUELTA` + fechas de cada transición |
| Habitación afectada | solo el **número**; el estado del cuarto es de `habitaciones` |
| Técnico asignado | id y nombre **copiados** del evento `orden.asignada` |

La copia del nombre del técnico es deliberada: evita que la UI tenga que
consultar dos servicios para pintar una lista.

---

## Con quién habla

| Dirección | Con quién | Cómo |
|---|---|---|
| Saliente | `habitaciones` | **HTTP sincrónico** — bloquear / liberar |
| Publica | `orden.creada`, `orden.resuelta` | RabbitMQ, exchange `hotel.eventos` |
| Consume | `orden.asignada` | cola `ordenes.orden-asignada` |
| Entrante | `gateway` → `ui` | HTTP |

Con `tecnicos` y `notificaciones` **no se habla por HTTP**: solo por eventos.

---

## El flujo de `POST /ordenes`, paso a paso

```
1. valida la petición
2. PUT habitaciones/{n}/fuera-de-servicio   ← timeout + reintento + breaker
   └─ si falla definitivamente ⇒ 503 y NO se crea la orden
3. guarda la orden en ABIERTA
4. publica orden.creada
```

**Pregunta guía sin responder:** si el paso 3 falla después del 2, la habitación
queda bloqueada sin orden. ¿Se compensa liberándola? ¿Se acepta y se limpia
manualmente? Decidir en `docs/adr/003-estrategia-comunicacion.md`.

---

## API prevista

| Método | Ruta |
|---|---|
| `POST` | `/ordenes` |
| `GET` | `/ordenes` (filtro por estado y habitación) |
| `GET` | `/ordenes/{id}` |
| `PUT` | `/ordenes/{id}/resolver` |
| `GET` | `/salud` |

---

## Cómo se levanta

```
docker compose up ordenes
```

Depende de `db-ordenes`, `rabbitmq` y — en tiempo de ejecución — de
`habitaciones`. Debe arrancar **aunque `habitaciones` esté caído**: el circuit
breaker existe precisamente para eso.

Variables que necesita (`.env.example`): conexión a PostgreSQL, credenciales de
RabbitMQ, URL base de habitaciones y los parámetros de resiliencia.

---

## Preguntas guía pendientes

1. ¿Qué código HTTP devuelve `POST /ordenes` con el circuito abierto?
2. ¿Se puede resolver una orden que nunca fue asignada?
3. Si llegan dos veces `orden.asignada`, ¿qué campo evita asignar dos veces?
