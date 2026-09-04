using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run;

/// <summary>
/// Lo que le pasa a la plantilla después de un partido y en qué momento exacto puede terminar la run.
/// Interno: la superficie pública del bucle es <see cref="RunEngine"/>.
///
/// <para><b>Derrota "en cualquier momento, incluido durante un partido" (RF-002b).</b>
/// <c>Simulator.Run</c> no se puede interrumpir: devuelve el partido entero. Así que la baja se detecta
/// recorriendo la <b>secuencia ordenada de eventos</b> y llevando la cuenta de disponibles tras cada
/// lesión grave o muerte propia. En cuanto la cuenta baja de 5, la run termina con el tick de ese
/// evento y <b>los eventos posteriores no se aplican a la plantilla</b>: el estado guardado es el que
/// había en ese instante, y el render puede cortar la reproducción en ese tick. Es la lectura literal
/// de "una lesión grave o una muerte en pleno partido con solo 5 en campo termina la run al
/// instante".</para>
/// </summary>
internal static class MatchResolution
{
    /// <summary>Sufijo con el que el motor marca un evento anulado por un perk (§7 de fase 1).</summary>
    private const string CancelledSuffix = ":cancelled";

    /// <summary>Detalle de una lesión grave en el evento INJURY.</summary>
    private const string SevereDetail = "severe";

    /// <summary>Resultado de aplicar un partido al estado de la run.</summary>
    internal sealed record Applied(RunState State, RunMatchSummary Summary, RunOutcome Outcome);

    /// <summary>
    /// Aplica el resultado de un partido: bajas, experiencia, contadores y partidos en el banquillo, y
    /// decide si la run termina.
    /// </summary>
    internal static Applied Apply(
        RunState state,
        MapNode node,
        MatchLineup lineup,
        MatchResult result,
        Catalog catalog)
    {
        var players = new List<RunPlayer>(state.Roster);
        var playedIds = new List<int>(lineup.Starters.Count);
        for (int i = 0; i < lineup.Starters.Count; i++)
        {
            playedIds.Add(lineup.Starters[i].Id);
        }

        playedIds.Sort();

        var benchIds = new List<int>(lineup.Bench.Count);
        for (int i = 0; i < lineup.Bench.Count; i++)
        {
            benchIds.Add(lineup.Bench[i].Id);
        }

        benchIds.Sort();

        // 1. La penalización de las lesiones leves ya se ha gastado en este partido (RF-091: "durante el
        //    siguiente partido"), así que los titulares salen de él con el contador a cero antes de
        //    sumar las lesiones nuevas. Los suplentes conservan la suya: no la han gastado.
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].MinorInjuries > 0
                && players[i].PhysicalState == PhysicalState.MinorInjury
                && Contains(playedIds, players[i].Id))
            {
                players[i] = players[i] with { MinorInjuries = 0, PhysicalState = PhysicalState.Healthy };
            }
        }

        // 2. Bajas, en orden de evento, vigilando el mínimo de plantilla tick a tick.
        int defeatTick = -1;
        int injuries = 0;
        int deaths = 0;
        var events = result.Events;
        for (int i = 0; i < events.Count && defeatTick < 0; i++)
        {
            var matchEvent = events[i];
            if (matchEvent.Team != 0 || matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            int index = IndexOf(players, matchEvent.Actor);
            if (index < 0)
            {
                continue;
            }

            switch (matchEvent.Type)
            {
                case EventType.Injury:
                    injuries++;
                    players[index] = matchEvent.Detail.StartsWith(SevereDetail, StringComparison.Ordinal)
                        ? players[index] with { PhysicalState = PhysicalState.SevereInjury }
                        : players[index] with
                        {
                            PhysicalState = PhysicalState.MinorInjury,
                            MinorInjuries = players[index].MinorInjuries + 1,
                        };
                    break;

                case EventType.Death:
                    deaths++;
                    players[index] = players[index] with { PhysicalState = PhysicalState.Dead };
                    break;

                default:
                    continue;
            }

            if (AvailableCount(players) < RunRules.MinimumAvailablePlayers)
            {
                defeatTick = matchEvent.Tick;
            }
        }

        // 3. Experiencia (RF-025) y nivel (RF-027), con los multiplicadores de perk fuera de partido.
        ApplyProgression(players, playedIds, benchIds, result, catalog);

        // 4. Partidos seguidos en el banquillo: los mercenarios abandonan tras 3 (RF-111). Quien los
        //    hace marcharse es el paquete X; el contador es del estado y se lleva aquí.
        for (int i = 0; i < players.Count; i++)
        {
            if (Contains(playedIds, players[i].Id))
            {
                players[i] = players[i] with { MatchesBenched = 0 };
            }
            else if (players[i].IsAvailable)
            {
                players[i] = players[i] with { MatchesBenched = players[i].MatchesBenched + 1 };
            }
        }

        bool won = result.Report.Winner == 0;
        var summary = new RunMatchSummary(
            node.Id,
            node.Kind,
            won,
            result.Report.Goals[0],
            result.Report.Goals[1],
            result.Report.Ticks,
            result.Report.WentToGoldenGoal,
            playedIds,
            benchIds,
            injuries,
            deaths,
            result.Report);

        var next = state
            .WithRoster(players)
            .WithNodeCompleted(node.Id, node.Kind, won ? NodeResult.Won : NodeResult.Lost);

        var outcome = Decide(node, won, defeatTick, result.Report.Ticks);
        return new Applied(next, summary, outcome);
    }

    /// <summary>
    /// Las dos únicas vías de derrota y la única de victoria (RF-002, RF-002b). La baja durante el
    /// partido manda sobre el resultado: si la run terminó en el tick 300, lo que pasara en el 900 ya
    /// no cuenta.
    /// </summary>
    private static RunOutcome Decide(MapNode node, bool won, int defeatTick, int ticks)
    {
        if (defeatTick >= 0)
        {
            return new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.NotEnoughPlayers, node.Id, defeatTick);
        }

        if (node.Kind != NodeKind.Boss)
        {
            // RF-002c: perder un partido ordinario no termina la run.
            return RunOutcome.InProgress;
        }

        if (!won)
        {
            return new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, node.Id, ticks);
        }

        return node.Act >= RunRules.Acts
            ? new RunOutcome(RunOutcomeKind.Victory, DefeatCause.None, node.Id, ticks)
            : RunOutcome.InProgress;
    }

    private static void ApplyProgression(
        List<RunPlayer> players,
        IReadOnlyList<int> playedIds,
        IReadOnlyList<int> benchIds,
        MatchResult result,
        Catalog catalog)
    {
        var played = new List<PlayerDefinition>(playedIds.Count);
        var bench = new List<PlayerDefinition>(benchIds.Count);
        for (int i = 0; i < players.Count; i++)
        {
            if (Contains(playedIds, players[i].Id))
            {
                played.Add(players[i].ToDefinition(catalog, applyMinorInjuryPenalty: false));
            }
            else if (Contains(benchIds, players[i].Id))
            {
                bench.Add(players[i].ToDefinition(catalog, applyMinorInjuryPenalty: false));
            }
        }

        var awards = ProgressionRules.AwardExperience(played, bench, catalog, catalog.Progression);
        for (int a = 0; a < awards.Count; a++)
        {
            int index = IndexOf(players, awards[a].PlayerId);
            if (index < 0)
            {
                continue;
            }

            var player = players[index];

            // +33% de experiencia para los canteranos (RF-114c). El resto de multiplicadores (habilidad
            // racial de los humanos, perks con modifyExperience) ya los ha aplicado Progression.
            int experience = awards[a].Experience;
            if (player.IsYouth)
            {
                experience = experience * (100 + RunRules.YouthExperienceBonusPercent) / 100;
            }

            var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
            definition = ProgressionRules.ApplyCounterDeltas(definition, result.CounterDeltas);

            int total = player.Experience + experience;
            int level = ProgressionRules.LevelFor(total, catalog.Progression);
            definition = ProgressionRules.LevelUp(definition, level, catalog.Progression);

            players[index] = player with
            {
                Experience = total,
                Level = definition.Level,
                Attributes = definition.Attributes,
                Counters = definition.Counters,
            };
        }
    }

    private static int AvailableCount(IReadOnlyList<RunPlayer> players)
    {
        int count = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].IsAvailable)
            {
                count++;
            }
        }

        return count;
    }

    private static int IndexOf(IReadOnlyList<RunPlayer> players, int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool Contains(IReadOnlyList<int> ids, int id)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return true;
            }
        }

        return false;
    }
}
