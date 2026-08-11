"""Pruebas del alta y la edición de técnicos.

Usan el TestClient de FastAPI contra SQLite en memoria, así que no hace falta
PostgreSQL ni RabbitMQ. El consumidor no se arranca: se sustituye el ciclo de
vida de la app, que es lo único que lo lanzaría.

    pip install -r requirements.txt -r requirements-dev.txt
    pytest
"""

import uuid
from contextlib import asynccontextmanager

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import StaticPool

import base_datos
import main
from modelos import Base, Especialidad, Tecnico, Turno


@pytest.fixture
def cliente(monkeypatch):
    motor = create_engine(
        "sqlite://",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    Base.metadata.create_all(motor)
    fabrica = sessionmaker(bind=motor, expire_on_commit=False)

    monkeypatch.setattr(base_datos, "FabricaSesiones", fabrica)

    # El lifespan real crea tablas, siembra y arranca el consumidor de RabbitMQ.
    # Aquí no queremos nada de eso.
    @asynccontextmanager
    async def sin_arranque(_app):
        yield

    monkeypatch.setattr(main.app.router, "lifespan_context", sin_arranque)

    with TestClient(main.app) as c:
        c.fabrica = fabrica
        yield c


def sembrar(fabrica, nombre="Ana", especialidad=Especialidad.PLOMERIA, turno=Turno.MANANA):
    with fabrica() as sesion:
        tecnico = Tecnico(
            id=uuid.uuid4(),
            nombre=nombre,
            especialidad=especialidad.value,
            turno=turno.value,
            activo=True,
        )
        sesion.add(tecnico)
        sesion.commit()
        return str(tecnico.id)


# --- Alta -------------------------------------------------------------------


def test_se_puede_dar_de_alta_un_tecnico(cliente):
    respuesta = cliente.post(
        "/tecnicos",
        json={
            "nombre": "Pedro Solano",
            "especialidad": "PLOMERIA",
            "turno": "TARDE",
            "activo": True,
        },
    )

    assert respuesta.status_code == 201
    cuerpo = respuesta.json()
    assert cuerpo["nombre"] == "Pedro Solano"
    assert cuerpo["especialidad"] == "PLOMERIA"
    assert cuerpo["id"]

    assert len(cliente.get("/tecnicos").json()) == 1


def test_el_alta_nace_activa_si_no_se_dice_lo_contrario(cliente):
    respuesta = cliente.post(
        "/tecnicos",
        json={"nombre": "Rosa", "especialidad": "CERRADURA", "turno": "NOCHE"},
    )

    assert respuesta.json()["activo"] is True


def test_no_se_admite_una_especialidad_fuera_del_catalogo(cliente):
    """Sin esto se podría dar de alta a alguien que ninguna falla va a pedir."""
    respuesta = cliente.post(
        "/tecnicos",
        json={"nombre": "Ana", "especialidad": "JARDINERIA", "turno": "MAÑANA"},
    )

    assert respuesta.status_code == 422


def test_no_se_admite_un_turno_inventado(cliente):
    respuesta = cliente.post(
        "/tecnicos",
        json={"nombre": "Ana", "especialidad": "PLOMERIA", "turno": "MADRUGADA"},
    )

    assert respuesta.status_code == 422


def test_el_nombre_no_puede_ir_vacio(cliente):
    respuesta = cliente.post(
        "/tecnicos",
        json={"nombre": "", "especialidad": "PLOMERIA", "turno": "TARDE"},
    )

    assert respuesta.status_code == 422


# --- Edición ----------------------------------------------------------------


def test_se_puede_cambiar_el_nombre(cliente):
    tecnico_id = sembrar(cliente.fabrica, nombre="Ana Beltran")

    respuesta = cliente.put(
        f"/tecnicos/{tecnico_id}",
        json={
            "nombre": "Ana Beltrán",
            "especialidad": "PLOMERIA",
            "turno": "MAÑANA",
            "activo": True,
        },
    )

    assert respuesta.status_code == 200
    assert respuesta.json()["nombre"] == "Ana Beltrán"


def test_se_puede_cambiar_de_turno_y_de_especialidad(cliente):
    tecnico_id = sembrar(cliente.fabrica)

    respuesta = cliente.put(
        f"/tecnicos/{tecnico_id}",
        json={
            "nombre": "Ana",
            "especialidad": "ELECTRICIDAD",
            "turno": "NOCHE",
            "activo": True,
        },
    )

    cuerpo = respuesta.json()
    assert cuerpo["especialidad"] == "ELECTRICIDAD"
    assert cuerpo["turno"] == "NOCHE"


def test_desactivar_lo_saca_del_reparto(cliente):
    """Poner activo en false es la forma de sacar a alguien de circulación.

    No hay borrado: las asignaciones apuntan al técnico y se perdería la traza
    de quién atendió qué.
    """
    tecnico_id = sembrar(cliente.fabrica)

    cliente.put(
        f"/tecnicos/{tecnico_id}",
        json={
            "nombre": "Ana",
            "especialidad": "PLOMERIA",
            "turno": "MAÑANA",
            "activo": False,
        },
    )

    # Sigue existiendo...
    assert cliente.get(f"/tecnicos/{tecnico_id}").json()["activo"] is False

    # ...pero el asignador ya no lo ve.
    with cliente.fabrica() as sesion:
        assert base_datos.candidatos_activos(sesion) == []


def test_editar_un_tecnico_que_no_existe_da_404(cliente):
    respuesta = cliente.put(
        f"/tecnicos/{uuid.uuid4()}",
        json={
            "nombre": "Fantasma",
            "especialidad": "PLOMERIA",
            "turno": "TARDE",
            "activo": True,
        },
    )

    assert respuesta.status_code == 404


# --- Lo que sigue sin poder hacerse por HTTP --------------------------------


def test_no_hay_endpoint_para_asignar_a_mano(cliente):
    """La asignación se dispara por el evento orden.creada, no por petición.

    Ofrecerla por HTTP contradiría el diseño: la regla vive en asignador.py y
    se ejecuta al consumir, no cuando alguien lo pide.
    """
    assert cliente.post("/asignaciones", json={}).status_code == 405


def test_no_se_puede_borrar_un_tecnico(cliente):
    tecnico_id = sembrar(cliente.fabrica)

    assert cliente.delete(f"/tecnicos/{tecnico_id}").status_code == 405
