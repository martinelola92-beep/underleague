using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// Un contador de partido declarado en <c>data/economy/counter-gold.json</c>
/// (<see cref="CounterGoldTable"/>) paga oro al resolver el partido, con su desglose visible (RF-119).
/// Es un canal <b>aparte</b> del premio (ADR 0113): el premio lo cobra quien gana (RF-114g) y esto se
/// cobra siempre, porque el slot ya se pagó por adelantado.
/// </summary>
public sealed class CounterGoldTests
{
    private static readonly ulong Seed = 424242UL;

    [Fact]
    public void ACounterWithARateAddsGoldToTheBreakdownAndIsVisibleAsARow()
    {
        var state = RunTestState();
        int ownPlayerId = state.Roster[0].Id;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var baseline = Summary(node.Id, ownPlayerId);
        var withGoals = baseline with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(ownPlayerId, "local_idol_goals", 3) },
        };

        var economy = EconomyWithRates(("local_idol_goals", 5));

        var withoutCounter = GoldCalculator.CounterGold(state, baseline, economy);
        var withCounter = GoldCalculator.CounterGold(state, withGoals, economy);

        // Paga y se ve: exactamente lo que declara la tarifa (3 unidades x 5 de oro), y la fila dice de
        // qué jugador y de qué contador salió (RF-119).
        Assert.Equal(0, withoutCounter.Total);
        Assert.Empty(withoutCounter.Rows);
        Assert.Equal(15, withCounter.Total);

        // Y NO se mezcla con el premio de partido: el desglose del premio vale lo mismo con y sin él.
        Assert.Equal(
            GoldCalculator.Breakdown(state, node, baseline, economy).Total,
            GoldCalculator.Breakdown(state, node, withGoals, economy).Total);

        var row = Assert.Single(withCounter.Rows);
        Assert.Equal(ownPlayerId, row.PlayerId);
        Assert.Equal("local_idol_goals", row.Counter);
        Assert.Equal(3, row.Delta);
        Assert.Equal(5, row.RatePerUnit);
        Assert.Equal(15, row.Gold);
    }

    [Fact]
    public void ACounterWithoutARateInTheTableDoesNotPay()
    {
        var state = RunTestState();
        int ownPlayerId = state.Roster[0].Id;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var summary = Summary(node.Id, ownPlayerId) with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(ownPlayerId, "not_in_the_tariff", 10) },
        };

        var economy = EconomyWithRates(("local_idol_goals", 5));

        var counterGold = GoldCalculator.CounterGold(state, summary, economy);

        Assert.Equal(0, counterGold.Total);
        Assert.Empty(counterGold.Rows);
    }

    /// <summary>Un contador del RIVAL (no está en la plantilla propia) nunca paga a este club.</summary>
    [Fact]
    public void ARivalsCounterNeverPaysThisClub()
    {
        var state = RunTestState();
        int rivalPlayerId = -1;
        while (state.FindPlayer(rivalPlayerId) is not null)
        {
            rivalPlayerId--;
        }

        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var summary = Summary(node.Id, state.Roster[0].Id) with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(rivalPlayerId, "local_idol_goals", 3) },
        };

        var economy = EconomyWithRates(("local_idol_goals", 5));

        var counterGold = GoldCalculator.CounterGold(state, summary, economy);

        Assert.Equal(0, counterGold.Total);
        Assert.Empty(counterGold.Rows);
    }

    /// <summary>
    /// Sin tarifa, ningún contador paga. El test se apoyaba en que <c>data/economy/counter-gold.json</c>
    /// no existía; ahora existe con las tarifas de la tanda 1, así que la ausencia se construye de forma
    /// <b>explícita</b> con <see cref="CounterGoldTable.Empty"/>: lo que se quiere afirmar es que el canal
    /// es inerte sin tarifa, no que el fichero falte.
    /// </summary>
    [Fact]
    public void WithoutARateNoCounterPaysAnything()
    {
        var state = RunTestState();
        int ownPlayerId = state.Roster[0].Id;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var summary = Summary(node.Id, ownPlayerId) with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(ownPlayerId, "local_idol_goals", 3) },
        };

        Assert.Equal(0, CounterGoldTable.Empty.Count);
        var economy = SystemsTestSupport.Systems.Economy with { CounterGold = CounterGoldTable.Empty };
        Assert.Equal(0, economy.CounterGold.Count);

        Assert.Equal(0, GoldCalculator.CounterGold(state, summary, economy).Total);
    }

    /// <summary>
    /// ADR 0113, y es el punto entero de la decisión: el oro de contador se cobra <b>también al perder</b>.
    /// Taquillero paga por terminar de pie y Máquina de publicidad por lo que le hace al rival; los dos se
    /// cobran justo en los partidos que se pierden. Un perk que solo rindiera ganando no sería una
    /// inversión que ocupa un slot, sería una propina.
    /// </summary>
    [Fact]
    public void ACounterPaysAlsoWhenTheMatchIsLost()
    {
        var baseSystems = SystemsTestSupport.Systems;
        var economy = EconomyWithRates(("local_idol_goals", 5));
        var systems = new StandardRunSystems(
            economy, baseSystems.Items, baseSystems.Consumables, baseSystems.Rivals,
            baseSystems.Map, baseSystems.Clubs, baseSystems.Events);

        var state = RunEngine.Start(SystemsTestSupport.Setup(), Seed, SystemsTestSupport.Catalog, systems);
        int ownPlayerId = state.Roster[0].Id;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

        var lost = Summary(node.Id, ownPlayerId) with { Won = false, GoalsFor = 0, GoalsAgainst = 1 };
        var lostWithCounter = lost with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(ownPlayerId, "local_idol_goals", 3) },
        };

        int withoutPerk = systems.AfterMatch(state, node, lost, SystemsTestSupport.Catalog).Gold;
        int withPerk = systems.AfterMatch(state, node, lostWithCounter, SystemsTestSupport.Catalog).Gold;

        // Perder sigue sin pagar premio (RF-114g): sin el perk, la derrota deja el oro donde estaba —hoy
        // la penalización vale 0 (`defeatGoldPenalty` 0 / 0 %), así que lo que se afirma es "no cobra",
        // no "no pierde". Con el perk entran sus 15 encima, que es la decisión de la ADR 0113.
        Assert.Equal(state.Gold, withoutPerk);
        Assert.Equal(withoutPerk + 15, withPerk);

        // Y la victoria sigue cobrando premio Y contador, sin que uno se coma al otro.
        var won = Summary(node.Id, ownPlayerId) with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(ownPlayerId, "local_idol_goals", 3) },
        };
        int wonWithPerk = systems.AfterMatch(state, node, won, SystemsTestSupport.Catalog).Gold;
        int wonWithout = systems.AfterMatch(state, node, Summary(node.Id, ownPlayerId), SystemsTestSupport.Catalog).Gold;
        Assert.Equal(wonWithout + 15, wonWithPerk);
        Assert.True(wonWithout > state.Gold, "ganar tiene que seguir pagando premio");
    }

    private static EconomyConfig EconomyWithRates(params (string Counter, int Rate)[] rates)
    {
        var pairs = string.Join(
            ", ", rates.Select(r => $"\"{r.Counter}\": {r.Rate}"));
        var table = CounterGoldTable.FromJson(new Dictionary<string, string>
        {
            [CounterGoldTable.Path] = $$"""{ "rates": { {{pairs}} } }""",
        });

        return SystemsTestSupport.Systems.Economy with { CounterGold = table };
    }

    private static RunMatchSummary Summary(int nodeId, int ownPlayerId)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = 1;
        builder.Goals[1] = 0;
        builder.Winner = 0;
        builder.Ticks = 500;
        var report = builder.Build();

        return new RunMatchSummary(
            NodeId: nodeId,
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
