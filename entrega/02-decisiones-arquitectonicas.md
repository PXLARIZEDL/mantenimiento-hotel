# 2. Decisiones arquitectónicas y su justificación

Cada decisión con su alternativa descartada. Si una decisión no tiene alternativa
descartada, no era una decisión: era lo único que se sabía hacer.

---

## 1. Diferencia entre arquitectura y diseño

El profesor pidió distinguirlas. La forma corta:

| | Arquitectura | Diseño |
|---|---|---|
| **Pregunta** | ¿En qué piezas se parte y cómo hablan? | ¿Cómo se organiza el código dentro de una pieza? |
| **Costo de cambiarla** | Alto: afecta a varios equipos y despliegues | Bajo: es refactor dentro de un servicio |
| **En el proyecto** | Seis servicios, base por servicio, RabbitMQ, un gateway | Máquina de estados de `Orden`, la regla en `asignador.py` |
| **Documento** | `docs/adr/001`, `002`, `003` | Este documento §7, y el documento 03 (SOLID) |

Un ejemplo concreto de la diferencia, sacado del proyecto:

- **Arquitectura:** que `tecnicos` sea el dueño de la regla de asignación y que
  `ordenes` se entere por un evento. Cambiarlo obliga a tocar dos servicios, dos
  bases y un contrato.
- **Diseño:** que dentro de `tecnicos` la regla esté en `asignador.py`, separada
  de `base_datos.py`. Cambiarlo no lo nota nadie fuera del servicio.

---

## 2. Los seis servicios y por qué se cortó ahí

Mínimo pedido: 4. Implementados: **6**.

| Servicio | Stack | Base | De qué es dueño |
|---|---|---|---|
| `habitaciones` | C# / .NET 8 | PostgreSQL | Inventario y **estado** de los 400 cuartos |
| `ordenes` | C# / .NET 8 | PostgreSQL | Ciclo de vida de la orden; orquesta el caso de uso |
| `tecnicos` | Python 3.12 / FastAPI | PostgreSQL | Personal, especialidades, turnos y **la regla de asignación** |
| `notificaciones` | Python 3.12 / FastAPI | — (memoria) | Avisos a recepción |
| `gateway` | C# / .NET 8 + YARP | — | Único punto de entrada |
| `ui` | React + Vite + nginx | — | Interfaz de recepción |

**El criterio del corte fue la propiedad del dato, no el tamaño.** Cada servicio
es dueño exclusivo de algo que nadie más escribe:

- El **estado de un cuarto** es de `habitaciones`. Que sea de un solo dueño es lo
  que impide vender un cuarto roto.
- El **turno y la especialidad** son de `tecnicos`. Por eso la regla de
  asignación vive ahí y no en `ordenes`, aunque `ordenes` sea quien orquesta.

### Alternativas descartadas

Están documentadas en `docs/limites-descartados.md`. Las dos más interesantes:

- **Fusionar `ordenes` y `tecnicos`.** Elimina un evento y una base. Se descartó
  porque los turnos del personal cambian por motivos que no tienen nada que ver
  con las órdenes: son dos dominios con dueños distintos en el hotel.
- **Separar `habitaciones` de `inventario`** (catálogo físico vs. estado
  operativo). Cambian a ritmos muy distintos, que es un buen motivo. Se descartó
  porque siempre se leen juntos: separarlos añadía una llamada HTTP sin ganar
  nada a esta escala.

---

## 3. Comunicación: una sola llamada sincrónica

```
ordenes  ──HTTP──▶  habitaciones      (la única sincrónica)
       ──RabbitMQ──▶  tecnicos, notificaciones
```

**Por qué esa sí es sincrónica.** Bloquear la habitación es *condición para que
la orden exista*. Si no se puede bloquear, la orden no debe crearse — si no,
existiría una orden sobre un cuarto que se sigue vendiendo.

**Por qué las demás no.** La asignación del técnico puede tardar segundos sin que
nada malo pase: el cuarto ya está protegido. Esperar sincrónicamente a `tecnicos`
solo acoplaría la creación de la orden a que ese servicio esté vivo.

### Alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| Todo sincrónico (REST entre todos) | Si `notificaciones` se cae, no se pueden crear órdenes. Un servicio secundario tumbaría el principal |
| Todo asincrónico, incluida la habitación | El usuario recibiría "orden creada" sin garantía de que el cuarto se bloqueó. Es la falla que el sistema existe para evitar |
| Transacción distribuida / 2PC | Complejidad alta, bloqueo de recursos y mal soporte en un sistema políglota |

---

## 4. Resiliencia de la llamada sincrónica

Los tres mecanismos exigidos, en `servicios/ordenes/appsettings.json` →
`Habitaciones:Resiliencia`. **Ningún valor está escrito en el código.**

| Mecanismo | Valor | Por qué ese |
|---|---|---|
| Timeout | 3 s por intento | Bloquear un cuarto es una escritura simple; más de 3 s es señal de problema |
| Reintento | 3, exponencial desde 200 ms **con jitter** | Sin jitter, todas las instancias reintentan a la vez y vuelven a tumbar al servicio justo cuando se recupera |
| Circuit breaker | Abre con 50 % de fallos, mín. 8 llamadas en 30 s; 30 s abierto | El mínimo evita que dos fallos aislados en un momento de poco tráfico abran el circuito |

### El detalle que más se pregunta: el orden

```
reintento  →  circuit breaker  →  timeout por intento
```

El reintento va **por fuera** del breaker. Así **cada intento individual
atraviesa el breaker y alimenta su contador**. Al revés, el breaker vería un solo
fallo por cada tanda completa de reintentos y prácticamente nunca llegaría a
abrirse.

### Qué se reintenta y qué no

- **Sí:** timeout, fallo de red, `5xx`. Son transitorios.
- **No:** `400`, `404`, `409`. La respuesta no cambia por insistir —
  **reintentar un `409` es un bug**, no una precaución.

Con el circuito abierto, `POST /ordenes` responde **`503`**, no `500`: comunica
"la dependencia no está, reintentá" en vez de "algo se rompió acá".

---

## 5. Sistema políglota: C# y Python

| | C# / .NET 8 | Python 3.12 / FastAPI |
|---|---|---|
| Servicios | `habitaciones`, `ordenes`, `gateway` | `tecnicos`, `notificaciones` |
| Por qué | Tipado fuerte y EF Core para el núcleo transaccional, donde una orden mal guardada cuesta dinero | Rapidez de escritura para reglas y plantillas de texto, que cambian más |

**Lo que hace posible que convivan es el contrato, no el lenguaje.** Los tres
archivos de `contratos/*.json` fijan la forma exacta de cada evento.

El punto más delicado del proyecto está aquí: **`orden.asignada` lo produce
Python y lo consume C#**. Si Python serializara con sus nombres internos
(`snake_case`), C# no entendería nada. Por eso `modelos.py` serializa siempre con
`by_alias=True` y el contrato exige **camelCase en el cable**.

> Está verificado por ejecución: los campos que publica `tecnicos` coinciden
> exactamente con `contratos/orden.asignada.v1.json` — ninguno falta y ninguno
> sobra.

---

## 6. Docker

Diez contenedores: RabbitMQ, tres PostgreSQL, cinco servicios y la UI.

**Decisiones que importan:**

- **Una base de datos por servicio, con volumen propio.** Nunca compartida. Es
  la garantía física de que nadie lee los datos de otro: aunque quisiera, no
  tiene credenciales ni ruta.
- **Solo el gateway y la UI publican puerto** hacia el host. Los servicios de
  dominio no son alcanzables desde afuera.
- **`ordenes` NO depende de `habitaciones`** en `depends_on`, a propósito: debe
  arrancar aunque esté caída. Para eso existe el circuit breaker.
- **Healthchecks con `condition: service_healthy`**, para no arrancar un servicio
  contra una base a medio iniciar.
- **Ningún valor literal**: todo entra por `${VARIABLE}` desde `.env`.
- **Imágenes multietapa**: se compila con el SDK y se ejecuta con el runtime. La
  imagen final de la UI no lleva Node, solo nginx con los archivos ya
  construidos.

---

## 7. Decisiones de diseño (dentro de cada servicio)

### El estado de la habitación es una lista, no un `ordenId`

`Habitacion.OrdenesActivas` guarda **todas** las órdenes abiertas del cuarto.

**Por qué:** si un cuarto tiene dos fallas y se resuelve una, no se libera. Solo
vuelve a `DISPONIBLE` cuando la lista queda vacía. Liberarlo antes devolvería al
inventario un cuarto que sigue roto — la falla original.

*Verificado en ejecución: dos órdenes sobre el mismo cuarto, resolver la primera
lo deja bloqueado y solo la segunda lo libera.*

### La idempotencia va por `eventoId`, no por `ordenId`

Cada consumidor guarda los `eventoId` ya procesados y comprueba **antes** de
aplicar nada.

**Por qué no `ordenId`:** una misma orden produce `orden.creada`,
`orden.asignada` y `orden.resuelta`, los tres con el mismo `ordenId`. Filtrar por
él descartaría eventos legítimos.

El efecto y el registro se guardan **en la misma transacción**; si no, un fallo
entre ambos reabre la ventana del duplicado.

### El nombre del técnico se copia dentro del evento

`orden.asignada` lleva `tecnicoNombre`, aunque el dueño del dato sea `tecnicos`.

**Por qué:** para que `notificaciones` pueda redactar el aviso aunque `tecnicos`
esté caído. Es *event-carried state transfer*: se acepta duplicar el dato para no
crear una dependencia en tiempo real.

El costo asumido: es el valor **en el momento del evento**. Si el técnico cambia
de nombre, el aviso ya emitido no se corrige.

### El desempate de la asignación es por menos carga

Cuando hay varios técnicos de la especialidad en turno, gana **el que menos
órdenes abiertas tiene**. A igualdad de carga, **por nombre**.

**Por qué carga y no antigüedad ni azar:** es la única de las tres que evita que
un técnico acumule la cola mientras otro del mismo turno está libre.

**Por qué el desempate por nombre:** hace la decisión **determinista**. La misma
entrada da siempre la misma salida, que es lo que permite probar la función y
reproducir un caso concreto en la defensa.

### Los turnos se calculan con huso horario

Los eventos viajan en **UTC** (lo fijan los contratos), pero los turnos son
horarios **locales**. Sin conversión, una falla reportada a las 20:00 locales
llegaría como 00:00 UTC y caería en el turno equivocado.

Por eso existe `HOTEL_UTC_OFFSET`, configurable. Es exactamente el tipo de valor
que se olvida y produce asignaciones absurdas.

---

## 8. Versionado de contratos

`contratos/` está bajo `CODEOWNERS`: cualquier cambio pasa por revisión.

| Tipo de cambio | Qué se hace |
|---|---|
| Agregar un campo que el consumidor puede ignorar | Campo opcional dentro de `v1` |
| Renombrar o quitar un campo | **`v2`**: archivo nuevo y routing key nueva |
| Cambiar el tipo o la lista de valores permitidos | **`v2`** |

La regla que lo hace funcionar: **el consumidor ignora los campos que no
conoce**. Sin eso, hasta agregar un campo opcional rompe a alguien.

Durante una migración a `v2`, el productor publica **las dos versiones** hasta
que todos los consumidores se muevan.

---

## 9. Integridad del CI

Hay **dos workflows separados**, y están separados a propósito: uno verifica los
*límites* de la arquitectura y el otro que el *código* funcione. Son dos
preocupaciones distintas, y así se puede revertir una sin tocar la otra.

### `limites.yml` — un pull request, un servicio

**Falla** si un PR modifica dos o más carpetas bajo `servicios/`.

No es burocracia: es la ley de Conway aplicada al revés. Si un cambio necesita
tocar dos servicios a la vez, casi siempre significa que hay acoplamiento que no
se declaró — y el CI obliga a hacerlo visible en vez de dejarlo pasar.

Los 14 pull requests del proyecto respetaron la regla.

### `verificacion.yml` — que lo que entra funcione

| Job | Qué comprueba |
|---|---|
| **Codificación** | Que ningún archivo de código esté fuera de UTF-8 |
| **Pruebas de tecnicos** | `pytest` sobre la regla de asignación |
| **Compilación C#** | Que `habitaciones`, `ordenes` y `gateway` compilen en Release, sin warnings |

El primero merece explicación, porque nació de un fallo real: un archivo `.py`
guardado en **cp1252** en vez de UTF-8 hace que Python 3 se niegue a leerlo
(`SyntaxError: Non-UTF-8 code`), y pytest ni siquiera lo recolecta. Pasó en este
repositorio, y **el CI dio verde igual** porque en ese momento no ejecutaba nada.

Es un riesgo específico de un proyecto políglota donde cada uno usa su editor en
Windows y el dominio está lleno de eñes y tildes (`MAÑANA`, `habitación`,
`plomería`). La comprobación cuesta segundos y evita un fallo que desde fuera
parece un problema de lógica.

> **Lección que vale para la defensa:** un CI que no ejecuta nada da una
> *garantía falsa*. Decía «pass» sobre un archivo que el intérprete no podía ni
> leer. Que exista el workflow no es lo mismo que estar verificando algo.
