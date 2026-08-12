# Mapa técnico

Extracción cruda del repositorio. Commit base: `5149599`.

Convención: `archivo:línea`. Las rutas son relativas a la raíz del repositorio.

---

# PARTE 1 — Inventario por servicio

## 1.1 `habitaciones` (C# / .NET 8)

| Archivo | Líneas |
|---|---|
| `servicios/habitaciones/Program.cs` | 70 |
| `servicios/habitaciones/Habitaciones.csproj` | 34 |
| `servicios/habitaciones/appsettings.json` | 25 |
| `servicios/habitaciones/Dockerfile` | 33 |
| `servicios/habitaciones/.dockerignore` | 28 |
| `servicios/habitaciones/README.md` | 155 |
| `servicios/habitaciones/.config/dotnet-tools.json` | 12 |
| `servicios/habitaciones/Modelos/EstadoHabitacion.cs` | 26 |
| `servicios/habitaciones/Modelos/Habitacion.cs` | 152 |
| `servicios/habitaciones/Datos/HabitacionesDbContext.cs` | 72 |
| `servicios/habitaciones/Datos/SembradorHabitaciones.cs` | 69 |
| `servicios/habitaciones/Endpoints/HabitacionesEndpoints.cs` | 199 |
| `servicios/habitaciones/Datos/Migraciones/20260809204648_EsquemaInicial.cs` | 57 |
| `servicios/habitaciones/Datos/Migraciones/20260809204648_EsquemaInicial.Designer.cs` | 78 |
| `servicios/habitaciones/Datos/Migraciones/HabitacionesDbContextModelSnapshot.cs` | 75 |

### Definiciones

**`Modelos/EstadoHabitacion.cs`**

| Línea | Define |
|---|---|
| 11 | `enum EstadoHabitacion` — `DISPONIBLE`, `OCUPADA`, `FUERA_DE_SERVICIO` |
| 18 | `static class EstadosHabitacion` |
| 24 | `static bool AdmiteReservas(this EstadoHabitacion)` — método de extensión |

Importa: nada (solo `namespace Habitaciones.Modelos`).
Lo usa: `Modelos/Habitacion.cs`, `Datos/HabitacionesDbContext.cs:43`, `Endpoints/HabitacionesEndpoints.cs`.

**`Modelos/Habitacion.cs`**

| Línea | Define |
|---|---|
| 4 | `enum TipoHabitacion` — `SENCILLA`, `DOBLE`, `SUITE` |
| 15 | `class TransicionInvalidaException : InvalidOperationException` |
| 17 | ctor `TransicionInvalidaException(string mensaje)` |
| 26 | `class Habitacion` |
| 28 | ctor privado `Habitacion()` — para materialización de EF |
| 64 | `List<Guid> OrdenesActivas` |
| 66 | `static Habitacion Crear(int numero, int piso, TipoHabitacion tipo, EstadoHabitacion estado)` |
| 95 | `bool MarcarFueraDeServicio(Guid ordenId)` |
| 123 | `bool Liberar(Guid ordenId)` |

Importa: `Habitaciones.Modelos` (mismo namespace).
Lo usa: `Datos/HabitacionesDbContext.cs`, `Datos/SembradorHabitaciones.cs`, `Endpoints/HabitacionesEndpoints.cs`.

**`Datos/HabitacionesDbContext.cs`**

| Línea | Define |
|---|---|
| 14 | `class HabitacionesDbContext : DbContext` |
| 16 | ctor `HabitacionesDbContext(DbContextOptions<HabitacionesDbContext>)` |
| 21 | `DbSet<Habitacion> Habitaciones` |
| 25 | `override OnModelCreating(ModelBuilder)` |

Importa: `Microsoft.EntityFrameworkCore`, `Habitaciones.Modelos`.
Lo usa: `Program.cs:27`, `Endpoints/HabitacionesEndpoints.cs`, `Datos/SembradorHabitaciones.cs`.

**`Datos/SembradorHabitaciones.cs`**

| Línea | Define |
|---|---|
| 7 | `sealed class OpcionesInventario` — `Total=400`, `Pisos=10`, `Seccion="Habitaciones"` |
| 20 | `static class SembradorHabitaciones` |
| 26 | `static async Task SembrarAsync(HabitacionesDbContext, OpcionesInventario, ILogger)` |

Importa: `Microsoft.EntityFrameworkCore`, `Habitaciones.Modelos`.
Lo usa: `Program.cs:53`.

**`Endpoints/HabitacionesEndpoints.cs`**

| Línea | Define |
|---|---|
| 12 | `sealed record TransicionPeticion(Guid OrdenId)` |
| 19 | `sealed record HabitacionRespuesta(...)` |
| 28 | `static HabitacionRespuesta De(Habitacion h)` |
| 32 | `static class HabitacionesEndpoints` |
| 34 | `static WebApplication MapHabitacionesEndpoints(this WebApplication)` |
| 56 | `private static async Task<IResult> ListarAsync(...)` |
| 82 | `private static async Task<IResult> ObtenerAsync(...)` |
| 98 | `private static Task<IResult> BloquearAsync(...)` |
| 109 | `private static Task<IResult> LiberarAsync(...)` |
| 125 | `private static async Task<IResult> AplicarTransicionAsync(...)` — tronco común de 98 y 109 |

Importa: `Microsoft.EntityFrameworkCore`, `Habitaciones.Datos`, `Habitaciones.Modelos`.
Lo usa: `Program.cs:64`.

**`Program.cs`** — sin clases; top-level statements.

| Línea | Qué hace |
|---|---|
| 18 | `ConfigureHttpJsonOptions` — camelCase + `JsonStringEnumConverter` |
| 24 | `GetConnectionString("Postgres")`; lanza `InvalidOperationException` si falta |
| 27 | `AddDbContext<HabitacionesDbContext>` con `UseNpgsql` + `EnableRetryOnFailure(3)` |
| 32-33 | `GetSection("Habitaciones").Get<OpcionesInventario>()` |
| 35 | `AddSingleton(opcionesInventario)` |
| 38-39 | `AddHealthChecks().AddNpgSql(...)` |
| 51 | `await bd.Database.MigrateAsync()` |
| 53 | `SembradorHabitaciones.SembrarAsync(...)` |
| 64 | `app.MapHabitacionesEndpoints()` |
| 68 | `app.MapHealthChecks("/salud")` |

**Sin proyecto de pruebas. NO IMPLEMENTADO.**

---

## 1.2 `ordenes` (C# / .NET 8)

| Archivo | Líneas |
|---|---|
| `servicios/ordenes/Program.cs` | 135 |
| `servicios/ordenes/Ordenes.csproj` | 59 |
| `servicios/ordenes/appsettings.json` | 45 |
| `servicios/ordenes/Dockerfile` | 39 |
| `servicios/ordenes/.dockerignore` | 32 |
| `servicios/ordenes/README.md` | 231 |
| `servicios/ordenes/Modelos/EstadoOrden.cs` | 40 |
| `servicios/ordenes/Modelos/TipoFalla.cs` | 29 |
| `servicios/ordenes/Modelos/Orden.cs` | 183 |
| `servicios/ordenes/Modelos/EventoProcesado.cs` | 30 |
| `servicios/ordenes/Datos/OrdenesDbContext.cs` | 88 |
| `servicios/ordenes/Clientes/HabitacionesClient.cs` | 221 |
| `servicios/ordenes/Clientes/OpcionesHabitaciones.cs` | 47 |
| `servicios/ordenes/Eventos/PublicadorEventos.cs` | 200 |
| `servicios/ordenes/Eventos/ConsumidorOrdenAsignada.cs` | 323 |
| `servicios/ordenes/Endpoints/OrdenesEndpoints.cs` | 411 |
| `servicios/ordenes/Pruebas/OrdenPruebas.cs` | 214 |
| `servicios/ordenes/Pruebas/Ordenes.Pruebas.csproj` | 43 |
| `servicios/ordenes/Datos/Migraciones/20260809012400_EsquemaInicial.cs` | 75 |
| `servicios/ordenes/Datos/Migraciones/20260809012400_EsquemaInicial.Designer.cs` | 128 |
| `servicios/ordenes/Datos/Migraciones/OrdenesDbContextModelSnapshot.cs` | 125 |

### Definiciones

**`Modelos/EstadoOrden.cs`**

| Línea | Define |
|---|---|
| 13 | `enum EstadoOrden` — `ABIERTA`, `ASIGNADA`, `RESUELTA` |
| 24 | `static class TransicionesOrden` |
| 26 | `static readonly Dictionary<EstadoOrden, EstadoOrden[]> Permitidas` |
| 38 | `static bool Permite(EstadoOrden desde, EstadoOrden hacia)` |

Tabla de transiciones (líneas 26-36):
- `ABIERTA` → `[ASIGNADA, RESUELTA]`
- `ASIGNADA` → `[RESUELTA]`
- `RESUELTA` → `[]`

Lo usa: `Modelos/Orden.cs`, `Pruebas/OrdenPruebas.cs`.

**`Modelos/TipoFalla.cs`**

| Línea | Define |
|---|---|
| 12 | `enum TipoFalla` — `AIRE_ACONDICIONADO`, `PLOMERIA`, `CERRADURA`, `ELECTRICIDAD` |
| 24 | `enum Prioridad` — `BAJA`, `MEDIA`, `ALTA` |

**`Modelos/Orden.cs`**

| Línea | Define |
|---|---|
| 8 | `class TransicionInvalidaException : InvalidOperationException` |
| 10 | ctor `TransicionInvalidaException(EstadoOrden desde, EstadoOrden hacia)` |
| 31 | `class Orden` |
| 34 | ctor privado `Orden()` |
| 97 | `static Orden Crear(Guid id, Guid habitacionId, int habitacionNumero, TipoFalla, string descripcion, Prioridad, string reportadoPor)` |
| 152 | `void Asignar(Guid tecnicoId, string tecnicoNombre, string especialidad, DateTimeOffset asignadaEn)` |
| 170 | `void Resolver(Guid? resueltoPor, string? notaCierre)` |

Lo usa: `Datos/OrdenesDbContext.cs`, `Endpoints/OrdenesEndpoints.cs`, `Eventos/ConsumidorOrdenAsignada.cs`, `Pruebas/OrdenPruebas.cs`.

**`Modelos/EventoProcesado.cs`**

| Línea | Define |
|---|---|
| 16 | `class EventoProcesado` — `EventoId`, `TipoEvento`, `ProcesadoEn` |
| 24 | `static EventoProcesado Registrar(Guid eventoId, string tipoEvento)` |

Lo usa: `Datos/OrdenesDbContext.cs:70`, `Eventos/ConsumidorOrdenAsignada.cs:251,264,278,290`.

**`Datos/OrdenesDbContext.cs`**

| Línea | Define |
|---|---|
| 14 | `class OrdenesDbContext : DbContext` |
| 16 | ctor `OrdenesDbContext(DbContextOptions<OrdenesDbContext>)` |
| 21 | `DbSet<Orden> Ordenes` |
| 24 | `DbSet<EventoProcesado> EventosProcesados` |

**`Clientes/HabitacionesClient.cs`**

| Línea | Define |
|---|---|
| 9 | `enum ResultadoHabitacion` — `Exito`, `NoExiste`, `TransicionInvalida`, `NoDisponible` |
| 34 | `sealed record RespuestaHabitaciones(ResultadoHabitacion, Guid?, string?)` |
| 48 | `interface IHabitacionesClient` — 2 métodos |
| 57 | `sealed class HabitacionesClient : IHabitacionesClient` |
| 62 | ctor `HabitacionesClient(HttpClient http, ILogger<HabitacionesClient> log)` |
| 68 | `Task<RespuestaHabitaciones> MarcarFueraDeServicioAsync(int, Guid, CancellationToken)` |
| 72 | `Task<RespuestaHabitaciones> LiberarAsync(int, Guid, CancellationToken)` |
| 76 | `private async Task<RespuestaHabitaciones> LlamarAsync(...)` |
| 142 | `private async Task<RespuestaHabitaciones> TraducirAsync(...)` |
| 220 | `private sealed record HabitacionDto(Guid Id, int Numero, string Estado)` |

Importa: `System.Net`, `System.Net.Http.Json`, `Polly.CircuitBreaker`, `Polly.Timeout`.
Lo usa: `Program.cs:42`, `Endpoints/OrdenesEndpoints.cs:107,324`.

**`Clientes/OpcionesHabitaciones.cs`**

| Línea | Define |
|---|---|
| 7 | `sealed class OpcionesHabitaciones` — `Seccion="Habitaciones"`, `UrlBase` |
| 14 | `OpcionesResiliencia Resiliencia` |
| 22 | `sealed class OpcionesResiliencia` — 7 propiedades |

**`Eventos/PublicadorEventos.cs`**

| Línea | Define |
|---|---|
| 10 | `sealed class OpcionesRabbitMq` — `Seccion="RabbitMq"` |
| 38 | `interface IPublicadorEventos` — 1 método |
| 47 | `sealed class PublicadorEventos : IPublicadorEventos, IDisposable` |
| 49 | `static readonly JsonSerializerOptions Json` — camelCase |
| 61 | `SemaphoreSlim _cerrojo = new(1, 1)` |
| 67 | ctor `PublicadorEventos(OpcionesRabbitMq, ILogger<PublicadorEventos>)` |
| 87 | `async Task PublicarAsync(string evento, object cuerpo, CancellationToken)` |
| 134 | `private static JsonObject ArmarSobre(string evento, object cuerpo)` |
| 158 | `private IModel AsegurarCanal()` |
| 188 | `void Dispose()` |

Importa: `System.Text`, `System.Text.Json`, `System.Text.Json.Nodes`, `RabbitMQ.Client`.
Lo usa: `Program.cs:95`, `Endpoints/OrdenesEndpoints.cs:243,396`.

**`Eventos/ConsumidorOrdenAsignada.cs`**

| Línea | Define |
|---|---|
| 21 | `sealed record MensajeOrdenAsignada` — esquema de entrada |
| 43 | `sealed class ConsumidorOrdenAsignada : BackgroundService` |
| 61 | ctor `ConsumidorOrdenAsignada(OpcionesRabbitMq, IServiceScopeFactory, ILogger)` |
| 78 | `override async Task ExecuteAsync(CancellationToken)` |
| 116 | `private void Conectar()` |
| 155 | `private async Task RecibirAsync(object, BasicDeliverEventArgs)` |
| 220 | `private async Task AplicarAsync(MensajeOrdenAsignada)` |
| 298 | `private void Desconectar()` |
| 318 | `override void Dispose()` |

Lo usa: `Program.cs:99`.

**`Endpoints/OrdenesEndpoints.cs`**

| Línea | Define |
|---|---|
| 13 | `sealed record CrearOrdenPeticion(int, TipoFalla, string, Prioridad, string)` |
| 21 | `sealed record ResolverOrdenPeticion(Guid?, string?)` |
| 25 | `sealed record OrdenRespuesta(...)` — 16 campos |
| 43 | `static OrdenRespuesta De(Orden o)` |
| 54 | `sealed record EventoOrdenCreada(...)` — cuerpo de negocio |
| 64 | `sealed record EventoOrdenResuelta(...)` — cuerpo de negocio |
| 70 | `static class OrdenesEndpoints` |
| 72 | `const string EventoCreada = "orden.creada"` |
| 73 | `const string EventoResuelta = "orden.resuelta"` |
| 75 | `static WebApplication MapOrdenesEndpoints(this WebApplication)` |
| 104 | `private static async Task<IResult> CrearAsync(...)` — 171 líneas |
| 274 | `private static async Task<IResult> ListarAsync(...)` |
| 300 | `private static async Task<IResult> ObtenerAsync(...)` |
| 323 | `private static async Task<IResult> ResolverAsync(...)` — 89 líneas |

**`Pruebas/OrdenPruebas.cs`** — 24 casos (`[Fact]` + `[InlineData]`).

---

## 1.3 `tecnicos` (Python 3.12 / FastAPI)

| Archivo | Líneas |
|---|---|
| `servicios/tecnicos/main.py` | 172 |
| `servicios/tecnicos/modelos.py` | 221 |
| `servicios/tecnicos/base_datos.py` | 255 |
| `servicios/tecnicos/asignador.py` | 146 |
| `servicios/tecnicos/consumidor.py` | 269 |
| `servicios/tecnicos/configuracion.py` | 47 |
| `servicios/tecnicos/requirements.txt` | 22 |
| `servicios/tecnicos/requirements-dev.txt` | 12 |
| `servicios/tecnicos/Dockerfile` | 33 |
| `servicios/tecnicos/.dockerignore` | 25 |
| `servicios/tecnicos/README.md` | 209 |
| `servicios/tecnicos/test_asignador.py` | 138 |
| `servicios/tecnicos/test_consumidor.py` | 380 |
| `servicios/tecnicos/test_gestion.py` | 217 |

### Definiciones

**`configuracion.py`**

| Línea | Define |
|---|---|
| 13 | `class Configuracion(BaseSettings)` |
| 17 | `database_url` (defecto `postgresql+psycopg://tecnicos:cambiar@db-tecnicos:5432/tecnicos`) |
| 20 | `rabbitmq_host` (defecto `rabbitmq`) |
| 37 | `hotel_utc_offset: int = -4` |
| 40 | `@property url_amqp` |
| 47 | `configuracion = Configuracion()` — singleton de módulo |

Lo usa: `base_datos.py:18`, `consumidor.py:22`, `main.py:23`.

**`modelos.py`**

| Línea | Define |
|---|---|
| 29 | `class Especialidad(str, Enum)` — 4 valores |
| 44 | `class Turno(str, Enum)` — `MANANA="MAÑANA"`, `TARDE`, `NOCHE` |
| 61 | `class Base(DeclarativeBase)` |
| 65 | `class Tecnico(Base)` — tabla `tecnicos` |
| 84 | `class Asignacion(Base)` — tabla `asignaciones` |
| 101 | `class EventoProcesado(Base)` — tabla `eventos_procesados` |
| 122 | `def a_camel(texto)` |
| 127 | `class EsquemaEvento(BaseModel)` — `alias_generator=a_camel`, `extra="ignore"` |
| 140 | `class EventoOrdenCreada(EsquemaEvento)` — entrada |
| 156 | `class EventoOrdenAsignada(EsquemaEvento)` — salida |
| 179 | `class TecnicoRespuesta(BaseModel)` |
| 189 | `class TecnicoNuevo(BaseModel)` |
| 204 | `class TecnicoCambio(BaseModel)` |
| 213 | `class AsignacionRespuesta(BaseModel)` |

**`asignador.py`**

| Línea | Define |
|---|---|
| 26 | `@dataclass(frozen=True) class CandidatoTecnico` |
| 37 | `@dataclass(frozen=True) class Decision` |
| 50 | `@property hubo_asignacion` |
| 54 | `def especialidad_para(tipo_falla) -> Especialidad \| None` |
| 68 | `def turno_vigente(momento_utc, offset_horas) -> Turno` |
| 87 | `def elegir_tecnico(tipo_falla, momento_utc, candidatos, offset_horas) -> Decision` |

Importa (línea 20): solo `modelos` (`Especialidad`, `Turno`). **No importa SQLAlchemy ni aio-pika.**
Lo usa: `consumidor.py:21`, `base_datos.py:17` (importa `CandidatoTecnico`), `main.py:22` (importa `turno_vigente`).

Turnos (líneas 80-84): `MAÑANA` 06-14, `TARDE` 14-22, `NOCHE` resto.

**`base_datos.py`**

| Línea | Define |
|---|---|
| 24 | `motor = create_engine(...)` con `pool_pre_ping=True`, `pool_size=5`, `max_overflow=5` |
| 32 | `FabricaSesiones = sessionmaker(...)` |
| 35 | `def obtener_sesion()` — dependencia de FastAPI |
| 49 | `def crear_tablas()` |
| 71 | `def sembrar_tecnicos()` — 12 técnicos (`_NOMBRES`, línea 54) |
| 98 | `def listar_tecnicos(sesion, especialidad, turno)` |
| 113 | `def obtener_tecnico(sesion, tecnico_id)` |
| 117 | `def crear_tecnico(sesion, nombre, especialidad, turno, activo)` |
| 132 | `def actualizar_tecnico(sesion, tecnico_id, ...)` |
| 159 | `def contar_asignaciones(sesion, tecnico_id)` |
| 165 | `def listar_asignaciones(sesion)` |
| 171 | `def candidatos_activos(sesion) -> list[CandidatoTecnico]` |
| 205 | `def guardar_asignacion(sesion, orden_id, tecnico_id, habitacion_numero, asignada_en)` |
| 227 | `def evento_ya_procesado(sesion, evento_id) -> bool` |
| 231 | `def registrar_evento(sesion, evento_id, tipo_evento)` |
| 247 | `def base_responde() -> bool` |

**`consumidor.py`**

| Línea | Define |
|---|---|
| 27-28 | `EVENTO_ENTRADA = "orden.creada"`, `EVENTO_SALIDA = "orden.asignada"` |
| 31 | `class ConsumidorOrdenCreada` |
| 34 | `def __init__(self)` — sin parámetros |
| 42 | `def arrancar(self)` — `asyncio.create_task` |
| 46 | `async def detener(self)` |
| 60 | `@property conectado` |
| 65 | `async def _correr(self)` — bucle de reconexión |
| 85 | `async def _conectar(self)` |
| 118 | `async def _recibir(self, mensaje)` |
| 157 | `def _aplicar_sincrono(self, evento) -> EventoOrdenAsignada \| None` |
| 226 | `async def _publicar(self, evento)` |
| 269 | `consumidor = ConsumidorOrdenCreada()` — singleton de módulo |

**`main.py`**

| Línea | Define |
|---|---|
| 41 | `async def ciclo_de_vida(_: FastAPI)` — lifespan |
| 66 | `GET /tecnicos` |
| 75 | `GET /tecnicos/disponibles` |
| 86 | `GET /tecnicos/{tecnico_id}` |
| 98 | `POST /tecnicos` → 201 |
| 121 | `PUT /tecnicos/{tecnico_id}` |
| 149 | `GET /asignaciones` |
| 155 | `GET /salud` |

> Orden importante: `/tecnicos/disponibles` (75) va **antes** de `/tecnicos/{tecnico_id}` (86). Invertirlo haría que el path param capture `disponibles`.

---

## 1.4 `notificaciones` (Python 3.12 / FastAPI)

| Archivo | Líneas |
|---|---|
| `servicios/notificaciones/main.py` | 263 |
| `servicios/notificaciones/consumidor.py` | 265 |
| `servicios/notificaciones/plantillas.py` | 234 |
| `servicios/notificaciones/requirements.txt` | 19 |
| `servicios/notificaciones/Dockerfile` | 31 |
| `servicios/notificaciones/.dockerignore` | 21 |
| `servicios/notificaciones/README.md` | 74 |

**Sin `requirements-dev.txt` y sin pruebas. NO IMPLEMENTADO.**

### Definiciones

**`main.py`**

| Línea | Define |
|---|---|
| 62 | `MAX_AVISOS = 50` |
| 65 | `_lock = threading.Lock()` |
| 66 | `_avisos: OrderedDict[str, dict]` |
| 77 | `_habitacion_por_orden: OrderedDict[str, int]` |
| 80 | `MAX_ORDENES_RECORDADAS = 500` |
| 83 | `def existe_evento_id(evento_id) -> bool` |
| 89 | `def agregar_aviso(aviso)` |
| 107 | `def recordar_habitacion(orden_id, numero_habitacion)` |
| 117 | `def habitacion_de_orden(orden_id) -> Optional[int]` |
| 123 | `def _listar_avisos()` |
| 129 | `def _obtener_aviso(aviso_id)` |
| 134 | `def _marcar_leida(aviso_id)` |
| 148 | `async def lifespan(app)` |
| 201 | `GET /notificaciones` |
| 221 | `GET /notificaciones/{aviso_id}` |
| 230 | `POST /notificaciones/{aviso_id}/leida` |
| 239 | `GET /salud` |

**`consumidor.py`**

| Línea | Define |
|---|---|
| 53-57 | `from main import agregar_aviso, existe_evento_id, habitacion_de_orden, recordar_habitacion` |
| 63 | `EXCHANGE_NOMBRE = os.environ.get("EXCHANGE", "hotel.eventos")` |
| 64 | `COLA_NOMBRE = "notificaciones.eventos"` |
| 65 | `ROUTING_KEY_COMODIN = "orden.*"` |
| 68-69 | `ESPERA_INICIAL_SEGUNDOS = 1`, `ESPERA_MAXIMA_SEGUNDOS = 30` |
| 71 | `PREFETCH_COUNT = 10` |
| 76 | `_conexion` — global |
| 79 | `def conexion_activa() -> bool` |
| 84 | `def _url_rabbitmq() -> str` |
| 108 | `async def _conectar_con_reintentos()` |
| 128 | `def _procesar_evento(cuerpo_bytes)` |
| 187 | `async def _on_mensaje(mensaje)` |
| 208 | `async def iniciar_consumidor()` |

> **Dependencia circular:** `consumidor.py:53` importa de `main.py`. Se rompe con imports diferidos en `main.py:157` y `main.py:256`.

**`plantillas.py`**

| Línea | Define |
|---|---|
| 43 | `DESTINATARIO_RECEPCION = "recepcion"` |
| 47 | `_NIVELES_POR_PRIORIDAD` |
| 57 | `_NIVEL_INFORMATIVO` |
| 61 | `_ESPECIALIDADES_LEGIBLES` — 4 valores |
| 72 | `HABITACION_DESCONOCIDA = 0` |
| 75 | `def _texto_habitacion(numero_habitacion)` |
| 82 | `def _construir_aviso(...)` |
| 107 | `def aviso_orden_creada(evento, numero_habitacion)` |
| 135 | `def aviso_orden_asignada(evento, numero_habitacion)` |
| 167 | `def aviso_orden_resuelta(evento, numero_habitacion)` |
| 203 | `def aviso_por_defecto(evento, numero_habitacion=HABITACION_DESCONOCIDA)` |
| 231 | `PLANTILLAS_POR_TIPO_EVENTO` — tabla de despacho |

---

## 1.5 `gateway` (C# / .NET 8 + YARP)

| Archivo | Líneas |
|---|---|
| `servicios/gateway/Program.cs` | 148 |
| `servicios/gateway/appsettings.json` | 185 |
| `servicios/gateway/Gateway.csproj` | 41 |
| `servicios/gateway/Dockerfile` | 39 |
| `servicios/gateway/.dockerignore` | 33 |
| `servicios/gateway/README.md` | 66 |

**`Program.cs`** — sin clases.

| Línea | Qué hace |
|---|---|
| 41 | `AddCors` — `AllowAnyOrigin/Header/Method` |
| 51 | `AddHealthChecks()` |
| 54 | `AddHttpClient("salud")` con `Timeout = 3 s` |
| 61-62 | `AddReverseProxy().LoadFromConfig(GetSection("ReverseProxy"))` |
| 66 | `app.UseCors()` |
| 70 | `app.Use(...)` — middleware de logging de reenvíos |
| 74 | `contexto.Features.Get<IReverseProxyFeature>()` |
| 89 | `app.MapHealthChecks("/health")` |
| 99 | `app.MapGet("/salud", ...)` — agregado |
| 104 | `GetSection("ReverseProxy:Clusters").GetChildren()` |
| 145 | `app.MapReverseProxy()` |

Única dependencia NuGet: `Yarp.ReverseProxy 2.1.0`.

**Sin pruebas. NO IMPLEMENTADO.**

---

## 1.6 `ui` (React 18 + Vite, servida por nginx)

| Archivo | Líneas |
|---|---|
| `servicios/ui/src/api.js` | 159 |
| `servicios/ui/src/App.jsx` | 127 |
| `servicios/ui/src/main.jsx` | 16 |
| `servicios/ui/src/estilos.css` | 408 |
| `servicios/ui/src/componentes/Ingreso.jsx` | 54 |
| `servicios/ui/src/componentes/NuevaOrden.jsx` | 174 |
| `servicios/ui/src/componentes/ListaOrdenes.jsx` | 176 |
| `servicios/ui/src/componentes/Habitaciones.jsx` | 139 |
| `servicios/ui/src/componentes/BandejaNotificaciones.jsx` | 116 |
| `servicios/ui/src/componentes/PanelSalud.jsx` | 126 |
| `servicios/ui/nginx.conf` | 51 |
| `servicios/ui/proxy-comun.inc` | 14 |
| `servicios/ui/vite.config.js` | 42 |
| `servicios/ui/index.html` | 14 |
| `servicios/ui/package.json` | 19 |
| `servicios/ui/package-lock.json` | 1690 |
| `servicios/ui/Dockerfile` | 33 |
| `servicios/ui/.dockerignore` | 30 |
| `servicios/ui/README.md` | 139 |

### Definiciones

**`src/api.js`**

| Línea | Define |
|---|---|
| 10 | `const BASE = import.meta.env.VITE_API_BASE ?? ''` |
| 12 | `TIEMPO_ESPERA_MS = 15000` |
| 15 | `export class ErrorApi extends Error` |
| 27 | `function mensajeDelCuerpo(cuerpo, respaldo)` |
| 41 | `async function pedir(ruta, opciones)` |
| 109 | `function conParametros(ruta, parametros)` |
| 120 | `listarHabitaciones` |
| 123 | `obtenerHabitacion` |
| 127 | `crearOrden` — `POST /ordenes` |
| 130 | `listarOrdenes` |
| 132 | `obtenerOrden` |
| 134 | `resolverOrden` — `PUT /ordenes/{id}/resolver` |
| 142 | `listarTecnicos` |
| 144 | `listarTecnicosDisponibles` |
| 146 | `listarAsignaciones` |
| 150 | `listarNotificaciones` |
| 153 | `marcarNotificacionLeida` |
| 159 | `obtenerSalud` |

Manejo de códigos en `pedir` (líneas 80-107): `400`, `404`, `409`, `503`, y `default`.
**`422` NO tiene rama propia** — cae en `default`.

**`src/App.jsx`**

| Línea | Define |
|---|---|
| 20 | `SECCIONES` — 5 entradas |
| 31 | `CLAVE_USUARIO = 'mantenimiento.usuario'` |
| 37 | `class Barrera extends Component` — error boundary |
| 62 | `export default function App()` |

**Componentes y qué importan de `api.js`:**

| Componente | Importa |
|---|---|
| `NuevaOrden.jsx:10` | `crearOrden`, `ErrorApi` |
| `ListaOrdenes.jsx:10` | `listarOrdenes`, `resolverOrden` |
| `Habitaciones.jsx:10` | `listarHabitaciones` |
| `BandejaNotificaciones.jsx:8` | `listarNotificaciones`, `marcarNotificacionLeida` |
| `PanelSalud.jsx:11` | `obtenerSalud` |
| `Ingreso.jsx` | nada de `api.js` |

---

# PARTE 2 — Constructores e inyección de dependencias

## 2.1 `ordenes`

| Clase | ctor | Recibe | Registro | Ciclo de vida |
|---|---|---|---|---|
| `OrdenesDbContext` | `Datos/OrdenesDbContext.cs:16` | `DbContextOptions<OrdenesDbContext>` | `Program.cs:29` `AddDbContext` | **Scoped** (por defecto de `AddDbContext`) |
| `HabitacionesClient` | `Clientes/HabitacionesClient.cs:62` | `HttpClient`, `ILogger<HabitacionesClient>` | `Program.cs:42` `AddHttpClient<IHabitacionesClient, HabitacionesClient>` | **Transient** (por defecto de `AddHttpClient`); el `HttpMessageHandler` subyacente se agrupa |
| `PublicadorEventos` | `Eventos/PublicadorEventos.cs:67` | `OpcionesRabbitMq`, `ILogger<PublicadorEventos>` | `Program.cs:95` `AddSingleton<IPublicadorEventos, PublicadorEventos>` | **Singleton** — mantiene conexión y canal de RabbitMQ; recrearlo por petición abriría una conexión nueva cada vez |
| `ConsumidorOrdenAsignada` | `Eventos/ConsumidorOrdenAsignada.cs:61` | `OpcionesRabbitMq`, `IServiceScopeFactory`, `ILogger` | `Program.cs:99` `AddHostedService` | **Singleton** — es un `BackgroundService`; usa `IServiceScopeFactory` (línea 222) para abrir un scope y obtener el `DbContext`, porque un singleton no puede recibir un scoped |
| `OpcionesHabitaciones` | sin ctor (POCO) | — | `Program.cs:37` `AddSingleton(opcionesHabitaciones)` | **Singleton** (instancia concreta) |
| `OpcionesRabbitMq` | sin ctor (POCO) | — | `Program.cs:94` `AddSingleton(opcionesRabbit)` | **Singleton** (instancia concreta) |

## 2.2 `habitaciones`

| Clase | ctor | Recibe | Registro | Ciclo de vida |
|---|---|---|---|---|
| `HabitacionesDbContext` | `Datos/HabitacionesDbContext.cs:16` | `DbContextOptions<HabitacionesDbContext>` | `Program.cs:27` | **Scoped** |
| `OpcionesInventario` | sin ctor (POCO) | — | `Program.cs:35` `AddSingleton` | **Singleton** |
| `Habitacion` | `Modelos/Habitacion.cs:28` (privado) | — | no está en el contenedor | materializado por EF; se crea con `Crear` (línea 66) |
| `TransicionInvalidaException` | `Modelos/Habitacion.cs:17` | `string mensaje` | — | — |

## 2.3 `ordenes` — modelos fuera del contenedor

| Clase | ctor | Nota |
|---|---|---|
| `Orden` | `Modelos/Orden.cs:34` (privado) | se crea con `Crear` (línea 97) |
| `TransicionInvalidaException` | `Modelos/Orden.cs:10` | `(EstadoOrden desde, EstadoOrden hacia)` |

## 2.4 `gateway`

No hay clases propias. Servicios registrados:

| Registro | Línea | Ciclo de vida |
|---|---|---|
| CORS | `Program.cs:41` | — |
| Health checks | `Program.cs:51` | — |
| `IHttpClientFactory` nombrado `"salud"` | `Program.cs:54` | fábrica singleton, clientes transient |
| YARP | `Program.cs:61` | singleton interno de YARP |

## 2.5 Python — no hay contenedor de DI

`tecnicos` y `notificaciones` no usan un contenedor. El acoplamiento es por módulo:

| Objeto | Dónde se crea | Equivalente |
|---|---|---|
| `configuracion` | `configuracion.py:47` | singleton de módulo |
| `motor` (SQLAlchemy) | `base_datos.py:24` | singleton de módulo |
| `FabricaSesiones` | `base_datos.py:32` | singleton de módulo |
| `consumidor` | `consumidor.py:269` | singleton de módulo |
| Sesión por petición | `base_datos.py:35` `obtener_sesion()` | **scoped**, vía `Depends()` en `main.py:66,75,86,98,121,149` |
| `_avisos`, `_habitacion_por_orden` | `notificaciones/main.py:66,77` | singletons de módulo, protegidos con `threading.Lock` (línea 65) |

---

# PARTE 3 — Cadena de llamadas

## 3.1 Crear una orden

**1. UI — formulario**
`servicios/ui/src/componentes/NuevaOrden.jsx:44` `async function enviar(evento)`
- `:45` `evento.preventDefault()`
- Valida formato: `:52` habitación 1..400, `:57` descripción no vacía
- `:63` llama `crearOrden({...})` con `habitacionNumero`, `tipoFalla`, `descripcion`, `prioridad`, `reportadoPor`

**2. `api.js`**
`servicios/ui/src/api.js:127` `crearOrden` → `pedir('/ordenes', { method: 'POST', body })`
- `:41` `pedir()` → `:47` `fetch(BASE + ruta)` con `BASE = ''` (`:10`), o sea **URL relativa** `/ordenes`
- `:43-44` `AbortController` con timeout 15 000 ms

**3. nginx (en producción)**
`servicios/ui/nginx.conf:29` `location /ordenes { proxy_pass http://gateway:8080; }`
- Cabeceras en `servicios/ui/proxy-comun.inc:4-8`
- En desarrollo el equivalente es `servicios/ui/vite.config.js:33` (`server.proxy`)

**4. gateway**
`servicios/gateway/appsettings.json:62` `"route-ordenes"` → `:65` `"Path": "/ordenes/{**catch-all}"` → cluster `ordenes`
- Cluster en `:127-128` `"Address": "http://ordenes:8080"`
- **Sin `Transforms`**: la ruta pasa tal cual
- (La variante `/api/ordenes` está en `:53-59` y sí lleva `PathRemovePrefix: /api`)
- Middleware de log: `servicios/gateway/Program.cs:70-87`

**5. `ordenes` — handler**
`servicios/ordenes/Endpoints/OrdenesEndpoints.cs:104` `CrearAsync`
- Parámetros inyectados: `CrearOrdenPeticion`, `OrdenesDbContext`, `IHabitacionesClient`, `IPublicadorEventos`, `ILoggerFactory`, `CancellationToken`
- `:114-146` validación → `Results.ValidationProblem` (400)
- `:149` `var ordenId = Guid.NewGuid()` — **se genera antes de llamar a habitaciones**

**6. `HabitacionesClient`**
`OrdenesEndpoints.cs:154` → `HabitacionesClient.cs:68` `MarcarFueraDeServicioAsync`
- `:70` delega en `LlamarAsync(..., "fuera-de-servicio", ...)`
- `:79` ruta `habitaciones/{numeroHabitacion}/fuera-de-servicio`
- `:83` `PutAsJsonAsync(ruta, new { ordenId })`
- Políticas aplicadas por el handler registrado en `Program.cs:49-87`:
  - `:58` retry — `MaxRetryAttempts=3`, `Delay=200 ms`, `Exponential`, `UseJitter=true`
  - `:75` circuit breaker — `FailureRatio=0.5`, `MinimumThroughput=8`, `SamplingDuration=30 s`, `BreakDuration=30 s`
  - `:86` timeout por intento `3 s`
  - Orden efectivo: **retry → breaker → timeout**
- Captura: `:91` `BrokenCircuitException`, `:103` `TimeoutRejectedException`, `:114` `HttpRequestException`, `:126` `OperationCanceledException`

**7. `habitaciones` — handler**
`servicios/habitaciones/Endpoints/HabitacionesEndpoints.cs:98` `BloquearAsync`
- `:100` delega en `AplicarTransicionAsync(..., aplicar: (h, ordenId) => h.MarcarFueraDeServicio(ordenId), accion: "bloquear")`
- `:125` `AplicarTransicionAsync`:

| Condición | Línea | Respuesta |
|---|---|---|
| `OrdenId == Guid.Empty` | 136 | `400` ValidationProblem |
| habitación no existe | 146 | `404` |
| `TransicionInvalidaException` | 160 | `409` |
| `cambio == false` (reintento) | 173 | `200` con la habitación |
| `DbUpdateConcurrencyException` | 190 | `409` |
| éxito | 197 | `200` con la habitación |

- Lógica de dominio: `Modelos/Habitacion.cs:95` `MarcarFueraDeServicio`
  - `:105` si `OrdenesActivas.Contains(ordenId)` → `return false`
  - `:114-116` agrega el id, pone `FUERA_DE_SERVICIO`, actualiza fecha

**8. Traducción de la respuesta**
`HabitacionesClient.cs:142` `TraducirAsync`
- `:148` `IsSuccessStatusCode` → lee `HabitacionDto` (`:220`) y devuelve `Exito` + `HabitacionId`
- `:170` `404` → `NoExiste`
- `:180` `409` → `TransicionInvalida`
- `:193` `400` → `TransicionInvalida`
- `:202` resto → `NoDisponible`

**9. Vuelta a `ordenes`**
`OrdenesEndpoints.cs:157-179` `switch (bloqueo.Resultado)`

| Resultado | Línea | Respuesta al usuario |
|---|---|---|
| `NoExiste` | 160 | `404` |
| `TransicionInvalida` | 166 | `409` |
| `NoDisponible` | 175 | `503` — "La orden no se creó" |

- `:181` si `bloqueo.HabitacionId` es null → `502` (línea 187)

**10. Persistencia**
- `:201` `Orden.Crear(ordenId, habitacionId, ...)` — `Modelos/Orden.cs:97`
- `:210` `bd.Ordenes.Add(orden)`
- `:211` `await bd.SaveChangesAsync(ct)`
- **Si falla:** `:223` compensa con `habitaciones.LiberarAsync(...)`; si la compensación también falla, `:229` `LogCritical`; `:234` devuelve `500`

**11. Publicación**
`:243` `publicador.PublicarAsync(EventoCreada, new EventoOrdenCreada(...))`
- `EventoCreada = "orden.creada"` (`:72`)
- `Eventos/PublicadorEventos.cs:87` `PublicarAsync`
  - `:89` `ArmarSobre(evento, cuerpo)` → `:134`
    - `:138` `eventoId` = `Guid.NewGuid()`
    - `:139` `tipoEvento` = el nombre del evento
    - `:140` `version` = 1
    - `:142` `ocurridoEn` = `yyyy-MM-dd'T'HH:mm:ss.fff'Z'`
    - `:147-151` mezcla los campos de negocio (camelCase por `Json` en `:49`)
  - `:158` `AsegurarCanal()` → `:173` `ExchangeDeclare(hotel.eventos, topic, durable: true)`, `:179` `ConfirmSelect()`
  - `:102` `propiedades.Persistent = true`
  - `:108` `BasicPublish(exchange, routingKey: evento, ...)`
  - `:117` `WaitForConfirmsOrDie(10 s)`
- **Si falla:** `:262` `LogCritical`; la orden ya está guardada y se responde `201` igual

**12. Respuesta al usuario**
`:271` `Results.Created($"/ordenes/{orden.Id}", OrdenRespuesta.De(orden))` → `201`

**13. RabbitMQ**
Exchange `hotel.eventos` (topic), routing key `orden.creada`.

**14. `tecnicos` — consumidor**
`servicios/tecnicos/consumidor.py:118` `_recibir`
- Cola: `configuracion.cola_orden_creada` = `"tecnicos.orden-creada"` (`configuracion.py:31`)
- Binding: `:105` `cola.bind(exchange, routing_key="orden.creada")`
- `:94` `set_qos(prefetch_count=1)`
- `:120` `EventoOrdenCreada.model_validate_json(mensaje.body)`; si falla → `:125` `nack(requeue=False)`
- `:129` `asyncio.to_thread(self._aplicar_sincrono, evento)`

**15. `_aplicar_sincrono`**
`consumidor.py:157`
- `:174` `evento_ya_procesado(sesion, evento.evento_id)` → si sí, `return None` (idempotencia)
- `:180` `candidatos_activos(sesion)` → `base_datos.py:171`
- `:182` `elegir_tecnico(...)`

**16. `asignador.elegir_tecnico`**
`servicios/tecnicos/asignador.py:87`
- `:99` `especialidad_para(tipo_falla)`; si `None` → `:104` `Decision(None, motivo)`
- `:108` `turno_vigente(momento_utc, offset_horas)` → `:68`
- `:113-119` filtra por especialidad y turno
- `:121` si no hay elegibles → `Decision(None, motivo, descartados)`
- `:132` `elegibles.sort(key=lambda t: (t.ordenes_abiertas, t.nombre))` — **desempate por carga, luego por nombre**
- `:133` `elegido = elegibles[0]`

**17. Persistencia + publicación en `tecnicos`**
- `:191` si `tecnico is None` → `:201` `registrar_evento`, `:202` commit, `return None` (**no publica**)
- `:208` `guardar_asignacion(...)`
- `:215` `registrar_evento(...)` — **misma transacción**
- `:216` `sesion.commit()`
- `:218` devuelve `EventoOrdenAsignada`
- `consumidor.py:130` `mensaje.ack()` — **después** de guardar
- `:155` `self._publicar(por_publicar)` → `:226`
  - `:237` `model_dump(by_alias=True, mode="json")` — **camelCase**
  - `:239` `exchange.publish(..., routing_key="orden.asignada")`
  - `:244` `delivery_mode=PERSISTENT`

**18. `ordenes` — `ConsumidorOrdenAsignada`**
`servicios/ordenes/Eventos/ConsumidorOrdenAsignada.cs:155` `RecibirAsync`
- Cola `ordenes.orden-asignada` (`Eventos/PublicadorEventos.cs:24`), binding `orden.asignada` (`:138`)
- `:142` `BasicQos(prefetchCount: 1)`
- `:220` `AplicarAsync`:
  - `:228` comprueba `EventosProcesados` → idempotencia
  - `:239` si la orden no existe → registra el evento y sale
  - `:257` si la orden está `RESUELTA` → registra y sale
  - `:271` si ya está `ASIGNADA` → registra y sale (no reasigna)
  - `:285` `orden.Asignar(tecnicoId, tecnicoNombre, especialidad, ocurridoEn)`
  - `:290` `EventosProcesados.Add(...)`
  - `:291` `SaveChangesAsync()` — **misma transacción**
- `:192` `BasicAck` solo tras guardar
- Fallo: `:199` si `entrega.Redelivered` → `:206` `nack(requeue: false)`; si no → `:215` `nack(requeue: true)`

**19. `notificaciones` — consumidor**
`servicios/notificaciones/consumidor.py:128` `_procesar_evento`
- Cola `notificaciones.eventos` (`:64`), binding `orden.*` (`:65`)
- `:146` `existe_evento_id(evento_id)` → idempotencia
- `:162` si `tipo_evento == "orden.creada"` → `:165` `recordar_habitacion(orden_id, numero)`
- `:167` si no → `habitacion_de_orden(orden_id)`
- `:169` `PLANTILLAS_POR_TIPO_EVENTO.get(tipo_evento)` → `plantillas.py:231`
  - `orden.creada` → `plantillas.py:107`
  - `orden.asignada` → `plantillas.py:135`
  - `orden.resuelta` → `plantillas.py:167`
  - desconocido → `:178` `aviso_por_defecto` (`plantillas.py:203`)
- `:183` `agregar_aviso(aviso)` → `main.py:89`
- `:197` `mensaje.ack()`

## 3.2 Resolver una orden

**1. UI** — `servicios/ui/src/componentes/ListaOrdenes.jsx:76` `confirmarResolver(id)` → `:78` `resolverOrden(id, nota)`
**2. `api.js`** — `:134` `PUT /ordenes/{id}/resolver`, cuerpo `{ notaCierre }`
**3. gateway** — misma ruta `route-ordenes` (`appsettings.json:62`)
**4. `ordenes`** — `OrdenesEndpoints.cs:323` `ResolverAsync`
- `:330` carga la orden; `:334` `404` si no existe
- `:340` si no está `RESUELTA`: `:344` `orden.Resolver(...)` (`Modelos/Orden.cs:170`), `:348` `SaveChangesAsync`
- `:352` `TransicionInvalidaException` → `409`; `:360` `DbUpdateConcurrencyException` → `409`
- `:368` `habitaciones.LiberarAsync(orden.HabitacionNumero, orden.Id, ct)` — **después** de persistir
- `:370` si `NoDisponible` → `503` (la orden queda `RESUELTA`)
- `:396` `PublicarAsync(EventoResuelta, new EventoOrdenResuelta(...))`
- `:409` `Results.Ok(...)`

**5. `habitaciones`** — `HabitacionesEndpoints.cs:109` `LiberarAsync` → `Modelos/Habitacion.cs:123` `Liberar`
- `:132` `OrdenesActivas.Remove(ordenId)`
- `:141` si quedan órdenes → sigue `FUERA_DE_SERVICIO`
- `:150` si la lista queda vacía → `DISPONIBLE`

**6. `notificaciones`** — `plantillas.py:167` `aviso_orden_resuelta`

`tecnicos` **no consume** `orden.resuelta`.

---

# PARTE 4 — Datos

## 4.1 `ordenes` — base `ordenes`

Migración: `servicios/ordenes/Datos/Migraciones/20260809012400_EsquemaInicial.cs`

**Tabla `ordenes`** (línea 15)

| Columna | Tipo | Null |
|---|---|---|
| `Id` | `uuid` (PK) | no |
| `HabitacionId` | `uuid` | no |
| `HabitacionNumero` | `integer` | no |
| `TipoFalla` | `varchar(40)` | no |
| `Descripcion` | `varchar(1000)` | no |
| `Prioridad` | `varchar(10)` | no |
| `ReportadoPor` | `varchar(120)` | no |
| `Estado` | `varchar(10)` | no |
| `CreadaEn` | `timestamptz` | no |
| `AsignadaEn` | `timestamptz` | sí |
| `ResueltaEn` | `timestamptz` | sí |
| `TecnicoId` | `uuid` | sí |
| `TecnicoNombre` | `varchar(120)` | sí |
| `Especialidad` | `varchar(40)` | sí |
| `ResueltoPor` | `uuid` | sí |
| `NotaCierre` | `varchar(1000)` | sí |
| `xmin` | `xid` (rowVersion) | no |

Índices: `IX_ordenes_Estado` (41), `IX_ordenes_HabitacionNumero` (46).
Configuración: `Datos/OrdenesDbContext.cs:27-72`. Concurrencia optimista en `:68-71`.

**Tabla `eventos_procesados`** (línea 2)

| Columna | Tipo |
|---|---|
| `EventoId` | `uuid` (PK) |
| `TipoEvento` | `varchar(60)` |
| `ProcesadoEn` | `timestamptz` |

## 4.2 `habitaciones` — base `habitaciones`

Migración: `servicios/habitaciones/Datos/Migraciones/20260809204648_EsquemaInicial.cs`

**Tabla `habitaciones`** (línea 16)

| Columna | Tipo | Null |
|---|---|---|
| `Id` | `uuid` (PK) | no |
| `Numero` | `integer` | no |
| `Piso` | `integer` | no |
| `Tipo` | `varchar(20)` | no |
| `Estado` | `varchar(20)` | no |
| `ActualizadaEn` | `timestamptz` | no |
| `OrdenesActivas` | `uuid[]` | no |
| `xmin` | `xid` (rowVersion) | no |

Índices: `IX_habitaciones_Estado` (33), `IX_habitaciones_Numero` **UNIQUE** (38-42), `IX_habitaciones_Piso` (44).
Configuración: `Datos/HabitacionesDbContext.cs:25-70`.

## 4.3 `tecnicos` — base `tecnicos`

Sin migraciones: las tablas se crean con `Base.metadata.create_all` en `base_datos.py:50`.

**Tabla `tecnicos`** — `modelos.py:65`

| Columna | Tipo | Línea |
|---|---|---|
| `id` | `uuid` (PK) | 68 |
| `nombre` | `String(120)` | 69 |
| `especialidad` | `String(40)` | 73 |
| `turno` | `String(10)` | 74 |
| `activo` | `Boolean` | 76 |

Índice: `ix_tecnicos_especialidad_turno_activo` (línea 80).

**Tabla `asignaciones`** — `modelos.py:84`

| Columna | Tipo | Línea |
|---|---|---|
| `orden_id` | `uuid` **(PK)** | 89 |
| `tecnico_id` | `uuid` FK → `tecnicos.id` | 90 |
| `habitacion_numero` | `Integer` | 93 |
| `asignada_en` | `DateTime(timezone=True)` | 94 |

Índice: `ix_asignaciones_tecnico` (línea 98).

**Tabla `eventos_procesados`** — `modelos.py:101`

| Columna | Tipo | Línea |
|---|---|---|
| `evento_id` | `uuid` (PK) | 113 |
| `tipo_evento` | `String(60)` | 114 |
| `procesado_en` | `DateTime(timezone=True)` | 115 |

## 4.4 `notificaciones`

**Sin base de datos.** Estado en memoria:

| Estructura | Archivo:línea | Límite |
|---|---|---|
| `_avisos` | `main.py:66` | `MAX_AVISOS = 50` (`:62`) |
| `_habitacion_por_orden` | `main.py:77` | `MAX_ORDENES_RECORDADAS = 500` (`:80`) |

## 4.5 Campos DUPLICADOS entre servicios

| Campo | Dueño | Copia en | Dónde se copia |
|---|---|---|---|
| `TecnicoNombre` | `tecnicos` (`modelos.py:69` `nombre`) | `ordenes.ordenes.TecnicoNombre` | `ConsumidorOrdenAsignada.cs:285` `orden.Asignar(...)` → `Modelos/Orden.cs:161` |
| `Especialidad` | `tecnicos` (`modelos.py:73`) | `ordenes.ordenes.Especialidad` | `ConsumidorOrdenAsignada.cs:285` → `Modelos/Orden.cs:162` |
| `HabitacionNumero` | `habitaciones` (`Habitacion.cs:38` `Numero`) | `ordenes.ordenes.HabitacionNumero` | `OrdenesEndpoints.cs:203` (viene de la petición) |
| `HabitacionId` | `habitaciones` (`Habitacion.cs:26` `Id`) | `ordenes.ordenes.HabitacionId` | `OrdenesEndpoints.cs:181` (viene de la respuesta HTTP) |
| `habitacion_numero` | `habitaciones` | `tecnicos.asignaciones.habitacion_numero` | `consumidor.py:212` `habitacion_numero=evento.habitacion_numero` |
| `numeroHabitacion` | `habitaciones` | `notificaciones._avisos[].numeroHabitacion` | `plantillas.py:96` `_construir_aviso` |
| `OrdenesActivas` (ids de orden) | `ordenes` (`ordenes.Id`) | `habitaciones.habitaciones.OrdenesActivas` | `Habitacion.cs:114` `OrdenesActivas.Add(ordenId)` |

---

# PARTE 5 — Contratos y eventos

## 5.1 `orden.creada`

| | |
|---|---|
| Contrato | `contratos/orden.creada.v1.json` |
| Construye (C#) | `servicios/ordenes/Endpoints/OrdenesEndpoints.cs:54` `record EventoOrdenCreada` |
| Sobre añadido por | `servicios/ordenes/Eventos/PublicadorEventos.cs:134` `ArmarSobre` |
| Deserializa (Python, `tecnicos`) | `servicios/tecnicos/modelos.py:140` `class EventoOrdenCreada` |
| Deserializa (Python, `notificaciones`) | `servicios/notificaciones/consumidor.py:139` `json.loads` — acceso por diccionario |

| Campo contrato | C# origen | Python `tecnicos` | Coincide |
|---|---|---|---|
| `eventoId` | `PublicadorEventos.cs:138` | `modelos.py:146` `evento_id` (alias `eventoId`) | ✅ |
| `tipoEvento` | `PublicadorEventos.cs:139` | `modelos.py:147` `tipo_evento` | ✅ |
| `version` | `PublicadorEventos.cs:140` | NO DECLARADO (`extra="ignore"`) | ✅ tolerado |
| `ocurridoEn` | `PublicadorEventos.cs:142` | `modelos.py:148` `ocurrido_en` | ✅ |
| `ordenId` | `OrdenesEndpoints.cs:55` `OrdenId` | `modelos.py:150` `orden_id` | ✅ |
| `habitacionId` | `OrdenesEndpoints.cs:56` `HabitacionId` | NO DECLARADO | ✅ tolerado |
| `habitacionNumero` | `OrdenesEndpoints.cs:57` `HabitacionNumero` | `modelos.py:151` `habitacion_numero` | ✅ |
| `tipoFalla` | `OrdenesEndpoints.cs:58` `TipoFalla` | `modelos.py:152` `tipo_falla` | ✅ |
| `descripcion` | `OrdenesEndpoints.cs:59` `Descripcion` | NO DECLARADO | ✅ tolerado |
| `prioridad` | `OrdenesEndpoints.cs:60` `Prioridad` | `modelos.py:153` `prioridad` | ✅ |
| `reportadoPor` | `OrdenesEndpoints.cs:61` `ReportadoPor` | NO DECLARADO | ✅ tolerado |

Conversión a camelCase: `PublicadorEventos.cs:52` `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.
Lado Python: `modelos.py:129` `alias_generator=a_camel`, `modelos.py:135` `extra="ignore"`.

`notificaciones` accede a: `habitacionNumero` (`consumidor.py:164`), `tipoFalla`, `descripcion`, `prioridad` (`plantillas.py:109-111`), `eventoId`, `tipoEvento`, `ocurridoEn` (`plantillas.py:127-131`).

## 5.2 `orden.asignada`

| | |
|---|---|
| Contrato | `contratos/orden.asignada.v1.json` |
| Construye (Python) | `servicios/tecnicos/modelos.py:156` `class EventoOrdenAsignada` |
| Serializa | `servicios/tecnicos/consumidor.py:237` `model_dump(by_alias=True, mode="json")` |
| Deserializa (C#) | `servicios/ordenes/Eventos/ConsumidorOrdenAsignada.cs:21` `record MensajeOrdenAsignada` |
| Deserializa (Python, `notificaciones`) | `consumidor.py:139` `json.loads` |

| Campo contrato | Python origen | C# destino | Coincide |
|---|---|---|---|
| `eventoId` | `modelos.py:163` `evento_id` | `ConsumidorOrdenAsignada.cs:23` `EventoId` | ✅ |
| `tipoEvento` | `modelos.py:164` `tipo_evento` | `:25` `TipoEvento` | ✅ |
| `version` | `modelos.py:165` `version` | NO DECLARADO | ✅ tolerado |
| `ocurridoEn` | `modelos.py:166` `ocurrido_en` | `:27` `OcurridoEn` | ✅ |
| `ordenId` | `modelos.py:168` `orden_id` | `:29` `OrdenId` | ✅ |
| `tecnicoId` | `modelos.py:169` `tecnico_id` | `:31` `TecnicoId` | ✅ |
| `tecnicoNombre` | `modelos.py:170` `tecnico_nombre` | `:33` `TecnicoNombre` | ✅ |
| `especialidad` | `modelos.py:171` `especialidad` | `:35` `Especialidad` | ✅ |

Deserialización C#: `ConsumidorOrdenAsignada.cs:47-52` — `PropertyNamingPolicy = CamelCase`, `PropertyNameCaseInsensitive = true`.

## 5.3 `orden.resuelta`

| | |
|---|---|
| Contrato | `contratos/orden.resuelta.v1.json` |
| Construye (C#) | `servicios/ordenes/Endpoints/OrdenesEndpoints.cs:64` `record EventoOrdenResuelta` |
| Deserializa | solo `notificaciones` — `consumidor.py:139`, campos leídos en `plantillas.py:167-201` |

| Campo contrato | C# origen | Consumido por |
|---|---|---|
| `eventoId` | `PublicadorEventos.cs:138` | `plantillas.py:195` |
| `tipoEvento` | `PublicadorEventos.cs:139` | `plantillas.py:196` |
| `version` | `PublicadorEventos.cs:140` | no se lee |
| `ocurridoEn` | `PublicadorEventos.cs:142` | `plantillas.py:199` |
| `ordenId` | `OrdenesEndpoints.cs:65` | `consumidor.py:161` |
| `habitacionId` | `OrdenesEndpoints.cs:66` | no se lee |
| `resueltoPor` | `OrdenesEndpoints.cs:67` | no se lee |
| `notaCierre` | `OrdenesEndpoints.cs:68` | `plantillas.py:183` |

> `orden.asignada` y `orden.resuelta` **no llevan** `habitacionNumero`. `notificaciones` lo resuelve por correlación: `consumidor.py:162-167` + `main.py:107,117`.

---

# PARTE 6 — Configuración

## 6.1 Variables de entorno

| Variable | `docker-compose.yml` | Se lee en | Si falta |
|---|---|---|---|
| `RABBITMQ_USUARIO` | 24, 128, 149, 166 | `PublicadorEventos.cs:20`; `configuracion.py:22`; `notificaciones/consumidor.py:102` | Defecto `guest` |
| `RABBITMQ_CONTRASENA` | 25, 129, 167 | ídem | Defecto `guest` |
| `RABBITMQ_HOST` | 147, 164 | `configuracion.py:20`; `notificaciones/consumidor.py:95` | `tecnicos`: defecto `rabbitmq`. `notificaciones`: **`RuntimeError`** (`consumidor.py:96`) |
| `RABBITMQ_PUERTO` | 148, 165 | `configuracion.py:21`; `notificaciones/consumidor.py:101` | Defecto `5672` |
| `RABBITMQ_PUERTO_CONSOLA` | 28 | solo compose | — |
| `EXCHANGE` | 130, 151, 168 | `PublicadorEventos.cs:24`; `configuracion.py:29`; `notificaciones/consumidor.py:63` | Defecto `hotel.eventos` |
| `CADENA_HABITACIONES` | 101 | `habitaciones/Program.cs:24` | **`InvalidOperationException`** al arrancar |
| `CADENA_ORDENES` | 114 | `ordenes/Program.cs:26` | **`InvalidOperationException`** al arrancar |
| `URL_TECNICOS` | 146 | `configuracion.py:17` (`database_url`) | Defecto con contraseña `cambiar` |
| `HABITACIONES_URL_BASE` | 117 | `OpcionesHabitaciones.cs:11` vía `Program.cs:34` | Defecto `http://habitaciones:8080` |
| `RESILIENCIA_TIMEOUT_SEGUNDOS` | 118 | `OpcionesHabitaciones.cs:29` | Defecto `3` |
| `RESILIENCIA_REINTENTOS` | 119 | `OpcionesHabitaciones.cs:32` | Defecto `3` |
| `RESILIENCIA_ESPERA_BASE_MS` | 120 | `OpcionesHabitaciones.cs:35` | Defecto `200` |
| `RESILIENCIA_UMBRAL_CIRCUITO` | 121 | `OpcionesHabitaciones.cs:38` | Defecto `0.5` |
| `RESILIENCIA_MINIMO_LLAMADAS` | 122 | `OpcionesHabitaciones.cs:44` | Defecto `8` |
| `RESILIENCIA_VENTANA_SEGUNDOS` | 123 | `OpcionesHabitaciones.cs:41` | Defecto `30` |
| `RESILIENCIA_CIRCUITO_ABIERTO_SEGUNDOS` | 124 | `OpcionesHabitaciones.cs` (`SegundosCircuitoAbierto`) | Defecto `30` |
| `HOTEL_UTC_OFFSET` | 152 | `configuracion.py:37` | Defecto `-4` |
| `DB_*_USUARIO` / `_CONTRASENA` / `_NOMBRE` | 46-48, 62-64, 78-80 | solo los contenedores `postgres` | Postgres no arranca |
| `ASPNETCORE_ENVIRONMENT` | 98, 113, 183 | ASP.NET | Defecto `Production` |
| `NIVEL_LOG` | 102, 132, 184 | ASP.NET | Defecto de `appsettings.json` |
| `GATEWAY_PUERTO` | 187 | solo compose | — |
| `UI_PUERTO` | 198 | solo compose | — |
| `RABBITMQ_URL` | **no está en compose** | `notificaciones/consumidor.py:91` | Se cae al armado por partes |

## 6.2 Puertos

| Servicio | Publicado al host | Interno |
|---|---|---|
| `rabbitmq` | `${RABBITMQ_PUERTO}:5672` (27), `${RABBITMQ_PUERTO_CONSOLA}:15672` (28) | 5672, 15672 |
| `gateway` | `${GATEWAY_PUERTO}:8080` (187) | 8080 |
| `ui` | `${UI_PUERTO}:80` (198) | 80 |
| `habitaciones` | **ninguno** (106) | 8080 |
| `ordenes` | **ninguno** | 8080 |
| `tecnicos` | **ninguno** | 8080 |
| `notificaciones` | **ninguno** | 8080 |
| `db-habitaciones`, `db-ordenes`, `db-tecnicos` | **ninguno** | 5432 |

## 6.3 `depends_on` y healthchecks

| Servicio | `depends_on` | Healthcheck |
|---|---|---|
| `rabbitmq` | — | `rabbitmq-diagnostics -q ping` (32), interval 10 s, retries 10, start_period 20 s |
| `db-habitaciones` | — | `pg_isready` (52) |
| `db-ordenes` | — | `pg_isready` (68) |
| `db-tecnicos` | — | `pg_isready` (84) |
| `habitaciones` | `db-habitaciones: service_healthy` (103-105) | ninguno propio |
| `ordenes` | `db-ordenes` + `rabbitmq`, ambos `service_healthy` (133-137) | ninguno propio |
| `tecnicos` | `db-tecnicos` + `rabbitmq`, ambos `service_healthy` (153-157) | ninguno propio |
| `notificaciones` | `rabbitmq: service_healthy` (169-171) | ninguno propio |
| `gateway` | `habitaciones`, `ordenes`, `tecnicos` (188-192) — **sin condition** | ninguno propio |
| `ui` | `gateway` (199) — sin condition | ninguno propio |

> `ordenes` **no** depende de `habitaciones` (comentario en `docker-compose.yml:138-139`).

---

# PARTE 7 — Resiliencia

| Mecanismo | Archivo:línea | Valor |
|---|---|---|
| Timeout por intento | `ordenes/Program.cs:86` | `TimeSpan.FromSeconds(r.TimeoutSegundos)` = 3 s |
| Reintentos | `ordenes/Program.cs:58-73` | `MaxRetryAttempts=3`, `Delay=200 ms`, `BackoffType=Exponential`, `UseJitter=true` |
| Predicado de reintento | `ordenes/Program.cs:66-72` | `HttpRequestException`, `TimeoutRejectedException`, `>=500`, `408` |
| Circuit breaker | `ordenes/Program.cs:75-85` | `FailureRatio=0.5`, `MinimumThroughput=8`, `SamplingDuration=30 s`, `BreakDuration=30 s` |
| Orden del pipeline | `ordenes/Program.cs:57-86` | retry → breaker → timeout |
| `HttpClient.Timeout` desactivado | `ordenes/Program.cs:47` | `Timeout.InfiniteTimeSpan` |
| Reintento de conexión a Postgres | `ordenes/Program.cs:30`, `habitaciones/Program.cs:28` | `EnableRetryOnFailure(3)` |
| Reconexión RabbitMQ (publicador) | `PublicadorEventos.cs:78-81` | `AutomaticRecoveryEnabled`, `NetworkRecoveryInterval` = `SegundosEsperaReconexion` (5) |
| Reconexión RabbitMQ (consumidor C#) | `ConsumidorOrdenAsignada.cs:80-113` | bucle con `Task.Delay(SegundosEsperaReconexion)` |
| Reconexión RabbitMQ (`tecnicos`) | `consumidor.py:65-83` | bucle con `asyncio.sleep(segundos_espera_reconexion)` |
| Reconexión RabbitMQ (`notificaciones`) | `consumidor.py:108-126` | backoff exponencial 1 s → 30 s (`:68-69`) |
| Confirmación de publicación | `PublicadorEventos.cs:117` | `WaitForConfirmsOrDie(10 s)` |
| Mensajes persistentes | `PublicadorEventos.cs:102`; `tecnicos/consumidor.py:244` | `Persistent` / `DeliveryMode.PERSISTENT` |
| Idempotencia `ordenes` | `ConsumidorOrdenAsignada.cs:228` | consulta `EventosProcesados` |
| Idempotencia `tecnicos` | `consumidor.py:174` | `evento_ya_procesado` |
| Idempotencia `notificaciones` | `consumidor.py:146` | `existe_evento_id` (en memoria) |
| Reintento acotado `ordenes` | `ConsumidorOrdenAsignada.cs:199-216` | `Redelivered` → descarta; si no, reencola |
| Reintento acotado `tecnicos` | `consumidor.py:135-148` | ídem |
| Descarte en `notificaciones` | `consumidor.py:205` | `nack(requeue=False)` siempre |
| Health check `ordenes` | `ordenes/Program.cs:102-107` | Npgsql + RabbitMQ |
| Health check `habitaciones` | `habitaciones/Program.cs:38-39` | Npgsql |
| Health check `tecnicos` | `tecnicos/main.py:155` | `base_responde()` + `consumidor.conectado` |
| Health check `notificaciones` | `notificaciones/main.py:239` | tarea viva + `conexion_activa()` |
| Health checks activos del gateway | `gateway/appsettings.json:113-122` | `Interval 10 s`, `Timeout 5 s`, `ConsecutiveFailures`, `Threshold 3`, `Path /salud` |
| Concurrencia optimista | `OrdenesDbContext.cs:68-71`; `HabitacionesDbContext.cs:64-67` | columna `xmin` |
| Barrera de error en la UI | `ui/src/App.jsx:37` | `class Barrera extends Component` |
| Timeout de la UI | `ui/src/api.js:12,43` | 15 000 ms con `AbortController` |
| Timeout de nginx | `ui/proxy-comun.inc:11-13` | connect 10 s, send 30 s, read 30 s |

## Manejo del fallo de `habitaciones`

| Paso | Archivo:línea |
|---|---|
| Captura de `BrokenCircuitException` | `HabitacionesClient.cs:91` → devuelve `NoDisponible` |
| Captura de `TimeoutRejectedException` | `HabitacionesClient.cs:103` |
| Captura de `HttpRequestException` | `HabitacionesClient.cs:114` |
| Traducción a HTTP | `OrdenesEndpoints.cs:171-179` → **`503`** |
| Texto al usuario | `OrdenesEndpoints.cs:177` — "La orden no se creó; vuelve a intentarlo." |
| Recepción en la UI | `api.js:93-100` → `ErrorApi` con `esCircuitoAbierto = true` |
| Presentación | `NuevaOrden.jsx:143-152` — bloque `mensaje aviso` |

---

# PARTE 8 — Lo que NO existe

## 8.1 Archivos que siguen siendo solo comentario

**NINGUNO.** Todos los archivos del esqueleto están implementados.

Verificado: ningún archivo bajo `servicios/` con extensión `.cs`, `.py`, `.jsx`, `.js`, `.json`, `.conf` o `.yml` tiene menos de 4 líneas de código no comentado.

## 8.2 Funciones declaradas y nunca llamadas

| Función | Archivo:línea | Referencias |
|---|---|---|
| `EstadosHabitacion.AdmiteReservas` | `habitaciones/Modelos/EstadoHabitacion.cs:24` | **1** (solo la definición) |
| `contar_asignaciones` | `tecnicos/base_datos.py:159` | **1** (solo la definición) |
| `obtenerHabitacion` | `ui/src/api.js:123` | **1** (solo la definición) |
| `obtenerOrden` | `ui/src/api.js:132` | **1** (solo la definición) |
| `listarTecnicos` | `ui/src/api.js:142` | **1** (solo la definición) |
| `listarTecnicosDisponibles` | `ui/src/api.js:144` | **1** (solo la definición) |
| `listarAsignaciones` | `ui/src/api.js:146` | **1** (solo la definición) |
| `Decision.descartados` | `tecnicos/asignador.py:47` | se llena (`:115,117,135`) pero ningún consumidor lo lee |

## 8.3 Mecanismos documentados pero NO codificados

| Mecanismo | Dónde se documenta | Estado |
|---|---|---|
| **Patrón Outbox** | `ordenes/README.md` (pendiente 1); `docs/adr/003-estrategia-comunicacion.md` punto 2 | **NO IMPLEMENTADO.** El código publica tras el commit (`OrdenesEndpoints.cs:243`) y registra `LogCritical` si falla (`:262`) |
| **Dead Letter Queue** | `docs/catalogo-eventos.md` (sección Entrega e idempotencia, punto 3) | **NO IMPLEMENTADO.** Los consumidores hacen `nack(requeue: false)` sin DLQ configurada |
| **Evento `orden.sin-tecnico`** | `contratos/orden.asignada.v1.json` campo `_caso_sin_tecnico` | **NO IMPLEMENTADO** |
| **Reintento de asignación al próximo turno** | mismo campo `_caso_sin_tecnico` | **NO IMPLEMENTADO** |
| **`tecnicos` consumiendo `orden.resuelta`** | `tecnicos/README.md` (pendiente 4) | **NO IMPLEMENTADO.** `consumidor.py:27` solo declara `EVENTO_ENTRADA = "orden.creada"` |
| **Estado del circuit breaker expuesto por HTTP** | `ui/src/componentes/PanelSalud.jsx:117-125` | **NO IMPLEMENTADO.** `ordenes` no tiene endpoint que lo publique |
| **Autenticación** | `ui/src/componentes/Ingreso.jsx:3-9` | **NO IMPLEMENTADO.** Sin contraseña, sin verificación en el backend |
| **Campo `habitacionLiberada`** | `plantillas.py:169-178` (comentario) | **ELIMINADO del contrato.** El aviso ya no distingue |
| **Reasignación de una orden** | `tecnicos/README.md` (pendiente 3) | **BLOQUEADO por el esquema:** `modelos.py:89` `orden_id` es clave primaria de `asignaciones` |
| **Flujo de check-in / `OCUPADA`** | `habitaciones/README.md` (pregunta 3) | **NO IMPLEMENTADO.** `OCUPADA` solo se asigna en la siembra (`SembradorHabitaciones.cs:57`) |
| **Trazas distribuidas** | `docs/limites-descartados.md` punto 3 | **NO IMPLEMENTADO** (descartado a propósito) |

## 8.4 Pruebas que NO existen

| Componente | Estado |
|---|---|
| `ordenes` — `Modelos/Orden.cs` | ✅ 24 casos (`Pruebas/OrdenPruebas.cs`) |
| `ordenes` — endpoints | **NO IMPLEMENTADO** |
| `ordenes` — `ConsumidorOrdenAsignada` | **NO IMPLEMENTADO** |
| `ordenes` — `HabitacionesClient` | **NO IMPLEMENTADO** |
| `ordenes` — `PublicadorEventos` | **NO IMPLEMENTADO** |
| `tecnicos` — `asignador.py` | ✅ 6 casos (`test_asignador.py`) |
| `tecnicos` — `consumidor.py` | ✅ 15 casos (`test_consumidor.py`) |
| `tecnicos` — alta y edición | ✅ 11 casos (`test_gestion.py`) |
| `tecnicos` — endpoints `GET` | **NO IMPLEMENTADO** |
| `habitaciones` — todo | **NO IMPLEMENTADO** (sin proyecto de pruebas) |
| `notificaciones` — todo | **NO IMPLEMENTADO** (sin `requirements-dev.txt`) |
| `gateway` — todo | **NO IMPLEMENTADO** |
| `ui` — todo | **NO IMPLEMENTADO** |

Total de pruebas existentes: **56** (24 xunit + 32 pytest).

## 8.5 Funcionalidad ausente en la UI

| Capacidad | API | Pantalla |
|---|---|---|
| Alta de técnico | ✅ `POST /tecnicos` (`main.py:98`) | **NO IMPLEMENTADO** |
| Edición de técnico | ✅ `PUT /tecnicos/{id}` (`main.py:121`) | **NO IMPLEMENTADO** |
| Ver técnicos | ✅ `GET /tecnicos` | **NO IMPLEMENTADO** (`api.js:142` existe pero ningún componente lo importa) |
| Ver asignaciones | ✅ `GET /asignaciones` | **NO IMPLEMENTADO** (`api.js:146` sin consumidor) |
| Detalle de una orden | ✅ `GET /ordenes/{id}` | **NO IMPLEMENTADO** (`api.js:132` sin consumidor) |
| Detalle de una habitación | ✅ `GET /habitaciones/{numero}` | **NO IMPLEMENTADO** (`api.js:123` sin consumidor) |

## 8.6 Otros huecos verificados

| Hueco | Archivo:línea |
|---|---|
| `422` sin rama propia en el manejo de errores de la UI | `ui/src/api.js:80-107` — cae en `default` (`:101`) |
| `notificaciones` sin `requirements-dev.txt` | directorio `servicios/notificaciones/` |
| `notificaciones` con dependencia circular | `consumidor.py:53` ↔ `main.py:157,256` |
| `gateway` sin `depends_on ... condition` | `docker-compose.yml:188-192` |
| Ningún servicio de aplicación define healthcheck propio en compose | `docker-compose.yml` — solo `rabbitmq` y las tres bases |
| `CODEOWNERS` con 6 de 7 reglas comentadas | `.github/CODEOWNERS:3-9` |
| `tecnicos` sin migraciones (usa `create_all`) | `base_datos.py:50` |
