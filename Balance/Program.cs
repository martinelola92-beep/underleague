using Underleague.Balance;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Perks;

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
        "tackles", "fouls", "yellow", "red", "injuries", "ballThird0", "ballThird1", "ballThird2", "finalBias",
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
