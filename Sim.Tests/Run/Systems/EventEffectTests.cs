using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Events;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.View;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0159 y su revisión independiente: cada efecto nuevo de evento hace exactamente lo que declara, con
/// su resultado exacto. Las cartas se construyen en memoria (un <see cref="EventCatalog"/> de una sola carta
/// con <c>minAct</c> 1: <c>EventSystem.Card</c> no tiene entonces más remedio que sacarla) para no depender
/// de qué carta real toque en qué nodo ni de cifras de <c>data/events/</c> que cambian con el balance. Lo que
/// sí es real es el resto: el catálogo, los objetos, los consumibles, los árbitros y la plantilla de la run.
/// Además se comprueba que la vista (<see cref="EventView"/>) y <see cref="EventSystem.Choose"/> dicen lo
/// mismo (RF-012d: lo que se enseña como pulsable se puede elegir, y sólo eso), y que una carta se elige una vez.
/// </summary>
public sealed class EventEffectTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    private static ItemCatalog Items => SystemsTestSupport.Systems.Items;

    private static ConsumableCatalog Consumables => SystemsTestSupport.Systems.Consumables;

    private static LocalizedName Name(string text) => new(text, text);

    private static EventOption Option(string id, bool needsTarget, bool needsSecond, params EventEffect[] effects) =>
        new(id, Name(id), effects, needsTarget, needsSecond);

    private static EventCatalog CardOf(params EventOption[] options) => new(new[]
    {
        new EventCard("test_card", Name("test_card"), Name("test_card"), 1, 1, options),
    });

    private static (RunState State, MapNode Node) AtAnEvent(ulong seed = 5001UL, int gold = 100, int skip = 0)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, SystemsTestSupport.Systems).WithGold(gold);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Event, skip);
        return (state, state.GetNode(state.PendingNodeId));
    }

    private static RunState Choose(RunState state, EventCatalog events, ChooseEventOption decision) =>
        EventSystem.Choose(
            state, decision, events, Items, Consumables, SystemsTestSupport.Systems.Economy, Catalog);

    private static EventScreenView View(RunState state, EventCatalog events) =>
        EventView.Build(state, Catalog, events, Items, Consumables, "es")!;

    private static RunPlayer FirstAvailable(RunState state) => state.Roster.First(p => p.IsAvailable);

    /// <summary>La plantilla sin perks ni objetos: un escenario donde lo único que hay es lo que el test pone.</summary>
    private static RunState Bare(RunState state) =>
        state.WithRoster(state.Roster.Select(p => p with { Perks = Array.Empty<string>(), Item = null }));

    // ------------------------------------------------------------------ grantItem

    [Theory]
    [InlineData(Rarity.Common)]
    [InlineData(Rarity.Uncommon)]
    [InlineData(Rarity.Rare)]
    [InlineData(Rarity.Legendary)]
    public void GrantItemPutsAnItemOfThatRarityInTheStoreAndIsDeterministic(Rarity rarity)
    {
        var (state, node) = AtAnEvent();
        var events = CardOf(Option("give", false, false, new EventEffect(EventEffectKind.GrantItem, 0, Rarity: rarity)));
        var pool = Items.OfferableTo(state.ClubRace, node.Act).Where(i => i.Rarity == rarity).ToList();

        if (pool.Count == 0)
        {
            // Sin objeto de esa rareza en este acto la opción no tiene dónde aterrizar: ni se ofrece ni se elige.
            Assert.False(View(state, events).Options[0].Affordable);
            Assert.Throws<InvalidOperationException>(() => Choose(state, events, new ChooseEventOption(0)));
            return;
        }

        var before = state.StoredItems;
        var first = Choose(state, events, new ChooseEventOption(0));
        var second = Choose(state, events, new ChooseEventOption(0));

        Assert.Equal(before.Count + 1, first.StoredItems.Count);
        string added = Assert.Single(first.StoredItems.Except(before).Distinct());
        Assert.Contains(pool, i => i.Id == added);
        Assert.Equal(rarity, Items.Get(added).Rarity);
        Assert.Equal(first.StoredItems, second.StoredItems);
    }

    [Fact]
    public void GrantItemCommonAlwaysHasACandidateInTheFirstAct()
    {
        // Instrumento (Regla J): sin esto, el caso «pool vacío» de arriba podría estar tragándose todas las rarezas.
        var (state, node) = AtAnEvent();
        Assert.Contains(Items.OfferableTo(state.ClubRace, node.Act), i => i.Rarity == Rarity.Common);
    }

    // ------------------------------------------------------------------ grantConsumable

    [Theory]
    [InlineData(ConsumableFamily.Medical)]
    [InlineData(ConsumableFamily.Tactical)]
    [InlineData(ConsumableFamily.Dirty)]
    [InlineData(ConsumableFamily.Supernatural)]
    public void GrantConsumableAddsOneOfThatFamily(ConsumableFamily family)
    {
        var (state, _) = AtAnEvent();
        var events = CardOf(Option("give", false, false, new EventEffect(EventEffectKind.GrantConsumable, 0, Family: family)));

        var after = Choose(state, events, new ChooseEventOption(0));

        // ADR 0172: llega a un hueco libre, ya equipado y manual (un clic en el partido).
        var granted = Assert.Single(after.Consumables);
        Assert.Equal(ConsumableMode.Manual, granted.Mode);
        Assert.Equal(family, Consumables.Find(granted.Id)!.Family);
        Assert.Equal(granted, Assert.Single(Choose(state, events, new ChooseEventOption(0)).Consumables));
    }

    [Fact]
    public void GrantConsumableIsNotViableWithoutAFreeSlotAndNeverRepeatsOne()
    {
        // ADR 0172: sin hueco libre no hay dónde aterrizar (la opción no es viable, como el canterano sin
        // plantilla), y el sorteo no ofrece el que ya se lleva.
        var (state, node) = AtAnEvent();
        var option = Option("give", false, false, new EventEffect(EventEffectKind.GrantConsumable, 0, Family: ConsumableFamily.Dirty));
        var events = CardOf(option);

        Assert.True(EventSystem.IsViable(state, Catalog, node, option, Items, Consumables));
        Assert.True(View(state, events).Options[0].Affordable);

        var granted = Assert.Single(Choose(state, events, new ChooseEventOption(0)).Consumables).Id;
        var withOne = state.WithTakenConsumable(granted);
        var second = Assert.Single(Choose(withOne, events, new ChooseEventOption(0)).Consumables, c => c.Id != granted);
        Assert.NotEqual(granted, second.Id);

        var full = withOne.WithTakenConsumable(second.Id);
        Assert.False(EventSystem.IsViable(full, Catalog, node, option, Items, Consumables));
        Assert.False(View(full, events).Options[0].Affordable);
        Assert.True(View(full, events).Options[0].NoConsumableSlot);
        Assert.False(View(state, events).Options[0].NoConsumableSlot);
        Assert.ThrowsAny<Exception>(() => Choose(full, events, new ChooseEventOption(0)));
    }

    // ------------------------------------------------------------------ grantTrait / removeTrait

    [Fact]
    public void GrantTraitAddsTheTraitToTheChosenPlayerOnly()
    {
        var (state, _) = AtAnEvent();
        var target = FirstAvailable(state);
        state = state.WithPlayer(target with { Traits = new[] { Trait.Aggressive } });
        var events = CardOf(Option("give", true, false, new EventEffect(EventEffectKind.GrantTrait, 0, Trait: Trait.Dirty)));

        var after = Choose(state, events, new ChooseEventOption(0, target.Id));

        Assert.Equal(new[] { Trait.Aggressive, Trait.Dirty }, after.GetPlayer(target.Id).Traits);
        foreach (var other in state.Roster.Where(p => p.Id != target.Id))
        {
            Assert.Equal(other.Traits, after.GetPlayer(other.Id).Traits);
        }
    }

    [Fact]
    public void GrantTraitIsNotEligibleWithThreeTraitsOrWhenAlreadyHeld()
    {
        var (state, _) = AtAnEvent();
        var full = FirstAvailable(state);
        var other = state.Roster.First(p => p.IsAvailable && p.Id != full.Id);
        state = state.WithPlayer(full with { Traits = new[] { Trait.Aggressive, Trait.Leader, Trait.Fast } })
            .WithPlayer(other with { Traits = new[] { Trait.Dirty } });
        var events = CardOf(Option("give", true, false, new EventEffect(EventEffectKind.GrantTrait, 0, Trait: Trait.Dirty)));

        // Con tres rasgos (RF-022c) no cabe otro; con el rasgo ya puesto, tampoco.
        Assert.Throws<ArgumentException>(() => Choose(state, events, new ChooseEventOption(0, full.Id)));
        Assert.Throws<ArgumentException>(() => Choose(state, events, new ChooseEventOption(0, other.Id)));

        var view = View(state, events).Options[0];
        Assert.DoesNotContain(view.Targets, t => t.PlayerId == full.Id || t.PlayerId == other.Id);
        Assert.True(view.Affordable, "el resto de la plantilla sigue pudiendo recibirlo");

        // Y si nadie puede recibirlo, la opción entera deja de ser pulsable.
        var everyoneFull = state.WithRoster(state.Roster.Select(p =>
            p with { Traits = new[] { Trait.Aggressive, Trait.Leader, Trait.Fast } }));
        Assert.False(View(everyoneFull, events).Options[0].Affordable);
    }

    [Fact]
    public void RemoveTraitTakesOnlyThatTraitAndNeedsHavingIt()
    {
        var (state, _) = AtAnEvent();
        var target = FirstAvailable(state);
        var other = state.Roster.First(p => p.IsAvailable && p.Id != target.Id);
        state = state.WithPlayer(target with { Traits = new[] { Trait.Aggressive, Trait.Leader } })
            .WithPlayer(other with { Traits = new[] { Trait.Leader } });
        var events = CardOf(Option("take", true, false, new EventEffect(EventEffectKind.RemoveTrait, 0, Trait: Trait.Aggressive)));

        var after = Choose(state, events, new ChooseEventOption(0, target.Id));

        Assert.Equal(new[] { Trait.Leader }, after.GetPlayer(target.Id).Traits);
        Assert.Throws<ArgumentException>(() => Choose(state, events, new ChooseEventOption(0, other.Id)));
        Assert.DoesNotContain(View(state, events).Options[0].Targets, t => t.PlayerId == other.Id);
    }

    // ------------------------------------------------------------------ attribute

    [Fact]
    public void AttributeRaisesTheChosenAttributeAndRespectsTheCap()
    {
        var (state, _) = AtAnEvent();
        var target = FirstAvailable(state);
        state = state.WithPlayer(target with { Attributes = new Attributes(50, 50, 50, 50, 40) });
        var events = CardOf(Option("train", true, false, new EventEffect(EventEffectKind.Attribute, 6, Attribute: AttributeKind.Speed)));

        var after = Choose(state, events, new ChooseEventOption(0, target.Id)).GetPlayer(target.Id);

        Assert.Equal(new Attributes(50, 56, 50, 50, 40), after.Attributes);

        // 95 + 6 = 101: tope en 99.
        state = state.WithPlayer(target with { Attributes = new Attributes(50, 95, 50, 50, 40) });
        after = Choose(state, events, new ChooseEventOption(0, target.Id)).GetPlayer(target.Id);
        Assert.Equal(99, after.Attributes.Speed);

        // Y quien ya está en 99 no es candidato.
        state = state.WithPlayer(target with { Attributes = new Attributes(50, 99, 50, 50, 40) });
        Assert.Throws<ArgumentException>(() => Choose(state, events, new ChooseEventOption(0, target.Id)));
        Assert.DoesNotContain(View(state, events).Options[0].Targets, t => t.PlayerId == target.Id);
    }

    [Fact]
    public void AttributePenaltyLowersAndNeverGoesBelowTheFloor()
    {
        var (state, _) = AtAnEvent();
        var target = FirstAvailable(state);
        state = state.WithPlayer(target with { Attributes = new Attributes(50, 50, 4, 50, 40) });
        var events = CardOf(Option("dull", true, false, new EventEffect(EventEffectKind.Attribute, -3, Attribute: AttributeKind.Technique)));

        Assert.Equal(1, Choose(state, events, new ChooseEventOption(0, target.Id)).GetPlayer(target.Id).Attributes.Technique);
    }

    // ------------------------------------------------------------------ level

    [Fact]
    public void LevelDropsOneLevelAndLeavesExperienceAtTheMinimumOfTheNewLevel()
    {
        var (state, _) = AtAnEvent();
        var target = FirstAvailable(state);
        var tuning = Catalog.Progression;
        int perLevel = tuning.AttributesPerLevel;
        state = state.WithPlayer(target with
        {
            Level = 4,
            Experience = ProgressionRules.MinExperienceForLevel(4, tuning) + 37,
            Attributes = new Attributes(60, 60, 60, 60, 40),
        });
        var events = CardOf(Option("curse", true, false, new EventEffect(EventEffectKind.Level, 1)));

        var after = Choose(state, events, new ChooseEventOption(0, target.Id)).GetPlayer(target.Id);

        Assert.Equal(3, after.Level);
        Assert.Equal(ProgressionRules.MinExperienceForLevel(3, tuning), after.Experience);
        Assert.Equal(3, ProgressionRules.LevelFor(after.Experience, tuning));
        Assert.Equal(new Attributes(60 - perLevel, 60 - perLevel, 60 - perLevel, 60 - perLevel, 40), after.Attributes);
    }

    [Fact]
    public void LevelIsNotEligibleAtLevelOne()
    {
        var (state, _) = AtAnEvent();
        var target = FirstAvailable(state);
        state = state.WithRoster(state.Roster.Select(p => p with { Level = 1, Experience = 0 }));
        var events = CardOf(Option("curse", true, false, new EventEffect(EventEffectKind.Level, 1)));

        Assert.Throws<ArgumentException>(() => Choose(state, events, new ChooseEventOption(0, target.Id)));
        Assert.False(View(state, events).Options[0].Affordable);

        // Con uno de nivel 2 vuelve a ser posible, y sólo con él.
        state = state.WithPlayer(target with { Level = 2, Experience = 100 });
        var row = View(state, events).Options[0];
        Assert.True(row.Affordable);
        Assert.Equal(target.Id, Assert.Single(row.Targets).PlayerId);
    }

    /// <summary>
    /// Regla I: perder un nivel y ganar menos experiencia de la que falta para el umbral NO debe devolver el
    /// nivel (la pérdida se deshacía sola cuando <c>MinExperienceForLevel</c> estaba desplazado un nivel).
    /// </summary>
    [Fact]
    public void ALostLevelIsNotRecoveredByGainingLessExperienceThanTheThresholdNeeds()
    {
        var (state, _) = AtAnEvent();
        var target = FirstAvailable(state);
        var tuning = Catalog.Progression;
        int threshold = ProgressionRules.MinExperienceForLevel(4, tuning);
        int minOfThree = ProgressionRules.MinExperienceForLevel(3, tuning);
        state = state.WithPlayer(target with { Level = 4, Experience = threshold + 10 });

        int missing = threshold - minOfThree;
        var short_ = CardOf(Option("both", true, false,
            new EventEffect(EventEffectKind.Level, 1),
            new EventEffect(EventEffectKind.ExperienceTarget, missing - 1)));
        var exact = CardOf(Option("both", true, false,
            new EventEffect(EventEffectKind.Level, 1),
            new EventEffect(EventEffectKind.ExperienceTarget, missing)));

        var stays = Choose(state, short_, new ChooseEventOption(0, target.Id)).GetPlayer(target.Id);
        Assert.Equal(3, stays.Level);
        Assert.Equal(threshold - 1, stays.Experience);
        Assert.Equal(3, ProgressionRules.LevelFor(stays.Experience, tuning));

        // Control positivo del instrumento: con lo justo, sí vuelve a subir.
        var climbs = Choose(state, exact, new ChooseEventOption(0, target.Id)).GetPlayer(target.Id);
        Assert.Equal(4, climbs.Level);
        Assert.Equal(threshold, climbs.Experience);
    }

    // ------------------------------------------------------------------ refereeGrudge

    [Fact]
    public void RefereeGrudgeMovesTheMemoryOfTheCardsRefereeAndIsCapped()
    {
        var (state, node) = AtAnEvent();
        Assert.NotEmpty(state.Referees);
        var referee = EventSystem.ReferenceReferee(state, node)!;
        int cap = Catalog.Tuning.Referee.Memory.MemoryCap;
        var up = CardOf(Option("dinner", false, false, new EventEffect(EventEffectKind.RefereeGrudge, 20)));
        var down = CardOf(Option("insult", false, false, new EventEffect(EventEffectKind.RefereeGrudge, -15)));

        var start = state.WithReferees(state.Referees.Select(r => r with { Memory = 0 }));
        var raised = Choose(start, up, new ChooseEventOption(0));
        var lowered = Choose(start, down, new ChooseEventOption(0));

        foreach (var r in raised.Referees)
        {
            Assert.Equal(r.Id == referee.Id ? 20 : 0, r.Memory);
        }

        foreach (var r in lowered.Referees)
        {
            Assert.Equal(r.Id == referee.Id ? -15 : 0, r.Memory);
        }

        // Acotada a ±memoryCap, en los dos sentidos.
        var high = start.WithReferees(start.Referees.Select(r => r.Id == referee.Id ? r with { Memory = cap - 5 } : r));
        Assert.Equal(cap, Choose(high, up, new ChooseEventOption(0)).Referees.First(r => r.Id == referee.Id).Memory);
        var low = start.WithReferees(start.Referees.Select(r => r.Id == referee.Id ? r with { Memory = -cap + 5 } : r));
        Assert.Equal(-cap, Choose(low, down, new ChooseEventOption(0)).Referees.First(r => r.Id == referee.Id).Memory);
    }

    // ------------------------------------------------------------------ recruit

    /// <summary>
    /// BA-G, ADR 0169: el canterano que da un evento tampoco se llama como nadie de la plantilla. Se fuerza el choque: alguien
    /// de la plantilla pasa a llevar el nombre que el canterano habría llevado, y el canterano cambia de nombre y de nada más.
    /// </summary>
    [Fact]
    public void RecruitedYouthNeverRepeatsARosterNameAndOnlyItsNameChanges()
    {
        var (state, _) = AtAnEvent();
        var events = CardOf(Option("welcome", false, false, new EventEffect(EventEffectKind.Recruit, 0)));
        var free = Choose(state, events, new ChooseEventOption(0)).Roster.Single(p => state.Roster.All(o => o.Id != p.Id));

        var clash = state.WithPlayer(state.Roster[2] with { Name = free.Name });
        var moved = Choose(clash, events, new ChooseEventOption(0)).Roster.Single(p => clash.Roster.All(o => o.Id != p.Id));

        Assert.NotEqual(free.Name, moved.Name);
        TestRuns.AssertSamePlayer(free, moved with { Name = free.Name });
    }

    [Fact]
    public void RecruitAddsAYouthWhenThereIsRoomAndIsNotViableWithAFullRoster()
    {
        var (state, _) = AtAnEvent();
        Assert.True(state.HasRosterSpace, "instrumento: la plantilla inicial tiene hueco");
        var events = CardOf(Option("welcome", false, false, new EventEffect(EventEffectKind.Recruit, 0)));

        var after = Choose(state, events, new ChooseEventOption(0));

        Assert.Equal(state.Roster.Count + 1, after.Roster.Count);
        var added = after.Roster.Single(p => state.Roster.All(o => o.Id != p.Id));
        Assert.True(added.IsYouth);
        Assert.True(added.IsAvailable);
        Assert.NotEqual(Position.Goalkeeper, added.Position);
        var again = Choose(state, events, new ChooseEventOption(0)).Roster.Single(p => state.Roster.All(o => o.Id != p.Id));
        Assert.Equal((added.Id, added.Name, added.Position, added.Attributes), (again.Id, again.Name, again.Position, again.Attributes));

        var full = state;
        while (full.HasRosterSpace)
        {
            full = full.WithNewPlayer(full.Roster[0] with { Id = -1, Name = "Relleno" });
        }

        Assert.False(View(full, events).Options[0].Affordable);
        Assert.Throws<InvalidOperationException>(() => Choose(full, events, new ChooseEventOption(0)));
    }

    // ------------------------------------------------------------------ sacrifice

    private sealed record SacrificeScenario(RunState State, RunPlayer Victim, string PerkId, IReadOnlyList<int> Heirs);

    /// <summary>
    /// Busca en el catálogo real una víctima titular (no portero) y un perk que un compañero pueda heredar, con la
    /// plantilla sin más perks para que el heredero sea inequívoco. <paramref name="perkFilter"/> restringe el perk.
    /// </summary>
    private static SacrificeScenario FindSacrifice(RunState state, Func<PerkDefinition, bool> perkFilter)
    {
        state = Bare(state);
        var starters = state.Lineup.Slots.Select(s => s.PlayerId).ToHashSet();
        foreach (var victim in state.Roster.Where(p => p.IsAvailable && p.Position != Position.Goalkeeper && starters.Contains(p.Id)))
        {
            foreach (var perk in Catalog.Perks.All.Where(perkFilter))
            {
                var carrier = victim with { Perks = new[] { perk.Id } };
                var scenario = state.WithPlayer(carrier);
                var heirs = EventSystem.Heirs(scenario, Catalog, carrier, perk.Id);
                if (heirs.Count > 0)
                {
                    return new SacrificeScenario(scenario, carrier, perk.Id, heirs);
                }
            }
        }

        throw new InvalidOperationException("ningún perk del catálogo tiene un heredero posible en esta plantilla");
    }

    private static EventCatalog SacrificeCard() =>
        CardOf(Option("sacrifice", true, true, new EventEffect(EventEffectKind.Sacrifice, 0)));

    [Fact]
    public void SacrificeKillsTheVictimRemovesItFromTheLineupAndPassesThePerk()
    {
        var (state, _) = AtAnEvent(5002UL);
        var found = FindSacrifice(state, _ => true);
        var itemId = Items.All.First(i => !i.IsRelic).Id;
        var scenario = found.State.WithPlayer(found.Victim with { Item = itemId });
        var relic = Items.FindRelic(RelicSystem.Classify(found.Victim.Career))!;
        int heirId = found.Heirs[0];
        var events = SacrificeCard();

        Assert.Contains(scenario.Lineup.Slots, s => s.PlayerId == found.Victim.Id);
        Assert.True(scenario.AvailablePlayerCount > RunRules.MinimumAvailablePlayers);

        var after = Choose(scenario, events, new ChooseEventOption(0, found.Victim.Id, heirId));

        Assert.Equal(PhysicalState.Dead, after.GetPlayer(found.Victim.Id).PhysicalState);
        Assert.Equal(scenario.AvailablePlayerCount - 1, after.AvailablePlayerCount);
        Assert.DoesNotContain(after.Lineup.Slots, s => s.PlayerId == found.Victim.Id);
        Assert.Null(after.GetPlayer(found.Victim.Id).Item);
        Assert.Equal(scenario.StockOf(itemId) + 1, after.StockOf(itemId));
        Assert.Equal(scenario.StockOf(relic.Id) + 1, after.StockOf(relic.Id));
        Assert.Contains(found.PerkId, after.GetPlayer(heirId).Perks);
        foreach (var other in scenario.Roster.Where(p => p.Id != found.Victim.Id && p.Id != heirId))
        {
            Assert.Equal(other.Perks, after.GetPlayer(other.Id).Perks);
        }
    }

    [Fact]
    public void SacrificeOfAMercenaryLeavesNoRelic()
    {
        var (state, _) = AtAnEvent(5002UL);
        var found = FindSacrifice(state, _ => true);
        var scenario = found.State.WithPlayer(found.Victim with { IsMercenary = true });
        var relic = Items.FindRelic(RelicSystem.Classify(found.Victim.Career))!;

        var after = Choose(scenario, SacrificeCard(), new ChooseEventOption(0, found.Victim.Id, found.Heirs[0]));

        Assert.Equal(PhysicalState.Dead, after.GetPlayer(found.Victim.Id).PhysicalState);
        Assert.Equal(scenario.StockOf(relic.Id), after.StockOf(relic.Id));
    }

    [Fact]
    public void SacrificeDoesNotPassADuplicatePerk()
    {
        var (state, _) = AtAnEvent(5002UL);
        var found = FindSacrifice(state, _ => true);
        int heirId = found.Heirs[0];
        var heir = found.State.GetPlayer(heirId);
        var scenario = found.State.WithPlayer(heir with { Perks = new[] { found.PerkId } });
        var events = SacrificeCard();

        Assert.DoesNotContain(heirId, EventSystem.Heirs(scenario, Catalog, found.Victim, found.PerkId));
        Assert.Throws<ArgumentException>(() => Choose(scenario, events, new ChooseEventOption(0, found.Victim.Id, heirId)));
        Assert.DoesNotContain(
            View(scenario, events).Options[0].SecondTargets,
            t => t.PlayerId == heirId && t.ForFirstPlayerId == found.Victim.Id);
    }

    [Fact]
    public void SacrificeDoesNotPassAPerkToAnotherPosition()
    {
        var (state, _) = AtAnEvent(5002UL);
        var found = FindSacrifice(state, p => p.PositionOnly is { } only && only != Position.Goalkeeper);
        var required = Catalog.Perks.Find(found.PerkId)!.PositionOnly!.Value;
        var events = SacrificeCard();

        Assert.All(found.Heirs, id => Assert.Equal(required, found.State.GetPlayer(id).Position));
        var wrongPosition = found.State.Roster.First(p =>
            p.IsAvailable && p.Id != found.Victim.Id && p.Position != required);
        Assert.Throws<ArgumentException>(() =>
            Choose(found.State, events, new ChooseEventOption(0, found.Victim.Id, wrongPosition.Id)));

        var ok = Choose(found.State, events, new ChooseEventOption(0, found.Victim.Id, found.Heirs[0]));
        Assert.Contains(found.PerkId, ok.GetPlayer(found.Heirs[0]).Perks);
    }

    [Fact]
    public void SacrificeIsNotViableWithOnlyFiveAvailablePlayers()
    {
        var (state, _) = AtAnEvent(5002UL);
        var found = FindSacrifice(state, _ => true);
        var events = SacrificeCard();

        // Exactamente el mínimo (RF-002b): todos los demás fuera de combate.
        var keep = new[] { found.Victim.Id, found.Heirs[0] };
        int available = 0;
        var roster = new List<RunPlayer>();
        foreach (var p in found.State.Roster)
        {
            bool stays = keep.Contains(p.Id) || (available < RunRules.MinimumAvailablePlayers && p.IsAvailable);
            if (stays && p.IsAvailable)
            {
                available++;
            }

            roster.Add(stays ? p : p with { PhysicalState = PhysicalState.SevereInjury });
        }

        var tight = found.State.WithRoster(roster);
        Assert.Equal(RunRules.MinimumAvailablePlayers, tight.AvailablePlayerCount);

        Assert.False(View(tight, events).Options[0].Affordable);
        Assert.ThrowsAny<Exception>(() =>
            Choose(tight, events, new ChooseEventOption(0, found.Victim.Id, found.Heirs[0])));

        // Control positivo del instrumento: con uno más de margen vuelve a ser posible.
        var spare = tight.Roster.First(p => !p.IsAvailable);
        var loose = tight.WithPlayer(spare with { PhysicalState = PhysicalState.Healthy });
        Assert.True(View(loose, events).Options[0].Affordable);
    }

    // ------------------------------------------------------------------ segundo objetivo

    [Fact]
    public void TheSecondTargetMustBeDifferentAndAvailable()
    {
        var (state, _) = AtAnEvent();
        var available = state.Roster.Where(p => p.IsAvailable).Take(3).ToList();
        var first = available[0];
        var second = available[1];
        var hurt = available[2];
        state = state.WithPlayer(hurt with { PhysicalState = PhysicalState.SevereInjury })
            .WithPlayer(first with { Traits = Array.Empty<Trait>() });
        var events = CardOf(Option("brawl", true, true,
            new EventEffect(EventEffectKind.GrantTrait, 0, Trait: Trait.Leader),
            new EventEffect(EventEffectKind.Injure, 1, UsesSecondTarget: true)));

        var after = Choose(state, events, new ChooseEventOption(0, first.Id, second.Id));
        Assert.Contains(Trait.Leader, after.GetPlayer(first.Id).Traits);
        Assert.Equal(PhysicalState.Healthy, after.GetPlayer(first.Id).PhysicalState);
        Assert.Equal(PhysicalState.MinorInjury, after.GetPlayer(second.Id).PhysicalState);
        Assert.DoesNotContain(Trait.Leader, after.GetPlayer(second.Id).Traits);

        Assert.Throws<ArgumentException>(() => Choose(state, events, new ChooseEventOption(0, first.Id, first.Id)));
        Assert.Throws<ArgumentException>(() => Choose(state, events, new ChooseEventOption(0, first.Id, hurt.Id)));
        Assert.Throws<ArgumentException>(() => Choose(state, events, new ChooseEventOption(0, first.Id)));
        Assert.ThrowsAny<Exception>(() => Choose(state, events, new ChooseEventOption(0, first.Id, 9999)));
    }

    // ------------------------------------------------------------------ la vista coincide con Choose

    private static IEnumerable<(string Label, RunState State)> Variants()
    {
        for (int skip = 0; skip < 2; skip++)
        {
            var (rich, _) = AtAnEvent(4001UL, gold: 100, skip: skip);
            yield return ($"oro 100, nodo {skip}", rich);
            yield return ($"sin oro, nodo {skip}", rich.WithGold(0));

            yield return ($"cinco disponibles, nodo {skip}", TightRoster(rich));

            yield return ($"nadie elegible, nodo {skip}", rich.WithRoster(rich.Roster.Select(p => p with
            {
                Level = 1,
                Experience = 0,
                Traits = new[] { Trait.Aggressive, Trait.Leader, Trait.Fast },
                Attributes = new Attributes(99, 99, 99, 99, 40),
            })));

            var full = rich;
            while (full.HasRosterSpace)
            {
                full = full.WithNewPlayer(full.Roster[0] with { Id = -1, Name = "Relleno" });
            }

            yield return ($"plantilla llena, nodo {skip}", full);
            yield return ($"nivel alto y perks, nodo {skip}", rich.WithRoster(rich.Roster.Select(p => p with
            {
                Level = 4,
                Experience = 500,
                Traits = new[] { Trait.Dirty },
            })));
        }
    }

    private static RunState TightRoster(RunState state)
    {
        int available = 0;
        var roster = new List<RunPlayer>();
        foreach (var p in state.Roster)
        {
            if (p.IsAvailable && available < RunRules.MinimumAvailablePlayers)
            {
                available++;
                roster.Add(p);
            }
            else
            {
                roster.Add(p with { PhysicalState = PhysicalState.SevereInjury });
            }
        }

        return state.WithRoster(roster);
    }

    private static bool Throws(Func<RunState> action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    [Fact]
    public void TheViewAgreesWithChooseForEveryRealCard()
    {
        int clickableWithTarget = 0;
        int clickableSecondTarget = 0;
        int blocked = 0;
        foreach (var real in SystemsTestSupport.Systems.Events.All)
        {
            var events = new EventCatalog(new[] { real with { MinAct = 1 } });
            foreach (var (label, state) in Variants())
            {
                var view = View(state, events);
                Assert.False(view.Resolved);
                for (int i = 0; i < real.Options.Count; i++)
                {
                    var option = real.Options[i];
                    var row = view.Options[i];
                    string where = $"carta '{real.Id}', opción '{option.Id}', {label}";

                    var firsts = option.NeedsTarget ? state.Roster.Select(p => p.Id).ToList() : new List<int> { -1 };
                    var seconds = option.NeedsSecondTarget ? state.Roster.Select(p => p.Id).ToList() : new List<int> { -1 };
                    var accepted = new List<(int, int)>();
                    foreach (int f in firsts)
                    {
                        foreach (int s in seconds)
                        {
                            if (!Throws(() => Choose(state, events, new ChooseEventOption(i, f, s))))
                            {
                                accepted.Add((f, s));
                            }
                        }
                    }

                    if (!row.Affordable)
                    {
                        blocked++;
                        Assert.True(accepted.Count == 0, $"la vista no la marca pulsable pero Choose la acepta: {where}: {string.Join(",", accepted)}");
                        continue;
                    }

                    Assert.True(accepted.Count > 0, $"la vista la marca pulsable pero Choose la rechaza siempre: {where}");
                    clickableWithTarget += option.NeedsTarget ? 1 : 0;
                    clickableSecondTarget += option.NeedsSecondTarget ? 1 : 0;

                    // Las parejas que enseña la vista son exactamente las que Choose acepta.
                    var shownFirst = option.NeedsTarget ? row.Targets.Select(t => t.PlayerId).ToList() : new List<int> { -1 };
                    var expected = new List<(int, int)>();
                    foreach (int f in shownFirst)
                    {
                        if (!option.NeedsSecondTarget)
                        {
                            expected.Add((f, -1));
                            continue;
                        }

                        foreach (var t in row.SecondTargets)
                        {
                            if (t.PlayerId != f && (t.ForFirstPlayerId == -1 || t.ForFirstPlayerId == f))
                            {
                                expected.Add((f, t.PlayerId));
                            }
                        }
                    }

                    Assert.True(
                        expected.Distinct().OrderBy(x => x).SequenceEqual(accepted.OrderBy(x => x)),
                        $"la vista y Choose no coinciden en los objetivos: {where}. Vista: {string.Join(",", expected.Distinct().OrderBy(x => x))}; Choose: {string.Join(",", accepted.OrderBy(x => x))}");
                }
            }
        }

        // Instrumento (Regla J): el bucle de arriba tuvo casos de los tres tipos que dice comprobar.
        Assert.True(clickableWithTarget > 0, "ninguna opción con objetivo salió pulsable");
        Assert.True(clickableSecondTarget > 0, "ninguna opción con segundo objetivo salió pulsable");
        Assert.True(blocked > 0, "ninguna opción salió no pulsable");
    }


    // ------------------------------------------------------------------ elegir dos veces

    [Fact]
    public void ChoosingTwiceThrowsAndTheViewShowsItResolvedWithNothingClickable()
    {
        var (state, _) = AtAnEvent(4001UL, gold: 500);
        var events = SystemsTestSupport.Systems.Events;
        var card = EventSystem.Card(state, state.GetNode(state.PendingNodeId), events);
        int way = Array.FindIndex(card.Options.ToArray(), o => o.Effects.Count == 0);

        var before = EventView.Build(state, Catalog, events, Items, Consumables, "es")!;
        Assert.False(before.Resolved);
        Assert.Contains(before.Options, o => o.Affordable);

        var after = EventSystem.Choose(
            state, new ChooseEventOption(way), events, Items, Consumables, SystemsTestSupport.Systems.Economy, Catalog);

        Assert.Throws<InvalidOperationException>(() => EventSystem.Choose(
            after, new ChooseEventOption(way), events, Items, Consumables, SystemsTestSupport.Systems.Economy, Catalog));

        var view = EventView.Build(after, Catalog, events, Items, Consumables, "es")!;
        Assert.True(view.Resolved);
        Assert.All(view.Options, o => Assert.False(o.Affordable));
    }

    [Fact]
    public void ChoosingAPayingOptionTwiceDoesNotPayTwice()
    {
        var (state, _) = AtAnEvent(gold: 100);
        var events = CardOf(Option("pay", false, false, new EventEffect(EventEffectKind.Gold, 30)));

        var once = Choose(state, events, new ChooseEventOption(0));
        Assert.Equal(130, once.Gold);
        Assert.Throws<InvalidOperationException>(() => Choose(once, events, new ChooseEventOption(0)));
    }
}
