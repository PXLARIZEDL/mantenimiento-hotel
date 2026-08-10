# Entrega — Sistema de mantenimiento hotelero con microservicios

Carpeta de documentación del proyecto. Cada archivo cubre una parte concreta de
lo que pidió el profesor.

---

## Qué pidió el mandato y dónde está respondido

| Lo que pidió | Dónde está |
|---|---|
| Una **necesidad** que los microservicios satisfagan | [01 — Necesidad y antecedentes](01-necesidad-y-antecedentes.md) |
| **Antecedentes** a nivel de investigación, con **fuentes citadas** | [01 — Necesidad y antecedentes](01-necesidad-y-antecedentes.md) |
| Conocer e investigar los antecedentes de los microservicios | [01 §3 — Antecedentes del estilo](01-necesidad-y-antecedentes.md) |
| **Diferencias de arquitectura y diseño** | [02 — Decisiones arquitectónicas](02-decisiones-arquitectonicas.md) |
| **SOLID** | [03 — SOLID en el código](03-solid-en-el-codigo.md) |
| **Docker** | [02 §6 — Empaquetado](02-decisiones-arquitectonicas.md) |
| **Python y C#** en el mismo sistema | [02 §5 — Políglota](02-decisiones-arquitectonicas.md) |
| **Mínimo 4 microservicios** | 6 implementados — [02 §2](02-decisiones-arquitectonicas.md) |
| Defensa: **apagar uno y que los demás funcionen** | [04 — Guion de defensa](04-guion-de-defensa.md) |
| Destacar **lo que nos pareció interesante e importante** | [05 — Para la presentación](05-para-la-presentacion.md) |

Reparto de puntos: **15 pts** el trabajo, **10 pts** la defensa.

---

## Archivos

| Archivo | Para qué sirve |
|---|---|
| [01-necesidad-y-antecedentes.md](01-necesidad-y-antecedentes.md) | El problema real, qué se ha hecho antes, y la bibliografía |
| [02-decisiones-arquitectonicas.md](02-decisiones-arquitectonicas.md) | Cada decisión y por qué se tomó, con las alternativas descartadas |
| [03-solid-en-el-codigo.md](03-solid-en-el-codigo.md) | Los cinco principios apuntando a archivos y líneas concretas |
| [04-guion-de-defensa.md](04-guion-de-defensa.md) | Qué mostrar, en qué orden y qué comandos escribir |
| [05-para-la-presentacion.md](05-para-la-presentacion.md) | Guion de diapositivas y lo que resultó más interesante |
| [06-estado-y-pendientes.md](06-estado-y-pendientes.md) | Qué funciona, qué falta y qué está mal a propósito |
| [07-manual-de-estudio.md](07-manual-de-estudio.md) | Guía de estudio del equipo, con ficha por persona |

---

## Antes de entregar

Tres cosas que hay que hacer a mano y que nadie más puede hacer por ustedes:

1. **Verificar los enlaces** de la bibliografía y **agregar la fecha de consulta**.
   Las referencias son reales, pero las URLs cambian y el formato de cita que
   exige la materia puede no ser el que está usado aquí.
2. **Poner los nombres** de los integrantes y la fecha de entrega.
3. **Leer el documento 06** — dice qué está sin terminar. Es mejor llegar
   sabiéndolo que que lo encuentre el profesor.

---

## El repositorio

```
https://github.com/PXLARIZEDL/mantenimiento-hotel
```

Para levantar el sistema completo:

```bash
cp .env.example .env
docker compose up --build
```

Interfaz en **http://localhost:5173**
