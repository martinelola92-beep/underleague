using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BC-A (docs/pendientes/BC-A.md): cuando el rival saca de centro, el equipo que acaba de marcar tiene que
/// estar <b>en su propio campo</b>. Lo reportó el revisor jugando: el goleador se quedaba plantado en campo
/// contrario mientras el otro equipo sacaba.
///
/// <para>Es el hermano de <see cref="GoalCelebrationPositionTests"/> y la otra mitad del mismo defecto.
/// Aquel prohíbe teletransportar al que celebra; éste exige que, aun sin teletransporte, acabe volviendo.
/// Sin los dos a la vez, cualquiera de los dos arreglos rompe el otro: no saltar es fácil si nadie te pide
/// que vuelvas, y volver es fácil si se te permite saltar.</para>
/// </summary>
public sealed class KickoffFormationTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Media casilla de gracia sobre la línea de medio campo: la tolerancia con la que la reanudación da
    /// a un jugador por colocado (<c>restart.inPlaceCells</c>) más el paso de un tick. Lo que este test
    /// caza no es medio metro, es un jugador entero plantado en el área rival.
    /// </summary>
    private const float HalfwayToleranceCells = 0.9f;

    /// <summary>
    /// Paso máximo de un jugador en un tick. Misma cota que <see cref="GoalCelebrationPositionTests"/>:
    /// el paso real observado es 0,21 casillas/tick y un teletransporte mide entre 4,6 y 10.
    /// </summary>
    private const float MaxNormalStepCells = 0.6f;

    /// <summary>
    /// Tope real de un desplazamiento en un tick durante el saque de centro, barrera incluida. Medido
    /// sobre 178.953 fotogramas de 40 semillas: **máximo 2,11 casillas** y sólo **48 fotogramas (0,027 %)**
    /// por encima del paso andando.
    ///
    /// <para>No es 0,6 y conviene decir por qué, para que nadie lo "arregle" bajándolo:
    /// <c>EnforceRestartClearance</c> coloca de golpe al rival que está demasiado cerca del balón (BB-B), y
    /// eso sí es un empujón instantáneo — acotado, puntual y **anterior a este trabajo**, anotado como
    /// hermano en <c>docs/pendientes/BC-A.md</c>. Lo que esta cota tiene que cazar es el otro caso: cruzar
    /// el campo en un fotograma, que medía entre 4,6 y 10 casillas.</para>
    /// </summary>
    private const float MaxClearanceStepCells = 2.5f;

    [Fact]
    public void NobodyFromTheScoringTeamIsStillInTheRivalHalfWhenTheKickoffIsTaken()
    {
        int kickoffsChecked = 0;
        int offending = 0;
        var detail = new List<string>();
        int cappedWaits = 0;

        for (ulong seed = 1; seed <= 60; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                // RECOVERY con detalle "kickoff" es el tick en el que el sacador toca el balón: el saque
                // ya se ha producido, así que es el fotograma en el que la formación tiene que estar hecha.
                if (e.Type != EventType.Recovery || e.Detail != "kickoff")
                {
                    continue;
                }

                int frame = trace.FrameOfTick(e.Tick);
                int taker = IndexOf(trace, e.Actor);
                if (frame <= 0 || taker < 0)
                {
                    continue;
                }

                // El TOPE de la espera (restart.kickoffMaxWaitTicks) es una red de seguridad declarada: si
                // salta, la reanudación arranca con quien no haya llegado todavía, y exigir aquí que todos
                // estén colocados sería exigir que el tope no exista. Se excluyen esos saques —y que sean
                // minoría lo comprueba TheKickoffWaitEndsBecauseEveryoneArrives, que es su pareja: sin él,
                // esta exclusión podría tragarse el caso entero.
                int endFrame = frame - 1;
                int startFrame = endFrame;
                var restartPhase = trace.PhaseAt(endFrame);
                while (startFrame > 0 && trace.PhaseAt(startFrame - 1) == restartPhase)
                {
                    startFrame--;
                }

                if (endFrame - startFrame + 1 >= Catalog.Tuning.Restart.KickoffMaxWaitTicks + Catalog.Tuning.Restart.KickoffTicks)
                {
                    cappedWaits++;
                    continue;
                }

                // El saque de centro del arranque del partido no prueba nada: nadie venía de ningún sitio.
                int scoringTeam = 1 - trace.Players[taker].Team;
                if (e.Tick <= 1)
                {
                    continue;
                }

                kickoffsChecked++;
                for (int slot = 0; slot < trace.Players.Count; slot++)
                {
                    if (trace.Players[slot].Team != scoringTeam || !trace.OnPitchAt(frame, slot))
                    {
                        continue;
                    }

                    float column = trace.PositionAt(frame, slot).X;
                    float middle = Pitch.Columns / 2f;
                    bool inRivalHalf = Pitch.AttackDirection(scoringTeam) > 0
                        ? column > middle + HalfwayToleranceCells
                        : column < middle - HalfwayToleranceCells;

                    if (inRivalHalf)
                    {
                        offending++;
                        detail.Add($"semilla {seed} tick {e.Tick}: jugador {trace.Players[slot].Id} estado {trace.StateAt(frame, slot)} col {column:F2}");
                    }
                }
            }
        }

        Assert.True(kickoffsChecked > 0, "ningún saque de centro tras gol en 60 semillas: el test no comprobó nada");
        Assert.True(offending == 0, $"({cappedWaits} saques excluidos por agotar el tope) " + string.Join(" | ", detail));
    }

    /// <summary>
    /// La espera termina porque **llega todo el mundo**, no porque se agote el tope. Sin esto,
    /// <see cref="NobodyFromTheScoringTeamIsStillInTheRivalHalfWhenTheKickoffIsTaken"/> pasaría igual si
    /// <c>EveryoneInPlace()</c> no devolviera <c>true</c> jamás y los saques agotaran los 180 ticks — un
    /// fallo silencioso que dejaría el partido lleno de esperas máximas (revisión independiente, BC-A).
    /// </summary>
    [Fact]
    public void TheKickoffWaitEndsBecauseEveryoneArrives()
    {
        int kickoffs = 0;
        int atTheCap = 0;
        long totalTicks = 0;
        int cap = Catalog.Tuning.Restart.KickoffMaxWaitTicks + Catalog.Tuning.Restart.KickoffTicks;

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Recovery || e.Detail != "kickoff" || e.Tick <= 1)
                {
                    continue;
                }

                int endFrame = trace.FrameOfTick(e.Tick - 1);
                if (endFrame <= 0)
                {
                    continue;
                }

                int startFrame = endFrame;
                var phase = trace.PhaseAt(endFrame);
                while (startFrame > 0 && trace.PhaseAt(startFrame - 1) == phase)
                {
                    startFrame--;
                }

                int length = endFrame - startFrame + 1;
                kickoffs++;
                totalTicks += length;
                if (length >= cap)
                {
                    atTheCap++;
                }
            }
        }

        Assert.True(kickoffs > 0, "ningún saque de centro tras gol en 40 semillas: el test no comprobó nada");
        Assert.True(
            atTheCap * 2 < kickoffs,
            $"{atTheCap} de {kickoffs} saques de centro agotaron el tope de {cap} ticks: la espera no está terminando porque llegue la gente, "
            + "sino por el techo — que existe sólo como red de seguridad");
    }

    /// <summary>
    /// NADIE SALTA durante el saque de centro, **el sacador incluido**. Es la otra mitad que pedía la
    /// revisión independiente: el hermano de BB-C prohíbe teletransportar al que celebra y
    /// <see cref="NobodyFromTheScoringTeamIsStillInTheRivalHalfWhenTheKickoffIsTaken"/> exige que vuelvan,
    /// pero ninguno de los dos miraba al sacador — que seguía cruzando el campo en un fotograma porque su
    /// teletransporte estaba camuflado dentro del de todo el equipo.
    /// </summary>
    [Fact]
    public void NobodyJumpsDuringTheKickoff()
    {
        int framesChecked = 0;
        float maxStep = 0f;
        int over = 0;
        string worst = "";

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Recovery || e.Detail != "kickoff" || e.Tick <= 1)
                {
                    continue;
                }

                int endFrame = trace.FrameOfTick(e.Tick - 1);
                if (endFrame <= 0)
                {
                    continue;
                }

                int startFrame = endFrame;
                var phase = trace.PhaseAt(endFrame);
                while (startFrame > 0 && trace.PhaseAt(startFrame - 1) == phase)
                {
                    startFrame--;
                }

                for (int frame = startFrame + 1; frame <= endFrame; frame++)
                {
                    for (int slot = 0; slot < trace.Players.Count; slot++)
                    {
                        if (!trace.OnPitchAt(frame, slot) || !trace.OnPitchAt(frame - 1, slot))
                        {
                            continue;
                        }

                        float step = Vec2.Distance(trace.PositionAt(frame - 1, slot), trace.PositionAt(frame, slot));
                        if (step > maxStep)
                        {
                            maxStep = step;
                            worst = $"semilla {seed} tick {trace.TickAt(frame)} jugador {trace.Players[slot].Id} estado {trace.StateAt(frame, slot)}";
                        }

                        Assert.True(
                            step <= MaxClearanceStepCells,
                            $"semilla {seed}, tick {trace.TickAt(frame)}: el jugador {trace.Players[slot].Id} saltó {step:F2} casillas "
                            + "durante el saque de centro — eso ya no es la barrera, es un teletransporte");

                        if (step > MaxNormalStepCells)
                        {
                            over++;
                        }

                        framesChecked++;
                    }
                }
            }
        }

        Assert.True(framesChecked > 0, "ningún fotograma de saque de centro en 40 semillas: el test no comprobó nada");

        // Y que sean RAROS, que es lo que distingue un empujón puntual de la barrera de un salto
        // sistemático: el teletransporte del sacador ocurría en TODOS los saques de centro, así que una
        // cota por fotograma sin cota de frecuencia se la habría tragado si el salto hubiera sido corto.
        Assert.True(
            over * 1000 < framesChecked,
            $"{over} de {framesChecked} fotogramas superan el paso normal (máx {maxStep:F2}, peor [{worst}]): "
            + "eso ya no es la barrera empujando de vez en cuando");
    }

    private static int IndexOf(MatchTrace trace, int playerId)
    {
        for (int slot = 0; slot < trace.Players.Count; slot++)
        {
            if (trace.Players[slot].Id == playerId)
            {
                return slot;
            }
        }

        return -1;
    }
}
