// PROPÓSITO: publicar en RabbitMQ los eventos de los que ordenes es productor
//   (orden.creada y orden.resuelta). Aísla al resto del servicio de la API de
//   RabbitMQ: quien publica solo entrega un objeto de evento.
//
// DEBE CONTENER:
//   1. Una interfaz IPublicadorEventos con un método PublicarAsync que reciba la
//      routing key y el evento.
//   2. La implementación con RabbitMQ.Client: conexión, canal y declaración del
//      exchange "hotel.eventos" de tipo topic, durable.
//   3. La declaración del exchange al arrancar, de forma idempotente — el
//      servicio no debe asumir que otro ya lo creó.
//   4. Serialización a JSON en camelCase, para que los consumidores Python lean
//      exactamente lo que dicen los contratos.
//   5. Marcado de los mensajes como persistentes, para que no se pierdan si
//      RabbitMQ se reinicia.
//   6. Reconexión ante caída del broker, con espera creciente.
//   7. Generación del sobre común de todo evento: eventoId, tipoEvento, version
//      y ocurridoEn en UTC.
//
// NO DEBE CONTENER:
//   - Consumo de mensajes; eso vive en Eventos/ConsumidorOrdenAsignada.cs.
//   - La forma de orden.asignada; ese evento lo PRODUCE tecnicos, aquí solo se
//     consume.
//   - Reglas de negocio ni decisiones sobre cuándo publicar; el "cuándo" lo
//     decide Endpoints/OrdenesEndpoints.cs.
//   - Acceso a la base de datos.
//
// RELACIONADO:
//   - contratos/orden.creada.v1.json, contratos/orden.resuelta.v1.json
//   - docs/catalogo-eventos.md
//   - Consumidores del otro lado: servicios/tecnicos/consumidor.py y
//     servicios/notificaciones/consumidor.py
