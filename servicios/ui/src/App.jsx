// Componente raíz: organiza las cinco pantallas y decide cuál se ve.
// Cada pantalla corresponde a un servicio del backend.
//
// La navegación es por PESTAÑAS y no por rutas a propósito: nginx reenvía al
// gateway los prefijos /ordenes, /habitaciones, /notificaciones... Si el
// navegador usara esas mismas rutas, chocarían con la API.

import { Component, useState } from 'react'

import Ingreso from './componentes/Ingreso.jsx'
import Habitaciones from './componentes/Habitaciones.jsx'
import NuevaOrden from './componentes/NuevaOrden.jsx'
import ListaOrdenes from './componentes/ListaOrdenes.jsx'
import BandejaNotificaciones from './componentes/BandejaNotificaciones.jsx'
import PanelSalud from './componentes/PanelSalud.jsx'

// Los iconos son emoji a propósito: se ven igual en cualquier máquina y no
// obligan a bajar una fuente de iconos, que el proyecto no podría cargar sin
// internet.
const SECCIONES = [
  { id: 'nueva', titulo: 'Reportar falla', icono: '📝', Componente: NuevaOrden },
  { id: 'ordenes', titulo: 'Órdenes', icono: '🧾', Componente: ListaOrdenes },
  { id: 'habitaciones', titulo: 'Habitaciones', icono: '🏨', Componente: Habitaciones },
  { id: 'bandeja', titulo: 'Avisos', icono: '🔔', Componente: BandejaNotificaciones },
  { id: 'salud', titulo: 'Salud del sistema', icono: '💓', Componente: PanelSalud },
]

// Se recuerda en el navegador para no volver a preguntar en cada recarga. Es
// una preferencia de esta terminal, no una sesión: no hay token ni nada que
// caduque.
const CLAVE_USUARIO = 'mantenimiento.usuario'

/**
 * Evita que un fallo de render deje la pantalla en blanco. Sin esto, un error
 * en cualquier pantalla tumba toda la aplicación y no se ve ni el menú.
 */
class Barrera extends Component {
  state = { error: null }

  static getDerivedStateFromError(error) {
    return { error }
  }

  render() {
    if (this.state.error) {
      return (
        <section className="tarjeta">
          <div className="mensaje error">
            <strong>Se rompió esta pantalla.</strong>
            <div style={{ marginTop: 6, fontSize: 13 }}>{String(this.state.error)}</div>
          </div>
          <button className="suave" onClick={() => this.setState({ error: null })}>
            Reintentar
          </button>
        </section>
      )
    }
    return this.props.children
  }
}

export default function App() {
  const [usuario, setUsuario] = useState(() => localStorage.getItem(CLAVE_USUARIO) ?? '')
  const [seccion, setSeccion] = useState('nueva')

  function entrar(nombre) {
    localStorage.setItem(CLAVE_USUARIO, nombre)
    setUsuario(nombre)
  }

  function salir() {
    localStorage.removeItem(CLAVE_USUARIO)
    setUsuario('')
    setSeccion('nueva')
  }

  if (!usuario) return <Ingreso onEntrar={entrar} />

  const activa = SECCIONES.find((s) => s.id === seccion) ?? SECCIONES[0]
  const Pantalla = activa.Componente

  return (
    <>
      <header className="principal">
        <div className="contenedor">
          <div className="barra-superior">
            <div>
              <h1><span aria-hidden="true">🛎️</span> Mantenimiento — Hotel</h1>
              <p>Gestión de órdenes de mantenimiento · 400 habitaciones</p>
            </div>

            <div className="usuario">
              <span className="saludo">
                Bienvenido, <strong>{usuario}</strong>
              </span>
              <button className="suave" onClick={salir}>Salir</button>
            </div>
          </div>

          <nav className="pestanas">
            {SECCIONES.map((s) => (
              <button
                key={s.id}
                onClick={() => setSeccion(s.id)}
                aria-current={s.id === seccion ? 'page' : undefined}
              >
                <span aria-hidden="true">{s.icono}</span>
                {s.titulo}
              </button>
            ))}
          </nav>
        </div>
      </header>

      <main className="contenedor">
        {/* La barrera se reinicia al cambiar de pestaña: un error en una
            pantalla no debe dejar inutilizadas las otras.

            usuario va a todas: la única que lo usa es NuevaOrden, para
            rellenar quién reporta. Las demás lo ignoran. */}
        <Barrera key={seccion}>
          <Pantalla usuario={usuario} />
        </Barrera>
      </main>
    </>
  )
}
