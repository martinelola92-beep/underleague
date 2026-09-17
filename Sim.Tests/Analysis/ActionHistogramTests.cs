using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Tanda 0 del catálogo conceptual (`docs/analisis/catalogo-conceptual-fase-a.md`, "Orden de trabajo que
/// se deduce"): el histograma de acción elegida por jugador/partido, medido contra el control emparejado
/// de la ADR 0087 (mismas plantillas, mismas semillas de partido, con el perk y sin él). Es el instrumento
/// que hace falta **antes** de tocar `modifyUtility` (C1): sin él, "esto cambia la conducta" es una
/// opinión, no una medida — y las tres pruebas de validación de la Fase C del mismo documento dependen de
/// que exista.
///
/// <para><b>No cambia comportamiento.</b> Solo lee <see cref="MatchTrace.ActionAt"/>, que ya existe (RT-098)
/// y ya está verificado como de solo lectura (RT-024: un partido con traza y sin ella es el mismo
/// partido). No se toca ningún peso, perk, tope ni el motor.</para>
///
/// <para><b>Calibración, no medición de un candidato de C1.</b> Se mide sobre cuatro perks ya existentes
/// en el catálogo actual, elegidos porque el propio documento predice su relación: dos que **no deberían**
/// cambiar el histograma en absoluto (`bulwark_stance`, `own_third_anchor` — los dos son
/// `modifyProbability` sobre `tackle`, que no toca la tabla de utilidad) y dos que sí podrían
/// (`sweeper_keeper`, que extiende la correa y por tanto la penalización de zona que sí entra en la
/// utilidad; `iron_gate`, que cancela una lesión y por tanto puede mantener decidiendo a un jugador que de
/// otro modo dejaría de hacerlo). El objetivo es fijar qué distancia L1 es "no ha cambiado nada" antes de
/// usar el mismo instrumento sobre un candidato real de C1 (Tanda 2).</para>
/// </summary>
public sealed class ActionHistogramTests
{
    private const Race NeutralRace = Race.Human;
    private const int Quality = 50;
    private const int Level = 4;

    /// <summary>Plantillas distintas por perk; cada una juega en las dos direcciones (local/visitante).</summary>
    private const int Rosters = 20;

    private const ulong Seed = 1;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public ActionHistogramTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void CalibrateAgainstKnownSimilarAndKnownDifferentPerks()
    {
        var bulwark = Measure("bulwark_stance", perkIndex: 1);
        var anchor = Measure("own_third_anchor", perkIndex: 2);
        var sweeper = Measure("sweeper_keeper", perkIndex: 3);
        var gate = Measure("iron_gate", perkIndex: 4);

        _output.WriteLine("Par 'sabemos iguales' (los dos son modifyProbability sobre tackle, no tocan la utilidad):");
        Report(bulwark);
        Report(anchor);
        _output.WriteLine(string.Empty);
        _output.WriteLine("Par 'sabemos distintas' (modifyLeash y cancelEvent, con vía plausible a la utilidad):");
        Report(sweeper);
        Report(gate);

        Assert.True(
            bulwark.MatchesArmed > 0 && anchor.MatchesArmed > 0 && sweeper.MatchesArmed > 0 && gate.MatchesArmed > 0,
            "algún perk de calibración no encontró portador elegible en las plantillas generadas: la calibración no se pudo ejecutar");
    }

    private void Report(PerkHistogramResult r)
    {
        _output.WriteLine(
            $"  {r.PerkId}: L1(con perk vs control)={r.SelfL1:F4}  " +
            $"partidos(con/control)={r.MatchesArmed}/{r.MatchesControl}  ticks(con/control)={r.TicksArmed}/{r.TicksControl}  " +
            $"activaciones={r.Activations} (de {r.MatchesArmed} partidos con el perk puesto)");
        _output.WriteLine($"    con perk:  {Describe(r.Armed)}");
        _output.WriteLine($"    control:   {Describe(r.Control)}");
    }

    private static string Describe(IReadOnlyDictionary<PlayerAction, double> histogram) =>
        string.Join(", ", histogram
            .Where(kv => kv.Value > 0.005)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key}={kv.Value * 100:F1}%"));

    /// <summary>
    /// Un perk, su portador elegible y su histograma de acción elegida (RT-098, vía traza) en dos brazos
    /// sobre las mismas plantillas y las mismas semillas de partido (ADR 0087): con el perk y sin él
    /// (control). Ninguna campaña se arrastra entre partidos (a diferencia de `PerkValueRunner`): para
    /// calibrar el instrumento basta con partidos independientes, y así Tanda 0 no depende de la mecánica
    /// de campaña para decidir nada.
    /// </summary>
    private readonly record struct PerkHistogramResult(
        string PerkId,
        IReadOnlyDictionary<PlayerAction, double> Armed,
        IReadOnlyDictionary<PlayerAction, double> Control,
        double SelfL1,
        int MatchesArmed,
        int MatchesControl,
        long TicksArmed,
        long TicksControl,
        long Activations);

    private static PerkHistogramResult Measure(string perkId, int perkIndex)
    {
        var perk = Catalog.Perks.All.Single(p => string.Equals(p.Id, perkId, StringComparison.Ordinal));
        var race = perk.Race ?? NeutralRace;
        var config = new SimConfig(CollectLog: false, Trace: true);

        var armedCounts = new long[Enum.GetValues<PlayerAction>().Length];
        var controlCounts = new long[armedCounts.Length];
        int matchesArmed = 0, matchesControl = 0;
        long activations = 0;

        for (int roster = 0; roster < Rosters; roster++)
        {
            var subjectRng = RngStreams.Generation(Seed, (perkIndex * 1000) + roster);
            var mirrorRng = RngStreams.Generation(Seed, (perkIndex * 1000) + 500 + roster);
            var subject = TeamGenerator.Generate(ref subjectRng, Catalog, "subject", race, Quality, 1, Level);
            var mirror = TeamGenerator.Generate(ref mirrorRng, Catalog, "mirror", race, Quality, 100001, Level);

            int carrierSlot = EligibleStarter(subject, perk);
            if (carrierSlot < 0)
            {
                continue;
            }

            var armedPlayers = subject.Players.ToList();
            armedPlayers[carrierSlot] = armedPlayers[carrierSlot] with { Perks = new[] { perk.Id } };
            var armed = subject with { Players = armedPlayers };
            int carrierId = armedPlayers[carrierSlot].Id;

            for (int direction = 0; direction < 2; direction++)
            {
                bool subjectAway = direction == 1;
                ulong matchSeed = RngStreams.MatchSeed(Seed, (perkIndex * 100_000) + (roster * 2) + direction);

                var armedResult = Simulator.Run(
                    subjectAway ? new MatchSetup(mirror, armed, Referee) : new MatchSetup(armed, mirror, Referee),
                    matchSeed, Catalog, config);
                if (Accumulate(armedResult.Trace, carrierId, armedCounts))
                {
                    matchesArmed++;
                }

                foreach (var summary in armedResult.Report.PerksSummary)
                {
                    if (string.Equals(summary.PerkId, perk.Id, StringComparison.Ordinal) && summary.OwnerId == carrierId)
                    {
                        activations += summary.Activations;
                    }
                }

                var controlResult = Simulator.Run(
                    subjectAway ? new MatchSetup(mirror, subject, Referee) : new MatchSetup(subject, mirror, Referee),
                    matchSeed, Catalog, config);
                if (Accumulate(controlResult.Trace, carrierId, controlCounts))
                {
                    matchesControl++;
                }
            }
        }

        var armedHistogram = Normalize(armedCounts, out long ticksArmed);
        var controlHistogram = Normalize(controlCounts, out long ticksControl);
        double l1 = L1Distance(armedHistogram, controlHistogram);

        return new PerkHistogramResult(
            perkId, armedHistogram, controlHistogram, l1, matchesArmed, matchesControl, ticksArmed, ticksControl, activations);
    }

    /// <summary>
    /// Suma al histograma cada tick en que el portador está en el campo y ya ha decidido algo
    /// (<see cref="MatchTrace.ActionAt"/> no es null). Solo lectura de la traza: nada de esto vuelve al
    /// motor (RT-024, mismo principio que <c>MatchTraceRecorder.Decided</c>).
    /// </summary>
    private static bool Accumulate(MatchTrace? trace, int carrierId, long[] counts)
    {
        if (trace is null)
        {
            return false;
        }

        int slot = -1;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Id == carrierId)
            {
                slot = i;
                break;
            }
        }

        if (slot < 0)
        {
            return false;
        }

        for (int frame = 0; frame < trace.FrameCount; frame++)
        {
            if (!trace.OnPitchAt(frame, slot))
            {
                continue;
            }

            var action = trace.ActionAt(frame, slot);
            if (action is { } chosen)
            {
                counts[(int)chosen]++;
            }
        }

        return true;
    }

    private static Dictionary<PlayerAction, double> Normalize(long[] counts, out long total)
    {
        total = counts.Sum();
        var histogram = new Dictionary<PlayerAction, double>();
        foreach (var action in Enum.GetValues<PlayerAction>())
        {
            histogram[action] = total > 0 ? (double)counts[(int)action] / total : 0.0;
        }

        return histogram;
    }

    private static double L1Distance(IReadOnlyDictionary<PlayerAction, double> a, IReadOnlyDictionary<PlayerAction, double> b)
    {
        double sum = 0.0;
        foreach (var action in Enum.GetValues<PlayerAction>())
        {
            sum += Math.Abs(a[action] - b[action]);
        }

        return sum;
    }

    /// <summary>Mismo filtro que el juego (<see cref="PerkAssignment.Eligible"/>), sobre los siete titulares.</summary>
    private static int EligibleStarter(TeamSetup team, PerkDefinition perk)
    {
        for (int i = 0; i < 7 && i < team.Players.Count; i++)
        {
            foreach (var candidate in PerkAssignment.Eligible(team.Players[i], Catalog))
            {
                if (string.Equals(candidate.Id, perk.Id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return -1;
    }
}
