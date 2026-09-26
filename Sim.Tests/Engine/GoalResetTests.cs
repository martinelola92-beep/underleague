using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0151 (decisión del revisor): tras un gol, dos segundos de celebración en los que nadie vuelve a
/// casa; al terminar, el motor coloca a todos en un tick —cada uno en su sitio de saque, el sacador sobre
/// el balón, los derribados de pie— y emite <see cref="EventType.TeamsReset"/>; después, la cuenta atrás
/// de siempre, sin esperar a nadie.
/// </summary>
public sealed class GoalResetTests
{
    private const int Seeds = 40;
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Lo que puede moverse un jugador parado en un tick: la separación de cuerpos (§2.1) sigue actuando y
    /// separa a dos que el gol dejó solapados. <b>Medido</b>: 0,06 casillas por tick (semilla 5, jugadores
    /// 4 y 103 a 0,20 en el tick del gol, separados en cuatro ticks y quietos después). Un paso andando mide
    /// 0,13-0,21, así que por debajo de 0,10 no hay nadie caminando.
    /// </summary>
    private const float StillCells = 0.10f;

    [Fact]
    public void EveryGoalIsFollowedByOneResetAfterTheCelebrationAndTheKickoffWithoutWaiting()
    {
        int goals = 0;
        var tuning = Catalog.Tuning;
        int celebration = tuning.States.CelebratingTicks;
        int countdown = tuning.Restart.KickoffTicks;

        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var result = Run(seed);
            var events = result.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type != EventType.Goal || events[i].Detail != "goal")
                {
                    continue;
                }

                int goalTick = events[i].Tick;

                // Dos goles que NO llevan reinicio, y está bien (revisión independiente): el del último tick,
                // tras el que el partido termina, y el que empata en el último tick y abre la turba y el
                // gol de oro en ese mismo tick —ese saque es el de la turba (ADR 0151 punto 4), no el de
                // tras gol—.
                if (events.Any(e => e.Tick >= goalTick && e.Tick < goalTick + celebration
                        && e.Type is EventType.MatchEnd or EventType.MobStart))
                {
                    continue;
                }

                goals++;
                var reset = events.Skip(i + 1).FirstOrDefault(e => e.Type == EventType.TeamsReset);
                var kickoff = events.Skip(i + 1).FirstOrDefault(e => e.Type == EventType.Recovery && e.Detail == "kickoff");

                Assert.True(reset is not null, $"semilla {seed}: gol en el tick {goalTick} sin reinicio");
                Assert.True(kickoff is not null, $"semilla {seed}: gol en el tick {goalTick} sin saque");
                Assert.Equal(celebration, reset!.Tick - goalTick);
                Assert.True(
                    kickoff!.Tick - reset.Tick <= countdown + 1,
                    $"semilla {seed}: el saque tardó {kickoff.Tick - reset.Tick} ticks tras el reinicio; "
                    + $"la cuenta atrás es {countdown} y todos están colocados, así que no debería esperar a nadie");
            }

            // Y al revés: todo reinicio es el de un gol, exactamente una celebración después.
            foreach (var reset in events.Where(e => e.Type == EventType.TeamsReset))
            {
                Assert.Contains(events, e => e.Type == EventType.Goal && e.Detail == "goal" && e.Tick == reset.Tick - celebration);
            }
        }

        Assert.True(goals >= 40, $"sólo {goals} goles en {Seeds} semillas: la prueba no mira casi nada");
    }

    [Fact]
    public void NobodyWalksHomeDuringTheCelebration()
    {
        int framesChecked = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var result = Run(seed);
            var trace = result.Trace!;
            foreach (var goal in result.Events.Where(e => e.Type == EventType.Goal && e.Detail == "goal"))
            {
                var reset = result.Events.First(e => e.Type == EventType.TeamsReset && e.Tick > goal.Tick);

                // Desde el primer paso de la celebración (del fotograma del gol al siguiente) hasta el
                // anterior al reinicio. La primera versión empezaba un paso más tarde y se saltaba justo el
                // tick en el que la barrera empujaba a los del equipo que marcó (revisión independiente).
                for (int frame = trace.FrameOfTick(goal.Tick) + 1; frame < trace.FrameOfTick(reset.Tick); frame++)
                {
                    for (int slot = 0; slot < trace.Players.Count; slot++)
                    {
                        if (!trace.OnPitchAt(frame, slot) || !trace.OnPitchAt(frame - 1, slot))
                        {
                            continue;
                        }

                        float step = Vec2.Distance(trace.PositionAt(frame - 1, slot), trace.PositionAt(frame, slot));
                        Assert.True(
                            step <= StillCells,
                            $"semilla {seed}, tick {trace.TickAt(frame)}: el jugador {trace.Players[slot].Id} "
                            + $"({trace.StateAt(frame, slot)}) se movió {step:F2} casillas durante la celebración");
                        framesChecked++;
                    }
                }
            }
        }

        Assert.True(framesChecked > 0);
    }

    [Fact]
    public void AfterTheResetEveryoneIsInPlaceStandingAndTheTakerIsOnTheBall()
    {
        int resets = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var result = Run(seed);
            var trace = result.Trace!;
            var events = result.Events;
            var goals = events.Where(e => e.Type == EventType.Goal && e.Detail == "goal").ToList();

            foreach (var reset in events.Where(e => e.Type == EventType.TeamsReset))
            {
                resets++;
                int frame = trace.FrameOfTick(reset.Tick);
                var goal = goals.Last(g => g.Tick < reset.Tick);
                int scoringTeam = goal.Team;
                var kickoff = events.First(e => e.Type == EventType.Recovery && e.Detail == "kickoff" && e.Tick > reset.Tick);
                int taker = IndexOf(trace, kickoff.Actor);
                var centre = new Vec2(Pitch.Columns / 2f, PitchConstants.CenterRow);

                Assert.True(
                    Vec2.Distance(trace.PositionAt(frame, taker), centre) < 0.01f,
                    $"semilla {seed}, tick {reset.Tick}: el sacador no está sobre el balón");

                for (int slot = 0; slot < trace.Players.Count; slot++)
                {
                    if (!trace.OnPitchAt(frame, slot))
                    {
                        continue;
                    }

                    var state = trace.StateAt(frame, slot);
                    Assert.False(
                        state is PlayerState.KnockedDown or PlayerState.Celebrating,
                        $"semilla {seed}, tick {reset.Tick}: el jugador {trace.Players[slot].Id} sigue en {state} tras el reinicio");

                    if (slot == taker)
                    {
                        continue;
                    }

                    // Nadie en campo contrario, y el que no saca fuera del círculo central (ADR 0147).
                    int team = trace.Players[slot].Team;
                    int direction = Pitch.AttackDirection(team);
                    float x = trace.PositionAt(frame, slot).X;
                    float middle = Pitch.Columns / 2f;
                    Assert.True(
                        direction > 0 ? x <= middle : x >= middle,
                        $"semilla {seed}, tick {reset.Tick}: el jugador {trace.Players[slot].Id} del equipo {team} "
                        + $"(marcó el {scoringTeam}) está en campo contrario tras el reinicio (x = {x:F2})");

                    // Y ya colocado: entre el reinicio y el saque nadie tiene que andar a ninguna parte.
                    int kickoffFrame = trace.FrameOfTick(kickoff.Tick) - 1;
                    float drift = Vec2.Distance(trace.PositionAt(frame, slot), trace.PositionAt(kickoffFrame, slot));
                    Assert.True(
                        drift <= StillCells,
                        $"semilla {seed}, tick {reset.Tick}: el jugador {trace.Players[slot].Id} anduvo {drift:F2} "
                        + "casillas entre el reinicio y el saque: el reinicio no lo dejó en su sitio");
                }
            }
        }

        Assert.True(resets >= 40, $"sólo {resets} reinicios en {Seeds} semillas");
    }

    /// <summary>
    /// Con la celebración a cero —el esquema lo permite— el reinicio es inmediato, no desaparece: antes de
    /// la revisión independiente se volvía andando sin aviso (regla I, RT-083).
    /// </summary>
    [Fact]
    public void WithoutCelebrationTheResetHappensOnTheGoalTick()
    {
        var catalog = Catalog with { Tuning = Catalog.Tuning with { States = Catalog.Tuning.States with { CelebratingTicks = 0 } } };
        int resets = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default with { Trace = true });
            foreach (var goal in result.Events.Where(e => e.Type == EventType.Goal && e.Detail == "goal"))
            {
                if (result.Events.Any(e => e.Tick == goal.Tick && e.Type is EventType.MatchEnd or EventType.MobStart))
                {
                    continue;
                }

                Assert.Contains(result.Events, e => e.Type == EventType.TeamsReset && e.Tick == goal.Tick);
                resets++;
            }
        }

        Assert.True(resets > 0);
    }

    private static MatchResult Run(ulong seed) =>
        Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true });

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
}
