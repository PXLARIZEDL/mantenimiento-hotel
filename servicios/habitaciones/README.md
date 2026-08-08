# Servicio `habitaciones`

C# / .NET 8 + PostgreSQL

---

## Qué hace

Mantiene el inventario de las 400 habitaciones del hotel y, sobre todo, **su
estado**: `DISPONIBLE`, `OCUPADA`, `FUERA_DE_SERVICIO`.

Es un servicio deliberadamente simple y **pasivo**: responde preguntas y aplica
cambios de estado que otros le piden. No decide nada sobre mantenimiento.

---

## De qué datos es dueño

| Dato | Detalle |
|---|---|
| Habitación | número (1..400), piso, tipo |
| Estado | `DISPONIBLE` / `OCUPADA` / `FUERA_DE_SERVICIO` |
| Motivo del bloqueo | identificador de la orden que la dejó fuera de servicio |

**Nadie más** escribe estos datos. Ningún otro servicio lee esta base de datos.

---

## Con quién habla

| Dirección | Con quién | Cómo |
|---|---|---|
| Entrante | `ordenes` | HTTP sincrónico — bloquear y liberar la habitación |
| Entrante | `gateway` → `ui` | HTTP — consultas de la pantalla de habitaciones |
| Saliente | **nadie** | — |
| RabbitMQ | **no participa** en la versión 1 | — |

> Que no llame a nadie es intencional: es el servicio del que otros dependen, así
> que se mantiene sin dependencias propias para que su disponibilidad sea alta.

---

## API prevista

| Método | Ruta | Quién la usa |
|---|---|---|
| `GET` | `/habitaciones` | UI |
| `GET` | `/habitaciones/{numero}` | UI |
| `PUT` | `/habitaciones/{numero}/fuera-de-servicio` | **`ordenes`** |
| `PUT` | `/habitaciones/{numero}/disponible` | `ordenes` |
| `GET` | `/salud` | `PanelSalud` de la UI |

Los dos `PUT` deben ser **idempotentes**: `ordenes` los reintenta.

---

## Cómo se levanta

```
docker compose up habitaciones
```

Depende de `db-habitaciones`. No depende de RabbitMQ ni de ningún otro servicio.

Variables que necesita (ver `.env.example`): cadena de conexión a PostgreSQL.

---

## Preguntas guía pendientes

1. Si una habitación tiene **dos** órdenes abiertas y se resuelve una, ¿se libera?
2. ¿Qué pasa si `ordenes` pide bloquear una habitación que ya está bloqueada por
   otra orden? ¿`409` o `200`?
3. ¿Quién marca una habitación como `OCUPADA`? (¿existe ese flujo en el proyecto?)
