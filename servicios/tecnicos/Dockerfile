# Imagen del servicio tecnicos (Python 3.12 / FastAPI).
#
# Ni la URL de la base ni las credenciales de RabbitMQ viven aquí: llegan por
# variables de entorno desde docker-compose.yml.
#
# Tampoco hay script de espera hacia RabbitMQ o PostgreSQL: eso se resuelve con
# depends_on + healthcheck en compose y con la reconexión de consumidor.py.

FROM python:3.12-slim

# Sin .pyc y con salida sin búfer: los logs aparecen en tiempo real en
# `docker logs` en vez de quedarse atascados en el búfer.
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

# requirements primero: mientras no cambien las dependencias, Docker reutiliza
# la capa del pip install.
COPY requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r requirements.txt

COPY *.py ./

# Usuario sin privilegios.
RUN useradd --create-home --uid 1000 tecnicos
USER tecnicos

EXPOSE 8080

# Sin --reload: esa bandera es solo para desarrollo local.
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8080"]
