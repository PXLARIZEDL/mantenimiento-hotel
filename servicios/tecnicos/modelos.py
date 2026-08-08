# PROPÓSITO: definir las formas de datos del servicio tecnicos: las entidades que
#   se guardan en PostgreSQL y los esquemas de los eventos que entran y salen.
#   Es el archivo que garantiza que Python y C# se entiendan.
#
# DEBE CONTENER:
#   1. La entidad Tecnico: id, nombre, especialidad, turno, activo.
#   2. La entidad Asignacion: id de la orden, id del técnico, número de
#      habitación, fecha de asignación. Es la traza de lo que este servicio
#      decidió.
#   3. Las enumeraciones del dominio:
#        Especialidad = AIRE | PLOMERIA | CERRADURA  (deben coincidir EXACTAMENTE
#          con el campo tipoFalla del contrato orden.creada.v1.json)
#        Turno = MAÑANA | TARDE | NOCHE
#   4. El esquema de ENTRADA del evento orden.creada, con los campos del contrato
#      (eventoId, ordenId, numeroHabitacion, tipoFalla, prioridad, ocurridoEn).
#   5. El esquema de SALIDA del evento orden.asignada, con serialización en
#      camelCase mediante alias — el consumidor del otro lado es C# y espera
#      camelCase, no snake_case.
#   6. Los esquemas de respuesta de los endpoints de main.py.
#   7. Tolerancia a campos desconocidos al deserializar eventos entrantes: un
#      campo nuevo en v1.1 no debe tumbar el consumidor.
#
# NO DEBE CONTENER:
#   1. La regla de asignación; vive en asignador.py.
#   2. La conexión ni las sesiones de base de datos; viven en base_datos.py.
#   3. Modelos de habitación ni de orden completa: de la orden solo interesan los
#      campos que llegan en el evento.
#   4. Lógica de RabbitMQ.
#
# RELACIONADO:
#   - contratos/orden.creada.v1.json (esquema de entrada)
#   - contratos/orden.asignada.v1.json (esquema de salida)
#   - consumidor.py (usa ambos esquemas), base_datos.py (persiste las entidades)
