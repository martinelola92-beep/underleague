using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Sub-experimento de §21.3/CannonUtilityDumpTests: la primera pasada (ball owner == carrier, sin filtrar
/// por estado) encontró 15 muestras en la ventana 8-11; filtrando por <c>PlayerState.Dribbling</c> (el
/// único estado donde `Shoot` es legal) encontró CERO. Antes de concluir nada sobre `cannon`, hay que
/// distinguir "el portador nunca dribla en la ventana" de "el portador nunca dribla en absoluto con este
/// carrier/posición" — dos hipótesis distintas con implicaciones distintas.
/// </summary>
public sealed class CannonDribblingStateDiagnosticTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Quality = 50;
    private const int Level = 4;
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private readonly ITestOutputHelper _output;
    public CannonDribblingStateDiagnosticTests(ITestOutputHelper output) => _output = output;

    private static int FindByPosition(TeamSetup team, Position position)
    {
        for (int i = 0; i < team.Players.Count; i++)
        {
            if (team.Players[i].Position == position)
            {
                return i;
            }
        }

        return -1;
    }

    [Theory]
    [InlineData(null)] // portador real de cannon (Defensa, tras el arreglo de §19.1)
    [InlineData(Position.Forward)] // comparador: ¿es "nunca Dribbling" propio del Defensa, o del motor en general?
    public void HowMuchTimeDoesTheCarrierSpendDribblingAtAll(Position? forcedPosition)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "cannon");
        var race = perk.Race ?? Race.Human;
        long framesOnPitch = 0, framesBallOwner = 0, framesDribbling = 0, framesDribblingWithBall = 0;
        long framesDribblingInWindow = 0;
        int matchesWithAnyDribbling = 0;
        int matches = 0;

        for (int roster = 0; roster < 20; roster++)
        {
            var homeRng = RngStreams.Generation(1, roster);
            var awayRng = RngStreams.Generation(1, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", race, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", race, Quality, 100001, Level);

            int carrierSlot = forcedPosition is { } position
                ? FindByPosition(home, position)
                : PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
            if (carrierSlot < 0)
            {
                continue;
            }

            var armedPlayers = home.Players.ToList();
            armedPlayers[carrierSlot] = armedPlayers[carrierSlot] with { Perks = new[] { perk.Id } };
            var armedHome = home with { Players = armedPlayers };
            int carrierId = armedPlayers[carrierSlot].Id;
            ulong matchSeed = RngStreams.MatchSeed(1, roster * 2);

            var config = new SimConfig(CollectLog: false, Trace: true);
            var result = Simulator.Run(new MatchSetup(armedHome, away, Referee), matchSeed, Catalog, config);
            var trace = result.Trace;
            if (trace is null)
            {
                continue;
            }

            matches++;
            int slot = -1;
            for (int i = 0; i < trace.Players.Count; i++)
            {
                if (trace.Players[i].Id == carrierId)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                continue;
            }

            int team = trace.Players[slot].Team;
            bool anyDribbling = false;
            for (int frame = 0; frame < trace.FrameCount; frame++)
            {
                if (!trace.OnPitchAt(frame, slot))
                {
                    continue;
                }

                framesOnPitch++;
                bool hasBall = trace.BallOwnerAt(frame) == slot;
                bool dribbling = trace.StateAt(frame, slot) == PlayerState.Dribbling;
                if (hasBall)
                {
                    framesBallOwner++;
                }

                if (dribbling)
                {
                    framesDribbling++;
                    anyDribbling = true;
                }

                if (dribbling && hasBall)
                {
                    framesDribblingWithBall++;
                    float distance = Vec2.Distance(trace.PositionAt(frame, slot), Pitch.GoalCenter(team));
                    if (distance > 8 && distance <= 11)
                    {
                        framesDribblingInWindow++;
                    }
                }
            }

            if (anyDribbling)
            {
                matchesWithAnyDribbling++;
            }
        }

        string label = forcedPosition?.ToString() ?? "Defensa (real de cannon)";
        _output.WriteLine($"cannon (portador {label}, {matches} partidos con traza):");
        _output.WriteLine($"  frames en el campo (total) = {framesOnPitch}");
        _output.WriteLine($"  frames con balón asignado (BallOwnerAt == portador) = {framesBallOwner} ({100.0 * framesBallOwner / framesOnPitch:F2}%)");
        _output.WriteLine($"  frames en estado Dribbling (cualquier posesión) = {framesDribbling} ({100.0 * framesDribbling / framesOnPitch:F2}%)");
        _output.WriteLine($"  frames en Dribbling CON balón = {framesDribblingWithBall}");
        _output.WriteLine($"  de esos, frames en la ventana de distancia (8,11] = {framesDribblingInWindow}");
        _output.WriteLine($"  partidos con AL MENOS UN frame en Dribbling = {matchesWithAnyDribbling}/{matches}");
    }
}
