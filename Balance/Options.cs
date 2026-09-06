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
    /// <c>--utility-census N</c>: censo del volcado de utilidad (RT-098) sobre N partidos de referencia.
    /// Herramienta de medición: no toca ninguna métrica ni ninguna puerta.
    /// </summary>
    public int? UtilityCensus { get; private set; }

    /// <summary>
    /// Null salvo que se pase --full-runs N: juega N runs completas con la política automática de
    /// <c>Sim.Analysis.RunPolicy</c> y vuelca runs.csv (fase2-diseno.md §10).
    /// </summary>
    public int? FullRuns { get; private set; }

    /// <summary>
    /// --ignore-scouting: en --full-runs, la política automática <b>no lee</b> el informe de ojeo
    /// (RF-013) y alinea a los tocados aunque el rival lleve perks letales (ADR 0046). Es la medida de
    /// control: la diferencia entre las dos cifras de muertes es lo que vale leer el informe.
    /// </summary>
    public bool IgnoreScouting { get; private set; }

    /// <summary>
    /// <c>--risk-aversion N</c>: cuánto pesa el indicador de riesgo de muerte al alinear
    /// (<c>RunPolicyOptions.DeathCostPercent</c>), sin dejar de leer el informe de ojeo. Es la palanca
    /// con la que se mide el <b>rango</b> de la agencia de la ADR 0048: 0 la ignora, un valor alto la
    /// obedece y un valor <b>negativo</b> hace lo contrario a propósito (el techo de muertes), que es lo
    /// que dice cuánto había en juego. null = el valor por defecto de la política.
    /// </summary>
    public int? RiskAversion { get; private set; }

    /// <summary>
    /// <c>--min-perk-value N</c>: listón de valor medido que la doctrina <b>contextual</b> le exige a un
    /// perk para gastarse un slot en él (<c>RunPolicyOptions.MinPerkValue</c>, ADR 0038). Es la palanca
    /// con la que se mide el coste de oportunidad del slot (AS-A): null = el valor por defecto de la
    /// política.
    /// </summary>
    public int? MinPerkValue { get; private set; }

    /// <summary><c>--min-perk-value-reward N</c>: el mismo listón, sólo para el perk que llega de recompensa (AS-A).</summary>
    public int? MinPerkValueReward { get; private set; }

    /// <summary><c>--min-perk-value-market N</c>: el mismo listón, sólo para el perk que se compra (AS-A).</summary>
    public int? MinPerkValueMarket { get; private set; }

    /// <summary><c>--slot-bar-off</c>: la doctrina contextual vuelve al listón constante de antes de la ADR 0072 (medida de control).</summary>
    public bool SlotBarOff { get; private set; }

    /// <summary><c>--slot-horizon N</c>: cuántos actos por delante cuenta el slot como escaso (ADR 0072); null = el de la política.</summary>
    public int? SlotHorizon { get; private set; }

    /// <summary><c>--arc-judged</c>: la línea perseguida se juzga sin crédito de arco (medida de control).</summary>
    public bool ArcJudged { get; private set; }

    /// <summary><c>--slot-gates</c>: pondera el coste de oportunidad del slot por la <b>exposición a puertas</b> (ADR 0076). Apagada por defecto: derivada, medida y descartada.</summary>
    public bool SlotGates { get; private set; }

    /// <summary><c>--act1-pass N</c>: tasa de paso de la puerta del acto 1 en milésimas que usa el listón del slot (ADR 0072); null = la de la política. Es la palanca con la que se mide el punto fijo de AU-D.</summary>
    public int? Act1Pass { get; private set; }

    /// <summary><c>--act2-pass N</c>: lo mismo para la puerta del acto 2.</summary>
    public int? Act2Pass { get; private set; }

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

                case "--utility-census":
                    options.UtilityCensus = ParseInt(arg, NextValue(args, ref i, arg));
                    if (options.UtilityCensus <= 0)
                    {
                        throw new ArgumentException("--utility-census debe ser mayor que cero");
                    }

                    break;

                case "--ignore-scouting":
                    options.IgnoreScouting = true;
                    break;

                case "--min-perk-value":
                    options.MinPerkValue = ParseInt(arg, NextValue(args, ref i, arg));
                    break;

                case "--min-perk-value-reward":
                    options.MinPerkValueReward = ParseInt(arg, NextValue(args, ref i, arg));
                    break;

                case "--min-perk-value-market":
                    options.MinPerkValueMarket = ParseInt(arg, NextValue(args, ref i, arg));
                    break;

                case "--slot-bar-off":
                    options.SlotBarOff = true;
                    break;

                case "--slot-horizon":
                    options.SlotHorizon = ParseInt(arg, NextValue(args, ref i, arg));
                    break;

                case "--arc-judged":
                    options.ArcJudged = true;
                    break;

                case "--slot-gates":
                    options.SlotGates = true;
                    break;

                case "--act1-pass":
                    options.Act1Pass = ParseInt(arg, NextValue(args, ref i, arg));
                    break;

                case "--act2-pass":
                    options.Act2Pass = ParseInt(arg, NextValue(args, ref i, arg));
                    break;

                case "--risk-aversion":
                    options.RiskAversion = ParseInt(arg, NextValue(args, ref i, arg));
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
