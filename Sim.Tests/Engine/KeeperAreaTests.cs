using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// El área del portero se cierra cuando él tiene el balón (decisión del revisor, 27 sep 2026): <i>«cuando
/// el portero atrapa un balón todos los jugadores deben alejarse de él; en el fútbol real hay un área y
/// cuando el portero tiene el balón deben salir de ella»</i>. Cerrada = su portero tiene el balón dentro
/// de ella, o su equipo tiene un saque de puerta pendiente. Se sale andando, nadie se teletransporta.
/// </summary>
public sealed class KeeperAreaTests
{
    private const int Seeds = 60;
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public KeeperAreaTests(ITestOutputHelper output) => _output = output;

    /// <summary>Mientras el área está cerrada, nadie que estuviera fuera pasa a estar dentro.</summary>
    [Fact]
    public void NobodyWalksIntoAClosedArea()
    {
        int framesChecked = 0, shoves = 0;
        float maxDepth = 0f;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var trace = Run(seed).Trace!;
            for (int f = 1; f < trace.FrameCount; f++)
            {
                // El área se cierra al final del tick anterior (el motor la mira antes de mover a nadie).
                int area = ClosedArea(trace, f - 1);
                if (area < 0 || ClosedArea(trace, f) != area)
                {
                    continue;
                }

                for (int i = 0; i < trace.Players.Count; i++)
                {
                    if (trace.Players[i].Role == Position.Goalkeeper || !trace.OnPitchAt(f, i) || !trace.OnPitchAt(f - 1, i))
                    {
                        continue;
                    }

                    bool wasOut = !Pitch.IsInArea(trace.PositionAt(f - 1, i), area);
                    float depth = Depth(trace.PositionAt(f, i), area);
                    if (wasOut && depth > 0f)
                    {
                        maxDepth = Math.Max(maxDepth, depth);
                        shoves++;
                    }

                    Assert.False(
                        wasOut && depth > ShoveCells,
                        $"semilla {seed}, tick {trace.TickAt(f)}: el jugador {trace.Players[i].Id} entró {depth:F2} casillas en el área "
                        + $"del equipo {area} con el portero en posesión");
                    framesChecked++;
                }
            }
        }

        _output.WriteLine($"{framesChecked} comprobaciones · {shoves} empujones hacia dentro, máximo {maxDepth:F3} casillas");
        Assert.True(framesChecked > 1000, $"sólo {framesChecked} comprobaciones: la prueba casi no mira nada");
    }

    /// <summary>
    /// Lo que la separación de cuerpos (§2.1) puede meter en el área a alguien que va por fuera pegado al
    /// borde: no es entrar, es un empujón de contacto. MEDIDO (27 sep 2026, 60 semillas): 75 empujones en
    /// 83.513 comprobaciones, como mucho 0,057 casillas. Un paso andando mide 0,13-0,21.
    /// </summary>
    private const float ShoveCells = 0.10f;

    /// <summary>Cuánto está un punto dentro del área de <paramref name="team"/>; 0 si está fuera.</summary>
    private static float Depth(Vec2 p, int team)
    {
        if (!Pitch.IsInArea(p, team))
        {
            return 0f;
        }

        float front = team == 0 ? Pitch.AreaColumns - p.X : p.X - (Pitch.Columns - Pitch.AreaColumns);
        return Math.Min(front, Math.Min(p.Y - Pitch.AreaTop, Pitch.AreaBottom - p.Y));
    }

    /// <summary>
    /// Al sacar de puerta ningún jugador de campo sigue dentro del área: la cuenta atrás (45 ticks) da de
    /// sobra para salir andando. Y cuántos compañeros hay pegados al portero, que era el síntoma.
    /// </summary>
    [Fact]
    public void TheAreaIsEmptyWhenTheGoalKickIsTaken()
    {
        int kicks = 0, inside = 0, glued = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var result = Run(seed);
            var trace = result.Trace!;
            foreach (var e in result.Events.Where(e => e.Type == EventType.Recovery && e.Detail == "goalKick"))
            {
                int f = trace.FrameOfTick(e.Tick);
                int keeper = trace.BallOwnerAt(f);
                if (keeper < 0 || trace.Players[keeper].Role != Position.Goalkeeper)
                {
                    continue;
                }

                kicks++;
                int team = trace.Players[keeper].Team;
                for (int i = 0; i < trace.Players.Count; i++)
                {
                    if (i == keeper || !trace.OnPitchAt(f, i) || trace.Players[i].Role == Position.Goalkeeper)
                    {
                        continue;
                    }

                    var p = trace.PositionAt(f, i);
                    if (Pitch.IsInArea(p, team))
                    {
                        inside++;
                    }

                    if (trace.Players[i].Team == team && Vec2.Distance(p, trace.PositionAt(f, keeper)) < 1f)
                    {
                        glued++;
                    }
                }
            }
        }

        _output.WriteLine($"{kicks} saques de puerta · jugadores de campo dentro del área al sacar: {inside} · "
            + $"compañeros a <1 casilla del portero: {(double)glued / kicks:0.00} por saque (antes 2,41)");
        Assert.True(kicks > 50);
        Assert.Equal(0, inside);
    }

    /// <summary>
    /// El despeje del portero no le vuelve (BN-A, ADR 0152): con la comba de un jugador de campo, un rival
    /// solo lo cabeceaba de vuelta y el portero lo atrapaba otra vez.
    ///
    /// <para>Se cuenta de forma ESTRUCTURAL —quién es el siguiente dueño del balón tras el despeje, tarde lo
    /// que tarde— y no con una ventana de ticks: la primera versión miraba 40 ticks desde el despeje, y el
    /// despeje nuevo tarda 36-44 sólo en volar, así que no podía ver el bucle aunque existiera (revisión
    /// independiente, regla J). Validado contra el caso conocido: con la comba antigua este contador ve 551
    /// vueltas en 150 partidos; con la del portero, 4.</para>
    /// </summary>
    [Fact]
    public void TheKeepersClearanceDoesNotComeBackToHim()
    {
        int clearances = 0, back = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var result = Run(seed);
            var trace = result.Trace!;
            foreach (var e in result.Events.Where(e => e.Type == EventType.Clearance))
            {
                int keeper = IndexOf(trace, e.Actor);
                if (keeper < 0 || trace.Players[keeper].Role != Position.Goalkeeper)
                {
                    continue;
                }

                clearances++;
                int f = trace.FrameOfTick(e.Tick) + 1;
                while (f < trace.FrameCount && trace.BallOwnerAt(f) < 0 && trace.RestartAt(f) == RestartKind.None)
                {
                    f++;
                }

                if (f < trace.FrameCount && trace.BallOwnerAt(f) == keeper)
                {
                    back++;
                }
            }
        }

        _output.WriteLine($"{clearances} despejes del portero · {back} le vuelven (comba antigua: ~3,7 por partido)");
        Assert.True(clearances > 100);
        // Cota con procedencia: el bucle original eran ~220 vueltas en 60 partidos (3,7 por partido); con la
        // comba, 3 (0,05). Con la ADR 0155 (el equipo que saca de puerta sube a buscar hueco y deja al portero
        // más solo) son 13 (0,22), por el mismo mecanismo pero raro. 30 (0,5 por partido) caza el bucle con
        // mucho margen sin confundirlo con eso. La primera cota, 6, no tenía más procedencia que haber visto 0.
        Assert.True(back <= Seeds / 2, $"{back} despejes del portero le vuelven en {Seeds} partidos: el bucle ha vuelto");
    }

    /// <summary>
    /// La comba del portero tiene que dejar el balón fuera del alcance de un salto durante casi toda la
    /// bajada: el vuelo sube como 4·A·t·(1−t), así que por debajo de <c>aerialReachHeightCells</c> sólo
    /// puede estar en el último tramo. Si alguien sube el alcance del salto o baja la comba, el bucle vuelve
    /// sin que falle nada más; esto lo ata.
    /// </summary>
    [Fact]
    public void TheKeepersClearanceStaysAboveAJumpForMostOfItsDescent()
    {
        float peak = Catalog.Tuning.Clear.KeeperPeakHeightCellsMilli / 1000f;
        float reach = Catalog.Tuning.Ball.AerialReachHeightCells;
        const float T = 0.85f;
        float heightAtT = 4f * peak * T * (1f - T);
        Assert.True(
            heightAtT >= reach,
            $"al 85 % del vuelo el despeje del portero va a {heightAtT:F2} casillas y un salto llega a {reach:F2}");
    }

    private static int IndexOf(MatchTrace trace, int playerId)
    {
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Id == playerId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Área cerrada en ese fotograma según la traza, con la misma regla que el motor.</summary>
    private static int ClosedArea(MatchTrace trace, int f)
    {
        if (trace.RestartAt(f) == RestartKind.GoalKick && trace.RestartTakerAt(f) >= 0)
        {
            return trace.Players[trace.RestartTakerAt(f)].Team;
        }

        int owner = trace.BallOwnerAt(f);
        return owner >= 0 && trace.Players[owner].Role == Position.Goalkeeper
            && Pitch.IsInArea(trace.BallAt(f), trace.Players[owner].Team)
            ? trace.Players[owner].Team
            : -1;
    }

    private static MatchResult Run(ulong seed) =>
        Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true });
}
