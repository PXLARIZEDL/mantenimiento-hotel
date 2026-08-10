# Cabeceras compartidas por todos los proxy_pass hacia el gateway.
# Se incluye desde nginx.conf para no repetirlas seis veces.

proxy_http_version 1.1;
proxy_set_header Host              $host;
proxy_set_header X-Real-IP         $remote_addr;
proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;

# El gateway ya aplica sus propios timeouts hacia los servicios; estos son solo
# para no dejar la petición del navegador colgada indefinidamente.
proxy_connect_timeout 10s;
proxy_send_timeout    30s;
proxy_read_timeout    30s;
