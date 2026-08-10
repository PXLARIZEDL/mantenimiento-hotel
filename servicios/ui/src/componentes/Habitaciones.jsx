// Estado de las 400 habitaciones. Responde de un vistazo la pregunta de
// recepción: "¿qué cuartos puedo vender ahora mismo?".
//
// No hay botones para cambiar el estado a mano: el estado lo mueve el servicio
// ordenes al abrir y resolver una orden. Un botón aquí dejaría el sistema
// inconsistente.

import { useCallback, useEffect, useMemo, useState } from 'react'

import { listarHabitaciones } from '../api.js'

const ESTADOS = ['DISPONIBLE', 'OCUPADA', 'FUERA_DE_SERVICIO']

const COLOR = { DISPONIBLE: 'verde', OCUPADA: 'azul', FUERA_DE_SERVICIO: 'rojo' }

export default function Habitaciones() {
  const [habitaciones, setHabitaciones] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState(null)
  const [estado, setEstado] = useState('')
  const [piso, setPiso] = useState('')

  const cargar = useCallback(async () => {
    try {
      setHabitaciones(await listarHabitaciones({ estado, piso }))
      setError(null)
    } catch (fallo) {
      setError(fallo)
    } finally {
      setCargando(false)
    }
  }, [estado, piso])

  useEffect(() => { cargar() }, [cargar])

  // El resumen cuenta sobre lo que se está viendo, no sobre las 400: si hay un
  // filtro puesto, mostrar el total del hotel confundiría.
  const resumen = useMemo(() => {
    const cuenta = { DISPONIBLE: 0, OCUPADA: 0, FUERA_DE_SERVICIO: 0 }
    for (const h of habitaciones) cuenta[h.estado] = (cuenta[h.estado] ?? 0) + 1
    return cuenta
  }, [habitaciones])

  const porPiso = useMemo(() => {
    const grupos = new Map()
    for (const h of habitaciones) {
      if (!grupos.has(h.piso)) grupos.set(h.piso, [])
      grupos.get(h.piso).push(h)
    }
    return [...grupos.entries()].sort((a, b) => a[0] - b[0])
  }, [habitaciones])

  const pisos = useMemo(
    () => [...new Set(habitaciones.map((h) => h.piso))].sort((a, b) => a - b),
    [habitaciones],
  )

  return (
    <section className="tarjeta">
      <h2>Habitaciones</h2>
      <p className="ayuda">
        El estado cambia por eventos que la UI no escucha: usá Actualizar para ver lo último.
      </p>

      <div className="resumen">
        {ESTADOS.map((e) => (
          // La franja del contador usa el MISMO color que el cuarto en la
          // rejilla, para que el resumen y el mapa se lean como una sola cosa.
          <div key={e} className={`dato ${COLOR[e]}`}>
            <div className="n">{resumen[e] ?? 0}</div>
            <div className="r">{e.replace(/_/g, ' ')}</div>
          </div>
        ))}
      </div>

      <div className="fila" style={{ marginBottom: 14 }}>
        <div className="campo">
          <label htmlFor="h-estado">Estado</label>
          <select id="h-estado" value={estado} onChange={(e) => setEstado(e.target.value)}>
            <option value="">Todos</option>
            {ESTADOS.map((e) => <option key={e} value={e}>{e}</option>)}
          </select>
        </div>
        <div className="campo">
          <label htmlFor="h-piso">Piso</label>
          <select id="h-piso" value={piso} onChange={(e) => setPiso(e.target.value)}>
            <option value="">Todos</option>
            {(piso ? [Number(piso)] : pisos).map((p) => <option key={p} value={p}>{p}</option>)}
          </select>
        </div>
        <button className="suave" onClick={cargar}>Actualizar</button>
      </div>

      {error && <div className="mensaje error">{error.message}</div>}
      {cargando && <div className="vacio">Cargando las habitaciones…</div>}

      {!cargando && habitaciones.length === 0 && (
        <div className="vacio">Ninguna habitación coincide con el filtro.</div>
      )}

      {porPiso.map(([numeroPiso, cuartos]) => (
        <div className="piso" key={numeroPiso}>
          <h3>Piso {numeroPiso}</h3>
          <div className="rejilla">
            {cuartos.map((h) => (
              // El color distingue de un vistazo, pero el número y el title
              // llevan el dato en texto: nunca solo color.
              <div
                key={h.id}
                className={`cuarto ${h.estado}`}
                title={
                  `Habitación ${h.numero} · ${h.estado.replace(/_/g, ' ')}` +
                  (h.ordenesActivas?.length
                    ? `\n${h.ordenesActivas.length} orden(es) abierta(s):\n${h.ordenesActivas.join('\n')}`
                    : '')
                }
              >
                {h.numero}
              </div>
            ))}
          </div>
        </div>
      ))}

      {habitaciones.some((h) => h.estado === 'FUERA_DE_SERVICIO') && (
        <div className="mensaje info" style={{ marginTop: 16 }}>
          Los cuartos en rojo están bloqueados por una orden abierta. Pasá el cursor por
          encima para ver cuáles, y mirá la pestaña <strong>Órdenes</strong> para el detalle.
        </div>
      )}

      <div style={{ marginTop: 14, display: 'flex', gap: 14, flexWrap: 'wrap' }}>
        {ESTADOS.map((e) => (
          <span key={e} className={`etiqueta ${COLOR[e]}`}>{e.replace(/_/g, ' ')}</span>
        ))}
      </div>
    </section>
  )
}
