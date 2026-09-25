using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// ¿A qué velocidades va el balón, y se distinguen entre sí?
///
/// <para>Existe antes de dibujar ningún rastro. Un rastro que sale siempre es ruido y uno que no sale
/// nunca es código muerto: lo que lo convierte en información es que haya un umbral por encima del cual
/// pasan pocas cosas y sean las interesantes. Ese umbral tiene que salir de la distribución real
/// (regla H de <c>CLAUDE.md</c>: ningún número sin procedencia), no de lo que a mí me parezca rápido.</para>
///
/// <para>Se mide sobre la TRAZA, que es lo que la pantalla tiene delante (RT-014): la diferencia de
/// posición del balón entre dos fotogramas consecutivos, en casillas por tick.</para>
/// </summary>
public sealed class BallSpeedCensusTests
{
    private const int Quality = 50;
    private const int Level = 4;
    private const int Matches = 8;

    /// <summary>La velocidad más alta que el motor le da al balón: `tuning.ball.shotSpeedCellsPerTickMilli`.</summary>
    private const float MaxLegitimateSpeed = 0.700f;
    private const ulong Seed = 1;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public BallSpeedCensusTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void HowFastDoesTheBallActuallyTravel()
    {
        var speeds = new List<float>();
        long moving = 0, still = 0;

        for (int match = 0; match < Matches; match++)
        {
            var homeRng = RngStreams.Generation(Seed, match);
            var awayRng = RngStreams.Generation(Seed, 500 + match);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, Quality, 100001, Level);

            var result = Simulator.Run(
                new MatchSetup(home, away, Referee),
                RngStreams.MatchSeed(Seed, match),
                Catalog,
                new SimConfig(CollectLog: false, Trace: true));

            var trace = result.Trace!;
            for (int f = 1; f < trace.FrameCount; f++)
            {
                var a = trace.BallAt(f - 1);
                var b = trace.BallAt(f);
                float dx = b.X - a.X;
                float dy = b.Y - a.Y;
                float d = MathF.Sqrt((dx * dx) + (dy * dy));

                // El teletransporte de una reanudación no es velocidad: el balón aparece en otro sitio.
                // El corte sale de `tuning`, no de un número a ojo: la velocidad de DISPARO (0,700 c/t)
                // es la más alta que el motor le da al balón —pase 0,250, parada 0,260, cabezazo 0,320—,
                // así que cualquier cosa por encima es un salto, no una velocidad.
                if (d > MaxLegitimateSpeed)
                {
                    continue;
                }

                if (d < 0.001f)
                {
                    still++;
                    continue;
                }

                moving++;
                speeds.Add(d);
            }
        }

        speeds.Sort();
        _output.WriteLine($"{Matches} partidos · {moving} fotogramas con el balón en movimiento, {still} quieto "
            + $"({100.0 * moving / (moving + still):0.0} % en movimiento)");
        _output.WriteLine("");
        _output.WriteLine("velocidad del balón, casillas por tick (percentiles de los fotogramas en movimiento)");
        foreach (int p in new[] { 10, 25, 50, 75, 90, 95, 98, 99 })
        {
            _output.WriteLine($"  p{p,-3} {speeds[speeds.Count * p / 100]:0.000}");
        }

        _output.WriteLine($"  max  {speeds[^1]:0.000}");
        _output.WriteLine("");

        // Cuántos fotogramas de PARTIDO superarían cada umbral: es lo que decide si un rastro es raro.
        long total = moving + still;
        foreach (float umbral in new[] { 0.15f, 0.20f, 0.25f, 0.30f, 0.40f, 0.50f })
        {
            int over = speeds.Count(s => s >= umbral);
            _output.WriteLine($"  umbral {umbral:0.00} · {over} fotogramas ({100.0 * over / total:0.00} % del partido)"
                + $" · {(double)over / Matches:0.0} por partido");
        }

        // Y lo que decide si un rastro es información o adorno: ¿QUÉ produce los balones rápidos? Si el
        // 0,40 lo cruzan pases cualesquiera, un rastro ahí no dice nada. Si lo cruzan disparos, dice
        // «ese balón iba fuerte», que es exactamente lo que se quiere enseñar.
        _output.WriteLine("");
        _output.WriteLine("qué suceso precede a un balón rápido (>= 0,40 c/t), por el último evento del fotograma o anterior");
        var porEvento = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int enVuelo = 0, rapidos = 0;

        for (int match = 0; match < Matches; match++)
        {
            var homeRng = RngStreams.Generation(Seed, match);
            var awayRng = RngStreams.Generation(Seed, 500 + match);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, Quality, 100001, Level);
            var result = Simulator.Run(
                new MatchSetup(home, away, Referee), RngStreams.MatchSeed(Seed, match), Catalog,
                new SimConfig(CollectLog: false, Trace: true));
            var trace = result.Trace!;
            var events = result.Events;

            string ultimo = "—";
            int cursor = 0;
            for (int f = 1; f < trace.FrameCount; f++)
            {
                int to = trace.EventFromAt(f) + trace.EventCountAt(f);
                for (; cursor < to && cursor < events.Count; cursor++)
                {
                    var t = events[cursor].Type;
                    if (t is not (EventType.PerkTriggered or EventType.PlayStart or EventType.PlayEnd))
                    {
                        ultimo = EventTypeNames.ToUpperSnake(t);
                    }
                }

                var a0 = trace.BallAt(f - 1);
                var b0 = trace.BallAt(f);
                float d = MathF.Sqrt(((b0.X - a0.X) * (b0.X - a0.X)) + ((b0.Y - a0.Y) * (b0.Y - a0.Y)));
                if (d < 0.40f || d > MaxLegitimateSpeed)
                {
                    continue;
                }

                rapidos++;
                if (trace.BallInFlightAt(f))
                {
                    enVuelo++;
                }

                porEvento[ultimo] = porEvento.GetValueOrDefault(ultimo) + 1;
            }
        }

        _output.WriteLine($"  en vuelo: {100.0 * enVuelo / rapidos:0.0} % de los {rapidos} fotogramas rápidos");
        foreach (var (k, v) in porEvento.OrderByDescending(e => e.Value).Take(8))
        {
            _output.WriteLine($"  {k,-18} {v,5}  ({100.0 * v / rapidos:0.0} %)");
        }

        Assert.NotEmpty(speeds);
    }
}
