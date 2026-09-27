using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BO-A (decisión del revisor, 27 sep 2026): <i>«dos rivales se quedan atascados compitiendo por el
/// balón, uno protegiendo y el otro intentando quitárselo, casi todo el partido en la misma jugada»</i>.
///
/// <para>Medido antes del arreglo (200 partidos): 20 tramos de más de 3 s con el mismo portador y el mismo
/// rival pegado, el peor de <b>632 ticks (42 s)</b> sin una sola entrada: el portador protegía porque le
/// apretaban, y el rival pegado elegía «ir a por el balón» (530) por encima de entrar (510). Con las dos
/// reglas de BO-A —a distancia de entrada no se persigue, se entra; y proteger tiene tope—, 5 tramos y el
/// peor de 53 ticks.</para>
/// </summary>
public sealed class StalledDuelTests
{
    private const int Seeds = 100;
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Tope del tramo más largo con el mismo portador y el mismo rival pegado (&lt; 1 casilla), en estas 100
    /// semillas. MEDIDO: 42 con las dos reglas, 53 sólo con el tope de protección, <b>90 sólo con la regla de
    /// ChaseBall</b> y 632 sin ninguna. 60 deja pasar el caso sano y caza que se retire el tope, que es la
    /// regla que corta el atasco (la primera versión usaba 90 y no lo cazaba: revisión independiente).
    /// </summary>
    private const int MaxStallTicks = 60;

    private readonly ITestOutputHelper _output;

    public StalledDuelTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ACarrierAndHisPresserNeverStayLockedTogether()
    {
        int worst = 0;
        string where = "";
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true }).Trace!;
            int start = -1, carrier = -1, presser = -1;
            for (int f = 0; f <= trace.FrameCount; f++)
            {
                int owner = f < trace.FrameCount ? trace.BallOwnerAt(f) : -1;
                int near = owner >= 0 ? NearestOpponent(trace, f, owner) : -1;
                if (owner >= 0 && owner == carrier && near >= 0 && near == presser)
                {
                    continue;
                }

                if (start >= 0 && f - start > worst)
                {
                    worst = f - start;
                    where = $"semilla {seed}, tick {trace.TickAt(start)}";
                }

                start = owner >= 0 && near >= 0 ? f : -1;
                carrier = owner;
                presser = near;
            }
        }

        _output.WriteLine($"tramo más largo con el mismo portador y el mismo rival pegado: {worst} ticks ({where})");
        Assert.True(worst <= MaxStallTicks, $"{worst} ticks con el mismo portador y el mismo rival pegado ({where}): el atasco ha vuelto");
    }

    /// <summary>
    /// La primera regla de BO-A, por separado: a distancia de entrada del poseedor rival no se persigue el
    /// balón. En la traza la acción es la de la última decisión (cada <c>decisionIntervalTicks</c>), así
    /// que se mira con un paso andando de margen (0,21) para no contar a quien decidió estando más lejos.
    /// MEDIDO (30 semillas): 17,5 % de esos fotogramas persiguiendo sin la regla, 2,7 % con ella —el resto
    /// son decisiones de un tick antes, con el balón todavía suelto, que la traza arrastra—. El 5 % separa
    /// los dos con margen.
    /// </summary>
    [Fact]
    public void NobodyChasesABallThatARivalCarriesWithinTackleRange()
    {
        float reach = Catalog.Ai.Context.TackleDistanceMaxCells - 0.21f;
        int frames = 0, chasing = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true }).Trace!;
            for (int f = 1; f < trace.FrameCount; f++)
            {
                int owner = trace.BallOwnerAt(f);
                if (owner < 0 || trace.BallOwnerAt(f - 1) != owner)
                {
                    continue;
                }

                for (int i = 0; i < trace.Players.Count; i++)
                {
                    if (trace.Players[i].Team == trace.Players[owner].Team || !trace.OnPitchAt(f, i)
                        || Vec2.Distance(trace.PositionAt(f - 1, i), trace.PositionAt(f - 1, owner)) > reach)
                    {
                        continue;
                    }

                    frames++;
                    if (trace.ActionAt(f, i) == PlayerAction.ChaseBall && trace.StateAt(f, i) == PlayerState.Chasing)
                    {
                        chasing++;
                    }
                }
            }
        }

        _output.WriteLine($"{frames} fotogramas con un rival a distancia de entrada del portador · persiguiendo: {chasing}");
        Assert.True(frames > 1000);
        Assert.True(chasing * 100 <= frames * 5, $"{chasing} de {frames} fotogramas persiguiendo el balón encima del portador");
    }

    private static int NearestOpponent(MatchTrace trace, int f, int owner)
    {
        int near = -1;
        float best = 1f;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Team == trace.Players[owner].Team || !trace.OnPitchAt(f, i))
            {
                continue;
            }

            float d = Vec2.Distance(trace.PositionAt(f, i), trace.PositionAt(f, owner));
            if (d < best)
            {
                best = d;
                near = i;
            }
        }

        return near;
    }
}
