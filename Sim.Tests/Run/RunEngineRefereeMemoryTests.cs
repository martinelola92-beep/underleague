using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0158 §4, revisión independiente, punto 9: prueba de extremo a extremo por <c>RunEngine</c> -no
/// <c>MatchResolution</c> directamente-, para comprobar que la memoria de verdad viaja de un partido al
/// siguiente pasando por todas las capas: <c>RunEngine.EnterMatch</c> juega el partido real,
/// <c>MatchResolution.ApplyRefereeMemory</c> actualiza <c>RunReferee.Memory</c>, y el siguiente nodo con
/// el MISMO árbitro arranca en esa memoria (<c>IRunSystems.RefereeFor</c>, <c>RefereeSetup.InitialBias</c>).
/// </summary>
public sealed class RunEngineRefereeMemoryTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Un único árbitro en el plantel: <c>node.Id % 1 == 0</c> siempre, así que TODOS los nodos de partido
    /// de la run los pita el mismo, sin depender de qué nodo toque.
    /// </summary>
    private static RunState StateWithOneReferee(ulong seed) =>
        RunEngine.Start(TestRuns.Setup(), seed, Catalog)
            .WithReferees(new[] { new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" } });

    [Fact]
    public void TheMatchPlayedThroughRunEngineUpdatesTheRefereesMemoryWithTheNewRule()
    {
        var systems = new TestRunSystems();
        var (walked, node) = TestRuns.WalkToMatch(StateWithOneReferee(4242UL), Catalog, systems);

        // El resultado REAL del mismo partido, reproducido por su cuenta (mismo patrón que
        // RunSaveTests.LeavingDuringAMatch_ReplaysTheSameMatchOnReturn), para saber qué shiftedAgainst
        // tiene que haber usado MatchResolution sin adivinarlo.
        var (setup, seed, _) = RunEngine.BuildMatch(walked, node.Id, Catalog, systems);
        var expectedResult = Simulator.Run(setup, seed, Catalog, systems.MatchConfig(walked, node, Catalog));
        int shiftedAgainstPlayer = expectedResult.Report.BiasShiftedAgainst[0];

        var entry = RunEngine.EnterMatch(walked, node.Id, Catalog, systems);

        var memory = Catalog.Tuning.Referee.Memory;
        int expectedMemory = Math.Clamp(
            0 - (shiftedAgainstPlayer * memory.MemoryPercent / 100) + (shiftedAgainstPlayer == 0 ? memory.CleanMatchBonus : 0),
            -memory.MemoryCap,
            memory.MemoryCap);

        Assert.Equal(expectedMemory, entry.State.Referees[0].Memory);
    }

    /// <summary>
    /// El siguiente nodo de partido, con el mismo árbitro (el único del plantel), arranca con la memoria
    /// que acaba de dejar el partido anterior: <c>RefereeSetup.InitialBias</c> == la memoria actualizada.
    /// </summary>
    [Fact]
    public void ALaterNodeWithTheSameRefereeStartsWithTheMemoryTheFirstMatchLeft()
    {
        var systems = new TestRunSystems();
        var (walked, firstNode) = TestRuns.WalkToMatch(StateWithOneReferee(4242UL), Catalog, systems);

        var entry = RunEngine.EnterMatch(walked, firstNode.Id, Catalog, systems);
        Assert.False(RunEngine.Outcome(entry.State).IsOver, "la run no puede haber terminado ya: el test necesita un partido siguiente");

        var nextNodes = RunEngine.AvailableNodes(entry.State);
        Assert.NotEmpty(nextNodes);

        // Busca el siguiente nodo de partido accesible; si el primero no lo es, camina como haría el
        // jugador (mismo ayudante que TestRuns.WalkToMatch usa por dentro).
        var (afterWalking, secondNode) = TestRuns.WalkToMatch(entry.State, Catalog, systems);

        int rememberedMemory = afterWalking.Referees[0].Memory;
        var setup = systems.RefereeFor(afterWalking, secondNode, Catalog);

        Assert.Equal(0, setup.RefereeId);
        Assert.Equal(rememberedMemory, setup.InitialBias);
    }
}
