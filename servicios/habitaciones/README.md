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
| Habitación | `id` (GUID), número (1..400), piso, tipo |
| Estado | `DISPONIBLE` / `OCUPADA` / `FUERA_DE_SERVICIO` |
| Motivo del bloqueo | **lista** de órdenes activas que mantienen el cuarto bloqueado |

**Nadie más** escribe estos datos. Ningún otro servicio lee esta base de datos.

### Por qué el `id` es un GUID y no el número

El esqueleto proponía usar el número como clave primaria. Se usa un GUID porque
`contratos/orden.creada.v1.json` exige un `habitacionId` **estable e
independiente de la numeración**: si el hotel renumera un piso, el número cambia
y las órdenes viejas quedarían apuntando a otro cuarto.

El número sigue siendo único y es lo que se usa en las rutas HTTP: `ordenes`
conoce el número, no el GUID. Lo recibe en la **respuesta** al bloquear el
cuarto, y lo guarda para publicarlo en los eventos sin volver a preguntar.

> Por eso el campo `id` de la respuesta no se puede quitar: `ordenes` lo lee.

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

## API

| Método | Ruta | Quién la usa | Respuestas |
|---|---|---|---|
| `GET` | `/habitaciones` (filtro `?estado=` y `?piso=`) | UI | `200` |
| `GET` | `/habitaciones/{numero}` | UI | `200` · `404` |
| `PUT` | `/habitaciones/{numero}/fuera-de-servicio` | **`ordenes`** | `200` · `400` · `404` · `409` |
| `PUT` | `/habitaciones/{numero}/disponible` | `ordenes` | `200` · `400` · `404` · `409` |
| `GET` | `/salud` | `PanelSalud` y health checks del gateway | `200` · `503` |

Los dos `PUT` reciben `{ "ordenId": "..." }` y son **idempotentes**: `ordenes`
los reintenta, y ese `ordenId` es lo que permite reconocer un reintento como la
misma operación en vez de como una nueva.

---

## Cómo se levanta

```
docker compose up habitaciones
```

Depende de `db-habitaciones`. No depende de RabbitMQ ni de ningún otro servicio.

Al arrancar aplica migraciones y **siembra las 400 habitaciones** si la base está
vacía. La siembra es idempotente: reiniciar el contenedor no duplica nada ni pisa
los estados existentes.

El reparto de tipos es fijo, no aleatorio, para que la demo dé el mismo hotel en
todas las máquinas. Una de cada cuatro nace `OCUPADA` para que el inventario no
se vea vacío.

### Migraciones

```
dotnet tool restore
dotnet dotnet-ef migrations add NombreDeLaMigracion --output-dir Datos/Migraciones
```

---

## Preguntas guía

**1. Si una habitación tiene dos órdenes abiertas y se resuelve una, ¿se libera?**

**No.** Por eso el cuarto guarda una **lista** de órdenes activas y no un solo
`ordenId`. `Liberar(ordenId)` quita esa orden de la lista, y solo cuando la lista
queda **vacía** el estado vuelve a `DISPONIBLE`.

Liberarlo al cerrar la primera devolvería al inventario un cuarto que sigue roto:
se le podría vender a un huésped con la segunda falla sin resolver. El costo de
la decisión contraria es que el cuarto queda bloqueado un rato de más — cuesta
dinero, pero no afecta a nadie.

**2. ¿Qué pasa si `ordenes` pide bloquear una habitación que ya está bloqueada por otra orden? ¿`409` o `200`?**

**`200`.** Hay que distinguir dos casos que parecen el mismo:

| Caso | Qué es | Respuesta |
|---|---|---|
| Mismo `ordenId` | Reintento de `ordenes` | `200`, sin cambios |
| `ordenId` distinto | Segunda falla legítima sobre el mismo cuarto | `200`, se agrega a la lista |

Ninguno es un conflicto. Devolver `409` al segundo sería un error de diseño:
`ordenes` no crearía la orden, la segunda falla se perdería, y el cuarto se
liberaría al cerrar la primera aunque siguiera roto.

El `409` queda reservado para lo que **sí** es un conflicto real: un choque de
concurrencia optimista, dos peticiones tocando el mismo cuarto a la vez.

**3. ¿Quién marca una habitación como `OCUPADA`?**

**Nadie: ese flujo no existe en el proyecto.** No hay check-in, ni reservas, ni
recepción registrando huéspedes — el sistema es de *mantenimiento*, y el ciclo de
alojamiento está fuera de su límite de contexto
(`docs/adr/002-limites-contextos.md`).

`OCUPADA` existe en el vocabulario por dos razones concretas:

- Es un estado desde el que **sí** se puede bloquear un cuarto: se puede reportar
  una falla en una habitación con huésped dentro, y de hecho es el caso más común.
- Distingue "no disponible porque hay alguien" de "no disponible porque está
  roto", que es justo lo que el hotel necesita separar.

Aparece solo en la siembra inicial. Y al liberar, el cuarto vuelve a
`DISPONIBLE`, nunca a `OCUPADA`: este servicio no sabe si había huésped, y
devolverlo a `OCUPADA` por su cuenta sería inventar un dato que no posee.

> Si algún día se integra un PMS de alojamiento, entra por aquí: un `PUT
> /habitaciones/{numero}/ocupada`. No se agrega antes de que exista quien lo llame.

---

## Pendientes conocidos

1. **Sin tests.** Faltan al menos los de las transiciones de `Modelos/Habitacion.cs`
   y los de idempotencia de los dos `PUT`.
2. **Sin autenticación.** En la v1 el gateway es la única frontera de entrada.
3. **La lista de órdenes activas no caduca.** Si `ordenes` nunca cierra una orden,
   el cuarto queda bloqueado para siempre y solo se destraba a mano.
