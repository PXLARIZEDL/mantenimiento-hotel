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
| Habitación afectada | `habitacionId` y **número**; el estado del cuarto es de `habitaciones` |
| Técnico asignado | id, nombre y especialidad **copiados** del evento `orden.asignada` |

La copia del nombre del técnico es deliberada: evita que la UI tenga que
consultar dos servicios para pintar una lista. Es el valor **en el momento del
evento**, no la fuente de verdad — si el técnico cambia de nombre después, la
copia no se corrige.

El `habitacionId` no se inventa ni se consulta aparte: llega en la respuesta de
`habitaciones` al bloquear el cuarto, y se guarda para poder publicarlo en
`orden.creada` y `orden.resuelta` sin volver a preguntar.

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

**Qué pasa si el paso 3 falla después del 2** (la pregunta que estaba abierta):
se **compensa** llamando a `PUT habitaciones/{n}/disponible` con el mismo
`ordenId`. Dejar una habitación fuera de servicio sin orden que la explique la
saca del inventario sin que nadie sepa por qué, y nadie la va a destrabar porque
no hay orden que cerrar. Si la compensación *también* falla, se registra un log
`Critical` — ahí sí queda inconsistencia real que alguien debe limpiar a mano.

**Qué pasa si el paso 4 falla después del 3:** la orden ya está guardada y es
válida, así que **no se deshace**; se registra `Critical` y se responde `201`. Lo
que se pierde es el disparo de la asignación: la orden se queda `ABIERTA` sin
técnico. Es el problema clásico de la **doble escritura** (base + broker), y la
solución correcta es un **outbox** — ver *Pendientes* al final.

---

## El flujo de `PUT /ordenes/{id}/resolver`

```
1. carga la orden y valida la transición
2. guarda RESUELTA
3. PUT habitaciones/{n}/disponible
4. publica orden.resuelta
```

El orden 2 → 3 es deliberado y es lo contrario del de creación. Si se liberara
la habitación **antes** de guardar y el guardado fallara, el cuarto volvería al
inventario con la falla sin resolver y se podría alojar a alguien en una
habitación rota. Al revés, el peor caso es una habitación bloqueada de más:
cuesta dinero, pero no afecta a ningún huésped y se arregla reintentando.

Por eso el endpoint es **idempotente**: reintentarlo sobre una orden que ya está
`RESUELTA` no da `409`, sino que reanuda desde el paso 3.

El evento va **último** porque el contrato dice que quien recibe `orden.resuelta`
puede asumir que la habitación **ya** está disponible.

---

## API

| Método | Ruta | Respuestas |
|---|---|---|
| `POST` | `/ordenes` | `201` · `400` · `404` · `409` · `503` |
| `GET` | `/ordenes` (filtro `?estado=` y `?habitacion=`) | `200` |
| `GET` | `/ordenes/{id}` | `200` · `404` |
| `PUT` | `/ordenes/{id}/resolver` | `200` · `404` · `409` · `503` |
| `GET` | `/salud` | `200` · `503` |

---

## Cómo se levanta

```
docker compose up ordenes
```

Depende de `db-ordenes`, `rabbitmq` y — en tiempo de ejecución — de
`habitaciones`. Arranca **aunque `habitaciones` esté caído**: el circuit breaker
existe precisamente para eso. También arranca aunque RabbitMQ no esté todavía:
publicador y consumidor reconectan solos.

Variables que necesita (`.env.example`): conexión a PostgreSQL, credenciales de
RabbitMQ, URL base de habitaciones y los parámetros de resiliencia.

### Migraciones

El esquema se aplica solo al arrancar (`Database.MigrateAsync()`). Para agregar
una migración nueva:

```
dotnet tool restore
dotnet dotnet-ef migrations add NombreDeLaMigracion --output-dir Datos/Migraciones
```

---

## Valores de resiliencia

Los tres mecanismos de `docs/adr/003`, en `appsettings.json` →
`Habitaciones:Resiliencia`. Ninguno está escrito en el código.

| Parámetro | Valor | Por qué |
|---|---|---|
| `TimeoutSegundos` | `3` | Bloquear una habitación es una escritura simple; más de 3s es señal de problema |
| `Reintentos` | `3` | Adicionales al primer intento |
| `EsperaBaseMilisegundos` | `200` | Exponencial y **con jitter**, para que varias instancias no reintenten en bloque |
| `UmbralFallosCircuito` | `0.5` | Abre con 50% de fallos en la ventana |
| `MinimoLlamadasCircuito` | `8` | Sin mínimo, dos fallos aislados abrirían el circuito |
| `SegundosVentanaMuestreo` | `30` | Ventana sobre la que se mide el umbral |
| `SegundosCircuitoAbierto` | `30` | Tiempo abierto antes de dejar pasar una prueba |

**El reintento va por fuera del circuit breaker:**

```
reintento  →  circuit breaker  →  timeout por intento
```

Así cada intento pasa por el breaker y lo alimenta. Al revés, el breaker vería
un solo "fallo" por cada tanda completa de reintentos y prácticamente nunca
llegaría a abrirse.

**Qué se reintenta:** timeout, fallo de red y `5xx`. **Qué no:** `400`, `404` y
`409` — reintentar un `409` es un bug, porque la respuesta no cambia por
insistir. La traducción de códigos HTTP a resultados de dominio está en
`Clientes/HabitacionesClient.cs`, para que el resto del servicio nunca vea un
`HttpResponseMessage`.

---

## Preguntas guía

**1. ¿Qué código HTTP devuelve `POST /ordenes` con el circuito abierto?**

`503 Service Unavailable`, con un cuerpo que dice explícitamente que la orden
**no** se creó y que se puede reintentar. No `500`: un `500` significa "algo se
rompió de este lado" y no invita a reintentar; un `503` comunica exactamente lo
que pasó — la dependencia no está disponible ahora mismo.

**2. ¿Se puede resolver una orden que nunca fue asignada?**

Sí. `ABIERTA → RESUELTA` es una transición válida
(`Modelos/EstadoOrden.cs`). Es el caso real del técnico que ya estaba en el piso
y arregla la falla antes de que el asignador le llegue. El contrato lo respalda:
`contratos/orden.resuelta.v1.json` admite `resueltoPor` nulo "si la orden se
cerró sin haber sido asignada nunca".

Lo que **no** se puede es reabrir: `RESUELTA` es terminal. Volver a la misma
habitación es una orden nueva.

**3. Si llegan dos veces `orden.asignada`, ¿qué campo evita asignar dos veces?**

**`eventoId`**, no `ordenId`. `Eventos/ConsumidorOrdenAsignada.cs` consulta la
tabla `eventos_procesados` **antes** de aplicar nada; si el `eventoId` ya está,
confirma el mensaje y no hace nada más.

`ordenId` no serviría: una misma orden produce `orden.creada`, `orden.asignada` y
`orden.resuelta`, todos con el mismo `ordenId`, así que filtrar por él
descartaría eventos legítimos.

El registro del `eventoId` y el efecto se guardan en la **misma transacción**; si
no, un fallo entre ambos reabriría la ventana del duplicado. Además `eventoId` es
la **clave primaria** de la tabla: si dos instancias del consumidor procesaran el
mismo mensaje a la vez, la restricción de unicidad es la última línea de defensa.

---

## Pendientes conocidos

1. **Outbox.** Hoy la orden se guarda y el evento se publica en dos pasos
   separados; si el broker falla entre medio, la orden queda `ABIERTA` sin
   técnico. Lo correcto es guardar el evento en la misma transacción que la orden
   y despacharlo con un proceso aparte.
2. **Solo hay pruebas del dominio.** `Pruebas/` cubre `Modelos/Orden.cs` con
   **24 casos**, sin PostgreSQL ni RabbitMQ:

   ```
   cd Pruebas && dotnet test
   ```

   Cubren el alta, las tres transiciones válidas, las que deben fallar y la
   tabla completa de `TransicionesOrden`. Que se puedan correr así es
   consecuencia del diseño: la entidad no lleva atributos de EF ni publica
   eventos, así que no conoce la red.

   Falta cubrir el consumidor idempotente y los endpoints. Eso necesitaría una
   base y un `WebApplicationFactory`: es otro tipo de prueba.
3. **Reasignación.** Si llega un `orden.asignada` con otro `eventoId` para una
   orden que ya está `ASIGNADA`, se conserva la primera asignación y se registra
   un warning. `v1` no define reasignación.
4. **Reintento del consumidor.** Es acotado a dos entregas (`Redelivered`) y
   luego descarta con log. Falta una DLQ de verdad y decidir quién la revisa
   (`docs/catalogo-eventos.md`).
