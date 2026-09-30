using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BB-K (docs/pendientes/BB-K.md, ADR 0176): dos compañeros que quieren la misma casilla «bailan y
/// parpadean». <c>CoverSpace</c> era la única acción de colocación que no miraba a los compañeros, así que
/// dos jugadores con zonas solapadas calculaban el mismo punto y se quedaban uno encima del otro,
/// empujándose por la separación de cuerpos.
///
/// <para><b>El instrumento.</b> Un «baile» es una racha de al menos <see cref="MinReversals"/> inversiones de
/// rumbo consecutivas: el desplazamiento del fotograma tiene signo contrario al anterior, con los dos
/// módulos por encima de <see cref="MinStepCells"/>. Es la definición con la que la ficha midió sus 12.160
/// episodios. <b>Se valida contra un caso cuya respuesta se conoce</b> (Regla J): con la separación apagada
/// (<c>coverSpacingCells = 0</c>, que es el motor de antes, bit a bit) tiene que ver el baile masivo; si no
/// lo viera, que con la separación encendida no vea nada no demostraría nada.</para>
/// </summary>
public sealed class DancingTeammatesTests
{
    private const int MinReversals = 4;
    private const float MinStepCells = 0.02f;

    /// <summary>Partidos por brazo. Medido con 150 (17,3 episodios por partido antes, 0,9 después); 60 basta para separar los dos con margen.</summary>
    private const int Matches = 60;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public DancingTeammatesTests(ITestOutputHelper output) => _output = output;

    /// <summary>Resultado de la sonda sobre un lote de partidos.</summary>
    internal readonly record struct DanceCensus(
        int Matches,
        int Episodes,
        int FramesDancing,
        int EpisodesInCover,
        int EpisodesWithMateUnderOneCell,
        int LongestEpisode,
        int LongestCoverEpisode);

    internal static DanceCensus Measure(Catalog catalog, int matches)
    {
        int episodes = 0, frames = 0, inCover = 0, withMate = 0, longest = 0, longestCover = 0;
        for (int m = 1; m <= matches; m++)
        {
            ulong seed = (ulong)m;
            var trace = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default with { Trace = true }).Trace!;
            int players = trace.Players.Count;
            for (int player = 0; player < players; player++)
            {
                int run = 0;
                int coverFrames = 0;
                int mateFrames = 0;
                for (int f = 2; f <= trace.FrameCount; f++)
                {
                    bool reversed = f < trace.FrameCount
                        && trace.OnPitchAt(f, player) && trace.OnPitchAt(f - 1, player) && trace.OnPitchAt(f - 2, player)
                        && Reverses(trace.PositionAt(f - 2, player), trace.PositionAt(f - 1, player), trace.PositionAt(f, player));

                    if (reversed)
                    {
                        run++;
                        if (trace.ActionAt(f, player) == PlayerAction.CoverSpace)
                        {
                            coverFrames++;
                        }

                        if (HasMateWithin(trace, f, player, 1f))
                        {
                            mateFrames++;
                        }

                        continue;
                    }

                    if (run >= MinReversals)
                    {
                        episodes++;
                        frames += run;
                        longest = Math.Max(longest, run);
                        if (coverFrames * 2 > run)
                        {
                            inCover++;
                            longestCover = Math.Max(longestCover, run);
                        }

                        if (mateFrames * 2 > run)
                        {
                            withMate++;
                        }
                    }

                    run = 0;
                    coverFrames = 0;
                    mateFrames = 0;
                }
            }
        }

        return new DanceCensus(matches, episodes, frames, inCover, withMate, longest, longestCover);
    }

    private static bool Reverses(Vec2 a, Vec2 b, Vec2 c)
    {
        var first = b - a;
        var second = c - b;
        return first.Length > MinStepCells
            && second.Length > MinStepCells
            && ((first.X * second.X) + (first.Y * second.Y)) < 0f;
    }

    private static bool HasMateWithin(MatchTrace trace, int frame, int player, float radius)
    {
        int team = trace.Players[player].Team;
        for (int other = 0; other < trace.Players.Count; other++)
        {
            if (other == player || trace.Players[other].Team != team || !trace.OnPitchAt(frame, other))
            {
                continue;
            }

            if (Vec2.Distance(trace.PositionAt(frame, player), trace.PositionAt(frame, other)) < radius)
            {
                return true;
            }
        }

        return false;
    }

    private static Catalog WithCoverSpacing(float cells) =>
        Catalog with { Ai = Catalog.Ai.WithContext(Catalog.Ai.Context with { CoverSpacingCells = cells }) };

    private void Report(string label, DanceCensus c) =>
        _output.WriteLine(
            $"{label}: partidos {c.Matches} · episodios {c.Episodes} ({(double)c.Episodes / c.Matches:F2} por partido) · " +
            $"fotogramas {c.FramesDancing} · en CoverSpace {c.EpisodesInCover} ({(double)c.EpisodesInCover / c.Matches:F2} por partido) · " +
            $"con compañero <1 casilla {c.EpisodesWithMateUnderOneCell} · más largo {c.LongestEpisode} · más largo en CoverSpace {c.LongestCoverEpisode}");

    /// <summary>
    /// Regla J: el instrumento ve el baile que la ficha describía cuando la separación está apagada. Medido en
    /// 150 partidos: 16,1 episodios por partido con <c>CoverSpace</c> dominante y rachas de hasta 100
    /// fotogramas; con 60 partidos el piso está muy por debajo de lo medido.
    /// </summary>
    [Fact]
    public void TheProbeSeesTheDanceWhenTheSpacingIsOff()
    {
        var off = Measure(WithCoverSpacing(0f), Matches);
        Report("separación apagada", off);

        Assert.True(off.EpisodesInCover >= 5 * Matches, $"el instrumento debía ver el baile con la separación apagada (≥ 5 episodios de cobertura por partido): {off.EpisodesInCover} en {Matches} partidos");
        Assert.True(off.LongestCoverEpisode >= 30, $"y rachas largas: la más larga fue de {off.LongestCoverEpisode} fotogramas");
    }

    /// <summary>
    /// El arreglo: con la separación de los datos reales, el baile de cobertura desaparece. Medido en 150
    /// partidos: 0,48 episodios de cobertura por partido (antes 16,1) y la racha más larga de 6 fotogramas
    /// (antes 100). Los topes dejan holgura de ruido y siguen siendo un orden de magnitud por debajo del
    /// motor de antes.
    /// </summary>
    [Fact]
    public void TwoTeammatesNoLongerDanceOverTheSameCoverPoint()
    {
        var on = Measure(Catalog, Matches);
        Report("separación de los datos", on);

        Assert.True(Catalog.Ai.Context.CoverSpacingCells > 0f, "los datos reales debían traer la separación encendida");
        Assert.True(on.EpisodesInCover <= 2 * Matches, $"la cobertura seguía bailando: {on.EpisodesInCover} episodios en {Matches} partidos (tope 2 por partido)");
        Assert.True(on.LongestCoverEpisode <= 15, $"quedó una racha de cobertura de {on.LongestCoverEpisode} fotogramas (tope 15)");
    }

    /// <summary>
    /// Reparto por id ascendente (calcado de <c>Marking</c>): el de id menor conserva su punto y el de id
    /// mayor se aparta hasta la separación de los datos. Es la propiedad que lo hace estable: si los dos
    /// cedieran, cada uno reaccionaría a la reacción del otro. Con la separación apagada, el mismo
    /// escenario da el mismo punto a los dos —la causa CONFIRMED de BB-K—.
    /// </summary>
    [Fact]
    public void TheHigherIdYieldsTheCoverPointAndTheLowerIdKeepsIt()
    {
        var (lowerAt, higherAt, spacing) = TwoDefendersCovering(Catalog);
        Assert.True(spacing > 0f);
        Assert.True(
            Vec2.Distance(lowerAt, higherAt) >= spacing - 0.06f,
            $"el de id mayor debía quedar a {spacing} casillas del punto del menor, quedó a {Vec2.Distance(lowerAt, higherAt)}");

        var (offLower, offHigher, _) = TwoDefendersCovering(WithCoverSpacing(0f));
        Assert.True(
            Vec2.Distance(offLower, offHigher) < 0.5f,
            $"control: sin separación los dos debían cubrir el mismo punto (causa de BB-K), quedaron a {Vec2.Distance(offLower, offHigher)}");
        Assert.Equal(offLower, lowerAt);
    }

    /// <summary>Los dos defensas de un equipo deciden con el balón en la recta que cruza las dos zonas.</summary>
    private static (Vec2 Lower, Vec2 Higher, float Spacing) TwoDefendersCovering(Catalog catalog)
    {
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        engine.ParkBallForTest(new Vec2(12f, 3.5f));

        int lower = engine.OutfieldIndexForTest(0, 0);
        int higher = engine.OutfieldIndexForTest(0, 1);
        Assert.True(engine.PlayerAtForTest(lower).Id < engine.PlayerAtForTest(higher).Id);
        Assert.Equal(PlayerAction.CoverSpace, engine.ChooseForTest(lower));
        var lowerAt = engine.PlayerAtForTest(lower).TargetPoint;

        Assert.Equal(PlayerAction.CoverSpace, engine.ChooseForTest(higher));
        var higherAt = engine.PlayerAtForTest(higher).TargetPoint;

        // El de id menor no mira al mayor: decidir otra vez no le mueve el punto.
        Assert.Equal(PlayerAction.CoverSpace, engine.ChooseForTest(lower));
        Assert.Equal(lowerAt, engine.PlayerAtForTest(lower).TargetPoint);

        return (lowerAt, higherAt, catalog.Ai.Context.CoverSpacingCells);
    }
}
