# BB-I — «Depredador de área» pareció activarse en un momento que no era un tiro

**Estado:** BLOCKED / NEEDS-REPRODUCTION

## Observación

**«Depredador de área» pareció activarse en un momento que no era un tiro**

## Análisis / estado actual

**Por datos es imposible**: `box_predator` es `trigger SHOT`, `scope actor`, `distanceToGoal(actor) < 3`. Solo puede saltar en un tiro **suyo** desde dentro del área. Sospecha: un tiro bloqueado al instante no se lee como tiro en pantalla. **Necesita la semilla para cerrarlo**

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_


## Búsqueda de semilla (16 sep 2026, orquestación de pendientes técnicos)

**Sin semilla reproducible en el repositorio.** Revisado: historial de `data/perks/box_predator.json`
(4 commits, ninguno con semilla), mensajes de commit y `docs/pendientes/BB-I.md` en todas sus versiones
(el propio commit que documenta la anotación ya dice "Necesita la semilla" sin aportarla),
`docs/analisis/` completo (13 ficheros, ninguna mención), `Sim.Tests/Perks/` (ningún test ejercita
`box_predator`), `tools/export-windows.sh` (no registra semilla de partida) y el repositorio entero por
guardados/replays/logs de partida (ninguno commiteado).

**No se inventa una semilla.** Queda BLOCKED hasta que el revisor reproduzca la partida y anote la
semilla, o hasta que aparezca evidencia nueva. La sospecha original —un tiro bloqueado al instante no se
lee como tiro en pantalla— sigue siendo la hipótesis más plausible por descarte de datos, pero no pasa a
CONFIRMED sin poder reproducirla.
