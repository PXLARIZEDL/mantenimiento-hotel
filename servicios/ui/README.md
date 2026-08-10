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

**¿Sondeo cada N segundos, o SSE/WebSocket?** Sondeo, y para la v1 alcanza:

| Pantalla | Cada |
|---|---|
| `ListaOrdenes` | 4 s |
| `BandejaNotificaciones` | 4 s |
| `PanelSalud` | 5 s, con botón para detenerlo |
| `Habitaciones` | solo al abrir y con botón — 400 cuartos no cambian tanto |

SSE o WebSocket exigirían que el gateway mantuviera conexiones abiertas y que
algún servicio empujara los cambios. Para cinco pantallas con un puñado de
usuarios en recepción, es complejidad que no se paga.

---

## Navegación por pestañas, no por rutas

Deliberado. nginx reenvía al gateway los prefijos `/ordenes`, `/habitaciones`,
`/notificaciones`, `/tecnicos`, `/asignaciones` y `/salud`. Si el navegador
usara esas mismas rutas, **chocarían con la API**: pedir `/ordenes` devolvería
JSON en vez de la pantalla.

Por eso tampoco entra `react-router-dom` como dependencia: un `useState` con la
pestaña activa resuelve lo mismo sin ese conflicto.

---

## Cómo se levanta

```
docker compose up ui          # nginx sirviendo el build
npm run dev                   # desarrollo, con proxy hacia el gateway
```

Con todo el sistema arriba: **http://localhost:5173**

---

## Para la defensa

Las tres cosas que se ven mejor desde la interfaz:

**1. La asignación es asincrónica.** Reporta una falla en *Reportar falla* y pasa
enseguida a *Órdenes*: aparece `ABIERTA` y, sin tocar nada, cambia sola a
`ASIGNADA` con el nombre del técnico. Ese cambio lo produjo un evento que viajó
por RabbitMQ hasta `tecnicos` y volvió.

**2. Apagar un servicio y seguir trabajando.**

```
docker compose stop notificaciones
```

*Salud del sistema* lo marca caído y el resto sigue en verde. Se pueden crear y
resolver órdenes con normalidad. Al encenderlo otra vez:

```
docker compose start notificaciones
```

los avisos que se perdió **aparecen solos** en la bandeja: quedaron esperando en
la cola durable de RabbitMQ.

**3. El circuit breaker.** Apagá `habitaciones` e intentá reportar una falla. Los
primeros intentos tardan (timeout y reintentos); después el circuito abre y la
respuesta pasa a ser inmediata, con un aviso claro de que la orden **no** se creó.

---

## Pendientes conocidos

1. **Sin tests.** Ni de componentes ni end to end.
2. **El estado del circuit breaker no se muestra** en `PanelSalud` porque
   `ordenes` no lo expone por HTTP. El panel lo dice en vez de fingir un
   indicador que no tiene de dónde leer.
3. **Sin autenticación.** Cualquiera con acceso a la red puede reportar y cerrar
   órdenes. En la v1 el gateway es la única frontera.
4. **El filtro de piso** se arma con los pisos que ya llegaron; si hay un filtro
   de estado puesto, solo ofrece los pisos que quedaron visibles.
