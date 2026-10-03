using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
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
        // Sobre el motor sin arranque (ADR 0185): con el arranque, dos que se disputan un punto se empujan más despacio y la
        // sonda ve menos episodios (medido: 274 en 60 partidos, bajo el piso de 300); el control valida el instrumento
        // contra el baile de BB-K, que se midió sin él.
        var off = Measure(AccelerationTests.WithAccel(WithCoverSpacing(0f), 0), Matches);
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

    /// <summary>
    /// Tres compañeros con zonas solapadas y el mismo punto bruto: se reparten por id ascendente (como
    /// <c>Marking</c>, RT-041). El de id menor conserva su punto aunque los demás decidan después, y cada uno
    /// queda a la separación de los datos de los de id menor; el orden en que se llama a decidir no cambia el
    /// resultado del de id menor.
    /// </summary>
    [Fact]
    public void ThreeTeammatesShareTheCoverLineInIdOrder()
    {
        float spacing = Catalog.Ai.Context.CoverSpacingCells;
        var players = new[]
        {
            Defender(1, new Cell(2, 2)),
            Defender(2, new Cell(2, 3)),
            Defender(3, new Cell(2, 4)),
            OpponentCarrier(),
        };
        for (int i = 0; i < players.Length; i++)
        {
            players[i].Index = i;
            players[i].Position = players[i].HomeCenter;
        }

        var ball = new Ball { InterceptAttempted = new bool[players.Length], Position = new Vec2(12f, 3.5f) };
        ball.Owner = players[3];
        players[3].Position = ball.Position;
        var context = new UtilityContext(players, ball, Catalog.Ai, Catalog.Tuning.ActionZone, Catalog.Tuning.Pass.InterceptRadiusCells);
        context.TacticalStates[0] = TacticalState.OutOfPossession;
        context.TacticalStates[1] = TacticalState.InPossession;
        context.HoldingTeam = 1;
        context.Carrier[1] = players[3];
        context.NearestToBall[0] = players[0];
        context.NearestToBall[1] = players[3];

        foreach (int index in new[] { 0, 1, 2 })
        {
            Assert.Equal(PlayerAction.CoverSpace, Utility.Choose(context, players[index], null));
        }

        var first = players[0].TargetPoint;
        var second = players[1].TargetPoint;
        var third = players[2].TargetPoint;
        Assert.True(Vec2.Distance(first, second) >= spacing - 0.06f, $"1-2: {Vec2.Distance(first, second)}");
        Assert.True(Vec2.Distance(first, third) >= spacing - 0.06f, $"1-3: {Vec2.Distance(first, third)}");
        Assert.True(Vec2.Distance(second, third) >= spacing - 0.06f, $"2-3: {Vec2.Distance(second, third)} pts {first} {second} {third}");

        // El de id menor no mira a los mayores: decidir otra vez después de ellos no le mueve el punto.
        Assert.Equal(PlayerAction.CoverSpace, Utility.Choose(context, players[0], null));
        Assert.Equal(first, players[0].TargetPoint);

        // Y decidir en otro orden (el 3 antes que el 2) no cambia lo que cubre el 1 ni deja a nadie encima.
        Assert.Equal(PlayerAction.CoverSpace, Utility.Choose(context, players[2], null));
        Assert.Equal(PlayerAction.CoverSpace, Utility.Choose(context, players[1], null));
        Assert.Equal(first, players[0].TargetPoint);
        Assert.True(Vec2.Distance(players[1].TargetPoint, players[2].TargetPoint) >= spacing - 0.06f);
    }

    private static MatchPlayer Defender(int id, Cell home) => Make(id, Position.Defender, home, 0);

    private static MatchPlayer OpponentCarrier() => Make(101, Position.Forward, new Cell(12, 3), 1);

    private static MatchPlayer Make(int id, Position position, Cell home, int team)
    {
        var definition = new PlayerDefinition(
            id, "p" + id, Race.Human, position, Rarity.Common, 1, new Attributes(50, 50, 50, 50, 50),
            Array.Empty<Trait>(), new[] { position.ToString() }, PhysicalState.Healthy);
        return new MatchPlayer(definition, team, home, Catalog);
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
