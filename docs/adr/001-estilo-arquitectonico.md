# ADR 001 — Estilo arquitectónico: microservicios

- **Estado:** aceptado
- **Fecha:** 2026-08-09
- **Decide:** el equipo de arquitectura del proyecto

---

## Contexto

- **¿Qué problema concreto del hotel resuelve el sistema?:** Resuelve la automatización del flujo de gestión de fallas de mantenimiento (reporte, bloqueo inmediato de la habitación afectada, asignación de técnico adecuado por especialidad/turno y aviso a recepción).
- **¿Cuántas personas lo van a mantener?:** Un equipo pequeño de desarrollo.
- **¿Hay partes que cambien a ritmos muy distintos entre sí?:** Sí, el catálogo de habitaciones es casi estático, el flujo de órdenes es transaccional y dinámico, y el módulo de técnicos se rige por horarios y turnos de personal. Además, el sistema requiere stacks tecnológicos políglotas (.NET 8 para servicios empresariales principales y Python 3.12/FastAPI para técnicos y notificaciones).
- **¿Hay partes que necesiten escalar de forma independiente?:** Sí, la consulta y notificación de estado puede tener mayor frecuencia de acceso que la configuración de habitaciones.
- **¿Qué restricciones impone que el proyecto sea universitario?:** Requiere que la infraestructura se ejecute de manera sencilla y eficiente en una sola máquina de desarrollo mediante `docker-compose`, evitando sobreingeniería como Kubernetes o Service Mesh.

---

## Decisión

Se adopta un estilo de **microservicios** con cinco servicios y una UI, donde cada
servicio es dueño de su base de datos y se comunican por HTTP (una sola llamada) y
por eventos en RabbitMQ.

---

## Alternativas consideradas

### A. Monolito modular

- **A favor:** Simplicidad de despliegue en un solo proceso, cero latencia de red interna y posibilidad de usar transacciones ACID locales.
- **En contra:** Dificulta el uso de un stack políglota (.NET y Python) y no impone el aislamiento estricto de datos entre dominios.
- **Por qué no se eligió:** Se requería demostrar el patrón de microservicios con comunicación asincrónica por eventos y propiedad explícita de datos.

### B. Monolito con módulos y una sola base

- **A favor:** Menos consumo de recursos de memoria en la máquina de desarrollo y configuración sencilla.
- **En contra:** Alto riesgo de acoplamiento en la base de datos (JOINs accidentales entre dominios) y punto único de fallo.

### C. Microservicios (elegida)

- **A favor:** Aislamiento total de dominios, propiedad de datos garantizada por servicio, soporte para stack políglota (.NET + Python) y despliegues independientes.
- **En contra:** Mayor complejidad operativa: latencia de red, consistencia eventual, necesidad de gestión de idempotencia y depuración distribuida.

---

## Consecuencias

- **¿Qué se volvió más difícil?:** La rastreabilidad de peticiones a través de servicios y la depuración de fallos distribuidos.
- **¿Qué garantía se perdió?:** Se perdieron las transacciones ACID distribuidas inmediatas. Se adopta consistencia eventual para las fases asincrónicas.
- **¿Qué se gana concretamente?:** Aislamiento de fallos (si el servicio de notificaciones o el de asignaciones se detiene, se pueden seguir registrando bloqueos de habitación) y código altamente enfocado.
- **¿Cuándo habría que revisar esta decisión?:** Si la sobrecarga de mantenimiento de contenedores locales supera los beneficios de aislamiento para el equipo.

---

## Relacionado

- `002-limites-contextos.md` (dónde se cortó)
- `003-estrategia-comunicacion.md` (cómo se hablan)
- `../limites-descartados.md` (qué particiones se evaluaron)

