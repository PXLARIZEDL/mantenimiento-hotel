{/*
  PROPÓSITO: mostrar si cada servicio del sistema está vivo. Es la pantalla que
    permite explicar y depurar la arquitectura: cuando algo no funciona, aquí se
    ve cuál de las cinco piezas se cayó.

  DEBE CONTENER:
    1. La consulta al endpoint de salud agregado del gateway desde src/api.js.
    2. Una tarjeta por servicio: habitaciones, ordenes, tecnicos, notificaciones
       y el propio gateway.
    3. Por cada uno: estado (sano / degradado / caído), tiempo de respuesta y,
       cuando el servicio lo reporte, el estado de sus dependencias — base de
       datos y conexión a RabbitMQ.
    4. Un indicador del ESTADO DEL CIRCUIT BREAKER de ordenes hacia habitaciones
       (cerrado / abierto / semiabierto), si el servicio lo expone. Es la prueba
       visible de que el patrón está implementado y no solo declarado.
    5. Refresco automático cada pocos segundos, con opción de detenerlo.
    6. Marca de la última actualización, para no mirar datos viejos creyendo que
       son de ahora.
    7. Manejo del caso en que el propio gateway no responda: ahí no se puede
       saber nada de nadie, y hay que decirlo así.

  NO DEBE CONTENER:
    1. Llamadas directas al /salud de cada servicio saltándose el gateway; la
       agregación es responsabilidad del gateway.
    2. Botones para reiniciar servicios, ejecutar migraciones ni purgar colas:
       es un panel de LECTURA.
    3. Métricas de negocio (cuántas órdenes, cuántas habitaciones); eso va en las
       otras pantallas.
    4. Credenciales ni cadenas de conexión en pantalla.

  RELACIONADO:
    - GET /salud del gateway (que a su vez consulta el /salud de los cuatro)
    - servicios/gateway/Program.cs (endpoint agregado)
    - docs/arquitectura.md, sección de observabilidad
*/}
