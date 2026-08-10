# 6. Estado real y pendientes

Este documento existe para que **ustedes lleguen sabiendo lo que falta**, en vez
de que lo encuentre el profesor. Un pendiente admitido y explicado suma; uno
descubierto, resta.

---

## Qué funciona, verificado en ejecución

Todo lo de esta tabla se corrió de verdad con los diez contenedores arriba.

| Comprobación | Resultado |
|---|---|
| Los diez contenedores levantan | ✅ |
| Salud agregada: los cuatro servicios | ✅ `sano` |
| Reportar falla → habitación bloqueada | ✅ |
| Asignación automática por especialidad y turno | ✅ *Diego Matos, cerradura* |
| Resolver → habitación liberada | ✅ `DISPONIBLE`, lista vacía |
| Los tres eventos generan aviso | ✅ 3 avisos |
| Dos órdenes en un cuarto: resolver una no lo libera | ✅ |
| Apagar `notificaciones`: el resto sigue operando | ✅ |
| Recuperación desde la cola durable | ✅ 2 mensajes procesados |
| El contrato real parsea en el consumidor Python | ✅ |
| Los campos publicados coinciden con el contrato | ✅ ninguno falta ni sobra |
| Los tres servicios C# compilan sin warnings | ✅ `TreatWarningsAsErrors` |
| Las rutas `/api/*` del gateway | ✅ |
| La UI y sus seis prefijos proxeados | ✅ |

### Tamaño

| Servicio | Líneas |
|---|---|
| `ordenes` | 1 707 |
| `ui` | 1 131 |
| `tecnicos` | 986 |
| `notificaciones` | 764 |
| `habitaciones` | 588 |
| `gateway` | 148 |
| **Total** | **≈ 5 300** |

12 pull requests, todos con CI en verde.

---

## Pendientes, por orden de importancia

### 1. Outbox — el único hueco de corrección conocido

**Qué pasa:** `ordenes` guarda la orden y publica el evento en **dos pasos
separados**. Si RabbitMQ falla justo entre medio, la orden queda guardada pero
nunca se publica `orden.creada`. Consecuencia: **la orden se queda `ABIERTA` sin
técnico para siempre**.

**Qué se hace hoy:** se registra un log `Critical` y se responde `201`. La orden
existe y es válida; lo que se perdió es el disparo de la asignación.

**La solución correcta:** el patrón **Transactional Outbox**. Guardar el evento
en una tabla dentro de **la misma transacción** que la orden, y que un proceso
aparte lea esa tabla y publique. Así o se guardan las dos cosas o ninguna.

**Por qué no se hizo:** requiere una tabla más, un despachador en segundo plano y
manejo de reintentos. Se decidió documentarlo antes que implementarlo a medias.

> Si preguntan por consistencia, **esta es la respuesta honesta y la que mejor
> queda**: sabemos cuál es el problema, sabemos cómo se llama la solución y
> sabemos por qué no está.

### 2. Cobertura de pruebas incompleta

**Lo que sí hay:** `servicios/tecnicos/test_asignador.py`, con **6 casos** sobre
la regla de asignación. Se ejecutan en el CI.

Ninguno levanta PostgreSQL ni RabbitMQ, y eso es el punto: `asignador.py` recibe
los candidatos como parámetro justamente para poder probarse sin infraestructura.
**Es la prueba de que la separación de responsabilidades sirve para algo
concreto**, no solo para quedar bien en un diagrama.

| Caso | Qué protege |
|---|---|
| Desempate por menos carga | La regla de reparto |
| Descarta a quien no está en turno | Que el turno se filtre antes de desempatar |
| Sin técnico de esa especialidad | Que el caso se devuelva explícito y con motivo |
| `tipoFalla` fuera del contrato | Que no reviente ante un valor inválido |
| Desempate determinista | Que se pueda reproducir un caso en la defensa |
| Plantilla vacía | Que sea un caso normal, no un error |

**Lo que falta:** el consumidor de `tecnicos` (idempotencia, mensaje ilegible,
reintento acotado), los servicios C# y la UI. Nada de eso tiene una sola prueba.

Cubrir el consumidor es lo que más rendiría a continuación, porque la
idempotencia es una de las preguntas más probables de la defensa y hoy solo se
puede demostrar a mano.

### 3. `CrearAsync` con 171 líneas

Hace seis cosas: validar, llamar por HTTP, persistir, compensar, publicar y
mapear la respuesta. Le falta una capa de servicio de aplicación.

**Por qué no se refactorizó:** a esta altura, sacar una capa nueva es más riesgo
que beneficio. Está documentado en el documento 03 y hay una respuesta preparada.

### 4. Sin autenticación

El login pide un nombre **sin contraseña** y solo sirve para identificar quién
reporta. El backend no lo verifica; cualquiera con acceso a la red entra igual.

**La pantalla se lo dice al usuario**, para que nadie asuma una barrera que no
existe. Sería trabajo del gateway y no está en la v1.

### 5. `tecnicos` no consume `orden.resuelta`

El desempate de la asignación usa `ordenes_abiertas`, pero ese contador **cuenta
todas las asignaciones históricas**, porque el servicio no se entera de cuándo se
cierra una orden.

Con el tiempo, el criterio degenera de *"quién está menos ocupado ahora"* a
*"quién lleva menos órdenes en total"*.

**Se arregla** suscribiendo `tecnicos` a `orden.resuelta` y descontando. La cola
y el evento ya existen; falta el consumidor.

### 6. `habitacionLiberada` y la capacidad perdida

`notificaciones` ya no puede distinguir *"el cuarto está disponible"* de *"esta
orden se cerró pero el cuarto sigue bloqueado por otra"*, porque el campo se
quitó del contrato.

Hoy el aviso pide verificar el estado antes de asignar el cuarto. Recuperar la
distinción **obliga a una `v2`** de `orden.resuelta`.

### 7. Detalles menores

- El estado del **circuit breaker no se muestra** en el panel de salud, porque
  `ordenes` no lo expone por HTTP. El panel lo dice y explica cómo comprobarlo.
- **La lista de órdenes activas de un cuarto no caduca.** Si una orden nunca se
  cierra, el cuarto queda bloqueado hasta que alguien lo destrabe a mano.
- **No hay DLQ real.** El reintento del consumidor está acotado a dos entregas y
  luego descarta con log. Falta una cola de mensajes muertos y decidir quién la
  revisa.
- **No hay trazas distribuidas.** Depurar un flujo entre cinco servicios se hace
  leyendo logs de cada uno. Se descartó a propósito en
  `docs/limites-descartados.md`.

---

## Lo que falta para la entrega, no para el código

| Tarea | Estado |
|---|---|
| Nombres de los integrantes en los documentos | ⬜ pendiente |
| Verificar los enlaces de la bibliografía y poner fecha de consulta | ⬜ pendiente |
| Ajustar el formato de cita al que exija la materia | ⬜ pendiente |
| Armar las diapositivas (guion en el documento 05) | ⬜ pendiente |
| Ensayar la demo al menos una vez completa | ⬜ pendiente |
| Repartir quién expone qué | ⬜ pendiente |

> **La demo hay que ensayarla con el sistema recién levantado**, no con el que
> lleva dos horas corriendo. Es donde aparecen las sorpresas.

---

## Una nota sobre honestidad técnica

Varios de los pendientes de arriba podrían disimularse: quitar el log `Critical`
y no mencionar el outbox, no hablar del contador que no baja, no admitir que
`CrearAsync` es demasiado grande.

Se documentaron a propósito. En una defensa de arquitectura, **saber dónde está
el límite del propio diseño vale más que fingir que no lo tiene** — y un profesor
que sabe del tema los va a encontrar igual.
