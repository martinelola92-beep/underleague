namespace Underleague.Balance;

/// <summary>Opciones de línea de comandos de /Balance (docs/fase0-diseno.md §4). Parseo manual, sin paquetes.</summary>
public sealed class Options
{
    public int Runs { get; private set; } = 1000;

    public ulong Seed { get; private set; } = 1;

    /// <summary>
    /// Null salvo que se pase --match-seed: ejecuta un único partido con esta semilla exacta de motor en
    /// vez del lote completo (docs/sim-debug), usando --teams y su primer emparejamiento.
    /// </summary>
    public ulong? MatchSeed { get; private set; }

    public string TeamsPath { get; private set; } = Path.Combine("data", "balance", "reference.json");

    /// <summary>Null hasta resolver: si no se pasa --data, se busca subiendo directorios desde cwd (Program.ResolveDataPath).</summary>
    public string? DataPath { get; private set; }

    /// <summary>Null hasta resolver: por defecto "out/&lt;seed&gt;/", calculado una vez se conoce Seed.</summary>
    public string? OutDir { get; private set; }

    public bool Log { get; private set; }

    public (int PlayerId, int Tick)? DumpUtility { get; private set; }

    public bool Quiet { get; private set; }

    /// <summary>
    /// Null salvo que se pase --builds: lista de ids de build (docs/fase1-diseno.md §8) para los modos
    /// matriz (--vs) y campaña (--campaign). "all" en la línea de comandos se expande a todas las builds
    /// de data/balance/builds/ (Program.cs), así que aquí nunca vale ["all"] literalmente salvo que el
    /// usuario tenga una build llamada exactamente así.
    /// </summary>
    public IReadOnlyList<string>? Builds { get; private set; }

    /// <summary>
    /// Null salvo que se pase --vs: id de la build rival única del modo matriz. Sin --vs (null) con
    /// --builds dado, el modo matriz es todos-contra-todos entre las builds listadas (§8).
    /// </summary>
    public string? Vs { get; private set; }

    /// <summary>--home-away: cada emparejamiento (matriz o campaña) se juega también con los equipos invertidos (§8).</summary>
    public bool HomeAway { get; private set; }

    /// <summary>Null salvo que se pase --campaign N: activa el modo campaña con N partidos consecutivos por build (§8).</summary>
    public int? Campaign { get; private set; }

    /// <summary>
    /// --rosters N: plantillas distintas generadas por build sobre las que se promedia cada celda de la
    /// matriz de builds (paquete I). Con una sola plantilla la tasa de victoria de una build depende más
    /// del dado del generador (sd de 15 puntos) que de sus perks.
    /// </summary>
    public int Rosters { get; private set; } = BuildBatchRunner.DefaultRosters;

    /// <summary>--boss-gate: mide la curva de puertas de la ADR 0033 (cada nivel de build contra cada jefe).</summary>
    public bool BossGate { get; private set; }

    /// <summary>--perk-values: mide el valor de cada perk contra su espejo sin él (ADR 0038).</summary>
    public bool PerkValues { get; private set; }

    /// <summary>
    /// Null salvo que se pase --full-runs N: juega N runs completas con la política automática de
    /// <c>Sim.Analysis.RunPolicy</c> y vuelca runs.csv (fase2-diseno.md §10).
    /// </summary>
    public int? FullRuns { get; private set; }

    /// <summary>Null salvo que se pase --describe [es|en]: activa el modo catálogo, con el idioma pedido (por defecto "es").</summary>
    public string? Describe { get; private set; }

    /// <summary>True si --runs se pasó explícitamente en la línea de comandos (el modo campaña usa un valor por defecto distinto, 60, cuando no se pasó).</summary>
    public bool RunsExplicit { get; private set; }

    /// <summary>Parsea argv. Lanza ArgumentException con un mensaje claro ante cualquier opción mal formada o desconocida.</summary>
    public static Options Parse(string[] args)
    {
        var options = new Options();
        string? outDir = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--runs":
                    options.Runs = ParseInt(arg, NextValue(args, ref i, arg));
                    if (options.Runs <= 0)
                    {
                        throw new ArgumentException("--runs debe ser mayor que cero");
                    }

                    options.RunsExplicit = true;
                    break;

                case "--builds":
                    string buildsValue = NextValue(args, ref i, arg);
                    options.Builds = buildsValue
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (options.Builds.Count == 0)
                    {
                        throw new ArgumentException("--builds requiere al menos un id de build");
                    }

                    break;

                case "--vs":
                    options.Vs = NextValue(args, ref i, arg);
                    break;

                case "--home-away":
                    options.HomeAway = true;
                    break;

                case "--boss-gate":
                    options.BossGate = true;
                    break;

                case "--perk-values":
                    options.PerkValues = true;
                    break;

                case "--full-runs":
                    options.FullRuns = ParseInt(arg, NextValue(args, ref i, arg));
                    if (options.FullRuns <= 0)
                    {
                        throw new ArgumentException("--full-runs debe ser mayor que cero");
                    }

                    break;

                case "--rosters":
                    options.Rosters = ParseInt(arg, NextValue(args, ref i, arg));
                    if (options.Rosters <= 0)
                    {
                        throw new ArgumentException("--rosters debe ser mayor que cero");
                    }

                    break;

                case "--campaign":
                    options.Campaign = ParseInt(arg, NextValue(args, ref i, arg));
                    if (options.Campaign <= 0)
                    {
                        throw new ArgumentException("--campaign debe ser mayor que cero");
                    }

                    break;

                case "--describe":
                    string language = "es";
                    if (i + 1 < args.Length && (args[i + 1] == "es" || args[i + 1] == "en"))
                    {
                        i++;
                        language = args[i];
                    }

                    options.Describe = language;
                    break;

                case "--seed":
                    options.Seed = ParseUlong(arg, NextValue(args, ref i, arg));
                    break;

                case "--match-seed":
                    options.MatchSeed = ParseUlong(arg, NextValue(args, ref i, arg));
                    break;

                case "--teams":
                    options.TeamsPath = NextValue(args, ref i, arg);
                    break;

                case "--data":
                    options.DataPath = NextValue(args, ref i, arg);
                    break;

                case "--out":
                    outDir = NextValue(args, ref i, arg);
                    break;

                case "--log":
                    options.Log = true;
                    break;

                case "--dump-utility":
                    options.DumpUtility = ParseDumpUtility(NextValue(args, ref i, arg));
                    break;

                case "--quiet":
                    options.Quiet = true;
                    break;

                default:
                    throw new ArgumentException($"opción desconocida '{arg}'");
            }
        }

        options.OutDir = outDir ?? Path.Combine("out", options.Seed.ToString());
        return options;
    }

    private static string NextValue(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requiere un valor");
        }

        i++;
        return args[i];
    }

    private static int ParseInt(string option, string value)
    {
        if (!int.TryParse(value, out int result))
        {
            throw new ArgumentException($"{option}: valor entero inválido '{value}'");
        }

        return result;
    }

    private static ulong ParseUlong(string option, string value)
    {
        if (!ulong.TryParse(value, out ulong result))
        {
            throw new ArgumentException($"{option}: valor entero sin signo inválido '{value}'");
        }

        return result;
    }

    private static (int PlayerId, int Tick) ParseDumpUtility(string value)
    {
        string[] parts = value.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int playerId) || !int.TryParse(parts[1], out int tick))
        {
            throw new ArgumentException($"--dump-utility: formato inválido '{value}', se espera 'playerId:tick'");
        }

        return (playerId, tick);
    }
}
