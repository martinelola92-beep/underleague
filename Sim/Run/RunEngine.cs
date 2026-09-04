using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run;

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
            maps.Add(MapGenerator.Generate(seed, act, new MapOptions(setup.NodesPerAct, opponents)));
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

        var available = AvailableNodes(state);
        MapNode? node = null;
        for (int i = 0; i < available.Count; i++)
        {
            if (available[i].Id == nodeId)
            {
                node = available[i];
                break;
            }
        }

        if (node is null)
        {
            throw new ArgumentException(
                $"el nodo {nodeId} no es accesible desde la posición actual (nodo {state.CurrentNodeId}, acto {state.Act})",
                nameof(nodeId));
        }

        return node.IsMatch
            ? EnterMatch(state, node, catalog, systems)
            : EnterInteractive(state, node, catalog, systems);
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
        RunState state, int nodeId, Catalog catalog, IRunSystems? systems = null)
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
        var home = new TeamSetup(state.ClubId, state.ClubId, state.ClubRace, lineup.Starters, lineup.Lineup);
        var away = systems.OpponentFor(state, node, catalog);
        var referee = systems.RefereeFor(state, node, catalog);
        return (new MatchSetup(home, away, referee), RngStreams.MatchSeed(state.Seed, node.Id), lineup);
    }

    // ------------------------------------------------------------------ interno

    private static RunState EnterMatch(RunState state, MapNode node, Catalog catalog, IRunSystems systems)
    {
        var (setup, seed, lineup) = BuildMatch(state, node.Id, catalog, systems);
        var result = Simulator.Run(setup, seed, catalog, systems.MatchConfig(state, node));
        var applied = MatchResolution.Apply(state, node, lineup, result, catalog);

        var next = applied.State.WithCurrentNode(node.Id);
        if (applied.Outcome.IsOver)
        {
            return next.WithOutcome(applied.Outcome);
        }

        // El nodo de jefe revela su modificador al llegar (RF-014b) y, si se gana, abre el acto siguiente.
        if (node.Kind == NodeKind.Boss)
        {
            next = next.WithMap(next.MapOf(node.Act).WithBossModifierRevealed(true));
        }

        next = systems.AfterMatch(next, node, applied.Summary, catalog);
        next = next.WithPhase(next.PendingNodeId >= 0 ? RunPhase.NodeOpen : RunPhase.OnMap);

        if (node.Kind == NodeKind.Boss && applied.Summary.Won && next.PendingNodeId < 0)
        {
            next = next.WithAct(node.Act + 1);
        }

        return Stamp(next);
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
            if (!player.IsAvailable)
            {
                throw new ArgumentException(
                    $"la alineación incluye al jugador {player.Id}, que está {player.PhysicalState} y no puede jugar (RF-092, RF-093)",
                    nameof(decision));
            }
        }

        return state.WithLineup(decision.Lineup);
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
            for (int i = 0; i < decision.Consumables.Count; i++)
            {
                manual |= decision.Consumables[i].Mode == ConsumableMode.Manual;
            }

            if (!manual)
            {
                throw new ArgumentException("al menos uno de los consumibles equipados debe ser manual (RF-082)", nameof(decision));
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
