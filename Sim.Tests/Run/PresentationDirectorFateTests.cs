using Underleague.Game.Match;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0171, el director con la tirada del destino: cámara lenta sólo a x1 y sólo hasta el fotograma de la
/// tirada, sin pausa, cediendo ante una pausa o una decisión, viva tras una búsqueda a mitad de tirada, y
/// una segunda tirada se presenta después de la primera. (RT-084 prohíbe probar interfaz; el director es lógica
/// de ritmo sin Godot.)
/// </summary>
public sealed class PresentationDirectorFateTests
{
    private const double Step = 1d / 60d;

    private static MatchMoment Fate(int rollFrame) => new(
        Frame: rollFrame - MatchMomentView.FateLeadFrames, FreezeFrame: rollFrame - MatchMomentView.FateLeadFrames - 1,
        LastFrame: rollFrame, Level: 3, Kind: MomentKind.Fate, Team: 0, LeadPlayerId: 3, Pauses: false,
        Decision: false, EventIndices: new[] { 0 }, Cancelled: false, HasGoal: false);

    private static MatchMoment Injury(int frame, bool decision = false) => new(
        Frame: frame, FreezeFrame: frame - 1, LastFrame: frame, Level: 3, Kind: MomentKind.SevereInjury, Team: 0,
        LeadPlayerId: 3, Pauses: true, Decision: decision, EventIndices: new[] { 1 }, Cancelled: false, HasGoal: false);

    private static PresentationDirector Director(params MatchMoment[] moments) =>
        new(moments, DirectorTimings.Default);

    /// <summary>Avanza el reloj de pantalla como lo hace la pantalla: el fotograma sube al ritmo de la escala pedida.</summary>
    private static (DirectorFrame Frame, int Screen) Play(PresentationDirector director, int screen, int seconds60, int speed)
    {
        DirectorFrame result = director.Advance(screen, 0d, speed);
        double carry = 0d;
        for (int i = 0; i < seconds60; i++)
        {
            carry += Step * 15d * speed * result.TimeScale;
            int advance = (int)carry;
            carry -= advance;
            result = director.Advance(result.Frozen ? result.DisplayFrame : screen + advance, Step, speed);
            screen = result.DisplayFrame;
        }

        return (result, screen);
    }

    [Fact]
    public void TheFateSlowsTheClockAtX1UntilTheRollFrameAndNeverFreezes()
    {
        var director = Director(Fate(rollFrame: 100));
        var (start, screen) = Play(director, 90, 0, 1);
        Assert.Equal(90, screen);

        var (mid, _) = Play(director, 93, 1, 1);
        Assert.Equal(MomentKind.Fate, mid.Voice!.Kind);
        Assert.False(mid.Frozen);
        Assert.Equal(DirectorTimings.Default.FateSlowScale, mid.TimeScale);
        Assert.Equal(1d, start.TimeScale);

        var (after, _) = Play(director, 101, 1, 1);
        Assert.Equal(1d, after.TimeScale);
    }

    [Fact]
    public void ChangingToX4InTheMiddleOfTheSlowMotionRestoresTheScale()
    {
        var director = Director(Fate(rollFrame: 100));
        Play(director, 93, 1, 1);
        var (midX1, _) = Play(director, 94, 1, 1);
        Assert.Equal(DirectorTimings.Default.FateSlowScale, midX1.TimeScale);

        var (midX4, _) = Play(director, 95, 1, 4);
        Assert.Equal(1d, midX4.TimeScale);
    }

    [Fact]
    public void AtX4TheFateIsCompressedWithoutSlowMotion()
    {
        var director = Director(Fate(rollFrame: 100));
        var (frame, _) = Play(director, 93, 1, 4);
        Assert.Equal(MomentKind.Fate, frame.Voice!.Kind);
        Assert.Equal(1d, frame.TimeScale);
        Assert.False(frame.Frozen);
    }

    [Fact]
    public void NothingIsShownAtX16()
    {
        var director = Director(Fate(rollFrame: 100));
        var (frame, _) = Play(director, 93, 1, 16);
        Assert.Null(frame.Voice);
    }

    [Fact]
    public void ThePausingInjuryThatFollowsTheRollTakesOverTheVoiceAndFreezes()
    {
        // ADR 0192: primero gira la ruleta (congelada en el fotograma anterior a los dados) y después la lesión.
        var director = Director(Fate(rollFrame: 100), Injury(100));
        Play(director, 93, 1, 1);
        var wheel = director.Advance(99, Step, 1);
        Assert.True(wheel.Frozen);
        Assert.Equal(99, wheel.DisplayFrame);
        Assert.True(wheel.FateSpin >= 0f);

        var result = wheel;
        for (int i = 0; i < 60 * 6 && result.Voice?.Kind != MomentKind.SevereInjury; i++)
        {
            result = director.Advance(result.Frozen ? 99 : 100, Step, 1);
        }

        Assert.Equal(MomentKind.SevereInjury, result.Voice!.Kind);
        Assert.True(result.Frozen);
        Assert.Equal(1d, result.TimeScale);
    }

    [Fact]
    public void TheWheelSpinsFrozenThenSettlesThenJumpsToTheRoll()
    {
        var director = Director(Fate(rollFrame: 100));
        Play(director, 93, 1, 1);
        var r = director.Advance(99, Step, 1);
        Assert.True(r.Frozen);
        Assert.False(r.FateSettled);

        int steps = 0;
        while (r.Frozen && steps < 60 * 10)
        {
            r = director.Advance(99, Step, 1);
            steps++;
            if (r.Frozen && steps == (int)(DirectorTimings.Default.FateSpin / Step) + 2)
            {
                Assert.True(r.FateSettled);
            }
        }

        double seconds = steps * Step;
        Assert.InRange(seconds, DirectorTimings.Default.FateSpin + DirectorTimings.Default.FateResult - 0.05, DirectorTimings.Default.FateSpin + DirectorTimings.Default.FateResult + 0.05);

        // Al terminar, en el fotograma de los dados: ninguna orden puede caer antes de la tirada ya enseñada.
        Assert.Equal(100, r.DisplayFrame);
        Assert.Equal(-1f, r.FateSpin);
    }

    [Fact]
    public void ALateRollThatStartsPastItsFrameDoesNotSpinBackwards()
    {
        var director = Director(Fate(rollFrame: 100));
        var r = director.Advance(101, Step, 1);
        Assert.Equal(MomentKind.Fate, r.Voice!.Kind);
        Assert.False(r.Frozen);
        Assert.True(r.DisplayFrame >= 101);
    }

    [Fact]
    public void ASecondRollOfTheSamePlayerAndTickIsNotQueued()
    {
        var first = Fate(rollFrame: 100);
        var second = Fate(rollFrame: 100);
        var director = Director(first, second);
        var r = director.Advance(93, Step, 1);
        Assert.Same(first, r.Voice);
        r = director.Advance(94, Step, 1);
        Assert.Same(first, r.Voice);

        var wheel = director.Advance(99, Step, 1);
        Assert.Same(first, wheel.Voice);
        for (int i = 0; i < 60 * 6 && wheel.Frozen; i++)
        {
            wheel = director.Advance(99, Step, 1);
        }

        var after = director.Advance(101, 3d, 1);
        Assert.NotSame(second, after.Voice);
    }

    [Fact]
    public void ARollInTheEightTicksAfterADecisionSurvivesTheSeek()
    {
        // La decisión de una lesión reanuda en el fotograma siguiente al suyo (Seek(decisionFrame + 1)); una
        // tirada cuyo arranque cae antes de ese fotograma pero cuya tirada cae después no puede desaparecer.
        var director = Director(Injury(200, decision: true), Fate(rollFrame: 205));
        director.Seek(201);
        director.Resolve();
        var result = director.Advance(201, Step, 1);
        Assert.Equal(MomentKind.Fate, result.Voice!.Kind);
    }

    [Fact]
    public void ARollWhoseFrameAlreadyPassedAfterASeekIsNotPresented()
    {
        var director = Director(Fate(rollFrame: 100));
        director.Seek(150);
        var result = director.Advance(150, Step, 1);
        Assert.Null(result.Voice);
    }

    [Fact]
    public void ASecondRollIsPresentedAfterTheFirstFinishes()
    {
        var director = Director(Fate(rollFrame: 100), Fate(rollFrame: 400));
        Play(director, 93, 1, 1);

        // El primero termina por duración (2,6 s a 1×); el reloj de pantalla ya va normal después de f.
        var (later, _) = Play(director, 101, 60 * 4, 1);
        Assert.Null(later.Voice);

        var second = director.Advance(393, Step, 1);
        Assert.Equal(MomentKind.Fate, second.Voice!.Kind);
        Assert.Equal(400 - MatchMomentView.FateLeadFrames, second.Voice.Frame);
    }
}
