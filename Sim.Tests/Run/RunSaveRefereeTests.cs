using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0158 §4: sube el esquema de guardado 5 → 6 (definitionId, grudge, blindSide del árbitro). Cargar
/// otra versión sigue siendo un error explícito (ya lo cubre <c>RunSaveTests.LoadingAnotherSchemaVersion_Fails</c>,
/// que ahora compara contra la 6); esto comprueba que los tres campos nuevos hacen la ida y vuelta.
/// </summary>
public sealed class RunSaveRefereeTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void DefinitionIdGrudgeAndBlindSideSurviveTheRoundTrip()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 777UL, Catalog)
            .WithReferees(new[]
            {
                new RunReferee(0, "Tuerto de prueba", RefereeTrait.OneEyed, BribesReceived: 2)
                {
                    DefinitionId = "tuerto_balbuena",
                    Grudge = -33,
                    BlindSide = RefereeSide.Bottom,
                },
                new RunReferee(1, "Neutro de prueba", RefereeTrait.Neutral, BribesReceived: 0)
                {
                    DefinitionId = string.Empty,
                    Grudge = 0,
                    BlindSide = RefereeSide.None,
                },
            });

        var loaded = RunSave.Load(RunSave.Save(state));

        Assert.Equal(6, loaded.SchemaVersion);
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
            Assert.Equal(before.Grudge, after.Grudge);
            Assert.Equal(before.BlindSide, after.BlindSide);
        }
    }
}
