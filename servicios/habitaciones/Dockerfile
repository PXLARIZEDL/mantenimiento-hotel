# Imagen del servicio habitaciones (.NET 8).
#
# Ni cadenas de conexión ni contraseñas: llegan por variables de entorno desde
# docker-compose.yml. Las migraciones y la siembra las decide Program.cs al
# arrancar, no la construcción de la imagen.

# --- Etapa de build ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /origen

# El .csproj primero y solo: mientras no cambien las dependencias, Docker
# reutiliza la capa del restore.
COPY Habitaciones.csproj ./
RUN dotnet restore Habitaciones.csproj

COPY . ./
RUN dotnet publish Habitaciones.csproj \
    --configuration Release \
    --no-restore \
    --output /publicado

# --- Etapa final ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

USER $APP_UID

COPY --from=build /publicado ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Habitaciones.dll"]
