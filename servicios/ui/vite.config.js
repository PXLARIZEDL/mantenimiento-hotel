import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Configuración de Vite: cómo se sirve la UI en desarrollo y cómo se construye
// para producción.
//
// El proxy de aquí resuelve el CORS en desarrollo y hace que src/api.js use las
// MISMAS rutas relativas que en producción. nginx.conf hace lo equivalente
// dentro del contenedor.

// Todo va al GATEWAY, nunca a un servicio directo: saltárselo rompería la regla
// de "único punto de entrada" y haría que la UI de desarrollo no se parezca a la
// de producción.
const gateway = process.env.GATEWAY_URL ?? 'http://localhost:8080'

// Los prefijos que atiende el backend. Es la única lista que hay que tocar si el
// gateway expone una ruta nueva.
const rutasApi = [
  '/habitaciones',
  '/ordenes',
  '/tecnicos',
  '/asignaciones',
  '/notificaciones',
  '/salud',
]

export default defineConfig({
  plugins: [react()],

  server: {
    host: '0.0.0.0',
    port: 5173,
    proxy: Object.fromEntries(
      rutasApi.map((ruta) => [ruta, { target: gateway, changeOrigin: true }]),
    ),
  },

  build: {
    // Lo que nginx sirve en la etapa final del Dockerfile.
    outDir: 'dist',
  },
})
