"""Configuración del servicio, leída de variables de entorno.

Existe como archivo aparte porque base_datos.py, consumidor.py y main.py
necesitan los mismos valores. Repetir la lectura en cada uno haría que un
cambio de nombre de variable se arreglara en tres lugares y se olvidara en uno.

Ningún valor de conexión está escrito literalmente en el código.
"""

from pydantic_settings import BaseSettings, SettingsConfigDict


class Configuracion(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # --- Base de datos propia ------------------------------------------------
    database_url: str = "postgresql+psycopg://tecnicos:cambiar@db-tecnicos:5432/tecnicos"

    # --- RabbitMQ ------------------------------------------------------------
    rabbitmq_host: str = "rabbitmq"
    rabbitmq_puerto: int = 5672
    rabbitmq_usuario: str = "guest"
    rabbitmq_contrasena: str = "guest"

    exchange: str = "hotel.eventos"
    cola_orden_creada: str = "tecnicos.orden-creada"

    segundos_espera_reconexion: int = 5

    # --- Dominio -------------------------------------------------------------
    # Los eventos viajan en UTC (lo fijan los contratos), pero los TURNOS son
    # horarios locales del hotel. Sin esta conversión, una orden de las 20:00
    # locales llegaría como 00:00 UTC y caería en el turno equivocado.
    #
    # -4 es el huso del hotel. Se deja configurable porque es exactamente el
    # tipo de valor que se olvida y produce asignaciones absurdas.
    hotel_utc_offset: int = -4

    @property
    def url_amqp(self) -> str:
        return (
            f"amqp://{self.rabbitmq_usuario}:{self.rabbitmq_contrasena}"
            f"@{self.rabbitmq_host}:{self.rabbitmq_puerto}/"
        )


configuracion = Configuracion()
