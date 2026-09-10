using Underleague.Sim.Engine;

namespace Underleague.Sim.Tests.Engine;

/// <summary>AZ-B paso 5 (ADR 0091): la legalidad del pase en profundidad es una carrera en ticks, entera y exacta.</summary>
public sealed class ThroughPassTests
{
    [Fact]
    public void AThroughPassIsIllegalWhenTheDefenderArrivesFirst()
    {
        // Balón 8 ticks, receptor 10 (llega 2 tarde, dentro de los 4 permitidos), defensa 12: con margen 3
        // el receptor tendría que llegar en 9 o antes.
        Assert.False(Utility.ThroughPassIsLegal(8, 10, 12, lateTicks: 4, marginTicks: 3));
        Assert.True(Utility.ThroughPassIsLegal(8, 9, 12, lateTicks: 4, marginTicks: 3));
    }

    [Fact]
    public void AThroughPassIsIllegalWhenTheReceiverIsTooLate()
    {
        Assert.False(Utility.ThroughPassIsLegal(8, 13, 30, lateTicks: 4, marginTicks: 3));
        Assert.True(Utility.ThroughPassIsLegal(8, 12, 30, lateTicks: 4, marginTicks: 3));
    }

    [Fact]
    public void TicksToReachRoundsUpAndNeverDividesByZero()
    {
        Assert.Equal(10, Utility.TicksToReach(2.5f, 250));
        Assert.Equal(11, Utility.TicksToReach(2.6f, 250));
        Assert.Equal(1, Utility.TicksToReach(0.1f, 250));
        Assert.Equal(2500, Utility.TicksToReach(2.5f, 0));
    }

    [Fact]
    public void FlightTicksMatchesTheEngine()
    {
        Assert.Equal(1, Utility.FlightTicks(0f, 250));
        Assert.Equal(10, Utility.FlightTicks(2.5f, 250));
        Assert.Equal(11, Utility.FlightTicks(2.51f, 250));
    }
}
