# BB-I — «Depredador de área» pareció activarse en un momento que no era un tiro

**Estado:** Abierta

## Observación

**«Depredador de área» pareció activarse en un momento que no era un tiro**

## Análisis / estado actual

**Por datos es imposible**: `box_predator` es `trigger SHOT`, `scope actor`, `distanceToGoal(actor) < 3`. Solo puede saltar en un tiro **suyo** desde dentro del área. Sospecha: un tiro bloqueado al instante no se lee como tiro en pantalla. **Necesita la semilla para cerrarlo**

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
