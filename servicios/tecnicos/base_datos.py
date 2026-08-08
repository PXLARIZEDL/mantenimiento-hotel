# PROPÓSITO: única puerta de acceso a la base PostgreSQL del servicio tecnicos.
#   Concentra la conexión, las sesiones y las consultas, para que ni el
#   asignador ni el consumidor sepan cómo se guardan las cosas.
#
# DEBE CONTENER:
#   1. La creación del motor de conexión leyendo la URL desde variables de
#      entorno, con pool y reconexión.
#   2. La fábrica de sesiones y la dependencia de FastAPI que las entrega y las
#      cierra.
#   3. La creación de las tablas al arrancar y la SIEMBRA inicial de técnicos de
#      prueba, con al menos uno por especialidad y por turno; sin ellos el caso
#      de uso no se puede demostrar.
#   4. Las consultas que el resto del servicio necesita, con nombre propio:
#        - buscar técnicos por especialidad y turno
#        - listar todos / obtener por id
#        - guardar una asignación
#        - contar asignaciones abiertas por técnico (para repartir la carga)
#   5. El registro de eventos ya procesados por eventoId, que es lo que hace
#      IDEMPOTENTE el consumo de orden.creada.
#   6. Índices sobre especialidad y turno.
#
# NO DEBE CONTENER:
#   1. La regla de a quién asignar; eso es de asignador.py. Aquí se BUSCA, allá
#      se DECIDE.
#   2. Endpoints HTTP ni esquemas de respuesta.
#   3. Publicación de eventos.
#   4. La URL de conexión escrita literalmente en el código.
#   5. Acceso a las bases de habitaciones ni de ordenes.
#
# RELACIONADO:
#   - modelos.py (las entidades que aquí se persisten)
#   - asignador.py (consume las consultas de búsqueda)
#   - consumidor.py (usa el registro de idempotencia)
#   - docker-compose.yml → servicio db-tecnicos
