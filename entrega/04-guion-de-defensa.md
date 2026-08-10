# 4. Guion de defensa

Todo lo de aquí está **probado en ejecución**. No hay ningún paso que no se haya
corrido de verdad.

Vale **10 puntos**. Duración estimada: 12-15 minutos.

---

## Antes de empezar

```bash
cd "PROYECTO ARQUITECTURA"
docker compose up -d --build
```

Esperar a que los diez contenedores estén arriba:

```bash
docker compose ps
```

Comprobar que el sistema está sano **antes** de que empiece la exposición:

```bash
curl http://localhost:8080/salud
```

Debe decir `"estado":"sano"` con los cuatro servicios en verde.

**Dejar abiertas tres ventanas:**

| Ventana | Qué |
|---|---|
| Navegador, pestaña 1 | `http://localhost:5173` — la interfaz |
| Navegador, pestaña 2 | `http://localhost:15672` — consola de RabbitMQ (`guest`/`guest`) |
| Terminal | para apagar servicios |

> **Consejo:** crear una o dos órdenes antes de empezar, para que las pantallas
> no estén vacías cuando se muestren.

---

## Demo 1 — El caso de uso completo (3 min)

**Qué se demuestra:** que cinco servicios en dos lenguajes coordinan un flujo de
negocio sin que nadie los orqueste a mano.

1. Entrar a la UI. Poner un nombre. → *"Bienvenido, {nombre}"*
2. Pestaña **Reportar falla**. Habitación `314`, tipo `Aire acondicionado`,
   prioridad `ALTA`. Enviar.
3. **Decir en voz alta:** *"fíjense que la orden nace ABIERTA, sin técnico"*.
4. Ir a **Órdenes**. Se ve `ABIERTA`.
5. **Esperar 4 segundos sin tocar nada.** Cambia sola a `ASIGNADA` con el nombre
   del técnico.

> **La frase clave:** *«Nadie tocó nada. Ese cambio lo produjo un evento que
> viajó por RabbitMQ desde `ordenes` hasta `tecnicos`, que eligió el técnico por
> especialidad y turno, y publicó otro evento que `ordenes` consumió para mover
> el estado. Son dos servicios, en dos lenguajes distintos, que nunca se llamaron
> entre sí.»*

6. Pestaña **Habitaciones**: el cuarto 314 está en rojo, `FUERA_DE_SERVICIO`.
7. Pestaña **Avisos**: los avisos de `orden.creada` y `orden.asignada`.
8. Volver a **Órdenes** → *Resolver*, escribir una nota, confirmar.
9. **Habitaciones**: el 314 volvió a verde.

---

## Demo 2 — Apagar un servicio (3 min)

**Es la demostración que pidió el profesor.**

```bash
docker compose stop notificaciones
```

1. Pestaña **Salud del sistema**: `notificaciones` aparece **caído** en rojo, los
   otros tres siguen en verde. El estado general dice `degradado`.
2. **Sin arreglar nada**, ir a *Reportar falla* y crear otra orden.
3. Funciona con normalidad. Ir a **Órdenes**: se asigna sola igual que antes.

> **La frase clave:** *«El servicio de avisos está caído y el negocio sigue
> operando. En un monolito, esto habría sido el mismo proceso: si se cae, se cae
> todo.»*

4. Ir a la consola de RabbitMQ → **Queues** → `notificaciones.eventos`.
   **Mostrar que hay mensajes acumulados.**

> *«Los eventos no se perdieron: están esperando en una cola durable.»*

5. Encender el servicio:

```bash
docker compose start notificaciones
```

6. Esperar unos segundos. Ir a **Avisos**: **los avisos que se perdió aparecen
   solos**.

> **La frase que cierra:** *«Se recuperó sin intervención. Eso es lo que compra
> la mensajería asincrónica: el servicio puede estar caído un rato sin que se
> pierda trabajo.»*

*Verificado: quedaron 2 mensajes en la cola y se procesaron los 2 al volver.*

---

## Demo 3 — El circuit breaker (3 min)

**Qué se demuestra:** que la única llamada sincrónica del sistema está protegida.

```bash
docker compose stop habitaciones
```

1. Intentar reportar una falla en la UI.
2. **Tarda unos segundos** — son el timeout y los tres reintentos.
3. Sale un aviso claro: **la orden NO se creó**, se puede reintentar.
4. Intentarlo varias veces seguidas. **A partir de cierto punto la respuesta es
   inmediata**: el circuito se abrió y ya ni lo intenta.

> **La frase clave:** *«Al principio esperaba y reintentaba. Después de varios
> fallos el circuito abre y falla rápido, sin gastar tiempo ni saturar a un
> servicio que ya sabemos que está caído. Cuando pasan 30 segundos deja pasar una
> llamada de prueba para ver si volvió.»*

5. Recuperar:

```bash
docker compose start habitaciones
```

6. En **Salud del sistema** vuelve a verde y ya se pueden crear órdenes.

> **Detalle que suma:** *«El reintento va por fuera del breaker, para que cada
> intento individual alimente su contador. Si fuera al revés, el breaker vería un
> solo fallo por tanda y casi nunca abriría.»*

---

## Demo 4 — Bases de datos separadas (2 min)

**Qué se demuestra:** que la propiedad de los datos no es un acuerdo, es física.

```bash
docker compose exec db-ordenes psql -U ordenes -d ordenes -c "\dt"
```

Se ven `ordenes` y `eventos_procesados`. **No hay ninguna tabla de habitaciones
ni de técnicos.**

```bash
docker compose exec db-habitaciones psql -U habitaciones -d habitaciones -c "SELECT numero, estado FROM habitaciones WHERE estado <> 'DISPONIBLE' LIMIT 5;"
```

> **La frase clave:** *«`ordenes` no puede leer esta base aunque quisiera: no
> tiene credenciales ni ruta. Por eso el estado de una habitación solo lo cambia
> `habitaciones`, y por eso no se puede vender un cuarto roto.»*

---

## Preguntas que van a hacer, y la respuesta

**«¿Por qué microservicios y no un monolito?»**
> Los tres dominios cambian a ritmos distintos y pertenecen a áreas distintas del
> hotel. Y hay una razón verificable: que el estado del cuarto tenga un solo
> dueño es lo que impide vender un cuarto roto. Dicho eso — para 400 habitaciones
> un monolito modular también funcionaría, y está escrito así en el ADR 001. La
> ventaja que sí es real y acabamos de mostrar es la tolerancia a fallos
> parciales.

**«¿Qué pasa si un evento llega dos veces?»**
> No se duplica nada. Cada consumidor guarda los `eventoId` procesados y comprueba
> antes de aplicar. La clave es `eventoId`, no `ordenId`, porque una misma orden
> genera tres eventos distintos con el mismo `ordenId`.

**«¿Y si se cae RabbitMQ?»**
> Los servicios reconectan solos, no hay que reiniciarlos. Las colas son durables
> y los mensajes persistentes, así que sobreviven a un reinicio del broker.

**«¿Cómo mantienen la consistencia sin transacciones distribuidas?»**
> No la mantenemos: aceptamos consistencia eventual y compensamos. Si la
> habitación se bloqueó y la orden no se pudo guardar, se libera con una llamada
> de compensación. Y hay un caso que **no** está resuelto: si la orden se guarda
> pero el evento no se publica, la orden queda sin técnico. La solución es el
> patrón *outbox* y está documentada como pendiente.

**«¿Aplicaron SOLID?»**
> Sí, y sabemos dónde no. Ver documento 03 — la respuesta preparada está al final.

**«¿Por qué dos lenguajes?»**
> Para demostrar que el contrato es lo que une, no el lenguaje. El caso
> interesante es `orden.asignada`: lo produce Python y lo consume C#. Si Python
> serializara con sus nombres internos, C# no entendería nada — por eso el
> contrato exige camelCase en el cable.

**«¿Esto tiene seguridad?»**
> No. El login pide un nombre sin contraseña y solo sirve para identificar quién
> reporta; el backend no lo verifica. La autenticación sería trabajo del gateway
> y no está en la v1. La propia pantalla se lo dice al usuario.

---

## Si algo falla en vivo

| Síntoma | Qué hacer |
|---|---|
| La UI no carga | `Ctrl+F5`. Si sigue: `docker compose restart ui` |
| Un servicio no responde | `docker compose logs <servicio> --tail 40` |
| Todo raro | `docker compose down && docker compose up -d` (mantiene los datos) |
| Empezar de cero | `docker compose down -v && docker compose up -d --build` (borra las bases y vuelve a sembrar) |

> **Si algo se rompe delante del profesor, mostrar los logs.** Un
> `docker compose logs ordenes` con el error a la vista demuestra que se sabe
> depurar un sistema distribuido — que es justamente lo difícil del estilo.
