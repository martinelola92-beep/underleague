using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Balance;

/// <summary>Una fila de campaign.csv (docs/fase1-diseno.md §8, item 4 del encargo del paquete H).</summary>
public sealed record CampaignRow(
    string Build,
    int MatchIndex,
    int OpponentQuality,
    int Campaigns,
    double WinRate,
    double AvgLevel,
    double AvgStrength,
    double AvgTechnique,
    double ActivationsPerMatch);

/// <summary>Resultado de <see cref="BuildBatchRunner.RunMatrix"/>: filas listas para builds.csv y perks.csv.</summary>
public sealed record BuildsMatrixResult(
    IReadOnlyList<BuildCellResult> Cells,
    IReadOnlyList<PerkActivationResult> PerkActivations,
    int TotalMatches,
    TimeSpan Elapsed);

/// <summary>Resultado de <see cref="BuildBatchRunner.RunCampaign"/>: filas listas para campaign.csv.</summary>
public sealed record CampaignResult(IReadOnlyList<CampaignRow> Rows, TimeSpan Elapsed);

/// <summary>
/// Ejecuta los modos de <c>/Balance</c> de fase 1 sobre builds (docs/fase1-diseno.md §8): matriz build ×
/// rival (<see cref="RunMatrix"/>) y campaña con progresión (<see cref="RunCampaign"/>). Sigue el mismo
/// esquema de generación que <see cref="BatchRunner"/> (fase 0): cada instancia de equipo se genera una
/// sola vez con <c>RngStreams.Generation(seed, índice)</c> e ids de jugador desde <c>1 + índice*100</c>,
/// con una instancia "gemela" en <c>1000 + índice</c> para los emparejamientos de una build contra sí
/// misma, para no colisionar ids de jugador dentro del mismo partido.
/// </summary>
public static class BuildBatchRunner
{
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    /// <summary>Plantillas distintas por build sobre las que se promedia cada celda de la matriz (--rosters).</summary>
    public const int DefaultRosters = 25;

    /// <summary>Primer id de jugador del equipo que lleva los ids bajos en un partido de la matriz.</summary>
    private const int PrimaryIdBase = 1;

    /// <summary>Primer id de jugador del equipo que lleva los ids altos en un partido de la matriz.</summary>
    private const int SecondaryIdBase = 100001;

    /// <summary>Desplazamiento del índice de generación de la segunda build cuando no es comparable con la primera.</summary>
    private const int RosterOffset = 500;

    /// <summary>Un emparejamiento local-visitante entre dos builds (Home siempre el "sujeto" cuando no hay --home-away).</summary>
    private readonly record struct BuildPairing(string Home, string Away);

    private sealed class CellAccumulator
    {
        public int Matches;
        public int Wins;
        public int GoalsFor;
        public int GoalsAgainst;
        public int InjuriesFor;
        public int InjuriesAgainst;
        public int Tackles;
        public int PassChains;
        public int PassChainTotalLength;
        public int Activations;
    }

    private struct PerkAccumulator
    {
        public int Activations;
        public int MatchesWithActivation;
    }

    private sealed class CampaignMatchAccumulator
    {
        public int Matches;
        public int Wins;
        public long LevelSum;
        public long StrengthSum;
        public long TechniqueSum;
        public int RosterSamples;
        public long Activations;
    }

    /// <summary>
    /// Modo matriz (<c>--builds</c> [+ <c>--vs</c>] [+ <c>--home-away</c>]): con <paramref name="vsId"/>,
    /// cada build de <paramref name="buildIds"/> contra esa única build rival; sin él, todos-contra-todos
    /// entre las builds listadas. <paramref name="totalRuns"/> se reparte por igual entre los
    /// emparejamientos (resto a los primeros); con <paramref name="homeAway"/> cada emparejamiento se
    /// juega también invertido y las estadísticas de builds.csv se acumulan sobre el total. Cada partido
    /// entre dos builds distintas alimenta a la vez la celda (A,B) y la celda (B,A): no hace falta simular
    /// dos veces para tener las dos perspectivas.
    /// </summary>
    public static BuildsMatrixResult RunMatrix(
        Catalog catalog,
        IReadOnlyDictionary<string, BuildConfig> allBuilds,
        IReadOnlyList<string> buildIds,
        string? vsId,
        bool homeAway,
        int totalRuns,
        ulong seed,
        int rosters = DefaultRosters)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(allBuilds);
        ArgumentNullException.ThrowIfNull(buildIds);
        ArgumentOutOfRangeException.ThrowIfLessThan(rosters, 1);

        var pairings = MatrixPairings(buildIds, vsId);
        if (pairings.Count == 0)
        {
            throw new ArgumentException(
                "--builds necesita al menos dos builds para el modo todos-contra-todos (sin --vs), "
                    + "o al menos una con --vs");
        }

        var orderedParticipants = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var pairing in pairings)
        {
            orderedParticipants.Add(pairing.Home);
            orderedParticipants.Add(pairing.Away);
        }

        var participantList = orderedParticipants.ToList();
        var indexByBuild = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < participantList.Count; i++)
        {
            string id = participantList[i];
            if (!allBuilds.ContainsKey(id))
            {
                throw new ArgumentException($"build desconocida '{id}' (no existe en data/balance/builds/)");
            }

            indexByBuild[id] = i;
        }

        // Paquete I: una sola plantilla por build hacía la medida inservible. Con una plantilla generada
        // por build, la tasa de victoria de la MISMA build contra su referencia iba del 16% al 60% según
        // qué jugadores le hubiera tocado sacar al generador (sd de 15 puntos entre plantillas, medido con
        // 20 plantillas x 200 partidos): lo que medía builds.csv era el dado de la generación, no los
        // perks. Ahora cada celda se promedia sobre `rosters` plantillas distintas y, cuando las dos
        // builds del emparejamiento comparten raza y calidad, las dos usan el MISMO índice de generación:
        // los dos equipos son los mismos jugadores y la única diferencia son los perks y la alineación,
        // que es exactamente lo que §8 quiere medir.
        var instances = new Dictionary<(string Id, int GenIndex, int IdBase), TeamSetup>();

        TeamSetup GetInstance(string id, int genIndex, int idBase)
        {
            var key = (id, genIndex, idBase);
            if (instances.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var rng = RngStreams.Generation(seed, genIndex);
            TeamSetup team;
            try
            {
                team = allBuilds[id].ToTeamSetup(ref rng, catalog, idBase);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"build '{id}': {ex.Message}", ex);
            }

            instances[key] = team;
            return team;
        }

        bool SameRosterBase(string a, string b) =>
            allBuilds[a].Race == allBuilds[b].Race && allBuilds[a].Quality == allBuilds[b].Quality;

        int pairingCount = pairings.Count;
        int baseCount = totalRuns / pairingCount;
        int remainder = totalRuns % pairingCount;

        var cellAcc = new Dictionary<(string Build, string Opponent), CellAccumulator>();
        var perkAcc = new Dictionary<(string PerkId, string Build), PerkAccumulator>();
        var matchesByBuild = new Dictionary<string, int>(StringComparer.Ordinal);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        int globalMatchIndex = 0;
        int totalSimulated = 0;

        // Variantes de un emparejamiento: orientación (quién es local) x reparto de ids de jugador. El
        // reparto de ids importa porque los desempates del motor (jugador más cercano al balón, empate de
        // utilidad) van por id ascendente: con ids fijos el equipo de ids bajos gana entre 2 y 3 puntos de
        // más (medido: 53,1% / 52,2% / 52,0% por raza con plantillas idénticas; intercambiando los ids,
        // 50,7% / 50,5% / 49,9%). Se alternan siempre, haya o no --home-away.
        int variants = homeAway ? 4 : 2;

        for (int p = 0; p < pairingCount; p++)
        {
            var pairing = pairings[p];
            int runsForPairing = baseCount + (p < remainder ? 1 : 0);
            if (runsForPairing == 0)
            {
                continue;
            }

            bool selfPairing = string.Equals(pairing.Home, pairing.Away, StringComparison.Ordinal);
            bool paired = !selfPairing && SameRosterBase(pairing.Home, pairing.Away);

            for (int k = 0; k < runsForPairing; k++)
            {
                int variant = k % variants;
                int rosterIndex = (k / variants) % rosters;
                bool swapOrientation = homeAway && (variant % 2) == 1;
                bool swapIds = (variant / (homeAway ? 2 : 1)) % 2 == 1;

                // Índice de generación: la build "A" del emparejamiento usa rosterIndex; la "B" usa el
                // mismo si son comparables (misma raza y calidad) y uno desplazado si no.
                int genA = rosterIndex;
                int genB = paired ? rosterIndex : RosterOffset + rosterIndex;
                int idBaseA = swapIds ? SecondaryIdBase : PrimaryIdBase;
                int idBaseB = swapIds ? PrimaryIdBase : SecondaryIdBase;

                var teamA = GetInstance(pairing.Home, genA, idBaseA);
                var teamB = GetInstance(pairing.Away, genB, idBaseB);

                string homeId = swapOrientation ? pairing.Away : pairing.Home;
                string awayId = swapOrientation ? pairing.Home : pairing.Away;
                TeamSetup homeTeam = swapOrientation ? teamB : teamA;
                TeamSetup awayTeam = swapOrientation ? teamA : teamB;

                var setup = new MatchSetup(homeTeam, awayTeam, Referee);
                var homeTeamPlayerIds = new HashSet<int>(homeTeam.Players.Select(pl => pl.Id));

                ulong matchSeed = RngStreams.MatchSeed(seed, globalMatchIndex);
                globalMatchIndex++;
                totalSimulated++;

                MatchResult result;
                try
                {
                    result = Simulator.Run(setup, matchSeed, catalog, new SimConfig(CollectLog: false));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"build '{homeId}' vs '{awayId}': {ex.Message}", ex);
                }

                AccumulateMatch(cellAcc, perkAcc, matchesByBuild, result.Report, homeId, awayId, homeTeamPlayerIds);
            }
        }

        stopwatch.Stop();

        // La build de --vs también entra en builds.csv: es la referencia contra la que se normalizan las
        // métricas de §8 (ADR 0012) y sin su fila no se puede calcular el cociente.
        var requested = new HashSet<string>(buildIds, StringComparer.Ordinal);
        if (vsId is not null)
        {
            requested.Add(vsId);
        }
        var cells = cellAcc
            .Where(kv => requested.Contains(kv.Key.Build))
            .Select(kv => new BuildCellResult(
                kv.Key.Build, kv.Key.Opponent, kv.Value.Matches, kv.Value.Wins,
                kv.Value.GoalsFor, kv.Value.GoalsAgainst, kv.Value.InjuriesFor, kv.Value.InjuriesAgainst,
                kv.Value.Tackles, kv.Value.PassChains, kv.Value.PassChainTotalLength, kv.Value.Activations))
            .OrderBy(c => c.Build, StringComparer.Ordinal)
            .ThenBy(c => c.Opponent, StringComparer.Ordinal)
            .ToList();

        // perks.csv: una fila por (build, perk) que la build asigna estáticamente a algún titular, aunque
        // nunca llegue a activarse (0 activaciones es justo lo que noDeadPerks necesita poder detectar).
        var perkRows = new List<PerkActivationResult>();
        foreach (var buildId in buildIds)
        {
            if (!allBuilds.TryGetValue(buildId, out var build))
            {
                continue;
            }

            int matches = matchesByBuild.GetValueOrDefault(buildId);
            var assignedPerks = build.Perks
                .Select(assignment => assignment.Perk)
                .Distinct(StringComparer.Ordinal);

            foreach (var perkId in assignedPerks)
            {
                perkAcc.TryGetValue((perkId, buildId), out var acc);
                perkRows.Add(new PerkActivationResult(perkId, buildId, matches, acc.MatchesWithActivation));
            }
        }

        perkRows = perkRows
            .OrderBy(r => r.PerkId, StringComparer.Ordinal)
            .ThenBy(r => r.Build, StringComparer.Ordinal)
            .ToList();

        return new BuildsMatrixResult(cells, perkRows, totalSimulated, stopwatch.Elapsed);
    }

    /// <summary>
    /// Modo campaña (<c>--builds --campaign N</c>): para cada build de <paramref name="buildIds"/>,
    /// <paramref name="campaigns"/> campañas independientes (semillas distintas) de
    /// <paramref name="matchesPerCampaign"/> partidos consecutivos contra la build <c>human_none</c> de
    /// calidad creciente (46, 48, ... 46+2(N-1)), arrastrando experiencia, niveles y contadores de carrera
    /// entre partidos de la misma campaña (§6). El rival no progresa: se regenera cada partido con la
    /// calidad que toque y sin historial.
    /// </summary>
    public static CampaignResult RunCampaign(
        Catalog catalog,
        IReadOnlyDictionary<string, BuildConfig> allBuilds,
        IReadOnlyList<string> buildIds,
        int matchesPerCampaign,
        int campaigns,
        ulong seed,
        bool homeAway)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(allBuilds);
        ArgumentNullException.ThrowIfNull(buildIds);

        if (!allBuilds.TryGetValue("human_none", out var opponentBuild))
        {
            throw new ArgumentException(
                "--campaign necesita la build 'human_none' en data/balance/builds/ como rival de progresión (docs/fase1-diseno.md §8)");
        }

        var orderedBuildIds = buildIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        foreach (var id in orderedBuildIds)
        {
            if (!allBuilds.ContainsKey(id))
            {
                throw new ArgumentException($"build desconocida '{id}' (no existe en data/balance/builds/)");
            }
        }

        var rows = new List<CampaignRow>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        int genIndex = 0;
        int matchIndexGlobal = 0;

        foreach (var buildId in orderedBuildIds)
        {
            var build = allBuilds[buildId];
            var matchAcc = new CampaignMatchAccumulator[matchesPerCampaign];
            for (int m = 0; m < matchesPerCampaign; m++)
            {
                matchAcc[m] = new CampaignMatchAccumulator();
            }

            for (int c = 0; c < campaigns; c++)
            {
                int buildGenIndex = genIndex++;
                var buildRng = RngStreams.Generation(seed, buildGenIndex);
                int buildFirstId = 1 + (buildGenIndex * 100);
                TeamSetup initialTeam;
                try
                {
                    initialTeam = build.ToTeamSetup(ref buildRng, catalog, buildFirstId);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"build '{buildId}': {ex.Message}", ex);
                }

                var starterIds = initialTeam.Lineup.Slots.Select(s => s.PlayerId).ToList();
                var allIds = initialTeam.Players.Select(pl => pl.Id).OrderBy(id => id).ToList();
                var benchIds = allIds.Except(starterIds).ToList();
                var buildPlayerIds = new HashSet<int>(allIds);

                var players = initialTeam.Players.ToDictionary(pl => pl.Id);
                var xpTotals = new Dictionary<int, int>();

                for (int m = 0; m < matchesPerCampaign; m++)
                {
                    int opponentQuality = 46 + (2 * m);
                    int oppGenIndex = genIndex++;
                    var oppRng = RngStreams.Generation(seed, oppGenIndex);
                    int oppFirstId = 1 + (oppGenIndex * 100);
                    var opponentTeam = opponentBuild.ToTeamSetup(ref oppRng, catalog, oppFirstId, opponentQuality);

                    var currentPlayers = players.Values.OrderBy(pl => pl.Id).ToList();
                    var buildTeamThisMatch = initialTeam with { Players = currentPlayers };

                    // Con --home-away se alterna de partido en partido dentro de la misma campaña para
                    // que la mitad se juegue como local y la mitad como visitante (§8: elimina el sesgo
                    // local/visitante también en la campaña).
                    bool buildHome = !homeAway || m % 2 == 0;
                    var homeTeam = buildHome ? buildTeamThisMatch : opponentTeam;
                    var awayTeam = buildHome ? opponentTeam : buildTeamThisMatch;
                    var setup = new MatchSetup(homeTeam, awayTeam, Referee);

                    ulong matchSeed = RngStreams.MatchSeed(seed, matchIndexGlobal);
                    matchIndexGlobal++;

                    MatchResult result;
                    try
                    {
                        result = Simulator.Run(setup, matchSeed, catalog, new SimConfig(CollectLog: false));
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException(
                            $"build '{buildId}' (campaña {c}, partido {m + 1}): {ex.Message}", ex);
                    }

                    var report = result.Report;
                    bool buildWon = buildHome ? report.Winner == 0 : report.Winner == 1;

                    // Progresión (§6): 100% de experiencia a los 7 titulares que jugaron, 45% (tuning) a
                    // los suplentes; niveles recalculados desde la experiencia acumulada de la campaña;
                    // contadores de carrera sumados desde los perks accumulatesAcrossMatches del partido.
                    var awards = ProgressionRules.AwardExperience(starterIds, benchIds, catalog.Progression);
                    foreach (var award in awards)
                    {
                        xpTotals[award.PlayerId] = xpTotals.GetValueOrDefault(award.PlayerId) + award.Experience;
                    }

                    foreach (var id in allIds)
                    {
                        int newLevel = ProgressionRules.LevelFor(xpTotals.GetValueOrDefault(id), catalog.Progression);
                        players[id] = ProgressionRules.LevelUp(players[id], newLevel, catalog.Progression);
                    }

                    foreach (var id in allIds)
                    {
                        players[id] = ProgressionRules.ApplyCounterDeltas(players[id], result.CounterDeltas);
                    }

                    int activations = report.PerkActivations.Count(a => buildPlayerIds.Contains(a.OwnerId));

                    var acc = matchAcc[m];
                    acc.Matches++;
                    if (buildWon)
                    {
                        acc.Wins++;
                    }

                    acc.Activations += activations;
                    foreach (var p in players.Values)
                    {
                        acc.LevelSum += p.Level;
                        acc.StrengthSum += p.Attributes.Strength;
                        acc.TechniqueSum += p.Attributes.Technique;
                    }

                    acc.RosterSamples += players.Count;
                }
            }

            for (int m = 0; m < matchesPerCampaign; m++)
            {
                var acc = matchAcc[m];
                rows.Add(new CampaignRow(
                    buildId,
                    m,
                    46 + (2 * m),
                    campaigns,
                    acc.Matches > 0 ? 100.0 * acc.Wins / acc.Matches : 0.0,
                    acc.RosterSamples > 0 ? (double)acc.LevelSum / acc.RosterSamples : 0.0,
                    acc.RosterSamples > 0 ? (double)acc.StrengthSum / acc.RosterSamples : 0.0,
                    acc.RosterSamples > 0 ? (double)acc.TechniqueSum / acc.RosterSamples : 0.0,
                    acc.Matches > 0 ? (double)acc.Activations / acc.Matches : 0.0));
            }
        }

        stopwatch.Stop();
        return new CampaignResult(rows, stopwatch.Elapsed);
    }

    private static List<BuildPairing> MatrixPairings(IReadOnlyList<string> builds, string? vsId)
    {
        var pairings = new List<BuildPairing>();
        if (vsId is not null)
        {
            foreach (var build in builds)
            {
                pairings.Add(new BuildPairing(build, vsId));
            }
        }
        else
        {
            for (int i = 0; i < builds.Count; i++)
            {
                for (int j = i + 1; j < builds.Count; j++)
                {
                    pairings.Add(new BuildPairing(builds[i], builds[j]));
                }
            }
        }

        return pairings;
    }

    /// <summary>
    /// Un partido entre dos builds da datos para las dos celdas de la matriz a la vez: (home,away) desde
    /// la perspectiva de home y (away,home) desde la de away. tacklesPerMatch e injuriesFor/Against se
    /// reparten por equipo con PlayerMatchStats.Team; passChainAvgLength es una estadística de todo el
    /// partido (no se atribuye a un lado) y se suma igual a las dos celdas.
    /// </summary>
    private static void AccumulateMatch(
        Dictionary<(string Build, string Opponent), CellAccumulator> cellAcc,
        Dictionary<(string PerkId, string Build), PerkAccumulator> perkAcc,
        Dictionary<string, int> matchesByBuild,
        MatchReport report,
        string homeBuild,
        string awayBuild,
        HashSet<int> homeTeamPlayerIds)
    {
        int homeGoals = report.Goals[0];
        int awayGoals = report.Goals[1];
        bool homeWon = report.Winner == 0;

        int tacklesHome = 0, tacklesAway = 0, injuriesHome = 0, injuriesAway = 0;
        foreach (var stat in report.Players)
        {
            if (stat.Team == 0)
            {
                tacklesHome += stat.Tackles;
                if (stat.Injured)
                {
                    injuriesHome++;
                }
            }
            else
            {
                tacklesAway += stat.Tackles;
                if (stat.Injured)
                {
                    injuriesAway++;
                }
            }
        }

        int activationsHome = 0, activationsAway = 0;
        foreach (var activation in report.PerkActivations)
        {
            if (homeTeamPlayerIds.Contains(activation.OwnerId))
            {
                activationsHome++;
            }
            else
            {
                activationsAway++;
            }
        }

        // Las cadenas de pases van por equipo (paquete I): la fila de cada build recibe las suyas, no las
        // del partido entero, que es lo que buildsWinDifferently necesita comparar.
        AddCell(cellAcc, homeBuild, awayBuild, homeWon, homeGoals, awayGoals, injuriesHome, injuriesAway,
            tacklesHome, report.PassChainsByTeam[0], report.PassChainTotalLengthByTeam[0], activationsHome);
        AddCell(cellAcc, awayBuild, homeBuild, !homeWon, awayGoals, homeGoals, injuriesAway, injuriesHome,
            tacklesAway, report.PassChainsByTeam[1], report.PassChainTotalLengthByTeam[1], activationsAway);

        matchesByBuild[homeBuild] = matchesByBuild.GetValueOrDefault(homeBuild) + 1;
        matchesByBuild[awayBuild] = matchesByBuild.GetValueOrDefault(awayBuild) + 1;

        var homePerkTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        var awayPerkTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var summary in report.PerksSummary)
        {
            var totals = homeTeamPlayerIds.Contains(summary.OwnerId) ? homePerkTotals : awayPerkTotals;
            totals[summary.PerkId] = totals.GetValueOrDefault(summary.PerkId) + summary.Activations;
        }

        AddPerkSide(perkAcc, homePerkTotals, homeBuild);
        AddPerkSide(perkAcc, awayPerkTotals, awayBuild);
    }

    private static void AddCell(
        Dictionary<(string Build, string Opponent), CellAccumulator> cellAcc,
        string build,
        string opponent,
        bool won,
        int goalsFor,
        int goalsAgainst,
        int injuriesFor,
        int injuriesAgainst,
        int tackles,
        int passChains,
        int passChainTotalLength,
        int activations)
    {
        var key = (build, opponent);
        if (!cellAcc.TryGetValue(key, out var acc))
        {
            acc = new CellAccumulator();
            cellAcc[key] = acc;
        }

        acc.Matches++;
        if (won)
        {
            acc.Wins++;
        }

        acc.GoalsFor += goalsFor;
        acc.GoalsAgainst += goalsAgainst;
        acc.InjuriesFor += injuriesFor;
        acc.InjuriesAgainst += injuriesAgainst;
        acc.Tackles += tackles;
        acc.PassChains += passChains;
        acc.PassChainTotalLength += passChainTotalLength;
        acc.Activations += activations;
    }

    private static void AddPerkSide(
        Dictionary<(string PerkId, string Build), PerkAccumulator> perkAcc, Dictionary<string, int> totals, string build)
    {
        foreach (var (perkId, count) in totals)
        {
            if (count <= 0)
            {
                continue;
            }

            var key = (perkId, build);
            perkAcc.TryGetValue(key, out var acc);
            acc.Activations += count;
            acc.MatchesWithActivation += 1;
            perkAcc[key] = acc;
        }
    }
}
