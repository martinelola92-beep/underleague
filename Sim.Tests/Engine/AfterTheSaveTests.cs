using System.Text.Json;
using System.Text.Json.Nodes;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BA-J (docs/pendientes/BA-J.md, ADR 0177): tras una parada, el equipo que tiró se repliega y el portero
/// espera unos ticks antes de sacar.
///
/// <para><b>Lo que había, medido (300 partidos de referencia, 664 paradas retenidas).</b> El portero soltaba el
/// balón <b>siempre a los 5 ticks</b> —el armado del pase, sin esperar a nada—, con 1,8 de los seis que
/// acababan de tirar a menos de 4 casillas de él, y con 4,9 de los seis todavía en campo contrario. La pausa
/// no existía: la decisión del portero se tomaba en el mismo tick de atrapar, con el área todavía llena.</para>
///
/// <para><b>El instrumento</b> (Regla J): se valida contra el caso conocido —con la pausa apagada
/// (<c>holdTicks = 0</c>, <c>retreatTicks = 0</c>, el motor de antes) tiene que ver el suelto a los 5 ticks y
/// a los rivales encima—; si no lo viera, que con la pausa puesta vea otra cosa no demostraría nada.</para>
/// </summary>
public sealed class AfterTheSaveTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private readonly ITestOutputHelper _output;

    public AfterTheSaveTests(ITestOutputHelper output) => _output = output;

    private static Catalog WithPause(int holdTicks, int retreatTicks)
    {
        var files = TestData.LoadAllFiles();
        var node = JsonNode.Parse(files["sim/tuning.json"])!;
        node["save"]!["holdTicks"] = holdTicks;
        node["save"]!["retreatTicks"] = retreatTicks;
        files["sim/tuning.json"] = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return DataLoader.FromJson(files);
    }

    /// <summary>Lo que se mide de cada parada retenida, agregado.</summary>
    private readonly record struct Census(int Saves, int Released, int ShortestRelease, double NearKeeperAtRelease, double BeyondHalfAtRelease);

    private static Census Measure(Catalog catalog, int matches)
    {
        int saves = 0;
        int released = 0;
        int shortest = int.MaxValue;
        double near = 0;
        double beyond = 0;

        for (int m = 1; m <= matches; m++)
        {
            ulong seed = (ulong)m;
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            var events = result.Events;
            for (int e = 0; e < events.Count; e++)
            {
                var save = events[e];
                if (save.Type != EventType.Save || save.Detail != "held")
                {
                    continue;
                }

                saves++;
                int release = -1;
                for (int k = e + 1; k < events.Count && events[k].Tick <= save.Tick + 200; k++)
                {
                    if (events[k].Actor == save.Actor && events[k].Type is EventType.PassAttempted or EventType.Clearance)
                    {
                        release = events[k].Tick - save.Tick;
                        break;
                    }
                }

                if (release < 0)
                {
                    continue;
                }

                released++;
                shortest = Math.Min(shortest, release);
                int shooters = 1 - save.Team;
                int frame = trace.FrameOfTick(save.Tick + release);
                int direction = Pitch.AttackDirection(shooters);
                Vec2 keeperAt = default;
                for (int p = 0; p < trace.Players.Count; p++)
                {
                    if (trace.Players[p].Id == save.Actor)
                    {
                        keeperAt = trace.PositionAt(frame, p);
                    }
                }

                for (int p = 0; p < trace.Players.Count; p++)
                {
                    var info = trace.Players[p];
                    if (info.Team != shooters || info.Role == Position.Goalkeeper || !trace.OnPitchAt(frame, p))
                    {
                        continue;
                    }

                    var at = trace.PositionAt(frame, p);
                    if (Vec2.Distance(at, keeperAt) < 4f)
                    {
                        near++;
                    }

                    float own = direction > 0 ? at.X : Pitch.Columns - at.X;
                    if (own > Pitch.Columns / 2f)
                    {
                        beyond++;
                    }
                }
            }
        }

        return new Census(saves, released, released == 0 ? 0 : shortest, near / Math.Max(1, released), beyond / Math.Max(1, released));
    }

    private void Report(string label, Census c) =>
        _output.WriteLine(
            $"{label}: paradas retenidas {c.Saves} · sueltan el balón {c.Released} · el más rápido a los {c.ShortestRelease} ticks · " +
            $"al soltar, del equipo que tiró: {c.NearKeeperAtRelease:F2} a <4 casillas del portero, {c.BeyondHalfAtRelease:F2} en campo contrario");

    /// <summary>Medido con 300 partidos: sin pausa suelta a los 5 ticks (mediana, p10 y p90 = 5) y 1,7 rivales encima.</summary>
    [Fact]
    public void TheProbeSeesTheHotRestartWhenThePauseIsOff()
    {
        var off = Measure(WithPause(0, 0), 100);
        Report("sin pausa", off);

        Assert.True(off.Released >= 100, $"el instrumento debía ver decenas de paradas retenidas en 100 partidos: {off.Released}");
        Assert.True(off.ShortestRelease <= 6, $"sin pausa el portero soltaba a los 5 ticks del armado: el más rápido fue a los {off.ShortestRelease}");
        Assert.True(off.NearKeeperAtRelease >= 1.3, $"sin pausa había ≥ 1,3 rivales a <4 casillas al soltar (medido 1,6-1,8): {off.NearKeeperAtRelease:F2}");
        Assert.True(off.BeyondHalfAtRelease >= 4.5, $"sin pausa casi todo el equipo seguía en campo contrario (medido 4,9 de 6): {off.BeyondHalfAtRelease:F2}");
    }

    /// <summary>El portero espera: ninguna parada retenida se suelta antes de <c>holdTicks</c>, y el equipo que tiró ya no está encima.</summary>
    [Fact]
    public void TheKeeperWaitsAndTheTeamThatShotFallsBack()
    {
        int hold = Catalog.Tuning.Save.HoldTicks;
        int retreat = Catalog.Tuning.Save.RetreatTicks;
        Assert.True(hold > 0 && retreat > 0, "los datos reales debían traer la pausa puesta");

        var on = Measure(Catalog, 100);
        Report("pausa de los datos", on);

        Assert.True(on.Released >= 100, $"debía haber paradas retenidas que medir: {on.Released}");
        Assert.True(on.ShortestRelease >= hold, $"el portero soltó a los {on.ShortestRelease} ticks, antes de la pausa de {hold}");
        Assert.True(on.NearKeeperAtRelease <= 0.8, $"al soltar seguían {on.NearKeeperAtRelease:F2} rivales a <4 casillas (medido 0,4; antes 1,7)");
        Assert.True(on.BeyondHalfAtRelease <= 4.0, $"el equipo que tiró seguía adelantado al soltar: {on.BeyondHalfAtRelease:F2} de 6 en campo contrario (medido ≈ 3,3; antes 4,9)");
    }

    /// <summary>
    /// El repliegue baja las casillas-hogar del equipo que tiró a donde las pondría la orden defensiva
    /// (<c>mentalityShift.Defensive</c>: −1 defensas, −2 medios, −3 delantero) y <b>no toca su orden</b>: la
    /// orden es del jugador y de sus gritos, y la vista de gritos la reconstruye de los eventos (ADR 0166).
    /// El otro equipo no se entera.
    /// </summary>
    [Fact]
    public void TheFallBackDropsTheLinesLikeTheDefensiveOrderAndLeavesTheOrderAlone()
    {
        // Dos motores idénticos, uno con la ventana abierta: así la rampa del bloque táctico, que se mueve en
        // cada llamada, queda igual en los dos y lo único que difiere es el repliegue.
        var control = new MatchEngine(TestMatches.Reference(Catalog, 3), 3, Catalog, SimConfig.Default);
        control.UpdateBlockShift();
        var before = HomesOf(control);

        var engine = new MatchEngine(TestMatches.Reference(Catalog, 3), 3, Catalog, SimConfig.Default);
        var orderBefore = new[] { engine.EffectiveOrder(0), engine.EffectiveOrder(1) };
        engine.StartFallBackForTest(1, 50);
        engine.UpdateBlockShift();
        var after = HomesOf(engine);

        int moved = 0;
        for (int i = 0; i < before.Length; i++)
        {
            var (team, role, x) = before[i];
            var (_, _, xAfter) = after[i];
            if (role == Position.Goalkeeper || team == 0)
            {
                Assert.Equal(x, xAfter);
                continue;
            }

            float expected = Catalog.Ai.MentalityShift(Mentality.Defensive, role) - Catalog.Ai.MentalityShift(Mentality.Neutral, role);
            float direction = Pitch.AttackDirection(team);
            Assert.True(expected < 0f, $"la orden defensiva debía bajar al {role}");
            Assert.Equal(x + (expected * direction), xAfter, 3);
            moved++;
        }

        Assert.True(moved >= 5, $"debían moverse los jugadores de campo del equipo que tiró: {moved}");
        Assert.Equal(orderBefore[0], engine.EffectiveOrder(0));
        Assert.Equal(orderBefore[1], engine.EffectiveOrder(1));
    }

    /// <summary>Un equipo que ya juega defensivo no baja dos veces: el repliegue lleva a todos al mismo sitio.</summary>
    [Fact]
    public void ADefensiveTeamDoesNotFallBackTwice()
    {
        var setup = TestMatches.Reference(Catalog, 3);
        var defensive = setup with { Away = setup.Away with { Order = Mentality.Defensive } };
        var control = new MatchEngine(defensive, 3, Catalog, SimConfig.Default);
        control.UpdateBlockShift();
        var before = HomesOf(control);

        var engine = new MatchEngine(defensive, 3, Catalog, SimConfig.Default);
        engine.StartFallBackForTest(1, 50);
        engine.UpdateBlockShift();
        var after = HomesOf(engine);

        for (int i = 0; i < before.Length; i++)
        {
            Assert.Equal(before[i].X, after[i].X);
        }
    }

    private static (int Team, Position Role, float X)[] HomesOf(MatchEngine engine)
    {
        var homes = new List<(int, Position, float)>();
        for (int i = 0; ; i++)
        {
            MatchPlayer player;
            try
            {
                player = engine.PlayerAtForTest(i);
            }
            catch (IndexOutOfRangeException)
            {
                break;
            }

            homes.Add((player.Team, player.Role, engine.EffectiveHomeForTest(i).X));
        }

        return homes.ToArray();
    }
}
