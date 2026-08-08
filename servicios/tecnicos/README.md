# Servicio `tecnicos`

Python 3.12 / FastAPI + PostgreSQL

---

## Qué hace

Conoce al personal de mantenimiento y **decide quién atiende cada falla**. La
asignación es automática: nadie la pide por HTTP, se dispara al consumir el
evento `orden.creada`.

---

## De qué datos es dueño

| Dato | Detalle |
|---|---|
| Técnico | id, nombre, especialidad, turno, activo |
| Especialidad | `AIRE` / `PLOMERIA` / `CERRADURA` |
| Turno | `MAÑANA` / `TARDE` / `NOCHE` |
| Asignación | qué técnico quedó a cargo de qué orden y cuándo |

Es el único servicio que sabe qué es un turno y qué es una especialidad. Por eso
la regla de asignación vive aquí y no en `ordenes`
(ver `docs/adr/002-limites-contextos.md`).

---

## Con quién habla

| Dirección | Con quién | Cómo |
|---|---|---|
| Consume | `orden.creada` | cola `tecnicos.orden-creada` |
| Publica | `orden.asignada` | exchange `hotel.eventos` |
| Entrante | `gateway` → `ui` | HTTP (solo consultas) |
| Saliente HTTP | **nadie** | — |

Es el único servicio Python que **produce** un evento consumido por C#. Por eso
`modelos.py` debe serializar en **camelCase**, no en snake_case.

---

## La regla de asignación

```
tipoFalla ──▶ especialidad ──▶ técnicos activos de esa especialidad
                                        │
                                 filtrar por turno vigente
                                        │
                                 desempate: menos órdenes abiertas
                                        │
                                 ¿ninguno? ⇒ NO se publica orden.asignada
```

Vive aislada en `asignador.py` para poder probarla sin base de datos ni broker.

---

## API prevista

| Método | Ruta |
|---|---|
| `GET` | `/tecnicos` (filtro por especialidad y turno) |
| `GET` | `/tecnicos/{id}` |
| `GET` | `/tecnicos/disponibles` |
| `GET` | `/asignaciones` |
| `GET` | `/salud` |

No hay `POST` de asignación: asignar es consecuencia de un evento, no de una
petición.

---

## Cómo se levanta

```
docker compose up tecnicos
```

Depende de `db-tecnicos` y de `rabbitmq`. Al arrancar siembra técnicos de prueba
—al menos uno por especialidad y turno— porque sin ellos no hay caso de uso que
demostrar.

---

## Preguntas guía pendientes

1. Si no hay técnico disponible, ¿el mensaje se descarta, se reencola o se
   pospone al próximo turno?
2. ¿El desempate es por menos carga, por antigüedad o aleatorio? Justificar.
3. ¿Qué pasa si llega `orden.creada` con un `tipoFalla` que no existe?
