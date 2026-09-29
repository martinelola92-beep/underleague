using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Equipment;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Rewards;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0161: botín de liga (§1, un objeto común al almacén al ganar), reliquia por muerte propia (§2,
/// según <c>RunCareer</c>) y el cofre (§3, <c>StoreItem</c>/<c>EquipStoredItem</c>/<c>TransferItem</c> sin
/// perder ni duplicar objetos).
/// </summary>
public sealed class LootAndRelicTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    // ------------------------------------------------------------------ 1. botín de liga

    [Fact]
    public void WinningALeagueMatchAddsExactlyOneCommonItemToTheWarehouse()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16001UL, Catalog, Systems);
        var node = FindNode(state, NodeKind.LeagueMatch);

        var after = Systems.AfterMatch(state, node, WonSummary(node), Catalog);

        Assert.Single(after.StoredItems);
        var item = Systems.Items.Get(after.StoredItems[0]);
        Assert.Equal(Rarity.Common, item.Rarity);
        Assert.False(item.IsRelic, "el botín de liga no puede ser una reliquia (ADR 0161 §1 frente a §2)");
    }

    [Fact]
    public void LosingALeagueMatchAddsNoLoot()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16002UL, Catalog, Systems);
        var node = FindNode(state, NodeKind.LeagueMatch);

        var after = Systems.AfterMatch(state, node, LostSummary(node), Catalog);

        Assert.Empty(after.StoredItems);
    }

    [Fact]
    public void WinningAnEliteOrBossMatchDoesNotAddLeagueLoot()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16003UL, Catalog, Systems);
        var elite = FindNode(state, NodeKind.EliteMatch);
        var boss = FindNode(state, NodeKind.Boss);

        var afterElite = Systems.AfterMatch(state, elite, WonSummary(elite), Catalog);
        var afterBoss = Systems.AfterMatch(state, boss, WonSummary(boss), Catalog);

        Assert.Empty(afterElite.StoredItems);
        Assert.Empty(afterBoss.StoredItems);
    }

    [Fact]
    public void LeagueLootIsDeterministic()
    {
        var items = Systems.Items;
        var first = LeagueLootSystem.Pick(4242UL, 101, 1, Race.Human, items);
        var second = LeagueLootSystem.Pick(4242UL, 101, 1, Race.Human, items);

        Assert.Equal(first.Id, second.Id);
    }

    // ------------------------------------------------------------------ 2. reliquia por muerte

    [Theory]
    [InlineData(3, 0, 0, "relic_scorer")]
    [InlineData(0, 2, 0, "relic_butcher")]
    [InlineData(0, 0, 5, "relic_wall")]
    [InlineData(0, 0, 0, "relic_generic")]
    public void DeathGivesTheRelicThatMatchesTheCareer(int goals, int injuriesCaused, int tacklesWon, string expectedRelicId)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16010UL, Catalog, Systems);
        var node = FindNode(state, NodeKind.LeagueMatch);
        var dead = state.Roster[0] with
        {
            PhysicalState = PhysicalState.Dead,
            Item = null,
            Career = new RunCareer(1, goals, 0, 0, tacklesWon, 0, 0, injuriesCaused, 0, 0, 500),
        };
        state = state.WithPlayer(dead);

        var summary = LostSummary(node) with
        {
            OwnDeaths = 1,
            DeathDetails = new[] { new PlayerDeathDetail(dead.Id, Array.Empty<string>(), -1, -1) },
        };

        var after = Systems.AfterMatch(state, node, summary, Catalog);

        Assert.Contains(expectedRelicId, after.StoredItems);
        Assert.True(Systems.Items.Get(expectedRelicId).IsRelic);
    }

    [Fact]
    public void DeathGivesARelicEvenWhenTheMatchIsLost()
    {
        // RF-093/ADR 0161 §2 no distingue victoria de derrota, igual que el equipo del muerto (ADR 0048).
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16011UL, Catalog, Systems);
        var node = FindNode(state, NodeKind.LeagueMatch);
        var dead = state.Roster[0] with { PhysicalState = PhysicalState.Dead, Item = null, Career = RunCareer.None };
        state = state.WithPlayer(dead);
        var summary = LostSummary(node) with
        {
            OwnDeaths = 1,
            DeathDetails = new[] { new PlayerDeathDetail(dead.Id, Array.Empty<string>(), -1, -1) },
        };

        var after = Systems.AfterMatch(state, node, summary, Catalog);

        Assert.Contains("relic_generic", after.StoredItems);
    }

    [Fact]
    public void NoDeathsMeansNoRelic()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16012UL, Catalog, Systems);
        var node = FindNode(state, NodeKind.LeagueMatch);

        var after = Systems.AfterMatch(state, node, LostSummary(node), Catalog);

        Assert.Empty(after.StoredItems);
    }

    // ------------------------------------------------------------------ 3. cofre: Store/Equip/Transfer

    [Fact]
    public void StoreItemMovesTheEquippedItemToTheWarehouseWithoutLosingIt()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16020UL, Catalog, Systems);
        var owner = state.Roster[0] with { Item = "worn_boots" };
        state = state.WithPlayer(owner);

        var next = EquipmentSystem.Apply(state, new StoreItem(owner.Id));

        Assert.Null(next.GetPlayer(owner.Id).Item);
        Assert.Contains("worn_boots", next.StoredItems);
    }

    [Fact]
    public void StoringWithNoItemEquippedThrows()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16021UL, Catalog, Systems);
        var owner = state.Roster[0] with { Item = null };
        state = state.WithPlayer(owner);

        Assert.Throws<ArgumentException>(() => EquipmentSystem.Apply(state, new StoreItem(owner.Id)));
    }

    /// <summary>
    /// El fallo que el encargo pedía comprobar (ADR 0161 §3): antes de este paquete,
    /// <c>EquipStoredItem</c> reutilizaba <c>AssignPurchasedItem</c> y el objeto desplazado se VENDÍA en
    /// vez de volver al cofre. Un objeto ya pagado que se sustituye no puede desaparecer del inventario ni
    /// convertirse en oro que nadie pidió.
    /// </summary>
    [Fact]
    public void EquippingFromTheWarehouseReturnsTheDisplacedItemInsteadOfSellingIt()
    {
        var items = Systems.Items;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16022UL, Catalog, Systems);
        var owner = state.Roster[0] with { Item = "iron_gauntlets" };
        state = state.WithPlayer(owner).WithStockedItem("worn_boots", 1).WithGold(0);

        var next = EquipmentSystem.Apply(state, new EquipStoredItem(owner.Id, "worn_boots"), items);

        Assert.Equal("worn_boots", next.GetPlayer(owner.Id).Item);
        Assert.Contains("iron_gauntlets", next.StoredItems);
        Assert.Equal(0, next.Gold);
    }

    [Fact]
    public void EquippingFromTheWarehouseWithNoStockThrows()
    {
        var items = Systems.Items;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16024UL, Catalog, Systems);
        var owner = state.Roster[0] with { Item = null };
        state = state.WithPlayer(owner);

        Assert.Throws<ArgumentException>(() => EquipmentSystem.Apply(state, new EquipStoredItem(owner.Id, "worn_boots"), items));
    }

    [Fact]
    public void StoreEquipAndTransferNeverLoseOrDuplicateItems()
    {
        var items = Systems.Items;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 16023UL, Catalog, Systems);
        var a = state.Roster[0] with { Item = "worn_boots" };
        var b = state.Roster[1] with { Item = "iron_gauntlets" };
        var c = state.Roster[2] with { Item = null };
        state = state.WithPlayer(a).WithPlayer(b).WithPlayer(c);

        var ids = new[] { a.Id, b.Id, c.Id };
        int before = TotalCopies(state, ids);

        // a guarda su objeto -> c lo saca del cofre -> b se lo pasa a a (b se queda sin nada) ->
        // a lo vuelve a guardar -> b lo recupera del cofre. Ni una copia se pierde ni se duplica en
        // ningún paso intermedio.
        state = EquipmentSystem.Apply(state, new StoreItem(a.Id));
        Assert.Equal(before, TotalCopies(state, ids));

        state = EquipmentSystem.Apply(state, new EquipStoredItem(c.Id, "worn_boots"), items);
        Assert.Equal(before, TotalCopies(state, ids));

        state = EquipmentSystem.Apply(state, new TransferItem(b.Id, a.Id), Systems.Economy, items);
        Assert.Equal(before, TotalCopies(state, ids));

        state = EquipmentSystem.Apply(state, new StoreItem(a.Id));
        Assert.Equal(before, TotalCopies(state, ids));

        state = EquipmentSystem.Apply(state, new EquipStoredItem(b.Id, "iron_gauntlets"), items);
        Assert.Equal(before, TotalCopies(state, ids));

        Assert.Equal("worn_boots", state.GetPlayer(c.Id).Item);
        Assert.Equal("iron_gauntlets", state.GetPlayer(b.Id).Item);
        Assert.Null(state.GetPlayer(a.Id).Item);
    }

    // ------------------------------------------------------------------ 4. el informe cuenta lo que la run hizo

    /// <summary>
    /// Revisión independiente de la ADR 0161: el informe recalcula el botín (<c>LeagueLootSystem.Pick</c>)
    /// en vez de recibirlo, así que si los argumentos con los que lo recalcula se desviaran de los de
    /// <c>AfterMatch</c> el informe enseñaría un objeto y el cofre guardaría otro. Con una liga real,
    /// ganada y sin muertes, lo único que entra en el almacén es el botín: tiene que ser el que el informe
    /// dice.
    /// </summary>
    [Fact]
    public void TheReportedLootIsExactlyWhatTheRunAddedToTheWarehouse()
    {
        int checkedRuns = 0;
        for (ulong seed = 17000; seed < 17060 && checkedRuns < 3; seed++)
        {
            var start = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, Systems);
            var (walked, node) = TestRuns.WalkToMatch(start, Catalog, Systems);
            if (node.Kind != NodeKind.LeagueMatch)
            {
                continue;
            }

            var entry = RunEngine.EnterMatch(walked, node.Id, Catalog, Systems);
            if (!entry.Summary.Won || entry.Summary.OwnDeaths > 0 || entry.State.Result.IsOver)
            {
                continue;
            }

            var playback = MatchPlaybacks.Of(walked, node.Id, Catalog, Systems);
            var report = PostMatchView.Build(playback, entry.State, entry.Summary, Catalog, Systems.Economy, Systems.Items);

            Assert.NotNull(report.Loot);
            Assert.Equal(new[] { report.Loot!.ItemId }, entry.State.StoredItems);
            checkedRuns++;
        }

        Assert.True(checkedRuns > 0, "ninguna de las 60 runs ganó una liga limpia: el test no ha comprobado nada");
    }

    /// <summary>
    /// El informe dice qué reliquia deja un muerto propio y es la que <c>AfterMatch</c> guarda (ADR 0161 §2).
    /// La muerte se inyecta en la secuencia de eventos porque una muerte natural es rara a propósito (ADR
    /// 0048); el resto —el estado tras el partido, el resumen y el catálogo— es el de la run.
    /// </summary>
    [Fact]
    public void TheReportAnnouncesTheRelicAnOwnDeadPlayerLeaves()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 17100UL, Catalog, Systems);
        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, Systems);
        var playback = MatchPlaybacks.Of(walked, node.Id, Catalog, Systems);
        int victim = playback.Setup.Home.Lineup.Slots[1].PlayerId;

        var dead = walked.GetPlayer(victim) with
        {
            PhysicalState = PhysicalState.Dead,
            Item = null,
            Career = new RunCareer(1, 0, 0, 0, 0, 0, 0, 3, 0, 0, 500),
        };
        var after = walked.WithPlayer(dead);
        var death = new MatchEvent(
            EventType.Death, 200, 0, victim, -1, -1, default, default, default, 0, 0, "perk:skullsplitter", 200);
        var withDeath = playback with { Result = playback.Result with { Events = playback.Result.Events.Append(death).ToList() } };
        var summary = LostSummary(node) with
        {
            OwnDeaths = 1,
            DeathDetails = new[] { new PlayerDeathDetail(victim, Array.Empty<string>(), -1, -1) },
        };

        var report = PostMatchView.Build(withDeath, after, summary, Catalog, Systems.Economy, Systems.Items);
        var row = Assert.Single(report.Casualties, c => c.Kind == CasualtyKind.Death);
        var stored = Systems.AfterMatch(after, node, summary, Catalog).StoredItems;

        Assert.Equal("relic_butcher", row.RelicId);
        Assert.Contains(row.RelicId, stored);
        Assert.False(string.IsNullOrEmpty(row.RelicName));
    }

    // ------------------------------------------------------------------ 5. el consumible en vivo llega por la run

    /// <summary>
    /// RF-082: la activación que pulsa el jugador (<c>MatchDecisions.ManualActivations</c>) viaja por
    /// <c>RunEquipment.ForMatch</c> hasta el <c>MatchSetup</c>, y la reproducción de la pantalla
    /// (<c>MatchPlaybacks.Of</c>) juega el mismo partido que la run (<c>EnterMatch</c>): la pantalla enseña
    /// lo que la run va a aplicar.
    /// </summary>
    [Fact]
    public void AManualActivationReachesTheMatchThroughTheRunAndReplayMatchesEnterMatch()
    {
        const int T = 300;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 17200UL, Catalog, Systems)
            .WithTakenConsumable("field_bandage");
        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, Systems);
        var decisions = MatchDecisions.None with { ManualActivations = new[] { new ManualActivation("field_bandage", T) } };

        var (setup, _, _) = RunEngine.BuildMatch(walked, node.Id, Catalog, Systems, decisions.ManualActivations);
        var consumable = Assert.Single(setup.Home.Consumables);
        Assert.Equal(T, consumable.ManualTick);

        var playback = MatchPlaybacks.Of(walked, node.Id, Catalog, Systems, decisions: decisions);
        var used = Assert.Single(playback.Result.Events, e => e.Type == EventType.ConsumableUsed);
        Assert.Equal(T, used.Tick);

        var entry = RunEngine.EnterMatch(walked, node.Id, Catalog, Systems, decisions);
        var activation = Assert.Single(entry.Summary.Report.ConsumableActivations);
        Assert.Equal(("field_bandage", 0, T), (activation.ConsumableId, activation.Team, activation.Tick));

        // Sin pulsar, el mismo estado no activa nada: la diferencia la hace la decisión, no el equipo.
        var untouched = RunEngine.EnterMatch(walked, node.Id, Catalog, Systems);
        Assert.Empty(untouched.Summary.Report.ConsumableActivations);
    }

    // ------------------------------------------------------------------ ayudantes

    private static int TotalCopies(RunState state, IReadOnlyList<int> playerIds)
    {
        int total = state.StoredItems.Count;
        for (int i = 0; i < playerIds.Count; i++)
        {
            if (state.GetPlayer(playerIds[i]).Item is not null)
            {
                total++;
            }
        }

        return total;
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

    private static RunMatchSummary LostSummary(MapNode node)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = 0;
        builder.Goals[1] = 1;
        builder.Winner = 1;
        builder.Ticks = 500;

        return new RunMatchSummary(
            NodeId: node.Id,
            Kind: node.Kind,
            Won: false,
            GoalsFor: 0,
            GoalsAgainst: 1,
            Ticks: 500,
            WentToGoldenGoal: false,
            PlayedPlayerIds: new[] { 0, 1, 2, 3, 4, 5, 6 },
            BenchedPlayerIds: Array.Empty<int>(),
            OwnInjuries: 0,
            OwnDeaths: 0,
            Report: builder.Build());
    }
}
