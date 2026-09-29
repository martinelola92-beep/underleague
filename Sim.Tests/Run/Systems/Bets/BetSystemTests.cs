using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Bets;

namespace Underleague.Sim.Tests.Run.Systems.Bets;

/// <summary>ADR 0157, punto 1: la apuesta del nodo se deriva de (semilla, nodo), no se guarda.</summary>
public sealed class BetSystemTests
{
    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    private static IEnumerable<(RunState State, MapNode Node)> MatchNodes(ulong seed)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, Systems);
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            foreach (var node in state.MapOf(act).Nodes.Where(n => n.IsMatch).OrderBy(n => n.Id))
            {
                yield return (state, node);
            }
        }
    }

    private static BetOffer? Offer(RunState state, MapNode node) =>
        BetSystem.OfferFor(state, node, Systems.Bets, Systems, SystemsTestSupport.Catalog);

    [Fact]
    public void TheSameSeedAndNodeOfferTheSameBet()
    {
        foreach (var (state, node) in MatchNodes(11UL))
        {
            Assert.Equal(Offer(state, node), Offer(state, node));
        }

        // Una run distinta reconstruida desde la misma semilla ofrece lo mismo: no hay estado oculto.
        var first = MatchNodes(11UL).Select(t => Offer(t.State, t.Node)).ToList();
        var second = MatchNodes(11UL).Select(t => Offer(t.State, t.Node)).ToList();
        Assert.Equal(first, second);
    }

    [Fact]
    public void OnlyMatchNodesOfferABet()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 3UL, SystemsTestSupport.Catalog, Systems);
        foreach (var node in state.MapOf(1).Nodes.Where(n => !n.IsMatch))
        {
            Assert.Null(Offer(state, node));
        }

        Assert.NotNull(Offer(state, state.MapOf(1).Nodes.First(n => n.Kind == NodeKind.LeagueMatch)));
        Assert.NotNull(Offer(state, state.MapOf(1).Nodes.First(n => n.Kind == NodeKind.Boss)));
    }

    /// <summary>Repartido sobre muchas semillas y nodos, salen las once condiciones (el sorteo no se atasca).</summary>
    [Fact]
    public void EveryConditionIsOfferedAcrossSeedsAndNodes()
    {
        var seen = new HashSet<BetKind>();
        for (ulong seed = 1; seed <= 25 && seen.Count < Enum.GetValues<BetKind>().Length; seed++)
        {
            foreach (var (state, node) in MatchNodes(seed))
            {
                seen.Add(Offer(state, node)!.Kind);
            }
        }

        Assert.Equal(Enum.GetValues<BetKind>().Length, seen.Count);
    }

    [Fact]
    public void TheStakeFollowsTheActAndThePayoutTheDifficulty()
    {
        foreach (var (state, node) in MatchNodes(5UL))
        {
            var offer = Offer(state, node)!;
            var bet = Systems.Bets.Find(offer.BetId)!;
            Assert.Equal(bet.StakeFor(node.Act), offer.Stake);
            Assert.Equal(bet.PayoutPercentFor(node.Difficulty), offer.PayoutPercent);
            Assert.Equal(offer.Stake * offer.PayoutPercent / 100, offer.Payout);
        }
    }

    [Fact]
    public void HuntTheStarNamesTheRealRivalPlayerOfThatNode()
    {
        int hunts = 0;
        for (ulong seed = 1; seed <= 25; seed++)
        {
            foreach (var (state, node) in MatchNodes(seed))
            {
                var offer = Offer(state, node)!;
                if (offer.Kind != BetKind.HuntTheStar)
                {
                    Assert.Equal(-1, offer.TargetPlayerId);
                    Assert.Equal(string.Empty, offer.TargetPlayerName);
                    continue;
                }

                hunts++;
                var rival = Systems.OpponentFor(state, node, SystemsTestSupport.Catalog);
                var named = rival.Players.Single(p => p.Id == offer.TargetPlayerId);
                Assert.Equal(named.Name, offer.TargetPlayerName);
                Assert.Contains(rival.Lineup.Slots, s => s.PlayerId == named.Id);
            }
        }

        Assert.True(hunts > 0, "ninguna oferta de hunt_the_star en 25 semillas: el sorteo no la alcanza");
    }

    [Fact]
    public void TargetIsTheHighestRarityStarterWithTheLowestIdOnATie()
    {
        PlayerDefinition P(int id, Rarity rarity) => new(
            id, "p" + id, Race.Human, Position.Midfielder, rarity, 1, new Attributes(50, 50, 50, 50, 50),
            Array.Empty<Trait>(), Array.Empty<string>(), PhysicalState.Healthy);

        var players = new List<PlayerDefinition>
        {
            P(30, Rarity.Rare), P(10, Rarity.Rare), P(20, Rarity.Uncommon), P(5, Rarity.Legendary),
        };

        // El legendario (id 5) NO está en la alineación: no juega, no puede ser el objetivo.
        var lineup = new Lineup(new[]
        {
            new LineupSlot(30, new Cell(3, 3)), new LineupSlot(10, new Cell(4, 2)), new LineupSlot(20, new Cell(4, 4)),
        });
        var team = new TeamSetup("r", "r", Race.Human, players, lineup);

        Assert.Equal(10, BetSystem.TargetFor(team)!.Id);
    }
}
