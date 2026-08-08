# Servicio `notificaciones`

Python 3.12 / FastAPI — **sin base de datos**

---

## Qué hace

Escucha todo lo que le pasa a una orden y lo convierte en un aviso legible para
recepción. Los avisos se "envían" guardándolos **en memoria**; la UI los lee de
ahí.

Es el servicio más simple del sistema y el único que no persiste nada.

---

## De qué datos es dueño

| Dato | Detalle |
|---|---|
| Aviso | id, tipo de evento, habitación, título, cuerpo, fecha, leído |

**Se pierden al reiniciar el contenedor.** Es una decisión consciente, no un
descuido: ver `docs/adr/002-limites-contextos.md` y
`docs/limites-descartados.md` (punto 2).

---

## Con quién habla

| Dirección | Con quién | Cómo |
|---|---|---|
| Consume | `orden.creada`, `orden.asignada`, `orden.resuelta` | cola `notificaciones.eventos`, binding `orden.*` |
| Entrante | `gateway` → `ui` | HTTP |
| Saliente | **nadie** | — |
| Publica | **nada** | — |

Es un consumidor puro. Todo lo que necesita para redactar el aviso viene dentro
del payload del evento: **nunca** le pregunta nada a `ordenes` ni a `tecnicos`.
Ese es el motivo por el que los contratos duplican el nombre del técnico y el
número de habitación.

---

## API prevista

| Método | Ruta |
|---|---|
| `GET` | `/notificaciones` (filtro por tipo y habitación) |
| `GET` | `/notificaciones/{id}` |
| `POST` | `/notificaciones/{id}/leida` |
| `GET` | `/salud` |

No existe `POST /notificaciones`: un aviso solo nace de un evento.

---

## Cómo se levanta

```
docker compose up notificaciones
```

Depende **solo** de `rabbitmq`. Sin base de datos, sin volumen.

---

## Preguntas guía pendientes

1. ¿Cuántos avisos se guardan en memoria antes de descartar los viejos?
2. Si el servicio estuvo caído media hora, ¿recupera los eventos perdidos?
   (pista: la cola es durable — ¿eso alcanza?)
3. Al agregar un evento nuevo, el binding `orden.*` lo va a recibir sin que nadie
   lo pida. ¿Qué hace el consumidor con un tipo que no reconoce?
