using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Cierre metodológico de la Fase A: ¿el TERCER canal (sesgo de atributos de la raza sobre la frecuencia
/// de las acciones que disparan un perk) explica de verdad las tres discrepancias de §23.2, o es una
/// explicación cómoda inventada después de ver el resultado?
///
/// <para>La prueba es INDEPENDIENTE del perk: mide la frecuencia bruta de los sucesos disparadores por
/// raza <b>sin equipar ningún perk</b>. Si el canal es real, el orden de razas por frecuencia de suceso
/// debe reproducir el orden de razas por exposición medido en §23.2. Si no lo reproduce, la hipótesis
/// queda refutada para ese caso.</para>
///
/// <para><b>Predicciones escritas antes de ejecutar</b> (§23.2, exposición medida por raza):
/// <list type="bullet">
/// <item><c>charge</c> (TACKLE, portador Defensa): Orc 95 &gt; Dwarf 90 &gt; Undead 78 &gt; Elf 62 = Human 62.
///   Debería reproducirse en ENTRADAS DEL DEFENSA por partido.</item>
/// <item><c>grudge</c> (FOUL, scope=opponent): Orc 52 &gt; Dwarf 20 &gt; Human 12 &gt; Elf 5 = Undead 5.
///   Debería reproducirse en FALTAS DEL RIVAL por partido — no en nada del portador.</item>
/// <item><c>sweeper_keeper</c> (RECOVERY, portador Portero): Dwarf 68 &gt; Orc 62 &gt; Human 58 &gt; Elf 55 &gt; Undead 40.
///   Debería reproducirse en RECUPERACIONES DEL PORTERO por partido.</item>
/// <item><b>Caso negativo</b>: <c>last_ditch</c> comparte disparador (TACKLE) y portador (Defensa) con
///   <c>charge</c>, pero su exposición apenas se mueve (dispersión 15 frente a 32). Si la tasa de entradas
///   varía mucho por raza y aun así <c>last_ditch</c> no se mueve, el canal NO basta por sí solo para
///   predecir la exposición: haría falta además saber si la métrica está saturada.</item>
/// </list></para>
/// </summary>
public sealed class AttributeBiasChannelTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Quality = 50;
    private const int Level = 4;
    private const int Rosters = 20;
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private readonly ITestOutputHelper _output;
    public AttributeBiasChannelTests(ITestOutputHelper output) => _output = output;

    private sealed record RaceRates(
        double DefenderTackles, double AwayFouls, double GoalkeeperRecoveries,
        double DefenderStrength, double DefenderTechnique, double GoalkeeperSpeed, double GoalkeeperLeash);

    /// <summary>Frecuencias brutas por raza, SIN ningún perk equipado: evidencia independiente del perk.</summary>
    private static RaceRates MeasureRates(Race race)
    {
        var config = new SimConfig(CollectLog: false, Trace: false);
        long defenderTackles = 0, awayFouls = 0, keeperRecoveries = 0;
        long strengthSum = 0, techniqueSum = 0, keeperSpeedSum = 0, keeperLeashSum = 0;
        int matches = 0, rostersCounted = 0;

        for (int roster = 0; roster < Rosters; roster++)
        {
            var homeRng = RngStreams.Generation(1, roster);
            var awayRng = RngStreams.Generation(1, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", race, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", race, Quality, 100001, Level);

            int defenderSlot = IndexOfPosition(home, Position.Defender);
            int keeperSlot = IndexOfPosition(home, Position.Goalkeeper);
            if (defenderSlot < 0 || keeperSlot < 0)
            {
                continue;
            }

            rostersCounted++;
            strengthSum += home.Players[defenderSlot].Attributes.Strength;
            techniqueSum += home.Players[defenderSlot].Attributes.Technique;
            keeperSpeedSum += home.Players[keeperSlot].Attributes.Speed;
            keeperLeashSum += home.Players[keeperSlot].Attributes.Leash;

            int defenderId = home.Players[defenderSlot].Id;
            int keeperId = home.Players[keeperSlot].Id;

            for (int direction = 0; direction < 2; direction++)
            {
                ulong seed = RngStreams.MatchSeed(1, (roster * 2) + direction);
                var setup = direction == 1 ? new MatchSetup(away, home, Referee) : new MatchSetup(home, away, Referee);
                var result = Simulator.Run(setup, seed, Catalog, config);
                matches++;

                foreach (var stats in result.Report.Players)
                {
                    if (stats.PlayerId == defenderId)
                    {
                        defenderTackles += stats.Tackles;
                    }
                }

                // Faltas del equipo RIVAL del portador (lo que dispara grudge, scope=opponent).
                int carrierTeam = direction == 1 ? 1 : 0;
                foreach (var stats in result.Report.Players)
                {
                    if (stats.Team != carrierTeam)
                    {
                        awayFouls += stats.Fouls;
                    }
                }

                foreach (var e in result.Events)
                {
                    if (e.Type == EventType.Recovery && e.Actor == keeperId)
                    {
                        keeperRecoveries++;
                    }
                }
            }
        }

        double n = Math.Max(1, matches);
        double r = Math.Max(1, rostersCounted);
        return new RaceRates(
            defenderTackles / n, awayFouls / n, keeperRecoveries / n,
            strengthSum / r, techniqueSum / r, keeperSpeedSum / r, keeperLeashSum / r);
    }

    private static int IndexOfPosition(TeamSetup team, Position position)
    {
        for (int i = 0; i < team.Players.Count; i++)
        {
            if (team.Players[i].Position == position)
            {
                return i;
            }
        }

        return -1;
    }

    [Fact]
    public void RawTriggerRatesByRaceWithNoPerkEquipped()
    {
        _output.WriteLine("raza | entradas_defensa/partido | faltas_rival/partido | recuperaciones_portero/partido | fuerza_def | tecnica_def | velocidad_pt | correa_pt");

        var rates = new Dictionary<Race, RaceRates>();
        foreach (var race in Catalog.Races.Select(r => r.Id))
        {
            var m = MeasureRates(race);
            rates[race] = m;
            _output.WriteLine($"{race,-7} | {m.DefenderTackles,24:F2} | {m.AwayFouls,20:F2} | {m.GoalkeeperRecoveries,30:F2} | {m.DefenderStrength,10:F1} | {m.DefenderTechnique,11:F1} | {m.GoalkeeperSpeed,12:F1} | {m.GoalkeeperLeash,9:F1}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== orden por tasa bruta (mayor a menor) frente al orden por exposición medido en §23.2 ===");
        Report("charge / last_ditch (TACKLE, Defensa)", rates, r => r.DefenderTackles, "Orc>Dwarf>Undead>Elf=Human (charge)");
        Report("grudge (FOUL del rival)", rates, r => r.AwayFouls, "Orc>Dwarf>Human>Elf=Undead");
        Report("sweeper_keeper (RECOVERY del portero)", rates, r => r.GoalkeeperRecoveries, "Dwarf>Orc>Human>Elf>Undead");
    }

    /// <summary>
    /// Caso negativo de §23.3: <c>last_ditch</c> comparte disparador y portador con <c>charge</c>, así que
    /// sufre la MISMA variación de tasa de entradas por raza, y aun así su exposición apenas se mueve. Si
    /// la TASA de activaciones sí se mueve mientras la EXPOSICIÓN no, el problema no es el canal — es que
    /// "fracción de partidos con ≥1 activación" satura y deja de distinguir.
    /// </summary>
    [Theory]
    [InlineData("last_ditch")]
    [InlineData("charge")]
    [InlineData("own_third_anchor")]
    public void ExposureSaturatesWhereTheActivationRateStillMoves(string perkId)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        _output.WriteLine($"{perkId}: raza | exposición (≥1 activación) | activaciones por partido");

        var exposures = new List<double>();
        var perMatch = new List<double>();
        foreach (var race in Catalog.Races.Select(r => r.Id))
        {
            var probe = RaceExposureProbe.Measure(Catalog, perk, race, Rosters, 1);
            if (probe.Matches == 0)
            {
                continue;
            }

            double rate = (double)probe.Activations / probe.Matches;
            exposures.Add(probe.ExposurePercent);
            perMatch.Add(rate);
            _output.WriteLine($"  {race,-7} | {probe.ExposurePercent,6:F1}% | {rate,6:F2}");
        }

        double exposureSpread = exposures.Max() - exposures.Min();
        double rateSpread = perMatch.Count == 0 || perMatch.Min() <= 0 ? double.NaN : perMatch.Max() / perMatch.Min();
        _output.WriteLine($"  dispersión de exposición: {exposureSpread:F0} puntos | razón max/min de la TASA: {rateSpread:F2}x");
        _output.WriteLine("");
    }

    private void Report(string label, Dictionary<Race, RaceRates> rates, Func<RaceRates, double> metric, string predicted)
    {
        var order = rates.OrderByDescending(kv => metric(kv.Value)).Select(kv => $"{kv.Key}={metric(kv.Value):F2}").ToList();
        _output.WriteLine($"{label}");
        _output.WriteLine($"  medido:    {string.Join(" > ", order)}");
        _output.WriteLine($"  predicho:  {predicted}");
    }
}
