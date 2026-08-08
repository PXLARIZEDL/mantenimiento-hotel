# PROPÓSITO: contener la REGLA DE NEGOCIO más importante de este servicio: dado
#   el tipo de falla de una orden, decidir qué técnico se encarga. Está en su
#   propio archivo, aislado de la cola y de la base, para poder probarlo solo.
#
# DEBE CONTENER:
#   1. Una función de decisión que reciba el tipo de falla (y la prioridad y la
#      hora) y devuelva el técnico elegido, o nada si no hay ninguno.
#   2. El mapeo de tipo de falla a especialidad: AIRE, PLOMERIA, CERRADURA.
#   3. El filtro por TURNO vigente según la hora del evento: un técnico del turno
#      de mañana no puede recibir una orden de las 2 a.m.
#   4. El criterio de desempate cuando hay varios candidatos — por ejemplo, el
#      que tenga menos órdenes abiertas. Debe quedar escrito CUÁL se eligió y por
#      qué, porque es una pregunta segura en la defensa.
#   5. El caso "no hay técnico disponible" devuelto de forma explícita, no como
#      una excepción ni como un valor vacío ambiguo.
#   6. Registro (log) del motivo de la decisión: a quién se eligió y por qué se
#      descartaron los demás.
#
# NO DEBE CONTENER:
#   1. Acceso a la base de datos; los candidatos se reciben como parámetro o se
#      piden a base_datos.py desde el llamador. Esta función debe ser probable
#      sin levantar PostgreSQL.
#   2. Publicación de eventos ni contacto con RabbitMQ; de eso se ocupa
#      consumidor.py.
#   3. Endpoints HTTP.
#   4. Reglas sobre el estado de la habitación o el estado de la orden; esos
#      dominios son de habitaciones y de ordenes.
#
# RELACIONADO:
#   - consumidor.py (único llamador)
#   - contratos/orden.creada.v1.json → campo tipoFalla
#   - contratos/orden.asignada.v1.json → campos tecnicoId, especialidad, turno
#   - docs/adr/002-limites-contextos.md (por qué esta regla vive aquí y no en
#     el servicio ordenes)
