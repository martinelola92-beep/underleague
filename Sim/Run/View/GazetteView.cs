using System.Globalization;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Gazette;
using Underleague.Sim.Run.Systems.Nicknames;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Run.View;

/// <summary>El mejor de la temporada (ADR 0163): quién es, su apodo y la línea que lo cuenta.</summary>
public sealed record GazetteMvp(int PlayerId, string Name, string Nickname, string Line);

/// <summary>El villano de la temporada (ADR 0163): el rival que más daño hizo a la plantilla propia.</summary>
public sealed record GazetteVillain(string Name, string Clan, int Deaths, int Injuries, string Line);

/// <summary>La esquela de un caído (ADR 0163, RF-122): quién era, su apodo, su carrera y cómo cayó.</summary>
public sealed record GazetteObituary(int PlayerId, string Name, string Nickname, string Title, string Career, string Epitaph);

/// <summary>
/// La Gaceta de fin de run (ADR 0163, RF-122, RT-035): la run contada como un periódico de humor. Dato
/// puro y determinista: con la misma run, el mismo catálogo y el mismo idioma sale <b>el mismo texto</b>,
/// porque las variantes se eligen con una función pura de la semilla de la run y no con un RNG.
/// </summary>
public sealed record GazetteReport(
    bool Victory,
    string Masthead,
    string Headline,
    string Lede,
    string MvpTitle,
    GazetteMvp? Mvp,
    string VillainTitle,
    GazetteVillain? Villain,
    string ObituariesTitle,
    string ObituariesNone,
    IReadOnlyList<GazetteObituary> Obituaries);

/// <summary>Compone la Gaceta desde el <see cref="RunState"/> final. Puro, sin E/S.</summary>
public static class GazetteView
{
    /// <summary>Muertes que vale «una baja grave» en la cuenta del villano (RivalCredits no guarda cronología).</summary>
    public const int VillainDeathWeight = 3;

    /// <summary>
    /// Peso de cada hecho de la carrera en la métrica del MVP. <b>Provisional, sin medir</b> (Regla H):
    /// pesa más lo que decide un partido (goles, asistencias) y las bajas que se causan al rival; una
    /// entrada ganada o un partido jugado sólo desempatan.
    /// </summary>
    private const int GoalWeight = 4;
    private const int AssistWeight = 3;
    private const int TackleWonWeight = 1;
    private const int InjuryCausedWeight = 2;
    private const int DeathCausedWeight = 3;
    private const int MatchWeight = 1;

    /// <summary>
    /// Métrica del MVP: <c>4·goles + 3·asistencias + 1·entradas ganadas + 2·lesiones causadas + 3·muertes
    /// causadas + 1·partidos</c>, sobre la carrera de la run de <b>todos</b> los jugadores, vivos o caídos
    /// (un muerto puede ser el mejor: es la historia). Empata el id menor. Sin partidos no hay MVP.
    /// </summary>
    public static int MvpScore(RunCareer career)
    {
        ArgumentNullException.ThrowIfNull(career);
        return (career.Goals * GoalWeight)
            + (career.Assists * AssistWeight)
            + (career.TacklesWon * TackleWonWeight)
            + (career.InjuriesCaused * InjuryCausedWeight)
            + (career.DeathsCaused * DeathCausedWeight)
            + (career.Matches * MatchWeight);
    }

    /// <summary>La Gaceta de una run terminada (también sirve a mitad de run: cuenta lo que hay).</summary>
    public static GazetteReport Build(
        RunState state,
        Catalog catalog,
        NicknameCatalog nicknames,
        RivalCatalog rivals,
        GazetteCatalog templates,
        string language = "es")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(nicknames);
        ArgumentNullException.ThrowIfNull(rivals);
        ArgumentNullException.ThrowIfNull(templates);

        var fallen = RunSummary.Fallen(state);
        bool victory = state.Result.Kind == RunOutcomeKind.Victory;
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["deaths"] = Number(fallen.Count),
            ["wins"] = Number(RunSummary.MatchesWon(state)),
            ["matches"] = Number(RunSummary.MatchesPlayed(state)),
            ["acts"] = Number(RunSummary.ActsCleared(state)),
            ["act"] = Number(state.Act),
        };

        string headlineKey = victory
            ? (fallen.Count == 0 ? "headline.victoryClean" : "headline.victory")
            : state.Result.Cause == DefeatCause.NotEnoughPlayers ? "headline.defeatPlayers" : "headline.defeatBoss";
        string ledeKey = victory
            ? "lede.victory"
            : state.Result.Cause == DefeatCause.NotEnoughPlayers ? "lede.defeatPlayers" : "lede.defeatBoss";

        var credits = AllCredits(state, rivals);
        return new GazetteReport(
            victory,
            Pick(templates, "masthead", language, state.Seed, 1, facts),
            Pick(templates, headlineKey, language, state.Seed, 2, facts),
            Pick(templates, ledeKey, language, state.Seed, 3, facts),
            Pick(templates, "mvp.title", language, state.Seed, 4, facts),
            Mvp(state, nicknames, templates, language),
            Pick(templates, "villain.title", language, state.Seed, 5, facts),
            Villain(state, credits, rivals, templates, language),
            Pick(templates, "obituaries.title", language, state.Seed, 6, facts),
            Pick(templates, "obituaries.none", language, state.Seed, 7, facts),
            Obituaries(state, catalog, nicknames, rivals, credits, templates, language));
    }

    // ------------------------------------------------------------------ MVP

    private static GazetteMvp? Mvp(RunState state, NicknameCatalog nicknames, GazetteCatalog templates, string language)
    {
        RunPlayer? best = null;
        int bestScore = 0;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.Career.Matches <= 0)
            {
                continue;
            }

            int score = MvpScore(player.Career);
            if (best is null || score > bestScore || (score == bestScore && player.Id < best.Id))
            {
                best = player;
                bestScore = score;
            }
        }

        if (best is null)
        {
            return null;
        }

        string nickname = NicknameSystem.For(best, nicknames)?.NameIn(language) ?? string.Empty;
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = best.Name,
            ["nick"] = nickname.Length == 0 ? string.Empty : " «" + nickname + "»",
            ["highlights"] = Highlights(best.Career, templates, language, limit: 3, includeMatches: false),
        };

        return new GazetteMvp(best.Id, best.Name, nickname, Pick(templates, "mvp.line", language, state.Seed, 100 + best.Id, facts));
    }

    /// <summary>
    /// Hechos de la carrera como lista legible («3 goles, 2 asistencias y 1 lesión ajena»), en el orden
    /// fijo partidos, goles, asistencias, entradas ganadas, lesiones y muertes causadas, con los que valen
    /// cero fuera. Si no queda ninguno, cadena vacía.
    /// </summary>
    private static string Highlights(RunCareer career, GazetteCatalog templates, string language, int limit, bool includeMatches)
    {
        var parts = new List<string>();
        void Add(string stat, int value)
        {
            if (value <= 0 || parts.Count >= limit)
            {
                return;
            }

            string key = "highlight." + stat + (value == 1 ? ".one" : ".many");
            var variants = templates.Variants(key, language);
            parts.Add(variants.Count == 0 ? string.Empty : Fill(variants[0], new Dictionary<string, string>(StringComparer.Ordinal) { ["n"] = Number(value) }));
        }

        if (includeMatches)
        {
            Add("matches", career.Matches);
        }

        Add("goals", career.Goals);
        Add("assists", career.Assists);
        Add("tacklesWon", career.TacklesWon);
        Add("injuriesCaused", career.InjuriesCaused);
        Add("deathsCaused", career.DeathsCaused);

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        var and = templates.Variants("highlight.and", language);
        string joiner = and.Count == 0 ? " / " : and[0];
        return string.Join(", ", parts.Take(parts.Count - 1)) + joiner + parts[^1];
    }

    // ------------------------------------------------------------------ villano

    /// <summary>
    /// Todos los hechos «un rival hizo daño a uno de los míos» de la run, por id de clan y luego por
    /// orden determinista de <see cref="RivalCredits.Against"/> (RT-041).
    /// </summary>
    private static IReadOnlyList<RivalCredit> AllCredits(RunState state, RivalCatalog rivals)
    {
        var all = new List<RivalCredit>();
        for (int i = 0; i < rivals.All.Count; i++)
        {
            var credits = RivalCredits.Against(state, rivals.All[i].Id);
            for (int c = 0; c < credits.Count; c++)
            {
                if (credits[c].Kind is RivalCreditKind.SufferedInjury or RivalCreditKind.SufferedDeath)
                {
                    all.Add(credits[c]);
                }
            }
        }

        return all;
    }

    /// <summary>
    /// El villano: el jugador rival concreto (clan e índice) con más daño a los míos, contando
    /// <c>3·muertes + 1·lesiones</c> (<see cref="VillainDeathWeight"/>: una muerte pesa más que una lesión;
    /// <c>RivalCredits</c> no guarda cronología, así que no hay «el último»). Empata el clan de id menor y
    /// luego el índice menor. Null si ningún rival hizo daño: la sección se omite.
    /// </summary>
    private static GazetteVillain? Villain(RunState state, IReadOnlyList<RivalCredit> credits, RivalCatalog rivals, GazetteCatalog templates, string language)
    {
        var deaths = new SortedDictionary<(string, int), int>(Comparer<(string, int)>.Create(static (a, b) =>
        {
            int byClan = string.CompareOrdinal(a.Item1, b.Item1);
            return byClan != 0 ? byClan : a.Item2.CompareTo(b.Item2);
        }));
        var injuries = new SortedDictionary<(string, int), int>(deaths.Comparer);
        for (int i = 0; i < credits.Count; i++)
        {
            var target = credits[i].Kind == RivalCreditKind.SufferedDeath ? deaths : injuries;
            var key = (credits[i].RivalId, credits[i].RivalIndex);
            target[key] = target.GetValueOrDefault(key) + credits[i].Count;
        }

        (string Clan, int Index) best = (string.Empty, -1);
        int bestScore = 0;
        var keys = new SortedSet<(string, int)>(deaths.Keys, deaths.Comparer);
        keys.UnionWith(injuries.Keys);
        foreach (var key in keys)
        {
            int score = (deaths.GetValueOrDefault(key) * VillainDeathWeight) + injuries.GetValueOrDefault(key);
            if (score > bestScore)
            {
                bestScore = score;
                best = key;
            }
        }

        if (bestScore <= 0)
        {
            return null;
        }

        var team = rivals.Find(best.Clan);
        if (team is null || best.Index < 0 || best.Index >= team.Players.Count)
        {
            return null;
        }

        int killed = deaths.GetValueOrDefault(best);
        int hurt = injuries.GetValueOrDefault(best);
        string clan = NameIn(team.Name, language);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["villain"] = team.Players[best.Index].Name,
            ["clan"] = clan,
            ["deaths"] = Number(killed),
            ["injuries"] = Number(hurt),
        };

        string line = Pick(templates, killed > 0 ? "villain.deaths" : "villain.injuries", language, state.Seed, 200 + best.Index, facts);
        return new GazetteVillain(team.Players[best.Index].Name, clan, killed, hurt, line);
    }

    // ------------------------------------------------------------------ esquelas

    private static IReadOnlyList<GazetteObituary> Obituaries(
        RunState state,
        Catalog catalog,
        NicknameCatalog nicknames,
        RivalCatalog rivals,
        IReadOnlyList<RivalCredit> credits,
        GazetteCatalog templates,
        string language)
    {
        var rows = new List<GazetteObituary>();
        var fallen = RunSummary.Fallen(state);
        for (int i = 0; i < fallen.Count; i++)
        {
            var player = fallen[i];
            string nickname = NicknameSystem.For(player, nicknames)?.NameIn(language) ?? string.Empty;
            string nick = nickname.Length == 0 ? string.Empty : " «" + nickname + "»";

            var entryFacts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = player.Name,
                ["nick"] = nick,
                ["race"] = NameIn(catalog.Race(player.Race).Name, language),
                ["level"] = Number(player.Level),
            };

            string careerText = Highlights(player.Career, templates, language, limit: 4, includeMatches: true);
            string career = careerText.Length == 0
                ? Pick(templates, "epitaph.noCareer", language, state.Seed, 400 + player.Id, entryFacts)
                : Pick(templates, "epitaph.career", language, state.Seed, 400 + player.Id, new Dictionary<string, string>(StringComparer.Ordinal) { ["career"] = careerText });

            rows.Add(new GazetteObituary(
                player.Id,
                player.Name,
                nickname,
                Pick(templates, "obituaries.entry", language, state.Seed, 300 + player.Id, entryFacts),
                career,
                Epitaph(state, player, credits, rivals, templates, language)));
        }

        return rows;
    }

    /// <summary>
    /// Cómo cayó: si <c>RivalCredits</c> tiene una muerte de ese jugador a manos de un rival concreto, «a manos
    /// de X»; si no (perk letal, sin rival identificable), una variante genérica.
    /// </summary>
    private static string Epitaph(RunState state, RunPlayer player, IReadOnlyList<RivalCredit> credits, RivalCatalog rivals, GazetteCatalog templates, string language)
    {
        RivalCredit? killer = null;
        for (int i = 0; i < credits.Count; i++)
        {
            if (credits[i].Kind == RivalCreditKind.SufferedDeath && credits[i].OwnPlayerId == player.Id
                && (killer is null || credits[i].Count > killer.Count))
            {
                killer = credits[i];
            }
        }

        if (killer is null)
        {
            return Pick(templates, "epitaph.unknown", language, state.Seed, 500 + player.Id, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var team = rivals.Find(killer.RivalId);
        if (team is null || killer.RivalIndex < 0 || killer.RivalIndex >= team.Players.Count)
        {
            return Pick(templates, "epitaph.unknown", language, state.Seed, 500 + player.Id, new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["killer"] = team.Players[killer.RivalIndex].Name,
            ["clan"] = NameIn(team.Name, language),
        };
        return Pick(templates, "epitaph.byRival", language, state.Seed, 500 + player.Id, facts);
    }

    // ------------------------------------------------------------------ texto

    private static string NameIn(LocalizedName name, string language) =>
        string.Equals(language, "en", StringComparison.Ordinal) ? name.En : name.Es;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Variante de una clave, elegida con una función pura de (semilla, sal): sin RNG, sin estado. Si el
    /// catálogo no tiene la clave devuelve cadena vacía (solo el catálogo <see cref="GazetteCatalog.Empty"/>:
    /// el cargador exige todas las claves).
    /// </summary>
    private static string Pick(GazetteCatalog templates, string key, string language, ulong seed, int salt, IReadOnlyDictionary<string, string> facts)
    {
        var variants = templates.Variants(key, language);
        if (variants.Count == 0)
        {
            return string.Empty;
        }

        return Fill(variants[(int)(Mix(seed, salt) % (ulong)variants.Count)], facts);
    }

    /// <summary>Sustituye <c>{marcador}</c> por su valor; los marcadores que el contexto no da desaparecen, no se ven.</summary>
    private static string Fill(string template, IReadOnlyDictionary<string, string> facts)
    {
        var text = new System.Text.StringBuilder(template.Length + 16);
        int i = 0;
        while (i < template.Length)
        {
            if (template[i] == '{')
            {
                int close = template.IndexOf('}', i + 1);
                if (close > i)
                {
                    string name = template.Substring(i + 1, close - i - 1);
                    if (facts.TryGetValue(name, out string? value))
                    {
                        text.Append(value);
                    }

                    i = close + 1;
                    continue;
                }
            }

            text.Append(template[i]);
            i++;
        }

        return text.ToString();
    }

    /// <summary>Mezcla entera de (semilla, sal) tipo SplitMix64: determinista, sin estado, sin RNG.</summary>
    private static ulong Mix(ulong seed, int salt)
    {
        unchecked
        {
            ulong z = seed + ((ulong)(uint)salt * 0x9E3779B97F4A7C15UL) + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
