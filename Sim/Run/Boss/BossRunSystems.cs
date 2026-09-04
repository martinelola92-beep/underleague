using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Bosses;

/// <summary>
/// Envoltorio de <see cref="IRunSystems"/> que aporta el paquete Y: los jefes de
/// <c>data/bosses/</c>. Todo lo que no es jefe se delega en el sistema interior (el del paquete W hoy,
/// el del paquete X cuando exista), de modo que los dos paquetes se componen sin tocarse.
///
/// <list type="bullet">
/// <item><see cref="OpponentFor"/>: en un nodo de jefe, el rival es la plantilla del jefe del acto
/// (RF-001c: la del acto 3, íntegramente legendaria), no un rival procedural.</item>
/// <item><see cref="BossRuleModifiers"/>: los ids de los modificadores del jefe del acto — el gancho que
/// el paquete W dejó abierto. Uno en los actos 1 y 2, dos en el 3 (RF-001b, RF-001c).</item>
/// <item><see cref="AfterMatch"/>: aplica la condición de derrota propia del jefe final (RF-001c, D-9).
/// Se resuelve aquí porque el motor no la conoce: un partido ganado en el gol de oro sigue siendo un
/// partido ganado para <c>Simulator.Run</c>, y es la regla del jefe la que lo convierte en derrota.</item>
/// <item><see cref="TransformMatch"/>: aplica <see cref="BossRules.Apply"/> al once del jugador. Lo llama
/// <c>RunEngine.BuildMatch</c>, así que el partido que el bucle de run <b>juega</b> y el que el informe de
/// ojeo <b>enseña</b> (RF-012b, RF-012d) son literalmente el mismo objeto, construido por la misma
/// llamada. Es la costura que el paquete Y dejó anotada como Y-8.</item>
/// </list>
/// </summary>
public sealed class BossRunSystems : IRunSystems
{
    private readonly IRunSystems _inner;

    public BossRunSystems(BossCatalog bosses, IRunSystems? inner = null)
    {
        ArgumentNullException.ThrowIfNull(bosses);
        Bosses = bosses;
        _inner = inner ?? DefaultRunSystems.Instance;
    }

    /// <summary>Catálogo de jefes con el que se juega la run (instantánea de <c>/data</c>, RT-061b).</summary>
    public BossCatalog Bosses { get; }

    /// <summary>
    /// Copia del estado con el id del jefe de cada acto anotado en su mapa. Se llama justo después de
    /// <c>RunEngine.Start</c>. <c>ActMap.BossModifierId</c> guarda el <b>id del jefe</b>, no el de un
    /// modificador: el jefe final tiene dos y el campo es uno solo; el catálogo resuelve el id del jefe
    /// a su lista de modificadores (RF-014b: lo que se registra en el compendio son esos ids).
    /// </summary>
    public RunState AssignBosses(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var next = state;
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            next = next.WithMap(next.MapOf(act).WithBossModifier(Bosses.ForAct(act).Id));
        }

        return next;
    }

    /// <summary>
    /// Partido de un nodo de jefe con los modificadores ya aplicados al equipo del jugador: el informe de
    /// ojeo (RF-012b, RF-012d: nada de lo que pase en el partido puede no estar anunciado).
    ///
    /// <para>Desde que <c>RunEngine.BuildMatch</c> llama a <see cref="TransformMatch"/> esto es
    /// literalmente <c>RunEngine.BuildMatch(state, nodeId, catalog, this)</c>; se conserva como nombre
    /// del informe de ojeo, y porque que ojeo y partido sean la <b>misma llamada</b> es exactamente lo
    /// que RF-012d pide.</para>
    /// </summary>
    public (MatchSetup Setup, ulong Seed, MatchLineup Lineup) BuildBossMatch(
        RunState state, int nodeId, Catalog catalog) =>
        RunEngine.BuildMatch(state, nodeId, catalog, this);

    /// <inheritdoc />
    public MatchSetup TransformMatch(
        RunState state, MapNode node, MatchSetup setup, int playerTeamIndex, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(node);
        var transformed = _inner.TransformMatch(state, node, setup, playerTeamIndex, catalog);
        if (node.Kind != NodeKind.Boss)
        {
            return transformed;
        }

        var modifiers = Bosses.Modifiers(BossRuleModifiers(state, node, catalog));
        return BossRules.Apply(transformed, playerTeamIndex, modifiers, catalog);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> BossRuleModifiers(RunState state, MapNode node, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Kind == NodeKind.Boss ? Bosses.ForAct(node.Act).ModifierIds : Array.Empty<string>();
    }

    /// <inheritdoc />
    public TeamSetup OpponentFor(RunState state, MapNode node, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(catalog);
        if (node.Kind != NodeKind.Boss)
        {
            return _inner.OpponentFor(state, node, catalog);
        }

        var boss = Bosses.ForAct(node.Act);
        var rng = RngStreams.Generation(state.Seed, node.Id);
        return boss.Template.ToTeamSetup(ref rng, catalog, boss.Id, DefaultRunSystems.OpponentFirstPlayerId);
    }

    /// <inheritdoc />
    public RunState AfterMatch(RunState state, MapNode node, RunMatchSummary summary, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(summary);

        var next = _inner.AfterMatch(state, node, summary, catalog);
        if (node.Kind != NodeKind.Boss || !summary.Won)
        {
            return next;
        }

        var boss = Bosses.ForAct(node.Act);
        if (!BossRules.DefeatConditionMet(boss.DefeatCondition, summary.Report))
        {
            return next;
        }

        // La condición propia del jefe no es una tercera vía de derrota del estado (RF-002b sigue
        // teniendo dos): es que el partido del jefe no se ha superado, así que la causa es la misma.
        return next.WithOutcome(new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, node.Id));
    }

    /// <inheritdoc />
    public IReadOnlyList<RunReferee> CreateReferees(ulong seed, int count, Catalog catalog) =>
        _inner.CreateReferees(seed, count, catalog);

    /// <inheritdoc />
    public RefereeSetup RefereeFor(RunState state, MapNode node, Catalog catalog) =>
        _inner.RefereeFor(state, node, catalog);

    /// <inheritdoc />
    public SimConfig MatchConfig(RunState state, MapNode node) => _inner.MatchConfig(state, node);

    /// <inheritdoc />
    public RunState OpenNode(RunState state, MapNode node, Catalog catalog) =>
        _inner.OpenNode(state, node, catalog);

    /// <inheritdoc />
    public RunState ApplyDecision(RunState state, RunDecision decision, Catalog catalog) =>
        _inner.ApplyDecision(state, decision, catalog);
}
