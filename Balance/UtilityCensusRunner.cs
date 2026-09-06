using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Balance;

/// <summary>Una fila del censo de utilidad: lo que le pasa a una acción a lo largo de N decisiones.</summary>
public sealed record ActionCensusRow(
    PlayerAction Action,
    int Legal,
    int Rejected,
    int Chosen,
    int RunnerUp,
    long ScoreSum,
    long MarginSum,
    int BestScore);

/// <summary>
/// Modo <c>--utility-census N</c>: **censo del volcado de utilidad (RT-098)**. Responde a "¿por qué una
/// acción no gana nunca la tabla?" con las tres respuestas que son distintas entre sí: porque se
/// **descarta** (no había a quién / no tocaba), porque **puntúa por debajo** aun siendo legal, o porque
/// simplemente no se evalúa.
///
/// <para>No toca <c>/Sim</c>: usa el volcado que ya existe. <c>SimConfig.DumpUtility</c> imprime la tabla
/// de <b>un</b> jugador en <b>un</b> tick, así que el censo repite el <b>mismo</b> partido —mismo setup y
/// misma semilla de motor— una vez por (jugador, tick) muestreado y acumula las tablas. Es caro en
/// partidos y barato en código, y el resultado es exactamente lo que la tabla de utilidad decide, no una
/// aproximación.</para>
/// </summary>
public static class UtilityCensusRunner
{
    /// <summary>Ticks entre muestras dentro de un partido.</summary>
    public const int TickStride = 30;

    /// <summary>Último tick muestreado (el reglamentario de un partido estándar).</summary>
    public const int LastTick = 1200;

    public static IReadOnlyList<ActionCensusRow> Run(
        Catalog catalog, ReferenceConfig reference, ulong seed, int matches)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfLessThan(matches, 1);

        var referee = new RefereeSetup("Referee", RefereeTrait.Neutral, 0);
        var pairing = reference.Pairings[0];
        int homeIndex = IndexOf(reference, pairing.HomeId);
        int awayIndex = IndexOf(reference, pairing.AwayId);

        var actions = Enum.GetValues<PlayerAction>();
        var legal = new int[actions.Length];
        var rejected = new int[actions.Length];
        var chosen = new int[actions.Length];
        var runnerUp = new int[actions.Length];
        var scoreSum = new long[actions.Length];
        var marginSum = new long[actions.Length];
        var bestScore = new int[actions.Length];
        for (int a = 0; a < actions.Length; a++)
        {
            bestScore[a] = int.MinValue;
        }

        for (int m = 0; m < matches; m++)
        {
            // Un partido distinto por m: plantillas nuevas (flujo de generación) y semilla de motor nueva.
            var homeRng = RngStreams.Generation(seed + (ulong)m, homeIndex);
            var awayRng = RngStreams.Generation(seed + (ulong)m, 1000 + awayIndex);
            var home = TeamGenerator.Generate(
                ref homeRng, catalog, reference.Teams[homeIndex].Id, reference.Teams[homeIndex].Race,
                reference.Teams[homeIndex].Quality, 1 + (homeIndex * 100), reference.Teams[homeIndex].Level,
                reference.Teams[homeIndex].UniformRarity);
            var away = TeamGenerator.Generate(
                ref awayRng, catalog, reference.Teams[awayIndex].Id, reference.Teams[awayIndex].Race,
                reference.Teams[awayIndex].Quality, 1 + ((1000 + awayIndex) * 100), reference.Teams[awayIndex].Level,
                reference.Teams[awayIndex].UniformRarity);
            var setup = new MatchSetup(home, away, referee);
            ulong matchSeed = RngStreams.MatchSeed(seed, m);

            var ids = new List<int>();
            for (int i = 0; i < home.Players.Count; i++)
            {
                ids.Add(home.Players[i].Id);
            }

            for (int i = 0; i < away.Players.Count; i++)
            {
                ids.Add(away.Players[i].Id);
            }

            foreach (int id in ids)
            {
                for (int tick = TickStride; tick <= LastTick; tick += TickStride)
                {
                    var config = new SimConfig(CollectLog: false, DumpUtility: (id, tick));
                    var result = Simulator.Run(setup, matchSeed, catalog, config);
                    var dump = result.Report.UtilityDump;
                    if (dump is null)
                    {
                        continue;
                    }

                    int winner = -1, winnerScore = int.MinValue, second = -1, secondScore = int.MinValue;
                    for (int r = 0; r < dump.Rows.Count; r++)
                    {
                        var row = dump.Rows[r];
                        int index = (int)row.Action;
                        legal[index]++;
                        if (row.Rejected)
                        {
                            rejected[index]++;
                            continue;
                        }

                        scoreSum[index] += row.Score;
                        if (row.Score > bestScore[index])
                        {
                            bestScore[index] = row.Score;
                        }

                        if (row.Score > winnerScore)
                        {
                            second = winner;
                            secondScore = winnerScore;
                            winner = index;
                            winnerScore = row.Score;
                        }
                        else if (row.Score > secondScore)
                        {
                            second = index;
                            secondScore = row.Score;
                        }
                    }

                    if (winner < 0)
                    {
                        continue;
                    }

                    chosen[winner]++;
                    if (second >= 0)
                    {
                        runnerUp[second]++;
                    }

                    for (int r = 0; r < dump.Rows.Count; r++)
                    {
                        var row = dump.Rows[r];
                        if (!row.Rejected)
                        {
                            marginSum[(int)row.Action] += winnerScore - row.Score;
                        }
                    }
                }
            }
        }

        var rows = new List<ActionCensusRow>(actions.Length);
        for (int a = 0; a < actions.Length; a++)
        {
            rows.Add(new ActionCensusRow(
                actions[a], legal[a], rejected[a], chosen[a], runnerUp[a],
                scoreSum[a], marginSum[a], bestScore[a] == int.MinValue ? 0 : bestScore[a]));
        }

        return rows;
    }

    private static int IndexOf(ReferenceConfig reference, string id)
    {
        for (int i = 0; i < reference.Teams.Count; i++)
        {
            if (string.Equals(reference.Teams[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new ArgumentException($"equipo desconocido en reference.json: {id}", nameof(id));
    }
}
