# `ui`

React + Vite, servida por nginx

---

## Qué hace

Interfaz para recepción y mantenimiento. Cinco pantallas, una por cada cosa que
el sistema sabe hacer.

---

## De qué datos es dueña

**De ninguno.** No guarda nada, no valida reglas de negocio y no toma decisiones.
Todo lo que muestra lo pide al backend y todo lo que hace lo pide como petición.

---

## Con quién habla

| Dirección | Con quién | Cómo |
|---|---|---|
| Saliente | **solo el `gateway`** | HTTP, por rutas relativas |
| RabbitMQ | **nunca** | el navegador no habla con la cola |

Nunca llama directamente a `habitaciones`, `ordenes`, `tecnicos` ni
`notificaciones`. En desarrollo lo garantiza el proxy de `vite.config.js`; en
producción, `nginx.conf`. Las rutas son las mismas en ambos entornos.

---

## Pantallas

| Componente | Qué muestra | Backend |
|---|---|---|
| `Habitaciones` | estado de los 400 cuartos | `habitaciones` |
| `NuevaOrden` | formulario para reportar una falla | `ordenes` |
| `ListaOrdenes` | ciclo de vida de las órdenes | `ordenes` |
| `BandejaNotificaciones` | avisos a recepción | `notificaciones` |
| `PanelSalud` | estado de los cinco servicios | `gateway` (agregado) |

---

## Lo que la UI **no** hace

- No elige el técnico: eso lo decide `tecnicos` al consumir un evento.
- No cambia el estado de una habitación a mano: lo mueve `ordenes`.
- No revalida reglas del backend.

---

## Refresco

La UI **no escucha RabbitMQ**. Una orden aparece `ABIERTA` y pasa a `ASIGNADA`
segundos después, cuando el evento se procesó. Por eso `ListaOrdenes`,
`BandejaNotificaciones` y `PanelSalud` necesitan refresco periódico o un botón
de recarga visible.

Pregunta guía: ¿sondeo cada N segundos, o se agrega SSE/WebSocket más adelante?
Para la versión 1, sondeo.

---

## Cómo se levanta

```
docker compose up ui          # nginx sirviendo el build
npm run dev                   # desarrollo, con proxy hacia el gateway
```
