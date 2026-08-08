# PROPÓSITO: punto de entrada del servicio tecnicos. Crea la aplicación FastAPI,
#   expone la API de consulta de técnicos y arranca el consumidor de eventos en
#   segundo plano. Es el único archivo que ve el cableado completo del servicio.
#
# DEBE CONTENER:
#   1. La instancia de FastAPI con título y versión.
#   2. El ciclo de vida (lifespan): al arrancar, crear las tablas o aplicar la
#      siembra inicial y lanzar el consumidor de RabbitMQ; al apagar, cerrar la
#      conexión al broker de forma ordenada.
#   3. GET /tecnicos — listado, con filtro opcional por especialidad y por turno.
#   4. GET /tecnicos/{id} — detalle.
#   5. GET /tecnicos/disponibles — quiénes están en turno ahora mismo; sirve para
#      depurar por qué una orden no se asignó.
#   6. GET /asignaciones — qué órdenes se le asignaron a quién, para la UI y la
#      defensa del proyecto.
#   7. GET /salud — estado del servicio, de la base y de la conexión a RabbitMQ,
#      para el PanelSalud de la UI.
#   8. Lectura de la configuración desde variables de entorno, nunca literales.
#
# NO DEBE CONTENER:
#   1. La REGLA de asignación (qué técnico corresponde a qué falla); vive en
#      asignador.py.
#   2. El bucle de consumo de mensajes; vive en consumidor.py.
#   3. Consultas SQL sueltas; el acceso a datos vive en base_datos.py.
#   4. Un endpoint para crear órdenes ni para cambiar el estado de habitaciones;
#      esos dominios son de otros servicios.
#   5. Un endpoint que asigne técnico "a mano" por HTTP: la asignación se dispara
#      por el evento orden.creada, no por petición del usuario.
#
# RELACIONADO:
#   - consumidor.py (se lanza desde el lifespan de aquí)
#   - asignador.py, base_datos.py, modelos.py
#   - Enrutado por servicios/gateway/appsettings.json
