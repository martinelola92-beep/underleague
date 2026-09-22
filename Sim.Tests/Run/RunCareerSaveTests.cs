using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// Guardado del historial de carrera (RF-122, ADR 0124): el objeto <c>career</c> de cada jugador y el
/// salto de versión de esquema de 4 a 5 que lo acompaña.
/// </summary>
public sealed class RunCareerSaveTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void RoundTrip_KeepsANonEmptyCareer()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 777, Catalog);
        var career = new RunCareer(
            Matches: 5,
            Goals: 3,
            Assists: 2,
            Tackles: 11,
            TacklesWon: 7,
            Fouls: 4,
            Cards: 1,
            InjuriesCaused: 2,
            DeathsCaused: 1,
            InjuriesSuffered: 3,
            TicksOnPitch: 6300);
        var player = state.Roster[0] with { Career = career };

        var loaded = RunSave.Load(RunSave.Save(state.WithPlayer(player)));

        Assert.Equal(career, loaded.GetPlayer(player.Id).Career);

        // El resto de la plantilla no lleva historial: RunCareer.None sobrevive igual al round-trip.
        Assert.Equal(RunCareer.None, loaded.GetPlayer(state.Roster[1].Id).Career);
    }

    [Fact]
    public void SavedText_IncludesTheCareerObject()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 778, Catalog);
        string json = RunSave.Save(state);

        Assert.Contains("\"career\"", json, StringComparison.Ordinal);
        Assert.Contains("\"injuriesCaused\"", json, StringComparison.Ordinal);
        Assert.Contains("\"deathsCaused\"", json, StringComparison.Ordinal);
        Assert.Contains("\"injuriesSuffered\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadingSchemaVersion4Fails()
    {
        // La versión 4 es la que escribía este código antes de la ADR 0124 (sin "career" en el jugador).
        // Cargarla no se migra en silencio: es un error explícito con la ruta JSON (modelo-datos.md).
        var state = RunEngine.Start(TestRuns.Setup(), 779, Catalog);
        string json = RunSave.Save(state).Replace(
            $"\"schemaVersion\":{RunSave.SchemaVersion}",
            "\"schemaVersion\":4",
            StringComparison.Ordinal);

        var error = Assert.Throws<RunSaveException>(() => RunSave.Load(json));
        Assert.Equal("$.schemaVersion", error.JsonPath);
        Assert.Contains("nunca se migra en silencio", error.Message, StringComparison.Ordinal);
    }
}
