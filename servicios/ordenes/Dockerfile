# PROPÓSITO: construir la imagen del servicio ordenes. Define cómo se compila el
#   proyecto .NET 8 y cómo se ejecuta dentro del contenedor.
#
# DEBE CONTENER:
#   1. Etapa de build sobre la imagen del SDK de .NET 8.
#   2. Copia del Ordenes.csproj y "dotnet restore" antes del resto del código,
#      para aprovechar la caché de capas.
#   3. Copia del resto de archivos y "dotnet publish" en Release.
#   4. Etapa final sobre la imagen de runtime aspnet:8.0.
#   5. EXPOSE del puerto interno de escucha.
#   6. ENTRYPOINT ejecutando el dll del servicio.
#
# NO DEBE CONTENER:
#   - La URL de habitaciones ni las credenciales de RabbitMQ; llegan por
#     variables de entorno desde docker-compose.yml.
#   - Un "wait-for" hacia RabbitMQ o hacia la base; la espera se resuelve con
#     depends_on + healthcheck en compose, y con reconexión en el propio código.
#   - Herramientas de desarrollo en la imagen final.
#
# RELACIONADO:
#   - Ordenes.csproj
#   - .dockerignore
#   - docker-compose.yml → servicio "ordenes" (depends_on: db-ordenes, rabbitmq)
