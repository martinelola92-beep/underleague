using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;
using Underleague.Sim.Tests.Perks;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// El agrupador de momentos (ADR 0119): que cada evento narrable caiga en exactamente un momento, que
/// las marcas de perk del primer segundo se descarten, que la política de velocidad siga la tabla de la
/// ADR, que la sustitución (ADR 0094) no cambie los momentos anteriores a su decisión, y que el número de
/// momentos por partido esté en la banda medida en la fase A.
///
/// <para>Los casos de borde de la fusión (regla 2) se prueban con <see cref="MatchMomentView.Group"/>
/// directamente, sobre eventos construidos a mano con <see cref="Ev"/> y <see cref="Identity"/> como
/// conversión tick↔fotograma: no hace falta jugar un partido para comprobar una ventana de 15 ticks.</para>
/// </summary>
public sealed class MatchMomentViewTests
{
    private readonly ITestOutputHelper _output;

    public MatchMomentViewTests(ITestOutputHelper output) => _output = output;

    // Mismo perk y misma semilla que MatchFlashViewTests: se cobra en cada pase completado del portador,
    // así que hay marcas de sobra para comprobar dónde caen respecto al primer segundo y a los momentos.
    private const ulong FlashSeed = 14UL;

    private static readonly string OnPass = TestPerks.Json(
        "test_flash",
        "PASS_COMPLETED",
        """[ { "type": "modifyProbability", "target": "actor", "probability": "pass", "value": 15, "duration": "match" } ]""");

    private const ulong BandSeed = 20260919UL;
    private const int BandRuns = 200;

    /// <summary>Conversión tick↔fotograma identidad, para los tests que llaman a <see cref="MatchMomentView.Group"/> sin traza.</summary>
    private static readonly Func<int, int> Identity = t => t;

    /// <summary>
    /// a) Todo evento clasificable cae en exactamente un momento, ningún PERK_TRIGGERED forma momento, los
    /// índices de cada momento son ascendentes, los propios momentos están ordenados, el fotograma de
    /// congelación es el anterior al del momento, el primero es el saque y el último contiene el final.
    /// </summary>
    [Fact]
    public void EveryNarratableEventBelongsToExactlyOneMoment()
    {
        var catalog = TestData.LoadCatalog();
        for (ulong seed = 0; seed < 40; seed++)
        {
            var setup = TestMatches.Reference(catalog, seed);
            var result = Simulator.Run(setup, seed, catalog, SimConfig.Default with { Trace = true });
            var moments = MatchMomentView.Build(setup, result, catalog);

            var coverage = new int[result.Events.Count];
            foreach (var moment in moments.Moments)
            {
                Assert.NotEmpty(moment.EventIndices);
                Assert.Equal(Math.Max(moment.Frame - 1, 0), moment.FreezeFrame);
                Assert.True(moment.Frame <= moment.LastFrame, $"semilla {seed}: Frame > LastFrame");
                Assert.InRange(moment.Level, 1, 4);

                for (int i = 0; i < moment.EventIndices.Count; i++)
                {
                    int index = moment.EventIndices[i];
                    Assert.NotEqual(EventType.PerkTriggered, result.Events[index].Type);
                    coverage[index]++;

                    if (i > 0)
                    {
                        Assert.True(
                            moment.EventIndices[i - 1] < moment.EventIndices[i],
                            $"semilla {seed}: EventIndices no ascendentes");
                    }
                }
            }

            for (int i = 0; i < result.Events.Count; i++)
            {
                int expected = IsClassifiable(result.Events[i]) ? 1 : 0;
                Assert.Equal(expected, coverage[i]);
            }

            for (int i = 1; i < moments.Moments.Count; i++)
            {
                var previous = moments.Moments[i - 1];
                var current = moments.Moments[i];
                Assert.True(
                    previous.Frame < current.Frame
                        || (previous.Frame == current.Frame && previous.EventIndices[0] < current.EventIndices[0]),
                    $"semilla {seed}: momentos no ordenados");
            }

            Assert.NotEmpty(moments.Moments);
            Assert.Equal(MomentKind.Kickoff, moments.Moments[0].Kind);

            int matchEndIndex = -1;
            for (int i = 0; i < result.Events.Count; i++)
            {
                if (result.Events[i].Type == EventType.MatchEnd)
                {
                    matchEndIndex = i;
                    break;
                }
            }

            Assert.True(matchEndIndex >= 0, $"semilla {seed}: el partido no tiene MATCH_END");
            var last = moments.Moments[^1];
            Assert.Equal(4, last.Level);
            Assert.Contains(matchEndIndex, last.EventIndices);
        }
    }

    /// <summary>
    /// b) Ninguna marca de perk cae antes del corte del primer segundo, y toda marca con MomentIndex >= 0
    /// apunta a un momento (que no sea el Kickoff) que de verdad contiene a su jugador.
    /// </summary>
    [Fact]
    public void NoMarkInTheFirstSecond()
    {
        var catalog = TestPerks.CatalogWith(("test_flash", OnPass));
        var setup = TestPerks.Match(catalog, FlashSeed, (1, new[] { "test_flash" }));
        var result = Simulator.Run(setup, FlashSeed, catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;
        var moments = MatchMomentView.Build(setup, result, catalog);

        Assert.NotEmpty(moments.Marks);

        int matchStartTick = FindMatchStartTick(result.Events);
        int cutoff = matchStartTick + MatchMomentView.KickoffTicks;

        foreach (var mark in moments.Marks)
        {
            int tick = trace.TickAt(mark.Flash.Frame);
            Assert.True(tick >= cutoff, $"marca en el tick {tick}, antes del corte de {cutoff}");

            Assert.True(mark.MomentIndex == -1 || (mark.MomentIndex >= 0 && mark.MomentIndex < moments.Moments.Count));
            if (mark.MomentIndex >= 0)
            {
                var moment = moments.Moments[mark.MomentIndex];
                Assert.NotEqual(MomentKind.Kickoff, moment.Kind);
                Assert.True(
                    ContainsPlayer(result.Events, moment, mark.Flash.PlayerId),
                    "la marca apunta a un momento que no contiene a su jugador");
            }
        }
    }

    /// <summary>c) La tabla de la regla 7, incluido el error explícito para una velocidad no soportada.</summary>
    [Fact]
    public void PresentationPolicyFollowsTheSpeedTable()
    {
        var goal = HandmadeMoment(MomentKind.Goal, level: 3, pauses: true, decision: false, hasGoal: true);
        var yellow = HandmadeMoment(MomentKind.Yellow, level: 2, pauses: false, decision: false);
        var foul = HandmadeMoment(MomentKind.Foul, level: 1, pauses: false, decision: false);
        var death = HandmadeMoment(MomentKind.Death, level: 4, pauses: true, decision: false);
        var fullTime = HandmadeMoment(MomentKind.FullTime, level: 4, pauses: true, decision: false);
        var severeWithDecision = HandmadeMoment(MomentKind.SevereInjury, level: 3, pauses: true, decision: true);

        AssertPresentation(goal, speed: 1, shown: true, compressed: false, pauses: true);
        AssertPresentation(goal, speed: 4, shown: true, compressed: true, pauses: true);
        AssertPresentation(goal, speed: 16, shown: false, compressed: false, pauses: false);

        AssertPresentation(yellow, speed: 1, shown: true, compressed: false, pauses: false);
        AssertPresentation(yellow, speed: 4, shown: false, compressed: false, pauses: false);
        AssertPresentation(yellow, speed: 16, shown: false, compressed: false, pauses: false);

        AssertPresentation(foul, speed: 1, shown: true, compressed: false, pauses: false);
        AssertPresentation(foul, speed: 4, shown: false, compressed: false, pauses: false);
        AssertPresentation(foul, speed: 16, shown: false, compressed: false, pauses: false);

        AssertPresentation(death, speed: 1, shown: true, compressed: false, pauses: true);
        AssertPresentation(death, speed: 4, shown: true, compressed: true, pauses: true);
        AssertPresentation(death, speed: 16, shown: true, compressed: true, pauses: true);

        AssertPresentation(fullTime, speed: 1, shown: true, compressed: false, pauses: true);
        AssertPresentation(fullTime, speed: 4, shown: true, compressed: true, pauses: true);
        AssertPresentation(fullTime, speed: 16, shown: true, compressed: true, pauses: true);

        AssertPresentation(severeWithDecision, speed: 1, shown: true, compressed: false, pauses: true);
        AssertPresentation(severeWithDecision, speed: 4, shown: true, compressed: true, pauses: true);
        AssertPresentation(severeWithDecision, speed: 16, shown: true, compressed: true, pauses: true);

        Assert.Throws<ArgumentOutOfRangeException>(() => MatchMomentView.Present(goal, speed: 2));
    }

    /// <summary>
    /// Corrección tras la revisión independiente: a x4 lo que decide si se ve es <c>HasGoal</c> (el
    /// momento contiene un gol no anulado), no que lo encabece un evento de tipo Goal — un gol al que
    /// luego se le fusiona una N3 posterior (aquí, un Red por simplicidad) sigue siendo un gol.
    /// </summary>
    [Fact]
    public void PresentAtSpeedFourUsesHasGoalNotTheHeadingKind()
    {
        var red = HandmadeMoment(MomentKind.Red, level: 3, pauses: false, decision: false);
        var severeWithoutDecision = HandmadeMoment(MomentKind.SevereInjury, level: 3, pauses: true, decision: false);
        var kickoff = HandmadeMoment(MomentKind.Kickoff, level: 3, pauses: false, decision: false);
        var goalHeadedByRed = HandmadeMoment(MomentKind.Red, level: 3, pauses: true, decision: false, hasGoal: true);

        AssertPresentation(red, speed: 4, shown: false, compressed: false, pauses: false);
        AssertPresentation(severeWithoutDecision, speed: 4, shown: false, compressed: false, pauses: false);
        AssertPresentation(kickoff, speed: 4, shown: false, compressed: false, pauses: false);
        AssertPresentation(goalHeadedByRed, speed: 4, shown: true, compressed: true, pauses: true);
    }

    /// <summary>d) Los momentos anteriores al tick de cada decisión de una cadena no cambian al añadir su sustitución.</summary>
    [Fact]
    public void MomentsBeforeADecisionDoNotChangeWhenTheMatchIsReplayedWithTheSubstitution()
    {
        var catalog = TestData.LoadCatalog();
        int decisionsCompared = 0;

        for (ulong seed = 0; seed < 200; seed++)
        {
            var setup = TestMatches.Reference(catalog, seed);
            var currentSetup = setup;
            var currentResult = Simulator.Run(currentSetup, seed, catalog, SimConfig.Default with { Trace = true });

            // Cadena de decisiones del equipo 0, una por una, exactamente como ResolveAutomatically:
            // en cada vuelta se resuelve el punto más temprano con la política por defecto y se vuelve a
            // jugar; lo que se compara es SIEMPRE la pareja (antes de esta sustitución, después de esta
            // sustitución), nunca el partido sin sustituciones contra el final de toda la cadena — una
            // sustitución anterior cambia el partido a partir de su propio tick, así que no es la misma
            // garantía (ADR 0094) comparar contra un partido con otra sustitución de por medio.
            for (int round = 0; round < 32; round++)
            {
                var point = SubstitutionPoints.Pending(currentSetup, currentResult, team: 0, catalog);
                if (point is null)
                {
                    break;
                }

                var beforeSetup = currentSetup;
                var beforeResult = currentResult;

                var outPlayer = beforeSetup.Home.Players.First(p => p.Id == point.OutPlayerId);
                var chosen = SubstitutionPolicy.Default(point, outPlayer);
                var substitution = new Substitution(point.Tick, point.OutPlayerId, chosen.Id);
                var afterSetup = beforeSetup with
                {
                    Home = beforeSetup.Home with { Substitutions = beforeSetup.Home.Substitutions.Append(substitution).ToArray() },
                };
                var afterResult = Simulator.Run(afterSetup, seed, catalog, SimConfig.Default with { Trace = true });

                var beforeMoments = MatchMomentView.Build(beforeSetup, beforeResult, catalog).Moments;
                var afterMoments = MatchMomentView.Build(afterSetup, afterResult, catalog).Moments;

                int decisionFrame = beforeResult.Trace!.FrameOfTick(point.Tick);
                var beforeFiltered = beforeMoments.Where(m => m.Frame <= decisionFrame).ToList();
                var afterFiltered = afterMoments.Where(m => m.Frame <= decisionFrame).ToList();

                Assert.Equal(beforeFiltered.Count, afterFiltered.Count);
                for (int i = 0; i < beforeFiltered.Count; i++)
                {
                    var b = beforeFiltered[i];
                    var a = afterFiltered[i];
                    string context = $"semilla {seed}, decisión en el tick {point.Tick}, momento {i}";

                    Assert.Equal(b.Frame, a.Frame);
                    Assert.Equal(b.Level, a.Level);
                    Assert.Equal(b.Kind, a.Kind);
                    Assert.Equal(b.Team, a.Team);
                    Assert.Equal(b.LeadPlayerId, a.LeadPlayerId);
                    Assert.Equal(b.Pauses, a.Pauses);
                    Assert.Equal(b.Decision, a.Decision);
                    Assert.Equal(b.Cancelled, a.Cancelled);
                    Assert.Equal(b.HasGoal, a.HasGoal);

                    // La sustitución de esta vuelta entra en el tick+1 y se fusiona en el momento que
                    // cierra (regla 4 corregida): "antes" no la tiene todavía y "después" sí, así que se
                    // descarta de los índices para comparar el mismo suceso narrado.
                    var beforeIndices = WithoutSubstitutions(beforeResult.Events, b.EventIndices);
                    var afterIndices = WithoutSubstitutions(afterResult.Events, a.EventIndices);
                    Assert.True(beforeIndices.SequenceEqual(afterIndices), $"{context}: EventIndices distintos");

                    int beforeLast = beforeIndices.Length > 0
                        ? beforeResult.Trace!.FrameOfTick(beforeResult.Events[beforeIndices[^1]].Tick)
                        : b.LastFrame;
                    int afterLast = afterIndices.Length > 0
                        ? afterResult.Trace!.FrameOfTick(afterResult.Events[afterIndices[^1]].Tick)
                        : a.LastFrame;
                    Assert.Equal(beforeLast, afterLast);
                }

                decisionsCompared++;
                currentSetup = afterSetup;
                currentResult = afterResult;
            }
        }

        _output.WriteLine($"decisiones comparadas en semillas 0..199: {decisionsCompared}");
        Assert.True(decisionsCompared >= 20, $"solo se compararon {decisionsCompared} decisiones, se necesitan al menos 20");
    }

    /// <summary>
    /// e) Sin Trait de Gate: no es la puerta de salida de fase 0, es la banda que fija la medición de la
    /// fase A de la ADR 0119 (8,6 momentos por partido de media, sin Kickoff), más la prueba de que la
    /// fusión hace algo de verdad: bastantes menos momentos que eventos clasificables. 200 partidos con el
    /// patrón del arnés: Parallel.For por índice, catálogo por hilo, reducción en orden después.
    /// </summary>
    [Fact]
    public void MomentCountIsInABand()
    {
        var totals = new int[BandRuns];
        var byLevel = new int[BandRuns][];
        var fullTimeCounts = new int[BandRuns];
        var classifiableCounts = new int[BandRuns];

        Parallel.For(0, BandRuns, i =>
        {
            var catalog = ThreadCatalogs.Current;
            ulong seed = RngStreams.MatchSeed(BandSeed, i);
            var setup = TestMatches.Reference(catalog, seed);
            var result = Simulator.Run(setup, seed, catalog, SimConfig.Default with { Trace = true });
            var moments = MatchMomentView.Build(setup, result, catalog);

            int total = 0;
            var levels = new int[4];
            int fullTime = 0;
            foreach (var moment in moments.Moments)
            {
                if (moment.Kind == MomentKind.Kickoff)
                {
                    continue;
                }

                total++;
                levels[moment.Level - 1]++;
                if (moment.Kind == MomentKind.FullTime)
                {
                    fullTime++;
                }
            }

            int classifiable = 0;
            for (int e = 0; e < result.Events.Count; e++)
            {
                var matchEvent = result.Events[e];
                if (matchEvent.Type != EventType.MatchStart && IsClassifiable(matchEvent))
                {
                    classifiable++;
                }
            }

            totals[i] = total;
            byLevel[i] = levels;
            fullTimeCounts[i] = fullTime;
            classifiableCounts[i] = classifiable;
        });

        double average = totals.Average();
        double averageClassifiable = classifiableCounts.Average();
        var levelAverages = new double[4];
        for (int level = 0; level < 4; level++)
        {
            levelAverages[level] = byLevel.Average(row => row[level]);
        }

        _output.WriteLine($"media de momentos por partido (sin Kickoff, {BandRuns} partidos, semilla {BandSeed}): {average:F2}");
        _output.WriteLine(
            $"media por nivel: N1={levelAverages[0]:F2} N2={levelAverages[1]:F2} N3={levelAverages[2]:F2} N4={levelAverages[3]:F2}");
        _output.WriteLine($"media de eventos clasificables por partido (sin MatchStart): {averageClassifiable:F2}");

        Assert.True(fullTimeCounts.All(c => c == 1), "algún partido no tiene exactamente un momento FullTime");
        Assert.InRange(average, 4.0, 14.0);
        Assert.True(
            average < 0.95 * averageClassifiable,
            $"la fusión no reduce lo bastante: {average:F2} momentos frente a {averageClassifiable:F2} eventos clasificables");
    }

    // ---- Casos de borde de la fusión (regla 2), sobre eventos construidos a mano ----

    [Fact]
    public void FusionWindowBoundaryIsFifteenTicksInclusive()
    {
        var withinWindow = MatchMomentView.Group(
            new[] { Ev(EventType.Foul, tick: 0, actor: 1), Ev(EventType.Foul, tick: 15, actor: 1) },
            Identity, playerTeam: 0, pending: null);
        Assert.Single(withinWindow);
        Assert.Equal(2, withinWindow[0].EventIndices.Count);

        var beyondWindow = MatchMomentView.Group(
            new[] { Ev(EventType.Foul, tick: 0, actor: 1), Ev(EventType.Foul, tick: 16, actor: 1) },
            Identity, playerTeam: 0, pending: null);
        Assert.Equal(2, beyondWindow.Count);
    }

    [Fact]
    public void FusionRequiresSharedPersonOrAChainWithinTheWindow()
    {
        var sharedPerson = MatchMomentView.Group(
            new[] { Ev(EventType.Foul, tick: 0, actor: 1), Ev(EventType.Foul, tick: 5, actor: 1) },
            Identity, playerTeam: 0, pending: null);
        Assert.Single(sharedPerson);

        var noSharedPerson = MatchMomentView.Group(
            new[] { Ev(EventType.Foul, tick: 0, actor: 1), Ev(EventType.Foul, tick: 5, actor: 2) },
            Identity, playerTeam: 0, pending: null);
        Assert.Equal(2, noSharedPerson.Count);
    }

    [Fact]
    public void GoalChainsToFullTimeWithoutSharedPerson()
    {
        var moments = MatchMomentView.Group(
            new[] { Ev(EventType.Goal, tick: 0, actor: 1, detail: "goal"), Ev(EventType.MatchEnd, tick: 5) },
            Identity, playerTeam: 0, pending: null);

        Assert.Single(moments);
        Assert.Equal(MomentKind.FullTime, moments[0].Kind);
        Assert.True(moments[0].HasGoal);
    }

    [Fact]
    public void MobChainsToRefereeLeavesWithoutSharedPerson()
    {
        var moments = MatchMomentView.Group(
            new[] { Ev(EventType.MobStart, tick: 0), Ev(EventType.RefereeLeaves, tick: 5) },
            Identity, playerTeam: 0, pending: null);

        Assert.Single(moments);
        Assert.Equal(MomentKind.RefereeLeaves, moments[0].Kind);
    }

    [Fact]
    public void DeathInjuryAndCardChainToFullTimeWithoutSharedPerson()
    {
        var death = MatchMomentView.Group(
            new[] { Ev(EventType.Death, tick: 0, actor: 1), Ev(EventType.MatchEnd, tick: 5) },
            Identity, playerTeam: 0, pending: null);
        Assert.Single(death);

        var injury = MatchMomentView.Group(
            new[] { Ev(EventType.Injury, tick: 0, actor: 1, detail: "severe"), Ev(EventType.MatchEnd, tick: 5) },
            Identity, playerTeam: 0, pending: null);
        Assert.Single(injury);

        var card = MatchMomentView.Group(
            new[] { Ev(EventType.Card, tick: 0, actor: 1, detail: "yellow"), Ev(EventType.MatchEnd, tick: 5) },
            Identity, playerTeam: 0, pending: null);
        Assert.Single(card);
    }

    [Fact]
    public void ACancelledEventNeverFormsAChain()
    {
        var moments = MatchMomentView.Group(
            new[] { Ev(EventType.Goal, tick: 0, actor: 1, detail: "goal:cancelled"), Ev(EventType.MatchEnd, tick: 5) },
            Identity, playerTeam: 0, pending: null);

        Assert.Equal(2, moments.Count);
    }

    [Fact]
    public void ACancelledEventKeepsItsBaseKindWithCancelledFlagAndLevelOne()
    {
        var moments = MatchMomentView.Group(
            new[] { Ev(EventType.Card, tick: 0, actor: 1, detail: "red:cancelled") },
            Identity, playerTeam: 0, pending: null);

        Assert.Single(moments);
        var moment = moments[0];
        Assert.Equal(MomentKind.Red, moment.Kind);
        Assert.True(moment.Cancelled);
        Assert.Equal(1, moment.Level);
        Assert.False(moment.Pauses);
    }

    [Fact]
    public void SevereInjuryPausesOnlyForThePlayerTeam()
    {
        var ownTeam = MatchMomentView.Group(
            new[] { Ev(EventType.Injury, tick: 0, team: 1, actor: 1, detail: "severe") },
            Identity, playerTeam: 1, pending: null);
        Assert.True(ownTeam[0].Pauses);

        var rivalTeam = MatchMomentView.Group(
            new[] { Ev(EventType.Injury, tick: 0, team: 0, actor: 1, detail: "severe") },
            Identity, playerTeam: 1, pending: null);
        Assert.False(rivalTeam[0].Pauses);
    }

    /// <summary>
    /// La corrección del defecto confirmado: una Injury y la Death del mismo jugador en el mismo tick son
    /// las dos orígenes de la misma decisión (la sustitución responde a las dos), y ahora quedan en UN
    /// solo momento con Decision y el nivel más alto de los dos (4, el de la muerte).
    /// </summary>
    [Fact]
    public void InjuryAndDeathOfTheSamePlayerInTheSameTickFormOneMomentWithDecisionAndLevelFour()
    {
        var events = new[]
        {
            Ev(EventType.Injury, tick: 10, team: 0, actor: 1, detail: "severe"),
            Ev(EventType.Death, tick: 10, team: 0, actor: 1),
            Ev(EventType.Substitution, tick: 11, team: 0, actor: 99, target: 1),
        };

        var moments = MatchMomentView.Group(events, Identity, playerTeam: 0, pending: null);

        Assert.Single(moments);
        var moment = moments[0];
        Assert.True(moment.Decision);
        Assert.Equal(4, moment.Level);
        Assert.Equal(MomentKind.Death, moment.Kind);
        Assert.Equal(3, moment.EventIndices.Count);
    }

    [Fact]
    public void AfterTheDecisionTickOnlyItsRespondingSubstitutionCanStillJoin()
    {
        var events = new[]
        {
            Ev(EventType.Injury, tick: 10, team: 0, actor: 1, detail: "severe"),
            Ev(EventType.Substitution, tick: 11, team: 0, actor: 99, target: 1),
            Ev(EventType.Foul, tick: 12, team: 0, actor: 2),
        };

        var moments = MatchMomentView.Group(events, Identity, playerTeam: 0, pending: null);

        Assert.Equal(2, moments.Count);
        Assert.True(moments[0].Decision);
        Assert.Equal(2, moments[0].EventIndices.Count);
        Assert.False(moments[1].Decision);
        Assert.Single(moments[1].EventIndices);
    }

    /// <summary>Marcas (regla 5): corte del saque, absorción por el TRAMO del momento (no solo su primer fotograma), y fuera de rango.</summary>
    [Fact]
    public void AbsorptionUsesTheWholeSpanOfTheMoment()
    {
        var events = new[] { Ev(EventType.Foul, tick: 20, actor: 5), Ev(EventType.Foul, tick: 30, actor: 5) };
        var moments = MatchMomentView.Group(events, Identity, playerTeam: 0, pending: null);
        Assert.Single(moments);
        Assert.Equal(20, moments[0].Frame);
        Assert.Equal(30, moments[0].LastFrame);

        var beforeCutoff = new MatchFlash(Frame: 5, Player: 0, PlayerId: 5, Team: 0, PerkId: "p", Name: "p");

        // 40 está a 20 de Frame (fuera de +-15 desde el primer fotograma) pero a solo 10 de LastFrame: solo
        // se absorbe si se mira el tramo completo, no solo el primer fotograma del momento.
        var nearLastFrame = new MatchFlash(Frame: 40, Player: 0, PlayerId: 5, Team: 0, PerkId: "p", Name: "p");
        var outside = new MatchFlash(Frame: 50, Player: 0, PlayerId: 5, Team: 0, PerkId: "p", Name: "p");

        var marks = MatchMomentView.Absorb(
            new[] { beforeCutoff, nearLastFrame, outside }, Identity, events, moments, cutoffTick: 10);

        Assert.Equal(2, marks.Count);
        Assert.Equal(nearLastFrame, marks[0].Flash);
        Assert.Equal(0, marks[0].MomentIndex);
        Assert.Equal(outside, marks[1].Flash);
        Assert.Equal(-1, marks[1].MomentIndex);
    }

    /// <summary>
    /// Tabla explícita de tipos narrables (regla 1), sin reproducir la lógica de detalles de la
    /// clasificación (Card/Injury/Goal) salvo el sufijo ":cancelled" — no es una llamada a la
    /// clasificación privada, es la lista de tipos que el propio encargo describe como narrables.
    /// </summary>
    private static bool IsClassifiable(MatchEvent matchEvent)
    {
        if (matchEvent.Detail.EndsWith(":cancelled", StringComparison.Ordinal))
        {
            return true;
        }

        return matchEvent.Type switch
        {
            EventType.MatchStart => true,
            EventType.Foul => true,
            EventType.ConsumableUsed => true,
            EventType.Substitution => true,
            EventType.Card => true,
            EventType.Injury => true,
            EventType.Goal => true,
            EventType.MobStart => true,
            EventType.RefereeLeaves => true,
            EventType.Death => true,
            EventType.MatchEnd => true,
            _ => false,
        };
    }

    private static int FindMatchStartTick(IReadOnlyList<MatchEvent> events)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Type == EventType.MatchStart)
            {
                return events[i].Tick;
            }
        }

        return 0;
    }

    private static bool ContainsPlayer(IReadOnlyList<MatchEvent> events, MatchMoment moment, int playerId)
    {
        for (int i = 0; i < moment.EventIndices.Count; i++)
        {
            var candidate = events[moment.EventIndices[i]];
            if (candidate.Actor == playerId || candidate.Target == playerId || candidate.Opponent == playerId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Los índices del momento cuyo evento no es una sustitución (para comparar (d) ignorando la que acaba de entrar).</summary>
    private static int[] WithoutSubstitutions(IReadOnlyList<MatchEvent> events, IReadOnlyList<int> indices)
    {
        var kept = new List<int>(indices.Count);
        for (int i = 0; i < indices.Count; i++)
        {
            if (events[indices[i]].Type != EventType.Substitution)
            {
                kept.Add(indices[i]);
            }
        }

        return kept.ToArray();
    }

    /// <summary>Evento sintético con solo los campos que la fusión mira; el resto queda en su valor por defecto.</summary>
    private static MatchEvent Ev(
        EventType type, int tick, int team = 0, int actor = -1, int target = -1, int opponent = -1, string detail = "") =>
        new(type, tick, team, actor, target, opponent, default, default, default, 0, 0, detail);

    private static MatchMoment HandmadeMoment(
        MomentKind kind, int level, bool pauses, bool decision, bool cancelled = false, bool hasGoal = false) => new(
        Frame: 100,
        FreezeFrame: 99,
        LastFrame: 100,
        Level: level,
        Kind: kind,
        Team: 0,
        LeadPlayerId: 1,
        Pauses: pauses,
        Decision: decision,
        EventIndices: new[] { 0 },
        Cancelled: cancelled,
        HasGoal: hasGoal);

    private static void AssertPresentation(MatchMoment moment, int speed, bool shown, bool compressed, bool pauses)
    {
        var presentation = MatchMomentView.Present(moment, speed);
        Assert.Equal(shown, presentation.Shown);
        Assert.Equal(compressed, presentation.Compressed);
        Assert.Equal(pauses, presentation.Pauses);
    }
}
