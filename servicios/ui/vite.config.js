// PROPÓSITO: configurar Vite: cómo se sirve la UI en desarrollo y cómo se
//   construye para producción. Es también donde se resuelve el problema de CORS
//   durante el desarrollo, redirigiendo las llamadas al gateway.
//
// DEBE CONTENER:
//   1. La exportación de la configuración con defineConfig.
//   2. El plugin de React.
//   3. server.host en 0.0.0.0 y el puerto de desarrollo, para poder abrirlo
//      desde fuera del contenedor si se corre en modo dev.
//   4. server.proxy: redirigir /habitaciones, /ordenes, /tecnicos,
//      /asignaciones, /notificaciones y /salud hacia el GATEWAY. Así en
//      desarrollo no hay CORS y src/api.js usa las mismas rutas relativas que
//      en producción.
//   5. build.outDir apuntando a la carpeta que nginx va a servir (dist).
//   6. La lectura de la URL del gateway desde una variable de entorno, con un
//      valor por defecto para desarrollo local.
//
// NO DEBE CONTENER:
//   1. Proxies apuntando DIRECTAMENTE a habitaciones, ordenes, tecnicos o
//      notificaciones: saltarse el gateway rompe la regla de "único punto de
//      entrada" y hace que la UI de desarrollo no se parezca a la de producción.
//   2. Lógica de la aplicación ni llamadas al backend; eso vive en src/api.js.
//   3. Credenciales.
//   4. Configuración de nginx; esa vive en nginx.conf y solo aplica al empaquetado.
//
// RELACIONADO:
//   - src/api.js (usa las rutas relativas que este proxy resuelve)
//   - nginx.conf (hace lo mismo, pero en producción)
//   - servicios/gateway/appsettings.json (destino real de todas estas rutas)
