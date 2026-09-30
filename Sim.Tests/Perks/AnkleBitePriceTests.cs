using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BM-C, censo por portador (Regla A) convertido en test permanente: con y sin <c>ankle_bite</c> sobre el MISMO jugador y las mismas
/// semillas, qué le pasa a quien lo lleva: faltas que comete, amarillas, rojas, lesiones que recibe y
/// lesiones que causa. Responde si la paga declarada («el árbitro aprende», «la víctima guarda rencor»)
/// existe hoy en cantidad apreciable.
/// </summary>
public sealed class AnkleBitePriceTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private readonly ITestOutputHelper _output;

    public AnkleBitePriceTests(ITestOutputHelper output) => _output = output;

    private record Tally(int Matches, int Fouls, int Yellows, int Reds, int InjuriesReceived, int InjuriesCaused, int Activations, int Tackles);

    private static Tally Run(bool withPerk)
    {
        int fouls = 0, yellows = 0, reds = 0, received = 0, caused = 0, activations = 0, tackles = 0, matches = 0;
        for (int slot = 1; slot <= 5; slot++)
        {
            for (int i = 0; i < 300; i++)
            {
                var homeRng = RngStreams.Generation(1, i);
                var awayRng = RngStreams.Generation(1, 10_000 + i);
                var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
                var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
                var players = home.Players.ToList();
                if (withPerk)
                {
                    players[slot] = players[slot] with { Perks = new[] { "ankle_bite" } };
                }

                int owner = players[slot].Id;
                var setup = new MatchSetup(home with { Players = players }, away, Referee);
                var result = Simulator.Run(setup, RngStreams.MatchSeed(1, i), Catalog, new SimConfig(CollectLog: false));
                matches++;
                foreach (var e in result.Events)
                {
                    switch (e.Type)
                    {
                        case EventType.Foul when e.Actor == owner:
                            fouls++;
                            break;
                        case EventType.Card when e.Actor == owner:
                            if (e.Detail.StartsWith("red", StringComparison.Ordinal)) { reds++; } else { yellows++; }
                            break;
                        case EventType.Injury when e.Actor == owner:
                            received++;
                            break;
                        case EventType.Injury when e.Opponent == owner:
                            caused++;
                            break;
                        case EventType.Tackle when e.Actor == owner && e.Detail is "won" or "missed" or "foul":
                            tackles++;
                            break;
                        case EventType.PerkTriggered when e.Actor == owner && e.Detail == "ankle_bite":
                            activations++;
                            break;
                    }
                }
            }
        }

        return new Tally(matches, fouls, yellows, reds, received, caused, activations, tackles);
    }

    [Fact]
    public void TheBiteCostsTheCarrierFoulsTheCensusCouldNotSeeBefore()
    {
        var without = Run(false);
        var with = Run(true);
        void Print(string name, Tally t) => _output.WriteLine(
            $"{name,-6} partidos {t.Matches}: por partido faltas {t.Fouls / (double)t.Matches:F3} amarillas {t.Yellows / (double)t.Matches:F3} "
            + $"rojas {t.Reds / (double)t.Matches:F4} lesiones recibidas {t.InjuriesReceived / (double)t.Matches:F3} causadas {t.InjuriesCaused / (double)t.Matches:F3} "
            + $"entradas {t.Tackles / (double)t.Matches:F3} activaciones {t.Activations / (double)t.Matches:F3}");
        Print("sin", without);
        Print("con", with);

        // Sin la falta arrastrada (BM-C, medido antes del cambio) el portador cometía 0,394 faltas por
        // partido con el perk contra 0,426 sin él: la paga no existía. Con ella sube a 0,577.
        Assert.True(with.Fouls > without.Fouls * 1.15, $"faltas con el perk {with.Fouls}, sin él {without.Fouls}");
        Assert.True(with.InjuriesCaused > without.InjuriesCaused, "el perk sigue lesionando más");
    }
}
