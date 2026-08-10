// Arranca React: conecta index.html con el árbol de componentes. Es el
// equivalente al Program.cs de los servicios .NET.
//
// Aquí no hay marcado ni llamadas al backend: el primer componente real es App.

import React from 'react'
import { createRoot } from 'react-dom/client'

import App from './App.jsx'
import './estilos.css'

createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
