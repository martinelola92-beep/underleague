using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Analysis.Detectors;

/// <summary>
/// Regla J: cada detector de <see cref="SymptomDetectors"/> se valida contra un caso <b>positivo construido</b>
/// (la respuesta es «sí, exactamente esto») y uno <b>negativo</b> (la respuesta es «no»), antes de creerse una
/// sola cifra del barrido. Más un control sobre una traza real: una reposición de equipos SÍ tiene que
/// aparecer como salto «explicado», y debe haber ventanas de reanudación que mirar.
/// Sintéticos: milisegundos, sin Godot. Índices: 0 y 7 porteros, 1-2 y 8-9 defensas, 3-4 y 10-11 medios, 5-6 y
/// 12-13 delanteros.
/// </summary>
public sealed class SymptomDetectorsValidationTests
{
    private static MatchEvent Ev(EventType type, int tick, int actor, int opponent = 0, string detail = "", int team = 0)
        => DetectorTrace.Event(type, tick, team, actor, opponent, detail);

    // ---- BB-K ----

    [Fact]
    public void DancePositiveTwoMatesOnTheSameSquareOscillating()
    {
        var t = DetectorTrace.Synthetic(40);
        for (int f = 0; f < 40; f++)
        {
            t.SetPos(f, 1, 5f + ((f % 2) * 0.1f), 2f);
            t.SetPos(f, 2, 5.1f, 2.05f); // compañero encima
        }

        var hits = SymptomDetectors.Dance(t);
        Assert.Single(hits);
        Assert.True(hits[0].Length >= 30, $"racha {hits[0].Length}");
    }

    [Fact]
    public void DanceNegativeSameOscillationWithoutMateAndStraightRun()
    {
        var t = DetectorTrace.Synthetic(40);
        for (int f = 0; f < 40; f++)
        {
            t.SetPos(f, 1, 5f + ((f % 2) * 0.1f), 2f); // oscila, pero su compañero más cercano está a > 1
            t.SetPos(f, 3, 3f + (f * 0.1f), 5f); // carrera recta
            t.SetPos(f, 2, 9f, 6f);              // el compañero, lejos
        }

        Assert.Empty(SymptomDetectors.Dance(t));
    }

    // ---- BC-G ----

    [Fact]
    public void LooseBallPositiveStillBallForFortyFiveTicks()
    {
        var t = DetectorTrace.Synthetic(60);
        for (int f = 0; f < 60; f++)
        {
            t.BallX[f] = f < 10 ? 8f + (f * 0.5f) : 12.5f; // se mueve 10 ticks y se queda quieto
        }

        var hits = SymptomDetectors.LooseBall(t);
        Assert.Single(hits);
        Assert.InRange(hits[0].Length, 45, 50);
    }

    [Fact]
    public void LooseBallNegativeOwnedBallRestartAndMovingBall()
    {
        var t = DetectorTrace.Synthetic(90);
        for (int f = 0; f < 30; f++)
        {
            t.Owner[f] = 3; // con dueño
        }

        for (int f = 30; f < 60; f++)
        {
            t.Restart[f] = RestartKind.Corner; // reanudación: el balón espera a propósito
        }

        for (int f = 60; f < 90; f++)
        {
            t.BallX[f] = 8f + (f * 0.1f); // en movimiento
        }

        Assert.Empty(SymptomDetectors.LooseBall(t));
    }

    // ---- BA-J ----

    private static DetectorTrace SaveHeldTrace(float outfieldX)
    {
        var events = new[] { Ev(EventType.Save, 5, 7, opponent: 5, detail: "held", team: 1) };
        var t = DetectorTrace.Synthetic(20, events);
        for (int f = 5; f < 10; f++)
        {
            t.Owner[f] = 7; // el portero rival retiene 5 ticks
        }

        for (int f = 0; f < 20; f++)
        {
            foreach (int p in new[] { 1, 2, 3, 4 })
            {
                t.SetPos(f, p, outfieldX, 0.5f + p);
            }
        }

        return t;
    }

    [Fact]
    public void NoRetreatPositiveFourNonForwardsStillAdvancedWhenTheKeeperReleases()
    {
        var hits = SymptomDetectors.NoRetreatAfterSave(SaveHeldTrace(12f), out var counts);
        Assert.Single(hits);
        Assert.Equal(4.0, counts[0]);
    }

    [Fact]
    public void NoRetreatNegativeEverybodyBackInTheirHalf()
    {
        Assert.Empty(SymptomDetectors.NoRetreatAfterSave(SaveHeldTrace(3f), out var counts));
        Assert.Equal(0.0, counts[0]);
    }

    // ---- BF-C ----

    [Fact]
    public void ForwardOffBallPositiveAndDefenderNegative()
    {
        var t = DetectorTrace.Synthetic(10, new[]
        {
            Ev(EventType.Tackle, 3, 5, opponent: 101, detail: "offBallMissed"), // delantero 5
            Ev(EventType.Tackle, 4, 1, opponent: 101, detail: "offBallFoul"),   // defensa 1
            Ev(EventType.Tackle, 5, 5, opponent: 101, detail: "won"),           // con balón: no cuenta
        });
        var hits = SymptomDetectors.ForwardOffBallHit(t, out int all);
        Assert.Single(hits);
        Assert.Equal(2, all);
    }

    [Fact]
    public void ForwardTackleChoicePositiveWithNoRivalCarrierAndNegativeAgainstTheCarrier()
    {
        var t = DetectorTrace.Synthetic(50);
        for (int f = 5; f < 15; f++)
        {
            t.Action[t.Slot(f, 5)] = (int)PlayerAction.Tackle;     // balón suelto: «pega» al aire / a su marca
        }

        for (int f = 20; f < 30; f++)
        {
            t.Action[t.Slot(f, 6)] = (int)PlayerAction.Tackle;     // un rival lleva el balón: entrada normal
            t.Owner[f] = 10;
        }

        for (int f = 30; f < 40; f++)
        {
            t.Action[t.Slot(f, 3)] = (int)PlayerAction.Tackle;     // un centrocampista: no es el síntoma
        }

        for (int f = 40; f < 45; f++)
        {
            t.Action[t.Slot(f, 5)] = (int)PlayerAction.Block;      // Block siempre es a quien no lleva el balón
            t.Owner[f] = 10;
        }

        var hits = SymptomDetectors.ForwardTackleChoice(t, out int frames);
        Assert.Equal(2, hits.Count);
        Assert.Equal(15, frames);
    }

    // ---- BB-G2 ----

    private static DetectorTrace LooseBallWithKeeperNearest(bool teammateChases)
    {
        var t = DetectorTrace.Synthetic(40);
        for (int f = 0; f < 40; f++)
        {
            t.BallX[f] = 4f;
            t.BallY[f] = 1f;            // fuera del área propia del portero 0 (x < 2) y quieto
            t.SetPos(f, 0, 4.1f, 1.0f); // el portero, el más cercano al balón
            if (teammateChases)
            {
                t.Action[t.Slot(f, 1)] = (int)PlayerAction.ChaseBall;
            }
        }

        return t;
    }

    [Fact]
    public void GoalkeeperChaserPositiveDeadlockAndNegativeWithAChasingMate()
    {
        var bad = SymptomDetectors.GoalkeeperChaser(LooseBallWithKeeperNearest(false), out _);
        Assert.Single(bad);
        Assert.Empty(SymptomDetectors.GoalkeeperChaser(LooseBallWithKeeperNearest(true), out _));
    }

    [Fact]
    public void GoalkeeperChaserCountsAChaseOutsideTheAreaButNotInside()
    {
        var t = DetectorTrace.Synthetic(10);
        for (int f = 0; f < 10; f++)
        {
            t.Action[t.Slot(f, 0)] = (int)PlayerAction.ChaseBall;
            t.BallX[f] = f < 5 ? 1f : 6f;
            t.BallY[f] = 3.5f; // dentro del área (x<2, y 1,5-5,5) los 5 primeros; fuera los demás
        }

        SymptomDetectors.GoalkeeperChaser(t, out int frames);
        Assert.Equal(5, frames);
    }

    // ---- BN-A ----

    [Fact]
    public void GoalkeeperCrowdPositiveAndNegative()
    {
        var crowd = DetectorTrace.Synthetic(20);
        var calm = DetectorTrace.Synthetic(20);
        for (int f = 5; f < 15; f++)
        {
            crowd.Owner[f] = 0;
            calm.Owner[f] = 0;
            crowd.SetPos(f, 0, 0.5f, 3.5f);
            crowd.SetPos(f, 1, 1f, 3.2f);
            crowd.SetPos(f, 2, 0.8f, 3.9f);
        }

        var hits = SymptomDetectors.GoalkeeperCrowd(crowd);
        Assert.Single(hits);
        Assert.Equal(10, hits[0].Length);
        Assert.Empty(SymptomDetectors.GoalkeeperCrowd(calm));
    }

    // ---- BO-A ----

    [Fact]
    public void CarrierStuckPositiveSeventyTicksAndNegativeWhenTheRivalChanges()
    {
        var stuck = DetectorTrace.Synthetic(100);
        var churn = DetectorTrace.Synthetic(100);
        for (int f = 10; f < 80; f++)
        {
            stuck.Owner[f] = 3;
            churn.Owner[f] = 3;
            stuck.SetPos(f, 10, stuck.X[stuck.Slot(f, 3)] + 0.5f, stuck.Y[stuck.Slot(f, 3)]);
            int rival = 8 + ((f / 20) % 3);
            churn.SetPos(f, rival, churn.X[churn.Slot(f, 3)] + 0.5f, churn.Y[churn.Slot(f, 3)]);
        }

        var hits = SymptomDetectors.CarrierStuck(stuck);
        Assert.Single(hits);
        Assert.Equal(70, hits[0].Length);
        Assert.Empty(SymptomDetectors.CarrierStuck(churn));
    }

    // ---- BB-A / BB-L ----

    [Fact]
    public void TeleportPositiveUnexplainedJumpAndNegativeResetAndWalking()
    {
        var jump = DetectorTrace.Synthetic(30);
        var reset = DetectorTrace.Synthetic(30, new[] { Ev(EventType.TeamsReset, 20, 0) });
        for (int f = 20; f < 30; f++)
        {
            jump.SetPos(f, 4, jump.X[jump.Slot(f, 4)] + 3f, 2f);
            reset.SetPos(f, 4, reset.X[reset.Slot(f, 4)] + 3f, 2f);
        }

        for (int f = 0; f < 30; f++)
        {
            jump.SetPos(f, 3, jump.X[jump.Slot(f, 3)] + (f * 0.15f), 5f); // andar normal: 0,15/tick
        }

        Assert.Single(SymptomDetectors.Teleports(jump, out int e1, out _, out _));
        Assert.Equal(0, e1);
        Assert.Empty(SymptomDetectors.Teleports(reset, out int e2, out _, out _));
        Assert.Equal(1, e2);
    }

    [Fact]
    public void TeleportPositiveInjuredPlayerFliesOffBeforeLeaving()
    {
        var t = DetectorTrace.Synthetic(20);
        for (int f = 10; f < 20; f++)
        {
            t.OnPitch[t.Slot(f, 4)] = false;
        }

        t.SetPos(9, 4, t.X[t.Slot(8, 4)] + 1.5f, 2f); // el último tick dentro: se desliza 1,5 «hacia» la salida
        SymptomDetectors.Teleports(t, out _, out int leaving, out _);
        Assert.Equal(1, leaving);
    }

    [Fact]
    public void TeleportDuringARestartIsCountedApartAndNotAsUnexplained()
    {
        var t = DetectorTrace.Synthetic(30);
        for (int f = 10; f < 30; f++)
        {
            t.Restart[f] = RestartKind.FreeKick;
            t.Taker[f] = 3;
            t.SetPos(f, 9, t.X[t.Slot(f, 9)] + 1.5f, 2f); // un rival apartado de la falta
        }

        Assert.Empty(SymptomDetectors.Teleports(t, out _, out _, out int restart));
        Assert.Equal(1, restart);
    }

    // ---- BB-B ----

    [Fact]
    public void StealBeforeRestartPositiveAndNegative()
    {
        var steal = DetectorTrace.Synthetic(30);
        var clean = DetectorTrace.Synthetic(30);
        for (int f = 5; f < 20; f++)
        {
            steal.Restart[f] = RestartKind.Kickoff;
            steal.Taker[f] = 3;
            steal.Owner[f] = f < 10 ? 3 : 10; // a partir del 10 el balón es de un rival
            clean.Restart[f] = RestartKind.Kickoff;
            clean.Taker[f] = 3;
            clean.Owner[f] = 3;
        }

        Assert.Single(SymptomDetectors.StealBeforeRestart(steal, kickoffOnly: true));
        Assert.Empty(SymptomDetectors.StealBeforeRestart(clean, kickoffOnly: true));
        Assert.Single(SymptomDetectors.StealBeforeRestart(steal, kickoffOnly: false));
    }

    // ---- BB-C ----

    private static DetectorTrace CelebrationTrace(float x, bool jumps)
    {
        var t = DetectorTrace.Synthetic(40, new[] { Ev(EventType.Goal, 10, 5) });
        for (int f = 10; f < 30; f++)
        {
            t.State[t.Slot(f, 5)] = PlayerState.Celebrating;
            t.SetPos(f, 5, jumps && f >= 14 ? 3f : x, 3.5f);
        }

        return t;
    }

    [Fact]
    public void CelebrationPositiveInOwnHalfAndPositiveJumpAndNegativeInTheirHalf()
    {
        Assert.Single(SymptomDetectors.Celebration(CelebrationTrace(3f, false)));
        Assert.Single(SymptomDetectors.Celebration(CelebrationTrace(14f, true)));
        Assert.Empty(SymptomDetectors.Celebration(CelebrationTrace(14f, false)));
    }

    // ---- BH-A ----

    [Fact]
    public void FreezePositiveTwoHundredSilentTicksAndNegativeWithEventsMovingBallOrOwnerOnPitch()
    {
        var frozen = DetectorTrace.Synthetic(200);
        Assert.Single(SymptomDetectors.Freeze(frozen, out int off));
        Assert.Equal(0, off);

        var noisy = DetectorTrace.Synthetic(200, new[] { Ev(EventType.PassCompleted, 70, 3), Ev(EventType.Shot, 140, 3) });
        Assert.Empty(SymptomDetectors.Freeze(noisy, out _));

        var moving = DetectorTrace.Synthetic(200);
        for (int f = 0; f < 200; f++)
        {
            moving.BallX[f] = 2f + (f * 0.05f);
        }

        Assert.Empty(SymptomDetectors.Freeze(moving, out _));

        var ghost = DetectorTrace.Synthetic(20);
        for (int f = 5; f < 15; f++)
        {
            ghost.Owner[f] = 4;
            ghost.OnPitch[ghost.Slot(f, 4)] = false;
        }

        SymptomDetectors.Freeze(ghost, out int ghostFrames);
        Assert.Equal(10, ghostFrames);
    }

    // ---- BA-E ----

    [Fact]
    public void GoalWithoutAnglePositiveFromTheByLineAndNegativeFromTheCentre()
    {
        var events = new[] { Ev(EventType.Shot, 5, 5), Ev(EventType.Goal, 8, 5), Ev(EventType.Shot, 15, 6), Ev(EventType.Goal, 18, 6) };
        var t = DetectorTrace.Synthetic(30, events);
        for (int f = 0; f < 30; f++)
        {
            t.SetPos(f, 5, 15.6f, 0.3f); // línea de fondo, casi sin ángulo: apertura 0,13
            t.SetPos(f, 6, 12f, 3.5f);   // de frente: apertura 1
        }

        var hits = SymptomDetectors.GoalsWithoutAngle(t, out int shots, out int low, out int goals);
        Assert.Single(hits);
        Assert.Equal(2, shots);
        Assert.Equal(1, low);
        Assert.Equal(2, goals);
    }

    // ---- BB-I ----

    [Fact]
    public void PerkWithoutShotPositiveAndNegativeAndBlockedAtOnce()
    {
        var bad = DetectorTrace.Synthetic(20, new[] { Ev(EventType.PerkTriggered, 5, 5, detail: "box_predator") });
        var good = DetectorTrace.Synthetic(20, new[]
        {
            Ev(EventType.Shot, 5, 5), Ev(EventType.PerkTriggered, 5, 5, detail: "box_predator"),
            Ev(EventType.ShotBlocked, 6, 5),
            Ev(EventType.Shot, 12, 5), Ev(EventType.PerkTriggered, 12, 5, detail: "box_predator"),
        });

        Assert.Single(SymptomDetectors.PerkWithoutShot(bad, "box_predator", out int tb, out _));
        Assert.Equal(1, tb);
        Assert.Empty(SymptomDetectors.PerkWithoutShot(good, "box_predator", out int tg, out int blocked));
        Assert.Equal(2, tg);
        Assert.Equal(1, blocked);
    }

    // ---- Control sobre una traza real ----

    [Fact]
    public void RealTraceHasExplainedKickoffJumpsAndRestartWindows()
    {
        var catalog = TestData.LoadCatalog();
        var result = Simulator.Run(TestMatches.Reference(catalog, 7), 7, catalog, SimConfig.Default with { Trace = true });
        var t = DetectorTrace.From(result);

        // Un partido de verdad repone los equipos al menos una vez (saque inicial o tras gol): el instrumento
        // tiene que verlo como salto EXPLICADO, no ignorarlo. Si diera 0 estaría ciego.
        SymptomDetectors.Teleports(t, out int explained, out _, out _);
        Assert.True(explained > 0, "el detector no ve ni una reposición de equipos en un partido real");

        // El detector de reanudaciones tiene ventanas que mirar.
        Assert.Contains(Enumerable.Range(0, t.Frames), f => t.Restart[f] != RestartKind.None && t.Taker[f] >= 0);
    }
}
