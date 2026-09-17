using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Sonda de exposición con población explícita (raza y, opcionalmente, puesto del portador), para la
/// medición de la Fase A (§22.8). Mismo esquema de generación y semillas que
/// <c>PairedBalanceHarness.RunWithEligibleCarrier</c>; lo único que cambia es que la raza y el puesto se
/// fijan desde fuera en vez de salir de <c>perk.Race ?? Human</c> + "primer titular de campo elegible".
/// No decide nada: solo cuenta activaciones.
/// </summary>
public static class RaceExposureProbe
{
    private const int Quality = 50;
    private const int Level = 4;
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    /// <param name="Matches">Partidos armados realmente jugados (0 si ninguna plantilla pudo llevar el perk).</param>
    /// <param name="MatchesWithActivation">Partidos con al menos una activación — la exposición de §5.4.</param>
    public sealed record Probe(int Matches, int MatchesWithActivation, long Activations, Position? CarrierRole)
    {
        public double ExposurePercent => Matches == 0 ? 0.0 : 100.0 * MatchesWithActivation / Matches;
    }

    public static Probe Measure(Catalog catalog, PerkDefinition perk, Race race, int rosters, ulong seed, Position? forcedRole = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(perk);

        var config = new SimConfig(CollectLog: false, Trace: false);
        int matches = 0, withActivation = 0;
        long activations = 0;
        Position? carrierRole = null;

        for (int roster = 0; roster < rosters; roster++)
        {
            var homeRng = RngStreams.Generation(seed, roster);
            var awayRng = RngStreams.Generation(seed, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, catalog, "home", race, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, catalog, "away", race, Quality, 100001, Level);

            int slot = forcedRole is { } role ? FindByPosition(home, role) : PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, catalog);
            if (slot < 0)
            {
                continue;
            }

            carrierRole ??= home.Players[slot].Position;
            var armedPlayers = home.Players.ToList();
            armedPlayers[slot] = armedPlayers[slot] with { Perks = new[] { perk.Id } };
            var armedHome = home with { Players = armedPlayers };
            int carrierId = armedPlayers[slot].Id;

            for (int direction = 0; direction < 2; direction++)
            {
                ulong matchSeed = RngStreams.MatchSeed(seed, (roster * 2) + direction);
                var setup = direction == 1
                    ? new MatchSetup(away, armedHome, Referee)
                    : new MatchSetup(armedHome, away, Referee);
                var result = Simulator.Run(setup, matchSeed, catalog, config);

                long inThisMatch = result.Report.PerksSummary
                    .Where(s => string.Equals(s.PerkId, perk.Id, StringComparison.Ordinal) && s.OwnerId == carrierId)
                    .Sum(s => s.Activations);

                matches++;
                activations += inThisMatch;
                if (inThisMatch > 0)
                {
                    withActivation++;
                }
            }
        }

        return new Probe(matches, withActivation, activations, carrierRole);
    }

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
}
