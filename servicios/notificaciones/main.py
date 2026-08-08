# PROPÓSITO: punto de entrada del servicio notificaciones. Crea la aplicación
#   FastAPI, arranca el consumidor de eventos y expone la bandeja de avisos que
#   la UI muestra a recepción. Es el único servicio SIN base de datos: todo vive
#   en memoria.
#
# DEBE CONTENER:
#   1. La instancia de FastAPI con título y versión.
#   2. El almacén EN MEMORIA de avisos: una estructura acotada (por ejemplo, los
#      últimos N avisos) para que el proceso no crezca sin límite. Debe ser
#      segura ante accesos concurrentes, porque escribe el consumidor y lee HTTP.
#   3. El ciclo de vida (lifespan): al arrancar, lanzar el consumidor de
#      RabbitMQ; al apagar, cerrarlo de forma ordenada.
#   4. GET /notificaciones — bandeja completa, más reciente primero, con filtro
#      opcional por tipo de evento y por habitación.
#   5. GET /notificaciones/{id} — detalle de un aviso.
#   6. POST /notificaciones/{id}/leida — marcar como leída desde la UI.
#   7. GET /salud — estado del servicio y de la conexión a RabbitMQ, y cuántos
#      avisos hay en memoria; lo consume el PanelSalud.
#   8. Una advertencia visible en la documentación de la API: los avisos se
#      PIERDEN al reiniciar el contenedor. Es una decisión, no un descuido.
#
# NO DEBE CONTENER:
#   1. Base de datos, ORM ni persistencia en disco; si algún día hace falta, es
#      un cambio de ADR, no un parche aquí.
#   2. El bucle de consumo de mensajes; vive en consumidor.py.
#   3. La redacción de los textos de los avisos; vive en plantillas.py.
#   4. Un endpoint para CREAR un aviso a mano: un aviso solo nace de un evento.
#   5. Llamadas HTTP a ordenes, tecnicos o habitaciones: todo lo que necesita
#      llega dentro del payload del evento.
#   6. Envío real de correo o SMS; en la versión 1 se "envía" guardando en
#      memoria.
#
# RELACIONADO:
#   - consumidor.py (llena el almacén de aquí)
#   - plantillas.py (arma el texto de cada aviso)
#   - servicios/ui/src/componentes/BandejaNotificaciones.jsx (consume estos
#     endpoints a través del gateway)
