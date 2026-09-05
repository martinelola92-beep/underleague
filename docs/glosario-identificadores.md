# Glosario de identificadores (español -> inglés)

Decisión D-12 / ADR 0009: el código, las claves JSON, los nombres de eventos, los ids de datos y las etiquetas van **en inglés**. Los documentos siguen en español y usan el término español seguido del identificador en código. Todo texto que ve el jugador se localiza (es/en) desde `data/l10n/`.

Esta tabla es la referencia única. Si un término no está aquí, se añade aquí antes de usarlo en código.

## Conceptos de run y partido

| Español | Identificador | Notas |
|---|---|---|
| Run | `Run` | |
| Acto | `Act` | |
| Nodo | `Node` | tipos: `LeagueMatch`, `EliteMatch`, `Market`, `Clinic`, `Workshop`, `Training`, `Event`, `Boss` |
| Casilla | `Cell` | `Column`, `Row` |
| Casilla-hogar | `HomeCell` | |
| Correa | `Leash` | atributo y radio |
| Jugada | `Play` | |
| Tramo de jugada | `PlayPhase` | `Recovery`, `Progression`, `FinalThird`, `Finishing` |
| Perk | `Perk` | |
| Etiqueta | `Tag` | |
| Rasgo | `Trait` | |
| Vínculo | `Bond` | tipos: `Partnership` (Sociedad), `BloodDebt` (Deuda de sangre), `Stonewall` (Muro) |
| Plantilla | `Roster` | |
| Alineación | `Lineup` | |
| Semilla | `Seed` | |
| Tick | `Tick` | |
| Oro | `Gold` | |
| Jefe | `Boss` | |
| Modificador de regla | `RuleModifier` | |
| División | `Division` | `Third`, `Second`, `First`, `World` |
| Club | `Club` | |
| Mercado | `Market` | |
| Canterano | `YouthPlayer` | |
| Mercenario | `Mercenary` | |
| Clínica | `Clinic` | |
| Taller de implantes | `Workshop` | |
| Prótesis | `Prosthesis` | |
| Informe de ojeo | `ScoutingReport` | |
| Informe post-partido | `MatchReport` | |
| Distintivo de dificultad | `DifficultyBadge` | |
| Partido excelente | `ExcellentMatch` | |
| Reglamentario | `Regulation` | |
| Gol de oro de la turba | `MobGoldenGoal` | "turba" = `Mob` |

## Jugador

| Español | Identificador |
|---|---|
| Jugador | `Player` |
| Nombre | `Name` |
| Raza | `Race` |
| Posición | `Position`: `Goalkeeper`, `Defender`, `Midfielder`, `Forward` |
| Rareza | `Rarity`: `Common`, `Rare`, `Legendary` |
| Nivel / Experiencia | `Level` / `Experience` |
| Atributos | `Attributes`: `Strength` (fuerza), `Speed` (velocidad), `Technique` (técnica), `Stamina` (resistencia), `Leash` (correa) |
| Estado físico | `PhysicalState`: `Healthy`, `MinorInjury`, `SevereInjury`, `Dead` |
| Salario | `Wage` |
| Equipo (objeto) | `Item` |
| Consumible | `Consumable` |
| Duelo | `Mourning` |
| Contadores | `Counters` |

## Rasgos (`Trait`)

| Español | Id |
|---|---|
| agresivo | `Aggressive` |
| rápido | `Fast` |
| goleador | `Scorer` |
| tiro lejano | `LongShot` |
| cerebral | `Cerebral` |
| sucio | `Dirty` |
| resistente | `Resilient` |
| cobarde | `Coward` |
| líder | `Leader` |
| vago | `Lazy` |
| Gato (portero) | `Cat` |
| Muro (portero) | `Wall` |
| Sale mucho (portero) | `Rusher` |
| Sucio pero discreto | `Discreet` |

## Etiquetas (`Tag`)

Raza, posición y rasgo comparten el sistema de etiquetas (RF-022d): el id del rasgo o de la posición es también su etiqueta.

| Español | Id | Origen |
|---|---|---|
| Neutral | `Neutral` | humanos |
| Bruto | `Brute` | orcos |
| Fino | `Fine` | elfos |
| Muro (enanos) | `Bulwark` | enanos (distinto del rasgo de portero `Wall`) |
| Frío | `Cold` | no-muertos |
| Ponzoña | `Venom` | elfos oscuros |
| Enorme | `Huge` | demonios |
| Sanguijuela | `Leech` | vampiros |
| Escamas | `Scales` | lagartos |
| Chatarra | `Scrap` | prótesis |
| Autómata | `Automaton` | 3 prótesis |
| Descompuesto | `Rotting` | resucitado |
| Extraño | `Stranger` | mercenario |

## Razas (`Race`)

`Human`, `Orc`, `Elf`, `Dwarf`, `Undead`, `DarkElf`, `Demon`, `Vampire`, `Lizard`. Prototipo (D-5): `Human`, `Orc`, `Elf`.

## Rediseño espacial (v0.9.1, ADR 0020-0029)

| Español | Identificador |
|---|---|
| Etiqueta de especie | `SpeciesTag` (`speciesTag` en JSON) |
| Etiqueta de estilo | `StyleTag` (`styleTag`, `styleTagWeights`) |
| Radio de cuerpo | `BodyRadius` (`bodyRadius`) |
| Disciplina | `Discipline` (`discipline`) |
| Zona de acción | `ActionZone` (`actionZone`, con `forward`, `back`, `sides`) |
| Margen exterior | `OuterLimit` (`outerLimitMultiplier`) |
| Separación / empuje | `Separation` / `Push` (`bodies`) |
| Presupuesto de atributos | `Budget` (`budgetByRarity`, `budgetPerLevel`, `positionShare`, `positionFloors`) |
| Habilidad racial | `RaceAbility` (`ability`) |
| Vínculo de colocación | `Link` (`links`: `beside`, `ahead`, `behind`, `left`, `right`, `diagonalAhead`, `diagonalBehind`) |
| Eje de activación | `Axis` (`axis`: `identity`, `accumulation`, `alignment`, `startZone`, `geometry`, `matchState`, `composition`, `proximity`) |
| Buscar espacio / presionar | `FindSpace` / `PressCarrier` |
| Pase corto / pase largo | `ShortPass` / `LongPass` (ADR 0030 §1) |
| Bloqueo sin balón | `Block` (ADR 0030 §2) |
| Jugada activa | `ActivePlay` (`blockActiveRadiusCells`, `blockCorridorHalfWidthCells`) |

## Simulación

| Español | Identificador |
|---|---|
| Simulador | `Simulator.Run(initialState, seed, catalog, config)` |
| Estado del partido | `MatchState` |
| Resultado | `MatchResult` (`Events`, `FinalState`, `Report`) |
| Máquina de estados del partido | `MatchPhase`: `Kickoff`, `OpenPlay`, `Restart`, `Penalty`, `MobGoldenGoal`, `Finished` |
| Estado táctico | `TacticalState`: `InPossession`, `OutOfPossession`, `OffensiveTransition`, `DefensiveTransition` |
| Estado del jugador | `PlayerState`: `Positioning`, `Chasing`, `Dribbling`, `Passing`, `Shooting`, `Tackling`, `KnockedDown`, `Injured`, `Celebrating`, `SentOff`, `Blocking` |
| Acciones | `PlayerAction`: `ChaseBall`, `MarkOpponent`, `OfferSupport`, `CoverSpace`, `Dribble`, `Shoot`, `Tackle`, `Retreat`, `FindSpace`, `PressCarrier`, `ShortPass`, `LongPass`, `Block`. `Pass` se retiró en la ADR 0030 §1: el pase son dos acciones que compiten, corta y larga |
| `Puede(estado, accion)` | `CanPerform(state, action)` |
| Utilidad | `Utility`, `UtilityTable` |
| Árbitro | `Referee` |
| Criterio del árbitro | `Bias` (-100..+100, positivo favorable al usuario) |
| Rasgos de árbitro | `Strict`, `Lenient`, `Homer`, `OneEyed`, `Cowardly`, `Corrupt`, `Incorruptible` |
| Soborno / Denuncia | `Bribe` / `Report` |
| Entrada | `Tackle` |
| Regate | `Dribble` |
| Duelo aéreo | `AerialDuel` |
| Recuperación | `Recovery` |
| Tiro / Parada | `Shot` / `Save` |
| Falta / Tarjeta | `Foul` / `Card` (`Yellow`, `Red`) |
| Lesión / Muerte | `Injury` / `Death` |
| Sustitución | `Substitution` |
| Incomparecencia | `Forfeit` |
| Saque / Reanudación | `Kickoff` / `Restart` (`ThrowIn`, `Corner`, `GoalKick`) |

## Eventos (RF-066)

En C# `EventType` en PascalCase; en `/data` y en logs, `UPPER_SNAKE`.

```
MATCH_START      MATCH_END        MOB_START        REFEREE_LEAVES
PLAY_START       PLAY_END
PASS_ATTEMPTED   PASS_COMPLETED   PASS_FAILED
DRIBBLE_ATTEMPTED DRIBBLE_WON     DRIBBLE_LOST
AERIAL_DUEL      TACKLE           RECOVERY
SHOT             GOAL             SAVE
FOUL             CARD             INJURY           DEATH
SUBSTITUTION     CONSUMABLE_USED
```

Contexto de evento (RF-067): `Actor`, `Target`, `Opponent`, `Cell`, `Zone` (`Own`, `Middle`, `Opposing`), `MatchPhase`, `Bias`, `DistanceToGoal`.

## Datos y perks

| Español | Identificador |
|---|---|
| disparador | `trigger` |
| condición | `condition` |
| efecto | `effect` (`type`, `target`, `attribute`, `value`, `duration`) |
| alcance / objetivo | `target`: `actor`, `target`, `opponent`, `adjacent`, `team`, `opposingTeam`, `withTag:<Tag>` |
| límite | `limit` (`per`: `play`, `match`, `mob`, `run`; `times`) |
| acumula entre partidos | `accumulatesAcrossMatches` |
| letal | `lethal` |
| solo posición | `positionOnly` |
| tipo de perk | `kind`: `filler` (relleno), `conditional`, `ruleBreaker` |
| duración | `duration`: `instant`, `play`, `match`, `run` |
| Funciones NCalc | `hasTag(who, 'Tag')`, `isMob()`, `bias()`, `zone(who)`, `adjacent(who, 'Tag')`, `distanceToGoal()`, `scoreDiff()`, `tick()`, `counter('name')` |
| Arquetipos de objeto | `cursed`, `fragile`, `restricted` |
| Familias de consumible | `medical`, `tactical`, `dirty`, `supernatural` |
| Efectos (catálogo inicial) | `modifyAttribute`, `modifyLeash`, `modifyBias`, `modifyProbability`, `cancelEvent`, `repeatEvent`, `heal`, `injure`, `addCounter`, `gold` |
