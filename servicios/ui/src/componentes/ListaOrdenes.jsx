// Muestra las órdenes y su avance por los tres estados. Es la pantalla donde se
// DEMUESTRA que el sistema distribuido funciona: una orden aparece ABIERTA y,
// segundos después, pasa a ASIGNADA sin que nadie la toque.
//
// No hay botón para asignar técnico a mano: la asignación llega por el evento
// orden.asignada.

import { useCallback, useEffect, useState } from 'react'

import { listarOrdenes, resolverOrden } from '../api.js'

const COLOR_ESTADO = { ABIERTA: 'amarillo', ASIGNADA: 'azul', RESUELTA: 'verde' }
const COLOR_PRIORIDAD = { ALTA: 'rojo', MEDIA: 'amarillo', BAJA: 'gris' }

// Pasado este tiempo, una orden que sigue ABIERTA probablemente no encontró
// técnico de esa especialidad en turno.
const SEGUNDOS_PARA_SOSPECHAR = 20

const hora = (iso) => (iso ? new Date(iso).toLocaleTimeString('es', { hour12: false }) : '—')

export default function ListaOrdenes() {
  const [ordenes, setOrdenes] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState(null)
  const [estado, setEstado] = useState('')
  const [habitacion, setHabitacion] = useState('')
  const [resolviendo, setResolviendo] = useState(null)
  const [nota, setNota] = useState('')

  const cargar = useCallback(async () => {
    try {
      setOrdenes(await listarOrdenes({ estado, habitacion }))
      setError(null)
    } catch (fallo) {
      setError(fallo)
    } finally {
      setCargando(false)
    }
  }, [estado, habitacion])

  // Refresco periódico: la transición a ASIGNADA llega por un evento de
  // RabbitMQ y el navegador no escucha la cola. Sin esto, la tabla se ve
  // congelada justo en el momento más interesante.
  useEffect(() => {
    cargar()
    const cada = setInterval(cargar, 4000)
    return () => clearInterval(cada)
  }, [cargar])

  async function confirmarResolver(id) {
    try {
      await resolverOrden(id, nota.trim() || 'Sin nota de cierre.')
      setResolviendo(null)
      setNota('')
      await cargar()
    } catch (fallo) {
      setError(fallo)
    }
  }

  return (
    <section className="tarjeta">
      <h2>Órdenes de mantenimiento</h2>
      <p className="ayuda">
        Se actualiza sola cada 4 segundos. Una orden pasa de ABIERTA a ASIGNADA por un
        evento, no por una acción del usuario.
      </p>

      <div className="fila" style={{ marginBottom: 14 }}>
        <div className="campo">
          <label htmlFor="f-estado">Estado</label>
          <select id="f-estado" value={estado} onChange={(e) => setEstado(e.target.value)}>
            <option value="">Todos</option>
            <option value="ABIERTA">ABIERTA</option>
            <option value="ASIGNADA">ASIGNADA</option>
            <option value="RESUELTA">RESUELTA</option>
          </select>
        </div>
        <div className="campo">
          <label htmlFor="f-hab">Habitación</label>
          <input
            id="f-hab"
            type="number"
            style={{ width: 110 }}
            value={habitacion}
            onChange={(e) => setHabitacion(e.target.value)}
            placeholder="todas"
          />
        </div>
        <button className="suave" onClick={cargar}>Actualizar</button>
      </div>

      {error && <div className="mensaje error">{error.message}</div>}
      {cargando && <div className="vacio">Cargando…</div>}

      {!cargando && ordenes.length === 0 && (
        <div className="vacio">No hay órdenes que mostrar. Reportá una falla para empezar.</div>
      )}

      {ordenes.length > 0 && (
        <div className="tabla-scroll">
          <table>
            <thead>
              <tr>
                <th>Hab.</th><th>Falla</th><th>Prioridad</th><th>Estado</th>
                <th>Técnico</th><th>Creada</th><th>Asignada</th><th></th>
              </tr>
            </thead>
            <tbody>
              {ordenes.map((o) => {
                const segundosAbierta = (Date.now() - new Date(o.creadaEn)) / 1000
                const sinTecnico =
                  o.estado === 'ABIERTA' && segundosAbierta > SEGUNDOS_PARA_SOSPECHAR

                return (
                  <tr key={o.id}>
                    <td><strong>{o.habitacionNumero}</strong></td>
                    <td>{o.tipoFalla.replace(/_/g, ' ').toLowerCase()}</td>
                    <td>
                      <span className={`etiqueta ${COLOR_PRIORIDAD[o.prioridad] ?? 'gris'}`}>
                        {o.prioridad}
                      </span>
                    </td>
                    <td>
                      <span className={`etiqueta ${COLOR_ESTADO[o.estado] ?? 'gris'}`}>
                        {o.estado}
                      </span>
                      {sinTecnico && (
                        <div style={{ fontSize: 11, color: 'var(--texto-suave)', marginTop: 4 }}>
                          esperando asignación · quizá no hay técnico de esa especialidad en turno
                        </div>
                      )}
                    </td>
                    {/* El nombre viene COPIADO dentro de la orden: no se
                        consulta a tecnicos para pintarlo. */}
                    <td>{o.tecnicoNombre ?? '—'}</td>
                    <td>{hora(o.creadaEn)}</td>
                    <td>{hora(o.asignadaEn)}</td>
                    <td>
                      {o.estado !== 'RESUELTA' && (
                        <button className="suave" onClick={() => { setResolviendo(o.id); setNota('') }}>
                          Resolver
                        </button>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {resolviendo && (
        <div className="mensaje info">
          <strong>Cerrar la orden</strong>
          <div className="campo" style={{ marginTop: 10 }}>
            <label htmlFor="nota">Nota de cierre del técnico</label>
            <textarea
              id="nota"
              value={nota}
              onChange={(e) => setNota(e.target.value)}
              placeholder="Se limpió el filtro y se destapó el drenaje."
            />
          </div>
          <div className="fila" style={{ marginTop: 10 }}>
            <button className="accion" onClick={() => confirmarResolver(resolviendo)}>
              Confirmar cierre
            </button>
            <button className="suave" onClick={() => setResolviendo(null)}>Cancelar</button>
          </div>
        </div>
      )}
    </section>
  )
}
