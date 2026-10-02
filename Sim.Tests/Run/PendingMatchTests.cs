using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// Guardar o salir a mitad de un partido (BR-A, RT-061, ADR 0183): el guardado lleva el estado de ANTES del
/// partido y las decisiones tomadas hasta salir, y al volver el partido se reproduce desde la semilla hasta el
/// mismo final.
/// </summary>
public class PendingMatchTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Decisiones con un poco de todo: una orden táctica, una activación y un «que siga jugando».</summary>
    private static MatchDecisions SomeDecisions() =>
        new(
            new[] { new ManualActivation("bandage", 90) },
            new[] { new Substitution(60, 3, 9) })
        {
            Declines = new[] { new DeclinedSubstitution(120, 5) },
            PlayOns = new[] { new PlayOn(75, 4, new Attributes(41, 52, 63, 74, 85)) },
            OrderChanges = new[] { new OrderChange(40, Mentality.Offensive), new OrderChange(200, Mentality.Defensive) },
        };

    private static (RunState State, int NodeId, TestRunSystems Systems) BeforeAMatch(ulong seed)
    {
        var systems = new TestRunSystems { OpponentQuality = 40 };
        var (state, node) = TestRuns.WalkToMatch(RunEngine.Start(TestRuns.Setup(), seed, Catalog), Catalog, systems);
        return (state, node.Id, systems);
    }

    [Fact]
    public void TheDecisionsAndTheWatchedTickSurviveTheRoundTrip()
    {
        var (state, nodeId, _) = BeforeAMatch(2026);
        var pending = new PendingMatch(nodeId, SomeDecisions(), 137);

        string json = RunSave.Save(state, pending);
        var loaded = RunSave.Load(json, out _, out var back);

        Assert.NotNull(back);
        Assert.Equal(nodeId, back.NodeId);
        Assert.Equal(137, back.WatchedTick);
        Assert.Equal(pending.Decisions.ManualActivations, back.Decisions.ManualActivations);
        Assert.Equal(pending.Decisions.Substitutions, back.Decisions.Substitutions);
        Assert.Equal(pending.Decisions.Declines, back.Decisions.Declines);
        Assert.Equal(pending.Decisions.PlayOns, back.Decisions.PlayOns);
        Assert.Equal(pending.Decisions.OrderChanges, back.Decisions.OrderChanges);

        // Y el estado guardado es el de antes del partido, intacto: reescribirlo da el mismo texto.
        Assert.Equal(json, RunSave.Save(loaded, back));
    }

    /// <summary>
    /// Valor conocido: guardar a mitad de partido, cargar y terminar da el mismo resultado byte a byte que
    /// no haber salido. Con decisiones de verdad dentro (una orden ofensiva), no con el partido por defecto.
    /// </summary>
    [Fact]
    public void SavingMidMatchLoadingAndFinishingGivesTheSameResultAsNeverLeaving()
    {
        var (state, nodeId, systems) = BeforeAMatch(2026);
        var decisions = new MatchDecisions(Array.Empty<ManualActivation>(), Array.Empty<Substitution>())
        {
            OrderChanges = new[] { new OrderChange(40, Mentality.Offensive) },
        };

        var never = RunEngine.EnterMatch(state, nodeId, Catalog, systems, decisions);

        string json = RunSave.Save(state, new PendingMatch(nodeId, decisions, 55));
        var loaded = RunSave.Load(json, out _, out var pending);
        Assert.NotNull(pending);
        var resumed = RunEngine.EnterMatch(loaded, pending.NodeId, Catalog, systems, pending.Decisions);

        Assert.Equal(RunSave.Save(never.State), RunSave.Save(resumed.State));
        Assert.Equal(never.Summary.Report.Goals, resumed.Summary.Report.Goals);
        Assert.Equal(never.Outcome, resumed.Outcome);

        // Control del instrumento (Regla J): la orden se aplica de verdad, y por eso perderla al guardar
        // —el fallo que este guardado evita— sí habría dado otro partido. Si esta orden no cambiara nada
        // en esta semilla, la igualdad de arriba no probaría que las decisiones viajan.
        var without = RunEngine.EnterMatch(state, nodeId, Catalog, systems);
        Assert.NotEqual(RunSave.Save(never.State), RunSave.Save(without.State));
    }

    /// <summary>
    /// Anti-abuso: salir y volver cualquier número de veces no cambia el resultado, y reanudar sin perder las
    /// decisiones es lo único que lo garantiza. Tres salidas seguidas dan el mismo guardado final que ninguna.
    /// </summary>
    [Fact]
    public void LeavingAndComingBackAnyNumberOfTimesDoesNotChangeTheResult()
    {
        var (state, nodeId, systems) = BeforeAMatch(77);
        var decisions = SomeDecisionsThatTheEngineAccepts();
        var never = RunEngine.EnterMatch(state, nodeId, Catalog, systems, decisions);

        var current = state;
        var pending = new PendingMatch(nodeId, decisions, 10);
        for (int exit = 0; exit < 3; exit++)
        {
            // Cada salida: se guarda (con el partido a medias cada vez más lejos) y se retoma el guardado.
            current = RunSave.Load(RunSave.Save(current, pending with { WatchedTick = 10 + (exit * 100) }), out _, out var back);
            Assert.NotNull(back);
            pending = back;
        }

        var resumed = RunEngine.EnterMatch(current, pending.NodeId, Catalog, systems, pending.Decisions);
        Assert.Equal(RunSave.Save(never.State), RunSave.Save(resumed.State));
    }

    private static MatchDecisions SomeDecisionsThatTheEngineAccepts() =>
        new(Array.Empty<ManualActivation>(), Array.Empty<Substitution>())
        {
            OrderChanges = new[] { new OrderChange(30, Mentality.Defensive), new OrderChange(150, Mentality.Offensive) },
        };

    /// <summary>Un guardado anterior (versión 8, sin <c>pendingMatch</c>) sigue cargando, sin partido pendiente.</summary>
    [Fact]
    public void ASchemaVersion8SaveStillLoadsWithoutAPendingMatch()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 5, Catalog);
        string v9 = RunSave.Save(state);
        Assert.Contains("\"pendingMatch\":null", v9, StringComparison.Ordinal);

        // Un guardado de la 8 no traía ni el campo ni la versión nueva.
        string v8 = v9
            .Replace($"\"schemaVersion\":{RunSave.SchemaVersion}", "\"schemaVersion\":8", StringComparison.Ordinal)
            .Replace("\"pendingMatch\":null,", string.Empty, StringComparison.Ordinal)
            .Replace(",\"pendingMatch\":null", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("pendingMatch", v8, StringComparison.Ordinal);

        var loaded = RunSave.Load(v8, out _, out var pending);

        Assert.Null(pending);
        Assert.Equal(state.Seed, loaded.Seed);
        Assert.Equal(state.Roster.Count, loaded.Roster.Count);

        // Revisión: la migración sube la versión al cargar, y al reescribirlo sale una 9 completa (con su
        // pendingMatch nulo), idéntica a la que se habría escrito de haber empezado la run con este código.
        Assert.Equal(9, loaded.SchemaVersion);
        Assert.Equal(v9, RunSave.Save(loaded));
    }

    [Fact]
    public void ADecisionAtOrBeforeTheWatchedTickIsRejectedAndTheNextOneIsAllowed()
    {
        Assert.False(PendingMatch.CanDecideAt(watchedTick: 100, decisionTick: 99));
        Assert.False(PendingMatch.CanDecideAt(watchedTick: 100, decisionTick: 100));
        Assert.True(PendingMatch.CanDecideAt(watchedTick: 100, decisionTick: 101));

        // Sin salir del partido el suelo es 0 y no estorba a nada.
        Assert.True(PendingMatch.CanDecideAt(watchedTick: 0, decisionTick: 1));
    }

    /// <summary>
    /// El guardado de antes de enseñar el partido lleva el peor caso, el último tick, para una victoria y para
    /// una derrota (un cierre forzado no deja un suelo a 0), y carga y reproduce el mismo partido.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheCheckpointWrittenBeforeShowingTheMatchCarriesTheWorstCaseWatchedTick(bool victory)
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var (state, nodeId, systems) = BeforeAMatch(seed);
            // El resultado que cuenta es el de EnterMatch (resuelve también los puntos del jugador con la
            // política por defecto), no el de la reproducción, que los deja pendientes.
            var expected = RunEngine.EnterMatch(state, nodeId, Catalog, systems);
            if ((expected.Summary.Report.Winner == 0) != victory)
            {
                continue;
            }

            var playback = Underleague.Sim.Run.View.MatchPlaybacks.Of(state, nodeId, Catalog, systems, trace: true);

            var pending = PendingMatch.BeforeShowing(nodeId, MatchDecisions.None, playback);
            var trace = playback.Trace!;
            Assert.Equal(trace.TickAt(trace.FrameCount - 1), pending.WatchedTick);
            Assert.True(pending.WatchedTick > 0);

            var loaded = RunSave.Load(RunSave.Save(state, pending), out _, out var back);
            Assert.Equal(pending.WatchedTick, back!.WatchedTick);
            Assert.False(PendingMatch.CanDecideAt(back.WatchedTick, back.WatchedTick));

            var entry = RunEngine.EnterMatch(loaded, back.NodeId, Catalog, systems, back.Decisions);
            Assert.Equal(RunSave.Save(expected.State), RunSave.Save(entry.State));
            return;
        }

        Assert.Fail($"ninguna de las 60 semillas dio {(victory ? "una victoria" : "una derrota")}");
    }

    /// <summary>
    /// Un guardado con <c>pendingMatch</c> no nulo cumple <c>run-save.schema.json</c> de verdad (JsonSchema.Net),
    /// y el nulo también: las claves no bastan, también los tipos y los <c>required</c>.
    /// </summary>
    [Fact]
    public void ASaveWithAPendingMatchValidatesAgainstTheSchema()
    {
        var schema = Json.Schema.JsonSchema.FromText(
            File.ReadAllText(Path.Combine(TestData.DataDirectory, "schemas", "run-save.schema.json")));
        var (state, nodeId, _) = BeforeAMatch(2026);

        foreach (var pending in new PendingMatch?[] { new PendingMatch(nodeId, SomeDecisions(), 137), null })
        {
            using var doc = System.Text.Json.JsonDocument.Parse(RunSave.Save(state, pending));
            var result = schema.Evaluate(doc.RootElement, new Json.Schema.EvaluationOptions { OutputFormat = Json.Schema.OutputFormat.List });
            Assert.True(result.IsValid, $"pendingMatch {(pending is null ? "nulo" : "no nulo")} no cumple el esquema");
        }
    }

    [Fact]
    public void AVersionBeforeTheOldestReadableOneStillFailsExplicitly()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 5, Catalog);
        string json = RunSave.Save(state).Replace(
            $"\"schemaVersion\":{RunSave.SchemaVersion}", "\"schemaVersion\":7", StringComparison.Ordinal);

        var error = Assert.Throws<RunSaveException>(() => RunSave.Load(json));
        Assert.Equal("$.schemaVersion", error.JsonPath);
    }

    [Fact]
    public void ABrokenPendingMatchSaysWhereItBroke()
    {
        var (state, nodeId, _) = BeforeAMatch(2026);
        string json = RunSave.Save(state, new PendingMatch(nodeId, SomeDecisions(), 5))
            .Replace("\"watchedTick\":5,", string.Empty, StringComparison.Ordinal);

        var error = Assert.Throws<RunSaveException>(() => RunSave.Load(json));
        Assert.Equal("$.pendingMatch.watchedTick", error.JsonPath);
    }
}
