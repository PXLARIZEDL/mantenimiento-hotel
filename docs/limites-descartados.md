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
- **Por qué se descartó:** *responder*. Pista: ¿alguien consulta el catálogo sin
  querer saber el estado?

---

## 2. Un servicio `notificaciones` por canal (correo, SMS, panel)

- **La idea:** un microservicio por medio de envío.
- **Por qué se pensó:** cada canal tiene su proveedor y su tasa de fallo.
- **Por qué se descartó:** *responder*. Pista: hoy solo se guarda en memoria.

---

## 3. Un servicio `auditoria` que consuma todos los eventos

- **La idea:** cola con binding `#` que persista todo lo que pasa.
- **Por qué se pensó:** trazabilidad y depuración.
- **Por qué se descartó:** *responder*. Pista: alcance del proyecto universitario.

---

## 4. Fusionar `ordenes` y `tecnicos` en un solo servicio

- **La idea:** la asignación es parte del ciclo de vida de la orden.
- **Por qué se pensó:** elimina un evento y una base de datos.
- **Por qué se descartó:** *responder*. Pista: ¿quién es dueño del turno y la
  especialidad?, ¿cambia por las mismas razones que el estado de la orden?

---

## 5. Un `bff` (backend for frontend) además del gateway

- **La idea:** una capa que agregue datos de varios servicios para la UI.
- **Por qué se pensó:** `ListaOrdenes` necesita datos de tres servicios.
- **Por qué se descartó:** *responder*. Pista: ¿cuántas pantallas hay?

---

## 6. Base de datos compartida entre los tres servicios con PostgreSQL

- **La idea:** una sola instancia, tres esquemas.
- **Por qué se pensó:** menos contenedores, menos memoria en la laptop.
- **Por qué se descartó:** *responder*. Pista: ¿qué impide técnicamente que un
  servicio lea la tabla de otro si comparten instancia?

---

## Criterio usado para trazar los límites finales

Responder en dos o tres frases: ¿el criterio fue el dato del que se es dueño, el
equipo que lo mantendría, o el ritmo de cambio? Debe coincidir con lo escrito en
`adr/002-limites-contextos.md`.
