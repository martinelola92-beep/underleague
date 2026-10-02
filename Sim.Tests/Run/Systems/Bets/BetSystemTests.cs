using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
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

    /// <summary>
    /// Repartido sobre muchas semillas y nodos salen todas las condiciones que alguna dificultad deja ofrecer
    /// (frecuencia medida >= 2 %), y ninguna que no: el sorteo no se atasca ni se salta la regla.
    /// </summary>
    [Fact]
    public void EveryOfferableConditionIsOfferedAndNoOtherIs()
    {
        var expected = Systems.Bets.All
            .Where(b => Enumerable.Range(1, 5).Any(d => b.FrequencyBasisPointsFor(d) >= BetSystem.MinOfferedBasisPoints))
            .Select(b => b.Kind)
            .ToHashSet();
        var seen = new HashSet<BetKind>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            foreach (var (state, node) in MatchNodes(seed))
            {
                var offer = Offer(state, node);
                if (offer is not null)
                {
                    seen.Add(offer.Kind);
                }
            }
        }

        Assert.Equal(expected.OrderBy(k => k), seen.OrderBy(k => k));
    }

    private static BetDefinition Bet(BetKind kind, int[] basisPoints) => new(
        "bet_" + kind, kind, new LocalizedName("n", "n"), new LocalizedName("c", "c"),
        new[] { 3, 4, 5 }, new[] { 200, 200, 200, 200, 200 }, basisPoints);

    /// <summary>
    /// La regla del 2 %: una apuesta cuya frecuencia medida en la dificultad del nodo es menor que el mínimo
    /// no se ofrece ahí (197 no, 200 sí); si ninguna llega, el nodo no ofrece nada.
    /// </summary>
    [Fact]
    public void ABetBelowTwoPercentInThatDifficultyIsNotOffered()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 9UL, SystemsTestSupport.Catalog, Systems);
        var node = state.MapOf(1).Nodes.First(n => n.Kind == NodeKind.LeagueMatch);
        int index = Math.Clamp(node.Difficulty, 1, 5) - 1;

        int[] Only(int atNode, int elsewhere) => Enumerable.Range(0, 5).Select(i => i == index ? atNode : elsewhere).ToArray();
        BetOffer? With(params BetDefinition[] bets) =>
            BetSystem.OfferFor(state, node, new BetCatalog(bets), Systems, SystemsTestSupport.Catalog);

        Assert.Null(With(Bet(BetKind.Comeback, Only(199, 5000))));
        Assert.Equal(BetKind.Comeback, With(Bet(BetKind.Comeback, Only(200, 0)))!.Kind);

        // Con dos, solo entra la elegible aquí, sea cual sea el sorteo.
        var eligible = Bet(BetKind.IntoTheMob, Only(200, 0));
        var blocked = Bet(BetKind.Comeback, Only(0, 5000));
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var other = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, Systems);
            var n = other.MapOf(1).Nodes.First(x => x.Id == node.Id);
            var offer = BetSystem.OfferFor(other, n, new BetCatalog(new[] { eligible, blocked }), Systems, SystemsTestSupport.Catalog);
            Assert.Equal(BetKind.IntoTheMob, offer!.Kind);
        }
    }

    /// <summary>
    /// ADR 0157, enmienda del 2 oct (decisión 2): una celda retirada (<c>withdrawnDifficulties</c>) no se ofrece
    /// nunca en esa dificultad, y control: en el resto de nodos la oferta es la misma que sin retirar nada, con
    /// la misma semilla (la retirada no desplaza las demás apuestas); solo cambian los nodos cuya primera tirada
    /// caía en una celda retirada, y ahí sale otra apuesta no retirada.
    /// </summary>
    [Fact]
    public void AWithdrawnCellIsNeverOfferedAndEveryOtherOfferIsUnchanged()
    {
        var withdrawn = Systems.Bets.All.Where(b => b.WithdrawnDifficulties is { Count: > 0 }).ToList();
        Assert.Equal(
            new[] { "into_the_mob", "split_the_goals" },
            withdrawn.Select(b => b.Id).OrderBy(i => i, StringComparer.Ordinal));
        Assert.All(withdrawn, b => Assert.Equal(new[] { 5 }, b.WithdrawnDifficulties));

        var unflagged = new BetCatalog(Systems.Bets.All.Select(b => b with { WithdrawnDifficulties = null }).ToList());
        int atRetiredCell = 0;
        int unchanged = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            foreach (var (state, node) in MatchNodes(seed))
            {
                var now = Offer(state, node);
                var before = BetSystem.OfferFor(state, node, unflagged, Systems, SystemsTestSupport.Catalog);
                Assert.NotNull(now);
                Assert.False(Systems.Bets.Find(now.BetId)!.IsWithdrawnAt(node.Difficulty));
                if (Systems.Bets.Find(before!.BetId)!.IsWithdrawnAt(node.Difficulty))
                {
                    atRetiredCell++;
                    Assert.NotEqual(before.BetId, now.BetId);
                }
                else
                {
                    unchanged++;
                    Assert.Equal(before, now);
                }
            }
        }

        Assert.True(atRetiredCell > 0, "el censo de semillas no cae nunca en una celda retirada: el control no prueba nada");
        Assert.True(unchanged > atRetiredCell, "la retirada debe dejar intactas casi todas las ofertas");
    }

    /// <summary>
    /// ADR 0157, enmienda del 2 oct (decisión 5), valor conocido: <c>comeback</c> en dificultad 1 anuncia la
    /// frecuencia medida (7,76 %) y paga round(85 / 7,76) = 1095 %: 33, 44 y 55 de oro por 3, 4 y 5 apostados.
    /// Su retorno esperado queda en -14,6 %, en línea con el resto (-15 % de diseño).
    /// </summary>
    [Theory]
    [InlineData(1, 3, 33)]
    [InlineData(2, 4, 44)]
    [InlineData(3, 5, 55)]
    public void ComebackOnDifficultyOneReturnsInLineWithTheRest(int act, int stake, int payout)
    {
        var comeback = Systems.Bets.Find(BetKind.Comeback)!;
        Assert.Equal(776, comeback.FrequencyBasisPointsFor(1));
        Assert.Equal(1095, comeback.PayoutPercentFor(1));
        Assert.Equal(stake, comeback.StakeFor(act));
        Assert.Equal(payout, BetSystem.PayoutFor(stake, comeback.PayoutPercentFor(1)));

        double expectedReturn = comeback.FrequencyBasisPointsFor(1) / 10000.0 * payout / stake;
        Assert.InRange(expectedReturn, 0.84, 0.86);
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
            Assert.Equal(BetSystem.PayoutFor(offer.Stake, offer.PayoutPercent), offer.Payout);
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

    private static PlayerDefinition Star(int id, Rarity rarity, Position position = Position.Midfielder, int level = 1, int attribute = 50) => new(
        id, "p" + id, Race.Human, position, rarity, level, new Attributes(attribute, attribute, attribute, attribute, attribute),
        Array.Empty<Trait>(), Array.Empty<string>(), PhysicalState.Healthy);

    private static TeamSetup Team(IReadOnlyList<PlayerDefinition> players, params int[] starters) => new(
        "r", "r", Race.Human, players,
        new Lineup(starters.Select((id, i) => new LineupSlot(id, new Cell(i % 7, i % 7))).ToList()));

    /// <summary>Desempate: rareza, luego nivel, luego suma de atributos, luego id menor; y el que no juega no cuenta.</summary>
    [Fact]
    public void TargetIsTheHighestRarityThenLevelThenAttributesThenLowestId()
    {
        var players = new List<PlayerDefinition>
        {
            Star(30, Rarity.Rare), Star(10, Rarity.Rare), Star(20, Rarity.Uncommon), Star(5, Rarity.Legendary),
        };

        // El legendario (id 5) NO está en la alineación: no juega, no puede ser el objetivo.
        Assert.Equal(10, BetSystem.TargetFor(Team(players, 30, 10, 20))!.Id);

        // Igual rareza: gana el nivel, aunque su id sea mayor.
        Assert.Equal(30, BetSystem.TargetFor(Team(new[] { Star(10, Rarity.Rare), Star(30, Rarity.Rare, level: 4) }, 10, 30))!.Id);

        // Igual rareza y nivel: gana la suma de atributos.
        Assert.Equal(30, BetSystem.TargetFor(Team(new[] { Star(10, Rarity.Rare), Star(30, Rarity.Rare, attribute: 60) }, 10, 30))!.Id);

        // Todo igual: id menor.
        Assert.Equal(10, BetSystem.TargetFor(Team(new[] { Star(30, Rarity.Rare), Star(10, Rarity.Rare) }, 30, 10))!.Id);
    }

    /// <summary>
    /// Regla J (revisión independiente): el portero, que es el slot 0 y gana el desempate por id, no puede ser
    /// la estrella ni aunque sea el de mayor rareza; sin jugadores de campo no hay estrella.
    /// </summary>
    [Fact]
    public void TheGoalkeeperIsNeverTheStar()
    {
        var goalkeeper = Star(0, Rarity.Legendary, Position.Goalkeeper, level: 9, attribute: 99);
        var outfield = Star(7, Rarity.Common);
        Assert.Equal(7, BetSystem.TargetFor(Team(new[] { goalkeeper, outfield }, 0, 7))!.Id);
        Assert.Null(BetSystem.TargetFor(Team(new[] { goalkeeper }, 0)));
    }

    /// <summary>
    /// Sobre los rivales reales de los tres actos, incluido el jefe final (todo <c>rare</c>, donde el desempate
    /// por id daba el portero el 100 % de las veces): la estrella nombrada jamás es un portero.
    /// </summary>
    [Fact]
    public void TheNamedStarIsNeverAGoalkeeperAgainstRealRivalsAndBosses()
    {
        int checkedNodes = 0;
        var bosses = BossCatalog.FromJson(TestData.LoadAllFiles());
        var systems = new BossRunSystems(bosses, Systems);
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var state = systems.AssignBosses(RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, systems));
            for (int act = 1; act <= RunRules.Acts; act++)
            {
                foreach (var node in state.MapOf(act).Nodes.Where(n => n.IsMatch))
                {
                    var rival = systems.OpponentFor(state, node, SystemsTestSupport.Catalog);
                    var star = BetSystem.TargetFor(rival);
                    Assert.NotNull(star);
                    Assert.NotEqual(Position.Goalkeeper, star!.Position);
                    checkedNodes++;
                }
            }
        }

        Assert.True(checkedNodes > 100);
    }

    /// <summary>El cobro se redondea al entero más cercano: truncar bajaba la esperanza de 0,85 a 0,73-0,83.</summary>
    [Fact]
    public void ThePayoutRoundsToTheNearestGold()
    {
        Assert.Equal(15, BetSystem.PayoutFor(3, 495));   // 14,85 -> 15 (truncaba a 14)
        Assert.Equal(14, BetSystem.PayoutFor(3, 466));   // 13,98 -> 14
        Assert.Equal(5, BetSystem.PayoutFor(3, 150));    // 4,5 -> 5 (medios hacia arriba)
        Assert.Equal(4, BetSystem.PayoutFor(3, 149));    // 4,47 -> 4
        Assert.Equal(60, BetSystem.PayoutFor(3, 2000));
    }
}
