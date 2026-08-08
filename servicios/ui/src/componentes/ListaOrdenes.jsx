{/*
  PROPÓSITO: mostrar las órdenes de mantenimiento y su avance por los tres
    estados. Es la pantalla donde se DEMUESTRA que el sistema distribuido
    funciona: una orden aparece ABIERTA y, segundos después, pasa a ASIGNADA sin
    que nadie la toque.

  DEBE CONTENER:
    1. La carga del listado desde src/api.js al montar el componente.
    2. Una tabla con: número de orden, habitación, tipo de falla, prioridad,
       estado, técnico asignado y fechas.
    3. Distintivo visual por estado: ABIERTA, ASIGNADA, RESUELTA.
    4. Filtros por estado y por número de habitación.
    5. El botón "Resolver" en las órdenes que lo admitan, con un campo para la
       nota de cierre del técnico.
    6. Un recargado periódico (o un botón de refresco bien visible) para ver la
       transición a ASIGNADA: la asignación llega por evento y la UI no escucha
       la cola.
    7. Una indicación de "esperando asignación" en las órdenes que llevan un rato
       ABIERTAS: puede ser que no haya técnico disponible de esa especialidad.
    8. Estados de carga, lista vacía y error.

  NO DEBE CONTENER:
    1. Un botón para asignar técnico a mano: la asignación es automática y llega
       por el evento orden.asignada.
    2. Un botón para saltar de ABIERTA a RESUELTA sin pasar por ASIGNADA si el
       backend no lo permite; el estado lo valida Modelos/Orden.cs.
    3. Consultas al servicio tecnicos para completar el nombre del técnico: ese
       nombre ya viene copiado dentro de la orden, a propósito.
    4. Llamadas fetch propias ni lógica de transición de estados.

  RELACIONADO:
    - GET /ordenes y PUT /ordenes/{id}/resolver (servicio ordenes, vía gateway)
    - servicios/ordenes/Endpoints/OrdenesEndpoints.cs
    - contratos/orden.asignada.v1.json (de ahí sale el nombre del técnico que
      aparece en la tabla)
*/}
