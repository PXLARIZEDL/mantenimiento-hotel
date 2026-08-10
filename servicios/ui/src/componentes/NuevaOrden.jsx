// Formulario con el que un empleado reporta una falla. Es el DISPARADOR de todo
// el caso de uso: al enviarlo se bloquea la habitación, se crea la orden y
// arranca la cadena de eventos.
//
// No hay selector de técnico: la asignación es automática y la decide el
// servicio tecnicos. Ofrecerla aquí contradiría el diseño.

import { useState } from 'react'

import { crearOrden, ErrorApi } from '../api.js'

// Los cuatro valores permitidos de contratos/orden.creada.v1.json. Deben
// coincidir EXACTAMENTE con lo que espera tecnicos para elegir especialidad.
const TIPOS_FALLA = [
  { valor: 'AIRE_ACONDICIONADO', texto: 'Aire acondicionado' },
  { valor: 'PLOMERIA', texto: 'Plomería' },
  { valor: 'CERRADURA', texto: 'Cerradura' },
  { valor: 'ELECTRICIDAD', texto: 'Electricidad' },
]

const PRIORIDADES = ['BAJA', 'MEDIA', 'ALTA']

const INICIAL = {
  habitacionNumero: '',
  tipoFalla: 'AIRE_ACONDICIONADO',
  descripcion: '',
  prioridad: 'MEDIA',
  reportadoPor: '',
}

// `usuario` viene del ingreso. Se usa para rellenar quién reporta en vez de
// hacer que se escriba a mano en cada falla, pero el campo sigue siendo
// editable: quien está en la terminal puede estar reportando por otra persona.
export default function NuevaOrden({ usuario = '' }) {
  const [datos, setDatos] = useState({ ...INICIAL, reportadoPor: usuario })
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState(null)
  const [creada, setCreada] = useState(null)

  const cambiar = (campo) => (evento) => {
    setDatos({ ...datos, [campo]: evento.target.value })
  }

  async function enviar(evento) {
    evento.preventDefault()
    setError(null)
    setCreada(null)

    // Validación de FORMATO nada más. Las reglas de negocio las valida el
    // backend; replicarlas aquí sería tener la misma regla en dos lugares.
    const numero = Number(datos.habitacionNumero)
    if (!Number.isInteger(numero) || numero < 1 || numero > 400) {
      setError(new ErrorApi('El número de habitación debe estar entre 1 y 400.', { codigo: 400 }))
      return
    }
    if (!datos.descripcion.trim()) {
      setError(new ErrorApi('Contá qué está fallando.', { codigo: 400 }))
      return
    }

    setEnviando(true)
    try {
      const orden = await crearOrden({
        habitacionNumero: numero,
        tipoFalla: datos.tipoFalla,
        descripcion: datos.descripcion.trim(),
        prioridad: datos.prioridad,
        reportadoPor: datos.reportadoPor.trim() || usuario || 'recepcion',
      })
      setCreada(orden)
      setDatos({ ...INICIAL, reportadoPor: datos.reportadoPor })
    } catch (fallo) {
      setError(fallo)
    } finally {
      // El botón se rehabilita siempre: dejarlo bloqueado tras un error
      // impediría reintentar justo cuando hace falta.
      setEnviando(false)
    }
  }

  return (
    <section className="tarjeta">
      <h2>Reportar una falla</h2>
      <p className="ayuda">
        Al enviar, la habitación se bloquea automáticamente y se busca un técnico.
      </p>

      <form onSubmit={enviar}>
        <div className="fila">
          <div className="campo">
            <label htmlFor="hab">Habitación (1–400)</label>
            <input
              id="hab"
              type="number"
              min="1"
              max="400"
              required
              style={{ width: 120 }}
              value={datos.habitacionNumero}
              onChange={cambiar('habitacionNumero')}
            />
          </div>

          <div className="campo">
            <label htmlFor="tipo">Tipo de falla</label>
            <select id="tipo" value={datos.tipoFalla} onChange={cambiar('tipoFalla')}>
              {TIPOS_FALLA.map((t) => (
                <option key={t.valor} value={t.valor}>{t.texto}</option>
              ))}
            </select>
          </div>

          <div className="campo">
            <label htmlFor="prio">Prioridad</label>
            <select id="prio" value={datos.prioridad} onChange={cambiar('prioridad')}>
              {PRIORIDADES.map((p) => <option key={p} value={p}>{p}</option>)}
            </select>
          </div>

          <div className="campo">
            <label htmlFor="quien">Reportado por</label>
            <input id="quien" value={datos.reportadoPor} onChange={cambiar('reportadoPor')} />
          </div>
        </div>

        <div className="campo" style={{ marginTop: 12 }}>
          <label htmlFor="desc">¿Qué está fallando?</label>
          <textarea
            id="desc"
            required
            value={datos.descripcion}
            onChange={cambiar('descripcion')}
            placeholder="No enfría y gotea sobre la alfombra."
          />
        </div>

        <button className="accion" type="submit" disabled={enviando} style={{ marginTop: 14 }}>
          {enviando ? 'Enviando…' : 'Reportar falla'}
        </button>
      </form>

      {error && (
        <div className={`mensaje ${error.esCircuitoAbierto ? 'aviso' : 'error'}`}>
          {error.esCircuitoAbierto ? (
            <>
              <strong>El servicio de habitaciones no responde.</strong>
              <div style={{ marginTop: 6 }}>
                La orden <strong>no se creó</strong>. No se perdió nada: volvé a intentarlo
                en unos segundos.
              </div>
            </>
          ) : (
            <>
              {error.codigo === 409 && <strong>No se pudo bloquear la habitación. </strong>}
              {error.message}
            </>
          )}
        </div>
      )}

      {creada && (
        <div className="mensaje exito">
          <strong>Orden creada.</strong> Habitación {creada.habitacionNumero} · nº{' '}
          <code>{creada.id}</code>
          <div style={{ marginTop: 8 }}>
            Quedó en estado <strong>{creada.estado}</strong>. La asignación del técnico es
            <strong> asincrónica</strong>: en unos segundos aparecerá como ASIGNADA en la
            pestaña <em>Órdenes</em>, sin que nadie la toque.
          </div>
        </div>
      )}
    </section>
  )
}
