// Muestra si cada servicio del sistema está vivo. Es la pantalla que permite
// explicar y depurar la arquitectura: cuando algo no funciona, aquí se ve cuál
// de las piezas se cayó.
//
// Es un panel de LECTURA: no hay botones para reiniciar servicios ni purgar
// colas. Y no consulta el /salud de cada servicio por su cuenta: la agregación
// es responsabilidad del gateway.

import { useCallback, useEffect, useState } from 'react'

import { obtenerSalud } from '../api.js'

const SEGUNDOS_REFRESCO = 5

export default function PanelSalud() {
  const [salud, setSalud] = useState(null)
  const [error, setError] = useState(null)
  const [milisegundos, setMilisegundos] = useState(null)
  const [actualizado, setActualizado] = useState(null)
  const [automatico, setAutomatico] = useState(true)

  const cargar = useCallback(async () => {
    const arranque = performance.now()
    try {
      const datos = await obtenerSalud()
      setMilisegundos(Math.round(performance.now() - arranque))
      setSalud(datos)
      setError(null)
    } catch (fallo) {
      // Si el gateway no responde no se puede saber nada de nadie, y hay que
      // decirlo así en vez de pintar todo en rojo como si los cuatro estuvieran
      // caídos.
      setError(fallo)
      setSalud(null)
    } finally {
      setActualizado(new Date())
    }
  }, [])

  useEffect(() => {
    cargar()
    if (!automatico) return
    const cada = setInterval(cargar, SEGUNDOS_REFRESCO * 1000)
    return () => clearInterval(cada)
  }, [cargar, automatico])

  return (
    <section className="tarjeta">
      <h2>Salud del sistema</h2>
      <p className="ayuda">
        El gateway consulta el <code>/salud</code> de los cuatro servicios y devuelve el resumen.
      </p>

      <div className="fila" style={{ marginBottom: 14 }}>
        <button className="suave" onClick={cargar}>Actualizar ahora</button>
        <button className="suave" onClick={() => setAutomatico(!automatico)}>
          {automatico ? `Detener refresco (cada ${SEGUNDOS_REFRESCO}s)` : 'Reanudar refresco'}
        </button>
        {actualizado && (
          <span style={{ color: 'var(--texto-suave)', fontSize: 12, alignSelf: 'center' }}>
            Última actualización: {actualizado.toLocaleTimeString('es', { hour12: false })}
            {milisegundos !== null && ` · ${milisegundos} ms`}
          </span>
        )}
      </div>

      {error && (
        <div className="mensaje error">
          <strong>El gateway no responde.</strong>
          <div style={{ marginTop: 6 }}>
            Sin él no se puede saber el estado de ningún servicio — que no aparezcan no
            significa que estén caídos. {error.message}
          </div>
        </div>
      )}

      {salud && (
        <>
          <div className={`mensaje ${salud.estado === 'sano' ? 'exito' : 'aviso'}`}>
            Estado general: <strong>{salud.estado}</strong>
            {salud.estado !== 'sano' && ' — hay al menos un servicio sin responder.'}
          </div>

          <div className="tarjetas-salud">
            {/* El gateway responde, así que está vivo por definición. */}
            <div className="servicio sano">
              <h3>gateway</h3>
              <span className="etiqueta verde">SANO</span>
              <div className="detalle" style={{ marginTop: 8 }}>
                Responde en {milisegundos} ms. Es el único puerto abierto hacia afuera.
              </div>
            </div>

            {salud.servicios?.map((s) => (
              <div
                key={s.nombre}
                className={`servicio ${s.estado === 'sano' ? 'sano' : s.estado === 'caido' ? 'caido' : 'desconocido'}`}
              >
                <h3>{s.nombre}</h3>
                <span className={`etiqueta ${s.estado === 'sano' ? 'verde' : 'rojo'}`}>
                  {s.estado.toUpperCase()}
                </span>
                <div className="detalle" style={{ marginTop: 8 }}>{s.detalle}</div>
              </div>
            ))}
          </div>
        </>
      )}

      <div className="mensaje info" style={{ marginTop: 18 }}>
        <strong>Para la demostración:</strong> apagá un servicio con{' '}
        <code>docker compose stop notificaciones</code> y miralo caer aquí. El resto del
        sistema sigue funcionando — se pueden seguir creando y resolviendo órdenes, y los
        avisos pendientes se acumulan en la cola durable hasta que vuelva.
      </div>

      {/* El circuit breaker de ordenes hacia habitaciones existe y funciona,
          pero ordenes no expone su estado por HTTP. Se dice en vez de
          inventar un indicador que no tiene de dónde leer. */}
      <div className="mensaje" style={{ border: '1px solid var(--borde)' }}>
        El <strong>circuit breaker</strong> de <code>ordenes</code> hacia{' '}
        <code>habitaciones</code> no se muestra aquí porque el servicio no expone su estado.
        Se comprueba apagando <code>habitaciones</code> e intentando reportar una falla: tras
        varios intentos fallidos el circuito abre y la respuesta pasa a ser inmediata.
      </div>
    </section>
  )
}
