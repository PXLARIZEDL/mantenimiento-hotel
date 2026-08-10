# 1. Necesidad y antecedentes

---

## 1. La necesidad

### El problema

En un hotel de 400 habitaciones, una falla de mantenimiento —el aire que no
enfría, una fuga, una cerradura que no abre— crea un conflicto entre dos áreas
que trabajan con información distinta:

- **Recepción** necesita saber, en el momento, qué cuartos puede vender.
- **Mantenimiento** necesita saber qué hay que arreglar y quién lo arregla.

Cuando esa comunicación es un cuaderno, un radio o un grupo de WhatsApp, aparecen
tres fallas concretas:

1. **Se vende un cuarto roto.** Recepción no se enteró de la falla, o se enteró y
   no lo anotó. Es el error más caro: reubicar a un huésped molesto cuesta más
   que la reparación.
2. **El cuarto queda bloqueado de más.** El técnico lo arregló y nadie avisó a
   recepción. La habitación sale del inventario días de más, sin motivo.
3. **La orden no llega al técnico correcto.** Un problema eléctrico va al
   plomero, o llega a alguien que ya salió de turno.

### Por qué es un problema de software

Las tres fallas son de **coordinación**, no de ejecución. Nadie hace mal su
trabajo: la información no llega a tiempo al que la necesita.

El sistema automatiza esa coordinación. Al reportarse una falla:

1. La habitación se **bloquea sola**, sin que recepción tenga que acordarse.
2. Se **asigna un técnico** según especialidad y turno vigente, sin que nadie
   elija a mano.
3. Recepción **recibe el aviso** de cada cambio.
4. Al cerrarse la orden, el cuarto **vuelve al inventario solo**.

### Por qué microservicios y no un monolito

Esta es la pregunta que hay que saber responder, y la respuesta honesta tiene dos
partes.

**La parte real.** El dominio se parte solo en tres pedazos que cambian a ritmos
distintos y pertenecen a áreas distintas del hotel:

| Pedazo | Quién lo posee | Cada cuánto cambia |
|---|---|---|
| Inventario y estado de los cuartos | Recepción | Casi nunca |
| Ciclo de vida de la orden | Mantenimiento | Constantemente |
| Personal, turnos, especialidades | Recursos humanos | Cada cambio de plantilla |

Que el estado de un cuarto sea de un solo dueño **es la regla que evita la falla
número 1**. Si cualquiera pudiera escribir ese dato, se vuelve al problema que se
quería resolver. Los microservicios hacen esa propiedad explícita y verificable:
nadie más tiene acceso a esa base.

**La parte honesta.** Para 400 habitaciones y un hotel, un monolito modular bien
hecho también funcionaría, y sería más simple de operar. La decisión de ir a
microservicios está tomada también porque **es el objeto de la práctica**, y eso
está escrito en el ADR 001 en vez de disimulado.

Lo que sí es genuino del estilo, y no se conseguiría igual de fácil en un
monolito, es la **tolerancia a fallos parciales**: que el servicio de avisos se
caiga y se puedan seguir creando órdenes. Eso se demuestra en vivo (documento 04).

---

## 2. Antecedentes del dominio: ¿se ha hecho algo parecido?

Sí. El problema está resuelto comercialmente hace décadas, en dos familias de
producto que conviene conocer porque delimitan qué es original y qué no.

### CMMS — Computerized Maintenance Management System

Software de gestión de mantenimiento: registra activos, genera órdenes de
trabajo, las asigna a técnicos y lleva el historial. Nació en los años 60 en la
industria y se generalizó en los 90.

Ejemplos vigentes: **IBM Maximo**, **Fiix**, **UpKeep**, **Limble CMMS**.

**Qué toman de aquí:** el concepto de *orden de trabajo* con ciclo de vida
(abierta → asignada → resuelta) y la asignación por especialidad.

**En qué se diferencia el proyecto:** un CMMS genérico no sabe qué es una
habitación de hotel ni que bloquear un cuarto tiene consecuencia comercial
inmediata. El acoplamiento entre *orden* y *disponibilidad del inventario* es lo
propio del dominio hotelero.

### Sistemas de operaciones hoteleras

Más cercanos al caso: gestionan las peticiones de servicio dentro del hotel
—mantenimiento, limpieza, pedidos de huéspedes— y se integran con el PMS
(*Property Management System*), que es el que sabe qué cuartos hay y en qué
estado están.

Ejemplos: **Amadeus HotSOS**, **Quore**, y los módulos de mantenimiento de
**Oracle OPERA**, **Mews** y **Cloudbeds**.

**Qué toman de aquí:** exactamente el flujo del proyecto —reportar, bloquear el
cuarto, despachar a un técnico, notificar a recepción.

**En qué se diferencia el proyecto:** los productos comerciales son en su mayoría
monolitos o suites integradas. Aquí el aporte no es el flujo —que está
inventado— sino **la partición**: separarlo en servicios independientes,
políglotas, con base de datos propia y comunicación por eventos.

> **Conclusión honesta para la defensa:** el caso de uso no es original. Lo que se
> está ejercitando es la **arquitectura**, no el descubrimiento del problema.
> Decirlo así es más sólido que fingir que se inventó algo.

---

## 3. Antecedentes del estilo arquitectónico

El profesor pidió conocer e investigar los antecedentes de los microservicios,
aunque no se usen todos. Esto es lo mínimo que hay que poder contar.

### 3.1 De dónde vienen

Los microservicios no aparecen de la nada: son la continuación de una discusión
de treinta años sobre cómo partir sistemas grandes.

| Etapa | Idea | Por qué se abandonó o evolucionó |
|---|---|---|
| **Años 90 — CORBA / DCOM** | Objetos distribuidos, llamadas remotas como si fueran locales | La abstracción mentía: la red falla y tiene latencia. Fue una lección cara |
| **Años 2000 — SOA con ESB** | Servicios de negocio con un bus central que orquesta y transforma | El bus se volvió un monolito con otro nombre, y un punto único de fallo |
| **2011-2014 — Microservicios** | Servicios pequeños, sin bus inteligente, dueños de sus datos | El estilo actual |

La frase que resume el cambio de SOA a microservicios es de James Lewis y Martin
Fowler: *«smart endpoints and dumb pipes»* — la lógica va en los servicios, no en
la tubería. **En este proyecto se cumple:** RabbitMQ solo enruta por routing key;
no transforma ni decide nada.

### 3.2 Los ocho principios de Lewis y Fowler (2014)

El artículo que fijó el término define nueve características. Estas son las que
el proyecto ejercita, y en qué archivo se ven:

| Característica | Dónde se ve |
|---|---|
| Componentización vía servicios | `servicios/` — seis procesos independientes |
| Organizado por capacidad de negocio | Cada servicio es un área del hotel, no una capa técnica |
| Productos, no proyectos | Cada servicio tiene su README y su dueño en `CODEOWNERS` |
| *Smart endpoints, dumb pipes* | RabbitMQ solo enruta; la lógica está en los consumidores |
| Gobierno descentralizado | C# y Python conviviendo, cada uno con sus librerías |
| **Datos descentralizados** | Tres PostgreSQL separados; nadie lee la base de otro |
| Diseño para el fallo | Circuit breaker, reintentos, idempotencia, colas durables |
| Automatización de infraestructura | `docker-compose.yml` y CI en GitHub Actions |

### 3.3 Casos reales que sirven de antecedente

- **Amazon (c. 2002).** Reorganización de un monolito hacia servicios con
  interfaces obligatorias entre equipos. Es el caso más citado de *Ley de
  Conway* aplicada a propósito: se cambió la organización para cambiar la
  arquitectura.
- **Netflix (2009-2016).** Migración de un monolito a cientos de servicios en la
  nube. De aquí salen las herramientas de resiliencia que hoy son estándar
  —**Hystrix**, el circuit breaker que popularizó el patrón que este proyecto
  implementa con Polly.
- **Uber.** Documentó públicamente tanto la migración a microservicios como los
  problemas de tenerlos de más, lo que dio lugar a la idea de *macroservicios*
  como corrección.

### 3.4 La crítica, que también hay que conocer

Presentar solo las ventajas es la forma más rápida de que una defensa se caiga.

- **Martin Fowler — «MonolithFirst» (2015):** casi todos los casos exitosos de
  microservicios empezaron como un monolito que se partió después. Los que
  arrancaron directamente en microservicios tuvieron problemas.
- **«Microservice Trade-Offs» (2015):** el estilo cobra un precio en
  consistencia, latencia y complejidad operativa. Solo se paga si el sistema es
  lo bastante grande.
- **La consistencia eventual no es gratis.** Pat Helland lo argumentó desde
  antes del término: sin transacciones distribuidas, el programador tiene que
  hacerse cargo a mano de la inconsistencia. **Este proyecto se topó con eso en
  la práctica** — ver el bug documentado en el documento 05.

---

## 4. Bibliografía

> Las referencias son reales. **Verificar los enlaces y agregar la fecha de
> consulta** antes de entregar, y ajustar el formato al que exija la materia
> (APA, IEEE…).

### Sobre el estilo arquitectónico

1. Lewis, J. y Fowler, M. (2014). *Microservices: a definition of this new
   architectural term.* martinfowler.com
   `https://martinfowler.com/articles/microservices.html`

2. Fowler, M. (2015). *Microservice Trade-Offs.* martinfowler.com
   `https://martinfowler.com/articles/microservice-trade-offs.html`

3. Fowler, M. (2015). *MonolithFirst.* martinfowler.com
   `https://martinfowler.com/bliki/MonolithFirst.html`

4. Newman, S. (2021). *Building Microservices: Designing Fine-Grained Systems*
   (2.ª ed.). O'Reilly Media.

5. Newman, S. (2019). *Monolith to Microservices: Evolutionary Patterns to
   Transform Your Monolith.* O'Reilly Media.

6. Richardson, C. (2018). *Microservices Patterns: With Examples in Java.*
   Manning Publications.

7. Richardson, C. *Microservice Architecture — pattern catalog.*
   `https://microservices.io/patterns/`
   (Patrones usados: *Database per Service*, *API Gateway*, *Saga*,
   *Transactional Outbox*, *Circuit Breaker*, *Idempotent Consumer*.)

### Sobre los límites y el diseño

8. Evans, E. (2003). *Domain-Driven Design: Tackling Complexity in the Heart of
   Software.* Addison-Wesley. — De aquí sale *bounded context*, que es lo que
   justifica dónde se cortó cada servicio.

9. Conway, M. E. (1968). *How Do Committees Invent?* Datamation, 14(5), 28-31.
   — La ley de Conway: la arquitectura termina copiando la estructura de
   comunicación del equipo.

10. Martin, R. C. (2017). *Clean Architecture: A Craftsman's Guide to Software
    Structure and Design.* Prentice Hall.

### Sobre resiliencia y datos distribuidos

11. Nygard, M. T. (2018). *Release It! Design and Deploy Production-Ready
    Software* (2.ª ed.). Pragmatic Bookshelf. — Origen del patrón
    **Circuit Breaker**, implementado en `servicios/ordenes/Program.cs`.

12. Fowler, M. (2014). *CircuitBreaker.* martinfowler.com
    `https://martinfowler.com/bliki/CircuitBreaker.html`

13. Hohpe, G. y Woolf, B. (2003). *Enterprise Integration Patterns: Designing,
    Building, and Deploying Messaging Solutions.* Addison-Wesley.
    — *Publish-Subscribe Channel*, *Idempotent Receiver*, *Dead Letter Channel*.

14. Helland, P. (2007). *Life beyond Distributed Transactions: An Apostate's
    Opinion.* CIDR (Conference on Innovative Data Systems Research).
    — Por qué se renuncia a las transacciones distribuidas.

15. Kleppmann, M. (2017). *Designing Data-Intensive Applications.* O'Reilly
    Media. — Entrega *at-least-once*, idempotencia y consistencia eventual.

### Documentación técnica

16. *RabbitMQ — AMQP 0-9-1 Model Explained.*
    `https://www.rabbitmq.com/tutorials/amqp-concepts.html`

17. Microsoft. *Build resilient HTTP apps with .NET.* Microsoft Learn.
    `https://learn.microsoft.com/dotnet/core/resilience/http-resilience`

18. Microsoft. *.NET Microservices: Architecture for Containerized .NET
    Applications.* Microsoft Learn.

### Sobre el dominio

19. *Computerized maintenance management system* — concepto y evolución.
    (Consultar bibliografía de gestión de mantenimiento industrial.)

20. Documentación de producto de sistemas de operaciones hoteleras:
    **Amadeus HotSOS**, **Quore**, **Oracle OPERA**. (Sitios de los fabricantes.)
