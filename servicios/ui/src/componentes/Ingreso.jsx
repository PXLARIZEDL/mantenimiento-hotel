// Pantalla de entrada al sistema.
//
// OJO CON EL NOMBRE: esto NO es autenticación. No hay clave, no hay usuarios en
// ninguna base y el backend no lo verifica. Solo pregunta quién está usando la
// terminal para saludarlo y para rellenar el campo "reportado por" de las
// órdenes, que hoy había que escribir a mano cada vez.
//
// Cualquiera con acceso a la red entra igual. Autenticación de verdad sería
// trabajo del gateway, y en la v1 no está (ver README de ui).

import { useState } from 'react'

export default function Ingreso({ onEntrar }) {
  const [nombre, setNombre] = useState('')

  function enviar(evento) {
    evento.preventDefault()
    const limpio = nombre.trim()
    if (limpio) onEntrar(limpio)
  }

  return (
    <div className="pantalla-ingreso">
      <form className="caja-ingreso" onSubmit={enviar}>
        <div className="marca">
          <span className="logo" aria-hidden="true">🛎️</span>
          <h1>Mantenimiento</h1>
          <p>Sistema de órdenes · Hotel 400 habitaciones</p>
        </div>

        <div className="campo">
          <label htmlFor="nombre">¿Quién sos?</label>
          <input
            id="nombre"
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            placeholder="Tu nombre"
            autoFocus
            autoComplete="off"
          />
        </div>

        <button className="accion" type="submit" disabled={!nombre.trim()}>
          Entrar al sistema
        </button>

        <p className="nota-ingreso">
          No hace falta contraseña. Tu nombre solo se usa para saludarte y para
          registrar quién reporta cada falla.
        </p>
      </form>
    </div>
  )
}
