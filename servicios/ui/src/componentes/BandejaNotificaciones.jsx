{/*
  PROPÓSITO: mostrar a recepción los avisos generados por el sistema. Es la
    ventana visible del flujo asincrónico: cada aviso de esta bandeja nació de un
    evento que viajó por RabbitMQ.

  DEBE CONTENER:
    1. La carga de la bandeja desde src/api.js al montar el componente.
    2. Un listado ordenado del más reciente al más antiguo, con el título, el
       cuerpo del aviso, la habitación y la hora.
    3. Un distintivo por tipo de evento: orden.creada, orden.asignada y
       orden.resuelta.
    4. Distinción visual entre leídos y no leídos, con la acción de marcar como
       leído.
    5. Filtros por tipo de evento y por número de habitación.
    6. Refresco periódico: los avisos llegan por eventos y la UI no escucha la
       cola, así que sin refresco la bandeja se ve congelada.
    7. Un aviso claro en la pantalla de que estas notificaciones viven EN MEMORIA
       y se pierden si el servicio se reinicia. El usuario no debe descubrirlo
       por accidente.
    8. Estados de carga, bandeja vacía y error.

  NO DEBE CONTENER:
    1. Un formulario para crear un aviso a mano: un aviso solo nace de un evento.
    2. Acciones sobre la orden (asignar, resolver) desde aquí; para eso está
       ListaOrdenes.jsx.
    3. Conexión a RabbitMQ desde el navegador: la UI ve los eventos ya
       traducidos, por HTTP.
    4. La redacción de los textos; ya vienen armados por plantillas.py.

  RELACIONADO:
    - GET /notificaciones y POST /notificaciones/{id}/leida (vía gateway)
    - servicios/notificaciones/main.py y plantillas.py
    - docs/catalogo-eventos.md (los tres eventos que llenan esta bandeja)
*/}
