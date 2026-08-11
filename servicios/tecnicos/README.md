# Servicio `tecnicos`

Python 3.12 / FastAPI + PostgreSQL

---

## Qué hace

Conoce al personal de mantenimiento y **decide quién atiende cada falla**. La
asignación es automática: nadie la pide por HTTP, se dispara al consumir el
evento `orden.creada`.

---

## De qué datos es dueño

| Dato | Detalle |
|---|---|
| Técnico | id, nombre, especialidad, turno, activo |
| Especialidad | `AIRE_ACONDICIONADO` / `PLOMERIA` / `CERRADURA` / `ELECTRICIDAD` |
| Turno | `MAÑANA` / `TARDE` / `NOCHE` |
| Asignación | qué técnico quedó a cargo de qué orden y cuándo |

Es el único servicio que sabe qué es un turno y qué es una especialidad. Por eso
la regla de asignación vive aquí y no en `ordenes`
(ver `docs/adr/002-limites-contextos.md`).

> Los valores de especialidad coinciden **exactamente** con los de `tipoFalla` en
> `contratos/orden.creada.v1.json`. Esa coincidencia es lo que permite mapear
> falla a especialidad sin tabla de traducción; renombrar uno obliga a subir el
> contrato a `v2`.

---

## Con quién habla

| Dirección | Con quién | Cómo |
|---|---|---|
| Consume | `orden.creada` | cola `tecnicos.orden-creada` |
| Publica | `orden.asignada` | exchange `hotel.eventos` |
| Entrante | `gateway` → `ui` | HTTP (solo consultas) |
| Saliente HTTP | **nadie** | — |

Es el único servicio Python que **produce** un evento consumido por C#. Por eso
`modelos.py` serializa siempre con `by_alias=True`: sin eso saldría `snake_case`
y el consumidor C# no entendería nada.

---

## La regla de asignación

```
tipoFalla ──▶ especialidad ──▶ técnicos activos de esa especialidad
                                        │
                                 filtrar por turno vigente
                                        │
                                 desempate: menos órdenes abiertas
                                        │
                                 ¿ninguno? ⇒ NO se publica orden.asignada
```

Vive aislada en `asignador.py`: recibe los candidatos como parámetro y no importa
nada de SQLAlchemy ni de aio-pika, así que se puede probar sin levantar
PostgreSQL ni RabbitMQ.

### Turnos y husos horarios

Los eventos viajan en **UTC** (lo fijan los contratos), pero los turnos son
horarios **locales** del hotel:

| Turno | Hora local |
|---|---|
| `MAÑANA` | 06:00 – 14:00 |
| `TARDE` | 14:00 – 22:00 |
| `NOCHE` | 22:00 – 06:00 |

La conversión usa `HOTEL_UTC_OFFSET` (por defecto `-4`). **No es un detalle
menor:** sin ella, una falla reportada a las 20:00 locales llegaría como 00:00
UTC y se asignaría al turno de noche cuando corresponde el de tarde. Se dejó
configurable porque es justo el valor que se olvida y produce asignaciones
absurdas.

---

## API

| Método | Ruta | Para qué |
|---|---|---|
| `GET` | `/tecnicos` (filtro `?especialidad=` y `?turno=`) | UI |
| `GET` | `/tecnicos/disponibles` | quiénes están en turno **ahora**; depurar por qué una orden no se asignó |
| `GET` | `/tecnicos/{id}` | detalle |
| `POST` | `/tecnicos` | dar de alta |
| `PUT` | `/tecnicos/{id}` | cambiar nombre, especialidad, turno o si está activo |
| `GET` | `/asignaciones` | traza de lo que este servicio decidió |
| `GET` | `/salud` | `PanelSalud` y health checks del gateway |

**No hay `POST` de asignación**: asignar es consecuencia de un evento, no de una
petición. Dar de alta a un técnico sí entra por HTTP, porque este servicio es el
dueño de la plantilla — son dos cosas distintas.

**Tampoco hay `DELETE`.** Las asignaciones apuntan al técnico, así que borrarlo
dejaría huérfana la traza de quién atendió qué. Para sacar a alguien de
circulación se pone `activo` en `false`: el asignador solo mira a los activos,
así que deja de recibir órdenes nuevas y conserva las que ya tenía.

`especialidad` y `turno` se validan contra el catálogo del dominio; un valor
inventado se rechaza con `422` antes de tocar la base.

---

## Cómo se levanta

```
docker compose up tecnicos
```

Depende de `db-tecnicos` y de `rabbitmq`, pero **arranca aunque RabbitMQ todavía
no esté**: el consumidor reintenta la conexión solo, por eso no hace falta un
script de espera en el `Dockerfile`.

Al arrancar siembra **12 técnicos**: uno por cada especialidad y turno (4 × 3).
Sin ellos no hay caso de uso que demostrar, porque ninguna orden llegaría a
asignarse. La siembra es idempotente.

---

## Preguntas guía

**1. Si no hay técnico disponible, ¿el mensaje se descarta, se reencola o se pospone al próximo turno?**

Ninguna de las tres del todo. **Se confirma (`ack`) y se registra**, y **no** se
publica `orden.asignada` — así lo fija `contratos/orden.asignada.v1.json`. La
orden se queda `ABIERTA` en `ordenes`.

Por qué no las otras opciones:

- **Reencolar** no sirve: que no haya técnico de esa especialidad en turno no es
  un error transitorio de infraestructura. El mensaje volvería en milisegundos,
  fallaría igual, y giraría en la cola bloqueando a los que sí se pueden atender.
- **Descartar sin registrar** perdería la traza de que el evento se manejó.

El evento **sí** se marca como procesado, porque sí se manejó: la decisión fue
"ninguno". Reintentarlo daría lo mismo mientras no cambie la plantilla.

Lo que queda **abierto** es qué pasa después, y está anotado en el contrato para
decidirse en ADR 003: un evento `orden.sin-tecnico` en `v2` que `notificaciones`
use para escalar a recepción, o un reintento al iniciar el próximo turno.
Mientras tanto, la orden solo se ve porque sigue `ABIERTA` en la UI. **Es la
laguna conocida de `v1`.**

**2. ¿El desempate es por menos carga, por antigüedad o aleatorio?**

**Por menos órdenes abiertas.** Es la única de las tres que evita que un técnico
acumule la cola mientras otro del mismo turno y especialidad está libre. La
antigüedad concentraría el trabajo en una persona; el azar no garantiza nada a
corto plazo, que es justo cuando importa.

A igualdad de carga se ordena **por nombre**, no al azar. Eso hace la decisión
**determinista**: la misma entrada da siempre la misma salida, que es lo que
permite probar la función y reproducir un caso concreto en la defensa.

**3. ¿Qué pasa si llega `orden.creada` con un `tipoFalla` que no existe?**

Se registra un `error`, se confirma el mensaje y **no** se publica nada. No se
reintenta: un `tipoFalla` inválido no se arregla insistiendo, daría el mismo
resultado siempre. Es un mensaje envenenado y reencolarlo atascaría la cola.

Que llegue uno significa que alguien rompió el contrato — `ordenes` valida
`tipoFalla` contra su enum antes de publicar, así que no debería ocurrir. Por eso
se registra como `error` y no como `warning`: es una alarma de que los dos lados
se desincronizaron.

---

## Pendientes conocidos

1. **Faltan pruebas de las consultas.** Hay **32**, y ninguna levanta PostgreSQL
   ni RabbitMQ:

   ```
   pip install -r requirements.txt -r requirements-dev.txt
   pytest
   ```

   | Archivo | Cubre |
   |---|---|
   | `test_asignador.py` | La regla: turno, desempate, sin técnico, falla inválida |
   | `test_consumidor.py` | Idempotencia, forma de lo publicado, política de ack/nack |
   | `test_gestion.py` | Alta y edición, validación del catálogo, y que no exista borrado |

   El consumidor se prueba con **SQLite en memoria** y un mensaje falso que
   anota si le hicieron ack o nack; la gestión, con el `TestClient` de FastAPI
   contra la misma base en memoria. Lo que falta son los `GET` de consulta.

2. **Outbox.** La asignación se guarda y el evento se publica en dos pasos; si el
   broker falla entre medio, la orden queda asignada aquí pero `ordenes` nunca se
   entera. Se registra `critical`. Mismo pendiente que en `ordenes`.

3. **Sin reasignación.** Si un técnico se enferma, no hay forma de mover sus
   órdenes. `v1` no lo contempla — y hay una restricción concreta que lo
   respalda: `Asignacion.orden_id` es la **clave primaria**, así que un segundo
   `orden.asignada` para la misma orden fallaría con violación de unicidad. Hoy
   no ocurre porque `ordenes` publica un `orden.creada` por orden, pero es lo
   primero que hay que cambiar si se admite reasignar.

4. **La carga no baja nunca.** `ordenes_abiertas` cuenta todas las asignaciones
   históricas, porque este servicio no se entera de `orden.resuelta` — no está
   suscrito a ese evento. Con el tiempo el desempate se vuelve "quien lleva menos
   órdenes en total", no "quien está menos ocupado ahora".
