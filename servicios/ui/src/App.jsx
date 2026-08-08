{/*
  PROPÓSITO: componente raíz de la interfaz. Organiza las cinco pantallas del
    sistema y decide cuál se ve. Es el mapa visual del proyecto: cada pantalla
    corresponde a un servicio del backend.

  DEBE CONTENER:
    1. La estructura general: encabezado con el nombre del sistema y una
       navegación entre las cinco secciones.
    2. El estado de qué sección está activa (por pestañas o por rutas si se usa
       react-router-dom).
    3. El renderizado de los cinco componentes de src/componentes/:
         - Habitaciones          → estado de los 400 cuartos
         - NuevaOrden            → reportar una falla
         - ListaOrdenes          → seguimiento del ciclo de vida
         - BandejaNotificaciones → avisos a recepción
         - PanelSalud            → estado de los servicios
    4. El manejo de un error global de la aplicación, para que una llamada
       fallida no deje la pantalla en blanco.

  NO DEBE CONTENER:
    1. Llamadas fetch directas; todas pasan por src/api.js.
    2. La lógica interna de cada pantalla; cada componente se ocupa de lo suyo.
    3. Reglas de negocio: qué técnico corresponde a qué falla, cuándo se libera
       una habitación o qué transición de estado es válida. Todo eso lo decide
       el backend; la UI solo muestra y pide.
    4. Validación fiscal ni de dominio replicada del backend.

  RELACIONADO:
    - main.jsx (lo monta)
    - src/componentes/*.jsx (los cinco hijos)
    - src/api.js (única vía de comunicación con el backend)
*/}
