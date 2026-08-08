# PROPÓSITO: construir la imagen del servicio habitaciones. Define cómo se
#   compila el proyecto .NET 8 y cómo se ejecuta dentro del contenedor.
#
# DEBE CONTENER:
#   1. Etapa de build sobre la imagen del SDK de .NET 8.
#   2. Copia del .csproj y "dotnet restore" ANTES de copiar el resto del código,
#      para aprovechar la caché de capas de Docker.
#   3. Copia del resto de los archivos y "dotnet publish" en configuración
#      Release hacia una carpeta de salida.
#   4. Etapa final sobre la imagen de runtime aspnet:8.0 (más liviana que el SDK).
#   5. Copia de los artefactos publicados desde la etapa de build.
#   6. EXPOSE del puerto interno en el que escucha el servicio.
#   7. ENTRYPOINT ejecutando el dll del servicio.
#
# NO DEBE CONTENER:
#   - Cadenas de conexión ni contraseñas; llegan por variables de entorno desde
#     docker-compose.yml.
#   - Comandos de migración de base de datos; eso lo decide Program.cs al
#     arrancar, no la construcción de la imagen.
#   - Instalación de herramientas de desarrollo en la imagen final.
#
# RELACIONADO:
#   - Habitaciones.csproj (lo que se restaura y compila)
#   - .dockerignore (qué NO entra en el contexto de build)
#   - docker-compose.yml → servicio "habitaciones"
