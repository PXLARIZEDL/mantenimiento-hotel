{/*
  PROPÓSITO: mostrar el estado de las 400 habitaciones del hotel. Es la pantalla
    que responde de un vistazo la pregunta de recepción: "¿qué cuartos puedo
    vender ahora mismo?".

  DEBE CONTENER:
    1. La carga del listado al montar el componente, llamando a la función
       correspondiente de src/api.js.
    2. Una vista compacta que soporte 400 elementos sin volverse ilegible:
       agrupación por piso, o cuadrícula con color por estado.
    3. Un código visual claro por estado: DISPONIBLE, OCUPADA y
       FUERA_DE_SERVICIO deben distinguirse sin leer el texto — pero el texto
       debe estar igual, no solo el color.
    4. Filtros por estado y por piso.
    5. Un contador de resumen: cuántas hay en cada estado.
    6. En una habitación FUERA_DE_SERVICIO, un enlace a la orden que la bloqueó.
    7. Estados de carga, de lista vacía y de error de red bien visibles.
    8. Un botón para recargar, porque el estado cambia por eventos que la UI no
       escucha.

  NO DEBE CONTENER:
    1. Botones para cambiar el estado de una habitación a mano: el estado lo
       mueve el servicio ordenes al abrir y resolver una orden. Un botón aquí
       dejaría el sistema inconsistente.
    2. Llamadas fetch propias; todo pasa por src/api.js.
    3. La regla de qué transición es válida; eso lo valida el backend.
    4. Datos de técnicos ni el formulario de nueva orden.

  RELACIONADO:
    - GET /habitaciones (servicio habitaciones, vía gateway)
    - servicios/habitaciones/Endpoints/HabitacionesEndpoints.cs
    - ListaOrdenes.jsx (a donde lleva el enlace de la orden que bloqueó el cuarto)
*/}
