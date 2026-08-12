// Avisos generados por el sistema. Es la ventana visible del flujo asincrónico:
// cada aviso de esta bandeja nació de un evento que viajó por RabbitMQ.
//
// No hay formulario para crear un aviso a mano: un aviso solo nace de un evento.

import { useCallback, useEffect, useState } from 'react'

import { listarNotificaciones, marcarNotificacionLeida } from '../api.js'

const COLOR_EVENTO = {
  'orden.creada': 'rojo',
  'orden.asignada': 'azul',
  'orden.resuelta': 'verde',
}

const hora = (iso) => (iso ? new Date(iso).toLocaleTimeString('es', { hour12: false }) : '—')

export default function BandejaNotificaciones() {
  const [avisos, setAvisos] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState(null)
  const [tipoEvento, setTipoEvento] = useState('')
  const [numeroHabitacion, setNumeroHabitacion] = useState('')

  const cargar = useCallback(async () => {
    try {
      setAvisos(await listarNotificaciones({ tipoEvento, numeroHabitacion }))
      setError(null)
    } catch (fallo) {
      setError(fallo)
    } finally {
      setCargando(false)
    }
  }, [tipoEvento, numeroHabitacion])

  // Los avisos llegan por eventos y el navegador no escucha la cola: sin
  // refresco, la bandeja se ve congelada.
  useEffect(() => {
    cargar()
    const cada = setInterval(cargar, 4000)
    return () => clearInterval(cada)
  }, [cargar])

  async function marcar(id) {
    try {
      await marcarNotificacionLeida(id)
      await cargar()
    } catch (fallo) {
      setError(fallo)
    }
  }

  return (
    <section className="tarjeta">
      <h2>Avisos a recepción</h2>
      <p className="ayuda">Cada aviso nació de un evento publicado en RabbitMQ.</p>

      {/* El usuario no debe descubrir por accidente que esto es volátil. */}
      <div className="mensaje aviso">
        Estos avisos viven <strong>solo en memoria</strong> y se pierden si el servicio se
        reinicia. Los eventos que queden sin procesar esperan en la cola, así que al volver
        se recuperan solos.
      </div>

      <div className="fila" style={{ marginBottom: 14 }}>
        <div className="campo">
          <label htmlFor="n-tipo">Tipo de evento</label>
          <select id="n-tipo" value={tipoEvento} onChange={(e) => setTipoEvento(e.target.value)}>
            <option value="">Todos</option>
            <option value="orden.creada">orden.creada</option>
            <option value="orden.asignada">orden.asignada</option>
            <option value="orden.resuelta">orden.resuelta</option>
          </select>
        </div>
        <div className="campo">
          <label htmlFor="n-hab">Habitación</label>
          <input
            id="n-hab"
            type="number"
            style={{ width: 110 }}
            value={numeroHabitacion}
            onChange={(e) => setNumeroHabitacion(e.target.value)}
            placeholder="todas"
          />
        </div>
        <button className="suave" onClick={cargar}>Actualizar</button>
      </div>

      {error && <div className="mensaje error">{error.message}</div>}
      {cargando && <div className="vacio">Cargando la bandeja…</div>}

      {!cargando && avisos.length === 0 && (
        <div className="vacio">
          No hay avisos. Reportá una falla y en unos segundos aparecerán aquí.
        </div>
      )}

      {avisos.map((a) => (
        <div key={a.id} className={`aviso-item ${a.leido ? 'leido' : 'no-leido'}`}>
          <h4>{a.titulo}</h4>
          <p>{a.cuerpo}</p>
          <div className="pie">
            <span className={`etiqueta ${COLOR_EVENTO[a.tipoEvento] ?? 'gris'}`}>
              {a.tipoEvento}
            </span>
            <span>Habitación {a.numeroHabitacion}</span>
            <span>{hora(a.marcaDeTiempo)}</span>
            {a.leido
              ? <span>leído</span>
              : <button className="suave" onClick={() => marcar(a.id)}>Marcar como leído</button>}
          </div>
        </div>
      ))}
    </section>
  )
}
