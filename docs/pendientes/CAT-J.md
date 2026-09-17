# CAT-J — Dos puertas de `BuildGateTests` en rojo en HEAD, sin relación con el trabajo de esta sesión.

**Estado:** Abierta

## Observación

Al re-ejecutar las 43 puertas (`Category=Gate`) tras el trabajo de infraestructura de balanceo del 18 sep
2026 (`docs/analisis/protocolo-balanceo-automatizado.md` §17), dos puertas fallan:

- `badBuildsLoseToNone_elf_brawler = 48.12` (rango esperado 10.00..45.00)
- `buildsWinDifferently_passChain = 1.10` (rango esperado 1.11..-)

`NoGateMetricIsOutOfRange` falla en consecuencia (agrega las dos anteriores).

**CONFIRMED que no lo causó el trabajo de esta sesión**: se hizo `git stash` de los ocho ficheros
modificados y tres nuevos de `Sim/Analysis/`+`Sim.Tests/Balance/`+`Sim.Tests/Analysis/` (ninguno toca
`MatchEngine`, `Utility.cs`, `/data`, ni ninguna ruta de `/Sim` que intervenga en un partido real — son
clasificador/auditoría/harness de test, puro), se ejecutaron las 43 puertas sobre el HEAD limpio
(`453be51`) y **fallan exactamente igual**, con los mismos dos números (`elf_brawler=48.12`,
`passChain=1.10`). El fallo es preexistente en `main`, no una regresión de esta sesión.

## Análisis / estado actual

**Abierta, sin investigar la causa todavía** — fuera del alcance del encargo que motivó la ejecución de
las puertas (protocolo de balanceo automatizado, restringido explícitamente a "no gameplay, no `/data`,
no cerrar Cazagoles/Ancla, no tocar `MinPassChainRatio`"). `passChain` en el nombre de la puerta sugiere
relación con `MinPassChainRatio`/la familia de builds de cadena de pases, pero no se ha mirado el commit
que lo introdujo ni se ha hecho bisección — solo se confirmó que ya estaba roto antes de esta sesión.

## Hermanos

_(por enlazar donde se detecten; puede ser el mismo origen que otras puertas de `elf_brawler` o de
`passChain` si existieran — no comprobado)_
