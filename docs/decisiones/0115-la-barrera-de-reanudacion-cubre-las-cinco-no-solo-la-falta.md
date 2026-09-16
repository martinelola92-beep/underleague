# 0115. La barrera de distancia de una reanudación cubre las cinco, no solo la falta

**Fecha:** 2026-09-16
**Estado:** Aceptada e implementada (`Sim/Engine/MatchEngine.cs`, `Sim/Data/Catalog.cs`, `Sim/Data/DataLoader.cs`, `data/sim/tuning.json`, `data/schemas/tuning.schema.json`)
**Decisión del revisor**, tras tres intentos y dos rondas de revisión independiente (BB-B, `docs/pendientes/BB-B.md`, `docs/analisis/bb-b-barrera-geometrica-diseno.md`). **Generaliza la ADR 0090** (que aceptó la barrera solo para el saque de falta) a `ThrowIn`/`GoalKick`/`Corner`/`Kickoff`/`FreeKick`; el penalti queda expresamente fuera (RF-054, sin tocar). Renombra `restart.freeKickClearanceCells` a `restart.restartClearanceCells` (RT-057: no es un ajuste silencioso, queda escrito aquí).
**Requisitos:** RF-052, RF-053, RF-054, RF-057, RT-056, RT-057

## El problema

«En el saque de centro los defensores van a robar el balón antes de que esté en juego» (revisor, BB-B).
Confirmado en código: durante la ventana de reanudación solo el sacador quedaba congelado; el resto del
motor seguía evaluando `Tackle`/`Block` con normalidad, sin ninguna condición que reconociera que el balón
seguía "muerto" en la práctica aunque `ctx.BallDead` ya fuera falso. La ADR 0090 ya había resuelto
exactamente esto para el saque de falta (una barrera de distancia de 2,0 casillas durante la cuenta atrás),
pero nunca se generalizó, y el saque de centro no tenía ningún equivalente del círculo central.

## Dos intentos rechazados antes de este

1. **Inmunidad temporal** (`UtilityContext.KickoffPending`): descartaba `Tackle` durante un plazo fijo tras
   el saque, sin mover a ningún jugador. Rechazada: el rival seguía viéndose pegado al sacador en pantalla
   (0,75 casillas de media) mientras la regla que lo protegía era invisible — viola "observable >
   invisible". Rota además: dos puertas estadísticas en rojo sin medir antes de proponer el cierre.
2. **Barrera geométrica generalizada, primera versión**: correcta en el radio, pero (a) la guarda del
   motor no distinguía tipos de reanudación y la barrera se colaba en el penalti sin que nadie lo midiera
   ni decidiera (RF-054); (b) sin techo de duración, un sacador que retiene el balón mucho tiempo (medido
   37 ticks reales) la deja activa indefinidamente; (c) la métrica de aceptación contaba **cualquier**
   `Tackle` del partido durante la ventana como violación, sin filtrar por cercanía al sacador ni al balón,
   así que un bloqueo contra un compañero al otro lado del campo se contaba como "robar el saque". Rota:
   `betterTeamWinRate_human_60_vs_human_40` (métrica **obligatoria** de RT-056) por la fuga al penalti.

## Lo que se implementa (tercer intento)

Generaliza el mecanismo de la ADR 0090, con tres correcciones sobre el segundo intento:

1. **`IsClearanceRestart(RestartKind)`** (expuesta `internal`, probada directamente): decide qué
   reanudaciones llevan la barrera —las cinco de `TakeRestart` (AZ-A), nunca el penalti, que tiene su
   propio `TakePenalty`—. La guarda del motor comprueba el tipo explícitamente en vez de asumir que el
   penalti nunca coincidirá con el estado de la barrera por casualidad de temporización.
2. **Techo de duración** (`RestartClearanceMaxTicks`, 30 ticks ≈ 2 s, lectura de RF-052): si el sacador
   retiene el balón más de la cuenta, la barrera se libera. Sin ella, la protección se volvería un escudo
   de duración libre en cuanto exista un perk que premie retener el balón (todavía no existe; ver
   `game-design-review` más abajo).
3. **`_restartClearanceOwner`** se limpia al empezar **cualquier** reanudación nueva (`BeginRestart`), no
   solo cuando cambia el dueño del balón: una falta resuelta a mitad del bucle de jugadores puede programar
   un penalti mientras el sacador anterior seguía teniendo el balón, y sin este guard la barrera de la
   reanudación ya cerrada se colaba un tick en el penalti nuevo.

El radio (`restartClearanceCells`, 2,0 casillas) no cambia: sigue siendo el valor que la ADR 0090 ya
midió y aceptó, sin búsqueda de parámetros nueva — supera con margen tanto `tackleDistanceMaxCells` (1,0)
como `blockReachMaxCells` (1,2).

## Lo que NO se implementa, y por qué

- **Protección de los compañeros del sacador** contra `Block`/marcaje sin balón lejos del balón: medido
  (200 semillas) en 38 casos dentro de las ventanas de reanudación, pero es contacto normal de juego
  abierto —RF-057 ya lo permite si está en la jugada activa— y no el problema literal de BB-B ("robar el
  saque"). Queda fuera del alcance de esta ADR; si en el futuro se decide que también es un problema,
  necesita su propio análisis y medición, no una ampliación silenciosa de esta barrera.
- **Evento explícito o liberación gradual al expirar el techo de duración**: `game-design-review` (16 sep
  2026) lo evaluó y decidió no añadirlo todavía —el caso (varios rivales convergiendo de golpe) ocurre hoy
  sin que exista ningún perk de retención de balón que lo explote, 1-2 veces en 200 partidos, muy por
  debajo de las retenciones típicas. Queda como **ítem obligatorio a revisar cuando el catálogo C1/C2
  diseñe un perk de retención de balón**: a partir de ahí el colapso brusco deja de ser un caso raro de
  juego abierto y pasa a ser la consecuencia legible de una elección de build, y RF-012d exige que sea
  previsible en pantalla, no solo en el código.

## Lo que se mide

200 semillas, `Sim.Tests/Engine/RestartClearanceTests.cs`:

| | antes de la barrera | con esta ADR |
|---|---|---|
| Disputas (`Tackle` real) contra el sacador, cinco reanudaciones | 33 | **0** |
| Distancia mínima rival→balón, dentro del techo | 0,008-0,125 | 1,595-1,873 |
| Penalti: distancia mínima rival→balón durante la cuenta atrás | 0,060 | 0,060 (sin cambio) |

43 puertas estadísticas (semilla 1, una invocación, **tras cerrar la fuga del §17 en `BeginRestart`**):
**4 rojas de 43**, mejor que las 5 de antes de tocar BB-B pero una más que las 3 que medía este documento
antes de esa corrección — la propia corrección desplaza el consumo de RNG lo suficiente para mover números
de builds concretos, la misma clase de efecto que ya se documentó para el bugfix de ADR 0090
(`docs/pendientes/BB-B.md` tiene la tabla completa de las cuatro composiciones distintas a lo largo de la
sesión). Ninguna de las cuatro es atribuible al propósito de esta barrera —cerrar el robo del saque—:

- `BadBuildsLoseToTheirBaseline` (`elf_out_of_zone`=49,38; antes `orc_misplaced`/`elf_brawler` u
  `elf_out_of_zone`=48,12 según qué corrección estaba puesta al medir): herencia de un bugfix
  independiente y anterior a esta ADR (`Utility.ClampToArea` aplicado sin comprobar `IsOutfield`, heredado
  de la propia ADR 0090). Decisión de rango pendiente, aparte.
- `CoherentBuildsBeatTheirBaseline` (`orc_violence`=56,67, rango ≥58): **nueva tras cerrar la fuga del
  penalti**, no aparecía en ninguna medición anterior de esta sesión. Un build concreto por 1,33 puntos,
  con el mismo patrón que la de arriba (RNG desplazado por un cambio real y correcto en `/Sim`, no una
  regresión de comportamiento).
- `TheThreeDoctrinesBuyDifferently` (`contextual`=1,16 vs `saver`=1,18, antes 1,19): la misma puerta rompió
  el primer intento (mecanismo completamente distinto). Medido con `--full-runs 240` en tres semillas: la
  semilla 1 está justo en el punto de vuelco de una magnitud (`contextual − saver`) cuya dispersión entre
  semillas es del tamaño de su propio valor. Señal de que la puerta necesita margen o varias semillas, no
  de que la barrera desequilibre las doctrinas.

`betterTeamWinRate_human_60_vs_human_40` (métrica obligatoria de RT-056, rota en el segundo intento por la
fuga al penalti): verde, y sigue verde tras cerrar la fuga del §17.

**Patrón que se repite y que conviene anotar aparte de esta ADR**: cada corrección de esta sesión —el
bugfix de ADR 0090, la barrera en sí, el cierre de la fuga del penalti— ha movido qué builds concretos
cruzan el techo/suelo de sus puertas, sin que ninguna de las tres tenga relación causal con el contenido
de esos builds. Es la firma de puertas de un solo partido/semilla operando cerca de su margen, no de un
problema de diseño en cada cambio. Candidato a su propia revisión de las puertas de build (margen o varias
semillas), fuera del alcance de esta ADR.

**Lote de `/Balance`** (RT-054, `dotnet run --project Balance -- --runs 2000 --seed 1`, equipo de
referencia, base contra el mismo árbol con esta ADR):

| Métrica | Base | Con esta ADR | Banda |
|---|---|---|---|
| `possessionChanges` | 20,35 | 20,43 | 12-28 |
| `passChainAvgLength` | 2,00 | 2,00 | 1,8-3,5 |
| `shotsPerMatch` | 8,46 | 8,46 | 7-15 |
| `tacklesPerMatch` | 11,64 | 11,68 | 6-14 |
| `injuriesPerMatch` | 0,85 | 0,84 | 0,3-0,9 |
| `goalsPerMatch` | 2,68 | 2,68 | INFO |
| `foulsPerMatch` | 7,54 | 7,55 | INFO |

Sin movimiento fuera del ruido de 2.000 partidos en ninguna métrica: la ventana de reanudación es una
fracción tan pequeña del partido que el efecto medido dentro de ella (menos faltas y entradas durante esos
pocos ticks, ver `docs/analisis/bb-b-barrera-geometrica-diseno.md` REGRESSION RISK del segundo intento) no
se nota en el agregado por partido. Coherente con las 43 puertas.

## Consecuencias

- El córner nunca ocurre en la muestra medida (0 en 400 partidos entre las dos rondas de revisión):
  hallazgo aparte, `docs/pendientes/BB-N.md`, sin relación causal con esta ADR.
- Instrumentar la ventana de reanudación (para medir esta ADR) destapó un bug preexistente y ajeno,
  `docs/pendientes/BB-O.md`: un jugador que sale del campo reteniendo el balón puede congelar el partido.
- Pendiente, sin bloquear esta ADR: una captura de `visual-review` que demuestre en pantalla la propiedad
  "observable > invisible" que la motiva frente a la inmunidad temporal rechazada.
- **Efecto de segundo orden encontrado después, en BA-N** (ADR 0116, `independent-reviewer`) —
  **corregido dos veces tras revisiones sucesivas, que refutaron las redacciones anteriores**: entre
  `ad3c472` (medido 1,7) y `99a22c2` (medido 3,0) — un tramo que incluye `ea1530a`, `d383328`, `088c5ba`,
  `ae5a20b`, `69e0952`, la barrera geométrica puesta y revertida — `EquippingAGoodBuildIsWorthSeveral-
  PointsOfWinRate` ya se había movido 1,3 de los 1,7 puntos totales, **antes de que existiera el arreglo
  de la fuga del penalti** (`3dd0b6d`, dentro del tramo siguiente). El tramo entre `99a22c2` y `f1ce8b3`
  (medido 3,4) incluye ese arreglo junto con `b4ba669` y `8408671`, ninguno de los tres medido por
  separado, así que tampoco ahí se puede aislar cuál lo movió. **No se atribuye a ninguno de los cambios
  en concreto** —el primer intento de anotarlo aquí decía que lo causó el cierre de la fuga del penalti, y
  la propia secuencia de medidas lo desmiente—. Es la misma firma que ya documentaba este párrafo en
  general ("desplaza el consumo de RNG lo suficiente para mover números de builds concretos, **sin que
  ninguno tenga relación causal con el contenido de esos builds**"), confirmada con un caso medido y ajeno
  a esta ADR, no una causa nueva que añadir. Ver `docs/pendientes/BB-P.md`.
