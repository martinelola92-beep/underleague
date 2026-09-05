using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Consumables;

namespace Underleague.Sim.Run;

/// <summary>Motivo por el que una alineación merece una advertencia antes de confirmarla (RF-012d).</summary>
public enum LineupWarningKind
{
    /// <summary>
    /// Un titular sale al campo con una lesión grave sin tratar: si vuelve a lesionarse, <b>muere</b>
    /// (RF-093 vía 1). Es una decisión legítima del jugador —el precedente es RF-002d— pero la interfaz
    /// tiene que decirlo de forma explícita antes de confirmar.
    /// </summary>
    SevereInjuryDeathRisk,

    /// <summary>
    /// Se juega en inferioridad numérica, con 5 o 6 (RF-002d): con 5 en campo, una sola baja termina la
    /// run (RF-002b).
    /// </summary>
    Shorthanded,

    /// <summary>
    /// Este titular puede morir en este partido concreto por un perk letal del rival (RF-093 vía 2, ADR
    /// 0048). Desde que un jugador sano puede morir, esta advertencia <b>no</b> pregunta por su estado:
    /// pregunta por él contra <b>ese</b> rival y en <b>esa</b> casilla. Lleva número
    /// (<see cref="LineupWarning.Risk"/>) porque la condición 3 de la ADR 0048 —"se puede reducir el
    /// riesgo"— es lo único que separa el azar duro del injusto, y una advertencia sin número no se
    /// puede reducir.
    /// </summary>
    LethalOpponentRisk,
}

/// <summary>
/// Advertencia sobre una alineación (RF-012d, RF-002d, RF-093). Es dato estructurado, no texto: el texto
/// visible lo compone la interfaz desde <c>data/l10n</c>. <c>PlayerId</c> es -1 cuando la advertencia es
/// del equipo entero y no de un jugador concreto.
/// </summary>
/// <param name="Risk">
/// Probabilidad de que esa advertencia se cumpla, en base 10.000, o 0 si la advertencia no es de riesgo
/// cuantificado. Para <see cref="LineupWarningKind.LethalOpponentRisk"/> es la probabilidad de que ese
/// titular <b>muera en este partido</b>, sumada sobre todos los perks letales del rival y calculada con
/// la misma función que el motor usa para matar (<c>Sim.Perks.Lethality</c>): el número que se enseña y
/// el dado que se tira son el mismo (RF-012c, RF-012d).
/// </param>
public sealed record LineupWarning(LineupWarningKind Kind, int PlayerId, int Risk = 0);

/// <summary>
/// Lo que deja un nodo de partido resuelto (<see cref="RunEngine.EnterMatch"/>): el estado ya avanzado,
/// el resumen del partido y el desenlace de la run después de jugarlo.
/// </summary>
/// <param name="State">Estado tras el partido, sus consecuencias y <see cref="IRunSystems.AfterMatch"/>.</param>
/// <param name="Summary">Resumen del partido, con su <see cref="MatchReport"/> completo (RF-119).</param>
/// <param name="Outcome">Desenlace de la run tras el partido: en curso, victoria o derrota con su causa.</param>
public sealed record MatchEntry(RunState State, RunMatchSummary Summary, RunOutcome Outcome);

/// <summary>
/// Superficie pública del bucle de run (<c>fase2-diseno.md</c> §3). Pura y determinista, igual que
/// <c>Simulator.Run</c>: mismo (estado, semilla, catálogo) =&gt; mismo resultado, sin E/S y sin reloj
/// (RT-012, RT-013).
///
/// <para><b>Qué resuelve el paquete W</b>: el arranque de la run, la generación de los tres mapas, el
/// avance por el grafo, los nodos de partido (llamando a <c>Simulator.Run</c> con
/// <c>RngStreams.MatchSeed</c>, RT-022), la alineación, la progresión de después del partido y las
/// <b>dos</b> condiciones de derrota de RF-002b. Todo lo demás -mercado, clínica, economía,
/// recompensas, modificadores de jefe- entra por <see cref="IRunSystems"/>, que implementan los
/// paquetes X e Y.</para>
///
/// <para>El parámetro <c>systems</c> es opcional para no separarse de la firma de §3; cuando se omite
/// se usa <see cref="DefaultRunSystems"/>, que resuelve los partidos con rivales procedurales y deja
/// sin efecto (o lanza <see cref="NotSupportedException"/>) lo que todavía no existe.</para>
/// </summary>
public static class RunEngine
{
    /// <summary>
    /// Empieza una run: congela la instantánea de <c>/data</c> (RT-061b), crea la plantilla inicial
    /// (RF-005), genera los mapas de los tres actos con el flujo <c>RngStreams.Map</c> (RT-022) y deja
    /// la run en la entrada del acto 1.
    /// </summary>
    public static RunState Start(RunSetup setup, ulong seed, Catalog catalog, IRunSystems? systems = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(catalog);
        systems ??= DefaultRunSystems.Instance;

        if (setup.RefereeCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(setup), setup.RefereeCount, "la run necesita al menos un árbitro (RF-061b)");
        }

        var roster = setup.Roster is { Count: > 0 }
            ? SortById(setup.Roster)
            : GenerateRoster(setup, seed, catalog);

        if (roster.Count < RunRules.MinimumAvailablePlayers)
        {
            throw new ArgumentException(
                $"la plantilla inicial tiene {roster.Count} jugadores y el mínimo para jugar son "
                    + $"{RunRules.MinimumAvailablePlayers} (RF-002b)",
                nameof(setup));
        }

        var maps = new List<ActMap>(RunRules.Acts);
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var opponents = OpponentIdsOf(setup, act);
            maps.Add(MapGenerator.Generate(seed, act, new MapOptions(setup.NodesOfAct(act), opponents)));
        }

        var state = new RunState
        {
            SchemaVersion = RunState.CurrentSchemaVersion,
            Seed = seed,
            Division = setup.Division,
            ClubId = setup.ClubId,
            ClubRace = setup.ClubRace,
            Act = 1,
            CurrentNodeId = -1,
            PendingNodeId = -1,
            Phase = RunPhase.OnMap,
            Gold = setup.StartingGold,
            Maps = maps,
            Result = RunOutcome.InProgress,
        }
            .WithRoster(roster)
            .WithReferees(systems.CreateReferees(seed, setup.RefereeCount, catalog))
            .WithDataSnapshot(setup.DataSnapshot);

        return state.WithLineup(RunLineup.Default(state));
    }

    /// <summary>
    /// Nodos a los que se puede entrar ahora: los de entrada del acto si todavía no se ha entrado en
    /// ninguno, y si no, los sucesores del nodo actual (RF-010: sin retroceso). Lista vacía si la run
    /// ha terminado o si hay un nodo interactivo abierto.
    /// </summary>
    public static IReadOnlyList<MapNode> AvailableNodes(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase != RunPhase.OnMap || Outcome(state).IsOver)
        {
            return Array.Empty<MapNode>();
        }

        var map = state.CurrentMap;
        var ids = state.CurrentNodeId < 0 ? map.EntryNodeIds : map.Get(state.CurrentNodeId).Next;
        var nodes = new List<MapNode>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            nodes.Add(map.Get(ids[i]));
        }

        return nodes;
    }

    /// <summary>
    /// Entra en un nodo: si es de partido lo resuelve entero; si no, lo abre a través de
    /// <see cref="IRunSystems.OpenNode"/>. Lanza <see cref="ArgumentException"/> si el nodo no es uno de
    /// los que devuelve <see cref="AvailableNodes"/>.
    /// </summary>
    public static RunState Enter(RunState state, int nodeId, Catalog catalog, IRunSystems? systems = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        systems ??= DefaultRunSystems.Instance;

        if (Outcome(state).IsOver)
        {
            throw new InvalidOperationException("la run ha terminado: no se puede entrar en más nodos");
        }

        if (state.Phase != RunPhase.OnMap)
        {
            throw new InvalidOperationException(
                $"hay un nodo abierto ({state.PendingNodeId}): resuélvelo con Apply antes de entrar en otro");
        }

        var node = Accessible(state, nodeId);
        return node.IsMatch
            ? ResolveMatch(state, node, catalog, systems).State
            : EnterInteractive(state, node, catalog, systems);
    }

    /// <summary>
    /// Entra en un nodo de <b>partido</b> y devuelve, además del estado, el <b>resumen del partido</b>
    /// que se acaba de jugar (RF-119). Es exactamente lo que hace
    /// <see cref="Enter"/> —mismo estado resultante, mismo orden de llamadas, mismas semillas—, pero sin
    /// tirar el resumen por el camino.
    ///
    /// <para>Existe porque la firma de <see cref="Enter"/> es <c>(estado) =&gt; estado</c> y el resumen
    /// no cabe en ella. La pantalla de partido y la del informe post-partido necesitan el
    /// <see cref="MatchReport"/> con sus eventos y sus activaciones de perk, y RT-014 les prohíbe
    /// volver a simular nada para conseguirlo: o se lo da el motor, o se lo inventan. Es también el
    /// único camino por el que el resumen llega cuando el partido <b>termina la run</b> (RF-002b), que
    /// es justo cuando <see cref="IRunSystems.AfterMatch"/> no se llega a llamar y no hay otro sitio
    /// del que sacarlo.</para>
    /// </summary>
    public static MatchEntry EnterMatch(RunState state, int nodeId, Catalog catalog, IRunSystems? systems = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        systems ??= DefaultRunSystems.Instance;

        if (Outcome(state).IsOver)
        {
            throw new InvalidOperationException("la run ha terminado: no se puede entrar en más nodos");
        }

        if (state.Phase != RunPhase.OnMap)
        {
            throw new InvalidOperationException(
                $"hay un nodo abierto ({state.PendingNodeId}): resuélvelo con Apply antes de entrar en otro");
        }

        var node = Accessible(state, nodeId);
        if (!node.IsMatch)
        {
            throw new ArgumentException($"el nodo {nodeId} es de tipo {node.Kind} y no se juega", nameof(nodeId));
        }

        return ResolveMatch(state, node, catalog, systems);
    }

    /// <summary>
    /// Aplica una decisión del jugador. El paquete W resuelve <see cref="SetLineup"/>,
    /// <see cref="SetConsumables"/> y <see cref="LeaveNode"/>; el resto se delega en
    /// <see cref="IRunSystems.ApplyDecision"/>. Tras aplicarla se vuelve a comprobar el mínimo de
    /// plantilla (RF-002b): vender un jugador también puede terminar la run.
    /// </summary>
    public static RunState Apply(RunState state, RunDecision decision, Catalog catalog, IRunSystems? systems = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(catalog);
        systems ??= DefaultRunSystems.Instance;

        if (Outcome(state).IsOver)
        {
            throw new InvalidOperationException("la run ha terminado: no se admiten más decisiones");
        }

        var next = decision switch
        {
            SetLineup setLineup => ApplyLineup(state, setLineup),
            SetConsumables setConsumables => ApplyConsumables(state, setConsumables),
            LeaveNode => CloseOpenNode(state),
            _ => systems.ApplyDecision(state, decision, catalog),
        };

        return Stamp(next);
    }

    /// <summary>
    /// Lo que la interfaz debe advertir <b>antes</b> de confirmar una alineación (RF-012d): que se juega
    /// en inferioridad (RF-002d) y qué titulares arrastran una lesión grave sin tratar y por tanto pueden
    /// morir (RF-093 vía 1). Devuelve dato estructurado, no texto: el texto visible lo compone la
    /// interfaz desde <c>data/l10n</c>.
    ///
    /// <para>Ordenado: primero la advertencia de equipo, si la hay, y luego las de jugador por id
    /// ascendente. Con <paramref name="lineup"/> a null se examina la alineación guardada.</para>
    /// </summary>
    public static IReadOnlyList<LineupWarning> LineupWarnings(RunState state, Lineup? lineup = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var slots = (lineup ?? state.Lineup).Slots;
        var warnings = new List<LineupWarning>();
        if (slots.Count < RunRules.MaxStarters)
        {
            warnings.Add(new LineupWarning(LineupWarningKind.Shorthanded, -1));
        }

        var ids = new List<int>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            var player = state.FindPlayer(slots[i].PlayerId);
            if (player is { PhysicalState: PhysicalState.SevereInjury })
            {
                ids.Add(player.Id);
            }
        }

        ids.Sort();
        for (int i = 0; i < ids.Count; i++)
        {
            warnings.Add(new LineupWarning(LineupWarningKind.SevereInjuryDeathRisk, ids[i]));
        }

        return warnings;
    }

    /// <summary>
    /// Las mismas advertencias <b>contra el rival concreto de ese nodo</b> (RF-012c, ADR 0048): a las de
    /// la alineación se le suma una por titular al que un perk letal del rival puede matar, <b>con su
    /// probabilidad</b> (<see cref="LineupWarning.Risk"/>, base 10.000).
    ///
    /// <para>Es la condición 3 de la ADR 0048, la que sostiene todo lo demás: desde que un jugador sano
    /// puede morir, "se sabía antes" no basta, hay que <b>poder hacer algo</b>. Y el número se mueve con
    /// las tres cosas que el jugador decide —a quién alinea, en qué estado y en qué casilla— porque
    /// <see cref="Underleague.Sim.Perks.Lethality.Chance"/> depende de las tres. Pasar un
    /// <paramref name="lineup"/> distinto devuelve números distintos: eso es lo que convierte el azar en
    /// decisión.</para>
    ///
    /// <para>Ordenado como el resto: primero la advertencia de equipo, luego las de jugador por id
    /// ascendente, y dentro de un jugador la de lesión grave antes que la de perk letal.</para>
    /// </summary>
    public static IReadOnlyList<LineupWarning> LineupWarnings(
        RunState state, int nodeId, Catalog catalog, IRunSystems? systems = null, Lineup? lineup = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);

        var warnings = new List<LineupWarning>(LineupWarnings(state, lineup));
        var risks = LethalRisks(state, nodeId, catalog, systems, lineup);
        for (int i = 0; i < risks.Count; i++)
        {
            if (risks[i].Risk > 0)
            {
                warnings.Add(risks[i]);
            }
        }

        warnings.Sort(static (a, b) =>
        {
            int byPlayer = a.PlayerId.CompareTo(b.PlayerId);
            return byPlayer != 0 ? byPlayer : ((int)a.Kind).CompareTo((int)b.Kind);
        });

        return warnings;
    }

    /// <summary>
    /// Riesgo de muerte por perk letal rival de <b>cada titular</b> de esa alineación en ese nodo, en
    /// base 10.000, por id de jugador ascendente y sin filtrar los ceros (RF-012c). Es el dato con el que
    /// la pantalla de alineación pinta el indicador y con el que una política automática decide.
    ///
    /// <para>Se compone perk a perk como probabilidad de que <b>al menos uno</b> acierte, con la misma
    /// función que el motor usa para matar y sobre los mismos atributos base, así que no es una
    /// estimación: es el número. Un portador que no está en el once rival no cuenta —no va a saltar al
    /// campo— y un nodo que no es de partido no tiene riesgo.</para>
    /// </summary>
    public static IReadOnlyList<LineupWarning> LethalRisks(
        RunState state, int nodeId, Catalog catalog, IRunSystems? systems = null, Lineup? lineup = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        systems ??= DefaultRunSystems.Instance;

        var slots = (lineup ?? state.Lineup).Slots;
        var risks = new List<LineupWarning>(slots.Count);

        var node = state.GetNode(nodeId);
        if (!node.IsMatch)
        {
            return risks;
        }

        var carriers = Underleague.Sim.Perks.Lethality.CarriersOf(
            systems.OpponentFor(state, node, catalog), catalog);
        if (carriers.Count == 0)
        {
            return risks;
        }

        var lethality = catalog.Tuning.Injury.Lethality;
        var ordered = new List<LineupSlot>(slots);
        ordered.Sort(static (a, b) => a.PlayerId.CompareTo(b.PlayerId));

        var exposed = new List<Underleague.Sim.Perks.Lethality.Exposed>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            var player = state.FindPlayer(ordered[i].PlayerId);
            if (player is null || player.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            var definition = player.ToDefinition(catalog);
            exposed.Add(new Underleague.Sim.Perks.Lethality.Exposed(
                ordered[i].PlayerId, definition.PhysicalState, definition.Attributes.Stamina, ordered[i].HomeCell));
        }

        var marked = Underleague.Sim.Perks.Lethality.MarkedRisks(lethality, carriers, exposed);
        for (int i = 0; i < exposed.Count; i++)
        {
            risks.Add(new LineupWarning(LineupWarningKind.LethalOpponentRisk, exposed[i].PlayerId, marked[i]));
        }

        return risks;
    }

    /// <summary>
    /// Desenlace de la run (RF-002, RF-002b). Además del desenlace ya registrado en el estado,
    /// comprueba el contador de disponibles frente al mínimo (RF-002e), de modo que cualquier cambio de
    /// plantilla hecho por otro paquete -una venta, un mercenario que se marcha- termine la run igual
    /// que lo haría una lesión.
    /// </summary>
    public static RunOutcome Outcome(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Result.IsOver)
        {
            return state.Result;
        }

        return state.IsBelowMinimum
            ? new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.NotEnoughPlayers, state.CurrentNodeId)
            : RunOutcome.InProgress;
    }

    /// <summary>
    /// Construye el partido de ese nodo tal y como se le pasaría a <c>Simulator.Run</c>. Público porque
    /// es lo que necesitan el informe de ojeo (RF-012b), el indicador de riesgo (RF-012c) y la
    /// reproducción de un partido desde la semilla (RF-120, RT-061).
    /// </summary>
    public static (MatchSetup Setup, ulong Seed, MatchLineup Lineup) BuildMatch(
        RunState state,
        int nodeId,
        Catalog catalog,
        IRunSystems? systems = null,
        IReadOnlyList<ManualActivation>? manualActivations = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        systems ??= DefaultRunSystems.Instance;

        var node = state.GetNode(nodeId);
        if (!node.IsMatch)
        {
            throw new ArgumentException($"el nodo {nodeId} es de tipo {node.Kind} y no se juega", nameof(nodeId));
        }

        var lineup = RunLineup.Build(state, catalog);

        // Los consumibles equipados entran con el equipo (RF-080..085). Los condicionales se resuelven
        // solos dentro del partido; el manual (RF-082) solo si el jugador lo pulsó, y eso llega como
        // parte del estado inicial en manualActivations (docs/arquitectura.md): volver a ejecutar el
        // partido con la misma lista reproduce exactamente lo mismo (RT-013, RT-061).
        var home = new TeamSetup(state.ClubId, state.ClubId, state.ClubRace, lineup.Starters, lineup.Lineup)
        {
            Consumables = state.Equipment.ForMatch(state.Consumables, manualActivations),
        };
        var away = systems.OpponentFor(state, node, catalog);
        var referee = systems.RefereeFor(state, node, catalog);

        // El último paso es el gancho de los sistemas (RF-001b/c): un modificador de regla de jefe es una
        // transformación del MatchSetup, y tiene que pasar por aquí para que el partido que se juega y el
        // que enseña el informe de ojeo sean el mismo (RF-012b, RF-012d). W-15: el jugador es local.
        var setup = systems.TransformMatch(state, node, new MatchSetup(home, away, referee), 0, catalog);
        return (setup, RngStreams.MatchSeed(state.Seed, node.Id), lineup);
    }

    // ------------------------------------------------------------------ interno

    /// <summary>
    /// Nodo accesible con ese id desde la posición actual (RF-010: sin retroceso); lanza si no lo es.
    /// </summary>
    private static MapNode Accessible(RunState state, int nodeId)
    {
        var available = AvailableNodes(state);
        for (int i = 0; i < available.Count; i++)
        {
            if (available[i].Id == nodeId)
            {
                return available[i];
            }
        }

        throw new ArgumentException(
            $"el nodo {nodeId} no es accesible desde la posición actual (nodo {state.CurrentNodeId}, acto {state.Act})",
            nameof(nodeId));
    }

    private static MatchEntry ResolveMatch(RunState state, MapNode node, Catalog catalog, IRunSystems systems)
    {
        var (setup, seed, lineup) = BuildMatch(state, node.Id, catalog, systems);
        var result = Simulator.Run(setup, seed, catalog, systems.MatchConfig(state, node, catalog));
        var applied = MatchResolution.Apply(state, node, lineup, result, catalog);

        var next = applied.State.WithCurrentNode(node.Id);

        // El modificador se revela por haber **jugado** el nodo de jefe, antes de mirar el desenlace
        // (RF-014b): perder contra el jefe es precisamente el caso en el que el jugador ya ha pagado la
        // sorpresa, y dejarlo sin registrar en el compendio sería cobrarla dos veces.
        if (node.Kind == NodeKind.Boss)
        {
            next = next.WithMap(next.MapOf(node.Act).WithBossModifierRevealed(true));
        }

        if (applied.Outcome.IsOver)
        {
            next = next.WithOutcome(applied.Outcome);
            return new MatchEntry(next, applied.Summary, applied.Outcome);
        }

        next = systems.AfterMatch(next, node, applied.Summary, catalog);
        next = next.WithPhase(next.PendingNodeId >= 0 ? RunPhase.NodeOpen : RunPhase.OnMap);

        if (node.Kind == NodeKind.Boss && applied.Summary.Won && next.PendingNodeId < 0)
        {
            next = next.WithAct(node.Act + 1);
        }

        next = Stamp(next);
        return new MatchEntry(next, applied.Summary, Outcome(next));
    }

    private static RunState EnterInteractive(RunState state, MapNode node, Catalog catalog, IRunSystems systems)
    {
        // El modificador del jefe se revela al llegar a su nodo (RF-014); los nodos interactivos no lo
        // tocan. Aquí solo se abre el nodo y se deja al sistema decidir si necesita decisiones.
        var opened = systems.OpenNode(state.WithCurrentNode(node.Id), node, catalog);
        if (opened.PendingNodeId >= 0)
        {
            return Stamp(opened.WithPhase(RunPhase.NodeOpen));
        }

        return Stamp(opened
            .WithNodeCompleted(node.Id, node.Kind, NodeResult.Completed)
            .WithPhase(RunPhase.OnMap));
    }

    private static RunState CloseOpenNode(RunState state)
    {
        if (state.Phase != RunPhase.NodeOpen || state.PendingNodeId < 0)
        {
            throw new InvalidOperationException("no hay ningún nodo abierto que cerrar");
        }

        var node = state.GetNode(state.PendingNodeId);
        var next = state.WithPendingNode(-1);

        // Un nodo de partido con recompensas pendientes ya se anotó al resolverse: no se anota dos veces.
        if (!node.IsMatch)
        {
            next = next.WithNodeCompleted(node.Id, node.Kind, NodeResult.Completed);
        }

        if (node.Kind == NodeKind.Boss && WonAt(state, node.Id))
        {
            next = node.Act >= RunRules.Acts
                ? next.WithOutcome(new RunOutcome(RunOutcomeKind.Victory, DefeatCause.None, node.Id))
                : next.WithAct(node.Act + 1);
        }

        return next;
    }

    private static bool WonAt(RunState state, int nodeId)
    {
        for (int i = state.NodeHistory.Count - 1; i >= 0; i--)
        {
            if (state.NodeHistory[i].NodeId == nodeId)
            {
                return state.NodeHistory[i].Result == NodeResult.Won;
            }
        }

        return false;
    }

    private static RunState ApplyLineup(RunState state, SetLineup decision)
    {
        ArgumentNullException.ThrowIfNull(decision.Lineup);
        var slots = decision.Lineup.Slots;
        if (slots.Count < RunRules.MinimumAvailablePlayers || slots.Count > RunRules.MaxStarters)
        {
            throw new ArgumentException(
                $"la alineación tiene {slots.Count} titulares; deben ser entre {RunRules.MinimumAvailablePlayers} "
                    + $"y {RunRules.MaxStarters} (RF-002d, RF-059)",
                nameof(decision));
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var player = state.FindPlayer(slots[i].PlayerId)
                ?? throw new ArgumentException($"la alineación incluye al jugador {slots[i].PlayerId}, que no está en la plantilla", nameof(decision));
            // RF-092 deja de ser un bloqueo duro y pasa a ser una advertencia (RF-093 vía 1): alinear a
            // alguien con una lesión grave sin tratar es una decisión legítima del jugador —el precedente
            // es la inferioridad numérica de RF-002d— con una consecuencia anunciada, que si vuelve a
            // lesionarse muere. Quien no puede volver al campo de ninguna manera es el muerto.
            // Las advertencias que la interfaz debe enseñar antes de confirmar las da LineupWarnings.
            if (player.PhysicalState == PhysicalState.Dead)
            {
                throw new ArgumentException(
                    $"la alineación incluye al jugador {player.Id}, que está muerto (RF-093)",
                    nameof(decision));
            }
        }

        return MarkSevereInjuryRisks(state, slots).WithLineup(decision.Lineup);
    }

    /// <summary>
    /// Deja anotado en el estado quién sale al campo con una lesión grave sin tratar, y borra la marca de
    /// quien ya no lo hace (RF-093 vía 1). La marca vale para <b>este</b> partido: sin ella,
    /// <c>RunLineup</c> no alinea a un lesionado grave ni aunque su nombre siga en la alineación
    /// guardada, de modo que arriesgarse es siempre una decisión tomada, nunca una herencia.
    /// </summary>
    private static RunState MarkSevereInjuryRisks(RunState state, IReadOnlyList<LineupSlot> slots)
    {
        var next = state;
        foreach (var (name, value) in state.Counters)
        {
            if (value != 0 && name.StartsWith(RunLineup.RiskCounterPrefix, StringComparison.Ordinal))
            {
                next = next.WithCounter(name, 0);
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var player = state.FindPlayer(slots[i].PlayerId);
            if (player is { PhysicalState: PhysicalState.SevereInjury })
            {
                next = next.WithCounter(RunLineup.RiskCounterPrefix + player.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
            }
        }

        return next;
    }

    private static RunState ApplyConsumables(RunState state, SetConsumables decision)
    {
        ArgumentNullException.ThrowIfNull(decision.Consumables);
        if (decision.Consumables.Count > 3)
        {
            throw new ArgumentException($"se pueden equipar 3 consumibles como máximo y se han pedido {decision.Consumables.Count} (RF-080)", nameof(decision));
        }

        if (decision.Consumables.Count > 0)
        {
            bool manual = false;
            int conditional = 0;
            for (int i = 0; i < decision.Consumables.Count; i++)
            {
                var consumable = decision.Consumables[i];
                if (consumable.Mode == ConsumableMode.Manual)
                {
                    manual = true;
                    continue;
                }

                conditional++;

                // El disparador se valida al equipar, no al empezar el partido: un disparador mal escrito
                // tiene que doler aquí y no convertirse en un consumible que nunca se dispara (RF-083).
                _ = ConsumableTriggers.Parse(consumable.Trigger);
            }

            if (!manual)
            {
                throw new ArgumentException("al menos uno de los consumibles equipados debe ser manual (RF-082)", nameof(decision));
            }

            if (conditional > 2)
            {
                throw new ArgumentException(
                    $"se pueden configurar 2 consumibles condicionales como máximo y se han pedido {conditional} (RF-081)",
                    nameof(decision));
            }
        }

        return state.WithConsumables(decision.Consumables);
    }

    /// <summary>
    /// Graba en el estado el desenlace que <see cref="Outcome"/> deduce. Se llama tras cada transición
    /// para que un estado guardado nunca esté "terminado de hecho pero en curso de nombre".
    /// </summary>
    private static RunState Stamp(RunState state)
    {
        var outcome = Outcome(state);
        return outcome.IsOver && !state.Result.IsOver ? state.WithOutcome(outcome) : state;
    }

    private static IReadOnlyList<string>? OpponentIdsOf(RunSetup setup, int act)
    {
        var byAct = setup.OpponentIdsByAct;
        if (byAct is null || act - 1 >= byAct.Count)
        {
            return null;
        }

        return byAct[act - 1];
    }

    /// <summary>
    /// Plantilla inicial procedural mientras <c>data/clubs/</c> no exista (paquete X): los 10 jugadores
    /// de RF-005 con <c>TeamGenerator</c> y los perks iniciales de su rareza (RF-023), sorteados con el
    /// flujo de generación, que es independiente del del mapa y del de los partidos (RT-022).
    /// </summary>
    private static List<RunPlayer> GenerateRoster(RunSetup setup, ulong seed, Catalog catalog)
    {
        var rng = RngStreams.Generation(seed, 0);
        var team = TeamGenerator.Generate(ref rng, catalog, setup.ClubId, setup.ClubRace, setup.GeneratedQuality, firstPlayerId: 0);
        var withPerks = PerkAssignment.AssignInitial(ref rng, team.Players, catalog);

        var roster = new List<RunPlayer>(withPerks.Count);
        for (int i = 0; i < withPerks.Count; i++)
        {
            roster.Add(RunPlayer.From(withPerks[i]));
        }

        roster.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return roster;
    }

    private static List<RunPlayer> SortById(IReadOnlyList<RunPlayer> players)
    {
        var sorted = new List<RunPlayer>(players);
        sorted.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return sorted;
    }
}
