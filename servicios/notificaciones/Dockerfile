# Imagen del servicio notificaciones (Python 3.12 / FastAPI).
#
# Sin credenciales: llegan por variables de entorno desde docker-compose.yml.
# Sin volúmenes ni rutas de persistencia: los avisos viven en memoria y se
# pierden al reiniciar, por decisión de arquitectura — este es el servicio que
# se apaga en la defensa para mostrar que el resto sigue funcionando sin él.

FROM python:3.12-slim

# Sin .pyc y con salida sin búfer: los logs aparecen en tiempo real en
# `docker logs`.
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

# requirements primero, para aprovechar la caché de capas.
COPY requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r requirements.txt

COPY *.py ./

# Usuario sin privilegios.
RUN useradd --create-home --uid 1000 notificaciones
USER notificaciones

EXPOSE 8080

# Sin --reload: esa bandera es solo para desarrollo local.
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8080"]
