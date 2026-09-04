using Underleague.Sim.Data;
using Underleague.Sim.Model;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run;

/// <summary>División de la liga (RF-128). La fase 2 juega siempre en <see cref="Third"/>.</summary>
public enum Division
{
    Third,
    Second,
    First,
    Continental,
    World,
}

/// <summary>Tipo de vínculo entre dos jugadores (RF-101, RF-102). No hay vínculos negativos (I-3, v0.9.1).</summary>
public enum BondKind
{
    Partnership,
    BloodDebt,
    Stonewall,
}

/// <summary>Vínculo de un jugador con otro. Sin signo: no existen vínculos negativos en el lanzamiento (I-3).</summary>
public sealed record RunBond(int OtherPlayerId, BondKind Kind);

/// <summary>Prótesis instalada en un jugador (RF-095). Fase 3; el campo existe desde la versión 1 del esquema.</summary>
public sealed record RunProsthesis(string Slot, string Effect);

/// <summary>Modo de uso de un consumible equipado (RF-080..082).</summary>
public enum ConsumableMode
{
    Manual,
    Conditional,
}

/// <summary>Consumible equipado para la run: máximo 3, mínimo 1 manual (RF-080..082).</summary>
public sealed record EquippedConsumable(string Id, ConsumableMode Mode, string Trigger);

/// <summary>Árbitro de la run (RF-061, RF-064c). <c>BribesReceived</c> es progresión de fase 3.</summary>
public sealed record RunReferee(int Id, string Name, RefereeTrait Trait, int BribesReceived);

/// <summary>Resultado con el que se cerró un nodo del historial.</summary>
public enum NodeResult
{
    /// <summary>Nodo no de partido, resuelto sin ganar ni perder.</summary>
    Completed,

    /// <summary>Partido ganado.</summary>
    Won,

    /// <summary>Partido perdido.</summary>
    Lost,
}

/// <summary>Entrada del historial de nodos (RT-030).</summary>
public sealed record NodeHistoryEntry(int NodeId, NodeKind Kind, NodeResult Result);

/// <summary>Dónde está la run: en el mapa eligiendo nodo, dentro de un nodo interactivo, o terminada.</summary>
public enum RunPhase
{
    /// <summary>En el mapa: <see cref="RunEngine.AvailableNodes"/> devuelve los nodos elegibles.</summary>
    OnMap,

    /// <summary>Dentro de un nodo interactivo (mercado, clínica, entrenamiento, evento, recompensa).</summary>
    NodeOpen,

    /// <summary>La run ha terminado, en victoria o en derrota.</summary>
    Finished,
}

/// <summary>Estado de la run (RF-002).</summary>
public enum RunOutcomeKind
{
    InProgress,
    Victory,
    Defeat,
}

/// <summary>
/// Causa de derrota. Solo hay dos (RF-002b) y este enum no crece sin cambiar esa regla del juego.
/// </summary>
public enum DefeatCause
{
    /// <summary>La run no ha terminado en derrota.</summary>
    None,

    /// <summary>Se perdió un partido de jefe (RF-002b).</summary>
    BossMatchLost,

    /// <summary>Los jugadores disponibles bajaron de 5 (RF-002b), dentro o fuera de un partido.</summary>
    NotEnoughPlayers,
}

/// <summary>
/// Desenlace de la run.
/// </summary>
/// <param name="Kind">En curso, victoria o derrota (RF-002, RF-002b).</param>
/// <param name="Cause">Causa de la derrota; <see cref="DefeatCause.None"/> si no la hay.</param>
/// <param name="NodeId">Nodo en el que se decidió; -1 si no aplica.</param>
/// <param name="Tick">
/// Tick del partido en el que se decidió, o -1 si ocurrió fuera de un partido. Con
/// <see cref="DefeatCause.NotEnoughPlayers"/> es el tick exacto de la baja que terminó la run: RF-002b
/// dice "al instante", así que el render puede cortar la reproducción justo ahí.
/// </param>
public sealed record RunOutcome(RunOutcomeKind Kind, DefeatCause Cause = DefeatCause.None, int NodeId = -1, int Tick = -1)
{
    /// <summary>Run en curso.</summary>
    public static RunOutcome InProgress { get; } = new(RunOutcomeKind.InProgress);

    /// <summary>True si la run ha terminado, con victoria o con derrota.</summary>
    public bool IsOver => Kind != RunOutcomeKind.InProgress;
}

/// <summary>
/// Un jugador de la plantilla de la run (RT-030, <c>modelo-datos.md</c>). Un objeto por jugador, con
/// los cinco atributos (I-1), un único objeto equipado (I-2) y vínculos sin signo (I-3).
/// Inmutable: se modifica con <c>with</c> o con los métodos <c>With*</c>.
/// </summary>
public sealed record RunPlayer(
    int Id,
    string Name,
    Race Race,
    Position Position,
    Rarity Rarity,
    int Level,
    int Experience,
    Attributes Attributes,
    IReadOnlyList<Trait> Traits,
    IReadOnlyList<string> Tags,
    PhysicalState PhysicalState)
{
    /// <summary>Diccionario entero vacío compartido: el caso normal es no tener contadores.</summary>
    internal static readonly IReadOnlyDictionary<string, int> NoCounters =
        new SortedDictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Etiqueta de especie, fija por raza (ADR 0024).</summary>
    public string SpeciesTag { get; init; } = string.Empty;

    /// <summary>Etiqueta de estilo individual, sorteada al generar el jugador (ADR 0024).</summary>
    public StyleTag StyleTag { get; init; } = StyleTag.Neutral;

    /// <summary>Perks asignados, por id. El máximo depende de la rareza (RF-023, Progression.PerkSlots).</summary>
    public IReadOnlyList<string> Perks { get; init; } = Array.Empty<string>();

    /// <summary>Objeto equipado, o null. Un único objeto por jugador (RF-076, I-2).</summary>
    public string? Item { get; init; }

    /// <summary>Lesiones leves acumuladas (RF-091). Cada una resta <see cref="RunRules.MinorInjuryPenaltyPercent"/>% a los atributos.</summary>
    public int MinorInjuries { get; init; }

    /// <summary>Prótesis instaladas (RF-095). Fase 3.</summary>
    public IReadOnlyList<RunProsthesis> Prostheses { get; init; } = Array.Empty<RunProsthesis>();

    /// <summary>Salario por partido; 0 salvo mercenarios (RF-111).</summary>
    public int Wage { get; init; }

    /// <summary>True si es mercenario (RF-110..113): otra raza, no forma vínculos, cuenta como Stranger.</summary>
    public bool IsMercenary { get; init; }

    /// <summary>True si entró como canterano (RF-114b/c): +33% de experiencia.</summary>
    public bool IsYouth { get; init; }

    /// <summary>Partidos seguidos sin jugar. Los mercenarios abandonan tras 3 (RF-111).</summary>
    public int MatchesBenched { get; init; }

    /// <summary>Vínculos, máximo 2 (RF-101, RF-102).</summary>
    public IReadOnlyList<RunBond> Bonds { get; init; } = Array.Empty<RunBond>();

    /// <summary>Partidos que le quedan de duelo, 0 si no aplica (RF-104).</summary>
    public int Mourning { get; init; }

    /// <summary>Contadores de perks acumulados entre partidos (RF-070). Ordenado por clave ordinal.</summary>
    public IReadOnlyDictionary<string, int> Counters { get; init; } = NoCounters;

    /// <summary>Contadores parciales de progreso de vínculo (asistencias A-&gt;B, etc.). Ordenado por clave ordinal.</summary>
    public IReadOnlyDictionary<string, int> BondProgress { get; init; } = NoCounters;

    /// <summary>
    /// True si el jugador puede alinearse: sano o con lesión leve. La lesión grave impide jugar hasta
    /// recibir tratamiento (RF-092) y el muerto no vuelve (RF-093). Es el predicado que cuenta para el
    /// mínimo de 5 de RF-002b/RF-002e.
    /// </summary>
    public bool IsAvailable => PhysicalState is PhysicalState.Healthy or PhysicalState.MinorInjury;

    /// <summary>Copia con otro estado físico.</summary>
    public RunPlayer WithPhysicalState(PhysicalState state) => this with { PhysicalState = state };

    /// <summary>Copia con la experiencia indicada.</summary>
    public RunPlayer WithExperience(int experience) => this with { Experience = experience };

    /// <summary>Copia con los contadores indicados, ordenados por clave ordinal.</summary>
    public RunPlayer WithCounters(IEnumerable<KeyValuePair<string, int>> counters)
    {
        ArgumentNullException.ThrowIfNull(counters);
        var sorted = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in counters)
        {
            sorted[name] = value;
        }

        return this with { Counters = sorted };
    }

    /// <summary>Copia con el progreso de vínculo indicado, ordenado por clave ordinal.</summary>
    public RunPlayer WithBondProgress(IEnumerable<KeyValuePair<string, int>> progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var sorted = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in progress)
        {
            sorted[name] = value;
        }

        return this with { BondProgress = sorted };
    }

    /// <summary>
    /// Convierte el jugador de la run en la definición que consume <c>Simulator.Run</c>, aplicando la
    /// penalización de las lesiones leves (RF-091: -15% a todos los atributos, acumulable) salvo que el
    /// jugador sea inmune a ella (efecto <c>immunity</c>, ADR 0026: los no-muertos, RF-035). Los
    /// atributos se acotan a 1..99 con <see cref="Attributes.Clamp"/>.
    /// <para><paramref name="applyMinorInjuryPenalty"/> a false devuelve los atributos de la plantilla
    /// sin tocar: es lo que necesita la progresión de después del partido, que no debe guardar la
    /// penalización dentro de los atributos permanentes del jugador.</para>
    /// </summary>
    public PlayerDefinition ToDefinition(Catalog catalog, bool applyMinorInjuryPenalty = true)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var definition = new PlayerDefinition(
            Id, Name, Race, Position, Rarity, Level, Attributes, Traits, Tags, PhysicalState)
        {
            SpeciesTag = SpeciesTag,
            StyleTag = StyleTag,
            Perks = Perks,
            Counters = Counters,
        };

        if (!applyMinorInjuryPenalty
            || MinorInjuries <= 0
            || ProgressionRules.HasImmunity(definition, catalog, Underleague.Sim.Perks.ImmunityKind.MinorInjuryPenalty))
        {
            return definition;
        }

        int percent = 100 - (RunRules.MinorInjuryPenaltyPercent * MinorInjuries);
        var a = Attributes;
        return definition with
        {
            Attributes = Attributes.Clamp(new Attributes(
                a.Strength * percent / 100,
                a.Speed * percent / 100,
                a.Technique * percent / 100,
                a.Stamina * percent / 100,
                a.Leash)),
        };
    }

    /// <summary>
    /// Construye un jugador de run a partir de una definición generada (<c>TeamGenerator</c>,
    /// <c>PlayerGenerator</c>). Es el puente que usan el arranque de la run y el mercado del paquete X.
    /// </summary>
    public static RunPlayer From(PlayerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new RunPlayer(
            definition.Id,
            definition.Name,
            definition.Race,
            definition.Position,
            definition.Rarity,
            definition.Level,
            0,
            definition.Attributes,
            definition.Traits,
            definition.Tags,
            definition.PhysicalState)
        {
            SpeciesTag = definition.SpeciesTag,
            StyleTag = definition.StyleTag,
            Perks = definition.Perks,
            Counters = definition.Counters,
        };
    }
}

/// <summary>
/// Constantes de regla del bucle de run que no viven en <c>/data</c> porque no son ajustes de balance
/// sino reglas de <c>docs/requisitos.md</c>. Las que sí son de balance (oro, precios, objetivos de
/// partido excelente) las trae el paquete X en <c>data/economy/</c>.
/// </summary>
public static class RunRules
{
    /// <summary>Jugadores disponibles por debajo de los cuales la run termina en derrota (RF-002b).</summary>
    public const int MinimumAvailablePlayers = 5;

    /// <summary>Titulares máximos de un equipo (RF-059; el simulador exige entre 5 y 7).</summary>
    public const int MaxStarters = 7;

    /// <summary>Actos por run (RF-001).</summary>
    public const int Acts = 3;

    /// <summary>
    /// Penalización de atributos por cada lesión leve acumulada, en porcentaje (RF-091). Es una regla
    /// del documento de requisitos, no un dial de balance; si algún día hay que ajustarla, se mueve a
    /// <c>data/</c> con un ADR, no se cambia aquí en silencio (RT-057).
    /// </summary>
    public const int MinorInjuryPenaltyPercent = 15;

    /// <summary>Experiencia extra de un canterano, en porcentaje (RF-114c).</summary>
    public const int YouthExperienceBonusPercent = 33;
}

/// <summary>
/// Estado versionado de una run (RT-030), según <c>docs/modelo-datos.md</c>. Inmutable: cada cambio
/// devuelve una copia con los métodos <c>With*</c>. Se serializa con <see cref="Save.RunSave"/>.
///
/// <para>Todo lo que afecta al resultado se guarda en listas o diccionarios ordenados: la plantilla va
/// ordenada por id ascendente y los diccionarios son <c>SortedDictionary</c> ordinal, para que dos
/// estados equivalentes se serialicen byte a byte igual (RT-021, orden determinista).</para>
/// </summary>
public sealed record RunState
{
    /// <summary>
    /// Versión del esquema del estado de la run (RT-030, RT-060). Sube con cualquier cambio de forma;
    /// una run guardada con otra versión no se migra en silencio (<c>modelo-datos.md</c>, "Versionado").
    /// Versión 1: primera con código (la 0 era el borrador sin implementar).
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Versión de esquema con la que se creó este estado.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Semilla de la run: de ella salen todos los flujos de RNG (RT-022).</summary>
    public ulong Seed { get; init; }

    /// <summary>División en la que se juega la run (RF-128).</summary>
    public Division Division { get; init; } = Division.Third;

    /// <summary>Id del club inicial (RF-004).</summary>
    public string ClubId { get; init; } = string.Empty;

    /// <summary>Raza del club inicial (RF-004): todos los jugadores del club son de ella.</summary>
    public Race ClubRace { get; init; } = Race.Human;

    /// <summary>Acto actual, 1..3 (RF-001).</summary>
    public int Act { get; init; } = 1;

    /// <summary>Nodo en el que está el jugador; -1 mientras no ha entrado en ninguno del acto.</summary>
    public int CurrentNodeId { get; init; } = -1;

    /// <summary>
    /// Nodo interactivo abierto (mercado, clínica, recompensa...); -1 si no hay ninguno. Es lo único que
    /// necesita el paquete X para recomponer el surtido: el contenido del nodo se deriva de
    /// <c>RngStreams.Rewards(Seed, PendingNodeId)</c> y de <see cref="NodeRerolls"/>, así que no hace
    /// falta serializarlo y salir a mitad de un mercado no permite volver a tirar el surtido.
    /// </summary>
    public int PendingNodeId { get; init; } = -1;

    /// <summary>Fase del bucle: en el mapa, dentro de un nodo, o terminada.</summary>
    public RunPhase Phase { get; init; } = RunPhase.OnMap;

    /// <summary>Oro disponible (RF-114g..k).</summary>
    public int Gold { get; init; }

    /// <summary>Rerolls usados en toda la run: su coste es creciente (RF-071b).</summary>
    public int RerollsUsed { get; init; }

    /// <summary>Rerolls usados en el nodo abierto. Uno por nodo (RF-071b).</summary>
    public int NodeRerolls { get; init; }

    /// <summary>Desenlace registrado. Ver también <see cref="RunEngine.Outcome"/>, que además vigila el mínimo de plantilla.</summary>
    public RunOutcome Result { get; init; } = RunOutcome.InProgress;

    /// <summary>Historial de nodos completados, en orden (RT-030).</summary>
    public IReadOnlyList<NodeHistoryEntry> NodeHistory { get; init; } = Array.Empty<NodeHistoryEntry>();

    /// <summary>Mapas de los tres actos, en orden. Se generan todos al empezar la run (flujo <c>RngStreams.Map</c>).</summary>
    public IReadOnlyList<ActMap> Maps { get; init; } = Array.Empty<ActMap>();

    /// <summary>Árbitros de la run, 6-8 (RF-061b, RF-064c).</summary>
    public IReadOnlyList<RunReferee> Referees { get; init; } = Array.Empty<RunReferee>();

    /// <summary>Plantilla completa, ordenada por id ascendente. Incluye lesionados graves y muertos.</summary>
    public IReadOnlyList<RunPlayer> Roster { get; init; } = Array.Empty<RunPlayer>();

    /// <summary>Alineación elegida (RF-041). Puede quedar obsoleta tras una baja: <see cref="RunLineup"/> la repara al entrar en un partido.</summary>
    public Lineup Lineup { get; init; } = new(Array.Empty<LineupSlot>());

    /// <summary>Consumibles equipados, máximo 3 (RF-080..082).</summary>
    public IReadOnlyList<EquippedConsumable> Consumables { get; init; } = Array.Empty<EquippedConsumable>();

    /// <summary>
    /// Siguiente id libre de jugador. Los ids se asignan en orden de creación dentro de la run
    /// (<c>determinismo.md</c>, "Orden") y no se reutilizan nunca, ni siquiera tras una muerte: un id
    /// reutilizado rompería el historial y los vínculos.
    /// </summary>
    public int NextPlayerId { get; init; }

    /// <summary>
    /// Contadores enteros de los sistemas de los paquetes X e Y (coste actual del reroll, oro gastado
    /// por sumidero, derrotas seguidas de un mercenario...). Existe para que añadir un sistema no
    /// obligue a subir la versión del esquema. Ordenado por clave ordinal.
    /// </summary>
    public IReadOnlyDictionary<string, int> Counters { get; init; } = RunPlayer.NoCounters;

    /// <summary>Progreso de logros de desbloqueo (RF-125b). Ordenado por clave ordinal.</summary>
    public IReadOnlyDictionary<string, int> Achievements { get; init; } = RunPlayer.NoCounters;

    /// <summary>
    /// Instantánea de <c>/data</c> congelada al empezar la run (RT-061b): ruta relativa -&gt; contenido.
    /// Cargar la run usa esta copia, nunca el <c>/data</c> del disco. Ordenada por ruta ordinal.
    /// </summary>
    public IReadOnlyDictionary<string, string> DataSnapshot { get; init; } =
        new SortedDictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Mapa del acto actual.</summary>
    public ActMap CurrentMap => MapOf(Act);

    /// <summary>Mapa del acto indicado.</summary>
    public ActMap MapOf(int act)
    {
        for (int i = 0; i < Maps.Count; i++)
        {
            if (Maps[i].Act == act)
            {
                return Maps[i];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(act), act, "la run no tiene mapa para ese acto");
    }

    /// <summary>
    /// Jugadores que pueden alinearse ahora mismo (RF-002e). Es consultable en todo momento y es la
    /// cifra que se compara con <see cref="RunRules.MinimumAvailablePlayers"/>.
    /// </summary>
    public int AvailablePlayerCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].IsAvailable)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>True si la plantilla ha bajado del mínimo de 5 (RF-002b).</summary>
    public bool IsBelowMinimum => AvailablePlayerCount < RunRules.MinimumAvailablePlayers;

    /// <summary>Jugadores alineables, en orden de id ascendente.</summary>
    public IReadOnlyList<RunPlayer> AvailablePlayers
    {
        get
        {
            var players = new List<RunPlayer>(Roster.Count);
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].IsAvailable)
                {
                    players.Add(Roster[i]);
                }
            }

            return players;
        }
    }

    /// <summary>Jugador con ese id, o null.</summary>
    public RunPlayer? FindPlayer(int id)
    {
        for (int i = 0; i < Roster.Count; i++)
        {
            if (Roster[i].Id == id)
            {
                return Roster[i];
            }
        }

        return null;
    }

    /// <summary>Jugador con ese id; lanza si no está en la plantilla.</summary>
    public RunPlayer GetPlayer(int id) =>
        FindPlayer(id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "no hay ningún jugador con ese id en la plantilla");

    /// <summary>Nodo con ese id en cualquiera de los tres actos; null si no existe.</summary>
    public MapNode? FindNode(int nodeId)
    {
        for (int i = 0; i < Maps.Count; i++)
        {
            var node = Maps[i].Find(nodeId);
            if (node is not null)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>Nodo con ese id; lanza si no existe.</summary>
    public MapNode GetNode(int nodeId) =>
        FindNode(nodeId) ?? throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "la run no tiene ningún nodo con ese id");

    // ------------------------------------------------------------------ With*

    /// <summary>Copia con el oro indicado. Nunca baja de 0.</summary>
    public RunState WithGold(int gold) => this with { Gold = gold < 0 ? 0 : gold };

    /// <summary>Copia sumando (o restando, con valor negativo) oro. Nunca baja de 0.</summary>
    public RunState AddGold(int delta) => WithGold(Gold + delta);

    /// <summary>Copia en el acto indicado, 1..3, colocada en la entrada del acto.</summary>
    public RunState WithAct(int act)
    {
        if (act < 1 || act > RunRules.Acts)
        {
            throw new ArgumentOutOfRangeException(nameof(act), act, $"el acto debe estar entre 1 y {RunRules.Acts}");
        }

        return this with { Act = act, CurrentNodeId = -1, PendingNodeId = -1, Phase = RunPhase.OnMap };
    }

    /// <summary>Copia situada en ese nodo del mapa.</summary>
    public RunState WithCurrentNode(int nodeId) => this with { CurrentNodeId = nodeId };

    /// <summary>Copia con ese nodo interactivo abierto (o -1 para cerrarlo) y la fase coherente.</summary>
    public RunState WithPendingNode(int nodeId) => this with
    {
        PendingNodeId = nodeId,
        Phase = nodeId < 0 ? RunPhase.OnMap : RunPhase.NodeOpen,
        NodeRerolls = nodeId < 0 ? 0 : NodeRerolls,
    };

    /// <summary>Copia en la fase indicada.</summary>
    public RunState WithPhase(RunPhase phase) => this with { Phase = phase };

    /// <summary>Copia con el desenlace indicado; si termina la run, la fase pasa a <see cref="RunPhase.Finished"/>.</summary>
    public RunState WithOutcome(RunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return this with { Result = outcome, Phase = outcome.IsOver ? RunPhase.Finished : Phase };
    }

    /// <summary>Copia con la plantilla indicada, reordenada por id ascendente.</summary>
    public RunState WithRoster(IEnumerable<RunPlayer> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var players = new List<RunPlayer>(roster);
        players.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        int next = NextPlayerId;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id >= next)
            {
                next = players[i].Id + 1;
            }
        }

        return this with { Roster = players, NextPlayerId = next };
    }

    /// <summary>Copia sustituyendo a un jugador de la plantilla por otro con el mismo id.</summary>
    public RunState WithPlayer(RunPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var players = new List<RunPlayer>(Roster.Count);
        bool found = false;
        for (int i = 0; i < Roster.Count; i++)
        {
            if (Roster[i].Id == player.Id)
            {
                players.Add(player);
                found = true;
            }
            else
            {
                players.Add(Roster[i]);
            }
        }

        if (!found)
        {
            throw new ArgumentOutOfRangeException(nameof(player), player.Id, "no hay ningún jugador con ese id en la plantilla");
        }

        return this with { Roster = players };
    }

    /// <summary>Copia con un jugador más. Le asigna <see cref="NextPlayerId"/> si su id es negativo.</summary>
    public RunState WithNewPlayer(RunPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var added = player.Id < 0 ? player with { Id = NextPlayerId } : player;
        if (FindPlayer(added.Id) is not null)
        {
            throw new ArgumentException($"ya hay un jugador con el id {added.Id} en la plantilla", nameof(player));
        }

        var players = new List<RunPlayer>(Roster) { added };
        players.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return this with { Roster = players, NextPlayerId = Math.Max(NextPlayerId, added.Id + 1) };
    }

    /// <summary>Copia sin el jugador indicado (venta, RF-114f). No reutiliza su id.</summary>
    public RunState WithoutPlayer(int playerId)
    {
        var players = new List<RunPlayer>(Roster.Count);
        for (int i = 0; i < Roster.Count; i++)
        {
            if (Roster[i].Id != playerId)
            {
                players.Add(Roster[i]);
            }
        }

        return this with { Roster = players };
    }

    /// <summary>Copia con esa alineación (RF-041).</summary>
    public RunState WithLineup(Lineup lineup)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        return this with { Lineup = lineup };
    }

    /// <summary>Copia con esos consumibles equipados (RF-080..082).</summary>
    public RunState WithConsumables(IEnumerable<EquippedConsumable> consumables)
    {
        ArgumentNullException.ThrowIfNull(consumables);
        return this with { Consumables = new List<EquippedConsumable>(consumables) };
    }

    /// <summary>Copia con esos árbitros (RF-061b).</summary>
    public RunState WithReferees(IEnumerable<RunReferee> referees)
    {
        ArgumentNullException.ThrowIfNull(referees);
        return this with { Referees = new List<RunReferee>(referees) };
    }

    /// <summary>Copia sustituyendo el mapa de su acto.</summary>
    public RunState WithMap(ActMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var maps = new List<ActMap>(Maps.Count);
        bool found = false;
        for (int i = 0; i < Maps.Count; i++)
        {
            if (Maps[i].Act == map.Act)
            {
                maps.Add(map);
                found = true;
            }
            else
            {
                maps.Add(Maps[i]);
            }
        }

        if (!found)
        {
            maps.Add(map);
            maps.Sort(static (a, b) => a.Act.CompareTo(b.Act));
        }

        return this with { Maps = maps };
    }

    /// <summary>Copia con una entrada más en el historial de nodos.</summary>
    public RunState WithNodeCompleted(int nodeId, NodeKind kind, NodeResult result)
    {
        var history = new List<NodeHistoryEntry>(NodeHistory) { new(nodeId, kind, result) };
        return this with { NodeHistory = history };
    }

    /// <summary>Copia con ese contador de run fijado al valor indicado.</summary>
    public RunState WithCounter(string name, int value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, current) in Counters)
        {
            counters[key] = current;
        }

        counters[name] = value;
        return this with { Counters = counters };
    }

    /// <summary>Valor de un contador de run, 0 si no está.</summary>
    public int Counter(string name) => Counters.TryGetValue(name, out int value) ? value : 0;

    /// <summary>Copia con ese contador de logro fijado al valor indicado (RF-125b).</summary>
    public RunState WithAchievement(string name, int value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var achievements = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, current) in Achievements)
        {
            achievements[key] = current;
        }

        achievements[name] = value;
        return this with { Achievements = achievements };
    }

    /// <summary>Copia con los rerolls indicados (RF-071b).</summary>
    public RunState WithRerolls(int rerollsUsed, int nodeRerolls) =>
        this with { RerollsUsed = rerollsUsed, NodeRerolls = nodeRerolls };

    /// <summary>Copia con la instantánea de <c>/data</c> indicada (RT-061b), ordenada por ruta ordinal.</summary>
    public RunState WithDataSnapshot(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, content) in files)
        {
            sorted[path] = content;
        }

        return this with { DataSnapshot = sorted };
    }
}
