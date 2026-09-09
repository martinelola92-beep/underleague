using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>AZ-B paso 1 (docs/plan-pases-trayectoria.md): el pase al pie. Aritmética exacta sobre la función pura.</summary>
public sealed class PassLeadTests
{
    private static readonly Vec2 Origin = new(5f, 2.5f);

    [Fact]
    public void ALeadIsCappedByTheAbsoluteMaximum()
    {
        // Intención a 6 casillas, velocidad alta (0,5 casillas/tick) y 12 ticks de vuelo: podría recorrer 6.
        var target = Utility.PassTarget(Origin, new Vec2(11f, 2.5f), 500, 12, 1.5f);
        Assert.Equal(1.5f, Vec2.Distance(target, Origin));
    }

    [Fact]
    public void ALeadNeverOvershootsTheReceiverIntention()
    {
        var target = Utility.PassTarget(Origin, new Vec2(5.4f, 2.5f), 500, 12, 1.5f);
        Assert.Equal(new Vec2(5.4f, 2.5f), target);
    }

    [Fact]
    public void AStandingReceiverGetsThePassAtHisFeet()
    {
        Assert.Equal(Origin, Utility.PassTarget(Origin, Origin, 500, 12, 1.5f));
    }

    [Fact]
    public void ASlowReceiverOnlyGetsWhatHeCanReach()
    {
        // 0,1 casillas/tick durante 4 ticks = 0,4, por debajo de la intención (6) y del techo (1,5).
        var target = Utility.PassTarget(Origin, new Vec2(11f, 2.5f), 100, 4, 1.5f);
        Assert.Equal(0.4f, Vec2.Distance(target, Origin), 3);
    }
}
