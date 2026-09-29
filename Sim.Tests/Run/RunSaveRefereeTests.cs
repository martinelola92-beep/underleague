using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0158 §4: sube el esquema de guardado 5 → 6 (definitionId, memory, blindSide del árbitro). Cargar
/// otra versión sigue siendo un error explícito (ya lo cubre <c>RunSaveTests.LoadingAnotherSchemaVersion_Fails</c>,
/// que ahora compara contra la 6); esto comprueba que los tres campos nuevos hacen la ida y vuelta. El
/// campo se llama <c>memory</c> en el guardado (revisión independiente: <c>grudge</c> es la represalia de
/// la ADR 0145, no esto); como la v6 aún no se había publicado, se pudo renombrar sin subir de versión.
/// </summary>
public sealed class RunSaveRefereeTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void DefinitionIdMemoryAndBlindSideSurviveTheRoundTrip()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 777UL, Catalog)
            .WithReferees(new[]
            {
                new RunReferee(0, "Tuerto de prueba", RefereeTrait.OneEyed, BribesReceived: 2)
                {
                    DefinitionId = "tuerto_balbuena",
                    Memory = -33,
                    BlindSide = RefereeSide.Bottom,
                },
                new RunReferee(1, "Neutro de prueba", RefereeTrait.Neutral, BribesReceived: 0)
                {
                    DefinitionId = string.Empty,
                    Memory = 0,
                    BlindSide = RefereeSide.None,
                },
            });

        var loaded = RunSave.Load(RunSave.Save(state));

        Assert.Equal(7, loaded.SchemaVersion);
        Assert.Equal(state.Referees.Count, loaded.Referees.Count);
        for (int i = 0; i < state.Referees.Count; i++)
        {
            var before = state.Referees[i];
            var after = loaded.Referees[i];
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.DefinitionId, after.DefinitionId);
            Assert.Equal(before.Name, after.Name);
            Assert.Equal(before.Trait, after.Trait);
            Assert.Equal(before.BribesReceived, after.BribesReceived);
            Assert.Equal(before.Memory, after.Memory);
            Assert.Equal(before.BlindSide, after.BlindSide);
        }
    }

    [Fact]
    public void SavedTextUsesTheMemoryKeyNotGrudge()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 778UL, Catalog)
            .WithReferees(new[] { new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { Memory = 7 } });

        string json = RunSave.Save(state);

        Assert.Contains("\"memory\":7", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"grudge\"", json, StringComparison.Ordinal);
    }
}
