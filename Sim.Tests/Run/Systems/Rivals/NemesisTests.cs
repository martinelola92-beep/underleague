using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Nicknames;
using Underleague.Sim.Run.Systems.Rivals;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run.Systems.Rivals;

/// <summary>
/// ADR 0165: la memoria de los clanes rivales. Un rival de clan que mata a uno de los nuestros se convierte en
/// némesis (con tope), un rival muerto no vuelve, un némesis cambia de clan entre actos de forma determinista y
/// la venganza se cobra en carrera, en oro y en apodo. Se ejercita <c>MatchResolution.Apply</c> con eventos a mano,
/// como los tests de créditos de rival (BE-B), sobre los datos reales.
/// </summary>
public sealed class NemesisTests
{
    private const string OpponentId = "act1_orc_ironclad";
    private const string Clan = "ironclad";

    private static readonly Catalog Catalog = SystemsTestSupport.Catalog;
    private static readonly StandardRunSystems Systems = SystemsTestSupport.Systems;
    private static readonly NemesisCatalog Nemesis = Systems.Nemesis;
    private static readonly RivalCatalog Rivals = Systems.Rivals;

    private static RunState BaseState(ulong seed = 20260929UL) =>
        RunEngine.Start(TestRuns.Setup(), seed, Catalog);

    private static MapNode Node(string opponentId = OpponentId, int act = 1, int id = 101) =>
        new(id, act, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), opponentId, 3);

    private static MatchLineup LineupFor(IReadOnlyList<RunPlayer> starters)
    {
        var startDefs = starters.Select(p => p.ToDefinition(Catalog)).ToList();
        return new MatchLineup(startDefs, Array.Empty<PlayerDefinition>(), Lineup.Default(startDefs), EmergencyGoalkeeperId: -1);
    }

    private static MatchResult ResultWith(IReadOnlyList<RunPlayer> own, IReadOnlyList<MatchEvent> events, int winner = 0)
    {
        var builder = new MatchReportBuilder { Winner = winner, Ticks = 900 };
        foreach (var p in own)
        {
            builder.Players.Add(new PlayerMatchStats(p.Id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 900, 0, 0));
        }

        return new MatchResult(events, builder.Build(), Array.Empty<PlayerCounterDelta>());
    }

    private static MatchEvent Injury(int team, int actor, int opponent, string detail = "minor", int tick = 50) =>
        new(EventType.Injury, tick, team, actor, -1, opponent, new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, detail);

    private static MatchEvent Death(int team, int actor, int opponent, int tick = 80) =>
        new(EventType.Death, tick, team, actor, -1, opponent, new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, "severeInjury");

    private static int RivalId(int slot) => RivalTeamBuilder.OpponentFirstPlayerId + slot;

    private static MatchResolution.Applied Play(RunState state, IReadOnlyList<MatchEvent> events, MapNode? node = null, int winner = 0)
    {
        var own = state.Roster.Take(7).ToList();
        return MatchResolution.Apply(state, node ?? Node(), LineupFor(own), ResultWith(own, events, winner), Catalog, null, Nemesis);
    }

    /// <summary>Estado con un némesis vivo ya en la memoria, sin pasar por un partido.</summary>
    private static RunState WithNemesis(RunState state, string clan, int slot, int id = 1, string title = "quiet_one")
    {
        var team = Rivals.OfClan(clan, 1)!;
        var made = new RivalNemesis(
            id, title, team.Players[slot].Name, team.Players[slot].Position, clan, slot, clan, slot,
            "Víctima", 999, 1, 1, NemesisStatus.Active);
        return state.WithRivalMemory(state.RivalMemory.WithNemesis(made));
    }

    // ------------------------------------------------------------------ némesis

    [Fact]
    public void ARivalWhoKillsAnOwnPlayerBecomesANemesisWithATitle()
    {
        var state = BaseState();
        var victim = state.Roster[2];

        var applied = Play(state, new[] { Death(0, victim.Id, RivalId(3)) });

        var memory = applied.State.RivalMemory;
        var nemesis = Assert.Single(memory.Nemeses);
        Assert.Equal(Rivals.OfClan(Clan, 1)!.Players[3].Name, nemesis.Name);
        Assert.Equal(Clan, nemesis.ClanId);
        Assert.Equal(3, nemesis.Slot);
        Assert.Equal(victim.Name, nemesis.VictimName);
        Assert.Equal(1, nemesis.Act);
        Assert.NotNull(Nemesis.Find(nemesis.TitleId));
        Assert.True(nemesis.IsActive);
        var made = Assert.Single(applied.Summary.NemesesMade);
        Assert.Equal(nemesis.Id, made.NemesisId);
    }

    [Fact]
    public void AnInjuryOrARivalDyingDoesNotMakeANemesis()
    {
        var state = BaseState();
        var applied = Play(state, new[] { Injury(0, state.Roster[1].Id, RivalId(3), "severe") });

        Assert.Empty(applied.State.RivalMemory.Nemeses);
    }

    [Fact]
    public void ANemesisPlaysWithOneMoreLevelAndItsOwnName()
    {
        var state = BaseState();
        var victim = state.Roster[2];
        var applied = Play(state, new[] { Death(0, victim.Id, RivalId(3)) });
        var team = Rivals.Find(OpponentId)!;

        var plain = RivalTeamBuilder.Build(team, Catalog);
        var withMemory = RivalTeamBuilder.Build(team, Catalog, applied.State.RivalMemory, state.Seed, Nemesis.LevelBonus);

        Assert.Equal(plain.Players[3].Name, withMemory.Players[3].Name);
        Assert.Equal(plain.Players[3].Level + Nemesis.LevelBonus, withMemory.Players[3].Level);
        Assert.Equal(plain.Players[2].Level, withMemory.Players[2].Level);
        Assert.Equal(plain.Players[4].Attributes, withMemory.Players[4].Attributes);
    }

    [Fact]
    public void ABenchNemesisPlaysAsAStarter()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 8);
        var team = Rivals.Find(OpponentId)!;

        var built = RivalTeamBuilder.Build(team, Catalog, state.RivalMemory, state.Seed, Nemesis.LevelBonus);

        Assert.Contains(built.Lineup.Slots, s => s.PlayerId == RivalId(8));
        Assert.Equal(7, built.Lineup.Slots.Count);
    }

    [Fact]
    public void ANemesisKillingAgainKeepsItsTitleAndCountsTheKill()
    {
        var state = BaseState();
        var first = Play(state, new[] { Death(0, state.Roster[2].Id, RivalId(3)) }).State;

        var second = Play(first, new[] { Death(0, first.Roster[3].Id, RivalId(3)) }).State;

        var nemesis = Assert.Single(second.RivalMemory.Nemeses);
        Assert.Equal(2, nemesis.Kills);
        Assert.Equal(first.RivalMemory.Nemeses[0].TitleId, nemesis.TitleId);
        Assert.Equal(first.RivalMemory.Nemeses[0].VictimName, nemesis.VictimName);
    }

    [Fact]
    public void TheCapOfLivingNemesesStopsTheNextKillerAndNotesIt()
    {
        var state = BaseState();
        int cap = Nemesis.MaxAlive;
        var events = new List<MatchEvent>();
        for (int i = 0; i <= cap; i++)
        {
            events.Add(Death(0, state.Roster[i].Id, RivalId(i + 1), tick: 60 + i));
        }

        var applied = Play(state, events);

        Assert.Equal(cap, applied.State.RivalMemory.Alive.Count);
        Assert.Equal(1, applied.Summary.NemesesCapped);
        Assert.Equal(1, applied.State.Counter(RunState.NemesisCappedCounter));
        Assert.Equal(cap, applied.Summary.NemesesMade.Count);
    }

    [Fact]
    public void TitlesAreNotRepeatedWhileSomeAreUnused()
    {
        var state = BaseState();
        var applied = Play(state, new[]
        {
            Death(0, state.Roster[0].Id, RivalId(1), tick: 60),
            Death(0, state.Roster[1].Id, RivalId(2), tick: 61),
        });

        var titles = applied.State.RivalMemory.Nemeses.Select(n => n.TitleId).ToList();
        Assert.Equal(titles.Count, titles.Distinct().Count());
    }

    [Fact]
    public void AKillInABossOrProceduralNodeMakesNoNemesis()
    {
        var state = BaseState();
        var boss = new MapNode(102, 1, 0, 0, NodeKind.Boss, Array.Empty<int>(), OpponentId, 3);
        var procedural = new MapNode(103, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var events = new[] { Death(0, state.Roster[2].Id, RivalId(3)) };

        Assert.Empty(Play(state, events, boss).State.RivalMemory.Nemeses);
        Assert.Empty(Play(state, events, procedural).State.RivalMemory.Nemeses);
    }

    [Fact]
    public void ADeathOfAnOwnPlayerByAnOwnPerkMakesNoNemesis()
    {
        var state = BaseState();
        var applied = Play(state, new[] { Death(0, state.Roster[2].Id, state.Roster[1].Id) });

        Assert.Empty(applied.State.RivalMemory.Nemeses);
    }

    // ------------------------------------------------------------------ un muerto no vuelve

    [Fact]
    public void ADeadRivalNeverComesBackAndASigningTakesItsSlotWithTheSameNumbers()
    {
        var state = BaseState();
        var team = Rivals.Find(OpponentId)!;
        var killer = state.Roster[0];

        var applied = Play(state, new[] { Death(1, RivalId(4), killer.Id) });

        Assert.Equal(0, applied.State.RivalMemory.VacancyAt(Clan, 4)!.Generation);
        var before = RivalTeamBuilder.Build(team, Catalog);
        var after = RivalTeamBuilder.Build(team, Catalog, applied.State.RivalMemory, state.Seed, Nemesis.LevelBonus);
        Assert.NotEqual(before.Players[4].Name, after.Players[4].Name);
        Assert.False(string.IsNullOrWhiteSpace(after.Players[4].Name));
        Assert.Equal(before.Players[4].Attributes, after.Players[4].Attributes);
        Assert.Equal(before.Players[4].Level, after.Players[4].Level);
        Assert.Equal(before.Players[4].Perks, after.Players[4].Perks);
        Assert.Equal(before.Players[3].Name, after.Players[3].Name);
    }

    [Fact]
    public void ASigningKeepsItsNameMatchAfterMatchAndTheNextOneIsNamedDifferently()
    {
        var state = BaseState();
        var team = Rivals.Find(OpponentId)!;
        var killer = state.Roster[0];
        var first = Play(state, new[] { Death(1, RivalId(4), killer.Id) }).State;

        string name1 = RivalTeamBuilder.Build(team, Catalog, first.RivalMemory, state.Seed, 1).Players[4].Name;
        string name1Again = RivalTeamBuilder.Build(team, Catalog, first.RivalMemory, state.Seed, 1).Players[4].Name;
        var second = Play(first, new[] { Death(1, RivalId(4), killer.Id) }).State;
        string name2 = RivalTeamBuilder.Build(team, Catalog, second.RivalMemory, state.Seed, 1).Players[4].Name;

        Assert.Equal(name1, name1Again);
        Assert.NotEqual(name1, name2);
    }

    // ------------------------------------------------------------------ venganza

    [Fact]
    public void InjuringANemesisIsARevengeInCareerGoldAndSummary()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);
        var avenger = state.Roster[1];

        var applied = Play(state, new[] { Injury(1, RivalId(3), avenger.Id) });

        var revenge = Assert.Single(applied.Summary.Revenges);
        Assert.Equal(avenger.Id, revenge.AvengerPlayerId);
        Assert.False(revenge.Slain);
        Assert.Equal(1, applied.State.FindPlayer(avenger.Id)!.Career.Revenges);
        Assert.Equal(1, applied.State.Counter(RunState.RevengesCounter));
        Assert.True(applied.State.RivalMemory.Find(1)!.IsActive);
    }

    [Fact]
    public void KillingANemesisSlaysItAndItsSlotIsFilledByASigning()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);
        var avenger = state.Roster[1];

        var applied = Play(state, new[] { Death(1, RivalId(3), avenger.Id) });

        var revenge = Assert.Single(applied.Summary.Revenges);
        Assert.True(revenge.Slain);
        Assert.Equal(NemesisStatus.Slain, applied.State.RivalMemory.Find(1)!.Status);
        Assert.Empty(applied.State.RivalMemory.Alive);
        Assert.NotNull(applied.State.RivalMemory.VacancyAt(Clan, 3));
    }

    [Fact]
    public void ARevengeIsPaidOncePerNemesisAndMatch()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);
        var applied = Play(state, new[]
        {
            Injury(1, RivalId(3), state.Roster[1].Id, tick: 40),
            Injury(1, RivalId(3), state.Roster[2].Id, tick: 41),
        });

        Assert.Single(applied.Summary.Revenges);
        Assert.Equal(1, applied.State.Counter(RunState.RevengesCounter));
    }

    /// <summary>Un fichaje que se hace némesis: matas al titular del puesto 4, el fichaje mata a uno de los tuyos.</summary>
    private static (RunState State, RivalNemesis Nemesis) SigningNemesis()
    {
        var state = BaseState();
        state = Play(state, new[] { Death(1, RivalId(4), state.Roster[0].Id) }).State;
        state = Play(state, new[] { Death(0, state.Roster[2].Id, RivalId(4)) }).State;
        var nemesis = Assert.Single(state.RivalMemory.Nemeses);
        Assert.Equal(4, nemesis.Slot);
        return (state, nemesis);
    }

    /// <summary>
    /// BS-A: la esquela de la Gaceta nombra a quien ocupaba el puesto cuando mató —aquí el fichaje que entró tras la
    /// muerte del titular—, no al jugador de datos del fichero del clan.
    /// </summary>
    [Fact]
    public void TheEpitaphNamesTheSigningWhoKilledNotTheDataPlayer()
    {
        var (state, nemesis) = SigningNemesis();
        var victim = state.Roster.Single(p => p.PhysicalState == PhysicalState.Dead);
        string dataName = Rivals.Find(OpponentId)!.Players[4].Name;
        Assert.NotEqual(dataName, nemesis.Name);
        Assert.Equal(1, state.DeathKillerOf(victim.Id));

        var gazette = Underleague.Sim.Run.View.GazetteView.Build(
            state, Catalog, Systems.Nicknames, Rivals, Systems.Gazette, "es", Nemesis);
        var obituary = gazette.Obituaries.Single(o => o.PlayerId == victim.Id);

        Assert.Contains(nemesis.Name, obituary.Epitaph, StringComparison.Ordinal);
        Assert.DoesNotContain(dataName, obituary.Epitaph, StringComparison.Ordinal);
    }

    /// <summary>
    /// BS-A (revisión): un némesis traspasado a otro clan que mata en el acto 2 queda anotado con su código de némesis,
    /// y la esquela lo nombra a él, no al jugador de datos del puesto que ocupa en su clan nuevo.
    /// </summary>
    [Fact]
    public void TheEpitaphNamesAHandedOverNemesisWhoKillsInItsNewClan()
    {
        var (state, nemesis) = SigningNemesis();
        state = NemesisSystem.TransferOnActEntry(state, Nemesis, act: 2);
        var moved = state.RivalMemory.Find(nemesis.Id)!;
        var target = Rivals.OfClan(moved.ClanId, 2)!;
        var victim = state.Roster.First(p => p.PhysicalState != PhysicalState.Dead && p.Id != state.Roster[0].Id);

        var after = Play(state, new[] { Death(0, victim.Id, RivalId(moved.Slot)) }, Node(target.Id, act: 2, id: 202)).State;

        Assert.Equal(RivalKiller.NemesisBase + nemesis.Id, after.DeathKillerOf(victim.Id));
        var gazette = Underleague.Sim.Run.View.GazetteView.Build(
            after, Catalog, Systems.Nicknames, Rivals, Systems.Gazette, "es", Nemesis);
        string epitaph = gazette.Obituaries.Single(o => o.PlayerId == victim.Id).Epitaph;
        Assert.Contains(nemesis.Name, epitaph, StringComparison.Ordinal);
        Assert.DoesNotContain(target.Players[moved.Slot].Name, epitaph, StringComparison.Ordinal);
    }

    /// <summary>
    /// BS-A: el villano de la Gaceta por créditos (sin némesis que lo prefiera) nombra al fichaje que ocupaba el puesto
    /// cuando mató, y el crédito de la muerte lleva su ocupante.
    /// </summary>
    [Fact]
    public void TheCreditVillainIsNamedAfterTheSigningNotTheDataPlayer()
    {
        var (state, nemesis) = SigningNemesis();
        string dataName = Rivals.Find(OpponentId)!.Players[4].Name;
        var death = RivalCredits.Against(state, OpponentId).Single(c => c.Kind == RivalCreditKind.SufferedDeath);
        Assert.Equal(4, death.RivalIndex);
        Assert.Equal(1, death.Occupant);

        var gazette = Underleague.Sim.Run.View.GazetteView.Build(
            state, Catalog, Systems.Nicknames, Rivals, Systems.Gazette, "es", nemesis: null);
        Assert.NotNull(gazette.Villain);
        Assert.Equal(nemesis.Name, gazette.Villain!.Name);
        Assert.NotEqual(dataName, gazette.Villain.Name);
    }

    /// <summary>BS-A: una lesión causada por un fichaje también anota quién ocupaba el puesto.</summary>
    [Fact]
    public void AnInjuryCreditRecordsTheSigningWhoCausedIt()
    {
        var (state, nemesis) = SigningNemesis();
        var victim = state.Roster.First(p => p.PhysicalState != PhysicalState.Dead && p.Id != state.Roster[0].Id);
        var after = Play(state, new[] { Injury(0, victim.Id, RivalId(4)) }).State;
        var credit = RivalCredits.Against(after, OpponentId)
            .Single(c => c.Kind == RivalCreditKind.SufferedInjury && c.OwnPlayerId == victim.Id);
        Assert.Equal(RivalKiller.NemesisBase + nemesis.Id, credit.Occupant);
    }

    /// <summary>BS-A: con el jugador de datos en el puesto no se anota ocupante.</summary>
    [Fact]
    public void ADataPlayerCreditHasNoOccupant()
    {
        var state = BaseState();
        var victim = state.Roster[2];
        var after = Play(state, new[] { Death(0, victim.Id, RivalId(3)) }).State;
        Assert.All(RivalCredits.Against(after, OpponentId), c => Assert.Equal(0, c.Occupant));
    }

    private static IReadOnlyList<RivalCredit> Credits(RunState state, int victimId, RivalCreditKind kind, int slot = 4) =>
        RivalCredits.Against(state, OpponentId).Where(c => c.OwnPlayerId == victimId && c.Kind == kind && c.RivalIndex == slot).ToList();

    /// <summary>
    /// BS-A (revisión): el jugador de datos lesiona a A, lo matamos, entra un fichaje que lesiona otra vez a A: son dos
    /// créditos de un hecho cada uno, de ocupantes distintos, no «el fichaje lesionó a A dos veces».
    /// </summary>
    [Fact]
    public void TheCountIsPerOccupantWhenTheSigningInjuresTheSameVictimAgain()
    {
        var state = BaseState();
        var victim = state.Roster[3];
        state = Play(state, new[] { Injury(0, victim.Id, RivalId(4)) }).State;
        state = Play(state, new[] { Death(1, RivalId(4), state.Roster[0].Id) }).State;
        state = Play(state, new[] { Injury(0, victim.Id, RivalId(4)) }).State;

        var credits = Credits(state, victim.Id, RivalCreditKind.SufferedInjury);
        Assert.Equal(2, credits.Count);
        var byData = Assert.Single(credits, c => c.Occupant == 0);
        var bySigning = Assert.Single(credits, c => c.Occupant != 0);
        Assert.Equal(1, byData.Count);
        Assert.Equal(1, bySigning.Count);
    }

    /// <summary>BS-A (revisión): el némesis que se va conserva su crédito y no firma lo que haga quien ocupe luego su puesto.</summary>
    [Fact]
    public void ANemesisWhoLeavesKeepsItsCreditAndDoesNotSignWhatTheNextOccupantDoes()
    {
        var (state, nemesis) = SigningNemesis();
        int code = RivalKiller.NemesisBase + nemesis.Id;
        var first = state.Roster.First(p => p.PhysicalState != PhysicalState.Dead && p.Id != state.Roster[0].Id);
        state = Play(state, new[] { Injury(0, first.Id, RivalId(4)) }).State;
        Assert.Equal(code, Credits(state, first.Id, RivalCreditKind.SufferedInjury).Single().Occupant);

        state = NemesisSystem.TransferOnActEntry(state, Nemesis, act: 2);
        Assert.Null(state.RivalMemory.ActiveAt(Clan, 4));
        var victim = state.Roster.First(p => p.PhysicalState != PhysicalState.Dead && p.Id != state.Roster[0].Id && p.Id != first.Id);
        state = Play(state, new[] { Injury(0, victim.Id, RivalId(4)) }).State;

        Assert.NotEqual(code, Credits(state, victim.Id, RivalCreditKind.SufferedInjury).Single().Occupant);
        Assert.Equal(code, Credits(state, first.Id, RivalCreditKind.SufferedInjury).Single().Occupant);
    }

    /// <summary>BS-A (revisión): el villano por créditos cuenta cada ocupante por separado cuando el puesto se reparte.</summary>
    [Fact]
    public void TheCreditVillainCountsOnlyTheOccupantsOwnVictims()
    {
        var state = BaseState();
        var (a, b, c) = (state.Roster[3], state.Roster[4], state.Roster[5]);
        // El jugador de datos lesiona a A y a B; lo matamos; el fichaje lesiona a C. El puesto suma 3 víctimas, pero
        // el villano es el jugador de datos con 2, no «el fichaje con 3».
        state = Play(state, new[] { Injury(0, a.Id, RivalId(4)), Injury(0, b.Id, RivalId(4), tick: 60) }).State;
        state = Play(state, new[] { Death(1, RivalId(4), state.Roster[0].Id) }).State;
        state = Play(state, new[] { Injury(0, c.Id, RivalId(4)) }).State;

        string dataName = Rivals.Find(OpponentId)!.Players[4].Name;
        var gazette = Underleague.Sim.Run.View.GazetteView.Build(
            state, Catalog, Systems.Nicknames, Rivals, Systems.Gazette, "es", nemesis: null);
        Assert.NotNull(gazette.Villain);
        Assert.Equal(dataName, gazette.Villain!.Name);
        Assert.Equal(2, gazette.Villain.Injuries);
    }

    /// <summary>BS-A: un guardado anterior (clave sin ocupante) se lee como el jugador de datos.</summary>
    [Fact]
    public void AnOldCreditKeyReadsAsTheDataPlayer()
    {
        var state = BaseState().WithCounter("rivalCredit:" + OpponentId + ":4:7:sufferedInjury", 2);
        var credit = Assert.Single(RivalCredits.Against(state, OpponentId));
        Assert.Equal(0, credit.Occupant);
        Assert.Equal(2, credit.Count);
    }

    /// <summary>BS-A: sin cambio de ocupante no se anota nada y la esquela nombra al jugador de datos, como antes.</summary>
    [Fact]
    public void ADataPlayerKillerLeavesNoCodeAndKeepsItsName()
    {
        var state = BaseState();
        var victim = state.Roster[2];
        var after = Play(state, new[] { Death(0, victim.Id, RivalId(3)) }).State;

        Assert.Equal(0, after.DeathKillerOf(victim.Id));
        var gazette = Underleague.Sim.Run.View.GazetteView.Build(
            after, Catalog, Systems.Nicknames, Rivals, Systems.Gazette, "es", Nemesis);
        Assert.Contains(Rivals.Find(OpponentId)!.Players[3].Name, gazette.Obituaries.Single(o => o.PlayerId == victim.Id).Epitaph, StringComparison.Ordinal);
    }

    /// <summary>Revisión de la ADR 0165: el némesis que era un fichaje no resucita en su puesto al morir.</summary>
    [Fact]
    public void ASigningWhoBecameANemesisDoesNotComeBackAfterDying()
    {
        var (state, nemesis) = SigningNemesis();
        var team = Rivals.Find(OpponentId)!;
        Assert.Equal(nemesis.Name, RivalTeamBuilder.Build(team, Catalog, state.RivalMemory, state.Seed, 1).Players[4].Name);

        var after = Play(state, new[] { Death(1, RivalId(4), state.Roster[1].Id) }).State;

        Assert.Equal(NemesisStatus.Slain, after.RivalMemory.Find(nemesis.Id)!.Status);
        var built = RivalTeamBuilder.Build(team, Catalog, after.RivalMemory, state.Seed, Nemesis.LevelBonus);
        Assert.NotEqual(nemesis.Name, built.Players[4].Name);
    }

    /// <summary>Revisión de la ADR 0165: el fichaje-némesis traspasado no se queda también en su clan de origen.</summary>
    [Fact]
    public void ASigningNemesisHandedOverIsNotAlsoLeftInItsHomeClan()
    {
        var (state, nemesis) = SigningNemesis();

        var next = NemesisSystem.TransferOnActEntry(state, Nemesis, act: 2);

        var moved = next.RivalMemory.Find(nemesis.Id)!;
        Assert.NotEqual(Clan, moved.ClanId);
        var home = RivalTeamBuilder.Build(Rivals.OfClan(Clan, 2)!, Catalog, next.RivalMemory, state.Seed, Nemesis.LevelBonus);
        Assert.DoesNotContain(home.Players, p => p.Name == nemesis.Name);
    }

    /// <summary>
    /// Revisión de la ADR 0165: un rival llega sano, así que muere por un perk letal sobre una lesión previa en el mismo
    /// partido; la venganza la abre la lesión y el informe tiene que decir que murió.
    /// </summary>
    [Fact]
    public void AnInjuryThenADeathInTheSameMatchIsProclaimedAsSlain()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);
        var avenger = state.Roster[1];

        var applied = Play(state, new[] { Injury(1, RivalId(3), avenger.Id, "severe", tick: 40), Death(1, RivalId(3), avenger.Id, tick: 41) });

        var revenge = Assert.Single(applied.Summary.Revenges);
        Assert.True(revenge.Slain);
        Assert.True(revenge.Paid);
        Assert.Equal(1, applied.State.Counter(RunState.RevengesCounter));
        Assert.Equal(NemesisStatus.Slain, applied.State.RivalMemory.Find(1)!.Status);
    }

    /// <summary>
    /// Revisión de la ADR 0165: la deuda de sangre se cobra una vez. Lesionar al mismo némesis en cada partido no
    /// paga ni suma; matarlo después se proclama sin segundo cobro; si vuelve a matar, vuelve a deber.
    /// </summary>
    [Fact]
    public void ABloodDebtIsPaidOnceAndComesBackIfTheNemesisKillsAgain()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);
        var first = Play(state, new[] { Injury(1, RivalId(3), state.Roster[1].Id) });
        Assert.True(Assert.Single(first.Summary.Revenges).Paid);
        Assert.True(first.State.RivalMemory.Find(1)!.Avenged);

        var second = Play(first.State, new[] { Injury(1, RivalId(3), state.Roster[1].Id) }, winner: 1);
        Assert.False(Assert.Single(second.Summary.Revenges).Paid);
        Assert.Equal(1, second.State.Counter(RunState.RevengesCounter));
        Assert.Equal(1, second.State.FindPlayer(state.Roster[1].Id)!.Career.Revenges);
        Assert.Equal(second.State.Gold, Systems.AfterMatch(second.State, Node(), second.Summary, Catalog).Gold);

        var killsAgain = Play(second.State, new[] { Death(0, state.Roster[3].Id, RivalId(3)) });
        Assert.False(killsAgain.State.RivalMemory.Find(1)!.Avenged);
        var third = Play(killsAgain.State, new[] { Injury(1, RivalId(3), state.Roster[1].Id) });
        Assert.True(Assert.Single(third.Summary.Revenges).Paid);
        Assert.Equal(2, third.State.Counter(RunState.RevengesCounter));
    }

    /// <summary>
    /// ADR 0167 (revisión independiente): la lesión de la turba no tiene autor (<c>Opponent</c> −1). No es venganza,
    /// y una lesión leve sobre un lesionado grave no lo cura: sigue grave (antes quedaba en leve).
    /// </summary>
    [Fact]
    public void AMobInjuryHasNoAuthorIsNoRevengeAndNeverHealsASevereInjury()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);
        var hurt = state.Roster[2] with { PhysicalState = PhysicalState.SevereInjury };
        state = state.WithPlayer(hurt);

        var applied = Play(state, new[] { Injury(1, RivalId(3), -1), Injury(0, hurt.Id, -1, "minor", tick: 60) });

        Assert.Empty(applied.Summary.Revenges);
        Assert.Equal(PhysicalState.SevereInjury, applied.State.FindPlayer(hurt.Id)!.PhysicalState);
        Assert.Single(applied.State.RivalMemory.Nemeses);
    }

    [Fact]
    public void InjuringAPlainRivalIsNotARevenge()
    {
        var state = BaseState();
        var applied = Play(state, new[] { Injury(1, RivalId(3), state.Roster[1].Id) });

        Assert.Empty(applied.Summary.Revenges);
        Assert.Equal(0, applied.State.FindPlayer(state.Roster[1].Id)!.Career.Revenges);
    }

    [Fact]
    public void TheRunEarnsTheRevengeGoldEvenWhenTheMatchIsLost()
    {
        Assert.True(Systems.Economy.RevengeGold > 0);
        var state = WithNemesis(BaseState(), Clan, slot: 3);
        var applied = Play(state, new[] { Injury(1, RivalId(3), state.Roster[1].Id) }, winner: 1);
        Assert.False(applied.Summary.Won);

        var after = Systems.AfterMatch(applied.State, Node(), applied.Summary, Catalog);

        Assert.Equal(applied.State.Gold + Systems.Economy.RevengeGold, after.Gold);
    }

    [Fact]
    public void TheAvengerNicknameIsEarnedWithTheFirstRevenge()
    {
        var nicknames = Systems.Nicknames;
        var avenger = nicknames.Find("avenger");
        Assert.NotNull(avenger);
        Assert.Equal(NicknameStat.Revenges, avenger!.Stat);

        var without = RunCareer.None;
        var with = RunCareer.None with { Revenges = 1 };
        Assert.NotEqual("avenger", NicknameSystem.For(without, nicknames)?.Id);
        Assert.Equal("avenger", NicknameSystem.For(with, nicknames)?.Id);
        Assert.Equal(without, NicknameSystem.BeforeMatch(with, new PlayerMatchStats(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0), 1));
    }

    // ------------------------------------------------------------------ traspaso entre actos

    [Fact]
    public void ANemesisMovesToAnotherClanAtTheNextActInASlotOfTheSamePosition()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);

        var next = NemesisSystem.TransferOnActEntry(state, Nemesis, act: 2);

        var moved = Assert.Single(next.RivalMemory.Nemeses);
        Assert.NotEqual(Clan, moved.ClanId);
        Assert.Equal(Position.Midfielder, moved.Position);
        var target = Rivals.OfClan(moved.ClanId, 2)!;
        Assert.Equal(Position.Midfielder, target.Players[moved.Slot].Position);
        Assert.InRange(moved.Slot, 0, 6);
        // El puesto de origen lo cubre ya un fichaje y el de destino no tiene a quien lo ocupaba.
        Assert.NotNull(next.RivalMemory.VacancyAt(Clan, 3));
        Assert.NotNull(next.RivalMemory.VacancyAt(moved.ClanId, moved.Slot));
        // Juega en su clan nuevo, con su nombre y un nivel más.
        var built = RivalTeamBuilder.Build(target, Catalog, next.RivalMemory, state.Seed, Nemesis.LevelBonus);
        Assert.Equal(moved.Name, built.Players[moved.Slot].Name);
        Assert.Equal(target.Players[moved.Slot].Level + Nemesis.LevelBonus, built.Players[moved.Slot].Level);
        // Y ya no juega en el de origen.
        var home = RivalTeamBuilder.Build(Rivals.OfClan(Clan, 2)!, Catalog, next.RivalMemory, state.Seed, Nemesis.LevelBonus);
        Assert.DoesNotContain(home.Players, p => p.Name == moved.Name);
    }

    [Fact]
    public void TheTransferIsDeterministicAndTakesTheWeakestStarterOfThePosition()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);

        var a = NemesisSystem.TransferOnActEntry(state, Nemesis, 2).RivalMemory.Nemeses[0];
        var b = NemesisSystem.TransferOnActEntry(state, Nemesis, 2).RivalMemory.Nemeses[0];

        Assert.Equal(a, b);
        var target = Rivals.OfClan(a.ClanId, 2)!;
        var sameRole = Enumerable.Range(0, 7).Where(i => target.Players[i].Position == a.Position).ToList();
        int minLevel = sameRole.Min(i => target.Players[i].Level);
        Assert.Equal(minLevel, target.Players[a.Slot].Level);
    }

    [Fact]
    public void TwoNemesesDoNotEndUpInTheSameSlotAndADeadOneDoesNotMove()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3, id: 1);
        state = WithNemesis(state, Clan, slot: 4, id: 2, title: "bill_collector");
        var slain = state.RivalMemory.Find(2)! with { Status = NemesisStatus.Slain };
        var both = state;
        var moved = NemesisSystem.TransferOnActEntry(both.WithRivalMemory(both.RivalMemory.WithNemesis(slain)), Nemesis, 2);

        Assert.Equal(Clan, moved.RivalMemory.Find(2)!.ClanId);
        Assert.NotEqual(Clan, moved.RivalMemory.Find(1)!.ClanId);

        var alive = WithNemesis(WithNemesis(BaseState(), Clan, 3, 1), Clan, 4, 2, "bill_collector");
        var movedBoth = NemesisSystem.TransferOnActEntry(alive, Nemesis, 2).RivalMemory.Alive;
        Assert.Equal(2, movedBoth.Count);
        Assert.NotEqual((movedBoth[0].ClanId, movedBoth[0].Slot), (movedBoth[1].ClanId, movedBoth[1].Slot));
    }

    [Fact]
    public void WinningTheBossHandsTheNemesisOverThroughTheEngine()
    {
        var state = BaseState();
        var first = Play(state, new[] { Death(0, state.Roster[2].Id, RivalId(3)) }).State;
        Assert.Equal(Clan, first.RivalMemory.Nemeses[0].ClanId);

        var next = NemesisSystem.TransferOnActEntry(first.WithAct(2), Nemesis, 2);

        Assert.Equal(2, next.Act);
        Assert.NotEqual(Clan, next.RivalMemory.Nemeses[0].ClanId);
    }

    // ------------------------------------------------------------------ guardado y determinismo

    [Fact]
    public void RivalMemoryAndRevengesSurviveTheSaveRoundTrip()
    {
        var state = WithNemesis(BaseState(), Clan, slot: 3);
        state = Play(state, new[]
        {
            Death(1, RivalId(6), state.Roster[0].Id, tick: 30),
            Injury(1, RivalId(3), state.Roster[1].Id, tick: 40),
        }).State;
        state = NemesisSystem.TransferOnActEntry(state.WithAct(2), Nemesis, 2);

        string json = RunSave.Save(state);
        var loaded = RunSave.Load(json);

        Assert.Equal(9, loaded.SchemaVersion);
        Assert.Equal(state.RivalMemory.Nemeses, loaded.RivalMemory.Nemeses);
        Assert.Equal(state.RivalMemory.Vacancies, loaded.RivalMemory.Vacancies);
        Assert.Equal(state.FindPlayer(state.Roster[1].Id)!.Career.Revenges, loaded.FindPlayer(state.Roster[1].Id)!.Career.Revenges);
        Assert.Equal(json, RunSave.Save(loaded));
    }

    [Fact]
    public void ASaveFromVersionSevenIsRejectedExplicitly()
    {
        string old = RunSave.Save(BaseState()).Replace($"\"schemaVersion\":{RunSave.SchemaVersion}", "\"schemaVersion\":7", StringComparison.Ordinal);

        var error = Assert.Throws<RunSaveException>(() => RunSave.Load(old));
        Assert.Equal("$.schemaVersion", error.JsonPath);
    }

    [Fact]
    public void ResolvingTheSameMatchTwiceGivesByteIdenticalStates()
    {
        var state = BaseState();
        var events = new[]
        {
            Death(0, state.Roster[2].Id, RivalId(3), tick: 60),
            Death(1, RivalId(5), state.Roster[0].Id, tick: 70),
        };

        var a = RunSave.Save(Play(state, events).State);
        var b = RunSave.Save(Play(state, events).State);

        Assert.Equal(a, b);
    }

    [Fact]
    public void WithoutANemesisCatalogTheMemoryIsNeverTouched()
    {
        var state = BaseState();
        var own = state.Roster.Take(7).ToList();

        var applied = MatchResolution.Apply(
            state, Node(), LineupFor(own), ResultWith(own, new[] { Death(0, state.Roster[2].Id, RivalId(3)) }), Catalog);

        Assert.Empty(applied.State.RivalMemory.Nemeses);
        Assert.Empty(applied.Summary.NemesesMade);
    }
}
