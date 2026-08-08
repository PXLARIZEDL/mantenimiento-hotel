// PROPÓSITO: única capa de comunicación entre la UI y el backend. Ningún
//   componente debe llamar a fetch por su cuenta: todo pasa por aquí. Así, si
//   cambia una ruta o el manejo de errores, se cambia en un solo archivo.
//
// DEBE CONTENER:
//   1. La URL base, tomada de una variable de entorno de Vite con un valor por
//      defecto relativo (para que funcione detrás de nginx y del proxy de Vite).
//   2. Una función interna de petición que centralice: cabeceras JSON, manejo de
//      códigos de error, tiempo de espera y traducción del error a un mensaje en
//      español que el componente pueda mostrar tal cual.
//   3. Las funciones de habitaciones: listar (con filtro de estado) y obtener
//      una por número.
//   4. Las funciones de ordenes: crear una orden, listar con filtros, obtener el
//      detalle y resolver.
//   5. Las funciones de tecnicos: listar técnicos, disponibles y asignaciones.
//   6. Las funciones de notificaciones: listar la bandeja y marcar como leída.
//   7. La función de salud del sistema, que pega al endpoint agregado del
//      gateway y alimenta al PanelSalud.
//   8. El manejo explícito del 503 que devuelve ordenes cuando el circuito hacia
//      habitaciones está abierto: el usuario debe entender que el problema es
//      temporal y que su orden NO se creó.
//
// NO DEBE CONTENER:
//   1. URLs apuntando directamente a los servicios: TODO entra por el gateway.
//      Llamar a habitaciones sin pasar por el gateway rompe la arquitectura.
//   2. Estado de la aplicación ni caché; eso es de los componentes.
//   3. Marcado JSX.
//   4. Reglas de negocio ni validaciones que ya hace el backend; a lo sumo,
//      validación de formato del formulario.
//   5. Cliente de RabbitMQ: el navegador no habla con la cola. Si la UI necesita
//      ver eventos, los ve como notificaciones vía HTTP.
//
// RELACIONADO:
//   - servicios/gateway/appsettings.json (las rutas que este archivo llama)
//   - vite.config.js y nginx.conf (resuelven esas rutas relativas)
//   - Todos los componentes de src/componentes/
