# Arquitectura de mantenimiento-hotel

Documento maestro. Explica **qué** es el sistema y **cómo** están dispuestas sus
piezas. Las decisiones y sus alternativas descartadas viven en `adr/`.

---

## 1. Contexto del negocio

Hotel de 400 habitaciones.

- **¿Quién reporta una falla y desde dónde?:** El personal del hotel (recepción, mantenimiento, limpieza) reporta la falla desde la interfaz web (`ui`).
- **¿Qué se pierde si una habitación queda ocupada teniendo una falla activa?:** Se compromete la experiencia y satisfacción del huésped, se corren riesgos de seguridad/daños mayores en la infraestructura del hotel (ej. filtraciones) y se genera sobrecosto operativo.
- **¿Cuántas órdenes por día se esperan?:** Para 400 habitaciones se estiman entre 10 y 50 órdenes diarias. Este volumen no exige escalabilidad extrema, pero el uso de colas (RabbitMQ) desacopla la transmisión de eventos y garantiza que el reporte no se bloquee ni se pierda.
- **¿Quién consulta el sistema?:** Recepción (para ver disponibilidad y notificaciones), mantenimiento (para atender y asignar órdenes) y administración.

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

- **¿Por qué el gateway no habla con `notificaciones` por cola sino por HTTP?:** Porque el gateway es únicamente un proxy inverso HTTP para la UI; no es un consumidor ni productor de RabbitMQ. La UI consulta a `notificaciones` mediante solicitudes HTTP GET a través del gateway.
- **¿Por qué `notificaciones` no tiene base de datos y qué se pierde con eso?:** Porque los avisos en la bandeja de recepción son notificaciones efímeras del turno actual. Se acepta perder el historial de avisos al reiniciar el contenedor a cambio de mantener el servicio simple y sin estado.

---

## 3. Propiedad de los datos

Cada servicio es dueño exclusivo de su esquema. Nadie lee la tabla de otro.

| Dato | Dueño | Quién más lo necesita | Cómo lo obtiene |
|---|---|---|---|
| Habitación, estado | `habitaciones` | `ordenes` | HTTP sincrónico |
| Orden, estado | `ordenes` | `tecnicos`, `notificaciones` | evento `orden.creada` |
| Técnico, especialidad, turno | `tecnicos` | `notificaciones` | evento `orden.asignada` |
| Aviso enviado | `notificaciones` | nadie | — |

- **¿Qué datos se duplican a propósito en el payload de los eventos y por qué eso no es un error?:** Se duplican campos como `numeroHabitacion`, `nombreTecnico`, `especialidad` y `turno`. No es un error sino un patrón de diseño asincrónico: permite que los consumidores (como `notificaciones`) procesen la información de forma autónoma sin realizar llamadas HTTP síncronas de retorno (*query back*) a otros servicios.

---

## 4. Flujo completo del caso de uso

1. `POST /ordenes` llega al gateway y se enruta a `ordenes`.
2. `ordenes` llama a `habitaciones` para marcarla `FUERA_DE_SERVICIO`. **(sincrónico)**
3. `ordenes` persiste la orden en `ABIERTA` y publica `orden.creada`. **(asincrónico)**
4. `tecnicos` consume `orden.creada`, elige técnico por especialidad y publica `orden.asignada`.
5. `ordenes` consume `orden.asignada` y pasa la orden a `ASIGNADA`.
6. `notificaciones` consume los tres eventos y arma el aviso a recepción.
7. Al resolver, `ordenes` publica `orden.resuelta` y libera la habitación.

- **¿En qué orden se hace el paso 2 y el paso 3, y qué pasa si falla el segundo?:** El paso 2 (bloqueo de habitación) se ejecuta estrictamente ANTES del paso 3. Si el paso 2 falla o `habitaciones` está caído, la orden NO se crea ni se persiste en la base de datos de `ordenes`, y se devuelve un error al cliente.
- **¿Quién devuelve la habitación a `DISPONIBLE` y en qué paso exacto?:** El servicio `ordenes` solicita liberar la habitación a `habitaciones` en el paso 7 al resolver la orden.

---

## 5. Resiliencia

- **Única llamada sincrónica:** timeout, reintento con espera creciente y circuit breaker. Detalle en `adr/003-estrategia-comunicacion.md`.
- **Consumidores:** Si un mensaje falla por problema transitorio se reintenta. Si falla por error de formato o regla irrecoverable se descarta/envía a dead-letter para no bloquear la cola.
- **Idempotencia:** Se utiliza `eventoId` como clave de idempotencia. Si un consumidor recibe un `eventoId` ya procesado o la orden ya cambió de estado, la operación se ignora sin duplicar asignaciones ni acciones.

---

## 6. Observabilidad mínima

- Endpoint de salud en cada servicio (`/health` o `/salud`).
- `PanelSalud` en la UI los consulta a través del gateway.

- **¿Qué debe reportar "sano" un servicio que depende de RabbitMQ?:** Debe reportar HTTP 200 (sano) solo cuando tanto su proceso interno como la conexión activa al broker de RabbitMQ (y a su base de datos, si aplica) estén respondiendo correctamente.

