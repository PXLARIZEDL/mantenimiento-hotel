# 5. Para la presentación

El profesor pidió destacar **lo que nos pareció interesante e importante**. Esa
parte es la que separa una entrega de una exposición: no es repetir qué se hizo,
sino qué se aprendió haciéndolo.

---

## Guion de diapositivas sugerido

| # | Diapositiva | Contenido | Tiempo |
|---|---|---|---|
| 1 | Portada | Título, integrantes, materia, fecha | — |
| 2 | El problema | Las tres fallas del cuaderno y el radio | 1 min |
| 3 | Antecedentes | CMMS y sistemas hoteleros: el flujo ya existe, lo nuestro es la partición | 1 min |
| 4 | Antecedentes del estilo | CORBA → SOA → microservicios, y qué se aprendió de cada fracaso | 2 min |
| 5 | Arquitectura | El diagrama de los seis servicios | 1 min |
| 6 | Dónde cortamos y por qué | Propiedad del dato, no tamaño | 2 min |
| 7 | Comunicación | Una sola llamada sincrónica, y por qué esa | 2 min |
| 8 | Resiliencia | Timeout + reintento + breaker, y por qué en ese orden | 2 min |
| 9 | Políglota | El contrato une, no el lenguaje | 1 min |
| 10 | SOLID | Un acierto y una violación, ambos concretos | 2 min |
| 11 | **Lo que nos pareció interesante** | Las cuatro de abajo | 3 min |
| 12 | Pendientes | Lo que falta, dicho por nosotros | 1 min |
| 13 | **Demostración en vivo** | Documento 04 | 12 min |

---

## Lo que nos pareció interesante

Esto es lo que hay que contar con ganas. Son cosas que **pasaron**, no teoría
copiada.

### 1. El bug que solo apareció al ejecutar

Todo compilaba. Los contratos cuadraban. Las pruebas de contrato pasaban. La
primera vez que se levantó el sistema completo, se descubrió que **la habitación
quedaba bloqueada para siempre**.

La causa: `ordenes` generaba un `ordenId` para bloquear el cuarto, pero la
entidad `Orden` acuñaba **otro GUID distinto** al crearse. El cuarto quedaba
bloqueado con un identificador y la orden nacía con otro. Al resolverla, se
mandaba un id que no estaba en la lista del cuarto.

**Y lo peor: respondía `200 OK`.** `habitaciones` interpreta "no encontré esa
orden" como un reintento ya aplicado — que es lo correcto para un reintento
legítimo — así que el fallo era silencioso.

> **Por qué vale contarlo:** es el costo real de no tener transacciones
> distribuidas, demostrado en vez de explicado. Dos servicios quedaron
> inconsistentes entre sí y **ninguno de los dos se dio cuenta**. En un monolito,
> una clave foránea lo habría impedido en la primera línea.
>
> Y la lección de método: **compilar no es funcionar**. Este error sobrevivió a
> la compilación, a la revisión de contratos y a las pruebas unitarias.

### 2. El orden del reintento y el circuit breaker

Parece un detalle de configuración y decide si el patrón funciona o no.

```
reintento  →  circuit breaker  →  timeout por intento
```

Si se pone al revés —el breaker por fuera— **cada tanda completa de reintentos
cuenta como un solo fallo**. El breaker necesitaría decenas de fallos reales para
abrirse, y en la práctica nunca abriría.

> **Por qué vale contarlo:** un patrón bien nombrado y mal ordenado no protege
> nada, y desde fuera parece implementado. Es el tipo de error que no se ve en
> una revisión de código superficial.

### 3. El contrato es lo que une, no el lenguaje

`orden.asignada` lo **produce Python** y lo **consume C#**.

Python serializa en `snake_case` por defecto. C# espera `camelCase`. Si nadie
fija la convención, el mensaje viaja bien, llega bien, se parsea bien… y todos
los campos llegan vacíos.

Por eso los contratos de `contratos/*.json` fijan **camelCase en el cable**, y
`modelos.py` serializa siempre con `by_alias=True`.

> **Por qué vale contarlo:** es la demostración concreta de que en un sistema
> políglota el acuerdo está en el formato del mensaje, no en el lenguaje. Se
> verificó comparando los campos que publica Python contra el archivo del
> contrato: coinciden exactamente, ninguno falta y ninguno sobra.

### 4. Los campos que se quitaron de un contrato rompieron una función

Al cerrar los contratos se quitó el campo `habitacionLiberada` de
`orden.resuelta`. Parecía un campo de más.

Resultó que `notificaciones` lo usaba para una distinción real: **no decir
"habitación disponible" si el cuarto sigue bloqueado por otra orden abierta**.

Sin ese campo, el aviso ya no puede afirmar disponibilidad. La solución fue
cambiar la redacción para que pida verificar antes de asignar el cuarto —
recuperar la función obligaría a subir el contrato a `v2`, porque `v1` ya está
publicado.

> **Por qué vale contarlo:** enseña qué significa de verdad *"un contrato
> publicado no se cambia"*. Quitar un campo no es un renombre: es **eliminar una
> capacidad** de un servicio que ni siquiera estaba en la sala cuando se decidió.

---

## Diagrama para la diapositiva 5

```
                        ┌──────────┐
   navegador ─────────▶ │    ui    │ (nginx)
                        └────┬─────┘
                             │ HTTP
                        ┌────▼─────┐
                        │ gateway  │  ← único puerto publicado
                        └────┬─────┘
            ┌────────────────┼────────────────┬──────────────┐
            │                │                │              │
      ┌─────▼──────┐   ┌─────▼────┐    ┌──────▼─────┐  ┌─────▼────────┐
      │habitaciones│◀──│ ordenes  │    │  tecnicos  │  │notificaciones│
      │   (C#)     │HTTP│  (C#)   │    │  (Python)  │  │   (Python)   │
      └─────┬──────┘   └─────┬────┘    └──────┬─────┘  └──────┬───────┘
            │                │                │               │
        ┌───▼───┐        ┌───▼───┐        ┌───▼───┐       (sin base:
        │  BD   │        │  BD   │        │  BD   │        en memoria)
        └───────┘        └───────┘        └───────┘
                             │                │               │
                             └────────────────┴───────────────┘
                                      RabbitMQ
                                  exchange hotel.eventos

  ── HTTP sincrónico (una sola: ordenes → habitaciones)
  ── eventos: orden.creada · orden.asignada · orden.resuelta
```

---

## Frases que conviene tener preparadas

Sobre la elección del estilo:

> «Elegimos microservicios y sabemos lo que costó. Para 400 habitaciones, un
> monolito modular también resolvía el problema. Lo que ganamos y pudimos
> demostrar es que se cae un servicio y el negocio sigue operando.»

Sobre la honestidad técnica:

> «Hay un caso que no resolvimos: si la orden se guarda pero el evento no se
> publica, la orden queda sin técnico. Sabemos cuál es la solución —el patrón
> outbox— y está documentada como pendiente en vez de escondida.»

Sobre lo aprendido:

> «Lo que más nos sorprendió es cuánto de esta arquitectura son decisiones
> pequeñas que parecen detalles de configuración y en realidad deciden si el
> sistema funciona: el orden del reintento respecto del breaker, la convención de
> nombres en el cable, qué campo se usa como clave de idempotencia.»

---

## Reparto sugerido de la exposición

Con un integrante por área, cada uno defiende lo suyo:

| Integrante | Diapositivas | Demo |
|---|---|---|
| Quien hizo `ordenes` y los contratos | 5-8 (arquitectura, comunicación, resiliencia) | Demos 1 y 3 |
| Quien hizo `gateway` y los ADR | 2-4 (problema y antecedentes) | Demo 4 |
| Quien hizo `notificaciones` | 9, 11 (políglota, lo interesante) | Demo 2 |
| Quien hizo `habitaciones` / `tecnicos` | 6, 10 (límites, SOLID) | Apoyo |

**Regla:** nadie explica código que no escribió. Si preguntan por un servicio
ajeno, lo contesta su autor — eso mismo demuestra que hubo reparto real.
