using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;

namespace Underleague.Sim.Tests.Run;

/// <summary>Guardado del estado de la run (RT-060, RT-061, RT-061b, RT-062).</summary>
public class RunSaveTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void RoundTrip_KeepsEverythingIncludingTheDataSnapshot()
    {
        var systems = new TestRunSystems { OpponentQuality = 40 };
        var state = RunEngine.Start(TestRuns.Setup(), 2468, Catalog);
        for (int i = 0; i < 3; i++)
        {
            var nodes = RunEngine.AvailableNodes(state);
            state = RunEngine.Enter(state, nodes[0].Id, Catalog, systems);
        }

        state = state
            .WithCounter("reroll_cost", 40)
            .WithAchievement("goals", 3)
            .WithConsumables(new[] { new EquippedConsumable("bandage", ConsumableMode.Manual, "TACKLE") });

        string json = RunSave.Save(state);
        var loaded = RunSave.Load(json);

        Assert.Equal(state.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(state.Seed, loaded.Seed);
        Assert.Equal(state.Division, loaded.Division);
        Assert.Equal(state.ClubId, loaded.ClubId);
        Assert.Equal(state.ClubRace, loaded.ClubRace);
        Assert.Equal(state.Act, loaded.Act);
        Assert.Equal(state.CurrentNodeId, loaded.CurrentNodeId);
        Assert.Equal(state.PendingNodeId, loaded.PendingNodeId);
        Assert.Equal(state.Phase, loaded.Phase);
        Assert.Equal(state.Gold, loaded.Gold);
        Assert.Equal(state.NextPlayerId, loaded.NextPlayerId);
        Assert.Equal(state.Result, loaded.Result);
        Assert.Equal(state.AvailablePlayerCount, loaded.AvailablePlayerCount);
        Assert.Equal(state.NodeHistory, loaded.NodeHistory);
        Assert.Equal(state.Roster.Count, loaded.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            TestRuns.AssertSamePlayer(state.Roster[i], loaded.Roster[i]);
        }

        Assert.Equal(state.Referees, loaded.Referees);
        Assert.Equal(state.Consumables, loaded.Consumables);
        Assert.Equal(state.Lineup.Slots, loaded.Lineup.Slots);
        Assert.Equal(40, loaded.Counter("reroll_cost"));
        Assert.Equal(3, loaded.Achievements["goals"]);

        // RT-061b: la instantánea de /data viaja dentro y sigue siendo un catálogo cargable.
        Assert.Equal(state.DataSnapshot.Count, loaded.DataSnapshot.Count);
        foreach (var (path, content) in state.DataSnapshot)
        {
            Assert.Equal(content, loaded.DataSnapshot[path]);
        }

        var snapshotCatalog = RunSave.CatalogFromSnapshot(loaded);
        Assert.Equal(Catalog.Races.Count, snapshotCatalog.Races.Count);
        Assert.Equal(Catalog.Perks.All.Count, snapshotCatalog.Perks.All.Count);

        // Y los mapas, nodo a nodo.
        Assert.Equal(state.Maps.Count, loaded.Maps.Count);
        for (int m = 0; m < state.Maps.Count; m++)
        {
            var before = state.Maps[m];
            var after = loaded.Maps[m];
            Assert.Equal(before.Act, after.Act);
            Assert.Equal(before.BossNodeId, after.BossNodeId);
            Assert.Equal(before.BossModifierRevealed, after.BossModifierRevealed);
            Assert.Equal(before.EntryNodeIds, after.EntryNodeIds);
            Assert.Equal(before.Nodes.Count, after.Nodes.Count);
            for (int n = 0; n < before.Nodes.Count; n++)
            {
                Assert.Equal(before.Nodes[n].Id, after.Nodes[n].Id);
                Assert.Equal(before.Nodes[n].Kind, after.Nodes[n].Kind);
                Assert.Equal(before.Nodes[n].Layer, after.Nodes[n].Layer);
                Assert.Equal(before.Nodes[n].Next, after.Nodes[n].Next);
                Assert.Equal(before.Nodes[n].Difficulty, after.Nodes[n].Difficulty);
                Assert.Equal(before.Nodes[n].OpponentId, after.Nodes[n].OpponentId);
            }
        }
    }

    [Fact]
    public void SavedText_IsStable()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 13, Catalog);
        string first = RunSave.Save(state);
        Assert.Equal(first, RunSave.Save(state));
        Assert.Equal(first, RunSave.Save(RunSave.Load(first)));
    }

    [Fact]
    public void LoadingAnotherSchemaVersion_Fails()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 5, Catalog);
        string json = RunSave.Save(state).Replace(
            $"\"schemaVersion\":{RunSave.SchemaVersion}",
            "\"schemaVersion\":99",
            StringComparison.Ordinal);

        var error = Assert.Throws<RunSaveException>(() => RunSave.Load(json));
        Assert.Equal("$.schemaVersion", error.JsonPath);
        Assert.Contains("nunca se migra en silencio", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadingRubbish_SaysWhereItBroke()
    {
        Assert.Throws<RunSaveException>(() => RunSave.Load("{ no es json"));
        var error = Assert.Throws<RunSaveException>(() => RunSave.Load("{\"schemaVersion\":1}"));
        Assert.Contains("$.", error.JsonPath, StringComparison.Ordinal);
    }

    [Fact]
    public void LeavingDuringAMatch_ReplaysTheSameMatchOnReturn()
    {
        // RT-061: "salir a mitad de partido reproduce el partido desde la semilla al volver". El
        // guardado ironman se hace al completar cada nodo, así que el estado guardado es el de antes de
        // entrar; volver y entrar en el mismo nodo tiene que dar exactamente el mismo partido.
        var systems = new TestRunSystems { OpponentQuality = 60 };
        var (state, node) = TestRuns.WalkToMatch(RunEngine.Start(TestRuns.Setup(), 9999, Catalog), Catalog, systems);

        string save = RunSave.Save(state);

        var straight = RunEngine.Enter(state, node.Id, Catalog, systems);
        var afterReload = RunEngine.Enter(RunSave.Load(save), node.Id, Catalog, systems);

        Assert.Equal(RunSave.Save(straight), RunSave.Save(afterReload));

        // Y el partido en sí, evento a evento.
        var (setupA, seedA, _) = RunEngine.BuildMatch(state, node.Id, Catalog, systems);
        var (setupB, seedB, _) = RunEngine.BuildMatch(RunSave.Load(save), node.Id, Catalog, systems);
        Assert.Equal(seedA, seedB);

        var a = Underleague.Sim.Engine.Simulator.Run(setupA, seedA, Catalog, systems.MatchConfig(state, node, Catalog));
        var b = Underleague.Sim.Engine.Simulator.Run(setupB, seedB, Catalog, systems.MatchConfig(state, node, Catalog));
        Assert.Equal(a.Events.Count, b.Events.Count);
        for (int i = 0; i < a.Events.Count; i++)
        {
            Assert.Equal(a.Events[i], b.Events[i]);
        }
    }

    [Fact]
    public void DebugState_LoadsWithoutADataSnapshot()
    {
        // RT-062: un estado predefinido escrito a mano no tiene por qué llevar una copia de /data.
        var state = RunEngine.Start(TestRuns.Setup(), 22, Catalog);
        string json = RunSave.Save(state.WithDataSnapshot(new Dictionary<string, string>()), indented: true);

        var loaded = RunSave.Load(json);
        Assert.Empty(loaded.DataSnapshot);
        Assert.Throws<RunSaveException>(() => RunSave.CatalogFromSnapshot(loaded));
        Assert.Equal(state.Roster.Count, loaded.Roster.Count);
    }

    [Fact]
    public void TheSaveMatchesItsSchema()
    {
        // No se valida con JsonSchema.Net (no es dependencia de Sim.Tests), pero sí se comprueba que el
        // esquema y el escritor no se separen: mismas claves, arriba y en cada jugador y cada nodo.
        using var schema = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(TestData.DataDirectory, "schemas", "run-save.schema.json")));
        using var save = System.Text.Json.JsonDocument.Parse(
            RunSave.Save(RunEngine.Start(TestRuns.Setup(), 1, Catalog)));

        AssertSameKeys(schema.RootElement.GetProperty("properties"), save.RootElement, "$");

        var defs = schema.RootElement.GetProperty("$defs");
        AssertSameKeys(
            defs.GetProperty("player").GetProperty("properties"),
            save.RootElement.GetProperty("roster")[0],
            "$.roster[0]");
        AssertSameKeys(
            defs.GetProperty("actMap").GetProperty("properties"),
            save.RootElement.GetProperty("maps")[0],
            "$.maps[0]");
        AssertSameKeys(
            defs.GetProperty("actMap").GetProperty("properties").GetProperty("nodes").GetProperty("items").GetProperty("properties"),
            save.RootElement.GetProperty("maps")[0].GetProperty("nodes")[0],
            "$.maps[0].nodes[0]");
    }

    private static void AssertSameKeys(System.Text.Json.JsonElement schemaProperties, System.Text.Json.JsonElement written, string path)
    {
        var expected = schemaProperties.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var actual = written.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(expected.SequenceEqual(actual, StringComparer.Ordinal),
            $"{path}: el esquema declara [{string.Join(", ", expected)}] y RunSave escribe [{string.Join(", ", actual)}]");
    }

    [Fact]
    public void PlayerFields_SurviveTheRoundTrip()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 31415, Catalog);
        var basePlayer = state.Roster[3] with
        {
            Item = "cursed_boots",
            MinorInjuries = 2,
            PhysicalState = PhysicalState.MinorInjury,
            Wage = 25,
            IsMercenary = true,
            IsYouth = true,
            MatchesBenched = 2,
            Mourning = 1,
            Prostheses = new[] { new RunProsthesis("leg", "speed") },
            Bonds = new[] { new RunBond(1, BondKind.BloodDebt) },
        };

        var player = basePlayer
            .WithCounters(new Dictionary<string, int> { ["kills"] = 3 })
            .WithBondProgress(new Dictionary<string, int> { ["assists_to_1"] = 2 });

        var loaded = RunSave.Load(RunSave.Save(state.WithPlayer(player)));
        TestRuns.AssertSamePlayer(player, loaded.GetPlayer(player.Id));
    }
}
