using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Medical;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>ADR 0164, RF-095, RF-095b, RF-095c: el herrero de la clínica y sus prótesis.</summary>
public sealed class BlacksmithTests
{
    private static EconomyConfig Economy => SystemsTestSupport.Systems.Economy;

    private static ProsthesisCatalog Prostheses => SystemsTestSupport.Systems.Prostheses;

    /// <summary>Run en una clínica abierta con oro de sobra y un jugador con lesión grave.</summary>
    private static (RunState State, RunPlayer Patient) ClinicWithSevere(ulong seed, int skip = 0, int gold = 200)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(gold);
        var patient = state.Roster[1] with { PhysicalState = PhysicalState.SevereInjury };
        state = state.WithPlayer(patient);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic, skip);
        return (state, patient);
    }

    private static RunState Forge(RunState state, int playerId, int extra = 0) =>
        MedicalSystem.Forge(state, new ForgePlayer(playerId, extra), Economy, Prostheses);

    // ---------------------------------------------------------------- datos

    [Fact]
    public void CatalogHasTwelveToSixteenProsthesesHalfImprovingHalfWorsening()
    {
        var all = Prostheses.All;
        Assert.InRange(all.Count, 12, 16);
        Assert.Equal(all.Count / 2, all.Count(p => p.Kind == ProsthesisKind.Improve));
        Assert.Equal(all.Count / 2, all.Count(p => p.Kind == ProsthesisKind.Worsen));
        Assert.True(Prostheses.Slots.Count >= 6, "una ranura libre no basta si solo hay dos ranuras: ranuras distintas");
        foreach (string slot in Prostheses.Slots)
        {
            Assert.Contains(all, p => p.Slot == slot && p.Kind == ProsthesisKind.Improve);
            Assert.Contains(all, p => p.Slot == slot && p.Kind == ProsthesisKind.Worsen);
        }

        Assert.All(all, p => Assert.True(p.Kind == ProsthesisKind.Improve ? p.Delta > 0 : p.Delta < 0));
        Assert.All(all, p => Assert.False(string.IsNullOrWhiteSpace(p.Name.Es) || string.IsNullOrWhiteSpace(p.Name.En)));
    }

    [Fact]
    public void LoaderRejectsAnImprovementThatSubtracts()
    {
        string content = TestData.LoadAllFiles()["prostheses/prostheses.json"]
            .Replace("\"delta\": 8", "\"delta\": -8");
        var files = new Dictionary<string, string> { ["prostheses/prostheses.json"] = content };
        var ex = Assert.Throws<DataException>(() => ProsthesisLoader.FromJson(files));
        Assert.Equal("prostheses/prostheses.json", ex.File);
    }

    [Fact]
    public void LoaderRejectsASlotWithoutBothKinds()
    {
        // El fichero es multilínea: se reescribe la ranura de bucket_skull para dejar 'skull' sin empeoramiento.
        string content = TestData.LoadAllFiles()["prostheses/prostheses.json"];
        int at = content.IndexOf("\"id\": \"bucket_skull\"", StringComparison.Ordinal);
        Assert.True(at > 0);
        int slotAt = content.IndexOf("\"slot\": \"skull\"", at, StringComparison.Ordinal);
        content = content.Remove(slotAt, "\"slot\": \"skull\"".Length).Insert(slotAt, "\"slot\": \"tail\"");
        var files = new Dictionary<string, string> { ["prostheses/prostheses.json"] = content };
        Assert.Throws<DataException>(() => ProsthesisLoader.FromJson(files));
    }

    [Fact]
    public void BlacksmithEconomyIsWellFormed()
    {
        var config = Economy.Blacksmith;
        Assert.Equal(100, config.BaseCurePercent + config.BaseImprovePercent + config.BaseWorsenPercent);
        Assert.True(MedicalSystem.BlacksmithBasePrice(Economy) < Economy.ClinicCost, "el herrero es más barato que el médico");
        Assert.True(config.BaseWorsenPercent - config.TotalShift >= config.MinWorsenPercent);
    }

    // ---------------------------------------------------------------- la tabla

    [Fact]
    public void OddsAlwaysSumToOneHundredAndInvestingNeverHurts()
    {
        var previous = MedicalSystem.BlacksmithOddsFor(Economy, 0);
        Assert.Equal(Economy.Blacksmith.BaseCurePercent, previous.CurePercent);
        for (int extra = 1; extra <= Economy.Blacksmith.MaxExtraGold; extra++)
        {
            var odds = MedicalSystem.BlacksmithOddsFor(Economy, extra);
            Assert.Equal(100, odds.CurePercent + odds.ImprovePercent + odds.WorsenPercent);
            Assert.True(odds.CurePercent >= previous.CurePercent);
            Assert.True(odds.ImprovePercent >= previous.ImprovePercent);
            Assert.True(odds.WorsenPercent < previous.WorsenPercent);
            Assert.True(odds.WorsenPercent >= Economy.Blacksmith.MinWorsenPercent);
            previous = odds;
        }
    }

    [Fact]
    public void ExtraGoldHasDiminishingReturnsAndACap()
    {
        int max = Economy.Blacksmith.MaxExtraGold;
        int previousGain = int.MaxValue;
        for (int extra = 1; extra <= max; extra++)
        {
            int gain = MedicalSystem.BlacksmithOddsFor(Economy, extra - 1).WorsenPercent
                - MedicalSystem.BlacksmithOddsFor(Economy, extra).WorsenPercent;
            Assert.True(gain > 0);
            Assert.True(gain <= previousGain, $"el oro extra {extra} rinde {gain}, más que el anterior ({previousGain})");
            previousGain = gain;
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => MedicalSystem.BlacksmithOddsFor(Economy, max + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MedicalSystem.BlacksmithOddsFor(Economy, -1));
    }

    /// <summary>
    /// La tabla que se enseña es la que se tira: el desenlace de cada tirada coincide, tirada a tirada, con el
    /// umbral que marcan los porcentajes de la vista sobre el mismo flujo de RNG. Con muchas semillas y con
    /// el oro extra mínimo y el máximo.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void TheTableShownIsTheTableRolled(int extra)
    {
        var counts = new int[3];
        int total = 0;
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var (state, patient) = ClinicWithSevere(seed);
            var quote = BlacksmithView.Quote(state, Economy, Prostheses, patient.Id, extra);
            var node = state.PendingNodeId;
            var rng = OfferStream.For(state.Seed, node, MedicalSystem.BlacksmithStreamBase + patient.Id);
            int roll = rng.Range(0, 100);
            var expected = roll < quote.Odds.CurePercent ? BlacksmithOutcomeKind.Cured
                : roll < quote.Odds.CurePercent + quote.Odds.ImprovePercent ? BlacksmithOutcomeKind.Improved
                : BlacksmithOutcomeKind.Worsened;

            var after = Forge(state, patient.Id, extra);
            var result = BlacksmithView.Outcome(state.GetPlayer(patient.Id), after.GetPlayer(patient.Id), Prostheses);
            Assert.Equal(expected, result.Kind);
            counts[(int)result.Kind]++;
            total++;
        }

        // Y las frecuencias sobre 300 semillas están cerca de lo que dice la tabla (±10 puntos, ~4 sigma).
        var odds = MedicalSystem.BlacksmithOddsFor(Economy, extra);
        Assert.InRange(counts[0] * 100.0 / total, odds.CurePercent - 10, odds.CurePercent + 10);
        Assert.InRange(counts[1] * 100.0 / total, odds.ImprovePercent - 10, odds.ImprovePercent + 10);
        Assert.InRange(counts[2] * 100.0 / total, odds.WorsenPercent - 10, odds.WorsenPercent + 10);
    }

    // ---------------------------------------------------------------- lo que hace

    [Fact]
    public void TheBlacksmithNeverKillsAndAlwaysLeavesThePlayerHealthy()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var (state, patient) = ClinicWithSevere(seed);
            var after = Forge(state, patient.Id, seed % 2 == 0 ? 0 : 5);
            var player = after.GetPlayer(patient.Id);
            Assert.Equal(PhysicalState.Healthy, player.PhysicalState);
            Assert.Equal(0, player.MinorInjuries);
        }
    }

    [Fact]
    public void ForgeChargesTheBasePriceAndTheExtraGold()
    {
        var (state, patient) = ClinicWithSevere(7);
        var after = Forge(state, patient.Id, 3);
        Assert.Equal(state.Gold - MedicalSystem.BlacksmithBasePrice(Economy) - 3, after.Gold);
    }

    [Fact]
    public void ForgeIsRejectedWithoutGoldForAHealthyPlayerOutsideTheClinicOrWithTooMuchExtra()
    {
        var (state, patient) = ClinicWithSevere(8, gold: 1);
        Assert.Throws<ArgumentException>(() => Forge(state, patient.Id));

        var (rich, severe) = ClinicWithSevere(8);
        var healthy = rich.Roster.First(p => p.PhysicalState == PhysicalState.Healthy);
        Assert.Throws<ArgumentException>(() => Forge(rich, healthy.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => Forge(rich, severe.Id, Economy.Blacksmith.MaxExtraGold + 1));

        var market = SystemsTestSupport.WithFakePendingNode(
            RunEngine.Start(SystemsTestSupport.Setup(), 9UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems).WithGold(100),
            NodeKind.Market);
        var injured = market.Roster[0] with { PhysicalState = PhysicalState.SevereInjury };
        market = market.WithPlayer(injured);
        Assert.Throws<InvalidOperationException>(() => Forge(market, injured.Id));
    }

    [Fact]
    public void AProsthesisChangesTheAttributesTheMatchReads()
    {
        // Regla I: el efecto va a RunPlayer.Attributes, que es lo que ToDefinition entrega al partido.
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var (state, patient) = ClinicWithSevere(seed);
            var after = Forge(state, patient.Id).GetPlayer(patient.Id);
            if (after.Prostheses.Count == 0)
            {
                Assert.Equal(patient.Attributes, after.Attributes);
                continue;
            }

            var prosthesis = Prostheses.Find(after.Prostheses[0].Effect)!;
            Assert.Equal(prosthesis.Slot, after.Prostheses[0].Slot);
            var definition = after.ToDefinition(SystemsTestSupport.Catalog);
            Assert.Equal(prosthesis.ApplyTo(patient.Attributes), definition.Attributes);
            Assert.Contains(MedicalSystem.ScrapTag, definition.Tags);
            Assert.NotEqual(patient.Attributes, definition.Attributes);
            return;
        }

        Assert.Fail("ninguna de 100 semillas instaló una prótesis");
    }

    [Fact]
    public void AnOccupiedSlotIsNeverRepeated()
    {
        var slots = Prostheses.Slots;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var (state, patient) = ClinicWithSevere(seed);
            // Ocupa todas las ranuras menos una: la única prótesis posible es la de esa ranura.
            string free = slots[(int)(seed % (ulong)slots.Count)];
            var occupied = slots.Where(s => s != free).Select(s => new RunProsthesis(s, "manual")).ToList();
            var prepared = patient with { Prostheses = occupied };
            state = state.WithPlayer(prepared);
            var after = Forge(state, patient.Id, 5).GetPlayer(patient.Id);
            if (after.Prostheses.Count == occupied.Count)
            {
                continue; // curación
            }

            Assert.Equal(occupied.Count + 1, after.Prostheses.Count);
            Assert.Equal(free, after.Prostheses[^1].Slot);
            Assert.Equal(after.Prostheses.Count, after.Prostheses.Select(p => p.Slot).Distinct().Count());
        }
    }

    [Fact]
    public void WithEverySlotTakenTheBlacksmithCannotTreatAndTheViewSaysSo()
    {
        var (state, patient) = ClinicWithSevere(3);
        var full = patient with { Prostheses = Prostheses.Slots.Select(s => new RunProsthesis(s, "manual")).ToList() };
        state = state.WithPlayer(full);
        var quote = BlacksmithView.Quote(state, Economy, Prostheses, patient.Id, 0);
        Assert.False(quote.HasFreeSlot);
        Assert.False(quote.CanConfirm);
        Assert.Throws<ArgumentException>(() => Forge(state, patient.Id));
    }

    [Fact]
    public void ThreeProsthesesAddAutomatonAndKeepTheSpeciesTag()
    {
        var (state, patient) = ClinicWithSevere(11);
        string species = patient.SpeciesTag;
        Assert.Contains(species, patient.Tags);
        var player = patient;
        var picks = new[] { "iron_arm", "peg_leg", "glass_eye" };
        for (int i = 0; i < picks.Length; i++)
        {
            player = MedicalSystem.Install(player, Prostheses.Find(picks[i])!);
            if (i < 2)
            {
                Assert.Equal(species, player.SpeciesTag);
                Assert.Contains(species, player.Tags);
                Assert.Contains(MedicalSystem.ScrapTag, player.Tags);
            }
        }

        Assert.Equal(species, player.SpeciesTag);
        Assert.Contains(species, player.Tags);
        Assert.Contains(MedicalSystem.AutomatonTag, player.Tags);
        Assert.Contains(MedicalSystem.ScrapTag, player.Tags);
        Assert.Equal(3, player.Prostheses.Count);
        Assert.True(player.ToDefinition(SystemsTestSupport.Catalog).HasTag(MedicalSystem.AutomatonTag));

        // Y la vista lo avisa antes: con dos prótesis, la siguiente es la que lo convierte.
        var two = MedicalSystem.Install(MedicalSystem.Install(patient, Prostheses.Find("iron_arm")!), Prostheses.Find("peg_leg")!);
        Assert.True(BlacksmithView.Quote(state.WithPlayer(two), Economy, Prostheses, patient.Id, 0).NextMakesAutomaton);
        Assert.False(BlacksmithView.Quote(state, Economy, Prostheses, patient.Id, 0).NextMakesAutomaton);
    }

    [Fact]
    public void TheBlacksmithIsAGambleNotAFreeUpgrade()
    {
        // Decisión del coordinador tras la revisión independiente: sin oro extra la esperanza de atributos de la
        // tirada es ligeramente negativa y solo con oro extra se vuelve positiva; si no, el herrero domina al médico.
        double improve = Prostheses.All.Where(p => p.Kind == ProsthesisKind.Improve).Average(p => p.Delta);
        double worsen = Prostheses.All.Where(p => p.Kind == ProsthesisKind.Worsen).Average(p => p.Delta);
        Assert.True(improve > 0 && worsen < 0 && improve < -worsen, "las mejoras valen menos que los empeoramientos");
        double Expected(int extra)
        {
            var odds = MedicalSystem.BlacksmithOddsFor(Economy, extra);
            return ((odds.ImprovePercent * improve) + (odds.WorsenPercent * worsen)) / 100.0;
        }

        Assert.InRange(Expected(0), -3.0, -0.1);
        Assert.True(Expected(Economy.Blacksmith.MaxExtraGold) > 0);
        Assert.True(Expected(Economy.Blacksmith.MaxExtraGold) > Expected(1));
    }

    [Fact]
    public void TheQuoteShowsTheRangesAndAttributesForTheFreeSlots()
    {
        var (state, patient) = ClinicWithSevere(3);
        var all = BlacksmithView.Quote(state, Economy, Prostheses, patient.Id, 0);
        Assert.NotNull(all.ImproveRange);
        Assert.NotNull(all.WorsenRange);
        Assert.Equal(Prostheses.All.Where(p => p.Kind == ProsthesisKind.Improve).Min(p => p.Delta), all.ImproveRange!.MinDelta);
        Assert.Equal(Prostheses.All.Where(p => p.Kind == ProsthesisKind.Improve).Max(p => p.Delta), all.ImproveRange.MaxDelta);
        Assert.Equal(Prostheses.All.Where(p => p.Kind == ProsthesisKind.Worsen).Min(p => p.Delta), all.WorsenRange!.MaxDelta);

        // Con todas las ranuras ocupadas salvo 'arm', solo puede salir Fuerza.
        var occupied = Prostheses.Slots.Where(s => s != "arm").Select(s => new RunProsthesis(s, "manual")).ToList();
        var armOnly = BlacksmithView.Quote(state.WithPlayer(patient with { Prostheses = occupied }), Economy, Prostheses, patient.Id, 0);
        Assert.Equal(new[] { AttributeKind.Strength }, armOnly.ImproveRange!.Attributes);
        Assert.Equal(new[] { AttributeKind.Strength }, armOnly.WorsenRange!.Attributes);
    }

    [Fact]
    public void AttributesStayInRangeWhateverTheProsthesis()
    {
        var (_, patient) = ClinicWithSevere(5);
        var low = patient with { Attributes = new Attributes(2, 2, 2, 2, 2) };
        var high = patient with { Attributes = new Attributes(98, 98, 98, 98, 98) };
        foreach (var prosthesis in Prostheses.All)
        {
            var a = MedicalSystem.Install(low, prosthesis).Attributes;
            var b = MedicalSystem.Install(high, prosthesis).Attributes;
            Assert.All(new[] { a.Strength, a.Speed, a.Technique, a.Stamina, a.Leash, b.Strength, b.Speed, b.Technique, b.Stamina, b.Leash },
                v => Assert.InRange(v, 1, 99));
        }
    }

    // ---------------------------------------------------------------- vista y determinismo

    /// <summary>La vista dice "se puede confirmar" exactamente cuando la decisión se acepta.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(13)]
    [InlineData(40)]
    public void TheViewAgreesWithWhatTheDecisionAccepts(int gold)
    {
        var (state, patient) = ClinicWithSevere(21, gold: gold);
        for (int extra = 0; extra <= Economy.Blacksmith.MaxExtraGold; extra++)
        {
            var quote = BlacksmithView.Quote(state, Economy, Prostheses, patient.Id, extra);
            Assert.Equal(MedicalSystem.BlacksmithBasePrice(Economy) + extra, quote.Price);
            Assert.Equal(MedicalSystem.BlacksmithOddsFor(Economy, extra), quote.Odds);
            bool accepted;
            try
            {
                Forge(state, patient.Id, extra);
                accepted = true;
            }
            catch (ArgumentException)
            {
                accepted = false;
            }

            Assert.Equal(quote.CanConfirm, accepted);
        }

        Assert.Equal(Economy.Blacksmith.MaxExtraGold + 1, BlacksmithView.Quotes(state, Economy, Prostheses, patient.Id).Count);
    }

    [Fact]
    public void ForgeIsDeterministic()
    {
        var (state, patient) = ClinicWithSevere(99);
        var a = Forge(state, patient.Id, 2).GetPlayer(patient.Id);
        var b = Forge(state, patient.Id, 2).GetPlayer(patient.Id);
        Assert.Equal(a.Prostheses, b.Prostheses);
        Assert.Equal(a.Attributes, b.Attributes);
        Assert.Equal(a.Tags, b.Tags);
    }

    [Fact]
    public void TheDecisionGoesThroughTheRunEngine()
    {
        var (state, patient) = ClinicWithSevere(31);
        var after = RunEngine.Apply(state, new ForgePlayer(patient.Id, 1), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        Assert.Equal(PhysicalState.Healthy, after.GetPlayer(patient.Id).PhysicalState);
        Assert.Equal(state.Gold - MedicalSystem.BlacksmithBasePrice(Economy) - 1, after.Gold);
    }
}
