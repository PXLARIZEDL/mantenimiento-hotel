# 3. SOLID en el código

Los cinco principios contra el código real, con archivo y línea. **Incluye dónde
NO se cumple**, porque una auditoría que solo encuentra aciertos no es una
auditoría.

Veredicto rápido:

| Principio | Veredicto |
|---|---|
| **S** — Responsabilidad única | Parcial: excelente en el dominio, violado en los endpoints |
| **O** — Abierto/cerrado | Bueno, con una grieta conocida |
| **L** — Sustitución de Liskov | Apenas ejercitado; sin violaciones |
| **I** — Segregación de interfaces | **Se cumple limpio** |
| **D** — Inversión de dependencias | Parcial: bien en HTTP y eventos, ausente en datos |

---

## S — Responsabilidad única

> *Una clase debe tener una sola razón para cambiar.*

### Dónde se cumple bien

**`servicios/tecnicos/asignador.py`** — el mejor ejemplo del proyecto.

Contiene **la regla de negocio y nada más**. Sus únicos imports son
`logging`, `uuid`, `dataclasses`, `datetime` y los enums del dominio
(línea 20). **No importa SQLAlchemy ni aio-pika.**

Consecuencia verificable: se puede probar la regla completa —turno correcto,
desempate por carga, caso sin técnico, tipo de falla inválido— **sin levantar
PostgreSQL ni RabbitMQ**. Está comprobado.

> *Razón para cambiar:* que cambie la política de asignación. Ninguna otra.

**`servicios/ordenes/Modelos/Orden.cs`** — la entidad protege su máquina de
estados y nada más. Sin atributos de EF (el mapeo vive en `Datos/`), sin HTTP,
sin publicar eventos. La entidad no conoce la red.

**`servicios/ordenes/Clientes/HabitacionesClient.cs`** — solo traduce HTTP a
dominio. **Las políticas de resiliencia no están aquí**: se registran sobre el
cliente en `Program.cs`. El archivo define *qué* se llama; `Program.cs` define
*con qué garantías*.

**`servicios/notificaciones/plantillas.py`** — solo redacta texto. No toca
RabbitMQ ni el almacén.

### Dónde NO se cumple

**`servicios/ordenes/Endpoints/OrdenesEndpoints.cs:104` — `CrearAsync`, 171 líneas.**

Hace seis cosas: valida la petición, llama a `habitaciones` por HTTP, persiste la
orden, compensa si falla, publica el evento y mapea la respuesta HTTP.

Es un *transaction script* dentro de un handler: **no hay capa de servicio de
aplicación**. `ResolverAsync` tiene el mismo patrón en 89 líneas.

Cómo se arreglaría: extraer un `ServicioCrearOrden` que reciba
`IHabitacionesClient` e `IPublicadorEventos` y devuelva un resultado de dominio;
el endpoint quedaría en unas 20 líneas de traducción HTTP.

**No se hizo**, y la razón está en el documento 06: agregar esa capa a estas
alturas es más riesgo que beneficio. Pero es una violación real y conviene
admitirla antes de que la encuentren.

**`servicios/tecnicos/base_datos.py`** — mezcla conexión, sesiones, siembra
inicial, consultas e idempotencia. Sembrar no es acceso a datos.

**`servicios/notificaciones/main.py`** — la app FastAPI, el almacén en memoria y
el ciclo de vida conviven en un módulo.

---

## O — Abierto/cerrado

> *Abierto a extensión, cerrado a modificación.*

### Dónde se cumple

**`servicios/notificaciones/plantillas.py:231` — `PLANTILLAS_POR_TIPO_EVENTO`.**
Tabla de despacho: agregar un tipo de evento es **agregar una entrada al
diccionario**, sin tocar el consumidor.

**Los valores de resiliencia salen de configuración.** Cambiar el timeout o el
umbral del breaker no toca una línea de código: es `appsettings.json` o una
variable de entorno.

**`TransicionesOrden.Permitidas`** declara las transiciones válidas **como
datos**, no como una cadena de `if`.

**Las rutas del gateway son datos** en `appsettings.json`, no código. Agregar un
destino no recompila nada.

### Dónde NO se cumple

**`servicios/notificaciones/consumidor.py:162`** — `if tipo_evento == "orden.creada"`.

Ese `if` resuelve de dónde sacar el número de habitación, y **rompe justo el OCP
que la tabla de despacho consigue**: un evento nuevo que traiga la habitación
obliga a editarlo.

Es honesto decir que se introdujo al integrar el servicio con los contratos
finales, y que la solución limpia sería que cada plantilla declarara cómo obtiene
su habitación.

> Los enums (`TipoFalla`, `EstadoOrden`, `Especialidad`) también obligan a
> modificar para extender. Eso es **inherente a un conjunto cerrado** y es
> deseable: agregar un estado debe ser una decisión revisada, no algo que se
> cuela.

---

## L — Sustitución de Liskov

> *Un subtipo debe poder usarse donde se espera el tipo base, sin sorpresas.*

**Veredicto honesto: apenas está ejercitado.** Hay dos interfaces y **una sola
implementación de cada una**. El resto de la herencia la impone el framework
(`DbContext`, `BackgroundService`, `BaseModel`, la `Base` de SQLAlchemy).

No se encontraron violaciones. Pero decir "cumplimos Liskov" cuando no hay nada
que sustituir es flojo.

**Lo que sí es defendible de verdad:**

`HabitacionesClient` (`Clientes/HabitacionesClient.cs:57`) **nunca lanza
excepciones donde la interfaz no lo implica**. Ante un timeout, un `404` o el
circuito abierto, devuelve un `RespuestaHabitaciones` con el resultado de
dominio; no propaga `HttpRequestException` ni `BrokenCircuitException`.

Eso significa que **cualquier implementación alternativa** —una falsa para
pruebas, una que hable gRPC— se comporta igual desde el punto de vista del
llamador. Quien usa `IHabitacionesClient` no tiene que saber cuál le tocó. Eso es
Liskov.

---

## I — Segregación de interfaces

> *Nadie debe depender de métodos que no usa.*

**Es el principio mejor cumplido del proyecto.**

| Interfaz | Métodos | Uso |
|---|---|---|
| `IPublicadorEventos` (`Eventos/PublicadorEventos.cs:38`) | **1** — `PublicarAsync` | Se usa |
| `IHabitacionesClient` (`Clientes/HabitacionesClient.cs:48`) | **2** — bloquear y liberar | Ambos se usan |

No hay interfaces gordas que obliguen a implementar lo que no se necesita, ni
métodos que lancen `NotImplementedException`.

Detalle menor: `ResolverAsync` solo necesita `LiberarAsync` pero recibe la
interfaz completa. Partirla en dos por eso sería peor que el problema.

---

## D — Inversión de dependencias

> *Depender de abstracciones, no de implementaciones.*

### Dónde se cumple

`OrdenesEndpoints` recibe **`IHabitacionesClient`** e **`IPublicadorEventos`**,
no las clases concretas. Se registran en `Program.cs`, y ahí mismo se les aplican
las políticas de Polly.

El efecto práctico: se puede cambiar cómo se habla con `habitaciones` —o
envolverlo en reintentos— **sin tocar el código que orquesta el caso de uso**.

### Dónde NO se cumple

**1. No existe ninguna abstracción de datos en todo el repositorio.**

`OrdenesEndpoints`, `ConsumidorOrdenAsignada` y los endpoints de `habitaciones`
dependen del **`DbContext` concreto**.

> **Esto tiene defensa buena, y hay que saber darla.** EF Core ya implementa
> *Unit of Work* y *Repository* internamente; envolverlo en otro repositorio es
> una discusión abierta y muchos lo consideran sobre-abstracción. Es un
> **trade-off deliberado**, no un descuido. Pero por la letra del principio, es
> una dependencia concreta, y conviene decirlo así.

**2. `servicios/notificaciones/consumidor.py:53` importa de `main.py`.**

El módulo de bajo nivel (el consumidor) depende del de alto nivel (la app). El
ciclo hubo que romperlo con **imports diferidos** dentro de funciones
(`main.py:157` y `main.py:256`).

**Ese import diferido es el síntoma.** La solución limpia sería un módulo
`almacen.py` del que dependan los dos.

**3. `servicios/tecnicos/base_datos.py:17`** importa `CandidatoTecnico` de
`asignador.py`: la capa de datos depende del módulo de reglas. Menor —es un DTO—
pero la dependencia va al revés de lo esperado.

---

## Resumen para la defensa

Si preguntan **"¿aplicaron SOLID?"**, la respuesta que sostiene:

> Sí, y sabemos dónde no. El mejor ejemplo es `asignador.py`: la regla de negocio
> no importa la base ni el broker, así que se prueba sola — eso es
> responsabilidad única con una consecuencia medible, no una etiqueta.
>
> La violación más clara es `CrearAsync`, con 171 líneas haciendo seis cosas: le
> falta una capa de servicio de aplicación. No la agregamos porque a esta altura
> era más riesgo que beneficio, y preferimos documentarlo antes que dejarlo
> escondido.
>
> Liskov casi no aplica: hay una sola implementación por interfaz. Lo que sí
> cumplimos es que el cliente HTTP nunca lanza excepciones donde su interfaz no
> lo implica, así que cualquier sustituto se comporta igual.
