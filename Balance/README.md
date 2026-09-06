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
| `--runs N` | 1000 (60 en modo campaña si no se pasa) | Total de partidos, repartidos por igual entre los emparejamientos de `--teams` (o entre las celdas de la matriz de `--builds`); en modo campaña, número de campañas por build |
| `--seed S` | 1 | Semilla base (entero sin signo). Equipos: `RngStreams.Generation(seed, índice)`. Partido *i* (0-based sobre el total del lote): `RngStreams.Match(seed, i)` |
| `--teams path` | `data/balance/reference.json` | Conjunto de referencia: equipos y emparejamientos (modo por defecto, sin `--builds`/`--describe`) |
| `--data path` | subir directorios desde el directorio de trabajo hasta encontrar `data/` | Raíz de `/data` |
| `--out dir` | `out/<seed>/` | Directorio de salida de los CSV |
| `--log` | apagado | Imprime `Report.Log` del primer partido del lote (o del partido de `--match-seed`) |
| `--dump-utility P:T` | ninguno | `SimConfig.DumpUtility = (P, T)` para el primer partido; imprime la tabla de utilidad la primera vez que el jugador P decide en un tick >= T |
| `--match-seed S` | ninguno | Ejecuta un único partido con esta semilla de motor exacta, con los equipos del primer emparejamiento de `--teams`; ignora `--runs`, no escribe `summary.csv`. Ver "Reproducir un partido concreto" abajo |
| `--quiet` | apagado | No imprime el resumen por consola (los CSV se escriben igual) |
| `--describe [es\|en]` | ninguno; activa el modo catálogo | Ver "Modos de fase 1" abajo |
| `--builds a,b,c` | ninguno; activa el modo matriz o campaña | Ídem. `all` = todas las builds de `data/balance/builds/` |
| `--vs id` | ninguno (sin ella: todos-contra-todos) | Ídem. Requiere `--builds` |
| `--campaign N` | ninguno; activa el modo campaña | Ídem. Requiere `--builds` |
| `--home-away` | apagado | Ídem. Aplica al modo matriz y al modo campaña |
| `--rosters N` | 25 | Plantillas distintas generadas por build sobre las que se promedia cada celda de la matriz (paquete I). Solo aplica al modo matriz |
| `--utility-census N` | ninguno; activa el modo censo | Censo del volcado de utilidad (RT-098) sobre N partidos del **primer emparejamiento** de `--teams`. Ver "Censo de utilidad" abajo |

Salida por consola (salvo `--quiet`): tabla de `summary.csv` alineada, tiempo total y partidos/segundo. Código de salida: `0` si todas las métricas están `IN`/`INFO` (o si se usó `--match-seed`/`--describe`, que no tienen métricas), `1` si alguna está `OUT` o si una build es inválida o desconocida (`error de build: ...`).

## Modos de fase 1: builds y catálogo (docs/fase1-diseno.md §8)

Tres modos nuevos, todos independientes de `--teams`/`reference.json`: cargan las builds de
`data/balance/builds/*.json` (formato en `data/perks/README.md` §8 y `Balance/BuildConfig.cs`) y, para el
catálogo, los perks de `data/perks/*.json`.

### `--describe [es|en]`

Imprime, para cada perk del catálogo (ordenado por id), su rareza, tipo (`Filler`/`Conditional`/
`RuleBreaker`), disparador y la descripción generada por `DescriptionGenerator.Describe` (RT-035: nunca
texto escrito a mano); después, la distribución RF-069 (filler/conditional/ruleBreaker, con su porcentaje y
si cae dentro de 60/30/10 ± 8 puntos). Por defecto en español (`es`); `en` para inglés.

```bash
dotnet run --project Balance -- --describe es
```

### `--builds a,b,c` [`--vs id`] [`--home-away`]: matriz build × rival

**Metodología (paquete I).** Cada celda se promedia sobre `--rosters` plantillas distintas por build y,
cuando las dos builds del emparejamiento comparten **raza y calidad**, las dos plantillas de un partido se
generan con el **mismo índice de generación**: los dos equipos son los mismos diez jugadores y lo único que
cambia son los perks, las rarezas y la alineación, que es lo que §8 quiere medir. Con una sola plantilla
por build (paquete H) la tasa de victoria de la misma build contra su referencia iba del 16,5% al 59,5%
según el dado del generador (sd de 14,9 puntos entre plantillas). Además, cada emparejamiento se juega en
las cuatro combinaciones de (local, visitante) × (ids de jugador bajos, ids altos): los desempates del
motor van por id ascendente y con el reparto fijo el equipo de ids bajos gana 2-3 puntos de más.

Sin `--vs`: todos-contra-todos entre las builds listadas. Con `--vs id`: cada build de `--builds` contra
esa única build rival (el caso de uso de la puerta RT-055/§8: comparar cada build contra la referencia sin
perks de su propia raza). La build de `--vs` también aparece en `builds.csv` con su propia fila: es el
denominador de las métricas normalizadas de §8 (ADR 0012). `--runs` se reparte por igual entre los emparejamientos (resto a los primeros).
Con `--home-away`, cada emparejamiento se juega también con los equipos invertidos y las estadísticas se
acumulan sobre el total (elimina cualquier sesgo local/visitante); sin ella, siempre se simula con la misma
orientación física. Un partido entre dos builds distintas alimenta a la vez las dos celdas de la matriz
(la perspectiva de cada una), así que no hace falta duplicar partidos para tener las dos filas.

```bash
dotnet run --project Balance -- --builds orc_violence,elf_tiki_taka,orc_misplaced --vs human_none --runs 3000 --home-away --out out/f1
dotnet run --project Balance -- --builds all --runs 1300 --home-away --out out/f1
```

Escribe `builds.csv` (`build,opponent,matches,winRate,goalsFor,goalsAgainst,injuriesFor,injuriesAgainst,tacklesPerMatch,passChainAvgLength,activationsPerMatch`)
y `perks.csv` (`perkId,build,activations,matchesWithActivation,activationRate`), con una fila por cada perk
que la build asigna estáticamente a algún titular aunque nunca llegue a activarse (0% es justo lo que
`noDeadPerks` de `Sim/Analysis/BuildMetrics.cs` necesita poder detectar).

- `injuriesFor`/`injuriesAgainst`, `tacklesPerMatch`: se reparten por equipo con `PlayerMatchStats.Team`
  (`injuriesFor` = lesiones sufridas por los propios jugadores de `build`; `injuriesAgainst` = lesiones que
  `build` le ha causado al rival).
- `passChainAvgLength`: se reparte por equipo con `MatchReport.PassChainsByTeam` /
  `PassChainTotalLengthByTeam` (paquete I): cada fila lleva las cadenas de su propia build. Antes era una
  estadística de partido completo y no podía distinguir a las dos builds, que es justo lo que compara
  `buildsWinDifferently`.
- `activationsPerMatch`: activaciones de perk (`MatchReport.PerkActivations`) cuyo `OwnerId` pertenece a
  los jugadores de esa build en ese partido.

### `--builds a,b,c --campaign N` [`--home-away`]: campaña con progresión

Para cada build de `--builds`, `--runs` campañas independientes (por defecto 60, semillas distintas) de `N`
partidos consecutivos (por defecto 8) contra la build `human_none` de calidad creciente (46, 48, ...,
46+2(N-1)); el rival se regenera cada partido con la calidad que toque, sin historial. Dentro de cada
campaña se arrastra progresión (docs/fase1-diseno.md §6): tras cada partido, `Progression.AwardExperience`
reparte experiencia (100% a los 7 titulares que jugaron, 45% a los suplentes), `Progression.LevelFor` +
`Progression.LevelUp` suben de nivel subiendo `attributesPerLevel` a todos los atributos salvo correa, y
`MatchResult.CounterDeltas` se aplican a los `Counters` de cada jugador (`Progression.ApplyCounterDeltas`).
Con `--home-away`, la build alterna de local a visitante partido a partido dentro de la misma campaña.

```bash
dotnet run --project Balance -- --builds orc_violence,elf_tiki_taka,orc_misplaced --campaign 8 --runs 40 --out out/f1c
```

Escribe `campaign.csv` (`build,matchIndex,opponentQuality,campaigns,winRate,avgLevel,avgStrength,avgTechnique,activationsPerMatch`,
una fila por `(build, matchIndex)` agregada sobre las `campaigns` repeticiones; `avgLevel`/`avgStrength`/
`avgTechnique` promedian los 10 jugadores de la plantilla, no solo los 7 titulares, después del partido de
ese `matchIndex`). Por consola, una tabla por build con la tasa de victoria en los partidos `1..N/2` frente
a `N/2+1..N`, calculada sobre victorias/partidos totales de las `campaigns` repeticiones (no como media de
porcentajes).

### `Sim/Analysis/BuildMetrics.cs`

Cálculo puro y reutilizable (sin E/S) de las métricas de §8 —`coherentBuildsBeatNone`,
`badBuildsLoseToNone`, `randomBuildNearNone`, `buildsWinDifferently`, `noDeadPerks` y la distribución
RF-069— a partir de los datos ya agregados de un lote (`BuildCellResult`, `PerkActivationResult`). Las
listas de builds coherentes/malas/aleatoria y el mapeo raza → build de referencia son parámetros, no están
codificados dentro: `/Balance` (paquete H) no las usa desde la línea de comandos; las carga y resuelve
`data/balance/groups.json` (`BuildConfig.BuildGroups` en `Balance/BuildConfig.cs`) la puerta
estadística de `Sim.Tests` (paquete I), que es quien decide los umbrales finales y las builds concretas a
comparar en `buildsWinDifferently`.

## Censo de utilidad (`--utility-census N`)

Responde a **"¿por qué esta acción no gana nunca la tabla?"** separando las tres respuestas que son
distintas entre sí: porque se **descarta** (no había a quién, o no tocaba en ese estado), porque **puntúa
por debajo** aun siendo legal, o porque no se evalúa.

`SimConfig.DumpUtility` imprime la tabla de **un** jugador en **un** tick (RT-098). El censo repite el
**mismo** partido —mismo setup y misma semilla de motor— una vez por (jugador, tick) muestreado y acumula
las tablas: 20 jugadores × 40 ticks (uno cada 30, hasta el 1.200) × N partidos. Es caro en partidos y
barato en código, y a cambio mide exactamente lo que la tabla de utilidad decide, no una aproximación.
**No toca `/Sim`.**

```bash
dotnet run --project Balance -c Release -- --utility-census 12 --seed 1
```

Columnas: `evaluada` (veces que la acción entró en una tabla), `descartada%` (de esas, cuántas se
descartaron), `elegida`/`elegida%` (cuántas ganaron), `2a%` (cuántas quedaron segundas), `scoreMedio` y
`scoreMax` sobre las no descartadas, y `margenMedio` (distancia media al score ganador). Sin CSV: la
salida es la tabla por consola.

**Aviso de muestreo**: se muestrean todos los estados de decisión, y las acciones con balón
(`ShortPass`, `LongPass`, `Dribble`, `Shoot`) solo son legales para el poseedor, así que salen
infrarrepresentadas frente al reparto de tiempo que ve el campo animado. Para las acciones sin balón el
reparto sí es directamente comparable.

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
| `betterTeamWinRate_<homeId>_vs_<awayId>` | **70-88** si diferencia de calidad = 20 (ADR 0054); si no, INFO | IN/OUT o INFO | Una fila por cada emparejamiento de `--teams` cuyos equipos tengan calidad distinta: tasa de victorias del equipo de mayor calidad sobre los partidos de ese emparejamiento |

## Decisiones fuera de la especificación

- **Semilla del motor**: `Simulator.Run` espera un `ulong seed`; `RngStreams.MatchSeed(seed, i)` deriva esa semilla escalar con la misma mezcla que `RngStreams.Match(seed, i)` (que en cambio devuelve un `Pcg32` ya construido), así que `Simulator.Run(setup, RngStreams.MatchSeed(seed, i), catalog, config)` usa el mismo flujo de partido que el resto de `/Sim` sin tener que exponer el estado interno de un `Pcg32`.
- **Árbitro**: `docs/fase0-diseno.md` no define cómo generar el árbitro de los partidos de `/Balance`. Se usa un árbitro fijo neutro (`RefereeSetup("Referee", RefereeTrait.Neutral, InitialBias: 0)`) para todos los partidos del lote.
- **`firstPlayerId` de la segunda instancia en autoenfrentamientos** (p. ej. `human_50` vs `human_50`): `1 + (1000 + índice) * 100`, siguiendo el mismo esquema que la instancia primaria (`1 + índice * 100`) para no colisionar con ninguna otra.
- **Rendimiento**: `SimConfig.CollectLog` y `SimConfig.DumpUtility` solo se activan (si se pidieron por `--log`/`--dump-utility`) para el primer partido del lote (índice 0 global); el resto corre con `CollectLog = false` para no acumular el log de miles de partidos en memoria.
- **players.csv**: formato no especificado en `docs/fase0-diseno.md` §4 (solo se menciona su existencia); columnas elegidas arriba.

### Paquete H (modos de fase 1 sobre builds)

- **Semántica de `--vs`**: docs/fase1-diseno.md §8 dice a la vez "`--vs <buildId>` (por defecto `human_none`)" y "sin `--vs`, todos contra todos". Se ha resuelto literalmente por la segunda frase: `--vs` es `null` si no se pasa el flag (modo todos-contra-todos) y exige un valor explícito cuando se pasa (como el resto de opciones de `Options.cs`); "por defecto `human_none`" se lee como el valor típico que se espera que el usuario escriba (`--vs human_none`), no como un valor implícito al omitir el flag. Es la lectura que hace que el comando de verificación del encargo (`--builds all --home-away`, sin `--vs`) tenga sentido como el modo todos-contra-todos que describe la puerta RT-055/§8.
- **`--campaign` siempre necesita un valor**: igual que `--runs`, no hay forma de escribir `--campaign` "a secas"; el "por defecto 8" de §8 se aplica solo cuando el flag no se pasa en absoluto (modo campaña desactivado), no como valor implícito del flag sin argumento.
- **Rival de campaña fijo a `human_none`**: §8 dice literalmente "N partidos consecutivos contra `human_none`", no contra la referencia de la propia raza; se ha implementado así aunque sea distinto del rival de la matriz por defecto (`--vs human_none` también, mismo id, pero por coincidencia con la baseline de Human, no por raza).
- **`avgLevel`/`avgStrength`/`avgTechnique` de `campaign.csv`**: promedian los 10 jugadores de la plantilla generada (titulares + suplentes), no solo los 7 titulares, porque los suplentes también progresan (45% de experiencia) y el objetivo de la columna es "cómo de fuerte está la plantilla completa en ese punto de la campaña".
- **`passChainAvgLength` de `builds.csv`**: es una estadística de todo el partido, no de un equipo (`MatchReport.PassChains`/`PassChainTotalLength` no distinguen equipo en el motor, y no se ha tocado `Sim/Engine` para partirla); el mismo valor alimenta la fila de las dos builds de cada partido. Es una medida más débil que si estuviera partida por equipo, pero es la que puede darse sin tocar el motor, y sigue sirviendo para `buildsWinDifferently` (compara el promedio de una build sobre todos sus partidos, no una resolución por bando).
- **`--rosters` por defecto 25**: es el valor mínimo con el que la tasa de victoria de una celda deja de
  depender de qué plantilla salió; la puerta de fase 1 (`Sim.Tests/Analysis/BuildGateTests.cs`) usa 80.
- **Los ids altos son `100001..`**, misma paridad que los bajos (`1..`): la paridad importa porque un
  jugador decide cuando `(tick + id) % decisionIntervalTicks == 0`, y con paridades distintas los dos
  equipos decidirían en ticks distintos.
- **`data/balance/groups.json`**: los grupos de builds para las métricas de fase 1 (`coherent`/`bad`/`random`/`baselineByRace`) viven fuera de `balance/builds/` porque no son una build; tienen esquema propio (`data/schemas/balance-groups.schema.json`) y el validador los reconoce.
- **`--describe` sin argumento**: por defecto imprime en español (`es`); solo consume el siguiente token de la línea de comandos si es exactamente `es` o `en`, para no confundirlo con la siguiente opción si `--describe` es el último flag antes de otra cosa.
