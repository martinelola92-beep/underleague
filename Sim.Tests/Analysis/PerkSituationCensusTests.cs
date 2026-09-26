using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// ¿Produce el catálogo **situaciones durante el partido**? No mide si un perk cumple una taxonomía —eso
/// lo decide una tabla y no necesita instrumento—: mide **cuántas veces se activa un perk mientras se
/// juega**, que es lo único que el jugador puede llegar a ver.
///
/// <para>Existe porque la medición que motivó toda la conversión se hizo con
/// <c>BroadcastCapture.FindPerkBurst</c>, dentro de Godot: seis partidos, y entre cero y dos activaciones
/// fuera de los primeros seis segundos. Eso es caro de repetir y no se puede comparar contra una línea
/// base. Aquí es un lote de `/Sim` puro, así que la misma pregunta se responde con el árbol de antes y el
/// de ahora.</para>
///
/// <para><b>No es una puerta y no afirma nada</b>: vuelca una tabla. La comparación antes/después la hace
/// quien lo ejecuta, con los dos árboles.</para>
/// </summary>
public sealed class PerkSituationCensusTests
{
    /// <summary>
    /// El arranque: los perks de <c>MATCH_START</c> se disparan aquí, con el pregón del saque tapando
    /// media pantalla. Seis segundos a 15 ticks/s (RT-020).
    /// </summary>
    private const int OpeningTicks = 90;

    private const int Quality = 50;
    private const int Level = 4;
    private const int Rosters = 12;
    private const ulong Seed = 1;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Los perks que se están midiendo. Se cambian a mano según qué tanda haya que comprobar.</summary>
    private static readonly string[] Moved =
    {
        "point_blank", "nutmeg", "bull_rush", "silver_tongue",
        "eyed_coward", "never_tracks_back", "shouting_wall",
    };

    private readonly ITestOutputHelper _output;

    public PerkSituationCensusTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void HowManyPerkActivationsHappenWhileTheresFootballToLookAt()
    {
        _output.WriteLine($"{Rosters * 2} partidos por perk · arranque = primeros {OpeningTicks} ticks ({OpeningTicks / 15} s)");
        _output.WriteLine("perk                  | act/partido | en arranque | EN JUEGO | partidos con >=1 en juego");
        _output.WriteLine("----------------------+-------------+-------------+----------+--------------------------");

        double totalOpen = 0;
        int totalMatches = 0;
        for (int i = 0; i < Moved.Length; i++)
        {
            var row = Measure(Moved[i], i);
            if (row.Matches == 0)
            {
                _output.WriteLine($"{Moved[i],-21} | SIN MEDIR: ningún titular puede llevarlo");
                continue;
            }

            totalOpen += row.OpenPlay;
            totalMatches = row.Matches;
            _output.WriteLine(
                $"{Moved[i],-21} | {(double)row.Total / row.Matches,11:0.00} | {(double)row.Opening / row.Matches,11:0.00} "
                + $"| {(double)row.OpenPlay / row.Matches,8:0.00} | {row.MatchesWithOpenPlay,3} de {row.Matches,-3} ({100.0 * row.MatchesWithOpenPlay / row.Matches,5:0.0} %)");
        }

        _output.WriteLine("----------------------+-------------+-------------+----------+--------------------------");
        _output.WriteLine(totalMatches == 0
            ? "Ninguno se ha podido medir."
            : $"Los medidos juntos aportan {totalOpen / totalMatches:0.00} activaciones en juego por partido.");

        // El censo no decide nada: sólo tiene que haber medido algo.
        Assert.True(totalMatches > 0);
    }

    private readonly record struct Row(int Matches, int Total, int Opening, int OpenPlay, int MatchesWithOpenPlay);

    private static Row Measure(string perkId, int index)
    {
        var perk = Catalog.Perks.All.Single(p => string.Equals(p.Id, perkId, StringComparison.Ordinal));
        var race = perk.Race ?? Race.Human;
        var config = new SimConfig(CollectLog: false);

        int matches = 0, total = 0, opening = 0, openPlay = 0, matchesWithOpenPlay = 0;
        for (int roster = 0; roster < Rosters; roster++)
        {
            var subjectRng = RngStreams.Generation(Seed, (index * 1000) + roster);
            var mirrorRng = RngStreams.Generation(Seed, (index * 1000) + 500 + roster);
            var subject = TeamGenerator.Generate(ref subjectRng, Catalog, "subject", race, Quality, 1, Level);
            var mirror = TeamGenerator.Generate(ref mirrorRng, Catalog, "mirror", race, Quality, 100001, Level);

            // Una habilidad racial no se asigna: el motor se la da a toda la plantilla de esa raza y no
            // ocupa slot (RF-031b), así que no aparece en PerkAssignment.Eligible y se cuenta para el
            // EQUIPO entero en vez de para un portador.
            // Racial es la que el motor reparte por raza (RF-031b), NO cualquier perk con `race` puesta:
            // `race` también sirve para decir "exclusivo de elfos", que es otra cosa y sí ocupa slot.
            // Confundirlas dejaba a `duelist` sin asignar y marcando cero en el censo.
            bool racial = string.Equals(Catalog.Race(race).Ability, perk.Id, StringComparison.Ordinal);
            var armed = subject;
            if (!racial)
            {
                // Se arma a TODOS los titulares de campo que puedan llevarlo, no a uno.
                //
                // La primera versión de este censo armaba al titular de índice 0, que en la alineación por
                // defecto es el PORTERO: un portero no entra, no dispara y no regatea, así que los cinco
                // perks no raciales marcaban casi cero y el censo decía que moverlos los había matado.
                // No los había matado: estaban en las manos equivocadas. Armar la línea entera es además
                // lo que responde a la pregunta de verdad —«si mi equipo lleva esto, ¿pasa algo?»— en vez
                // de a «¿le pasa algo a este jugador concreto?».
                var players = subject.Players.ToList();
                bool any = false;
                // Un perk de portero se le pone AL PORTERO: saltárselo siempre dejaba sin medir a los que
                // sólo él puede llevar, y un cero por no haber armado a nadie se lee igual que un cero por
                // no activarse nunca.
                bool keeperOnly = perk.PositionOnly == Position.Goalkeeper;
                for (int i = 0; i < players.Count; i++)
                {
                    bool isKeeper = players[i].Position == Position.Goalkeeper;
                    if (isKeeper != keeperOnly || !CanCarry(players[i], perk))
                    {
                        continue;
                    }

                    players[i] = players[i] with { Perks = new[] { perk.Id } };
                    any = true;
                }

                if (!any)
                {
                    continue;
                }

                armed = subject with { Players = players };
            }

            for (int direction = 0; direction < 2; direction++)
            {
                bool away = direction == 1;
                ulong matchSeed = RngStreams.MatchSeed(Seed, (index * 100_000) + (roster * 2) + direction);
                var result = Simulator.Run(
                    away ? new MatchSetup(mirror, armed, Referee) : new MatchSetup(armed, mirror, Referee),
                    matchSeed, Catalog, config);

                matches++;
                int inPlay = 0;
                foreach (var activation in result.Report.PerkActivations)
                {
                    if (!string.Equals(activation.PerkId, perk.Id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    total++;
                    if (activation.Tick <= OpeningTicks)
                    {
                        opening++;
                    }
                    else
                    {
                        openPlay++;
                        inPlay++;
                    }
                }

                if (inPlay > 0)
                {
                    matchesWithOpenPlay++;
                }
            }
        }

        return new Row(matches, total, opening, openPlay, matchesWithOpenPlay);
    }

    private static bool CanCarry(PlayerDefinition player, PerkDefinition perk)
    {
        foreach (var candidate in PerkAssignment.Eligible(player, Catalog))
        {
            if (string.Equals(candidate.Id, perk.Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
