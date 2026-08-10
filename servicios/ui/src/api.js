// Única capa de comunicación entre la UI y el backend.
//
// Ningún componente llama a fetch por su cuenta: todo pasa por aquí. Si cambia
// una ruta o el manejo de errores, se cambia en un solo archivo.
//
// TODAS las rutas son relativas y entran por el GATEWAY. En desarrollo las
// resuelve el proxy de vite.config.js; en producción, nginx.conf. Llamar a un
// servicio directo rompería la arquitectura.

const BASE = import.meta.env.VITE_API_BASE ?? ''

const TIEMPO_ESPERA_MS = 15000

/** Error con un mensaje ya redactado en español para mostrar tal cual. */
export class ErrorApi extends Error {
  constructor(mensaje, { codigo = 0, esCircuitoAbierto = false } = {}) {
    super(mensaje)
    this.name = 'ErrorApi'
    this.codigo = codigo
    // El 503 de ordenes con el circuito abierto merece un trato aparte: la
    // orden NO se creó y el usuario puede reintentar.
    this.esCircuitoAbierto = esCircuitoAbierto
  }
}

/** Saca el mensaje del cuerpo, sea ProblemDetails o { mensaje }. */
function mensajeDelCuerpo(cuerpo, respaldo) {
  if (!cuerpo) return respaldo

  if (typeof cuerpo === 'string') return cuerpo || respaldo

  // ProblemDetails de ASP.NET: { title, detail, errors }
  if (cuerpo.errors && typeof cuerpo.errors === 'object') {
    const primero = Object.values(cuerpo.errors).flat()[0]
    if (primero) return primero
  }

  return cuerpo.detail || cuerpo.mensaje || cuerpo.title || respaldo
}

async function pedir(ruta, opciones = {}) {
  const control = new AbortController()
  const alarma = setTimeout(() => control.abort(), TIEMPO_ESPERA_MS)

  let respuesta
  try {
    respuesta = await fetch(BASE + ruta, {
      ...opciones,
      signal: control.signal,
      headers: {
        'Content-Type': 'application/json',
        ...(opciones.headers ?? {}),
      },
    })
  } catch (error) {
    clearTimeout(alarma)

    if (error.name === 'AbortError') {
      throw new ErrorApi('El servidor tardó demasiado en responder.')
    }
    throw new ErrorApi('No se pudo contactar al servidor. ¿Está levantado el sistema?')
  } finally {
    clearTimeout(alarma)
  }

  // 204 y demás respuestas sin cuerpo.
  const texto = await respuesta.text()
  let cuerpo = null
  if (texto) {
    try {
      cuerpo = JSON.parse(texto)
    } catch {
      cuerpo = texto
    }
  }

  if (respuesta.ok) return cuerpo

  switch (respuesta.status) {
    case 400:
      throw new ErrorApi(mensajeDelCuerpo(cuerpo, 'Los datos enviados no son válidos.'), {
        codigo: 400,
      })
    case 404:
      throw new ErrorApi(mensajeDelCuerpo(cuerpo, 'No se encontró lo que se pidió.'), {
        codigo: 404,
      })
    case 409:
      throw new ErrorApi(
        mensajeDelCuerpo(cuerpo, 'La operación no es válida en el estado actual.'),
        { codigo: 409 },
      )
    case 503:
      throw new ErrorApi(
        mensajeDelCuerpo(
          cuerpo,
          'El servicio de habitaciones no está disponible. La orden NO se creó; volvé a intentarlo.',
        ),
        { codigo: 503, esCircuitoAbierto: true },
      )
    default:
      throw new ErrorApi(
        mensajeDelCuerpo(cuerpo, `El servidor respondió ${respuesta.status}.`),
        { codigo: respuesta.status },
      )
  }
}

function conParametros(ruta, parametros) {
  const limpios = Object.entries(parametros ?? {}).filter(
    ([, valor]) => valor !== undefined && valor !== null && valor !== '',
  )
  if (limpios.length === 0) return ruta

  return `${ruta}?${new URLSearchParams(limpios).toString()}`
}

// --- habitaciones ----------------------------------------------------------

export const listarHabitaciones = (filtros) =>
  pedir(conParametros('/habitaciones', filtros))

export const obtenerHabitacion = (numero) => pedir(`/habitaciones/${numero}`)

// --- ordenes ---------------------------------------------------------------

export const crearOrden = (orden) =>
  pedir('/ordenes', { method: 'POST', body: JSON.stringify(orden) })

export const listarOrdenes = (filtros) => pedir(conParametros('/ordenes', filtros))

export const obtenerOrden = (id) => pedir(`/ordenes/${id}`)

export const resolverOrden = (id, notaCierre) =>
  pedir(`/ordenes/${id}/resolver`, {
    method: 'PUT',
    body: JSON.stringify({ notaCierre }),
  })

// --- tecnicos --------------------------------------------------------------

export const listarTecnicos = (filtros) => pedir(conParametros('/tecnicos', filtros))

export const listarTecnicosDisponibles = () => pedir('/tecnicos/disponibles')

export const listarAsignaciones = () => pedir('/asignaciones')

// --- notificaciones --------------------------------------------------------

export const listarNotificaciones = (filtros) =>
  pedir(conParametros('/notificaciones', filtros))

export const marcarNotificacionLeida = (id) =>
  pedir(`/notificaciones/${id}/leida`, { method: 'POST' })

// --- salud -----------------------------------------------------------------

/** Endpoint AGREGADO del gateway: él consulta el /salud de los cuatro. */
export const obtenerSalud = () => pedir('/salud')
