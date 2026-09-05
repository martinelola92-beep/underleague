using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Rewards;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0043: el trampolín y el desgaste por acto. La recompensa deja de ser la misma tras cualquier
/// victoria —el élite paga más y con rareza mejorada, el jefe paga mucho más, da <b>dos</b> perks y cura
/// la plantilla—, se puede <b>rechazar</b> (RF-071 obligaba a elegir una de las tres) y la probabilidad de
/// lesión escala por acto y en el nodo de élite.
/// </summary>
public sealed class NodeRewardTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    private static EconomyConfig Economy => Systems.Economy;

    /// <summary>El escalón de oro por tipo de nodo: liga &lt; élite &lt; jefe, con el mismo acto y dificultad.</summary>
    [Fact]
    public void GoldIsTieredByNodeKind()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 909UL, Catalog, Systems);
        int league = GoldFor(state, NodeKind.LeagueMatch);
        int elite = GoldFor(state, NodeKind.EliteMatch);
        int boss = GoldFor(state, NodeKind.Boss);

        Assert.True(elite > league, $"el élite paga {elite} y la liga {league}");
        Assert.True(boss > elite, $"el jefe paga {boss} y el élite {elite}");
        Assert.Equal(0, Economy.LeagueReward.GoldBonusPercent);
    }

    /// <summary>
    /// El jefe da <b>dos</b> elecciones y la de liga una: la pieza que convierte el final del acto en
    /// trampolín. La segunda elección trae un surtido distinto, no el mismo dos veces.
    /// </summary>
    [Fact]
    public void ABossNodeGivesTwoPicksWithDifferentOffers()
    {
        Assert.Equal(1, Economy.LeagueReward.Picks);
        Assert.Equal(1, Economy.EliteReward.Picks);
        Assert.Equal(2, Economy.BossReward.Picks);

        var state = SystemsTestSupport.WithFakePendingNode(
            RunEngine.Start(SystemsTestSupport.Setup(), 5150UL, Catalog, Systems), NodeKind.Boss);
        var node = state.GetNode(state.PendingNodeId);

        var first = RewardSystem.Options(state, node, Catalog, Economy, Systems.Items);
        Assert.False(RewardSystem.AlreadyClaimed(state, node, Economy));

        var afterFirst = RunEngine.Apply(state, new DeclineReward(), Catalog, Systems);
        Assert.Equal(1, RewardSystem.PicksTaken(afterFirst, node.Id));
        Assert.False(RewardSystem.AlreadyClaimed(afterFirst, node, Economy));

        var second = RewardSystem.Options(afterFirst, node, Catalog, Economy, Systems.Items);
        Assert.NotEqual(first.Select(Describe), second.Select(Describe));

        var afterSecond = RunEngine.Apply(afterFirst, new DeclineReward(), Catalog, Systems);
        Assert.True(RewardSystem.AlreadyClaimed(afterSecond, node, Economy));
        Assert.Throws<InvalidOperationException>(() => RewardSystem.Decline(afterSecond, Economy));
    }

    /// <summary>
    /// Rechazar (ADR 0043) consume la elección y no toca la plantilla: con perks irreversibles (RF-072) y
    /// slots limitados (RF-023), no llevarse nada es una jugada legítima.
    /// </summary>
    [Fact]
    public void DecliningTakesNothingAndConsumesThePick()
    {
        var state = SystemsTestSupport.WithFakePendingNode(
            RunEngine.Start(SystemsTestSupport.Setup(), 6161UL, Catalog, Systems), NodeKind.LeagueMatch);
        var node = state.GetNode(state.PendingNodeId);

        int rosterBefore = state.Roster.Count;
        int perksBefore = state.Roster.Sum(p => p.Perks.Count);
        int goldBefore = state.Gold;

        var after = RunEngine.Apply(state, new DeclineReward(), Catalog, Systems);

        Assert.Equal(rosterBefore, after.Roster.Count);
        Assert.Equal(perksBefore, after.Roster.Sum(p => p.Perks.Count));
        Assert.Equal(goldBefore, after.Gold);
        Assert.True(RewardSystem.AlreadyClaimed(after, node, Economy));
        Assert.Throws<InvalidOperationException>(
            () => RewardSystem.Choose(after, new ChooseReward(0), Catalog, Economy, Systems.Items));
    }

    /// <summary>
    /// Rareza mejorada del nodo de élite: sobre el mismo conjunto de nodos, el élite ofrece bastantes más
    /// opciones por encima de común que la liga, que no mejora ninguna.
    /// </summary>
    [Fact]
    public void EliteOffersBetterRarityThanLeague()
    {
        Assert.Equal(0, Economy.LeagueReward.RarityFloorPercent);
        Assert.True(Economy.EliteReward.RarityFloorPercent > Economy.BossReward.RarityFloorPercent);

        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7272UL, Catalog, Systems);
        int leagueBetter = 0, eliteBetter = 0, total = 0;

        for (int nodeId = 101; nodeId < 141; nodeId++)
        {
            var league = new MapNode(nodeId, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
            var elite = new MapNode(nodeId, 1, 0, 0, NodeKind.EliteMatch, Array.Empty<int>(), string.Empty, 4);
            leagueBetter += AboveCommon(state, league);
            eliteBetter += AboveCommon(state, elite);
            total += 3;
        }

        Assert.True(
            eliteBetter > leagueBetter + (total / 10),
            $"el élite ofrece {eliteBetter} opciones por encima de común y la liga {leagueBetter} de {total}");
    }

    /// <summary>
    /// Superar el jefe cura la plantilla (ADR 0043): la lesión grave y las leves acumuladas desaparecen,
    /// el muerto no vuelve (RF-093). Es lo que cierra el ciclo de desgaste de cada acto.
    /// </summary>
    [Fact]
    public void WinningTheBossHealsTheRosterButNotTheDead()
    {
        Assert.True(Economy.BossReward.HealsRoster);
        Assert.False(Economy.LeagueReward.HealsRoster);
        Assert.False(Economy.EliteReward.HealsRoster);

        var state = RunEngine.Start(SystemsTestSupport.Setup(), 8383UL, Catalog, Systems);
        var roster = state.Roster;
        state = state
            .WithPlayer(roster[0] with { PhysicalState = PhysicalState.SevereInjury })
            .WithPlayer(roster[1] with { PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 2 })
            .WithPlayer(roster[2] with { PhysicalState = PhysicalState.Dead });

        var bossNode = FindNode(state, NodeKind.Boss);
        var afterBoss = Systems.AfterMatch(state, bossNode, WonSummary(bossNode), Catalog);

        Assert.Equal(PhysicalState.Healthy, afterBoss.GetPlayer(roster[0].Id).PhysicalState);
        Assert.Equal(PhysicalState.Healthy, afterBoss.GetPlayer(roster[1].Id).PhysicalState);
        Assert.Equal(0, afterBoss.GetPlayer(roster[1].Id).MinorInjuries);
        Assert.Equal(PhysicalState.Dead, afterBoss.GetPlayer(roster[2].Id).PhysicalState);

        // Un partido de liga ganado no cura nada: el alivio es del jefe.
        var leagueNode = FindNode(state, NodeKind.LeagueMatch);
        var afterLeague = Systems.AfterMatch(state, leagueNode, WonSummary(leagueNode), Catalog);
        Assert.Equal(PhysicalState.SevereInjury, afterLeague.GetPlayer(roster[0].Id).PhysicalState);
    }

    /// <summary>
    /// Desgaste creciente por acto (ADR 0043): el multiplicador de lesión sube de acto en acto y el nodo
    /// de élite añade el suyo. Está en datos (<c>tuning.injury</c>) y no toca la fórmula: un partido
    /// suelto sigue jugándose al 100%.
    /// </summary>
    [Fact]
    public void InjuryWearGrowsWithTheActAndTheEliteNode()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 9494UL, Catalog, Systems);
        var injury = Catalog.Tuning.Injury;
        Assert.Equal(100, SimConfig.Default.InjuryScalePercent);

        int[] byAct = new int[RunRules.Acts];
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var node = new MapNode(act * 100, act, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, act);
            byAct[act - 1] = Systems.MatchConfig(state, node, Catalog).InjuryScalePercent;
            Assert.Equal(injury.ScaleForAct(act), byAct[act - 1]);
        }

        Assert.True(byAct[0] < byAct[1] && byAct[1] < byAct[2], $"el desgaste no crece por acto: {string.Join('/', byAct)}");

        var eliteNode = new MapNode(202, 2, 0, 0, NodeKind.EliteMatch, Array.Empty<int>(), string.Empty, 3);
        int elite = Systems.MatchConfig(state, eliteNode, Catalog).InjuryScalePercent;
        Assert.True(elite > byAct[1], $"el élite del acto 2 desgasta {elite} y su liga {byAct[1]}");
    }

    /// <summary>
    /// El rival de un nodo de élite es el del acto <b>subido de nivel</b> (ADR 0043): más riesgo, la otra
    /// mitad de lo que le da su función al nodo. Sube con RF-027, no con una tabla aparte.
    /// </summary>
    [Fact]
    public void TheEliteRivalPlaysAboveTheLeagueRivalOfItsAct()
    {
        int bonus = Systems.Map.EliteRivalLevelBonus;
        Assert.True(bonus > 0, "data/map/map.json debe declarar el escalón de nivel del rival de élite (ADR 0043)");

        var state = RunEngine.Start(SystemsTestSupport.Setup(), 10101UL, Catalog, Systems);
        string opponentId = Systems.Rivals.OfAct(2)[0];
        var league = new MapNode(201, 2, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), opponentId, 3);
        var elite = new MapNode(202, 2, 0, 0, NodeKind.EliteMatch, Array.Empty<int>(), opponentId, 4);

        var leagueTeam = Systems.OpponentFor(state, league, Catalog);
        var eliteTeam = Systems.OpponentFor(state, elite, Catalog);

        for (int i = 0; i < leagueTeam.Players.Count; i++)
        {
            Assert.Equal(leagueTeam.Players[i].Level + bonus, eliteTeam.Players[i].Level);
            Assert.True(
                Sum(eliteTeam.Players[i].Attributes) > Sum(leagueTeam.Players[i].Attributes),
                "el rival de élite tiene que ser mejor, no solo llevar un número más alto");
        }
    }

    // ------------------------------------------------------------------ ayudantes

    private static int Sum(Attributes a) => a.Strength + a.Speed + a.Technique + a.Stamina + a.Leash;

    private static int GoldFor(RunState state, NodeKind kind)
    {
        var node = new MapNode(101, 1, 0, 0, kind, Array.Empty<int>(), string.Empty, 3);
        return GoldCalculator.GoldForWin(state, node, WonSummary(node), Economy);
    }

    private static int AboveCommon(RunState state, MapNode node)
    {
        int better = 0;
        foreach (var option in RewardSystem.Options(state, node, Catalog, Economy, Systems.Items))
        {
            bool above = option switch
            {
                PerkRewardOption perk => Catalog.Perks.Get(perk.PerkId).Rarity != Rarity.Common,
                ItemRewardOption item => Systems.Items.Find(item.ItemId)?.Rarity != Rarity.Common,
                PlayerRewardOption player => player.Player.Rarity != Rarity.Common,
                _ => false,
            };

            if (above)
            {
                better++;
            }
        }

        return better;
    }

    private static MapNode FindNode(RunState state, NodeKind kind)
    {
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var node = state.MapOf(act).Nodes.FirstOrDefault(n => n.Kind == kind);
            if (node is not null)
            {
                return node;
            }
        }

        throw new InvalidOperationException($"no hay ningún nodo {kind} en los mapas de esta run");
    }

    private static RunMatchSummary WonSummary(MapNode node)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = 2;
        builder.Goals[1] = 0;
        builder.Winner = 0;
        builder.Ticks = 500;

        return new RunMatchSummary(
            NodeId: node.Id,
            Kind: node.Kind,
            Won: true,
            GoalsFor: 2,
            GoalsAgainst: 0,
            Ticks: 500,
            WentToGoldenGoal: false,
            PlayedPlayerIds: new[] { 0, 1, 2, 3, 4, 5, 6 },
            BenchedPlayerIds: Array.Empty<int>(),
            OwnInjuries: 0,
            OwnDeaths: 0,
            Report: builder.Build());
    }

    private static string Describe(RewardOption option) => option switch
    {
        PerkRewardOption perk => "perk:" + perk.PerkId,
        ItemRewardOption item => "item:" + item.ItemId,
        PlayerRewardOption player => "player:" + player.Player.Rarity + ":" + player.Player.Position + ":" + player.Player.Attributes,
        _ => option.GetType().Name,
    };
}
