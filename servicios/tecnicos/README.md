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

Solo consultas. **No hay `POST` de asignación**: asignar es consecuencia de un
evento, no de una petición.

| Método | Ruta | Para qué |
|---|---|---|
| `GET` | `/tecnicos` (filtro `?especialidad=` y `?turno=`) | UI |
| `GET` | `/tecnicos/disponibles` | quiénes están en turno **ahora**; depurar por qué una orden no se asignó |
| `GET` | `/tecnicos/{id}` | detalle |
| `GET` | `/asignaciones` | traza de lo que este servicio decidió |
| `GET` | `/salud` | `PanelSalud` y health checks del gateway |

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

1. **Sin tests.** La regla de `asignador.py` está aislada justamente para poder
   probarla sin infraestructura, pero el archivo de pruebas no existe todavía.
   Iría con `pytest` en un `requirements-dev.txt` aparte.
2. **Outbox.** La asignación se guarda y el evento se publica en dos pasos; si el
   broker falla entre medio, la orden queda asignada aquí pero `ordenes` nunca se
   entera. Se registra `critical`. Mismo pendiente que en `ordenes`.
3. **Sin reasignación.** Si un técnico se enferma, no hay forma de mover sus
   órdenes. `v1` no lo contempla.
4. **La carga no baja nunca.** `ordenes_abiertas` cuenta todas las asignaciones
   históricas, porque este servicio no se entera de `orden.resuelta` — no está
   suscrito a ese evento. Con el tiempo el desempate se vuelve "quien lleva menos
   órdenes en total", no "quien está menos ocupado ahora".
