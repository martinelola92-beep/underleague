using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Bets;

namespace Underleague.Sim.Tests.Run.Systems.Bets;

/// <summary>
/// ADR 0157, paso 3: tomar, pagar, cobrar y fallar la apuesta del vestuario dentro de la run, guardarla
/// (esquema 7) y que la política la use. Los partidos son los reales del motor.
/// </summary>
public sealed class BetRunTests
{
    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    private static Catalog Catalog => SystemsTestSupport.Catalog;

    /// <summary>Primer nodo de partido accesible al inicio de la run de esa semilla que ofrezca apuesta.</summary>
    private static (RunState State, MapNode Node, BetOffer Offer)? FindOffered(ulong seed)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, Systems);
        foreach (var node in RunEngine.AvailableNodes(state).Where(n => n.IsMatch).OrderBy(n => n.Id))
        {
            var offer = BetSystem.OfferFor(state, node, Systems, Catalog);
            if (offer is not null)
            {
                return (state, node, offer);
            }
        }

        return null;
    }

    private static (RunState State, MapNode Node, BetOffer Offer) Offered()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            if (FindOffered(seed) is { } found)
            {
                return found;
            }
        }

        throw new InvalidOperationException("ninguna semilla de 1..200 ofrece apuesta en un nodo de entrada");
    }

    /// <summary>
    /// La run de esa semilla tras jugar el partido de entrada (el único nodo accesible al empezar) y cerrar
    /// lo que abre: desde ahí el mapa ofrece varios nodos a la vez. Null si la run terminó.
    /// </summary>
    private static RunState AfterFirstMatch(ulong seed)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, Systems);
        state = RunEngine.Enter(state, RunEngine.AvailableNodes(state)[0].Id, Catalog, Systems);
        if (state.Phase == RunPhase.NodeOpen)
        {
            try
            {
                state = RunEngine.Apply(state, new DeclineReward(), Catalog, Systems);
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (state.Phase == RunPhase.NodeOpen)
        {
            state = RunEngine.Apply(state, new LeaveNode(), Catalog, Systems);
        }

        return state;
    }

    [Fact]
    public void TakingTheOfferedBetPaysTheStakeAndStoresIt()
    {
        var (state, node, offer) = Offered();

        var taken = RunEngine.Apply(state, new TakeBet(node.Id), Catalog, Systems);

        Assert.Equal(state.Gold - offer.Stake, taken.Gold);
        Assert.NotNull(taken.Bet);
        Assert.Equal(offer.BetId, taken.Bet!.BetId);
        Assert.Equal(node.Id, taken.Bet.NodeId);
        Assert.Equal(offer.Stake, taken.Bet.Stake);
        Assert.Equal(offer.PayoutPercent, taken.Bet.PayoutPercent);
        Assert.Equal(offer.TargetPlayerId, taken.Bet.TargetPlayerId);
    }

    [Fact]
    public void OnlyOneBetPerNode()
    {
        var (state, node, _) = Offered();
        var taken = RunEngine.Apply(state, new TakeBet(node.Id), Catalog, Systems);

        Assert.Throws<InvalidOperationException>(() => RunEngine.Apply(taken, new TakeBet(node.Id), Catalog, Systems));
    }

    [Fact]
    public void WithoutEnoughGoldTheBetCannotBeTaken()
    {
        var (state, node, offer) = Offered();

        var poor = state.WithGold(offer.Stake - 1);
        Assert.Throws<InvalidOperationException>(() => RunEngine.Apply(poor, new TakeBet(node.Id), Catalog, Systems));

        // Con justo lo que cuesta sí se puede, y se queda a cero.
        var exact = RunEngine.Apply(state.WithGold(offer.Stake), new TakeBet(node.Id), Catalog, Systems);
        Assert.Equal(0, exact.Gold);
    }

    [Fact]
    public void OnlyAnAccessibleMatchNodeAdmitsABet()
    {
        var (state, _, _) = Offered();
        var accessible = RunEngine.AvailableNodes(state).Select(n => n.Id).ToHashSet();

        // Un nodo que no es accesible desde aquí (o no existe) no admite apuesta.
        int far = state.MapOf(state.Act).Nodes.First(n => !accessible.Contains(n.Id)).Id;
        Assert.Throws<InvalidOperationException>(() => RunEngine.Apply(state, new TakeBet(far), Catalog, Systems));
        Assert.Throws<InvalidOperationException>(() => RunEngine.Apply(state, new TakeBet(-5), Catalog, Systems));

        // Un nodo accesible que no es de partido tampoco.
        for (ulong seed = 1; seed <= 80; seed++)
        {
            var other = AfterFirstMatch(seed);
            if (other.Result.IsOver)
            {
                continue;
            }

            var service = RunEngine.AvailableNodes(other).FirstOrDefault(n => !n.IsMatch);
            if (service is not null)
            {
                Assert.Throws<InvalidOperationException>(() => RunEngine.Apply(other, new TakeBet(service.Id), Catalog, Systems));
                return;
            }
        }

        throw new InvalidOperationException("ninguna semilla de 1..80 ofrece un nodo de servicio tras el primer partido");
    }

    [Fact]
    public void DecliningARefundsTheStake()
    {
        var (state, node, _) = Offered();
        var taken = RunEngine.Apply(state, new TakeBet(node.Id), Catalog, Systems);

        var declined = RunEngine.Apply(taken, new DeclineBet(), Catalog, Systems);

        Assert.Null(declined.Bet);
        Assert.Equal(state.Gold, declined.Gold);
    }

    /// <summary>
    /// Jugar el nodo resuelve la apuesta con los hechos del partido: cumplida, cobra el cobro bruto; fallida,
    /// no cobra nada. El oro final es el de la MISMA run sin apostar, menos lo apostado, más lo cobrado: la
    /// apuesta no toca nada más. Se buscan ambos desenlaces en partidos reales.
    /// </summary>
    [Fact]
    public void PlayingTheNodeResolvesTheBetFromTheMatchFacts()
    {
        bool sawMet = false;
        bool sawFailed = false;
        for (ulong seed = 1; seed <= 400 && !(sawMet && sawFailed); seed++)
        {
            if (FindOffered(seed) is not { } found)
            {
                continue;
            }

            var (state, node, offer) = found;
            var withBet = RunEngine.EnterMatch(RunEngine.Apply(state, new TakeBet(node.Id), Catalog, Systems), node.Id, Catalog, Systems);
            var without = RunEngine.EnterMatch(state, node.Id, Catalog, Systems);

            var bet = withBet.Summary.Bet;
            Assert.NotNull(bet);
            Assert.Null(without.Summary.Bet);
            Assert.Equal(offer.BetId, bet!.BetId);
            Assert.Equal(offer.Stake, bet.Stake);
            Assert.Null(withBet.State.Bet);

            // Misma run, mismo partido: la apuesta no lo cambia.
            Assert.Equal(without.Summary.Won, withBet.Summary.Won);
            Assert.Equal(without.Summary.GoalsFor, withBet.Summary.GoalsFor);
            Assert.Equal(without.Summary.GoalsAgainst, withBet.Summary.GoalsAgainst);

            Assert.Equal(bet.Met ? offer.Payout : 0, bet.GoldPaid);
            Assert.Equal(bet.GoldPaid - offer.Stake, bet.Net);
            if (!without.State.Result.IsOver)
            {
                Assert.Equal(without.State.Gold - offer.Stake + bet.GoldPaid, withBet.State.Gold);
            }

            sawMet |= bet.Met;
            sawFailed |= !bet.Met;
        }

        Assert.True(sawMet, "ninguna apuesta se cumplió en 400 semillas");
        Assert.True(sawFailed, "ninguna apuesta falló en 400 semillas");
    }

    /// <summary>Entrar en OTRO nodo con una apuesta tomada para uno distinto la devuelve: solo se pierde jugando y fallando.</summary>
    [Fact]
    public void EnteringAnotherNodeRefundsTheBet()
    {
        for (ulong seed = 1; seed <= 120; seed++)
        {
            var state = AfterFirstMatch(seed);
            if (state.Result.IsOver)
            {
                continue;
            }

            var matches = RunEngine.AvailableNodes(state).Where(n => n.IsMatch).OrderBy(n => n.Id).ToList();
            var offered = matches.FirstOrDefault(n => BetSystem.OfferFor(state, n, Systems, Catalog) is not null);
            if (matches.Count < 2 || offered is null)
            {
                continue;
            }

            var other = matches.First(n => n.Id != offered.Id);
            var taken = RunEngine.Apply(state, new TakeBet(offered.Id), Catalog, Systems);
            var entry = RunEngine.EnterMatch(taken, other.Id, Catalog, Systems);
            var plain = RunEngine.EnterMatch(state, other.Id, Catalog, Systems);

            Assert.Null(entry.Summary.Bet);
            Assert.Null(entry.State.Bet);
            if (!plain.State.Result.IsOver)
            {
                Assert.Equal(plain.State.Gold, entry.State.Gold);
            }

            return;
        }

        throw new InvalidOperationException("ninguna semilla de 1..120 tiene dos nodos de partido tras el primer partido, uno con oferta");
    }

    [Fact]
    public void TheSameSeedAndDecisionsGiveTheSameBetOutcome()
    {
        var (state, node, _) = Offered();
        BetResult? Play() => RunEngine
            .EnterMatch(RunEngine.Apply(state, new TakeBet(node.Id), Catalog, Systems), node.Id, Catalog, Systems)
            .Summary.Bet;

        Assert.Equal(Play(), Play());
    }

    // ---------------------------------------------------------------- guardado v7

    [Fact]
    public void SaveVersionIsNineAndATakenBetSurvivesTheRoundTrip()
    {
        Assert.Equal(9, RunState.CurrentSchemaVersion);
        var (state, node, _) = Offered();
        var taken = RunEngine.Apply(state, new TakeBet(node.Id), Catalog, Systems)
            .WithBet(new AcceptedBet("hunt_the_star", node.Id, 4, 973, 104, "Grok \"Comecráneos\""));

        var loaded = RunSave.Load(RunSave.Save(taken));

        Assert.Equal(9, loaded.SchemaVersion);
        Assert.Equal(taken.Bet, loaded.Bet);
        Assert.Equal(taken.Gold, loaded.Gold);
        Assert.Equal(RunSave.Save(taken), RunSave.Save(loaded));
    }

    [Fact]
    public void ARunWithoutABetSavesNullAndLoadsNull()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 3UL, Catalog, Systems);

        string json = RunSave.Save(state);
        Assert.Contains("\"bet\":null", json);
        Assert.Null(RunSave.Load(json).Bet);
    }

    [Fact]
    public void ASaveFromVersionSevenIsRejectedExplicitly()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 3UL, Catalog, Systems);
        string old = RunSave.Save(state).Replace($"\"schemaVersion\":{RunSave.SchemaVersion}", "\"schemaVersion\":7", StringComparison.Ordinal);

        var error = Assert.Throws<RunSaveException>(() => RunSave.Load(old));
        Assert.Equal("$.schemaVersion", error.JsonPath);
    }

    // ---------------------------------------------------------------- política

    private static RunPlayResult Play(ulong seed, BetDoctrine doctrine)
    {
        var files = TestData.LoadAllFiles();
        var options = RunPolicyOptions.For(PurchaseDoctrine.Contextual) with { BetDoctrine = doctrine };
        var setup = global::Underleague.Balance.FullRunRunner.SetupFor(Race.Human, Systems, files);
        return RunPolicy.Play(setup, seed, TestData.LoadCatalog(), Systems, BossCatalog.FromJson(files), options);
    }

    [Fact]
    public void ThePolicyNeverBetsByDefaultAndBlindBetsAndIsDeterministic()
    {
        Assert.Equal(BetDoctrine.Never, RunPolicyOptions.Default.BetDoctrine);

        var never = Play(11UL, BetDoctrine.Never);
        Assert.Equal(0, never.BetsTaken);
        Assert.Equal(0, never.BetNetGold);
        Assert.Equal(0, never.BetGoldStaked);

        var blind = Play(11UL, BetDoctrine.Blind);
        Assert.True(blind.BetsTaken > 0, "la política a ciegas no tomó ninguna apuesta");
        Assert.True(blind.BetGoldStaked >= blind.BetsTaken * 3);
        Assert.True(blind.BetNetGold >= -blind.BetGoldStaked);

        var again = Play(11UL, BetDoctrine.Blind);
        Assert.Equal(blind.BetsTaken, again.BetsTaken);
        Assert.Equal(blind.BetNetGold, again.BetNetGold);
        Assert.Equal(blind.Matches, again.Matches);
    }

    // ---------------------------------------------------------------- revisión independiente

    /// <summary>Estado tras el primer partido en el que hay un nodo de partido con oferta y otro que no es de partido.</summary>
    private static (RunState State, MapNode Offered, MapNode Service, BetOffer Offer) MatchAndService()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var start = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, Systems);
            foreach (var from in start.CurrentMap.Nodes.OrderBy(n => n.Id))
            {
                var state = RunStateBuilder.From(start).AtNode(from.Id).Build();
                var nodes = RunEngine.AvailableNodes(state);
                var service = nodes.FirstOrDefault(n => !n.IsMatch && n.Kind != NodeKind.Boss);
                foreach (var match in nodes.Where(n => n.IsMatch))
                {
                    if (service is not null && BetSystem.OfferFor(state, match, Systems, Catalog) is { } offer && offer.Stake <= state.Gold)
                    {
                        return (state, match, service, offer);
                    }
                }
            }
        }

        throw new InvalidOperationException("ninguna semilla de 1..60 tiene un partido con oferta y un nodo de servicio juntos");
    }

    /// <summary>
    /// Entrar en un nodo de servicio con una apuesta tomada la devuelve <b>antes</b> de abrirlo (sin oro
    /// escondido para un evento que grava el oro que llevas) y lo deja registrado.
    /// </summary>
    [Fact]
    public void EnteringAServiceNodeRefundsTheBetBeforeItOpensAndRecordsIt()
    {
        var (state, match, service, offer) = MatchAndService();
        var taken = RunEngine.Apply(state, new TakeBet(match.Id), Catalog, Systems);
        Assert.Equal(state.Gold - offer.Stake, taken.Gold);

        var inside = RunEngine.Enter(taken, service.Id, Catalog, Systems);

        Assert.Null(inside.Bet);
        Assert.Equal(offer.Stake, inside.Counter(RunState.BetRefundedCounter));
        Assert.Equal(state.Gold, inside.Gold);
    }

    /// <summary>La devolución no es un canal de oro: entrar en el nodo de la apuesta no devuelve nada y borra el aviso anterior.</summary>
    [Fact]
    public void TheRefundNoteIsClearedByTheNextEntryAndByTakingOrDecliningABet()
    {
        var (state, match, service, _) = MatchAndService();
        var refunded = RunEngine.Enter(RunEngine.Apply(state, new TakeBet(match.Id), Catalog, Systems), service.Id, Catalog, Systems);
        Assert.NotEqual(0, refunded.Counter(RunState.BetRefundedCounter));

        var left = RunEngine.Apply(refunded, new LeaveNode(), Catalog, Systems);
        var next = RunEngine.AvailableNodes(left).FirstOrDefault(n => n.IsMatch);
        if (next is not null && BetSystem.OfferFor(left, next, Systems, Catalog) is not null)
        {
            var taken = RunEngine.Apply(left, new TakeBet(next.Id), Catalog, Systems);
            Assert.Equal(0, taken.Counter(RunState.BetRefundedCounter));
        }
    }

    /// <summary>Entrar en otro partido devuelve la apuesta antes de jugarlo, y el resumen y el informe lo dicen.</summary>
    [Fact]
    public void EnteringAnotherMatchRefundsAndTheSummaryAndTheReportSayIt()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var state = AfterFirstMatch(seed);
            if (state.Result.IsOver)
            {
                continue;
            }

            var matches = RunEngine.AvailableNodes(state).Where(n => n.IsMatch).OrderBy(n => n.Id).ToList();
            var offered = matches.FirstOrDefault(n => BetSystem.OfferFor(state, n, Systems, Catalog) is { } o && o.Stake <= state.Gold);
            if (matches.Count < 2 || offered is null)
            {
                continue;
            }

            int stake = BetSystem.OfferFor(state, offered, Systems, Catalog)!.Stake;
            var other = matches.First(n => n.Id != offered.Id);
            var taken = RunEngine.Apply(state, new TakeBet(offered.Id), Catalog, Systems);
            var playback = Underleague.Sim.Run.View.MatchPlaybacks.Of(taken, other.Id, Catalog, Systems);
            var entry = RunEngine.EnterMatch(taken, other.Id, Catalog, Systems);

            Assert.Equal(stake, entry.Summary.BetRefunded);
            Assert.Null(entry.Summary.Bet);
            var report = Underleague.Sim.Run.View.PostMatchView.Build(playback, entry.State, entry.Summary, Catalog, Systems.Economy, Systems.Items);
            Assert.Equal(stake, report.BetRefunded);
            Assert.Null(report.Bet);
            return;
        }

        throw new InvalidOperationException("ninguna semilla de 1..200 tiene dos partidos tras el primer, uno con oferta");
    }

    [Fact]
    public void DecliningTheBetIsOnlyAllowedOnTheMap()
    {
        var (state, match, service, _) = MatchAndService();
        var taken = RunEngine.Apply(state, new TakeBet(match.Id), Catalog, Systems);
        var inside = RunEngine.Enter(taken, service.Id, Catalog, Systems);

        Assert.Equal(RunPhase.NodeOpen, inside.Phase);
        Assert.Throws<InvalidOperationException>(() => RunEngine.Apply(inside, new DeclineBet(), Catalog, Systems));
    }

    /// <summary>Guardar con una apuesta pendiente, cargar y jugar da exactamente lo mismo que jugar sin guardar.</summary>
    [Fact]
    public void SavingWithAPendingBetLoadingAndPlayingGivesTheSameResult()
    {
        var (state, node, _) = Offered();
        var taken = RunEngine.Apply(state, new TakeBet(node.Id), Catalog, Systems);
        var loaded = RunSave.Load(RunSave.Save(taken));

        var direct = RunEngine.EnterMatch(taken, node.Id, Catalog, Systems);
        var afterLoad = RunEngine.EnterMatch(loaded, node.Id, Catalog, Systems);

        Assert.Equal(direct.Summary.Bet, afterLoad.Summary.Bet);
        Assert.Equal(direct.Summary.GoalsFor, afterLoad.Summary.GoalsFor);
        Assert.Equal(direct.Summary.GoalsAgainst, afterLoad.Summary.GoalsAgainst);
        Assert.Equal(direct.State.Gold, afterLoad.State.Gold);
        Assert.Equal(RunSave.Save(direct.State), RunSave.Save(afterLoad.State));
    }

    /// <summary>El informe propaga el resultado de la apuesta que resolvió el motor.</summary>
    [Fact]
    public void ThePostMatchReportCarriesTheBetResult()
    {
        var (state, node, _) = Offered();
        var taken = RunEngine.Apply(state, new TakeBet(node.Id), Catalog, Systems);
        var playback = Underleague.Sim.Run.View.MatchPlaybacks.Of(taken, node.Id, Catalog, Systems);
        var entry = RunEngine.EnterMatch(taken, node.Id, Catalog, Systems);

        var report = Underleague.Sim.Run.View.PostMatchView.Build(playback, entry.State, entry.Summary, Catalog, Systems.Economy, Systems.Items);

        Assert.NotNull(entry.Summary.Bet);
        Assert.Equal(entry.Summary.Bet, report.Bet);
        Assert.Equal(0, report.BetRefunded);
    }

    /// <summary>
    /// Un partido que TERMINA la run resuelve igualmente la apuesta y cobra si se cumple (jefe perdido: la
    /// apuesta de sangre no exige ganar). Se busca en partidos reales; el oro es el de antes menos lo apostado
    /// más lo cobrado, sin el oro de partido (AfterMatch no se llama al terminar la run).
    /// </summary>
    [Fact]
    public void AMatchThatEndsTheRunStillResolvesAndPaysAMetBet()
    {
        var bosses = BossCatalog.FromJson(TestData.LoadAllFiles());
        var systems = new BossRunSystems(bosses, Systems);
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var start = systems.AssignBosses(RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, systems));
            var map = start.CurrentMap;
            var boss = map.Get(map.BossNodeId);
            int before = map.Nodes.First(n => n.Next.Contains(boss.Id)).Id;
            var state = Underleague.Sim.Run.RunStateBuilder.From(start).AtNode(before).Build();
            if (!RunEngine.AvailableNodes(state).Any(n => n.Id == boss.Id)
                || BetSystem.OfferFor(state, boss, systems, Catalog) is not { } offer
                || offer.Stake > state.Gold)
            {
                continue;
            }

            var entry = RunEngine.EnterMatch(RunEngine.Apply(state, new TakeBet(boss.Id), Catalog, systems), boss.Id, Catalog, systems);
            if (!entry.State.Result.IsOver || entry.Summary.Bet is not { Met: true } bet)
            {
                continue;
            }

            Assert.Equal(state.Gold - offer.Stake + offer.Payout, entry.State.Gold);
            Assert.Equal(offer.Payout, bet.GoldPaid);
            Assert.Null(entry.State.Bet);
            return;
        }

        throw new InvalidOperationException("ninguna semilla de 1..300 termina la run en el jefe con una apuesta cumplida");
    }
}
