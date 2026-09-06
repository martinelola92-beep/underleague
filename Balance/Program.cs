using System.Globalization;
using Underleague.Balance;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems.Items;

// Punto de entrada de /Balance (docs/fase0-diseno.md §4, docs/balance.md). Sin paquetes NuGet, parseo
// manual de argumentos (Options.cs).

Options options;
try
{
    options = Options.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"error de argumentos: {ex.Message}");
    PrintUsage();
    return 1;
}

try
{
    string dataPath = ResolveDataPath(options.DataPath);
    var dataFiles = LoadDataFiles(dataPath);
    Catalog catalog = DataLoader.FromJson(dataFiles);

    if (options.Describe is { } language)
    {
        // --describe [es|en]: catálogo de perks con descripción generada y distribución RF-069
        // (docs/fase1-diseno.md §8). No toca reference.json ni simula ningún partido.
        RunDescribe(catalog, language);
        return 0;
    }

    if (options.FullRuns is { } fullRuns)
    {
        // --full-runs N: N runs completas con la política automática (fase2-diseno.md §10). Responde a
        // la pregunta que la curva de puertas deja abierta: si la economía permite llegar a cada puerta
        // con la build que esa puerta exige.
        FullRunResult full = FullRunRunner.Run(catalog, dataFiles, options.Seed, fullRuns, options.IgnoreScouting, options.RiskAversion);

        var fullSummary = full.Metrics
            .Select(m => new MetricRow(m.Name, m.Value, m.RangeMin, m.RangeMax, m.Status))
            .ToList();

        WriteRunsCsv(options.OutDir!, full.Runs);

        // La política de control que esquiva los mercados (ADR 0055) no cabe en la columna `doctrine`
        // de runs.csv —es contextual— y es el perfil "sin build" de la ADR 0057. Se vuelca aparte para
        // poder desglosarla por acto como a las otras tres.
        WriteRunsCsv(options.OutDir!, full.Marketless, "runs-nomarket.csv");
        WriteSummaryCsv(options.OutDir!, fullSummary);

        if (!options.Quiet)
        {
            Console.WriteLine();
            PrintSummaryTable(fullSummary);
            Console.WriteLine();
            double fullSeconds = full.Elapsed.TotalSeconds;
            Console.WriteLine(
                $"{full.Runs.Count} runs ({full.TotalMatches} partidos, {FullRunRunner.Doctrines.Count} doctrinas) en {fullSeconds:F2} s");
            Console.WriteLine($"CSV escritos en {options.OutDir}");
        }

        return full.Metrics.Any(m => m.Status == "OUT") ? 1 : 0;
    }

    if (options.PerkValues)
    {
        // --perk-values: cuánto vale cada perk (ADR 0038). Alimenta data/economy/perk-values.json, de
        // donde sale el peso de cada perk en el pool de recompensas y en el surtido del mercado.
        var perkRows = PerkValueRunner.Run(
            catalog, options.Seed, options.Rosters, options.RunsExplicit ? options.Runs : 16);

        WritePerkValuesCsv(options.OutDir!, perkRows);

        if (!options.Quiet)
        {
            Console.WriteLine();
            PerkValueRunner.PrintTable(perkRows);
            Console.WriteLine();
            Console.WriteLine("bloque 'values' para data/economy/perk-values.json:");
            Console.WriteLine(PerkValueRunner.ToJsonValues(perkRows));
            Console.WriteLine($"CSV escritos en {options.OutDir}");
        }

        return 0;
    }

    if (options.BossGate)
    {
        // --boss-gate: la curva de puertas de la ADR 0033 con partidos directos build-contra-jefe
        // (docs/fase2-diseno.md, paquete Y). --rosters = plantillas por celda; --runs = partidos por
        // plantilla (múltiplo de 4: local/visitante x reparto de ids).
        var bossCatalog = BossCatalog.FromJson(dataFiles);
        var bossBuilds = BuildConfig.LoadAll(Path.Combine(dataPath, "balance", "builds"));
        var bossGroups = BuildGroups.Load(Path.Combine(dataPath, "balance", "groups.json"));
        int matchesPerRoster = options.RunsExplicit ? options.Runs : 8;

        var itemCatalog = ItemLoader.FromJson(dataFiles);

        BossGateResult gate = BossGateRunner.Run(
            catalog, bossCatalog, bossBuilds, bossGroups.QualityLevels, bossGroups.ActDensity, itemCatalog,
            options.Seed, options.Rosters, matchesPerRoster);

        WriteBossGateCsv(options.OutDir!, gate.Cells);

        if (!options.Quiet)
        {
            Console.WriteLine();
            BossGateRunner.PrintTable(gate.Metrics, bossCatalog);
            Console.WriteLine();
            Console.WriteLine($"{gate.TotalMatches} partidos en {gate.Elapsed.TotalSeconds:F2} s");
            Console.WriteLine($"CSV escritos en {options.OutDir}");
        }

        return gate.Metrics.Any(m => m.Status == "OUT") ? 1 : 0;
    }

    if (options.Builds is { } requestedBuildIds)
    {
        // --builds [--vs|--campaign] [--home-away]: modos de fase 1 sobre builds (docs/fase1-diseno.md
        // §8). Tampoco usan reference.json: las builds y sus emparejamientos vienen de --builds/--vs.
        string buildsDir = Path.Combine(dataPath, "balance", "builds");
        var allBuilds = BuildConfig.LoadAll(buildsDir);
        var buildIds = ResolveBuildIds(requestedBuildIds, allBuilds);

        if (options.Campaign is { } campaignLength)
        {
            int campaigns = options.RunsExplicit ? options.Runs : 60;
            CampaignResult campaign = BuildBatchRunner.RunCampaign(
                catalog, allBuilds, buildIds, campaignLength, campaigns, options.Seed, options.HomeAway);

            WriteCampaignCsv(options.OutDir!, campaign.Rows);

            if (!options.Quiet)
            {
                Console.WriteLine();
                PrintCampaignTables(campaign.Rows, campaignLength);
                Console.WriteLine();
                double campaignSeconds = campaign.Elapsed.TotalSeconds;
                Console.WriteLine(
                    $"{buildIds.Count} builds x {campaigns} campañas x {campaignLength} partidos en {campaignSeconds:F2} s");
                Console.WriteLine($"CSV escritos en {options.OutDir}");
            }

            return 0;
        }

        BuildsMatrixResult matrix = BuildBatchRunner.RunMatrix(
            catalog, allBuilds, buildIds, options.Vs, options.HomeAway, options.Runs, options.Seed, options.Rosters);

        WriteBuildsCsv(options.OutDir!, matrix.Cells);
        WritePerksCsv(options.OutDir!, matrix.PerkActivations);

        if (!options.Quiet)
        {
            Console.WriteLine();
            PrintBuildsTable(matrix.Cells);
            Console.WriteLine();
            double matrixSeconds = matrix.Elapsed.TotalSeconds;
            double matrixPerSecond = matrixSeconds > 0 ? matrix.TotalMatches / matrixSeconds : 0;
            Console.WriteLine($"{matrix.TotalMatches} partidos en {matrixSeconds:F2} s ({matrixPerSecond:F1} partidos/s)");
            Console.WriteLine($"CSV escritos en {options.OutDir}");
        }

        return 0;
    }

    string referenceContent = File.ReadAllText(options.TeamsPath);
    ReferenceConfig reference = ReferenceConfig.Load(referenceContent);

    if (options.UtilityCensus is { } censusMatches)
    {
        // --utility-census N: censo del volcado de utilidad (RT-098) sobre el primer emparejamiento de
        // reference.json. Instrumento de medición, sin métricas ni puertas.
        var census = UtilityCensusRunner.Run(catalog, reference, options.Seed, censusMatches);
        PrintUtilityCensus(census);
        return 0;
    }

    if (options.MatchSeed is { } matchSeed)
    {
        // --match-seed: un único partido reproducido con esa semilla de motor exacta, sin métricas de
        // lote (no tienen sentido sobre un solo partido) y sin el código de salida 1/OUT (docs/sim-debug,
        // revisión independiente de fase 0).
        BatchResult single = BatchRunner.RunSingle(options, catalog, reference, matchSeed);

        WriteMatchesCsv(options.OutDir!, single.Matches);
        WritePlayersCsv(options.OutDir!, single.Players);

        if (options.Log)
        {
            Console.WriteLine();
            Console.WriteLine("--- log del partido (Report.Log) ---");
            if (single.FirstMatchLog.Count == 0)
            {
                Console.WriteLine("(sin líneas: --log no estaba activo)");
            }
            else
            {
                foreach (var line in single.FirstMatchLog)
                {
                    Console.WriteLine(line);
                }
            }
        }

        if (options.DumpUtility is not null)
        {
            Console.WriteLine();
            Console.WriteLine("--- tabla de utilidad (--dump-utility) ---");
            if (single.FirstMatchUtilityDump is null)
            {
                Console.WriteLine("(sin volcado: el jugador pedido no llegó a decidir en ningún tick >= el pedido)");
            }
            else
            {
                PrintUtilityDump(single.FirstMatchUtilityDump);
            }
        }

        if (!options.Quiet)
        {
            Console.WriteLine();
            Console.WriteLine($"partido con --match-seed {matchSeed} escrito en {options.OutDir}");
        }

        return 0;
    }

    BatchResult batch = BatchRunner.Run(options, catalog, reference);

    var metrics = Metrics.Compute(batch.Matches, reference);

    WriteMatchesCsv(options.OutDir!, batch.Matches);
    WritePlayersCsv(options.OutDir!, batch.Players);
    WriteSummaryCsv(options.OutDir!, metrics);

    if (options.Log)
    {
        Console.WriteLine();
        Console.WriteLine("--- log del primer partido (Report.Log) ---");
        if (batch.FirstMatchLog.Count == 0)
        {
            Console.WriteLine("(sin líneas: --log no estaba activo en SimConfig del primer partido, o el partido no llegó a ejecutarse)");
        }
        else
        {
            foreach (var line in batch.FirstMatchLog)
            {
                Console.WriteLine(line);
            }
        }
    }

    if (options.DumpUtility is not null)
    {
        Console.WriteLine();
        Console.WriteLine("--- tabla de utilidad del primer partido (--dump-utility) ---");
        if (batch.FirstMatchUtilityDump is null)
        {
            Console.WriteLine("(sin volcado: el tick/jugador pedido no coincidió en el primer partido)");
        }
        else
        {
            PrintUtilityDump(batch.FirstMatchUtilityDump);
        }
    }

    bool anyOut = metrics.Any(m => m.Status == "OUT");

    if (!options.Quiet)
    {
        Console.WriteLine();
        PrintSummaryTable(metrics);
        Console.WriteLine();
        double seconds = batch.Elapsed.TotalSeconds;
        double matchesPerSecond = seconds > 0 ? batch.Matches.Count / seconds : 0;
        Console.WriteLine($"{batch.Matches.Count} partidos en {seconds:F2} s ({matchesPerSecond:F1} partidos/s)");
        Console.WriteLine($"CSV escritos en {options.OutDir}");
    }

    return anyOut ? 1 : 0;
}
catch (DataException ex)
{
    Console.Error.WriteLine($"error cargando /data: {ex.Message}");
    return 1;
}
catch (Exception ex) when (ex is FormatException or IOException or DirectoryNotFoundException or FileNotFoundException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}
catch (ArgumentException ex)
{
    // Build inválida (Simulator.Run: slots por rareza, positionOnly, tagsRequired/tagsForbidden) o id de
    // build desconocido en --builds/--vs/--campaign (docs/fase1-diseno.md §5, §8): el mensaje ya nombra
    // la build, el jugador y el perk (Sim/Engine/Simulator.cs ValidatePerks). Aborta el lote sin más.
    Console.Error.WriteLine($"error de build: {ex.Message}");
    return 1;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"error de build: {ex.Message}");
    return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine(
        """
        uso: dotnet run --project Balance -- [opciones]
          --runs N            total de partidos (por defecto 1000)
          --seed S            semilla base, entero sin signo (por defecto 1)
          --match-seed S      un único partido con esta semilla de motor exacta (primer emparejamiento de
                               --teams); ignora --runs, no escribe summary.csv, código de salida siempre 0
          --teams path        por defecto data/balance/reference.json
          --data path         por defecto: subir directorios desde cwd hasta encontrar data/
          --out dir           por defecto out/<seed>/
          --log               imprime el log del primer partido (o del partido de --match-seed)
          --dump-utility P:T  SimConfig.DumpUtility para el primer partido; imprime la tabla
          --quiet             sin resumen por consola
          --describe [es|en]  catálogo de perks con descripción y distribución RF-069; por defecto "es"
          --builds a,b,c      modos de fase 1 sobre builds (data/balance/builds/*.json); "all" = todas
          --vs id             build rival única del modo matriz (sin ella: todos-contra-todos); requiere --builds
          --campaign N        modo campaña: N partidos consecutivos por build contra human_none de calidad
                               creciente; requiere --builds; --runs = campañas por build (por defecto 60)
          --home-away         cada emparejamiento (matriz o campaña) también con los equipos invertidos
          --rosters N         plantillas distintas por build sobre las que promediar cada celda (25)
          --boss-gate         curva de puertas de la ADR 0033: cada nivel de build (qualityLevels de
                               data/balance/groups.json) contra cada jefe de data/bosses/, con sus
                               modificadores; --runs = partidos por plantilla (8), --rosters = plantillas
          --full-runs N       N runs completas por cada una de las tres doctrinas de compra de la ADR
                               0037 (contextual, gastadora, ahorradora) sobre las mismas semillas;
                               escribe runs.csv y summary.csv con las métricas de fase2-diseno.md §10
        """);
}

/// <summary>Sube directorios desde cwd hasta encontrar un directorio "data" (por defecto de --data).</summary>
static string ResolveDataPath(string? given)
{
    if (given is not null)
    {
        return given;
    }

    DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        string candidate = Path.Combine(dir.FullName, "data");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException(
        $"no se encontró un directorio 'data' subiendo desde {Directory.GetCurrentDirectory()}; usa --data para indicarlo");
}

/// <summary>Lee todos los *.json bajo dataRoot excepto dataRoot/schemas/, en rutas relativas con barras.</summary>
static Dictionary<string, string> LoadDataFiles(string dataRoot)
{
    string fullRoot = Path.GetFullPath(dataRoot);
    string schemasDir = Path.GetFullPath(Path.Combine(fullRoot, "schemas")) + Path.DirectorySeparatorChar;

    var files = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (string file in Directory.EnumerateFiles(fullRoot, "*.json", SearchOption.AllDirectories))
    {
        string fullFile = Path.GetFullPath(file);
        if (fullFile.StartsWith(schemasDir, StringComparison.Ordinal))
        {
            continue;
        }

        string relative = Path.GetRelativePath(fullRoot, fullFile).Replace(Path.DirectorySeparatorChar, '/');
        files[relative] = File.ReadAllText(fullFile);
    }

    return files;
}

static void WriteMatchesCsv(string outDir, IReadOnlyList<MatchRow> matches)
{
    string[] header =
    {
        "index", "seed", "homeId", "awayId", "homeGoals", "awayGoals", "winner", "ticks", "goldenGoal",
        "forfeit", "possessionChanges", "passChains", "passChainAvgLength", "shots", "shotsOnTarget",
        "tackles", "blocks", "fouls", "yellow", "red", "injuries", "ballThird0", "ballThird1", "ballThird2", "finalBias",
    };

    var rows = matches.Select(m =>
    {
        double avgLength = m.PassChains > 0 ? (double)m.PassChainTotalLength / m.PassChains : 0.0;
        return (IReadOnlyList<string>)new[]
        {
            m.Index.ToString(),
            m.Seed.ToString(),
            m.HomeId,
            m.AwayId,
            m.HomeGoals.ToString(),
            m.AwayGoals.ToString(),
            m.Winner.ToString(),
            m.Ticks.ToString(),
            CsvWriter.Bool(m.GoldenGoal),
            CsvWriter.Bool(m.Forfeit),
            m.PossessionChanges.ToString(),
            m.PassChains.ToString(),
            CsvWriter.F2(avgLength),
            m.Shots.ToString(),
            m.ShotsOnTarget.ToString(),
            m.Tackles.ToString(),
            m.Blocks.ToString(),
            m.Fouls.ToString(),
            m.Yellow.ToString(),
            m.Red.ToString(),
            m.Injuries.ToString(),
            m.BallThird0.ToString(),
            m.BallThird1.ToString(),
            m.BallThird2.ToString(),
            m.FinalBias.ToString(),
        };
    });

    CsvWriter.Write(Path.Combine(outDir, "matches.csv"), header, rows);
}

static void WritePlayersCsv(string outDir, IReadOnlyList<PlayerAggregate> players)
{
    string[] header =
    {
        "playerId", "teamId", "name", "race", "position", "rarity", "matches", "goals", "assists",
        "shots", "passesAttempted", "passesCompleted", "tackles", "tacklesWon", "fouls", "cards",
        "injuries", "ticksOnPitch",
    };

    var rows = players.Select(p => (IReadOnlyList<string>)new[]
    {
        p.PlayerId.ToString(),
        p.TeamId,
        p.Name,
        p.Race,
        p.Position,
        p.Rarity,
        p.Matches.ToString(),
        p.Goals.ToString(),
        p.Assists.ToString(),
        p.Shots.ToString(),
        p.PassesAttempted.ToString(),
        p.PassesCompleted.ToString(),
        p.Tackles.ToString(),
        p.TacklesWon.ToString(),
        p.Fouls.ToString(),
        p.Cards.ToString(),
        p.Injuries.ToString(),
        p.TicksOnPitch.ToString(),
    });

    CsvWriter.Write(Path.Combine(outDir, "players.csv"), header, rows);
}

/// <summary>perk-values.csv del modo --perk-values (ADR 0038): una fila por perk medido.</summary>
static void WritePerkValuesCsv(string outDir, IReadOnlyList<PerkValueRow> rows)
{
    string[] header = { "perk", "slot", "matches", "wins", "winRate", "valueMilli" };
    var data = rows.Select(r => (IReadOnlyList<string>)new[]
    {
        r.PerkId,
        r.Slot.ToString(CultureInfo.InvariantCulture),
        r.Matches.ToString(CultureInfo.InvariantCulture),
        r.Wins.ToString(CultureInfo.InvariantCulture),
        CsvWriter.F2(r.WinRate),
        r.ValueMilli.ToString(CultureInfo.InvariantCulture),
    });

    CsvWriter.Write(Path.Combine(outDir, "perk-values.csv"), header, data);
}

/// <summary>runs.csv del modo --full-runs (fase2-diseno.md §10): una fila por run jugada.</summary>
static void WriteRunsCsv(string outDir, IReadOnlyList<RunPlayResult> runs, string fileName = "runs.csv")
{
    string[] header =
    {
        "seed", "doctrine", "race", "outcome", "cause", "actReached", "matches", "matchesWon", "bossesBeaten",
        "matchesAct1", "matchesAct2", "matchesAct3",
        "winsAct1", "winsAct2", "winsAct3",
        "goldEarned", "goldEarnedAct1", "goldEarnedAct2", "goldEarnedAct3", "goldFromSales",
        "goldMarket", "goldClinic", "goldEnrollment", "goldReroll", "goldWages", "goldLeft",
        "deaths", "ownInjuries", "matchInjuries", "severeInjuries", "rosterSize", "available", "averageLevel",
        "perks", "starterPerks", "items", "counters",
        "markets", "offersSeen", "offersAffordable", "goldAtMarkets", "brokeMarkets", "purchases", "perksBought",
        "itemsBought", "playersSigned", "youths", "mercenaries", "playersSold", "treatments", "slotsBought", "rerolls",
        "rewardsTaken", "rewardsDeclined", "nodes",

        // ADR 0051: qué arco cerró la run y con qué build terminó. Sin las dos columnas no se puede
        // reconstruir a mano ni "¿los arcos existen?" ni "¿hay compromiso?".
        "masters", "finalPerks",
    };

    var rows = runs.Select(r => (IReadOnlyList<string>)new[]
    {
        r.Seed.ToString(CultureInfo.InvariantCulture),
        r.Doctrine.ToString(),
        r.ClubRace.ToString(),
        r.Outcome.ToString(),
        r.Cause.ToString(),
        Int(r.ActReached), Int(r.Matches), Int(r.MatchesWon), Int(r.BossesBeaten),
        Int(r.MatchesByAct[0]), Int(r.MatchesByAct[1]), Int(r.MatchesByAct[2]),
        Int(r.WinsByAct[0]), Int(r.WinsByAct[1]), Int(r.WinsByAct[2]),
        Int(r.GoldEarned), Int(r.GoldEarnedByAct[0]), Int(r.GoldEarnedByAct[1]), Int(r.GoldEarnedByAct[2]),
        Int(r.GoldFromSales),
        Int(r.GoldSpentMarket), Int(r.GoldSpentClinic), Int(r.GoldSpentEnrollment), Int(r.GoldSpentReroll), Int(r.GoldSpentWages),
        Int(r.GoldLeft),
        Int(r.Deaths), Int(r.OwnInjuries), Int(r.MatchInjuries), Int(r.SevereInjuriesSuffered), Int(r.FinalRosterSize),
        Int(r.FinalAvailable), CsvWriter.F2(r.AverageLevelTimes100 / 100.0),
        Int(r.PerksOnRoster), Int(r.PerksOnStarters), Int(r.ItemsOnRoster), Int(r.AccumulatedCounters),
        Int(r.MarketsVisited), Int(r.OffersSeen), Int(r.OffersAffordable), Int(r.GoldAtMarketArrival), Int(r.BrokeMarketVisits),
        Int(r.Purchases), Int(r.PerksBought),
        Int(r.ItemsBought), Int(r.PlayersSigned), Int(r.YouthsSigned), Int(r.MercenariesHired),
        Int(r.PlayersSold), Int(r.Treatments), Int(r.SlotsBought), Int(r.Rerolls),
        Int(r.RewardsTaken), Int(r.RewardsDeclined), Int(r.NodesVisited),
        string.Join(" ", r.Masters), string.Join(" ", r.FinalPerks),
    });

    CsvWriter.Write(Path.Combine(outDir, fileName), header, rows);

    static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
}

static void WriteSummaryCsv(string outDir, IReadOnlyList<MetricRow> metrics)
{
    string[] header = { "metric", "value", "rangeMin", "rangeMax", "status" };

    var rows = metrics.Select(m => (IReadOnlyList<string>)new[]
    {
        m.Name,
        CsvWriter.F2(m.Value),
        m.RangeMin is { } min ? CsvWriter.F2(min) : string.Empty,
        m.RangeMax is { } max ? CsvWriter.F2(max) : string.Empty,
        m.Status,
    });

    CsvWriter.Write(Path.Combine(outDir, "summary.csv"), header, rows);
}

/// <summary>
/// Resuelve la lista de --builds contra las builds cargadas de disco: "all" se expande a todas (orden
/// ordinal de id), cualquier otro valor tiene que existir ya (BuildBatchRunner también comprueba esto para
/// las que aparecen como --vs, pero aquí se comprueba pronto para dar un error claro antes de simular).
/// </summary>
static IReadOnlyList<string> ResolveBuildIds(IReadOnlyList<string> requested, IReadOnlyDictionary<string, BuildConfig> allBuilds)
{
    if (requested.Count == 1 && requested[0] == "all")
    {
        return allBuilds.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    foreach (var id in requested)
    {
        if (!allBuilds.ContainsKey(id))
        {
            throw new ArgumentException($"--builds: build desconocida '{id}' (no existe en data/balance/builds/)");
        }
    }

    return requested;
}

/// <summary>--describe [es|en] (docs/fase1-diseno.md §8): catálogo completo con descripción generada y distribución RF-069.</summary>
static void RunDescribe(Catalog catalog, string language)
{
    var templates = catalog.Localization.Get(language);
    var perks = catalog.Perks.All;

    string[] headers = { "id", "rareza", "tipo", "disparador", "descripcion" };
    var rows = perks
        .Select(p => new[]
        {
            p.Id,
            p.Rarity.ToString(),
            p.Kind.ToString(),
            p.Trigger.ToString(),
            DescriptionGenerator.Describe(p, templates),
        })
        .ToList();

    PrintAlignedTable(headers, rows);

    Console.WriteLine();
    int total = perks.Count;
    int filler = perks.Count(p => p.Kind == PerkKind.Filler);
    int conditional = perks.Count(p => p.Kind == PerkKind.Conditional);
    int ruleBreaker = perks.Count(p => p.Kind == PerkKind.RuleBreaker);
    Console.WriteLine($"catálogo: {total} perks");
    PrintDistributionLine("filler", filler, total, 60);
    PrintDistributionLine("conditional", conditional, total, 30);
    PrintDistributionLine("ruleBreaker", ruleBreaker, total, 10);
}

/// <summary>Una línea de la comprobación RF-069 (60/30/10 ± 8 puntos), reutilizando BuildMetrics.Rf069Distribution para no duplicar el criterio de rango.</summary>
static void PrintDistributionLine(string kind, int count, int total, int target)
{
    double pct = total > 0 ? 100.0 * count / total : 0.0;
    bool inRange = pct >= target - BuildMetrics.Rf069Tolerance && pct <= target + BuildMetrics.Rf069Tolerance;
    Console.WriteLine(
        $"  {kind,-12} {count,3}/{total,-3} ({pct,6:F2}%) objetivo {target}% ± {BuildMetrics.Rf069Tolerance} -> {(inRange ? "IN" : "OUT")}");
}

static void WriteBuildsCsv(string outDir, IReadOnlyList<BuildCellResult> cells)
{
    string[] header =
    {
        "build", "opponent", "matches", "winRate", "goalsFor", "goalsAgainst", "injuriesFor",
        "injuriesAgainst", "tacklesPerMatch", "passChainAvgLength", "activationsPerMatch",
    };

    var rows = cells.Select(c => (IReadOnlyList<string>)new[]
    {
        c.Build,
        c.Opponent,
        c.Matches.ToString(),
        CsvWriter.F2(c.WinRate),
        c.GoalsFor.ToString(),
        c.GoalsAgainst.ToString(),
        c.InjuriesFor.ToString(),
        c.InjuriesAgainst.ToString(),
        CsvWriter.F2(c.TacklesPerMatch),
        CsvWriter.F2(c.PassChainAvgLength),
        CsvWriter.F2(c.ActivationsPerMatch),
    });

    CsvWriter.Write(Path.Combine(outDir, "builds.csv"), header, rows);
}

static void WritePerksCsv(string outDir, IReadOnlyList<PerkActivationResult> perkRows)
{
    string[] header = { "perkId", "build", "activations", "matchesWithActivation", "activationRate" };

    var rows = perkRows.Select(r => (IReadOnlyList<string>)new[]
    {
        r.PerkId,
        r.Build,
        r.MatchesAssigned.ToString(),
        r.MatchesWithActivation.ToString(),
        CsvWriter.F2(r.ActivationRate),
    });

    CsvWriter.Write(Path.Combine(outDir, "perks.csv"), header, rows);
}

static void WriteCampaignCsv(string outDir, IReadOnlyList<CampaignRow> rows)
{
    string[] header =
    {
        "build", "matchIndex", "opponentQuality", "campaigns", "winRate", "avgLevel", "avgStrength",
        "avgTechnique", "activationsPerMatch",
    };

    var csvRows = rows.Select(r => (IReadOnlyList<string>)new[]
    {
        r.Build,
        r.MatchIndex.ToString(),
        r.OpponentQuality.ToString(),
        r.Campaigns.ToString(),
        CsvWriter.F2(r.WinRate),
        CsvWriter.F2(r.AvgLevel),
        CsvWriter.F2(r.AvgStrength),
        CsvWriter.F2(r.AvgTechnique),
        CsvWriter.F2(r.ActivationsPerMatch),
    });

    CsvWriter.Write(Path.Combine(outDir, "campaign.csv"), header, csvRows);
}

static void PrintBuildsTable(IReadOnlyList<BuildCellResult> cells)
{
    string[] headers =
    {
        "build", "opponent", "matches", "winRate", "goalsFor", "goalsAgainst", "injuriesFor",
        "injuriesAgainst", "tacklesPerMatch", "passChainAvgLength", "activationsPerMatch",
    };

    var rows = cells
        .Select(c => new[]
        {
            c.Build, c.Opponent, c.Matches.ToString(), CsvWriter.F2(c.WinRate), c.GoalsFor.ToString(),
            c.GoalsAgainst.ToString(), c.InjuriesFor.ToString(), c.InjuriesAgainst.ToString(),
            CsvWriter.F2(c.TacklesPerMatch), CsvWriter.F2(c.PassChainAvgLength), CsvWriter.F2(c.ActivationsPerMatch),
        })
        .ToList();

    PrintAlignedTable(headers, rows);
}

/// <summary>
/// Tabla por consola de la campaña (docs/fase1-diseno.md §8): por build, tasa de victoria en la primera
/// mitad de partidos (1..N/2) frente a la segunda (N/2+1..N), calculada sobre victorias/partidos totales
/// (no como media de porcentajes) para no arrastrar redondeo.
/// </summary>
static void PrintCampaignTables(IReadOnlyList<CampaignRow> rows, int matchesPerCampaign)
{
    int half = matchesPerCampaign / 2;
    string[] headers = { "build", "campaigns", "winRate1..N/2", "winRate(N/2+1)..N", "delta" };
    var tableRows = new List<string[]>();

    foreach (var group in rows.GroupBy(r => r.Build).OrderBy(g => g.Key, StringComparer.Ordinal))
    {
        var ordered = group.OrderBy(r => r.MatchIndex).ToList();
        int campaigns = ordered.Count > 0 ? ordered[0].Campaigns : 0;

        double firstWins = 0, firstMatches = 0, secondWins = 0, secondMatches = 0;
        foreach (var row in ordered)
        {
            double matches = row.Campaigns;
            double wins = row.WinRate / 100.0 * matches;
            if (row.MatchIndex < half)
            {
                firstWins += wins;
                firstMatches += matches;
            }
            else
            {
                secondWins += wins;
                secondMatches += matches;
            }
        }

        double firstRate = firstMatches > 0 ? 100.0 * firstWins / firstMatches : 0.0;
        double secondRate = secondMatches > 0 ? 100.0 * secondWins / secondMatches : 0.0;

        tableRows.Add(new[]
        {
            group.Key,
            campaigns.ToString(),
            CsvWriter.F2(firstRate),
            CsvWriter.F2(secondRate),
            CsvWriter.F2(secondRate - firstRate),
        });
    }

    PrintAlignedTable(headers, tableRows);
}

static void PrintSummaryTable(IReadOnlyList<MetricRow> metrics)
{
    string[] headers = { "metric", "value", "rangeMin", "rangeMax", "status" };
    var rows = metrics
        .Select(m => new[]
        {
            m.Name,
            CsvWriter.F2(m.Value),
            m.RangeMin is { } min ? CsvWriter.F2(min) : "-",
            m.RangeMax is { } max ? CsvWriter.F2(max) : "-",
            m.Status,
        })
        .ToList();

    PrintAlignedTable(headers, rows);
}

static void PrintUtilityCensus(IReadOnlyList<ActionCensusRow> rows)
{
    int decisions = rows.Sum(r => r.Chosen);
    Console.WriteLine();
    Console.WriteLine($"censo de utilidad (RT-098): {decisions} decisiones muestreadas");
    string[] headers = { "accion", "evaluada", "descartada%", "elegida", "elegida%", "2a%", "scoreMedio", "scoreMax", "margenMedio" };
    var table = rows
        .OrderByDescending(r => r.Chosen)
        .Select(r =>
        {
            int scored = r.Legal - r.Rejected;
            return new[]
            {
                r.Action.ToString(),
                r.Legal.ToString(),
                r.Legal > 0 ? (100.0 * r.Rejected / r.Legal).ToString("F1", CultureInfo.InvariantCulture) : "-",
                r.Chosen.ToString(),
                decisions > 0 ? (100.0 * r.Chosen / decisions).ToString("F2", CultureInfo.InvariantCulture) : "-",
                decisions > 0 ? (100.0 * r.RunnerUp / decisions).ToString("F2", CultureInfo.InvariantCulture) : "-",
                scored > 0 ? ((double)r.ScoreSum / scored).ToString("F0", CultureInfo.InvariantCulture) : "-",
                r.BestScore.ToString(),
                scored > 0 ? ((double)r.MarginSum / scored).ToString("F0", CultureInfo.InvariantCulture) : "-",
            };
        })
        .ToList();

    PrintAlignedTable(headers, table);
}

static void PrintUtilityDump(UtilityDump dump)
{
    Console.WriteLine($"jugador {dump.PlayerId}, tick {dump.Tick}, estado {dump.State}, elegida {dump.Chosen}");

    string[] headers = { "accion", "score", "base", "tactical", "trait", "context", "rejected", "fueraCenti" };
    var rows = dump.Rows
        .Select(r => new[]
        {
            r.Action.ToString(),
            r.Score.ToString(),
            r.Base.ToString(),
            r.TacticalMultiplier.ToString(),
            r.TraitMultiplier.ToString(),
            r.Context.ToString(),
            r.Rejected.ToString(),
            r.OutsideCentiCells.ToString(),
        })
        .ToList();

    PrintAlignedTable(headers, rows);
}

static void PrintAlignedTable(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
{
    int columns = headers.Count;
    var widths = new int[columns];
    for (int c = 0; c < columns; c++)
    {
        widths[c] = headers[c].Length;
    }

    foreach (var row in rows)
    {
        for (int c = 0; c < columns; c++)
        {
            widths[c] = Math.Max(widths[c], row[c].Length);
        }
    }

    Console.WriteLine(FormatRow(headers, widths));
    foreach (var row in rows)
    {
        Console.WriteLine(FormatRow(row, widths));
    }
}

static string FormatRow(IReadOnlyList<string> cells, IReadOnlyList<int> widths)
{
    var parts = new string[cells.Count];
    for (int c = 0; c < cells.Count; c++)
    {
        parts[c] = cells[c].PadRight(widths[c]);
    }

    return string.Join("  ", parts);
}

/// <summary>bossgate.csv: una fila por celda (jefe, nivel de build, build) de la curva de la ADR 0033.</summary>
static void WriteBossGateCsv(string outDir, IReadOnlyList<BossGateCell> cells)
{
    string[] header = { "boss", "act", "level", "build", "matches", "wins", "winRate", "pitchWinRate" };

    var rows = cells.Select(c => (IReadOnlyList<string>)new[]
    {
        c.BossId,
        c.Act.ToString(),
        c.Level,
        c.BuildId,
        c.Matches.ToString(),
        c.Wins.ToString(),
        CsvWriter.F2(c.WinRate),
        CsvWriter.F2(c.PitchWinRate),
    });

    CsvWriter.Write(Path.Combine(outDir, "bossgate.csv"), header, rows);
}
