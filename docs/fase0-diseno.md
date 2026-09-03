# Fase 0: diseño de implementación

Especificación cerrada para implementar el simulador sin gráficos. Concreta `simulacion.md`, `determinismo.md`, `arquitectura.md` y `balance.md` al nivel de tipos, firmas y fórmulas. Los subagentes implementan **contra este documento**; si algo no está aquí, se pregunta al orquestador, no se improvisa.

Convenciones: identificadores en inglés (`glosario-identificadores.md`). Todos los enteros son `int` salvo semillas (`ulong`). Probabilidades en base 10000 salvo que se diga "porcentaje". Posiciones en `float`. Ninguna colección sin orden se itera. Ningún `System.Random`, `DateTime`, `Guid`, `Parallel`, `File`.

## 1. Estructura de `/Sim`

```
Sim/
  Random/Pcg32.cs            struct Pcg32, SplitMix64, RngStreams
  Model/Enums.cs             Race, Position, Rarity, PhysicalState, Trait, RefereeTrait, Zone
  Model/Attributes.cs        readonly record struct Attributes
  Model/Cell.cs              readonly record struct Cell; static class Pitch
  Model/PlayerDefinition.cs  sealed record PlayerDefinition
  Model/TeamSetup.cs         sealed record TeamSetup, Lineup, LineupSlot
  Model/MatchSetup.cs        sealed record MatchSetup, RefereeSetup
  Events/EventType.cs        enum EventType (catálogo RF-066)
  Events/MatchEvent.cs       sealed record MatchEvent
  Engine/Vec2.cs             readonly record struct Vec2
  Engine/MatchPhase.cs       enums MatchPhase, TacticalState, PlayerState, PlayerAction
  Engine/StateMachine.cs     static class StateMachine (CanPerform, duraciones)
  Engine/MatchPlayer.cs      sealed class MatchPlayer (estado en partido)
  Engine/Ball.cs             sealed class Ball
  Engine/Utility.cs          static class Utility (puntuación de acciones)
  Engine/MatchEngine.cs      internal sealed class MatchEngine (bucle de ticks)
  Engine/MatchReport.cs      sealed class MatchReport, PlayerMatchStats, UtilityDump
  Engine/Simulator.cs        public static class Simulator; sealed record SimConfig, MatchResult
  Data/Catalog.cs            sealed record Catalog, RaceDefinition, TraitDefinition, AiWeights, Tuning
  Data/DataLoader.cs         static class DataLoader (System.Text.Json, desde strings)
  Analysis/MatchMetrics.cs   sealed record struct MatchSummary; static class MatchMetrics (métricas RT-056, sin E/S)
  Generation/NameGenerator.cs
  Generation/PlayerGenerator.cs
  Generation/TeamGenerator.cs
```

`Sim.csproj` no añade paquetes en fase 0 (`System.Text.Json` viene con el framework). Namespace raíz `Underleague.Sim`, subnamespaces por carpeta.

## 2. Tipos públicos

### 2.1 Random

```csharp
namespace Underleague.Sim.Random;

/// PCG32 (Melissa O'Neill). Estado 64 bits, incremento 64 bits (impar), salida 32 bits.
public struct Pcg32
{
    public Pcg32(ulong seed, ulong stream);           // pcg32_srandom_r: state=0; inc=(stream<<1)|1; Next(); state+=seed; Next();
    public uint Next();                                // oldstate*6364136223846793005 + inc; xorshifted = ((old>>18)^old)>>27; rot = old>>59; rotr32
    public int Range(int minInclusive, int maxExclusive); // sin sesgo: rechazo de Lemire o módulo con umbral; documentar cuál
    public bool Chance(int probabilityBase10000);     // Range(0,10000) < p
    public int Percent(int probabilityPercent);       // Range(0,100) < p ? 1 : 0  (helper)
    public T Pick<T>(IReadOnlyList<T> items);
    public void Shuffle<T>(IList<T> items);           // Fisher-Yates desde el final
    public ulong State { get; }                        // solo lectura, para tests
}

public static class SplitMix64 { public static ulong Next(ref ulong state); public static ulong Mix(ulong seed, ulong salt); }

public static class RngStreams
{
    // Cada flujo se deriva de (runSeed, kind, index) con SplitMix64.Mix; kind es una constante distinta por flujo.
    public static Pcg32 Match(ulong runSeed, int nodeIndex);
    public static Pcg32 Map(ulong runSeed, int act);
    public static Pcg32 Rewards(ulong runSeed, int nodeIndex);
    public static Pcg32 Generation(ulong runSeed, int index);
}
```

Vectores de prueba: `new Pcg32(42, 54)` produce `0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e` (demo oficial de PCG).

### 2.2 Model

```csharp
namespace Underleague.Sim.Model;

public enum Race { Human, Orc, Elf, Dwarf, Undead, DarkElf, Demon, Vampire, Lizard }
public enum Position { Goalkeeper, Defender, Midfielder, Forward }
public enum Rarity { Common, Rare, Legendary }
public enum PhysicalState { Healthy, MinorInjury, SevereInjury, Dead }
public enum Trait { Aggressive, Fast, Scorer, LongShot, Cerebral, Dirty, Resilient, Coward, Leader, Lazy, Cat, Wall, Rusher }
public enum RefereeTrait { Neutral, Strict, Lenient, Homer, OneEyed, Cowardly, Corrupt, Incorruptible }
public enum Zone { Own, Middle, Opposing }

public readonly record struct Attributes(int Strength, int Speed, int Technique, int Stamina, int Leash)
{
    public int Get(AttributeKind kind); public Attributes With(AttributeKind kind, int value);
    public static Attributes Clamp(Attributes a); // 1..99
}
public enum AttributeKind { Strength, Speed, Technique, Stamina, Leash }

public readonly record struct Cell(int Column, int Row);

public static class Pitch
{
    public const int Columns = 16, Rows = 5;
    public const int AreaColumns = 2, AreaRows = 3;      // área: 2 columnas desde la línea de gol, filas 1..3
    public static bool IsInArea(Vec2 p, int team);       // team 0 defiende x<2; team 1 defiende x>14
    public static Vec2 GoalCenter(int attackingTeam);    // team 0 ataca (16, 2.5); team 1 ataca (0, 2.5)
    public static int AttackDirection(int team);         // +1 / -1
    public static Zone ZoneOf(Vec2 p, int team);         // tercios respecto al equipo: Own = su tercio defensivo
    public static Vec2 CellCenter(Cell c);
    public static Cell CellOf(Vec2 p);                   // clamp a la cuadrícula
}

public sealed record PlayerDefinition(
    int Id, string Name, Race Race, Position Position, Rarity Rarity, int Level,
    Attributes Attributes, IReadOnlyList<Trait> Traits, IReadOnlyList<string> Tags, PhysicalState PhysicalState)
{
    public bool HasTag(string tag);   // Tags contiene raza, posición y rasgos como strings (RF-022d)
}

/// Coordenadas relativas al equipo: Column 0..7 desde la propia portería, Row 0..4. El motor refleja para el equipo 1.
public sealed record LineupSlot(int PlayerId, Cell HomeCell);
public sealed record Lineup(IReadOnlyList<LineupSlot> Slots)
{
    public static Lineup Default(IReadOnlyList<PlayerDefinition> starters); // GK (0,2); DEF (2,1),(2,3); MID (4,0),(4,2),(4,4); FWD (6,2); si faltan/sobran, rellena por posición en orden de id
}
public sealed record TeamSetup(string Id, string Name, Race Race, IReadOnlyList<PlayerDefinition> Players, Lineup Lineup)
{
    // Players incluye titulares y suplentes; Lineup dice quién juega. Validación: 5..7 titulares, exactamente 1 portero alineado, casillas en 0..7 x 0..4 sin repetir.
}
public sealed record RefereeSetup(string Name, RefereeTrait Trait, int InitialBias);
public sealed record MatchSetup(TeamSetup Home, TeamSetup Away, RefereeSetup Referee);
```

### 2.3 Events

```csharp
namespace Underleague.Sim.Events;

public enum EventType { MatchStart, MatchEnd, MobStart, RefereeLeaves, PlayStart, PlayEnd,
    PassAttempted, PassCompleted, PassFailed, DribbleAttempted, DribbleWon, DribbleLost,
    AerialDuel, Tackle, Recovery, Shot, Goal, Save, Foul, Card, Injury, Death, Substitution, ConsumableUsed }

public static class EventTypeNames { public static string ToUpperSnake(EventType t); } // MATCH_START...

/// -1 en cualquier id no aplicable. Team es el equipo del Actor.
public sealed record MatchEvent(
    EventType Type, int Tick, int Team, int Actor, int Target, int Opponent,
    Cell Cell, Zone Zone, MatchPhase Phase, int Bias, int DistanceToGoal, string Detail);
```

`Detail` es texto corto en inglés y estable (se usa en tests y CSV): p. ej. `"won"`, `"missed"`, `"yellow"`, `"red"`, `"minor"`, `"severe"`, `"offTarget"`, `"penalty"`, `"throwIn"`, `"corner"`, `"goalKick"`, `"kickoff"`, `"forfeit"`, `"goldenGoal"`.

### 2.4 Engine (tipos públicos)

```csharp
namespace Underleague.Sim.Engine;

public readonly record struct Vec2(float X, float Y) { +, -, * escalar, Length, Distance, Normalized (0 si longitud 0), Lerp }

public enum MatchPhase { Kickoff, OpenPlay, Restart, Penalty, RegulationEnd, MobGoldenGoal, Finished }
public enum TacticalState { InPossession, OutOfPossession, OffensiveTransition, DefensiveTransition }
public enum PlayerState { Positioning, Chasing, Dribbling, Passing, Shooting, Tackling, KnockedDown, Injured, Celebrating, SentOff }
public enum PlayerAction { ChaseBall, MarkOpponent, OfferSupport, CoverSpace, Pass, Dribble, Shoot, Tackle, Retreat }

public static class StateMachine
{
    /// Tabla explícita (RT-089c). Positioning/Chasing: acciones sin balón. Dribbling: acciones con balón. Resto: ninguna.
    public static bool CanPerform(PlayerState state, PlayerAction action);
    public static IReadOnlyList<PlayerAction> LegalActions(PlayerState state);   // en orden del enum
    public static bool IsDecisionState(PlayerState state);                      // Positioning, Chasing, Dribbling
}

public sealed record SimConfig(bool CollectLog = true, (int PlayerId, int Tick)? DumpUtility = null, int? RegulationTicksOverride = null)
{ public static SimConfig Default { get; } }

public sealed record MatchResult(IReadOnlyList<MatchEvent> Events, MatchReport Report);

public static class Simulator
{
    /// Puro. Valida el setup (lanza ArgumentException con mensaje claro) y ejecuta el partido completo.
    public static MatchResult Run(MatchSetup setup, ulong seed, Catalog catalog, SimConfig config);
}

public sealed class MatchReport
{
    public int[] Goals { get; }                 // [2]
    public int Winner { get; }                  // 0 o 1, nunca empate (gol de oro; si se agota, gana el equipo con más tiros a puerta, luego el visitante... ver 3.9)
    public int Ticks { get; }
    public bool WentToGoldenGoal { get; }
    public bool Forfeit { get; }
    public int PossessionChanges { get; }
    public int PassChains { get; }              // número de cadenas
    public int PassChainTotalLength { get; }    // suma de longitudes (pases completados consecutivos por posesión)
    public int[] Shots { get; }                 // [2]
    public int[] ShotsOnTarget { get; }         // [2]
    public int Tackles { get; }
    public int Fouls { get; }
    public int YellowCards { get; }
    public int RedCards { get; }
    public int Injuries { get; }
    public int Deaths { get; }
    public int[] BallTicksByThird { get; }      // [3] en coordenadas absolutas: x<16/3, medio, x>32/3
    public int[] PossessionTicks { get; }       // [2]
    public int FinalBias { get; }
    public IReadOnlyList<PlayerMatchStats> Players { get; }
    public IReadOnlyList<string> Log { get; }   // RF-121, vacío si CollectLog=false
    public UtilityDump? UtilityDump { get; }    // RT-098
}
public sealed record PlayerMatchStats(int PlayerId, int Team, int Goals, int Assists, int Shots, int PassesAttempted, int PassesCompleted,
    int Tackles, int TacklesWon, int Fouls, int Cards, bool Injured, int TicksOnPitch);
public sealed record UtilityDump(int PlayerId, int Tick, PlayerState State, IReadOnlyList<UtilityRow> Rows, PlayerAction Chosen);
public sealed record UtilityRow(PlayerAction Action, int Score, int Base, int TacticalMultiplier, int TraitMultiplier, int Context, bool LeashFiltered);
```

### 2.5 Data

```csharp
namespace Underleague.Sim.Data;

public sealed record LocalizedName(string Es, string En);
public sealed record RaceDefinition(Race Id, LocalizedName Name, string Tag, bool Launch, int CellsOccupied,
    Attributes AttributeBias, int IndividualDeviation, IReadOnlyList<(Trait Trait, int Weight)> TraitWeights,
    IReadOnlyList<string> FirstNames, IReadOnlyList<string> LastNames);
public sealed record TraitDefinition(Trait Id, LocalizedName Name, IReadOnlyList<(PlayerAction Action, int MultiplierPercent)> ActionMultipliers,
    int HardTackleBonus, int SpeedBonusPercent, int ShotQualityBonus, int ShootRangeBonusCells, int PassQualityBonus,
    int FoulChanceBonus, int InjuryChanceBonus, int FatigueResistancePercent, int InjuryResistanceBonus,
    int AdjacentTeammateBonusPercent, int SaveBonusClose, int SaveBonusFar, int LeashBonus, bool GoalkeeperOnly);
public sealed class AiWeights
{
    public int Base(Position p, PlayerAction a);
    public int Tactical(TacticalState s, PlayerAction a);      // porcentaje
    public AiContext Context { get; }                          // record con cada clave de "context" de weights.json
    public BlockShift Shift(TacticalState s);                  // (float Shift, int SpeedTicks)
}
public sealed record Tuning(...)   // un campo por clave de tuning.json, anidado por sección, nombres idénticos en PascalCase
public sealed record Catalog(IReadOnlyList<RaceDefinition> Races, IReadOnlyList<TraitDefinition> Traits, AiWeights Ai, Tuning Tuning)
{
    public RaceDefinition Race(Race id); public TraitDefinition Trait(Trait id);
}

public static class DataLoader
{
    /// files: ruta relativa a /data ("races/human.json") -> contenido. Sin E/S. Lanza DataException con fichero y ruta JSON en cualquier error.
    public static Catalog FromJson(IReadOnlyDictionary<string, string> files);
}
public sealed class DataException : Exception { public string File { get; } public string JsonPath { get; } }
```

Las claves `_doc` de los JSON se ignoran. Claves desconocidas dentro de `context`, `tuning` o `traits` son error (así un typo no pasa en silencio, RT-032).

Constantes de resolución que el motor tenía como `private const` y que desde el paquete E son datos de
`tuning.json` (ningún número que el balance pueda querer mover vive en el código):
`assistWindowTicks` (60), `dribble.lostKnockdownTicks` (6), `shot.penaltyQualityBonus` (15),
`save.qualityWeight` (60), `tackle.hardTackleYellowBonus` (1500), `tackle.hardTackleRedBonus` (200).
Se añaden además `states.TackleCooldownTicks` (§3.5) y `generation.leashBase` (§2.6).

### 2.6 Generation

```csharp
namespace Underleague.Sim.Generation;

public sealed class NameGenerator { public NameGenerator(RaceDefinition race); public string Next(ref Pcg32 rng); } // "First Last"; sin repetir dentro de un equipo (el TeamGenerator lo garantiza)

public static class PlayerGenerator
{
    /// Cada atributo = clamp(quality + raceBias + positionBias + Range(-dev, dev+1), 1, 99). positionBias en tuning.generation.
    /// Rasgos: n = pick ponderado de tuning.generation.traitCountWeights (1..3); elegidos por peso de race.TraitWeights sin repetición.
    /// Portero: además, con probabilidad tuning.generation.goalkeeperTraitChance, un rasgo de portero (Cat/Wall/Rusher, uniforme).
    /// Tags = [race.Tag, position.ToString(), ...traits].
    public static PlayerDefinition Generate(ref Pcg32 rng, Catalog catalog, RaceDefinition race, Position position, Rarity rarity, int quality, int id, string name);
}

public static class TeamGenerator
{
    /// 10 jugadores: titulares GK, DEF, DEF, MID, MID, MID, FWD (ids firstId..firstId+6) y suplentes DEF, MID, FWD. Uno de los 10 es Rare (RF-005), elegido con rng. Lineup.Default.
    public static TeamSetup Generate(ref Pcg32 rng, Catalog catalog, string teamId, Race race, int quality, int firstPlayerId);
}
```

Añadir a `data/sim/tuning.json`:

```json
"generation": {
  "positionBias": {
    "Goalkeeper": { "strength": 4, "speed": 0, "technique": 2, "stamina": 4, "leash": -30 },
    "Defender":   { "strength": 6, "speed": -2, "technique": -4, "stamina": 4, "leash": -5 },
    "Midfielder": { "strength": -2, "speed": 2, "technique": 4, "stamina": 6, "leash": 8 },
    "Forward":    { "strength": 0, "speed": 6, "technique": 4, "stamina": -4, "leash": 0 }
  },
  "traitCountWeights": [50, 35, 15],
  "goalkeeperTraitChance": 5000
},
"leash": { "minCells": 1, "cellsPer99": 4 }
```

Correa en casillas: `leashCells = minCells + leash * cellsPer99 / 99` (entero).

El atributo `leash` es la **excepción** a la fórmula de arriba: `clamp(generation.leashBase + raceBias +
positionBias, 1, 99)`, sin `quality` y sin dado. Es disciplina posicional, no nivel. Con `quality` dentro,
la conversión entera a casillas cruzaba un escalón entre calidad 40 y 60 (4 casillas de radio frente a 5)
y el radio de acción resultó ser el canal de ventaja más fuerte del motor: con él, `betterTeamWinRate`
para una diferencia de calidad de 20 no bajaba de 85-90% por mucho que se aplanara el resto (paquete E).
Con `leashBase` 50 y `cellsPer99` 8 la correa queda en 2 casillas (portero), 4 (defensa) y 5 (medio y
delantero), igual para los dos equipos.

## 3. Motor: algoritmo

### 3.1 Coordenadas

Posición continua `Vec2` en casillas: X en [0,16], Y en [0,5]. Equipo 0 (local) ataca hacia X=16 y defiende X=0; equipo 1 lo contrario. Casilla-hogar absoluta del equipo 1: `Column = 15 - col`. Portería: X=0 o 16, filas 1..3 (Y en [1,4]). Área: 2 columnas desde la línea de gol, filas 1..3.

### 3.2 Bucle de tick

```
init: MatchStart; kickoff(equipo 0)
while phase != Finished:
  tick++
  actualizar estado táctico y desplazamiento de bloque (3.4)
  para cada jugador por id ascendente:
     si StateTicksLeft > 0: StateTicksLeft--; si llega a 0 -> resolver salida del estado (3.6)
     si IsDecisionState y (tick + Id) % decisionIntervalTicks == 0: decidir (3.5)
     ejecutar acción actual: mover (3.3) o acción instantánea
  actualizar balón (3.7)
  comprobar fuera/gol/incomparecencia (3.8)
  contabilizar métricas (posesión, tercio)
  comprobar fin de reglamentario / gol de oro (3.9)
```

El recorrido de jugadores va por **id ascendente en los ticks pares y por id descendente en los impares**
(`MatchEngine.PlayerInTurnOrder`). Con un orden fijo, el equipo cuyos jugadores tienen los ids más bajos
resolvía antes las entradas, los pases y los tiros del mismo tick y ganaba el 53,6% de los partidos espejo
(medido en el paquete E con dos equipos de la misma calidad, 4.800 partidos); alternando, gana el 50,6%,
que es ruido. La alternancia es igual de determinista y reproducible entre plataformas (RT-020, RT-024): no
introduce ninguna fuente de aleatoriedad, solo depende del número de tick. La ventaja de local, cuando la
haya, debe venir del criterio del árbitro (RF-060), no del orden de un array.

Orden de resolución de eventos en el mismo tick: el orden del bucle de ese tick. Nunca se reordena por otro criterio.

### 3.3 Movimiento

`speedPerTick = (movement.baseCellsPerTickMilli + movement.speedCellsPerTickMilliPer99 * Speed / 99) / 1000f`, x `dribbleSpeedPercent/100` si conduce, x `(100 + Fast.speedBonusPercent)/100` si tiene `Fast`. Fatiga: a partir de `fatigueStartTick`, factor `1 - fatigueMaxSlowPercent/100 * (100 - Stamina)/100 * progreso`, con `progreso = (tick - fatigueStartTick) / (regulationTicks - fatigueStartTick)` acotado a [0,1], y reducido por `Resilient.fatigueResistancePercent`. Todo en `float` solo en el último paso; los porcentajes se combinan en `int` antes.

Un jugador se mueve hacia su `TargetPoint` a `speedPerTick`, sin superar la distancia restante. El punto objetivo se acota a la correa: si `Distance(target, EffectiveHomeCell) > LeashCells`, se proyecta sobre la circunferencia. El portero además se acota al rectángulo del área (RF-057b).

### 3.4 Estado táctico y bloque

`TacticalState` por equipo: `InPossession` si un jugador propio posee el balón o el balón vuela tras un pase propio; `OutOfPossession` si lo posee el rival; balón suelto: se mantiene el anterior. Al cambiar de poseedor entre equipos: el que gana entra en `OffensiveTransition` y el que pierde en `DefensiveTransition` durante `transitionTicks`, y luego pasan a los estados estables.

`CurrentShift` (float, por equipo) se acerca al `Shift(state).Shift` objetivo a razón de `|objetivo| / SpeedTicks` por tick. `EffectiveHomeCell = HomeCellCenter + (CurrentShift * AttackDirection, 0)`.

### 3.5 Decisión (utilidad)

Para cada acción legal del estado (`StateMachine.LegalActions`):

```
score = Base(pos, a) * Tactical(state, a) / 100 * TraitMult(a) / 100 + Context(a)
TraitMult(a) = producto de ActionMultipliers de los rasgos del jugador (porcentaje, 100 = neutro), evaluado en int
               como acumulación secuencial: m = m * x / 100; al final se multiplica por (100 + LeaderBonus) / 100
LeaderBonus  = suma de adjacentTeammateBonusPercent de los compañeros con rasgo Leader cuya casilla-hogar es
               contigua a la del jugador (incluidas las diagonales, Pitch.AreAdjacent). Las casillas-hogar no
               cambian durante el partido: se resuelve una vez al construir el motor
```

Se descarta (`LeashFiltered`) toda acción de movimiento cuyo punto objetivo, tras acotar a la correa, quede a menos de 0.25 casillas del jugador y a la vez el objetivo real esté fuera de la correa (es decir, la acción exigiría salir). `Shoot`, `Pass` y `Tackle` no se filtran por correa. El campo `LeashFiltered` de `UtilityRow` marca **cualquier** descarte, no solo el de correa: en el volcado (RT-098) una acción descartada por falta de candidato o por enfriamiento aparece también con esa marca.

Dos descartes no son de correa y sí de mecánica:

- `Tackle` se descarta mientras `TackleCooldown > 0` (§3.6). Sin ese enfriamiento la utilidad elegía `Tackle` en casi cada decisión con un rival cerca y el motor resolvía unas 75 entradas por partido, cuatro veces el rango de RT-056; con él, el número de entradas lo gobierna el peso de la acción en `weights.json`, que es una palanca continua.
- `ChaseBall` recibe `+chaseBallIncomingPassBonus` y queda exenta del filtro de correa cuando el jugador es el receptor previsto de un pase en vuelo. Sin ese término ganaba `OfferSupport` y el receptor se alejaba del punto de llegada mientras el pase viajaba: el balón caía suelto en el 42% de los pases y la posesión duraba tres segundos.

Términos de contexto (claves de `weights.json`, todo en enteros; distancias en casillas convertidas con `(int)(d * 100)` cuando se multiplican):

| Acción | Objetivo de movimiento | Contexto |
|---|---|---|
| ChaseBall | posición del balón (si vuela, su punto de llegada) | `+chaseBallLooseBonus` si suelto; `+chaseBallIncomingPassBonus` si es el receptor del pase en vuelo (y sin filtro de correa); `-chaseBallDistancePenaltyPerCell * d`; `-chaseBallNotNearestPenalty` si no es el compañero más cercano al balón (empate por id) |
| MarkOpponent | rival de campo más cercano dentro de la correa | `-markDistancePenaltyPerCell * d`; sin candidato: descartada |
| OfferSupport | `(carrierX + 2*dir, fila de la **casilla-hogar** acercada 1 hacia 2.5)` | `+supportAheadBonus` si el jugador está por delante del balón en sentido de ataque; `-supportCrowdedPenalty` por compañero a < 1.5 del objetivo. Solo si su equipo posee el balón; si no, descartada |
| CoverSpace | punto del segmento balón->propia portería a distancia `LeashCells` de la casilla-hogar efectiva (acotado) | `+coverBetweenBallAndGoalBonus` si ya está entre el balón y la portería (proyección sobre X) |
| Pass | — | receptor: compañeros a <= 7 casillas, visibles (ningún rival a < 1.0 del receptor); se elige el de mayor `avance*100 - distancia*20`; `+passOpenReceiverBonus` si hay receptor; `+passUnderPressureBonus` si un rival está a < 1.0 del poseedor; `-passNoReceiverPenalty` si no hay receptor |
| Dribble | 1 casilla hacia la portería rival, Y hacia 2.5 | `+dribbleOpenSpaceBonus` si ningún rival a < 2 casillas por delante; `-dribbleOpponentAheadPenalty` por cada rival a < 2 por delante |
| Shoot | — | `range = shootBaseRangeCells + LongShot.shootRangeBonusCells`; `d` = distancia al centro de la portería; en rango: `+shootInRangeBonus - shootDistancePenaltyPerCell*d - shootAnglePenaltyPerRow*|Y-2.5|`; fuera: `-shootOutOfRangePenalty` |
| Tackle | poseedor rival | descartada si `TackleCooldown > 0`; poseedor a <= `tackleDistanceMaxCells`: `+tackleBallCarrierBonus`; si no: `-tackleOutOfReachPenalty` |
| Retreat | casilla-hogar efectiva | `+retreatDistanceBonusPerCell * d`; `-retreatAtHomePenalty` si d < 0.5 |

Gana la puntuación máxima; empate: la primera en el orden del enum. Puntuación mínima para actuar: no hay; siempre se elige una. El jugador seleccionado en `SimConfig.DumpUtility` guarda la tabla en el tick indicado.

Portero: mismas reglas con estos ajustes: `ChaseBall` solo si el balón está suelto dentro de su área; `MarkOpponent` y `OfferSupport` descartadas; `CoverSpace` objetivo = punto a 0.7 casillas de la línea de gol sobre la recta portería->balón; `Pass` elige el compañero más adelantado visible sin límite de distancia.

### 3.6 Estados y resolución

Duraciones en `tuning.states`. Transiciones:

- Decidir `Pass` -> `Passing` (PassingTicks); al expirar, lanza el pase (3.7) y pasa a `Positioning`.
- Decidir `Shoot` -> `Shooting`; al expirar, lanza el tiro (3.7) y pasa a `Positioning`.
- Decidir `Tackle` -> `Tackling` (TacklingTicks) y `TackleCooldown = TackleCooldownTicks + TacklingTicks`; al expirar, resuelve la entrada (3.7) y pasa a `Positioning` o `KnockedDown`. El enfriamiento se decrementa un tick por tick y mientras dure la utilidad descarta `Tackle` (3.5): un jugador no se tira dos veces seguidas.
- Decidir `Dribble` con balón -> `Dribbling` (se mueve cada tick). Sin balón, `ChaseBall`/`Mark`/`Support`/`Cover`/`Retreat` -> `Chasing` (ChaseBall) o `Positioning` (resto), con objetivo guardado.
- Al perder el balón desde `Dribbling` -> `Positioning`.
- `KnockedDown` -> `Positioning` al expirar. `Celebrating` -> `Positioning`. `Injured` y `SentOff` son terminales: el jugador sale del campo (posición `(-1,-1)`, no cuenta ni decide).

### 3.7 Balón y resoluciones

`Ball`: `Position`, `Velocity`, `OwnerId` (-1 = suelto), `InFlight`, `FlightTarget` (Vec2), `FlightTicksLeft`, `PassReceiverId`, `PassSucceeds`, `LastTouchTeam`, `LastTouchPlayer`. Con dueño, el balón está en la posición del dueño.

**Pase**: `dist` al receptor. `p = pass.baseSuccess + pass.techniqueFactor*(Technique-50) + Cerebral.passQualityBonus*100 - pass.distancePenaltyPerCell*dist - pass.pressurePenalty*(rival a <1.0 del pasador)`; `PassSucceeds = rng.Chance(clamp(p, 500, 9800))`. `FlightTicksLeft = max(1, ceil(dist / (ball.passSpeedCellsPerTickMilli/1000)))`. Objetivo: posición del receptor + su velocidad * ticks (anticipación), acotada al campo. Evento `PassAttempted`. Cada tick de vuelo, para cada rival por id que esté a < `interceptRadiusCells` del balón y no haya intentado aún en este pase: `rng.Chance(interceptBaseChance + interceptTechniqueFactor*(Technique-50))` -> intercepta: dueño, `PassFailed` (Detail `"intercepted"`), `Recovery`. Al llegar: si `PassSucceeds` y receptor a < 1.0 -> dueño, `PassCompleted`; si no, suelto con `Velocity` = dirección * 0.1, `PassFailed` (Detail `"loose"`).

**Balón suelto**: `Position += Velocity; Velocity *= looseBallFrictionPercent/100`. Recogida: el jugador más cercano a < 0.5 casillas (empate por id) pasa a dueño; si es de otro equipo que el último toque -> `Recovery`.

**Tiro**: `d` al centro de la portería, `pressure` = rivales a < 1.0. `quality = clamp((shot.baseQuality + shot.techniqueFactor*Technique + shot.strengthFactor*Strength + Scorer.shotQualityBonus*100 - shot.distancePenaltyPerCell*d - shot.pressurePenalty*pressure) / 100, 5, 95)` (0..100). `offTarget = rng.Chance(shot.offTargetBase + shot.offTargetDistanceFactor*d - quality*20)`. Evento `Shot` (Detail `"onTarget"`/`"offTarget"`). Vuelo hasta la línea de gol a `shotSpeed`. Si va a puerta, al llegar: `gkRel = d <= save.closeRangeCells ? Speed : Strength`, más `Cat.saveBonusClose` o `Wall.saveBonusFar`; `savePercent = save.basePercent + (gkRel - 50) * save.attributeWeightPercent / 50 - (quality - 50) * save.qualityWeight / 100 - save.consecutiveShotDecayPercent * consecutivosSinPerder`, acotado 5..95, reducido por `Stamina` del portero: el decaimiento se multiplica por `(100 - Stamina) / 50` acotado [0.2, 2] en enteros (`* (100-Stamina) / 50`). `rng.Chance(savePercent*100)` -> `Save` (portero dueño) o `Goal`. Sin portero en campo: gol si va a puerta. Fuera: saque de puerta. Penalti: tiro desde `(goalX - 2*dir, 2.5)` con `pressure = 0` y `quality + shot.penaltyQualityBonus`.

**Entrada**: al expirar `Tackling`, si el objetivo se fue de `tackleDistanceMaxCells + 0.3` (o ya no está en el campo) no hay contacto: el que entra vuelve a `Positioning` sin evento. Si sigue en alcance, la entrada se resuelve **tenga o no el balón**: cuando lo ha soltado dentro de los ticks de `Tackling` (un pase tarda `PassingTicks`) es una entrada a destiempo, que tira falta y lesión igual pero no puede robar un balón que ya no está (`isWin` se anula) ni cuenta como evento `Tackle`. Antes el motor volvía a `Positioning` en silencio también en ese caso, y el número de entradas por partido dependía de si `TacklingTicks` era mayor o menor que `PassingTicks` (2,3 entradas con 6/5; 30 con 4/6): una carrera de ticks, no una palanca. Resolución: `win = tackle.baseWin + strengthFactor*(Str-50) + speedFactor*(Spd-50) - carrierTechniqueFactor*(carrierTech-50)`; `foul = tackle.foulBase + foulStrengthFactor*(Str-50) + Dirty.foulChanceBonus*100 + Aggressive.hardTackleBonus*100 + biasShift`, con `biasShift = -referee.biasFoulShiftPer10 * Bias / 10` si el que entra es del equipo 0 y `+` si es del 1 (Bias positivo favorece al local). Primero `isFoul = rng.Chance(foul)`, luego `isWin = rng.Chance(win)`; siempre se consumen los dos rolls. Evento `Tackle` (Detail `"won"`/`"missed"`/`"foul"`).
- Falta: `Foul`; poseedor conserva el balón; el que entra `KnockedDown` (KnockedDownTicks); tarjeta: `rng.Chance(redCardBase + (hard ? hardTackleRedBonus : 0))` -> roja; si no, `rng.Chance(yellowCardBase + (hard ? hardTackleYellowBonus : 0))` -> amarilla (segunda amarilla = roja). `hard` = `Aggressive` o `Dirty` o `Str * 100 >= tackle.hardTackleThreshold`; los sumandos de tarjeta dura son `tackle.hardTackleRedBonus` y `tackle.hardTackleYellowBonus`. Roja: `SentOff`. Si la falta ocurre dentro del área del que entra: `rng.Chance(referee.penaltyOnFoulInArea)` -> fase `Penalty`.
- Sin falta y `isWin`: `Recovery` para el que entra, poseedor `KnockedDown`. Sin falta y no gana: el que entra `KnockedDown` durante `KnockedDownTicks / 2`.
- Lesión (después de resolver, y solo si hubo contacto: el poseedor tenía el balón o hubo falta): `inj = injury.onTackleBase + (isFoul ? injury.onFoulBase : 0) + attackerStrengthFactor*(Str-50) - victimStaminaResistFactor*(Sta-50) + Dirty.injuryChanceBonus*100 - Resilient.injuryResistanceBonus*100`; `rng.Chance(clamp(inj, 0, 5000))` -> `Injury` (Detail `"severe"` si `rng.Chance(injury.severeShare)`, si no `"minor"`); el lesionado pasa a `Injured` y sale del campo; si tenía el balón, queda suelto. Muerte: nunca en fase 0 (RF-093 requiere estado previo).

**Regate**: en `Dribbling`, si un rival de campo está a < 0.8 y `DribbleDuelCooldown == 0`: `DribbleAttempted`; `win = dribble.baseWin + attackerTechniqueFactor*(Tech-50) - defenderSpeedFactor*(Spd-50) - defenderStrengthFactor*(Str-50)`; ganado: `DribbleWon`, rival `KnockedDown` `dribble.lostKnockdownTicks` ticks; perdido: `DribbleLost`, rival dueño, `Recovery`. `DribbleDuelCooldownTicks` para **los dos** duelistas: con el enfriamiento solo en el conductor, el defensor que ganaba el balón lo perdía al tick siguiente contra el mismo rival, que seguía a menos de 0.8 casillas, y el balón rebotaba entre los dos equipos.

### 3.8 Fuera, gol, reanudaciones, incomparecencia

- `Y < 0` o `Y > 5`: saque de banda para el equipo contrario al último toque: fase `Restart` durante `throwInTicks`; el balón se coloca en el punto de salida acotado; al terminar, el jugador de ese equipo más cercano (empate id) se teletransporta al punto y es dueño. Evento `Recovery` con Detail `"throwIn"`.
- `X < 0` o `X > 16` sin ser gol: último toque del atacante -> saque de puerta (portero dueño en su casilla, `goalKickTicks`, Detail `"goalKick"`); del defensor -> córner (atacante más cercano dueño en la esquina, `cornerTicks`, Detail `"corner"`).
- Gol: `Goal` (Actor = tirador; Target = asistente si el último pase completado fue a < `assistWindowTicks` ticks), goleador `Celebrating`, marcador, `Kickoff` para el equipo que encaja durante `kickoffTicks`: todos a su casilla-hogar (sin desplazamiento), centrocampista central (más cercano a (8,2.5)) del equipo que saca es dueño en el centro.
- Penalti: fase `Penalty` durante `penaltyTicks`; el tirador es el jugador de campo con más `Technique` del equipo; los demás quedan quietos; al expirar se resuelve el tiro; después, saque de puerta o kickoff según resultado.
- Incomparecencia: si un equipo tiene < 5 jugadores en campo (Injured/SentOff descontados) -> `MatchEnd` con Detail `"forfeit"`, gana el otro (RF-059). `Report.Forfeit = true`.

Durante `Restart`, `Kickoff` y `Penalty` los jugadores no deciden ni se mueven, salvo el teletransporte indicado.

### 3.9 Fin, gol de oro

Al llegar `tick == regulationTicks`: si hay ganador, `MatchEnd`. Si empate: `MobStart` y `RefereeLeaves` (en ese orden, mismo tick; en fase 0 no cambian nada más: el campo no se estrecha ni se anula al árbitro, eso es fase 3), fase `MobGoldenGoal`, kickoff para el equipo 1, y el primer gol termina. Si se agotan `goldenGoalMaxTicks` sin gol: gana el equipo con más tiros a puerta; si empatan, el que tuvo más ticks de posesión; si empatan, el visitante (regla explícita para que nunca haya empate, RF-055c; se registra en `Report` con Detail `"tiebreak"`). Evento `MatchEnd` con Detail `"regulation"`, `"goldenGoal"`, `"tiebreak"` o `"forfeit"`.

### 3.10 Jugadas y métricas

- Posesión: cambia cuando el balón pasa a un dueño del otro equipo (Recovery, intercepción, saque para el otro equipo). `PossessionChanges` cuenta esos cambios (sin contar el kickoff inicial).
- Jugada: `PlayStart` al iniciar una posesión, `PlayEnd` (Detail `"shot"`/`"lost"`) al terminar. Cadena de pases: pases completados consecutivos dentro de la posesión; al terminar la posesión, si hubo >= 1 pase completado, `PassChains++` y `PassChainTotalLength += longitud`.
- `BallTicksByThird`: cada tick, según X absoluta del balón. `PossessionTicks[team]`: ticks con dueño de ese equipo.
- Log (RF-121): una línea por evento relevante: `"[t=0123] Grok tackles Aelar: foul (yellow)"`. Formato `[t=NNNN] {Actor} {verbo} {Target}: {Detail}`. Sin log si `CollectLog=false`.

### 3.11 Decisiones de implementación

Detalles que la especificación no cerraba y que el motor resolvió al implementarlo (paquete B) o al ajustar
el balance (paquete E). Cada uno está también comentado en el punto del código donde se aplica.

**Paquete B (motor).**

1. **`OfferSupport` usa la fila de la casilla-hogar**, no la Y instantánea del jugador. Con la Y instantánea el punto de apoyo se recalcula en cada decisión, todo el bloque converge en pocos ticks a la fila 2.5, se solapa y el partido se bloquea.
2. **`hard` en la tarjeta se decide con `tackle.hardTackleThreshold`** (`Str * 100 >= threshold`), no con un 70 escrito en el código.
3. **Las constantes literales del motor son `private const` con documentación**, nunca números sueltos en medio de una fórmula. El paquete E subió a `tuning.json` las que el balance puede querer mover (§2.5); las que quedan son geométricas (radio de recogida 0.5, radio de presión 1.0, radio de duelo 0.8, margen de alcance de entrada 0.3, punto de penalti a 2 casillas, velocidad del balón suelto 0.1).
4. **`CoverSpace` resuelve la intersección** del segmento balón->portería propia con la circunferencia de correa (ecuación de segundo grado, raíz más cercana al balón); si el segmento nunca alcanza esa distancia, se toma el punto del segmento más cercano a la casilla-hogar.
5. **`LeashFiltered` marca cualquier descarte** de la fila del volcado de utilidad, no solo el de correa: una acción sin candidato (`MarkOpponent` sin rival, `OfferSupport` sin posesión) o descartada por enfriamiento aparece con la misma marca.
6. **`DistanceToGoal` de `MatchEvent` va en centésimas de casilla** (entero), como el resto de distancias que entran en aritmética entera (RT-023).
7. **El reloj no se detiene** en reanudaciones, penaltis ni celebraciones: los ticks de `Restart`, `Kickoff` y `Penalty` cuentan dentro de `regulationTicks`. No hay tiempo añadido en fase 0.
8. **Solo se marca gol desde un `Shot`**: no hay gol por el balón cruzando la línea suelto ni en propia puerta. Un balón que sale por la línea de fondo es siempre saque de puerta o córner.
9. **Ganar el balón fuerza `Dribbling`**: `SetOwner` mete al nuevo poseedor en `Dribbling` si venía de `Positioning`, `Chasing` o `Tackling`, y saca al anterior de `Dribbling`/`Passing`/`Shooting`. Así nadie conduce un balón que ya no tiene ni se queda parado con el balón hasta su siguiente decisión.
10. **`Trait.Leader` quedó sin aplicar en el paquete B**; el paquete E lo implementa (§3.5).
11. **Los tipos del motor son `internal`** (`MatchPlayer`, `Ball`, `Utility`, `MatchEngine`, `UtilityContext`): no forman parte de la superficie pública de §2.4. `Sim.csproj` declara `InternalsVisibleTo("Underleague.Sim.Tests")` para que los tests los ejerciten directamente.
12. **El test de incomparecencia usa equipos escritos a mano** (cinco frágiles contra siete brutales) porque `TeamGenerator` no puede producir los extremos necesarios para provocar cinco bajas.

**Paquete E (ajuste).**

13. **El recorrido del bucle de tick alterna con la paridad del tick** (§3.2): quita la ventaja del equipo con ids más bajos.
14. **El receptor previsto de un pase va a por el balón** (`chaseBallIncomingPassBonus`, §3.5).
15. **El enfriamiento del duelo de regate se aplica a los dos duelistas** (§3.7).
16. **La entrada a destiempo tiene consecuencias** —falta y lesión— **pero no cuenta como `Tackle` ni roba el balón** (§3.7), y **`Tackle` tiene enfriamiento propio** (§3.5, §3.6).
17. **El atributo `leash` no depende de la calidad ni del dado** (§2.6).

## 4. `/Balance` (consola)

Sin paquetes externos. Parseo manual de argumentos.

```
--runs N            total de partidos (por defecto 1000); se reparten por igual entre los emparejamientos de reference.json (en orden, resto a los primeros)
--seed S            semilla base (por defecto 1); partido i usa RngStreams.Match(S, i); los equipos se generan con RngStreams.Generation(S, indiceEquipo)
--teams path        por defecto data/balance/reference.json
--data path         por defecto: subir directorios desde cwd hasta encontrar data/
--out dir           por defecto out/<seed>/; escribe summary.csv, matches.csv, players.csv
--log               imprime el log del primer partido
--dump-utility P:T  SimConfig.DumpUtility para el primer partido; imprime la tabla
--quiet             sin resumen por consola
```

`matches.csv`: `index,seed,homeId,awayId,homeGoals,awayGoals,winner,ticks,goldenGoal,forfeit,possessionChanges,passChains,passChainAvgLength,shots,shotsOnTarget,tackles,fouls,yellow,red,injuries,ballThird0,ballThird1,ballThird2,finalBias`.
`summary.csv`: `metric,value,rangeMin,rangeMax,status` con `status` en `IN`/`OUT`/`INFO`.

Métricas y rangos (de `balance.md`, RT-056): `possessionChanges` 12-25; `passChainAvgLength` 2-4; `shotsPerMatch` 8-16; `scorelineShare_1-0_to_3-2` >= 50 (INFO además de `share_over5goals` < 5, `drawShareAtRegulation` < 15); `ballThirdMaxShare` <= 50; `tacklesPerMatch` 6-14; `injuriesPerMatch` 0.3-0.8. Extra fase 0: `betterTeamWinRate` para cada emparejamiento cuyas calidades difieran (`human_60` vs `human_40`, `human_60` vs `human_50`), rango 65-80 para diferencia 20, INFO para diferencia 10. Todos los valores con dos decimales.

El cálculo vive en `Sim/Analysis/MatchMetrics.cs` (público, sin E/S y sin aleatoriedad): `Balance/Metrics.cs`
es solo el adaptador a las columnas de `summary.csv`, y la puerta estadística de `Sim.Tests` (§6) llama al
mismo código. Una métrica no puede significar una cosa en el lote y otra en la puerta.

Consola: tabla alineada, tiempo total y partidos/segundo. Código de salida 1 si alguna métrica `OUT` (para CI), 0 si no.

## 5. `/tools/DataValidator`

Paquete `JsonSchema.Net`. `dotnet run --project tools/DataValidator -- data/` valida cada fichero contra `data/schemas/<carpeta>.schema.json` (JSON Schema draft 2020-12), después intenta `DataLoader.FromJson` con todos los ficheros. Salida: una línea por fichero (`OK`/`ERROR` + ruta JSON + mensaje). Código de salida 1 si hay algún error.

Esquemas a escribir: `races.schema.json`, `traits.schema.json`, `ai-weights.schema.json`, `tuning.schema.json`, `balance-reference.schema.json`. `additionalProperties: false` en todo salvo claves `_doc`.

## 6. Tests (`Sim.Tests`)

| Fichero | Contenido |
|---|---|
| `Random/Pcg32Tests.cs` | vectores de referencia; `Range` uniforme sin sesgo (10^5 muestras, chi-cuadrado grosero); `Shuffle` determinista |
| `Random/RngStreamsTests.cs` | flujos distintos para kinds/índices distintos; mismo input -> misma secuencia |
| `Data/DataLoaderTests.cs` | carga `data/` real (los tests sí leen disco, con ruta relativa al repo); clave desconocida -> `DataException` con fichero y ruta |
| `Generation/GeneratorTests.cs` | atributos en 1..99; 1-3 rasgos sin repetir; nombres sin repetir en equipo; un Rare por equipo; determinismo con misma semilla |
| `Engine/StateMachineTests.cs` | tabla `CanPerform` completa contra la de 2.4; estados terminales sin acciones |
| `Engine/UtilityTests.cs` | empate -> primera acción del enum; correa filtra; multiplicadores de rasgo |
| `Engine/DeterminismTests.cs` | `SameSeedSameEvents` (comparación elemento a elemento de 20 semillas); `IndependentStreams`; `CrossPlatformFingerprint` escribe `fingerprint.txt` (hash FNV-1a de la secuencia de eventos de 100 semillas) en el directorio de salida de tests |
| `Engine/ArchitectureTests.cs` | `Underleague.Sim` no referencia ensamblados cuyo nombre contenga `Godot`; y no contiene tipos que referencien `System.IO.File`, `System.Random`, `System.DateTime` (escaneo de metadatos con `System.Reflection.Metadata`) |
| `Engine/MatchRulesTests.cs` | nunca empate; forfeit con < 5; el portero nunca sale del área (comprobación sobre todas las posiciones registradas en un partido con `DumpUtility`... o exponiendo `Report.Players` con `MaxDistanceFromArea` — añadir campo `GoalkeeperLeftArea: bool` a `MatchReport`) |
| `Engine/StatisticalTests.cs` | 1.000 partidos del conjunto de referencia con la misma generación de equipos y las mismas semillas que `Balance/BatchRunner` (`RngStreams.Generation(seed, índice)`, ids desde `1 + índice*100`, gemelo con índice `1000+i` para un equipo contra sí mismo, `RngStreams.MatchSeed(seed, i)`, árbitro neutro): métricas RT-056 en rango y `betterTeamWinRate` 65-80 para una diferencia de calidad de 20. Reutiliza `Sim.Analysis.MatchMetrics` y lee `data/balance/reference.json` de disco, para no duplicar ni el cálculo ni el conjunto. **Es la puerta de salida de la fase 0**: se marca con `[Trait("Category","Gate")]` y se puede excluir del bucle de desarrollo con `dotnet test --filter Category!=Gate`; en CI corre entera |

## 7. CI

`.github/workflows/ci.yml`: matriz `ubuntu-latest` / `windows-latest`; `dotnet build`, `dotnet test`, `dotnet run --project tools/DataValidator -- data/`, `dotnet run --project Balance -c Release -- --runs 2000 --quiet`; sube `fingerprint.txt` como artefacto por SO y un último job compara ambos (falla si difieren: activa RT-023b).

## 8. Paquetes de trabajo

| Paquete | Agente | Depende de | Ficheros |
|---|---|---|---|
| A. Cimientos | fast-worker | — | `Sim/Random`, `Sim/Model`, `Sim/Events`, `Sim/Data`, `Sim/Generation`, `Sim/Engine/Vec2.cs`, `Sim/Engine/MatchPhase.cs`, `Sim/Engine/StateMachine.cs`, `Sim/Engine/MatchReport.cs`, `Sim/Engine/Simulator.cs` (solo firma: `Run` lanza `NotSupportedException("engine pending")`), `data/sim/tuning.json` (sección `generation` y `leash`), tests de Random/Data/Generation/StateMachine |
| B. Motor | deep-reasoner | A | `Sim/Engine/MatchPlayer.cs`, `Ball.cs`, `Utility.cs`, `MatchEngine.cs`, `Simulator.cs` (implementación), tests `UtilityTests`, `DeterminismTests`, `MatchRulesTests` |
| C. Balance | fast-worker | A | `Balance/*` |
| D. Validador y CI | fast-worker | — | `tools/DataValidator/*`, `data/schemas/*`, `.github/workflows/ci.yml`, `ArchitectureTests` |
| E. Estadísticos y ajuste | deep-reasoner + orquestador | B, C | `StatisticalTests`, iteración sobre `data/ai/weights.json` y `data/sim/tuning.json` hasta cumplir RT-056 |

Revisión del orquestador tras B y tras E.
