{/*
  PROPÓSITO: formulario con el que un empleado reporta una falla. Es el DISPARADOR
    de todo el caso de uso: al enviarlo se bloquea la habitación, se crea la
    orden y arranca la cadena de eventos.

  DEBE CONTENER:
    1. Campo de número de habitación (1..400), con validación de formato y, si se
       puede, selección desde el listado de habitaciones.
    2. Selector de tipo de falla con exactamente las tres opciones del contrato:
       AIRE, PLOMERIA, CERRADURA. Deben coincidir con lo que espera el servicio
       tecnicos para elegir especialidad.
    3. Campo de descripción libre y selector de prioridad (BAJA, MEDIA, ALTA).
    4. Campo o valor de quién reporta.
    5. El envío mediante la función de crear orden de src/api.js, con el botón
       deshabilitado mientras la petición está en curso — enviar dos veces crea
       dos órdenes.
    6. Manejo diferenciado de los errores que puede devolver el backend:
         - 400: datos inválidos, se muestra en el campo correspondiente
         - 409: la habitación ya está fuera de servicio
         - 503: el servicio de habitaciones no responde (circuito abierto) —
                debe decir claramente que la orden NO se creó y que se puede
                reintentar
    7. Confirmación de éxito mostrando el número de orden creado.
    8. Un aviso al usuario de que el técnico se asigna en unos segundos, porque
       la asignación es ASINCRÓNICA y la orden aparecerá primero como ABIERTA.

  NO DEBE CONTENER:
    1. Un selector de técnico: la asignación es automática y la decide el
       servicio tecnicos. Ofrecerla aquí contradice el diseño.
    2. Un cambio directo del estado de la habitación; eso lo hace ordenes.
    3. Llamadas fetch propias.
    4. Validación de reglas de negocio replicada del backend.

  RELACIONADO:
    - POST /ordenes (servicio ordenes, vía gateway)
    - servicios/ordenes/Endpoints/OrdenesEndpoints.cs
    - contratos/orden.creada.v1.json (los campos de este formulario alimentan el
      evento)
*/}
