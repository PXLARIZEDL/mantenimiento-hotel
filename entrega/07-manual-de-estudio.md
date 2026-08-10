# Manual de estudio — Sistema de mantenimiento hotelero

Documento doble: **contenido para estudiar** y **especificación para construir
un HTML** a partir de él.

---

# PARTE 0 — Instrucciones para quien construya el HTML

> Esta sección es para el chat/persona que reciba este archivo. Si estás
> estudiando, saltá a la Parte 1.

## Qué construir

Un **único archivo HTML autocontenido** (todo el CSS y el JS embebidos, sin CDN
ni fuentes externas) que funcione como guía de estudio del equipo.

## Comportamiento

1. **Pantalla de entrada:** el usuario elige su nombre de una lista de seis.
2. Al elegir, se muestran **dos bloques**:
   - **Lo que todos deben saber** (Parte 1 de este documento) — igual para los seis.
   - **Tu servicio** (Parte 2) — solo la ficha de la persona elegida.
3. Debe poder **cambiar de persona** sin recargar.
4. Conviene recordar la elección en `localStorage` para no repetir el paso.

## Requisitos

- **Responsive**: se va a leer en teléfono mientras esperan turno para exponer.
- **Sin dependencias externas**: tiene que abrirse con doble clic, sin internet.
- Los bloques de código y las tablas deben poder leerse en pantalla chica
  (scroll horizontal propio, que el `body` nunca scrollee lateralmente).
- Marcar visualmente las **preguntas de defensa** y sus respuestas, que es lo que
  más se va a consultar a último momento.
- Los diagramas de flujo van como texto preformateado (`<pre>`), no como imagen.

## Tono

Es material de estudio para una defensa oral. Prioridad: que se encuentre rápido
lo que se busca. Nada de animaciones ni adornos que estorben la lectura.

---

# PARTE 1 — Lo que TODOS deben saber

Esto lo tiene que poder contestar cualquiera del equipo, aunque le pregunten por
un servicio que no es el suyo.

## 1.1 La necesidad

En un hotel de 400 habitaciones, una falla de mantenimiento crea un conflicto
entre dos áreas que manejan información distinta: **recepción** necesita saber
qué cuartos puede vender, y **mantenimiento** necesita saber qué arreglar y
quién lo arregla.

Cuando eso se coordina por cuaderno o WhatsApp, pasan tres cosas:

| Falla | Consecuencia |
|---|---|
| Se vende un cuarto roto | La más cara: reubicar a un huésped molesto |
| El cuarto queda bloqueado de más | Sale del inventario días sin motivo |
| La orden va al técnico equivocado | Un problema eléctrico al plomero |

**Las tres son de coordinación, no de ejecución.** Nadie hace mal su trabajo: la
información no llega a tiempo. Eso es lo que automatiza el sistema.

## 1.2 Por qué microservicios (la respuesta honesta)

**La parte real:** el dominio se parte solo en tres pedazos con dueños y ritmos
distintos.

| Pedazo | Dueño en el hotel | Cambia |
|---|---|---|
| Estado de los cuartos | Recepción | Casi nunca |
| Ciclo de vida de la orden | Mantenimiento | Constantemente |
| Personal, turnos, especialidades | RRHH | Cada cambio de plantilla |

Que el estado de un cuarto tenga **un solo dueño** es lo que impide vender un
cuarto roto. Los microservicios hacen esa propiedad verificable: nadie más tiene
acceso a esa base.

**La parte honesta:** para 400 habitaciones, un monolito modular también
funcionaría y sería más simple de operar. Está escrito así en el ADR 001. Lo que
sí es genuino del estilo es la **tolerancia a fallos parciales**, y eso se
demuestra en vivo.

> **Si preguntan esto, no vendan humo.** Reconocer el trade-off suma más que
> defender que los microservicios siempre son mejores.

## 1.3 Los seis servicios

| Servicio | Stack | Base | De qué es dueño |
|---|---|---|---|
| `habitaciones` | C# / .NET 8 | PostgreSQL | Inventario y **estado** de los 400 cuartos |
| `ordenes` | C# / .NET 8 | PostgreSQL | Ciclo de vida de la orden; **orquesta** el caso de uso |
| `tecnicos` | Python 3.12 / FastAPI | PostgreSQL | Personal, turnos y **la regla de asignación** |
| `notificaciones` | Python 3.12 / FastAPI | — (memoria) | Avisos a recepción |
| `gateway` | C# / .NET 8 + YARP | — | **Único** punto de entrada |
| `ui` | React + Vite + nginx | — | Interfaz de recepción |

Mínimo pedido: 4. **Implementados: 6.**

## 1.4 Tecnologías y por qué cada una

| Tecnología | Para qué | Por qué esa |
|---|---|---|
| **.NET 8** | Núcleo transaccional | Tipado fuerte y EF Core donde una orden mal guardada cuesta dinero |
| **Python / FastAPI** | Reglas y textos | Rapidez de escritura donde el código cambia más |
| **PostgreSQL** | Una base **por servicio** | Propiedad de datos física, no acordada |
| **RabbitMQ** | Eventos, exchange `topic` | Desacopla productores de consumidores; colas durables |
| **YARP** | Proxy inverso del gateway | Enrutado por configuración, no por código |
| **Polly** | Timeout, reintento, breaker | Resiliencia de la única llamada sincrónica |
| **Docker Compose** | Levantar todo | 10 contenedores con un comando |
| **React + Vite** | UI | Servida por nginx; en producción no corre Node |
| **xUnit / pytest** | 45 pruebas | Ninguna necesita infraestructura |

## 1.5 El flujo completo

```
  ┌─ 1. Recepción reporta una falla (UI → gateway → ordenes)
  │
  ├─ 2. ordenes ──HTTP──▶ habitaciones      [SINCRÓNICO]
  │       "bloqueá el cuarto 314"
  │       └─ si falla ⇒ la orden NO se crea, responde 503
  │
  ├─ 3. ordenes guarda la orden en estado ABIERTA
  │
  ├─ 4. ordenes publica  orden.creada  ─────▶ RabbitMQ
  │                                             │
  │        ┌────────────────────────────────────┴──────────┐
  │        ▼                                               ▼
  ├─ 5. tecnicos consume                          notificaciones consume
  │     elige por especialidad + turno            avisa "cuarto bloqueado"
  │        │
  │        └─ publica  orden.asignada  ─────▶ RabbitMQ
  │                                             │
  │        ┌────────────────────────────────────┴──────────┐
  │        ▼                                               ▼
  ├─ 6. ordenes consume                           notificaciones consume
  │     mueve la orden a ASIGNADA                 avisa "técnico asignado"
  │
  ├─ 7. El técnico resuelve (UI → gateway → ordenes)
  │     ordenes guarda RESUELTA ─▶ pide liberar el cuarto ─▶ publica orden.resuelta
  │
  └─ 8. notificaciones avisa; el cuarto vuelve a DISPONIBLE
```

**El punto que hay que saber vender:** entre el paso 4 y el 6 pasan ~200 ms y
**nadie tocó nada**. Esa transición de `ABIERTA` a `ASIGNADA` la produjo un
evento que viajó por RabbitMQ hasta un servicio escrito en otro lenguaje y volvió.

## 1.6 Quién habla con quién

```
   navegador ──▶ ui (nginx) ──▶ gateway ──▶ { habitaciones, ordenes,
                                              tecnicos, notificaciones }

   ordenes ──HTTP──▶ habitaciones          ← LA ÚNICA sincrónica

   RabbitMQ · exchange "hotel.eventos" (topic)
     orden.creada    : ordenes  ──▶ tecnicos, notificaciones
     orden.asignada  : tecnicos ──▶ ordenes, notificaciones
     orden.resuelta  : ordenes  ──▶ notificaciones
```

**Reglas que nunca se rompen:**

- Nadie lee la base de datos de otro servicio.
- Todo lo que entra al sistema pasa por el gateway.
- Con `tecnicos` y `notificaciones` **no se habla por HTTP**, solo por eventos.
- El navegador **nunca** habla con RabbitMQ.

## 1.7 Los tres eventos

Todos llevan el **sobre común**: `eventoId`, `tipoEvento`, `version`, `ocurridoEn`.

| Evento | Productor | Consumidores | Campos de negocio |
|---|---|---|---|
| `orden.creada` | `ordenes` | `tecnicos`, `notificaciones` | `ordenId`, `habitacionId`, `habitacionNumero`, `tipoFalla`, `descripcion`, `prioridad`, `reportadoPor` |
| `orden.asignada` | `tecnicos` | `ordenes`, `notificaciones` | `ordenId`, `tecnicoId`, `tecnicoNombre`, `especialidad` |
| `orden.resuelta` | `ordenes` | `notificaciones` | `ordenId`, `habitacionId`, `resueltoPor`, `notaCierre` |

- **camelCase en el cable**, sin excepciones.
- `ocurridoEn` en **ISO-8601 UTC con Z**.
- Valores de `tipoFalla` y `especialidad`: `AIRE_ACONDICIONADO`, `PLOMERIA`,
  `CERRADURA`, `ELECTRICIDAD`.

## 1.8 Conceptos que hay que saber explicar

**Idempotencia.** Procesar el mismo mensaje dos veces deja el mismo resultado.
Se logra guardando los `eventoId` ya procesados y comprobando **antes** de
aplicar nada.

> La clave es `eventoId`, **no** `ordenId`. Una misma orden produce tres eventos
> distintos con el mismo `ordenId`: filtrar por él descartaría mensajes
> legítimos.

**Entrega at-least-once.** RabbitMQ puede entregar el mismo mensaje más de una
vez. Se prefiere a *at-most-once* porque perder un `orden.creada` significa una
orden que nunca se asigna. La contrapartida es obligatoria: **todo consumidor
debe ser idempotente**.

**Circuit breaker.** Tras varios fallos seguidos, deja de intentar y falla
rápido, sin gastar tiempo ni saturar a un servicio caído. A los 30 s deja pasar
una llamada de prueba.

**Consistencia eventual.** No hay transacción que abarque varios servicios.
Durante unos milisegundos, `ordenes` dice `ABIERTA` y `tecnicos` ya asignó. Se
acepta y se compensa.

**Compensación.** Si la habitación se bloqueó y la orden no se pudo guardar, se
llama a `habitaciones` para liberarla. Es el reemplazo del *rollback*.

**Event-carried state transfer.** El evento lleva los datos que el consumidor
necesita —por eso `tecnicoNombre` viaja dentro de `orden.asignada`— para que no
tenga que preguntar por HTTP y depender de que el otro esté vivo.

## 1.9 Preguntas que caen seguro (para cualquiera)

**«¿Por qué microservicios y no un monolito?»** → §1.2. Reconocer el trade-off.

**«¿Qué pasa si un evento llega dos veces?»** → Nada se duplica. Idempotencia
por `eventoId`, comprobada antes de aplicar, y el registro se guarda en la misma
transacción que el efecto.

**«¿Y si se cae RabbitMQ?»** → Los servicios reconectan solos. Las colas son
durables y los mensajes persistentes: sobreviven a un reinicio del broker.

**«¿Cómo mantienen la consistencia sin transacciones distribuidas?»** → No la
mantenemos: aceptamos consistencia eventual y compensamos. Y hay un caso **no
resuelto**: si la orden se guarda pero el evento no se publica, queda sin
técnico. La solución es el patrón *outbox* y está documentada como pendiente.

**«¿Aplicaron SOLID?»** → Sí, y sabemos dónde no. El mejor ejemplo es
`asignador.py`: no importa la base ni el broker, así que se prueba solo. La
violación más clara es `CrearAsync`, con 171 líneas haciendo seis cosas.

**«¿Esto tiene seguridad?»** → No. El login pide un nombre sin contraseña y solo
sirve para identificar quién reporta; el backend no lo verifica. Sería trabajo
del gateway y no está en la v1. **La propia pantalla se lo dice al usuario.**

## 1.10 Comandos de la demo

```bash
docker compose up -d --build      # levantar los 10 contenedores
docker compose ps                 # ver el estado
curl http://localhost:8080/salud  # salud agregada

docker compose stop notificaciones    # apagar uno
docker compose start notificaciones   # y recuperarlo

docker compose logs ordenes --tail 40 # si algo falla en vivo
```

| Qué | Dónde |
|---|---|
| Interfaz | `http://localhost:5173` |
| API por el gateway | `http://localhost:8080` |
| Consola de RabbitMQ | `http://localhost:15672` (`guest`/`guest`) |

---

# PARTE 2 — Fichas por persona

> **Verificar antes de usar:** el reparto de `habitaciones` y `ui` se dedujo por
> el nombre de usuario de GitHub. Confirmarlo con el equipo.

---

## Ricardo — `ordenes` + contratos + infraestructura

**Stack:** C# / .NET 8 · PostgreSQL · RabbitMQ · Polly

### Qué hace

Es el **orquestador**. Recibe el reporte, bloquea la habitación, guarda la orden
y publica los eventos. Gestiona `ABIERTA → ASIGNADA → RESUELTA`.

### De qué es dueño

Orden (id, tipo de falla, descripción, prioridad, quién reportó), estado y
fechas, y copias del técnico asignado.

### Archivos clave

| Archivo | Qué defiende |
|---|---|
| `Modelos/Orden.cs` | La máquina de estados |
| `Endpoints/OrdenesEndpoints.cs` | La orquestación de `POST /ordenes` |
| `Clientes/HabitacionesClient.cs` | Traducción HTTP → dominio |
| `Program.cs` | Las políticas de Polly desde configuración |
| `Pruebas/OrdenPruebas.cs` | 24 pruebas del dominio |

### Lo que tiene que poder explicar

**El orden del reintento y el breaker.**
`reintento → circuit breaker → timeout por intento`. El reintento va **por
fuera** para que cada intento alimente el contador del breaker. Al revés, el
breaker vería un solo fallo por tanda y casi nunca abriría.

**Qué se reintenta y qué no.** Sí: timeout, red, `5xx`. No: `400`, `404`, `409`
— **reintentar un `409` es un bug**, la respuesta no cambia por insistir.

**Con el circuito abierto responde `503`, no `500`.** El `503` dice "la
dependencia no está, reintentá"; el `500` dice "algo se rompió acá".

**La compensación.** Si falla el guardado tras bloquear el cuarto, se libera con
el mismo `ordenId`. Un cuarto bloqueado sin orden que lo explique sale del
inventario y nadie lo destraba.

**El orden invertido al resolver.** Primero persiste `RESUELTA`, después libera.
Al revés, un fallo devolvería al inventario un cuarto con la falla sin resolver.
El peor caso así es un cuarto bloqueado de más: cuesta dinero pero no afecta a
nadie, y se arregla reintentando. Por eso el endpoint es **idempotente**.

### El bug que encontró la primera ejecución

`ordenes` generaba un `ordenId` para bloquear el cuarto, pero la entidad `Orden`
acuñaba **otro GUID**. El cuarto quedaba bloqueado con un id y la orden nacía con
otro: **la habitación no se liberaba nunca**. Y respondía `200 OK`, porque
`habitaciones` interpretaba "no encontré esa orden" como un reintento ya aplicado.

> Es el mejor material de la defensa: **dos servicios quedaron inconsistentes y
> ninguno se dio cuenta**. Es el costo real de no tener transacciones
> distribuidas, demostrado en vez de explicado. Hoy hay una prueba que lo fija.

### Pendiente que debe admitir

**Outbox.** La orden y el evento se escriben en dos pasos. Si el broker falla
entre medio, la orden queda `ABIERTA` sin técnico. Se registra `Critical`. La
solución correcta es guardar el evento en la misma transacción y despacharlo
aparte.

### En la demo

Demos 1 (flujo completo) y 3 (circuit breaker).

---

## Yadfreidel — `gateway` + documentación de arquitectura

**Stack:** C# / .NET 8 + YARP

### Qué hace

Es el **único puerto abierto hacia afuera**. Todo lo que entra al sistema pasa
por aquí y se reenvía al servicio que corresponda. Existe para que la UI conozca
una sola dirección y no la topología interna.

### Archivos clave

| Archivo | Qué defiende |
|---|---|
| `appsettings.json` | El mapa completo del sistema: rutas y clústeres |
| `Program.cs` | El `/salud` agregado y el logging de reenvíos |
| `docs/adr/001`, `002`, `003` | Las decisiones de arquitectura |

### Lo que tiene que poder explicar

**Por qué la configuración es datos y no código.** Las rutas viven en
`appsettings.json`: cambiar el enrutamiento no recompila nada.

**El `/salud` agregado.** Consulta el `/salud` de los cuatro servicios y devuelve
un resumen. Es lo que pinta el `PanelSalud`. Devuelve `200` aunque uno esté
caído: se **reporta** como degradado, pero el gateway sigue respondiendo.

**Health checks activos.** YARP consulta cada 10 s el `/salud` de cada clúster.
Tras 3 fallos seguidos, deja de enviarle tráfico.

**Por qué NO es un BFF.** No agrega ni transforma respuestas de varios servicios.
Solo enruta. Está descartado a propósito en `docs/limites-descartados.md`.

### Los ADR que escribió

- **001 — Estilo:** microservicios vs. monolito modular vs. monolito con una base.
- **002 — Límites:** dónde se cortó cada servicio y por qué.
- **003 — Comunicación:** una sola llamada sincrónica y los valores de resiliencia.

### Preguntas específicas

**«¿Y si se cae el gateway?»** → Nadie entra al sistema desde afuera, pero los
servicios siguen funcionando entre sí: los eventos siguen fluyendo. Es un punto
único de fallo **de entrada**, no de proceso.

**«¿Por qué un gateway y no que la UI llame a cada servicio?»** → Porque la UI
tendría que conocer la topología interna, y cualquier cambio de puertos o nombres
la rompería. Además centraliza CORS y el logging de entrada.

### En la demo

Demo 4 (bases separadas) y apoyo en la de salud.

---

## Josué — `tecnicos`

**Stack:** Python 3.12 / FastAPI · PostgreSQL · aio-pika

### Qué hace

Conoce al personal y **decide quién atiende cada falla**. La asignación es
automática: nadie la pide por HTTP, se dispara al consumir `orden.creada`.

### De qué es dueño

Técnico (id, nombre, especialidad, turno, activo), las especialidades, los turnos
y la traza de asignaciones.

### Archivos clave

| Archivo | Qué defiende |
|---|---|
| `asignador.py` | **La regla de negocio**, aislada |
| `consumidor.py` | Consumo idempotente y publicación |
| `test_asignador.py` + `test_consumidor.py` | 21 pruebas |

### Lo que tiene que poder explicar

**La regla completa:**

```
tipoFalla ──▶ especialidad ──▶ técnicos activos de esa especialidad
                                        │
                                 filtrar por turno vigente
                                        │
                                 desempate: menos órdenes abiertas
                                        │
                                 ¿ninguno? ⇒ NO se publica orden.asignada
```

**Por qué la regla vive aquí y no en `ordenes`.** Depende de **especialidad y
turno**, datos que solo este servicio posee. Si la decidiera `ordenes`, tendría
que replicar el catálogo de especialidades y el calendario de turnos, y
redesplegarse cada vez que esas reglas cambien.

**El desempate es por menos carga.** Es la única de las tres opciones que evita
que un técnico acumule la cola mientras otro del mismo turno está libre. A
igualdad de carga, **por nombre** — para que la decisión sea **determinista** y
se pueda reproducir en la defensa.

**Los turnos y el huso horario.** Los eventos viajan en UTC pero los turnos son
horarios **locales**. Sin conversión, una falla de las 20:00 locales llegaría
como 00:00 UTC y caería en el turno equivocado. Por eso existe
`HOTEL_UTC_OFFSET`.

**Es el único servicio Python que produce un evento que consume C#.** Por eso
`modelos.py` serializa con `by_alias=True`: sin eso saldría `snake_case` y C# no
entendería nada.

### Preguntas específicas

**«¿Si no hay técnico disponible?»** → Se confirma el mensaje y se registra; **no**
se publica `orden.asignada`. La orden queda `ABIERTA`. Reencolar no sirve: que no
haya técnico en turno no es un error transitorio. Qué pasa después sigue abierto
en el ADR 003 — es la laguna conocida de `v1`.

**«¿Y si llega un `tipoFalla` que no existe?»** → Se registra `error`, se confirma
y no se publica nada. No se reintenta: no se arregla insistiendo. Se registra como
`error` y no `warning` porque significa que los dos lados se desincronizaron.

### Pendiente que debe admitir

**La carga nunca baja.** `ordenes_abiertas` cuenta todas las asignaciones
históricas porque el servicio **no consume `orden.resuelta`**. Con el tiempo el
desempate degenera en "quien lleva menos órdenes en total".

### En la demo

Demo 1: explicar por qué se eligió ese técnico y no otro.

---

## Raylin — `notificaciones`

**Stack:** Python 3.12 / FastAPI · **sin base de datos**

### Qué hace

Traduce eventos técnicos en avisos que una persona de recepción entiende. Es la
**ventana visible del flujo asincrónico**: cada aviso nació de un evento que
viajó por RabbitMQ.

### Archivos clave

| Archivo | Qué defiende |
|---|---|
| `consumidor.py` | Consumo con el comodín `orden.*` |
| `plantillas.py` | La redacción de cada aviso |
| `main.py` | El almacén en memoria y la API |

### Lo que tiene que poder explicar

**Por qué no tiene base de datos.** Los avisos viven **en memoria** (máximo 50) y
se pierden al reiniciar. Es una decisión de arquitectura, no un descuido: **es el
servicio que se apaga en la defensa** para demostrar que el resto sigue
funcionando. Los eventos pendientes quedan en la cola durable y se procesan solos
al volver.

**El comodín `orden.*`.** La cola está atada a `orden.*`, no a los tres eventos
uno por uno.

| Gana | Arriesga |
|---|---|
| Un evento nuevo le llega sin tocar nada | Recibe eventos que no sabe manejar |

La mitigación es obligatoria: ante un `tipoEvento` desconocido debe generar un
aviso genérico o descartarlo con log, **nunca lanzar excepción**. Si no, el
mensaje falla, se reencola y bloquea la cola.

**La correlación por `ordenId`.** Solo `orden.creada` trae el número de
habitación; `orden.asignada` y `orden.resuelta` **no**. El servicio recuerda a
qué cuarto pertenece cada orden al ver su creación, y los eventos siguientes lo
consultan. Es oportunista: si se reinicia, el aviso sale como
`Habitación (sin identificar)` en vez de fallar.

### La capacidad que se perdió

El diseño original usaba `habitacionLiberada` para no decir *"habitación
disponible"* cuando el cuarto seguía bloqueado por otra orden. **Ese campo se
quitó del contrato.** Hoy el aviso pide verificar antes de asignar el cuarto.
Recuperarlo obliga a una `v2`.

> Enseña qué significa de verdad *"un contrato publicado no se cambia"*: quitar
> un campo no es un renombre, es **eliminar una capacidad** de un servicio que ni
> estaba en la sala cuando se decidió.

### En la demo

**Demo 2 — la más importante.** Apagar el servicio, mostrar que el resto sigue,
mostrar los mensajes acumulados en la consola de RabbitMQ, encenderlo y ver los
avisos aparecer solos.

---

## Alberto — `habitaciones`

**Stack:** C# / .NET 8 · PostgreSQL

### Qué hace

Mantiene el inventario de los 400 cuartos y **su estado**. Es deliberadamente
**pasivo**: responde preguntas y aplica cambios que otros le piden. No decide
nada sobre mantenimiento.

### De qué es dueño

Habitación (`id`, número 1-400, piso, tipo), estado
(`DISPONIBLE`/`OCUPADA`/`FUERA_DE_SERVICIO`) y la **lista de órdenes activas**.

> **Nadie más escribe estos datos. Ningún otro servicio lee esta base.**

### Archivos clave

| Archivo | Qué defiende |
|---|---|
| `Modelos/Habitacion.cs` | Las transiciones y la lista de órdenes |
| `Endpoints/HabitacionesEndpoints.cs` | Los dos `PUT` idempotentes |
| `Datos/SembradorHabitaciones.cs` | La siembra de las 400 |

### Lo que tiene que poder explicar

**Por qué el `id` es un GUID y no el número.** El contrato exige un
`habitacionId` estable e **independiente de la numeración**: si el hotel renumera
un piso, las órdenes viejas quedarían apuntando a otro cuarto. El número sigue
siendo único y es lo que se usa en las rutas HTTP.

**Por qué guarda una LISTA de órdenes activas.** Si un cuarto tiene dos fallas y
se resuelve una, **no se libera**. Solo vuelve a `DISPONIBLE` cuando la lista
queda vacía. Liberarlo antes devolvería al inventario un cuarto que sigue roto.

**Por qué no llama a nadie.** Es el servicio del que otros dependen, así que se
mantiene sin dependencias propias para que su disponibilidad sea alta. Sin
RabbitMQ, sin clientes HTTP salientes.

**Los dos `PUT` son idempotentes.** `ordenes` los reintenta, y el `ordenId` es lo
que permite reconocer un reintento como la misma operación.

### Preguntas específicas

**«¿Dos órdenes abiertas y se resuelve una, se libera?»** → **No.** Es la razón
de la lista.

**«¿Bloquear un cuarto ya bloqueado por otra orden: `409` o `200`?»** → **`200`.**
Hay que distinguir dos casos que parecen el mismo:

| Caso | Qué es | Respuesta |
|---|---|---|
| Mismo `ordenId` | Reintento de `ordenes` | `200`, sin cambios |
| `ordenId` distinto | Segunda falla legítima | `200`, se agrega a la lista |

Devolver `409` al segundo sería un error de diseño: `ordenes` no crearía la
orden, la falla se perdería, y el cuarto se liberaría al cerrar la primera aunque
siguiera roto. El `409` queda para el conflicto **real**: choque de concurrencia.

**«¿Quién marca `OCUPADA`?»** → **Nadie: ese flujo no existe.** No hay check-in ni
reservas; el sistema es de mantenimiento. El estado existe porque **se puede
reportar una falla en un cuarto con huésped dentro** —el caso más común— y porque
distingue "no disponible porque hay alguien" de "no disponible porque está roto".
Aparece solo en la siembra. Al liberar vuelve a `DISPONIBLE`, nunca a `OCUPADA`:
el servicio no sabe si había huésped y no inventa un dato que no posee.

### En la demo

Demo 4: mostrar que la base no tiene ninguna tabla de órdenes ni de técnicos.

---

## Luis — `ui`

**Stack:** React 18 + Vite · servida por nginx

### Qué hace

Interfaz para recepción. Cinco pantallas, una por cada cosa que el sistema sabe
hacer.

### De qué es dueña

**De nada.** No guarda, no valida reglas de negocio y no toma decisiones. Todo lo
que muestra lo pide y todo lo que hace lo pide como petición.

### Archivos clave

| Archivo | Qué defiende |
|---|---|
| `src/api.js` | **La única vía** hacia el backend |
| `src/App.jsx` | Las pestañas y la barrera de errores |
| `src/componentes/*.jsx` | Las cinco pantallas |
| `nginx.conf` | El proxy hacia el gateway |

### Lo que tiene que poder explicar

**Ningún componente llama a `fetch` por su cuenta.** Todo pasa por `api.js`, que
centraliza cabeceras, timeout y la traducción de `400/404/409/503` a mensajes en
español. Si cambia una ruta, se cambia en un solo archivo.

**Las rutas son relativas y siempre por el gateway.** En desarrollo las resuelve
el proxy de Vite; en producción, nginx. **Idénticas en ambos entornos.**

**Por qué pestañas y no rutas.** nginx reenvía al gateway los prefijos
`/ordenes`, `/habitaciones`, `/notificaciones`… Si el navegador usara esas mismas
rutas, **chocarían con la API**: pedir `/ordenes` devolvería JSON en vez de la
pantalla. Por eso tampoco entra `react-router-dom`.

**Por qué hay refresco periódico.** La UI **no escucha RabbitMQ** — el navegador
no habla con la cola. Una orden aparece `ABIERTA` y pasa a `ASIGNADA` cuando el
evento se procesó, así que sin sondeo la tabla se ve congelada justo en el
momento más interesante. Cada 4 s en órdenes y avisos, 5 s en salud.

**El color nunca es el único portador del dato.** El número del cuarto, el texto
de la etiqueta y el `title` llevan la información: alguien daltónico lee lo
mismo. Y se respeta `prefers-reduced-motion`.

**El login no es autenticación.** Pide un nombre sin contraseña y solo sirve para
identificar quién reporta. El backend no lo verifica. **La propia pantalla se lo
dice al usuario**, para que nadie asuma una barrera que no existe.

### Lo que la UI deliberadamente NO hace

- No elige el técnico: lo decide `tecnicos` al consumir un evento.
- No cambia el estado de una habitación a mano: lo mueve `ordenes`.
- No revalida reglas del backend.

### En la demo

Es **el escenario de las demos 1, 2 y 3** — todo se ve desde su pantalla.

---

# PARTE 3 — Datos para el HTML

Lista de personas para el selector:

| Nombre a mostrar | Servicio | Stack corto |
|---|---|---|
| Ricardo | `ordenes` | C# / .NET 8 |
| Yadfreidel | `gateway` | C# / YARP |
| Josué | `tecnicos` | Python / FastAPI |
| Raylin | `notificaciones` | Python / FastAPI |
| Alberto | `habitaciones` | C# / .NET 8 |
| Luis | `ui` | React + Vite |

Colores sugeridos por servicio, para distinguirlos de un vistazo:

| Servicio | Color |
|---|---|
| `ordenes` | índigo `#4f46e5` |
| `habitaciones` | verde `#059669` |
| `tecnicos` | ámbar `#d97706` |
| `notificaciones` | rosa `#e11d48` |
| `gateway` | azul `#0284c7` |
| `ui` | violeta `#7c3aed` |
