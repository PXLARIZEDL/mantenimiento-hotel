# PROPÓSITO: construir la imagen de la UI. Compila la aplicación de React con
#   Vite y la sirve como archivos estáticos con nginx. En producción no corre
#   Node: solo hay HTML, CSS y JavaScript ya empaquetados.
#
# DEBE CONTENER:
#   1. Etapa de build sobre la imagen node:20-alpine (o la LTS vigente).
#   2. Copia de package.json y package-lock.json e instalación con "npm ci"
#      ANTES de copiar el código, para aprovechar la caché de capas.
#   3. Copia del resto del proyecto y "npm run build", que deja el resultado en
#      dist/.
#   4. Etapa final sobre nginx:alpine.
#   5. Copia de dist/ desde la etapa de build al directorio que nginx sirve.
#   6. Copia de nginx.conf al directorio de configuración de nginx, sustituyendo
#      la configuración por defecto.
#   7. EXPOSE del puerto 80.
#
# NO DEBE CONTENER:
#   - node_modules copiados desde la máquina local; se instalan dentro.
#   - La URL del gateway escrita a mano; se resuelve por nginx.conf o por una
#     variable de entorno de Vite en tiempo de construcción.
#   - "npm run dev" ni el servidor de desarrollo de Vite en la imagen final.
#   - Node en la etapa final: la imagen que se despliega solo lleva nginx.
#
# RELACIONADO:
#   - package.json (script build)
#   - nginx.conf (configuración que se copia en la etapa final)
#   - docker-compose.yml → servicio "ui"
