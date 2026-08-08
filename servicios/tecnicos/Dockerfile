# PROPÓSITO: construir la imagen del servicio tecnicos. Define cómo se instala el
#   entorno de Python 3.12 y cómo se ejecuta FastAPI dentro del contenedor.
#
# DEBE CONTENER:
#   1. Imagen base python:3.12-slim.
#   2. Variables de entorno de Python: no escribir .pyc y salida sin búfer, para
#      que los logs aparezcan en tiempo real en docker logs.
#   3. Directorio de trabajo de la aplicación.
#   4. Copia de requirements.txt e instalación con pip ANTES de copiar el código,
#      para aprovechar la caché de capas.
#   5. Copia del resto de los archivos .py.
#   6. EXPOSE del puerto interno de escucha.
#   7. CMD que arranque uvicorn apuntando a main:app con host 0.0.0.0.
#
# NO DEBE CONTENER:
#   - La URL de la base ni las credenciales de RabbitMQ; llegan por variables de
#     entorno desde docker-compose.yml.
#   - Un script de espera hacia RabbitMQ o PostgreSQL; eso se resuelve con
#     depends_on + healthcheck en compose y con reconexión en consumidor.py.
#   - La bandera --reload de uvicorn, que es solo para desarrollo local.
#
# RELACIONADO:
#   - requirements.txt
#   - main.py (el módulo que ejecuta uvicorn)
#   - docker-compose.yml → servicio "tecnicos" (depends_on: db-tecnicos, rabbitmq)
