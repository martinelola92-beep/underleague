# Tests lentos del bucle de desarrollo — 3 oct 2026

**Objetivo del revisor:** acelerar `tools/test-resumen.sh Sim.Tests -c Release --filter "Category!=Gate&Category!=Diagnostic"`
sin quitar protección. Sólo dos técnicas aprobadas: (2) caso fijo con comprobación previa explícita y (3) compartir
con un fixture lo que varias pruebas de una clase juegan igual. No se reducen muestras a ojo ni se mueve nada a `Gate`.

**Commit base:** `ad53f44`. **Medición (Regla H, procedencia):** `tools/test-resumen.sh` con `Release`, `-m:1`, el
filtro de arriba, sobre el mismo commit base y sobre el HEAD final; las duraciones por clase salen de la suma de
duraciones por prueba del `.trx` (son tiempo de reloj de cada prueba, con las clases corriendo en paralelo entre sí;
sirven para comparar dentro de la misma medición, no como CPU exacta).

## Resultado

La máquina estaba compartida con lotes de `/Balance` y otras suites del otro agente (load average 20-28 durante las
dos mediciones, 4 núcleos), así que **el reloj no es comparable**; se mide el tiempo de CPU del proceso
(`times` del shell que lanza `tools/test-resumen.sh`), que sí lo es razonablemente. Mismo filtro, mismo `-c Release -m:1`,
HEAD frente a `ad53f44` (mismos 1.899 tests verdes los dos):

| | CPU (user+sys) | Reloj (con carga) |
|---|---|---|
| Base `ad53f44` | 13 m 15 s + 11 s = **806 s** | 4 m 47 s |
| HEAD | 12 m 27 s + 12 s = **759 s** | 5 m 04 s (load 28) |

Ahorro medido: **~47 s de CPU (~6 %)**, a repartir entre 4 hilos: ~12 s de reloj en una máquina libre. Es menos de lo
que sugería la lista de candidatos porque **las clases más caras son estadísticas o «nunca»** (ver abajo) y ahí sólo
valían las dos técnicas aprobadas. Una medición de reloj limpia (máquina libre) queda por hacer; una primera con la
máquina libre de antes de empezar dio 4 m 31 s de base (879 CPU-s según el revisor).

Reloj por clase tocada, base → HEAD (suma de duraciones por prueba del `.trx`, ambas con carga alta, orientativo):
PreResolutionParticipants 111 → 50, GuardShot 28 → 11, ShotAperture 17 → 11, ShotHeight 18 → 6, ConsumableLoop 10 → 7,
Steamroller 13 → 6, EndToEnd 26 → 20. `ReboundShotTests` salió 30 → 52 en estas mediciones por la carga (su trabajo
bajó de 2.300 a 2.050 partidos): hay que repetirla con la máquina libre antes de darle crédito.

## Qué se cambió (técnica 3 en todos los casos)

Ninguna clase admitió la técnica 2: las que buscan «un caso donde pase X» (`AShotArcsInsteadOfTravellingFlat`, que
ya corta en la primera semilla que lo cumple; `TheBallLeavesTheGroundDuringShots`, 0,6 s) son baratas, y las caras
afirman «nunca pasa X en N partidos» o comparan tasas, donde un caso fijo no sustituye a la muestra. Por eso todo lo
que sigue es compartir. En todos los fixtures: el resultado compartido es función pura de la clave (semilla, perk,
slot…), xUnit ejecuta las pruebas de una clase una detrás de otra (sin acceso concurrente; el `lock` es por si
alguna vez no lo fuera), el fixture es de clase y se suelta al terminar, y nada mutable se comparte entre clases
ni entre hilos del arnés. No cambia ningún assert de comportamiento.

| Clase | Antes (s) | Técnica | Qué protege | Qué se comparte |
|---|---|---|---|---|
| `PreResolutionParticipantsTests` | 98 | 3 | BM-B (ADR 0180): una resolución publicada antes de tirarse mira a sus participantes después; nadie acaba un tick derribado con el balón; `ankle_bite` no entra contra la víctima que ya no está; la mordida que lesiona se pita al mismo ritmo | Los partidos `(índice, perk, slot)` que repiten `ATackleThatWon…` y `AMissedTackle…` (charge, bull_rush × 300) y `AVictimInjured…` y `TheBiteThatInjures…` (`ankle_bite` × 5 slots × 300). Se guardan **sin traza** (con traza 1.500 partidos pasaban de 3 GB; sin ella ~220 MB); la prueba que necesita la traza (`NobodyEndsATick…`) sigue jugando los suyos |
| `GuardShotTests` | 24 | 3 | BC-D (ADR 0181): `last_man` sólo se activa con un tiro rival a puerta, una vez por partido, al ritmo del dato; tras la tirada fallida el bloqueo genérico sigue actuando | Los 750 partidos reales con `last_man` (3 defensas × 250) que recorren `InRealMatches…` y `AfterAFailedRoll…` |
| `EndToEndProtocolDemoTests` | 28 | 3 | El protocolo de balanceo es ejecutable de punta a punta (tripleta de Tuning, réplica, determinismo, registro) | Los lotes emparejados de Tuning (valores 40/60/100, semilla 2) y la réplica (semilla 3) que juegan las dos pruebas |
| `ReboundShotTests` | 44 (30 en la 2.ª medición) | 3 | BC-C (ADR 0180): cada activación de `double_shot`/`point_blank` sigue a un rebote del propio tiro y precede a otro tiro; límite y enfriamiento; dos perks de rebote no gastan los dos | `double_shot` en el slot 6 (índices 0..249 los juegan la prueba por perk y la del límite) |
| `ShotApertureTests` | 14 | 3 | ADR 0135/0138: un tiro sin ángulo no convierte mejor que la media; el término de apertura cuesta tiros a puerta | Los 400 informes de `WithAperture()` (penalización 2000) que leen `ShotsWithoutAngle…` y `TheApertureTerm…`; el brazo apagado (penalización 0) sólo lo juega una |
| `SteamrollerConditionTests` | 11 | 3 | BB-Q: `steamroller` se activa y sigue activándose menos que su gemelo `charge` | Los dos lotes de 200 partidos (`charge`, `steamroller`) que cuentan `SteamrollerFires…` y `SteamrollerChains…` |
| `ShotHeightTests` | 11 | 3 | ADR 0135 paso 2: los goles no van todos al centro; el marco rechaza y no cuenta como gol | Las observaciones por semilla (filas de gol, tiros, tiros al marco, problemas) de los partidos 1..300/1..400 con traza; se guardan las observaciones y no la traza |
| `ConsumableLoopTests` | 10 | 3 | CAT-B: los consumibles se compran y llegan al partido | Las doce runs contextuales (semillas 1..12) que leen `ThePolicyBuysConsumables` y `TheConsumablesBoughtActuallyReachAMatch`. `EquippingKeepsTheRunDeterministic` sigue jugando dos veces la semilla 7 a propósito (compara dos ejecuciones) |

## Lo que se examinó y NO se toca (con motivo)

| Clase | s | Por qué no |
|---|---|---|
| `RunPolicyItemSlotTests` | 185 | Ya comparte con una memoria (`Memo`) todo lo que se repite; quedan 11 lotes de 48 runs **distintos** (doctrina × listón × tabla). Las afirmaciones son «con el listón inalcanzable no se compra ni un objeto», «la puerta nunca bloquea a una doctrina que no mira el valor» e igualdades lote a lote: «nunca pasa X en N runs», que un caso fijo no sustituye. El `Runs = 48` está justificado en el propio fichero (el mínimo que dejó de cruzar a cero, medido). Es la clase que fija el camino crítico del bucle |
| `PenaltyAreaSymmetryTests` | 62 | Estadística de verdad: reparto de penaltis entre las filas 1 y 5 en 9.000 partidos (con un mínimo de muestra que el propio test exige). Ya comparte el lote con un `Lazy` |
| `AnkleBitePriceTests` | 35 | Compara tasas con y sin perk (faltas ×1,15, lesiones causadas): estadística. Los 1.500 partidos «con perk» coinciden con los de `PreResolutionParticipantsTests`, pero son de otra clase y se jugarían sin traza; compartirlos pediría un estado estático entre clases |
| `RunPolicySellTests` | 37 | `AWholePolicyRunNeverTriesToSellASigningWhoHasNotPlayed`: «nunca lanza en 60 runs completas». El bug original sólo se reproducía en 9 de 60 y ya no se puede saber cuáles sin revertir el arreglo; un caso fijo sería otra afirmación |
| `RunNamesTests` | 33 | Casi todo el coste es `AWholeRunEndsWithoutARepeatedName` (36 runs, «ninguna acaba con repetidos») y los mercados de `NobodyOnTheClubSide…` (200 plantillas × 30 mercados): «nunca». No hay ningún lote repetido entre pruebas |
| `ConsumableTargetTests` | 29 | Compara tasas de lesión grave del rival y las propias con y sin la emboscada (600 partidos × 3 brazos) y valida el instrumento contra el caso mal apuntado (Regla J): estadística |
| `RunPolicyBlacksmithTests` | 21 | `AWholePolicyRun…` (40 runs) y `TheNoBlacksmithControl…` (12 + 12) comparten sólo las semillas 1..12 del brazo real, pero la primera lleva un observador con asserts; compartir ahorraría ~4 s y cambiaría dónde fallan los asserts del observador. No compensa |
| `BetHitCensusTests` | 12 | Probado y **revertido**: compartir las runs `Never`/`Blind` entre sus tres pruebas añadía un observador a las que no lo llevaban y ahorraba 0,8 s de 13 s medidos en aislamiento |
| `BetCensusTests`, `BetConditionsRealMatchTests` | 9 / 7 | Son comparaciones de determinismo y de equivalencia (observador vs. sin observador, paralelo vs. secuencial); jugar dos veces es la prueba. El lote de partidos reales de `BetConditions…` ya está compartido por un `Lazy` |
| `LethalPerkTests` | 9 | Compara cuánto muere el tocado frente a cualquier sano en 360 partidos: estadística |
| `TerritorialBalanceTests`, `MobNarrowingTests`, `FateRollTests`, `AttributeBiasChannelTests`, `MatchResolutionDeathIsTerminalTests` | ≤ 21 | Tasas por perk/raza o «nunca» sobre partidos y runs reales; sin lotes repetidos entre pruebas |

## Candidatos que no son de las dos técnicas (decisión del revisor, no mía)

`CalibrationDiagnosticsTests` (14 s), `FaseBMeasurementTests` (14 s), `CannonUtilityDumpTests` (7 s),
`PerkInjuryCensusTests` (8 s) y `ScreeningRunnerSmokeTests` (16 s) parecen sondas de medición (imprimen, barren,
comparan con una expectativa de la fase de balance) más que pruebas con asserts de comportamiento. Si el revisor
las reclasifica como `Category=Diagnostic` el bucle bajaría ~60 s de reloj, pero eso es mover protección y no se
ha hecho. Otra palanca, fuera de las dos aprobadas: `RunPolicyItemSlotTests` juega sus lotes en serie dentro de un
solo hilo y es el camino crítico de la suite; repartir cada lote con `Parallel.For` (semilla = función pura del
índice, un `Catalog` por hilo) acortaría el reloj sin bajar la CPU.
