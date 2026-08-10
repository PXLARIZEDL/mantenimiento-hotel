# Imagen de la UI. Compila React con Vite y sirve el resultado con nginx.
#
# En producción NO corre Node: la imagen final solo lleva HTML, CSS y JavaScript
# ya empaquetados, más nginx.
#
# La URL del gateway no se escribe aquí: la resuelve nginx.conf dentro de la red
# de Docker.

# --- Etapa de build ---------------------------------------------------------
FROM node:20-alpine AS build
WORKDIR /origen

# El manifiesto y el lock primero: mientras no cambien las dependencias, Docker
# reutiliza la capa del npm ci.
COPY package.json package-lock.json ./
RUN npm ci

COPY . ./
RUN npm run build

# --- Etapa final ------------------------------------------------------------
FROM nginx:alpine AS final

# Sustituye la configuración por defecto de nginx.
RUN rm -f /etc/nginx/conf.d/default.conf
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY proxy-comun.inc /etc/nginx/conf.d/proxy-comun.inc

COPY --from=build /origen/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
