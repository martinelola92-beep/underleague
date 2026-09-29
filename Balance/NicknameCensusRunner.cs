using System.Diagnostics;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Nicknames;

namespace Underleague.Balance;

/// <summary>
/// Un apodo en el censo: en cuántas runs lo lleva al terminar al menos un jugador, y cuántos jugadores lo
/// llevan como apodo final (el de mayor prioridad que cumplen, <see cref="NicknameSystem.For(RunPlayer, NicknameCatalog)"/>).
/// </summary>
public sealed record NicknameCensusCell(string NicknameId, int RunsWith, long Players, int RunsEligible, long PlayersEligible, int MaxValue)
{
    public double RunShare(int runs) => runs == 0 ? 0 : (double)RunsWith / runs;
}

/// <summary>Resultado del modo <c>--nickname-census</c>.</summary>
public sealed record NicknameCensusResult(
    int Runs,
    long PlayersWhoPlayed,
    long PlayersWithNickname,
    IReadOnlyList<NicknameCensusCell> Cells,
    TimeSpan Elapsed);

/// <summary>
/// Modo <c>--nickname-census N</c> de <c>/Balance</c> (ADR 0163, sección «Censo»): juega N runs completas con
/// la política contextual y, al terminar cada una, cuenta qué apodo lleva cada jugador que pisó el campo
/// (vivos y caídos: un muerto también se llevó su apodo a la tumba). Un apodo que casi todas las runs tienen
/// no significa nada, y uno que nunca sale es un umbral inalcanzable: el censo da la razón de runs con al
/// menos un portador y el reparto de jugadores.
///
/// <para>Misma disciplina que <see cref="BetCensusRunner"/>: semilla de la run <c>i</c> = <c>seed*100000+i</c>,
/// <c>Parallel.For</c> por índice con un catálogo por hilo y reducción en orden.</para>
/// </summary>
public static class NicknameCensusRunner
{
    public static NicknameCensusResult Run(
        Catalog catalog,
        IReadOnlyDictionary<string, string> dataFiles,
        ulong seed,
        int runs,
        Func<Catalog>? catalogPerThread = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dataFiles);
        ArgumentOutOfRangeException.ThrowIfLessThan(runs, 1);

        var standard = StandardRunSystems.FromJson(dataFiles);
        var bosses = BossCatalog.FromJson(dataFiles);
        var races = FullRunRunner.LaunchRaces(catalog);
        var options = RunPolicyOptions.For(PurchaseDoctrine.Contextual);
        var all = standard.Nicknames.All;

        var stopwatch = Stopwatch.StartNew();
        var finals = new RunState?[runs];
        Parallel.For(0, runs, i =>
        {
            var threadCatalog = catalogPerThread?.Invoke() ?? BalanceCatalogs.Current(catalog);
            var setup = FullRunRunner.SetupFor(races[i % races.Count], standard, dataFiles);
            finals[i] = RunPolicy.Play(setup, BetCensusRunner.RunSeed(seed, i), threadCatalog, standard, bosses, options).FinalState;
        });

        var players = new long[all.Count];
        var runsWith = new int[all.Count];
        var eligible = new long[all.Count];
        var runsEligible = new int[all.Count];
        var maxValue = new int[all.Count];
        long played = 0;
        long named = 0;
        for (int i = 0; i < runs; i++)
        {
            var state = finals[i]!;
            var seen = new bool[all.Count];
            var seenEligible = new bool[all.Count];
            for (int p = 0; p < state.Roster.Count; p++)
            {
                if (state.Roster[p].Career.Matches <= 0)
                {
                    continue;
                }

                played++;
                for (int n = 0; n < all.Count; n++)
                {
                    int value = NicknameSystem.Value(state.Roster[p].Career, all[n].Stat);
                    maxValue[n] = Math.Max(maxValue[n], value);
                    if (value >= all[n].Threshold)
                    {
                        eligible[n]++;
                        seenEligible[n] = true;
                    }
                }

                var nickname = NicknameSystem.For(state.Roster[p], standard.Nicknames);
                if (nickname is null)
                {
                    continue;
                }

                named++;
                for (int n = 0; n < all.Count; n++)
                {
                    if (all[n].Id == nickname.Id)
                    {
                        players[n]++;
                        seen[n] = true;
                    }
                }
            }

            for (int n = 0; n < all.Count; n++)
            {
                if (seen[n])
                {
                    runsWith[n]++;
                }

                if (seenEligible[n])
                {
                    runsEligible[n]++;
                }
            }
        }

        stopwatch.Stop();
        var cells = new List<NicknameCensusCell>();
        for (int n = 0; n < all.Count; n++)
        {
            cells.Add(new NicknameCensusCell(all[n].Id, runsWith[n], players[n], runsEligible[n], eligible[n], maxValue[n]));
        }

        return new NicknameCensusResult(runs, played, named, cells, stopwatch.Elapsed);
    }
}
