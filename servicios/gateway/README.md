# Servicio `gateway`

C# / .NET 8 + YARP

---

## Qué hace

Es el **único punto de entrada** al sistema. Recibe todas las peticiones de la UI
y las reenvía al servicio que corresponda. Nada más entra desde afuera: los otros
cuatro servicios no publican puerto hacia el host.

---

## De qué datos es dueño

**De ninguno.** No tiene base de datos, no guarda estado y no conoce el dominio.
Solo sabe que la ruta `/ordenes` va a un lugar y `/tecnicos` a otro.

Si algún día necesita saber qué es una orden, dejó de ser un gateway.

---

## Con quién habla

| Dirección | Con quién | Cómo |
|---|---|---|
| Entrante | `ui` (navegador) | HTTP |
| Saliente | `habitaciones`, `ordenes`, `tecnicos`, `notificaciones` | HTTP (reenvío) |
| RabbitMQ | **no participa** | — |

---

## Tabla de enrutamiento prevista

| Ruta pública | Servicio destino |
|---|---|
| `/habitaciones/**` | `habitaciones` |
| `/ordenes/**` | `ordenes` |
| `/tecnicos/**`, `/asignaciones/**` | `tecnicos` |
| `/notificaciones/**` | `notificaciones` |
| `/salud` | agregado propio: consulta el `/salud` de los cuatro |

Los destinos son los **nombres de servicio** de la red de `docker-compose`, nunca
`localhost` ni IPs.

---

## Cómo se levanta

```
docker compose up gateway
```

Debe arrancar **aunque los servicios destino estén caídos**. Un servicio no sano
se reporta como tal en `/salud`; no impide que el gateway responda.

---

## Preguntas guía pendientes

1. ¿Por qué esto es un gateway y no un BFF? (ver `docs/limites-descartados.md`,
   punto 5)
2. ¿Qué devuelve el gateway cuando el destino está caído: `502` o `503`?
3. ¿La UI habla con el gateway o nginx hace de proxy hacia él? Decidir y que
   coincida con `../ui/nginx.conf` y `../ui/src/api.js`.
