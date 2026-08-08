# PROPÓSITO: construir la imagen del servicio notificaciones. Define cómo se
#   instala el entorno de Python 3.12 y cómo se ejecuta FastAPI en el contenedor.
#
# DEBE CONTENER:
#   1. Imagen base python:3.12-slim.
#   2. Variables de entorno de Python: no escribir .pyc y salida sin búfer, para
#      ver los logs en tiempo real.
#   3. Directorio de trabajo de la aplicación.
#   4. Copia de requirements.txt e instalación con pip antes del código, para
#      aprovechar la caché de capas.
#   5. Copia del resto de los archivos .py.
#   6. EXPOSE del puerto interno de escucha.
#   7. CMD que arranque uvicorn apuntando a main:app con host 0.0.0.0.
#
# NO DEBE CONTENER:
#   - Credenciales de RabbitMQ; llegan por variables de entorno desde compose.
#   - Ningún volumen ni ruta de persistencia: los avisos viven en memoria y se
#     pierden al reiniciar, por decisión de arquitectura.
#   - Un cliente de PostgreSQL instalado en el sistema.
#   - La bandera --reload de uvicorn.
#
# RELACIONADO:
#   - requirements.txt
#   - main.py (módulo que ejecuta uvicorn)
#   - docker-compose.yml → servicio "notificaciones" (depends_on: rabbitmq, y
#     NINGUNA base de datos)
