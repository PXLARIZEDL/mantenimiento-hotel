# PROPÓSITO: construir la imagen del gateway. Define cómo se compila el proyecto
#   .NET 8 y cómo se ejecuta dentro del contenedor.
#
# DEBE CONTENER:
#   1. Etapa de build sobre la imagen del SDK de .NET 8.
#   2. Copia del Gateway.csproj y "dotnet restore" antes del resto del código.
#   3. Copia del resto de archivos y "dotnet publish" en Release.
#   4. Etapa final sobre la imagen de runtime aspnet:8.0.
#   5. EXPOSE del puerto público — este es el ÚNICO servicio cuyo puerto se
#      publica hacia el host en docker-compose.yml.
#   6. ENTRYPOINT ejecutando el dll del gateway.
#
# NO DEBE CONTENER:
#   - Las direcciones de los servicios destino; van en appsettings.json y se
#     sobrescriben por variables de entorno.
#   - Un script de espera a que los servicios estén arriba: el gateway debe
#     arrancar igual y responder que un destino no está sano.
#   - Herramientas de desarrollo en la imagen final.
#
# RELACIONADO:
#   - Gateway.csproj
#   - docker-compose.yml → servicio "gateway" (el único con puerto publicado)
