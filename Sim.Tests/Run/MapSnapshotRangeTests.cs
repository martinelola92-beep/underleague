using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Map;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0170, RT-061b: el diseño bajó los nodos por acto de 10-12 a 8-10, pero una run guardada antes lleva en su
/// instantánea de <c>/data</c> el <c>map.json</c> de 11/12/12 y sus mapas ya generados. Retomarla no puede fallar
/// (la run ironman se perdería por actualizar el juego), mientras que el <c>/data</c> vigente sí exige el rango nuevo.
/// </summary>
public sealed class MapSnapshotRangeTests
{
    private const string OldMap = "{ \"nodesPerAct\": [11, 12, 12], \"eliteRivalLevelBonus\": 2 }";

    private static Dictionary<string, string> WithMap(string mapJson)
    {
        var files = TestData.LoadAllFiles();
        files["map/map.json"] = mapJson;
        return files;
    }

    private static string Map(params int[] nodes) =>
        "{ \"nodesPerAct\": [" + string.Join(", ", nodes) + "], \"eliteRivalLevelBonus\": 2 }";

    /// <summary>El caso real: una run empezada con el mapa antiguo se guarda, se carga y se sigue jugando.</summary>
    [Fact]
    public void ARunSavedWithTheOldMapConfigLoadsAndKeepsPlaying()
    {
        var oldFiles = WithMap(OldMap);
        var catalog = DataLoader.FromJson(oldFiles);
        var setup = new RunSetup("test_club", Race.Orc, oldFiles)
        {
            StartingGold = 100,
            GeneratedQuality = 70,
            NodesPerActByAct = [11, 12, 12],
        };

        var started = RunEngine.Start(setup, 4242, catalog);
        Assert.Equal(11, MapInvariants.PathLength(started.MapOf(1)));
        Assert.Equal(12, MapInvariants.PathLength(started.MapOf(2)));
        Assert.Equal(12, MapInvariants.PathLength(started.MapOf(3)));

        var loaded = RunSave.Load(RunSave.Save(started));

        // El /data vigente NO acepta esa configuración; la instantánea de una run, sí.
        Assert.ThrowsAny<Exception>(() => StandardRunSystems.FromJson(loaded.DataSnapshot));
        var systems = StandardRunSystems.FromJson(loaded.DataSnapshot, fromRunSnapshot: true);
        Assert.Equal(new[] { 11, 12, 12 }, systems.Map.NodesPerAct);

        var resumed = RunSave.CatalogFromSnapshot(loaded);
        var played = TestRuns.PlayToTheEnd(loaded, resumed, new TestRunSystems { OpponentQuality = 30 });

        Assert.True(played.NodeHistory.Count >= 8, $"la run retomada sólo pasó por {played.NodeHistory.Count} nodos ({RunEngine.Outcome(played).Kind})");
        Assert.Equal(started.Maps.Count, played.Maps.Count);
        Assert.Equal(11, MapInvariants.PathLength(played.MapOf(1)));
    }

    [Fact]
    public void TheCurrentDataRequiresTheDesignRangeAndTwoEliteActsFromNineNodes()
    {
        Assert.Equal(new[] { 8, 9, 9 }, MapLoader.FromJson(WithMap(Map(8, 9, 9))).NodesPerAct);
        Assert.Equal(new[] { 10, 10, 10 }, MapLoader.FromJson(WithMap(Map(10, 10, 10))).NodesPerAct);

        Assert.Throws<DataException>(() => MapLoader.FromJson(WithMap(OldMap)));
        Assert.Throws<DataException>(() => MapLoader.FromJson(WithMap(Map(7, 9, 9))));
        Assert.Throws<DataException>(() => MapLoader.FromJson(WithMap(Map(11, 9, 9))));

        // Con 8 nodos en los actos 2 y 3 AssignElites puede dejar un solo élite: los datos vigentes no lo admiten.
        Assert.Throws<DataException>(() => MapLoader.FromJson(WithMap(Map(8, 8, 9))));
        Assert.Throws<DataException>(() => MapLoader.FromJson(WithMap(Map(8, 9, 8))));
    }

    [Fact]
    public void ARunSnapshotAcceptsTheHistoricRangeAndNothingOutsideIt()
    {
        Assert.Equal(new[] { 11, 12, 12 }, MapLoader.FromJson(WithMap(OldMap), fromRunSnapshot: true).NodesPerAct);
        Assert.Equal(new[] { 8, 8, 12 }, MapLoader.FromJson(WithMap(Map(8, 8, 12)), fromRunSnapshot: true).NodesPerAct);

        Assert.Throws<DataException>(() => MapLoader.FromJson(WithMap(Map(7, 9, 9)), fromRunSnapshot: true));
        Assert.Throws<DataException>(() => MapLoader.FromJson(WithMap(Map(9, 13, 9)), fromRunSnapshot: true));
    }
}
