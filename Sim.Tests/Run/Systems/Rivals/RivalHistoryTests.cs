using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Tests.Run.Systems.Rivals;

/// <summary>
/// Historial de enfrentamientos contra rivales (RF-122, ADR 0124, enmienda R3): <see cref="RivalHistory"/>
/// se deriva de <see cref="RunState.NodeHistory"/> sin tocar el esquema de guardado. No hay atribución por
/// jugador aquí -<see cref="RivalEncounter"/> no lleva ningún id de jugador-, solo contra qué rival, en
/// qué nodo y con qué resultado.
/// </summary>
public sealed class RivalHistoryTests
{
    /// <summary>
    /// Mapa de un acto con: un partido de liga contra <c>rival_a</c> (nodo 1), uno de élite contra
    /// <c>rival_b</c> (nodo 2), un mercado sin rival (nodo 3), el jefe (nodo 4, con un <c>opponentId</c>
    /// fantasma que nadie más usa), y un segundo encuentro con <c>rival_a</c> en otro nodo (5).
    /// </summary>
    private static ActMap BuildMap() => new(
        1,
        new[]
        {
            new MapNode(1, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), "rival_a", 1),
            new MapNode(2, 1, 1, 0, NodeKind.EliteMatch, Array.Empty<int>(), "rival_b", 2),
            new MapNode(3, 1, 2, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0),
            new MapNode(4, 1, 3, 0, NodeKind.Boss, Array.Empty<int>(), "rival_a", 5),
            new MapNode(5, 1, 4, 0, NodeKind.LeagueMatch, Array.Empty<int>(), "rival_a", 1),
        },
        new[] { 1 },
        BossNodeId: 4,
        BossModifierId: string.Empty,
        BossModifierRevealed: false);

    /// <summary>
    /// Historial en orden cronológico: rival_a (nodo 1), rival_b (nodo 2), el mercado (sin rival), el
    /// jefe (nodo 4, opponentId "rival_a" fantasma) y un segundo encuentro con rival_a (nodo 5).
    /// </summary>
    private static RunState BuildState() =>
        new RunState()
            .WithMap(BuildMap())
            .WithNodeCompleted(1, NodeKind.LeagueMatch, NodeResult.Won)
            .WithNodeCompleted(2, NodeKind.EliteMatch, NodeResult.Lost)
            .WithNodeCompleted(3, NodeKind.Market, NodeResult.Completed)
            .WithNodeCompleted(4, NodeKind.Boss, NodeResult.Won)
            .WithNodeCompleted(5, NodeKind.LeagueMatch, NodeResult.Lost);

    [Fact]
    public void EncountersExcludeTheBossNode()
    {
        var state = BuildState();
        var encounters = RivalHistory.Encounters(state);

        Assert.DoesNotContain(encounters, e => e.Kind == NodeKind.Boss);
        Assert.DoesNotContain(encounters, e => e.NodeId == 4);
    }

    [Fact]
    public void EncountersAreInChronologicalOrder()
    {
        var state = BuildState();
        var encounters = RivalHistory.Encounters(state);

        // El mercado (nodo 3) no tiene rival y el jefe (nodo 4) está excluido: quedan 1, 2 y 5, en ese
        // orden -el mismo en el que se completaron, no el orden por id de nodo.
        Assert.Equal(
            new[] { (1, "rival_a"), (2, "rival_b"), (5, "rival_a") },
            encounters.Select(e => (e.NodeId, e.RivalId)).ToArray());
    }

    [Fact]
    public void AgainstFiltersByRivalAndKeepsOrder()
    {
        var state = BuildState();
        var againstA = RivalHistory.Against(state, "rival_a");

        Assert.Equal(2, againstA.Count);
        Assert.Equal(1, againstA[0].NodeId);
        Assert.Equal(NodeResult.Won, againstA[0].Result);
        Assert.Equal(5, againstA[1].NodeId);
        Assert.Equal(NodeResult.Lost, againstA[1].Result);

        var againstB = RivalHistory.Against(state, "rival_b");
        Assert.Single(againstB);
        Assert.Equal(2, againstB[0].NodeId);
    }

    [Fact]
    public void HasFacedIsFalseForARivalNeverPlayed()
    {
        var state = BuildState();

        Assert.True(RivalHistory.HasFaced(state, "rival_a"));
        Assert.True(RivalHistory.HasFaced(state, "rival_b"));
        Assert.False(RivalHistory.HasFaced(state, "rival_c"));

        // Y el fantasma del jefe no cuenta como haberse enfrentado a él por esa vía: si rival_a solo
        // apareciera en el nodo 4, HasFaced tendría que dar false.
        var bossOnly = new RunState()
            .WithMap(new ActMap(
                1,
                new[] { new MapNode(4, 1, 0, 0, NodeKind.Boss, Array.Empty<int>(), "rival_ghost", 5) },
                new[] { 4 },
                BossNodeId: 4,
                BossModifierId: string.Empty,
                BossModifierRevealed: false))
            .WithNodeCompleted(4, NodeKind.Boss, NodeResult.Won);

        Assert.False(RivalHistory.HasFaced(bossOnly, "rival_ghost"));
    }
}
