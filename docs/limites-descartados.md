# Límites de contexto considerados y descartados

Este documento existe para dejar constancia de que los cinco servicios actuales no
son la única partición posible. Documentar lo que **no** se hizo vale tanto como
documentar lo que sí.

Formato para cada candidato: qué era, por qué se pensó, por qué se descartó.

---

## 1. Un servicio `habitaciones` separado de `inventario`

- **La idea:** separar el catálogo físico (número, piso, tipo) del estado operativo
  (DISPONIBLE / OCUPADA / FUERA_DE_SERVICIO).
- **Por qué se pensó:** cambian a ritmos muy distintos.
- **Por qué se descartó:** Para un hotel de 400 habitaciones, el inventario físico y su estado operativo siempre se leen y actualizan en conjunto. Separarlos requeriría consultas HTTP adicionales entre servicios sin beneficio real para la escala del proyecto.

---

## 2. Un servicio `notificaciones` por canal (correo, SMS, panel)

- **La idea:** un microservicio por medio de envío.
- **Por qué se pensó:** cada canal tiene su proveedor y su tasa de fallo.
- **Por qué se descartó:** En la arquitectura actual solo se guardan los avisos en memoria para el panel de recepción. No existen integraciones externas con SMS o proveedores de email, por lo que dividir el servicio por canal crearía una sobreingeniería innecesaria.

---

## 3. Un servicio `auditoria` que consuma todos los eventos

- **La idea:** cola con binding `#` que persista todo lo que pasa.
- **Por qué se pensó:** trazabilidad y depuración.
- **Por qué se descartó:** Supera el alcance del proyecto. La observabilidad básica y la trazabilidad del flujo se resuelven con el registro de logs estructurados en cada servicio y la inspección directa en la consola de RabbitMQ.

---

## 4. Fusionar `ordenes` y `tecnicos` en un solo servicio

- **La idea:** la asignación es parte del ciclo de vida de la orden.
- **Por qué se pensó:** elimina un evento y una base de datos.
- **Por qué se descartó:** El servicio `tecnicos` es dueño de la gestión de personal, turnos y especialidades, los cuales evolucionan independientemente de las órdenes. Fusionarlos acoplaría dos dominios de negocio distintos en una sola base de datos.

---

## 5. Un `bff` (backend for frontend) además del gateway

- **La idea:** una capa que agregue datos de varios servicios para la UI.
- **Por qué se pensó:** `ListaOrdenes` necesita datos de tres servicios.
- **Por qué se descartó:** El sistema solo cuenta con una aplicación frontend (`ui`) simple y de pocas pantallas. Añadir un BFF aumentaría la complejidad sin justificación; el gateway enruta de forma transparente y la UI o los eventos suministran los datos necesarios.

---

## 6. Base de datos compartida entre los tres servicios con PostgreSQL

- **La idea:** una sola instancia, tres esquemas.
- **Por qué se pensó:** menos contenedores, menos memoria en la laptop.
- **Por qué se descartó:** Compartir instancia facilita que por error o facilidad se realicen consultas o JOINs directos entre tablas de distintos servicios, violando el principio de aislamiento de datos y autonomía de despliegue.

---

## Criterio usado para trazar los límites finales

El criterio fundamental para trazar los límites finales fue la **propiedad exclusiva del dato de dominio** (*domain data ownership*) y el aislamiento por responsabilidad de negocio. Cada servicio es dueño absoluto de sus entidades y evoluciona según sus propias razones de cambio, tal como se documenta en `adr/002-limites-contextos.md`.

