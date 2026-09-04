using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Tests.Run;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// El camino completo del equipamiento: de lo que la run guarda (ids en <c>RunPlayer.Item</c> y
/// <c>RunState.Consumables</c>) a lo que el partido recibe (<c>MatchSetup</c>), y de vuelta (consumibles
/// gastados, RF-085). Y las dos vías de muerte vistas desde el bucle de run (RF-093).
/// </summary>
public sealed class RunEquipmentTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = SystemsTestSupport.Catalog;

    [Fact]
    public void TheEquippedItemTravelsFromTheRosterToTheMatch()
    {
        // RF-075..078: el objeto de data/items/ se resuelve con la instantánea de la run (RT-061b) y llega
        // al titular que lo lleva, con sus efectos, sin ocupar slot de perk (RF-076).
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 4242, Catalog, SystemsTestSupport.Systems);
        var carrier = state.Roster[1];
        state = state.WithPlayer(carrier with { Item = "iron_gauntlets" });

        // Cualquier nodo de partido del mapa vale: BuildMatch es la función que usa el informe de ojeo
        // (RF-012b), y no exige haber llegado hasta él.
        var node = state.CurrentMap.Nodes.First(n => n.IsMatch);
        var (setup, _, _) = RunEngine.BuildMatch(state, node.Id, Catalog, SystemsTestSupport.Systems);

        var definition = Assert.Single(setup.Home.Players, p => p.Id == carrier.Id);
        Assert.NotNull(definition.Item);
        Assert.Equal("iron_gauntlets", definition.Item!.Id);
        Assert.NotEmpty(definition.Item.Effects);
        Assert.Empty(definition.Perks);
    }

    [Fact]
    public void AFragileItemWorksInTheMatchUntilItBreaks()
    {
        // RF-077, frágil: dentro del partido rinde como cualquier otro; lo que lo define vive fuera, en
        // el estado de la run, y cuando se agota deja de llegar al MatchSetup. Es el ciclo entero: el
        // partido lo usa (paquete de esta tanda) y la run lo gasta (EquipmentSystem, paquete X).
        var items = SystemsTestSupport.Systems.Items;
        var fragile = items.All.First(i => i.Archetype == Underleague.Sim.Run.Systems.Items.ItemArchetype.Fragile);
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 5150, Catalog, SystemsTestSupport.Systems);
        var owner = state.Roster[1];
        state = state.WithPlayer(owner with { Item = fragile.Id, PhysicalState = PhysicalState.Healthy });

        var node = state.CurrentMap.Nodes.First(n => n.IsMatch);
        var (before, _, _) = RunEngine.BuildMatch(state, node.Id, Catalog, SystemsTestSupport.Systems);
        Assert.Equal(fragile.Id, before.Home.Players.Single(p => p.Id == owner.Id).Item?.Id);

        var report = new MatchReportBuilder();
        report.Goals[0] = 1;
        var summary = new RunMatchSummary(
            node.Id, node.Kind, true, 1, 0, 500, false, new[] { owner.Id }, Array.Empty<int>(), 0, 0, report.Build());
        // ADR 0036: la rotura es una tirada por partido, no un contador de usos. Con una probabilidad
        // positiva, repetir el post-partido acaba rompiéndolo; lo que el test afirma es el ciclo, no el
        // número exacto de partidos.
        var broken = state;
        for (int match = 0; match < 200 && broken.GetPlayer(owner.Id).Item is not null; match++)
        {
            var forMatch = summary with { NodeId = node.Id + match };
            broken = Underleague.Sim.Run.Systems.Equipment.EquipmentSystem.ProcessFragileItems(broken, forMatch, items);
        }

        state = broken;
        Assert.Null(state.GetPlayer(owner.Id).Item);
        var (after, _, _) = RunEngine.BuildMatch(state, node.Id, Catalog, SystemsTestSupport.Systems);
        Assert.Null(after.Home.Players.Single(p => p.Id == owner.Id).Item);
    }

    [Fact]
    public void ALoadedRunKeepsPlayingWithItsOwnItems()
    {
        // RT-061b: los catálogos de equipamiento salen de la instantánea de la run, así que cargar una
        // partida guardada tiene que seguir equipando igual. Es dato derivado y no se guarda: se
        // reconstruye al cargar.
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7171, Catalog, SystemsTestSupport.Systems);
        var carrier = state.Roster[2];
        state = state.WithPlayer(carrier with { Item = "worn_boots" });

        var reloaded = Underleague.Sim.Run.Save.RunSave.Load(Underleague.Sim.Run.Save.RunSave.Save(state));
        var node = reloaded.CurrentMap.Nodes.First(n => n.IsMatch);
        var (setup, _, _) = RunEngine.BuildMatch(reloaded, node.Id, Catalog, SystemsTestSupport.Systems);

        var definition = Assert.Single(setup.Home.Players, p => p.Id == carrier.Id);
        Assert.Equal("worn_boots", definition.Item?.Id);
    }

    [Fact]
    public void ConsumablesAreEquippedForTheMatchAndSpentAfterIt()
    {
        // RF-080..085: hasta 3, con al menos un manual, llegan al MatchSetup, y al terminar el partido no
        // persisten. El manual sin pulsar no se gasta; el condicional se resuelve solo si su disparador
        // se cumple.
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 909, Catalog, SystemsTestSupport.Systems);
        state = RunEngine.Apply(
            state,
            new SetConsumables(new[]
            {
                new EquippedConsumable("field_bandage", ConsumableMode.Manual, string.Empty),
                new EquippedConsumable("lucky_charm", ConsumableMode.Conditional, "lastSeconds"),
            }),
            Catalog,
            SystemsTestSupport.Systems);

        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, SystemsTestSupport.Systems);
        var (setup, _, _) = RunEngine.BuildMatch(walked, node.Id, Catalog, SystemsTestSupport.Systems);

        Assert.Equal(2, setup.Home.Consumables.Count);
        Assert.Equal(Underleague.Sim.Perks.ConsumableTrigger.Manual, setup.Home.Consumables[0].Trigger);
        Assert.Equal(-1, setup.Home.Consumables[0].ManualTick);
        Assert.Equal(Underleague.Sim.Perks.ConsumableTrigger.LastSeconds, setup.Home.Consumables[1].Trigger);
        Assert.Empty(setup.Away.Consumables);

        var after = RunEngine.Enter(walked, node.Id, Catalog, SystemsTestSupport.Systems);
        Assert.Empty(after.Consumables);
    }

    [Fact]
    public void AnUnknownTriggerIsRejectedWhenEquipping()
    {
        // RF-083: un disparador mal escrito duele al equipar, no se convierte en un consumible que no se
        // dispara nunca sin decir nada.
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 55, Catalog, SystemsTestSupport.Systems);
        var decision = new SetConsumables(new[]
        {
            new EquippedConsumable("field_bandage", ConsumableMode.Manual, string.Empty),
            new EquippedConsumable("lucky_charm", ConsumableMode.Conditional, "cuandoMeApetezca"),
        });

        Assert.Throws<ArgumentException>(() => RunEngine.Apply(state, decision, Catalog, SystemsTestSupport.Systems));
        Assert.Equal(
            (Underleague.Sim.Perks.ConsumableTrigger.GoalsConceded, 3),
            ConsumableTriggers.Parse("goalsConceded:3"));
    }

    [Fact]
    public void LiningUpASeverelyInjuredPlayerIsAllowedAndWarned()
    {
        // RF-092 deja de ser un bloqueo duro: alinear a un lesionado grave es una decisión legítima
        // (precedente, RF-002d) con su advertencia explícita antes de confirmar (RF-012d, RF-093).
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 31, Catalog, SystemsTestSupport.Systems);
        var wounded = state.Roster.First(p => p.Position != Position.Goalkeeper);
        state = state.WithPlayer(wounded with { PhysicalState = PhysicalState.SevereInjury });

        // Sin decisión explícita no sale al campo, aunque su nombre siga en la alineación guardada.
        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, SystemsTestSupport.Systems);
        var (before, _, _) = RunEngine.BuildMatch(walked, node.Id, Catalog, SystemsTestSupport.Systems);
        Assert.DoesNotContain(before.Home.Players, p => p.Id == wounded.Id);

        // Con ella, sí, y las advertencias lo dicen.
        var lineup = state.Lineup.Slots.Any(s => s.PlayerId == wounded.Id)
            ? state.Lineup
            : new Lineup(state.Lineup.Slots.Take(6).Append(new LineupSlot(wounded.Id, new Cell(5, 0))).ToList());

        var warnings = RunEngine.LineupWarnings(state, lineup);
        Assert.Contains(warnings, w => w.Kind == LineupWarningKind.SevereInjuryDeathRisk && w.PlayerId == wounded.Id);

        state = RunEngine.Apply(state, new SetLineup(lineup), Catalog, SystemsTestSupport.Systems);
        var (after, _, _) = RunEngine.BuildMatch(state, node.Id, Catalog, SystemsTestSupport.Systems);
        Assert.Contains(after.Home.Players, p => p.Id == wounded.Id);
        Assert.Contains(after.Home.Players, p => p.Id == wounded.Id && p.PhysicalState == PhysicalState.SevereInjury);
    }

    [Fact]
    public void ADeadPlayerCanNeverBeLinedUp()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 32, Catalog, SystemsTestSupport.Systems);
        var dead = state.Roster.First(p => p.Position != Position.Goalkeeper);
        state = state.WithPlayer(dead with { PhysicalState = PhysicalState.Dead });

        var lineup = new Lineup(state.Lineup.Slots.Take(6).Append(new LineupSlot(dead.Id, new Cell(5, 0))).ToList());
        var error = Assert.Throws<ArgumentException>(
            () => RunEngine.Apply(state, new SetLineup(lineup), Catalog, SystemsTestSupport.Systems));
        Assert.Contains("muerto", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRiskOfPlayingInjuredIsTakenMatchByMatch()
    {
        // La marca de "lo alineo a sabiendas" no se hereda: tras el partido hay que volver a decidirlo,
        // porque si no, un lesionado grave saldría solo al campo partido tras partido (RF-093).
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 33, Catalog, SystemsTestSupport.Systems);
        var wounded = state.Roster.First(p => p.Position != Position.Goalkeeper);
        state = state.WithPlayer(wounded with { PhysicalState = PhysicalState.SevereInjury });

        var lineup = new Lineup(state.Lineup.Slots.Take(6).Append(new LineupSlot(wounded.Id, new Cell(5, 0))).ToList());
        state = RunEngine.Apply(state, new SetLineup(lineup), Catalog, SystemsTestSupport.Systems);
        Assert.Equal(1, state.Counter(RunLineup.RiskCounterPrefix + wounded.Id));

        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, SystemsTestSupport.Systems);
        var after = RunEngine.Enter(walked, node.Id, Catalog, SystemsTestSupport.Systems);
        Assert.Equal(0, after.Counter(RunLineup.RiskCounterPrefix + wounded.Id));
        Assert.DoesNotContain(after.Lineup.Slots, s => s.PlayerId == wounded.Id);
    }
}
