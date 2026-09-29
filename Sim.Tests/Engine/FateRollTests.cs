using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0171, la tirada del destino: el motor anuncia con <c>FATE_ROLL</c> la probabilidad real de las
/// tiradas graves o letales, salvadas incluidas, sin cambiar un solo dado (RT-021/RT-024) y sin volverse
/// una pausa en cada partido (RF-012d, ver el umbral <see cref="MatchEngine.FateRollMinSevereBasisPoints"/>).
/// </summary>
public sealed class FateRollTests
{
    private const int Seeds = 200;
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MatchResult Run(ulong seed) =>
        Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default);

    /// <summary>
    /// Desgaste de acto tardío (ADR 0043) forzado al ×6: en un partido suelto ninguna lesión grave llega al
    /// umbral (medido: ~1,4 % en el peor emparejamiento), así que para ver los sucesos hay que subir la escala.
    /// </summary>
    private static MatchResult RunBrutal(ulong seed) =>
        Simulator.Run(TestMatches.Brutal(Catalog), seed, Catalog, SimConfig.Default with { InjuryScalePercent = 600 });

    [Fact]
    public void EveryFateRollThatHitIsFollowedByItsInjuryOrDeathInTheSameTick()
    {
        int hits = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var events = RunBrutal(seed).Events;
            for (int i = 0; i < events.Count; i++)
            {
                var roll = events[i];
                if (roll.Type != EventType.FateRoll || !roll.Detail.EndsWith(":hit", StringComparison.Ordinal))
                {
                    continue;
                }

                hits++;
                Assert.Contains(events.Skip(i + 1).TakeWhile(e => e.Tick == roll.Tick), e =>
                    e.Actor == roll.Actor && e.Type is EventType.Injury or EventType.Death);
            }
        }

        Assert.True(hits > 0, "en 200 partidos tendría que haber alguna tirada del destino que acabe en desgracia");
    }

    [Fact]
    public void AnnouncedRollsAreWellFormedAboveTheThresholdAndSomeAreSaved()
    {
        int saved = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            foreach (var roll in RunBrutal(seed).Events.Where(e => e.Type == EventType.FateRoll))
            {
                var parts = roll.Detail.Split(':');
                Assert.Equal(3, parts.Length);
                Assert.True(int.Parse(parts[1]) >= (parts[0] == "death"
                    ? MatchEngine.FateRollMinDeathBasisPoints
                    : MatchEngine.FateRollMinSevereBasisPoints));
                Assert.Contains(parts[0], new[] { "severe", "death" });
                Assert.Contains(parts[2], new[] { "hit", "saved" });
                if (parts[2] == "saved")
                {
                    saved++;
                }
            }
        }

        Assert.True(saved > 0, "el «se salva» tiene que emitirse: sin él la tirada sólo se vería cuando sale mal");
    }

    [Fact]
    public void FateMomentsAreRarePerMatch()
    {
        int total = 0;
        int severe = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var rolls = Run(seed).Events.Where(e => e.Type == EventType.FateRoll).ToList();
            total += rolls.Count;
            severe += rolls.Count(e => e.Detail.StartsWith("severe", StringComparison.Ordinal));
        }

        // Partido de referencia, los dos equipos juntos: tope de ~1 por partido (encargo, ADR 0171). La medida
        // que manda es la de la run completa (columna fateMoments de runs.csv), que es la que juega el jugador.
        Assert.True(total <= Seeds, $"{total} tiradas del destino ({severe} graves) en {Seeds} partidos: hay que subir el umbral");
    }
}
