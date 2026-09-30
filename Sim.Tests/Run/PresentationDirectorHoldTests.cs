using Underleague.Game.Match;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0173 (BB-D), la pausa breve del director: se abre a 1x ante un suceso que detiene el juego, salta al
/// fotograma del suceso al terminar, no existe a x4 ni x16, se corta al cambiar de velocidad, cede ante una voz
/// alta en el escenario, no hace retroceder la imagen al encadenarse y no toca lo que ya congela por sí solo.
/// (RT-084 prohíbe probar interfaz; el director es lógica de ritmo sin Godot.)
/// </summary>
public sealed class PresentationDirectorHoldTests
{
    private const double Step = 1d / 15d;

    private static MatchMoment M(int frame, int level, MomentKind kind, bool pauses = false, bool goal = false) => new(
        Frame: frame, FreezeFrame: Math.Max(frame - 1, 0), LastFrame: frame, Level: level, Kind: kind, Team: 0,
        LeadPlayerId: 1, Pauses: pauses, Decision: false, EventIndices: new[] { 0 }, Cancelled: false, HasGoal: goal);

    // La política de la pantalla, sin traza: sólo falta y roja detienen el juego.
    private static bool Stops(MatchMoment m) => m.Kind is MomentKind.Foul or MomentKind.Red;

    private static PresentationDirector D(params MatchMoment[] moments) => new(moments, DirectorTimings.Default, Stops);

    private static DirectorFrame RunTo(PresentationDirector d, int from, int to, int speed = 1)
    {
        DirectorFrame last = null!;
        for (int f = from; f <= to; f++)
        {
            last = d.Advance(f, Step, speed);
        }

        return last;
    }

    [Fact]
    public void AFoulHoldsTheFrameBeforeAndThenJumpsToTheEventFrame()
    {
        var foul = M(100, 1, MomentKind.Foul);
        var d = D(foul);
        Assert.False(RunTo(d, 90, 99).Frozen);

        var first = d.Advance(100, Step, 1);
        Assert.True(first.Frozen);
        Assert.Equal(99, first.DisplayFrame);
        Assert.Same(foul, first.Held);
        Assert.Same(foul, first.Stamp);
        Assert.Null(first.Voice);

        int frozen = 0;
        var f = first;
        while (f.Frozen && frozen < 50)
        {
            f = d.Advance(99, Step, 1);
            frozen++;
        }

        Assert.False(f.Frozen);
        Assert.Equal(100, f.DisplayFrame);
        Assert.Null(f.Held);
        Assert.InRange(frozen, 8, 10);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    public void ThereIsNoHoldAtX4OrX16(int speed)
    {
        var r = RunTo(D(M(100, 1, MomentKind.Foul)), 90, 100, speed);
        Assert.False(r.Frozen);
        Assert.Equal(100, r.DisplayFrame);
    }

    [Fact]
    public void ThereIsNoHoldWithoutAPolicyOrWhenPlayDoesNotStop()
    {
        Assert.False(RunTo(new PresentationDirector(new[] { M(100, 1, MomentKind.Foul) }, DirectorTimings.Default), 90, 100).Frozen);
        Assert.False(RunTo(D(M(100, 1, MomentKind.Yellow)), 90, 100).Frozen);
    }

    [Fact]
    public void ThereIsNoHoldWhileALoudVoiceIsOnStage()
    {
        // Un N3 sin pausa abre voz a 1x durante 3 s; una falta un segundo después no congela.
        var d = D(M(90, 3, MomentKind.Mob), M(105, 1, MomentKind.Foul));
        var r = RunTo(d, 80, 90);
        Assert.NotNull(r.Voice);
        Assert.False(r.Frozen);
        r = RunTo(d, 91, 105);
        Assert.False(r.Frozen);
        Assert.Null(r.Held);
    }

    [Fact]
    public void ChangingSpeedCutsTheHoldAtOnce()
    {
        var d = D(M(100, 1, MomentKind.Foul));
        Assert.True(RunTo(d, 90, 100).Frozen);
        var next = d.Advance(99, Step, 4);
        Assert.False(next.Frozen);
        Assert.Equal(100, next.DisplayFrame);
    }

    [Fact]
    public void ARedCardHoldsAndSpeaksAtTheSameTime()
    {
        var red = M(100, 3, MomentKind.Red);
        var d = D(red);
        var r = RunTo(d, 90, 100);
        Assert.True(r.Frozen);
        Assert.Equal(99, r.DisplayFrame);
        Assert.Same(red, r.Voice);
        Assert.Same(red, r.Held);

        var f = r;
        for (int i = 0; i < 12; i++)
        {
            f = d.Advance(f.Frozen ? 99 : f.DisplayFrame + 1, Step, 1);
        }

        Assert.False(f.Frozen);
        Assert.Same(red, f.Voice);
    }

    [Fact]
    public void AGoalStillFreezesWithItsOwnVoiceAndNoHold()
    {
        var goal = M(100, 3, MomentKind.Goal, pauses: true, goal: true);
        var r = RunTo(D(goal), 90, 100);
        Assert.True(r.Frozen);
        Assert.Same(goal, r.Voice);
        Assert.Null(r.Held);
    }

    [Fact]
    public void ChainedHoldsNeverMakeTheFrameGoBackwards()
    {
        // Dos faltas pegadas: la segunda congelaría en el fotograma 99, por detrás del 100 al que acaba de
        // saltar la primera. No hay segunda pausa: la imagen nunca retrocede.
        var d = D(M(100, 1, MomentKind.Foul), M(100, 1, MomentKind.Foul), M(140, 1, MomentKind.Foul));
        int shown = 0;
        int holds = 0;
        for (int screen = 90; screen <= 150; screen++)
        {
            var r = d.Advance(screen, Step, 1);
            int guard = 0;
            while (r.Frozen && guard++ < 50)
            {
                Assert.True(r.DisplayFrame >= shown, $"la imagen retrocedió de {shown} a {r.DisplayFrame}");
                if (r.Held is not null && guard == 1)
                {
                    holds++;
                }

                r = d.Advance(r.DisplayFrame, Step, 1);
            }

            Assert.True(r.DisplayFrame >= shown, $"la imagen retrocedió de {shown} a {r.DisplayFrame}");
            shown = r.DisplayFrame;
        }

        Assert.Equal(2, holds);
    }

    [Fact]
    public void SeekingBackwardsForgetsThePreviousHold()
    {
        var d = D(M(100, 1, MomentKind.Foul));
        RunTo(d, 90, 100);
        d.Seek(50);
        var r = RunTo(d, 90, 100);
        Assert.True(r.Frozen);
        Assert.Equal(99, r.DisplayFrame);
    }
}
