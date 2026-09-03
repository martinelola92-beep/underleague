# /Balance

Consola .NET que ejecuta N partidos de `Sim` sin Godot y vuelca CSV (docs/fase0-diseno.md §4, docs/balance.md). Sin paquetes NuGet: parseo manual de argumentos y `System.Text.Json` del framework.

## Uso

```bash
dotnet run --project Balance -- \
  --runs 10000 \
  --seed 1 \
  --teams data/balance/reference.json \
  --data data/ \
  --out out/1/ \
  --log \
  --dump-utility 101:50 \
  --quiet
```

Opciones (todas opcionales):

| Opción | Por defecto | Descripción |
|---|---|---|
| `--runs N` | 1000 | Total de partidos, repartidos por igual entre los emparejamientos de `--teams` (en orden, el resto de la división a los primeros) |
| `--seed S` | 1 | Semilla base (entero sin signo). Equipos: `RngStreams.Generation(seed, índice)`. Partido *i* (0-based sobre el total del lote): `RngStreams.Match(seed, i)` |
| `--teams path` | `data/balance/reference.json` | Conjunto de referencia: equipos y emparejamientos |
| `--data path` | subir directorios desde el directorio de trabajo hasta encontrar `data/` | Raíz de `/data` |
| `--out dir` | `out/<seed>/` | Directorio de salida de los CSV |
| `--log` | apagado | Imprime `Report.Log` del primer partido del lote (o del partido de `--match-seed`) |
| `--dump-utility P:T` | ninguno | `SimConfig.DumpUtility = (P, T)` para el primer partido; imprime la tabla de utilidad la primera vez que el jugador P decide en un tick >= T |
| `--match-seed S` | ninguno | Ejecuta un único partido con esta semilla de motor exacta, con los equipos del primer emparejamiento de `--teams`; ignora `--runs`, no escribe `summary.csv`. Ver "Reproducir un partido concreto" abajo |
| `--quiet` | apagado | No imprime el resumen por consola (los CSV se escriben igual) |

Salida por consola (salvo `--quiet`): tabla de `summary.csv` alineada, tiempo total y partidos/segundo. Código de salida: `0` si todas las métricas están `IN`/`INFO` (o si se usó `--match-seed`, que no tiene métricas), `1` si alguna está `OUT`.

## Reproducir un partido concreto

`matches.csv` guarda en su columna `seed` la semilla de motor exacta de cada partido (`RngStreams.MatchSeed(seedBase, índice)`, no el índice). Para reproducirlo bit a bit, incluido el log tick a tick:

```bash
dotnet run --project Balance -- --match-seed <seed de la fila> --teams <mismo --teams del lote> --seed <misma --seed del lote> --log
```

`--seed` sigue haciendo falta porque los equipos se generan con `RngStreams.Generation(seed, índice)`; solo la semilla del propio partido se fija con `--match-seed`. Ver también la skill `sim-debug`.

## Ficheros de salida

### `matches.csv`

Una fila por partido simulado con éxito.

`index,seed,homeId,awayId,homeGoals,awayGoals,winner,ticks,goldenGoal,forfeit,possessionChanges,passChains,passChainAvgLength,shots,shotsOnTarget,tackles,fouls,yellow,red,injuries,ballThird0,ballThird1,ballThird2,finalBias`

- `index`: el índice 0-based del partido en el lote completo. `seed`: la semilla de motor real de ese partido, `RngStreams.MatchSeed(seedBase, índice)` (revisión independiente, fase 0: antes llevaba el mismo valor que `index`, que no era la semilla que recibió `Simulator.Run` y no servía para reproducir el partido con `--match-seed`).
- `winner`: `0` = gana `homeId`, `1` = gana `awayId`.
- `goldenGoal`/`forfeit`: `true`/`false`.
- `shots`/`shotsOnTarget`: suma de ambos equipos.
- `passChainAvgLength`: longitud media de cadena de pases de ese partido (`PassChainTotalLength / PassChains`, `0` si no hubo cadenas).
- `ballThird0..2`: ticks de balón en cada tercio absoluto del campo.

### `players.csv`

Una fila por jugador generado que llegó a jugar al menos un partido, con sus estadísticas acumuladas a lo largo de **todos** los partidos del lote en los que participó (decisión fuera de la especificación: el formato de `players.csv` no está detallado en `docs/fase0-diseno.md` §4, solo se menciona su existencia).

`playerId,teamId,name,race,position,rarity,matches,goals,assists,shots,passesAttempted,passesCompleted,tackles,tacklesWon,fouls,cards,injuries,ticksOnPitch`

### `summary.csv`

`metric,value,rangeMin,rangeMax,status`, con `status` en `IN`/`OUT`/`INFO`. Todos los valores numéricos con dos decimales; `rangeMin`/`rangeMax` vacíos cuando la métrica no tiene ese límite (p. ej. `share_over5goals` no tiene mínimo).

| Métrica | Rango | Estado | Cómo se calcula |
|---|---|---|---|
| `possessionChanges` | 12-25 | IN/OUT | Media de `PossessionChanges` por partido |
| `passChainAvgLength` | 2-4 | IN/OUT | Suma de `PassChainTotalLength` / suma de `PassChains` de todo el lote (media ponderada, no media de medias) |
| `shotsPerMatch` | 8-16 | IN/OUT | Media de tiros (ambos equipos) por partido |
| `scorelineShare_1-0_to_3-2` | >= 50 | IN/OUT | Porcentaje de partidos cuyo marcador final tiene entre 1 y 5 goles totales con diferencia de 1 o 2 goles (1-0, 2-0, 2-1, 3-1, 3-2 y sus simétricos visitante-local) |
| `share_over5goals` | < 5 | INFO | Porcentaje de partidos con más de 5 goles totales |
| `drawShareAtRegulation` | < 15 | INFO | Porcentaje de partidos que llegaron empatados al final del reglamentario (`WentToGoldenGoal`) |
| `ballThirdMaxShare` | <= 50 | IN/OUT | Se suman los ticks de balón por tercio de todo el lote y se toma el máximo de los tres porcentajes resultantes |
| `tacklesPerMatch` | 6-14 | IN/OUT | Media de `Tackles` por partido |
| `injuriesPerMatch` | 0.3-0.8 | IN/OUT | Media de `Injuries` por partido |
| `betterTeamWinRate_<homeId>_vs_<awayId>` | 65-80 si diferencia de calidad = 20; si no, INFO | IN/OUT o INFO | Una fila por cada emparejamiento de `--teams` cuyos equipos tengan calidad distinta: tasa de victorias del equipo de mayor calidad sobre los partidos de ese emparejamiento |

## Decisiones fuera de la especificación

- **Semilla del motor**: `Simulator.Run` espera un `ulong seed`; `RngStreams.MatchSeed(seed, i)` deriva esa semilla escalar con la misma mezcla que `RngStreams.Match(seed, i)` (que en cambio devuelve un `Pcg32` ya construido), así que `Simulator.Run(setup, RngStreams.MatchSeed(seed, i), catalog, config)` usa el mismo flujo de partido que el resto de `/Sim` sin tener que exponer el estado interno de un `Pcg32`.
- **Árbitro**: `docs/fase0-diseno.md` no define cómo generar el árbitro de los partidos de `/Balance`. Se usa un árbitro fijo neutro (`RefereeSetup("Referee", RefereeTrait.Neutral, InitialBias: 0)`) para todos los partidos del lote.
- **`firstPlayerId` de la segunda instancia en autoenfrentamientos** (p. ej. `human_50` vs `human_50`): `1 + (1000 + índice) * 100`, siguiendo el mismo esquema que la instancia primaria (`1 + índice * 100`) para no colisionar con ninguna otra.
- **Rendimiento**: `SimConfig.CollectLog` y `SimConfig.DumpUtility` solo se activan (si se pidieron por `--log`/`--dump-utility`) para el primer partido del lote (índice 0 global); el resto corre con `CollectLog = false` para no acumular el log de miles de partidos en memoria.
- **players.csv**: formato no especificado en `docs/fase0-diseno.md` §4 (solo se menciona su existencia); columnas elegidas arriba.
