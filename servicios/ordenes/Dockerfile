# Imagen del servicio ordenes (.NET 8).
#
# Ni la URL de habitaciones ni las credenciales de RabbitMQ viven aquí: llegan
# por variables de entorno desde docker-compose.yml.
#
# Tampoco hay "wait-for" hacia RabbitMQ o la base: la espera se resuelve con
# depends_on + healthcheck en compose, y con reconexión en el propio código
# (Eventos/PublicadorEventos.cs y Eventos/ConsumidorOrdenAsignada.cs).

# --- Etapa de build ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /origen

# El .csproj primero y solo: mientras no cambien las dependencias, Docker
# reutiliza la capa del restore y no vuelve a bajar los paquetes.
COPY Ordenes.csproj ./
RUN dotnet restore Ordenes.csproj

# Ahora sí el resto del código.
COPY . ./
RUN dotnet publish Ordenes.csproj \
    --configuration Release \
    --no-restore \
    --output /publicado

# --- Etapa final ------------------------------------------------------------
# Solo el runtime: la imagen final no lleva el SDK ni herramientas de desarrollo.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Usuario sin privilegios; la imagen base ya lo trae creado.
USER $APP_UID

COPY --from=build /publicado ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Ordenes.dll"]
