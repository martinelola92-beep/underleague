# Balance

Concreta RT-050..057 y las métricas obligatorias dispersas por los requisitos (RF-024, RF-064e, RF-064g, RF-114k, RT-055, RT-081). `/Balance` existe desde la fase 0 y es el criterio de salida de cada fase, no una herramienta de final de proyecto (riesgo "balanceo inabordable con 150 perks").

## Herramienta `/Balance` (RT-050..053)

Consola .NET que ejecuta N partidos sin Godot y vuelca CSV.

```
dotnet run --project Balance -- \
  --runs 10000 \
  --seed 1 \
  --teams data/balance/reference.json \       # parejas o pool de equipos
  --perks bloodlust,innocent_face \           # filtro opcional
  --out out/2026-09-03/ \
  [--state path.json]                         # RT-062: estado predefinido
  [--dump-utility playerId:tick]              # RT-098
```

Modos añadidos después de la fase 0:

```
dotnet run --project Balance -- --builds all [--vs id] [--campaign N] [--home-away] [--rosters N]
dotnet run --project Balance -- --boss-gate [--rosters 32] [--runs 4]     # curva de la ADR 0033
dotnet run --project Balance -- --full-runs 500 [--seed 1]                # runs completas (fase 2)
dotnet run --project Balance -- --full-runs 500 --ignore-scouting         # la misma run sin leer el ojeo
dotnet run --project Balance -- --full-runs 300 --risk-aversion N         # cuánto pesa el indicador de riesgo
dotnet run --project Balance -- --describe [es|en]                        # catálogo de perks
```

`--full-runs N` juega N runs completas **con cada una de las tres doctrinas de compra de la ADR 0037**
(contextual, gastadora, ahorradora) sobre las mismas semillas, y escribe `runs.csv` (una fila por run:
acto alcanzado, causa de derrota, oro ganado y gastado por sumidero, muertes, lesiones, tamaño final de
plantilla, nivel medio, mercados visitados y qué se compró) más `summary.csv` con las métricas de
`fase2-diseno.md` §10 y de la ADR 0037. La política automática y sus reglas están en
`Sim.Analysis.RunPolicy` y explicadas en `docs/balance/fase2-resultados.md` §1.

Escribe además **`runs-nomarket.csv`**, con las mismas columnas, para la cuarta política del modo: la
medida de control de la ADR 0055, que es contextual pero **esquiva los mercados** y por eso no se
distingue de la build buena en la columna `doctrine` de `runs.csv`. Es el perfil **sin build** de la ADR
0057 —con `economy.rewardPerkWeight = 0` en `/data`— y sin este volcado no se puede desglosar por acto
(ADR 0064, `fase2-diseno.md` §31.1).

`--ignore-scouting` apaga la lectura del informe de ojeo y del indicador de riesgo al alinear
(`RunPolicyOptions.HeedsLethalScouting`), y `--risk-aversion N` fija cuánto descuenta la política el valor
de un titular por su exposición a un perk letal (`DeathCostPercent`): 0 ignora el número, un valor alto lo
obedece y un valor **negativo** hace lo contrario a propósito. Las dos existen para medir la **agencia**
que la ADR 0048 declara obligatoria —si atender al indicador no cambia las muertes, el azar no tiene
agencia— y su lectura está en `fase2-diseno.md` §21.2.

Rendimiento: 10.000 partidos en menos de 60 s en máquina de desarrollo (RT-051). Se mide en cada ejecución y se imprime al final.

Salida (`summary.csv` + `matches.csv` + `perks.csv`):

- `matches.csv`: `seed`, `teamA`, `teamB`, `goalsA`, `goalsB`, `winner`, `ticks`, `possessionChanges` (alternancias), `avgPassChain` (cadena media de pases), `shots`, `tackles`, `fouls`, `cards`, `injuries`, `deaths`, `mob` (bool, turba), `finalBias` (criterio final), `ballTimeByThird` (tiempo del balón por tercio).
- `perks.csv`: `perkId`, `activations`, `matchesWithActivation`, `contribution` (goles, lesiones, recuperaciones).
- `summary.csv`: cada métrica de la tabla siguiente con valor (`value`), rango (`range`) y `IN|OUT` (dentro/fuera de rango).

## Métricas de sensación de fútbol (RT-056)

Criterio de salida de la fase 0 e indicador permanente del equilibrio fútbol/agresividad.

| Métrica | Rango objetivo | Cómo se mide |
|---|---|---|
| Alternancias de posesión por partido | 12-25 | Cambios de equipo poseedor |
| Longitud media de cadena de pases | 2-4 | Pases completados consecutivos por posesión |
| Tiros por partido (ambos equipos) | 8-16 | Eventos `SHOT` |
| Distribución de resultados | Mayoría entre 1-0 y 3-2; < 5% con más de 5 goles totales; < 15% de empates **al final del reglamentario** | Marcador antes de la turba |
| Tiempo del balón por tercio | Ningún tercio > 50% | Ticks con el balón en cada tercio de columnas |
| Entradas por partido | 6-14 | Eventos `TACKLE` |
| Lesiones por partido | 0,3-0,8 | Eventos `INJURY` |

Los rangos son puntos de partida. **Cambiar un rango es una decisión explícita** (RT-057): ADR en `decisiones/` con los datos que lo motivan y actualización de esta tabla en el mismo commit.

### Cómo se leen estas métricas (medido en el paquete E)

- **El cálculo es único**: `Sim/Analysis/MatchMetrics.cs`. Lo usan el lote de `/Balance` y la puerta estadística de `Sim.Tests`; no hay dos definiciones de la misma métrica.
- **Alternancias de posesión y tiros están acopladas**: toda posesión que acaba en tiro acaba también en cambio de poseedor (parada, saque de puerta o saque de centro). Con 8-16 tiros, entre 8 y 16 de las 12-25 alternancias ya están gastadas; el resto del presupuesto es para intercepciones, entradas ganadas, regates perdidos, saques de banda y córneres. Subir los tiros sin bajar las pérdidas saca `possessionChanges` de rango.
- **`ballThirdMaxShare` cuenta también el balón parado**: durante reanudaciones el reloj sigue y el balón está quieto en el punto del saque (§3.11), así que el tercio de los saques de puerta suma. Es intencionado: el tiempo muerto es parte del reparto.
- **`drawShareAtRegulation` < 15% no es alcanzable junto con el resto de la fila de resultados.** Con dos marcadores aproximadamente independientes, la probabilidad de empate es `e^-2λ·I₀(2λ)`: 27% con 2,5 goles por partido, 23% con 3,2 y 14% solo a partir de 8 goles por partido, que rompería a la vez `< 5%` de partidos con más de cinco goles y la mayoría de resultados entre 1-0 y 3-2. La medición del paquete E se queda en 29-31% con 2,4 goles por partido. La métrica es `INFO` en `summary.csv` y **no bloquea** la puerta; queda anotada como inconsistencia I-11 en `pendientes.md`. El partido nunca termina en empate de todos modos (gol de oro, RF-055c): lo que mide en la práctica es la frecuencia de turba.
- **`betterTeamWinRate` mide dos plantillas concretas, no dos calidades.** Cada equipo del conjunto de referencia se genera una sola vez por lote (`RngStreams.Generation(semilla, índice)`), así que los 333 partidos de un emparejamiento en un lote de 2.000 son la misma pareja de plantillas: el tamaño de muestra efectivo para esta métrica es el número de **semillas**, no el de partidos. Medida sobre diez semillas, la tasa para una diferencia de calidad de 20 tiene una desviación de unos 6 puntos alrededor de la media (medido en el paquete U: 69% a 84% según la semilla, con media 78,8% sobre ocho parejas de plantillas y 3.200 partidos). Si el valor se mueve al cambiar `--runs` o `--seed`, no es ruido de partido: es otra plantilla.
- **`quality` es la media objetivo de atributos del equipo, no un nivel.** Desde el paquete U el dial desplaza el presupuesto de generación (`+5` por punto de calidad, uno por atributo) y la banda de suelo y techo (`+1` por punto), así que un equipo de calidad 60 es exactamente uno de calidad 40 con **veinte puntos más en cada atributo**, que es lo que la fila dice medir. Entre los paquetes Q y V el dial se traducía a `nivel = quality/10` y una diferencia de 20 puntos de calidad eran 16 puntos de presupuesto sobre 290: la métrica daba 40,8% para el equipo "mejor" y no medía nada. `level` y `rarity` son ahora campos propios de `reference.json` y de las builds.
- **El nivel y la rareza no son la calidad.** `level` (1-8) vale `budgetPerLevel` = 8 puntos de presupuesto por nivel; `rarity` cambia el presupuesto base (250/275/300) y la banda de atributos. Un común de nivel 8 (306) y un legendario de nivel 2 (308) son deliberadamente equivalentes (ADR 0027).
- **Local y visitante no cambian nada**: desde el paquete E, dos equipos de la misma calidad ganan 50,6%/49,4% (4.800 partidos espejo). Cualquier ventaja de local debe venir del criterio del árbitro (RF-060).

## Puertas de CI (RT-054, RT-055)

En cada commit sobre `/Sim` o `/data`:

1. `dotnet test Sim.Tests` incluida la prueba estadística de 1.000 partidos (RT-081) contra los rangos anteriores.
2. Lote de balance con el conjunto de referencia. **El build falla** si alguna build catalogada supera el 70% o baja del 30% de tasa de victoria contra la referencia (RT-055).
3. Validación de `/data` (RT-083).

Las tres puertas automáticas viven en `Sim.Tests` con `Trait("Category", "Gate")` y suman unos 40 s:

| Puerta | Fichero | Muestra | Qué defiende |
|---|---|---|---|
| Sensación de fútbol | `Engine/StatisticalTests.cs` | 1.000 partidos, semilla 1 | RT-056 y `betterTeamWinRate` (banda 70-88, ADR 0054) |
| Criterio de salida de fase 1 | `Analysis/BuildGateTests.cs` | 40 plantillas × 12 partidos × 14 celdas = 6.720, semilla 1, ~30 s | Coherentes >= 58%, malas <= 45%, aleatoria 40-60%, `buildsWinDifferently`, `noDeadPerks`, RF-069 |
| Rareza y jefe final | `Analysis/RarityAndBossTests.cs` | 24 plantillas × 20 partidos × 3 comparaciones, semilla 1 | RF-024 y la salvaguarda de la ADR 0027 |
| Equilibrio entre razas | `Analysis/RaceBalanceTests.cs` | 250 plantillas × 4 partidos × 10 parejas, semilla 1 | D-29: ninguna raza fuera del 40-60% agrupado |
| **Curva de puertas de la ADR 0033** | `Analysis/BossGateTests.cs` | 32 plantillas × 4 partidos × 4 niveles × 3 jefes × 5 razas = 7.680, semilla 1, ~35 s | **La** métrica de la fase 2: cada nivel de calidad de build contra cada jefe |
| **Run completa** | `Analysis/FullRunGateTests.cs` | 60 runs × 3 doctrinas de compra, semilla 1, ~14 s | Duración de la run, causas de derrota, RF-114k, compras por mercado, determinismo del bucle |

**RT-055 no está automatizada**: se mide a mano con `--builds all --vs human_none`. Al cierre del paquete U
la incumplen las razas, no las builds (elfos 67,5%, orcos 23,5% contra `human_none` sin perks); anotado como
D-29 en `pendientes.md`.

## Métricas de diseño obligatorias

Se añaden a `summary.csv` cuando el sistema correspondiente existe:

| Métrica | Requisito | Condición de aprobado | Fase |
|---|---|---|---|
| Común nivel 8 vs legendario nivel 2, en igualdad de perks | RF-024 (v0.9.1, ADR 0027) | El común queda entre el 45% y el 55%. **Implementada** en `Sim.Tests/Analysis/RarityAndBossTests.cs`; medido 49,79% | 1 |
| Común nivel 8 vs legendario de nivel alto | RF-024 (v0.9.1, ADR 0027) | El común pierde con claridad: por debajo del 40%. Medido 38,75% | 1 |
| Equipo sin ningún legendario contra el jefe final | RF-024 (v0.9.1, ADR 0027) | Puede ganar con una tasa razonable si el jugador ha jugado bien; el umbral operativo es **>= 25%**. Si no, la decisión de la ADR 0027 hay que revisarla. Medido 57,92% con una build coherente de comunes de nivel 8 | 2 (adelantada a fase 1) |
| Común superviviente competitivo ante jefe final | RF-023b | Tasa de victoria del equipo con comunes de nivel 8 dentro de 30-70% contra el jefe final. Medido 38,75% sin perks | 2 |
| Build de violencia con sobornos vs sin sobornos | RF-064e | Viable (>=40%) con sobornos, inviable (<30%) sin ellos | 3 |
| Build de violencia con sobornos + 2 mitigaciones | RF-064g | Alcanza la tasa de referencia sin depender de una sola mitigación (retirar cualquiera no la hunde por debajo del 30%) | 3 |
| Oro medio por acto | RF-114k | Permite usar 2-3 sumideros, nunca todos. **Implementada** en `FullRunMetrics.SinksAffordable`; medido 2,40 y nunca los cuatro | 2 |
| Tasa de victoria de la run | `fase2-diseno.md` §10 | 25-40% con la política contextual. Medido **13,0%**; la banda no es compatible con la tabla de la ADR 0033 (Z-G en `pendientes.md`) | 2 |
| Ventaja de la política contextual | ADR 0037 | >= 8 puntos sobre la gastadora y la ahorradora. Medido **+5,0** y **+0,8**; bloqueada por la ADR 0036 (Z-H) | 2 |
| Escasez del mercado | ADR 0037 | 20-35% del surtido asequible al llegar, 1-2 compras por visita, < 15% de oro sobrante, 10-25% de runs sin poder comprar. Medido 40,5 · 1,43 · 23,2 · 49,2 (Z-K, Z-L) | 2 |
| Cada raza sostiene 3 builds viables distintas | RF-032 | Tres configuraciones con tasa 30-70% y perks mayoritariamente distintos | 2-3 |
| Distribución del catálogo de perks | RF-069 | 60/30/10 ±5 puntos | 1+ |
| Perks que acumulan entre partidos | RF-070 | >= 15 en el catálogo de lanzamiento. **Cumplido**: 15 desde el paquete Z | 2+ |
| Arcos de build cerrados por run | ADR 0051 | Una política que persigue un maestro llega a cerrar un arco en una fracción razonable de las runs. **Implementada** en `FullRunMetrics.MastersReached`; medido **5,5%** desde que el maestro solo se compra (ADR 0055), con la banda bajada a >= 2 y la causa en `fase2-diseno.md` §23.5 | 2 |
| Divergencia entre builds con maestros distintos | ADR 0051, RF-032 | Dos runs de la misma raza que toman maestros distintos coinciden en menos perks que dos que toman el mismo; >= 5 puntos de diferencia. **Implementada** en `FullRunMetrics.MasterDivergence`; medido **9,0** | 2 |
| Ganar la run sin entrar en ningún mercado | ADR 0055 | Por debajo del **5%**. **Implementada** en `FullRunMetrics.MarketlessWinRate` (la contextual con `AvoidsMarkets`); medido **23,5%**, por encima incluso de la misma política usando los mercados (20,0%) | 2 |
| Mejores equipos ganan más con sorpresas creíbles | Fase 0, banda revisada por la **ADR 0054** | Equipo **+20** en todos los atributos gana **70-88%**; +10 es informativo y se vigila en 55-70%. Medido 79,52 | 0 |

## Definición de "build" para `/Balance`

Un fichero en `data/balance/builds/<id>.json`: club, plantilla con niveles, perks asignados, objetos, consumibles. Las builds catalogadas son las que RT-055 vigila. Toda build nueva que se diseñe (una raza, un arquetipo) entra aquí antes de darse por terminada.

## Procedimiento al ajustar un número

1. Cambia el valor en `/data` (nunca en código si el valor debería ser dato).
2. Ejecuta el lote de referencia con la misma semilla base que la última medición.
3. Compara `summary.csv` con el anterior. Anota en el commit qué métricas se movieron y por qué.
4. Si una métrica sale de rango y crees que el rango está mal, no toques el rango: abre un ADR.
