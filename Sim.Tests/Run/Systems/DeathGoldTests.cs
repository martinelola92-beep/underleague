using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// La muerte de un jugador con un perk de <c>data/economy/death-gold.json</c> paga oro a la run (paquete
/// BB, primitiva "run-level: oro y atributos al salir de la plantilla", consumidor Seguro de vida). Mismo
/// canal que <see cref="GoldCalculator.CounterGold"/> (ADR 0113): se cobra se gane o se pierda, con su
/// desglose visible (RF-119).
/// </summary>
public sealed class DeathGoldTests
{
    private static readonly ulong Seed = 909090UL;

    /// <summary>La muerte del asegurado cambia el oro de la run.</summary>
    [Fact]
    public void ADeathWithAnInsuredPerkPaysGoldToTheRun()
    {
        var state = RunTestState();
        int ownPlayerId = state.Roster[0].Id;

        var withoutDeath = Summary(ownPlayerId);
        var withInsuredDeath = withoutDeath with
        {
            OwnDeaths = 1,
            DeathDetails = new[] { new PlayerDeathDetail(ownPlayerId, new[] { "life_insurance" }, -1) },
        };

        var economy = EconomyWithDeathRates(("life_insurance", 8));

        var withoutGold = GoldCalculator.DeathGold(state, withoutDeath, economy);
        var withGold = GoldCalculator.DeathGold(state, withInsuredDeath, economy);

        Assert.Equal(0, withoutGold.Total);
        Assert.Empty(withoutGold.Rows);
        Assert.Equal(8, withGold.Total);

        var row = Assert.Single(withGold.Rows);
        Assert.Equal(ownPlayerId, row.PlayerId);
        Assert.Equal("life_insurance", row.PerkId);
        Assert.Equal(8, row.Gold);
    }

    /// <summary>Sin tarifa para ese perk, la muerte no paga nada.</summary>
    [Fact]
    public void ADeathWithAPerkNotInTheTariffPaysNothing()
    {
        var state = RunTestState();
        int ownPlayerId = state.Roster[0].Id;
        var summary = Summary(ownPlayerId) with
        {
            OwnDeaths = 1,
            DeathDetails = new[] { new PlayerDeathDetail(ownPlayerId, new[] { "not_in_the_tariff" }, -1) },
        };

        var economy = EconomyWithDeathRates(("life_insurance", 8));

        var deathGold = GoldCalculator.DeathGold(state, summary, economy);

        Assert.Equal(0, deathGold.Total);
        Assert.Empty(deathGold.Rows);
    }

    /// <summary>
    /// El mismo canal que el oro de contador (ADR 0113): se cobra TAMBIÉN al perder un partido ordinario,
    /// porque el jugador ya pagó por adelantado el slot del perk.
    /// </summary>
    [Fact]
    public void ADeathPaysAlsoWhenTheMatchIsLost()
    {
        var baseSystems = SystemsTestSupport.Systems;
        var economy = EconomyWithDeathRates(("life_insurance", 8));
        var systems = new StandardRunSystems(
            economy, baseSystems.Items, baseSystems.Consumables, baseSystems.Rivals,
            baseSystems.Map, baseSystems.Clubs, baseSystems.Events);

        var state = RunEngine.Start(SystemsTestSupport.Setup(), Seed, SystemsTestSupport.Catalog, systems);
        int ownPlayerId = state.Roster[0].Id;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

        var lost = Summary(ownPlayerId) with { Won = false, GoalsFor = 0, GoalsAgainst = 1 };
        var lostWithDeath = lost with
        {
            OwnDeaths = 1,
            DeathDetails = new[] { new PlayerDeathDetail(ownPlayerId, new[] { "life_insurance" }, -1) },
        };

        int withoutPerk = systems.AfterMatch(state, node, lost, SystemsTestSupport.Catalog).Gold;
        int withPerk = systems.AfterMatch(state, node, lostWithDeath, SystemsTestSupport.Catalog).Gold;

        Assert.Equal(state.Gold, withoutPerk);
        Assert.Equal(withoutPerk + 8, withPerk);
    }

    private static EconomyConfig EconomyWithDeathRates(params (string PerkId, int Rate)[] rates)
    {
        var pairs = string.Join(", ", rates.Select(r => $"\"{r.PerkId}\": {r.Rate}"));
        var table = DeathGoldTable.FromJson(new Dictionary<string, string>
        {
            [DeathGoldTable.Path] = $$"""{ "rates": { {{pairs}} } }""",
        });

        return SystemsTestSupport.Systems.Economy with { DeathGold = table };
    }

    private static RunMatchSummary Summary(int ownPlayerId)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = 1;
        builder.Goals[1] = 0;
        builder.Winner = 0;
        builder.Ticks = 500;
        var report = builder.Build();

        return new RunMatchSummary(
            NodeId: 101,
            Kind: NodeKind.LeagueMatch,
            Won: true,
            GoalsFor: 1,
            GoalsAgainst: 0,
            Ticks: 500,
            WentToGoldenGoal: false,
            PlayedPlayerIds: new[] { ownPlayerId },
            BenchedPlayerIds: Array.Empty<int>(),
            OwnInjuries: 0,
            OwnDeaths: 0,
            Report: report);
    }

    private static RunState RunTestState() =>
        RunEngine.Start(SystemsTestSupport.Setup(), Seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
}
