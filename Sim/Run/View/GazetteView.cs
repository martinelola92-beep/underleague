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
    string MvpNone,
    string VillainTitle,
    GazetteVillain? Villain,
    string ObituariesTitle,
    string ObituariesNone,
    IReadOnlyList<GazetteObituary> Obituaries);

/// <summary>Compone la Gaceta desde el <see cref="RunState"/> final. Puro, sin E/S.</summary>
public static class GazetteView
{
    /// <summary>
    /// Lo que pesa un muerto frente a un lesionado en la cuenta del villano (<c>3·muertos + 1·lesionados</c>).
    /// <b>Provisional, sin medir</b> (Regla H): sólo dice que una muerte es peor que una lesión; ninguna
    /// medición fija el 3. Cambiarlo sólo cambia a quién nombra la Gaceta, no el partido.
    /// </summary>
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
    /// Hechos mínimos (goles, asistencias, entradas ganadas, lesiones y muertes causadas; los partidos no
    /// cuentan) que exige el MVP: la línea los enumera, y sin ninguno diría «fue el mejor del club: .».
    /// Es un mínimo estructural de la frase, no una cifra de balance. Sin nadie que lo cumpla, la Gaceta
    /// dice <c>mvp.none</c>.
    /// </summary>
    public const int MvpMinimumFacts = 1;

    /// <summary>
    /// Métrica del MVP: <c>4·goles + 3·asistencias + 1·entradas ganadas + 2·lesiones causadas + 3·muertes
    /// causadas + 1·partidos</c>, sobre la carrera de la run de <b>todos</b> los jugadores, vivos o caídos
    /// (un muerto puede ser el mejor: es la historia). Desempate: más partidos, luego quien no es portero
    /// (un portero con la misma cifra ha hecho menos de lo que se cuenta) y por último el id menor. Sin
    /// <see cref="MvpMinimumFacts"/> hechos no hay MVP.
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

    /// <summary>Hechos que cuentan para <see cref="MvpMinimumFacts"/>: todo lo de la carrera menos los partidos.</summary>
    public static int MvpFacts(RunCareer career)
    {
        ArgumentNullException.ThrowIfNull(career);
        return career.Goals + career.Assists + career.TacklesWon + career.InjuriesCaused + career.DeathsCaused;
    }

    /// <summary>La Gaceta de una run terminada (también sirve a mitad de run: cuenta lo que hay).</summary>
    public static GazetteReport Build(
        RunState state,
        Catalog catalog,
        NicknameCatalog nicknames,
        RivalCatalog rivals,
        GazetteCatalog templates,
        string language = "es",
        NemesisCatalog? nemesis = null)
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
            Pick(templates, "mvp.none", language, state.Seed, 8, facts),
            Pick(templates, "villain.title", language, state.Seed, 5, facts),
            NemesisVillain(state, nemesis, templates, language) ?? Villain(state, credits, rivals, templates, language, catalog),
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
            if (player.Career.Matches <= 0 || MvpFacts(player.Career) < MvpMinimumFacts)
            {
                continue;
            }

            int score = MvpScore(player.Career);
            if (best is null || BeatsMvp(player, score, best, bestScore))
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

    /// <summary>Desempate del MVP: puntos, más partidos, no portero, id menor.</summary>
    private static bool BeatsMvp(RunPlayer candidate, int score, RunPlayer best, int bestScore)
    {
        if (score != bestScore)
        {
            return score > bestScore;
        }

        if (candidate.Career.Matches != best.Career.Matches)
        {
            return candidate.Career.Matches > best.Career.Matches;
        }

        bool candidateKeeper = candidate.Position == Position.Goalkeeper;
        if (candidateKeeper != (best.Position == Position.Goalkeeper))
        {
            return !candidateKeeper;
        }

        return candidate.Id < best.Id;
    }

    /// <summary>
    /// Hechos de la carrera como lista legible («3 goles, 2 asistencias y 1 lesión ajena»), en el orden
    /// fijo <b>de lo más memorable a lo menos</b>: partidos (sólo en la esquela), muertes causadas, lesiones
    /// causadas, goles, asistencias y entradas ganadas, con los que valen cero fuera. El límite recorta por
    /// el final, así que lo que se pierde es lo menos memorable. Si no queda ninguno, cadena vacía.
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

        Add("deathsCaused", career.DeathsCaused);
        Add("injuriesCaused", career.InjuriesCaused);
        Add("goals", career.Goals);
        Add("assists", career.Assists);
        Add("tacklesWon", career.TacklesWon);

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
    /// El villano: el jugador rival concreto (clan e índice) con más <b>víctimas distintas</b> entre los
    /// míos, contando <c>3·muertos + 1·lesionados</c> (<see cref="VillainDeathWeight"/>: una muerte pesa
    /// más que una lesión). Una víctima cuenta <b>una vez</b>: quien lesionó y luego mató al mismo jugador
    /// suma un muerto, no un muerto y un lesionado; y lesionar tres veces al mismo suma un lesionado.
    /// <c>RivalCredits</c> no guarda cronología, así que no hay «el último». Empata el clan de id menor y
    /// luego el índice menor. Null si ningún rival hizo daño: la sección se omite.
    /// </summary>
    /// <summary>
    /// ADR 0165 punto 4: la Gaceta prefiere como villano a un <b>némesis</b> —vivo o vengado— antes que al rival
    /// que más daño sumó en los créditos: es el que el jugador ha visto nacer, con título y víctima. Entre
    /// varios, el que más muertes propias suma; a igual cuenta, el de id menor (RT-041). Null si la run no tuvo
    /// ninguno (o no se le pasa el catálogo de némesis): entonces manda la regla de siempre.
    /// </summary>
    private static GazetteVillain? NemesisVillain(RunState state, NemesisCatalog? nemesis, GazetteCatalog templates, string language)
    {
        if (nemesis is null)
        {
            return null;
        }

        var villain = NemesisView.Villain(state, nemesis, language);
        if (villain is null)
        {
            return null;
        }

        string name = villain.Title.Length == 0 ? villain.Name : villain.Name + ", " + villain.Title;
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["villain"] = name,
            ["clan"] = villain.Clan,
            ["deaths"] = Number(villain.Kills),
            ["injuries"] = Number(0),
        };

        string line = Pick(templates, "villain.deaths", language, state.Seed, 300 + villain.NemesisId, facts);
        return new GazetteVillain(name, villain.Clan, villain.Kills, 0, line);
    }

    private static GazetteVillain? Villain(RunState state, IReadOnlyList<RivalCredit> credits, RivalCatalog rivals, GazetteCatalog templates, string language, Catalog catalog)
    {
        var comparer = Comparer<(string, int, int)>.Create(static (a, b) =>
        {
            int byClan = string.CompareOrdinal(a.Item1, b.Item1);
            if (byClan != 0)
            {
                return byClan;
            }

            int bySlot = a.Item2.CompareTo(b.Item2);
            return bySlot != 0 ? bySlot : a.Item3.CompareTo(b.Item3);
        });
        var killedBy = new SortedDictionary<(string, int, int), SortedSet<int>>(comparer);
        var hurtBy = new SortedDictionary<(string, int, int), SortedSet<int>>(comparer);
        for (int i = 0; i < credits.Count; i++)
        {
            var target = credits[i].Kind == RivalCreditKind.SufferedDeath ? killedBy : hurtBy;
            var key = (credits[i].RivalId, credits[i].RivalIndex, credits[i].Occupant);
            if (!target.TryGetValue(key, out var victims))
            {
                victims = new SortedSet<int>();
                target[key] = victims;
            }

            victims.Add(credits[i].OwnPlayerId);
        }

        (string Clan, int Index, int Occupant) best = (string.Empty, -1, 0);
        int bestScore = 0;
        int bestKilled = 0;
        int bestHurt = 0;
        var keys = new SortedSet<(string, int, int)>(killedBy.Keys, comparer);
        keys.UnionWith(hurtBy.Keys);
        foreach (var key in keys)
        {
            var killedVictims = killedBy.GetValueOrDefault(key);
            int killedCount = killedVictims?.Count ?? 0;
            int hurtCount = 0;
            if (hurtBy.TryGetValue(key, out var hurtVictims))
            {
                foreach (int victim in hurtVictims)
                {
                    if (killedVictims is null || !killedVictims.Contains(victim))
                    {
                        hurtCount++;
                    }
                }
            }

            int score = (killedCount * VillainDeathWeight) + hurtCount;
            if (score > bestScore)
            {
                bestScore = score;
                best = key;
                bestKilled = killedCount;
                bestHurt = hurtCount;
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

        int killed = bestKilled;
        int hurt = bestHurt;
        // BS-A: el nombre es el de quien ocupaba el puesto, no el del jugador de datos.
        string villainName = RivalKiller.Name(best.Occupant, team, best.Index, state.RivalMemory, state.Seed, catalog);
        string clan = NameIn(team.Name, language);
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["villain"] = villainName,
            ["clan"] = clan,
            ["deaths"] = Number(killed),
            ["injuries"] = Number(hurt),
        };

        string line = Pick(templates, killed > 0 ? "villain.deaths" : "villain.injuries", language, state.Seed, 200 + best.Index, facts);
        return new GazetteVillain(villainName, clan, killed, hurt, line);
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
                Epitaph(state, player, credits, rivals, templates, language, catalog)));
        }

        return rows;
    }

    /// <summary>
    /// Cómo cayó, según lo que la run sabe de su muerte (<see cref="RunState.DeathCauseOf"/>) y, en un
    /// partido, de <c>RivalCredits</c>:
    /// <list type="bullet">
    /// <item>sacrificado en un evento → <c>epitaph.sacrifice</c>; en la clínica → <c>epitaph.quack</c>;</item>
    /// <item>en un partido con un rival del catálogo acreditado → <c>epitaph.byRival</c> («a manos de X, de
    /// Y»);</item>
    /// <item>en un partido a manos de un rival que no está en el catálogo (jefe o rival procedural: hubo
    /// matador pero no crédito) → <c>epitaph.byOpponent</c>, que dice que lo mató un rival sin nombrarlo;</item>
    /// <item>en un partido sin matador → <c>epitaph.noAuthor</c>, la única que habla de «el golpe que nadie
    /// vio»;</item>
    /// <item>causa no registrada (guardado anterior) → <c>epitaph.unknown</c>, neutra: no afirma dónde ni
    /// cómo.</item>
    /// </list>
    /// </summary>
    private static string Epitaph(RunState state, RunPlayer player, IReadOnlyList<RivalCredit> credits, RivalCatalog rivals, GazetteCatalog templates, string language, Catalog catalog)
    {
        var none = new Dictionary<string, string>(StringComparer.Ordinal);
        int salt = 500 + player.Id;
        var cause = state.DeathCauseOf(player.Id);
        if (cause == PlayerDeathCause.Sacrifice)
        {
            return Pick(templates, "epitaph.sacrifice", language, state.Seed, salt, none);
        }

        if (cause == PlayerDeathCause.Quack)
        {
            return Pick(templates, "epitaph.quack", language, state.Seed, salt, none);
        }

        RivalCredit? killer = null;
        for (int i = 0; i < credits.Count; i++)
        {
            if (credits[i].Kind == RivalCreditKind.SufferedDeath && credits[i].OwnPlayerId == player.Id
                && (killer is null || credits[i].Count > killer.Count))
            {
                killer = credits[i];
            }
        }

        var team = killer is null ? null : rivals.Find(killer.RivalId);
        if (killer is not null && team is not null && killer.RivalIndex >= 0 && killer.RivalIndex < team.Players.Count)
        {
            var facts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // BS-A: quien ocupaba el puesto en aquel partido (fichaje, némesis), no el jugador de datos.
                ["killer"] = RivalKiller.Name(state.DeathKillerOf(player.Id), team, killer.RivalIndex, state.RivalMemory, state.Seed, catalog),
                ["clan"] = NameIn(team.Name, language),
            };
            return Pick(templates, "epitaph.byRival", language, state.Seed, salt, facts);
        }

        return cause switch
        {
            PlayerDeathCause.MatchByOpponent => Pick(templates, "epitaph.byOpponent", language, state.Seed, salt, none),
            PlayerDeathCause.MatchNoAuthor => Pick(templates, "epitaph.noAuthor", language, state.Seed, salt, none),
            _ => Pick(templates, "epitaph.unknown", language, state.Seed, salt, none),
        };
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

    /// <summary>
    /// Sustituye <c>{marcador}</c> por su valor. Un marcador que el contexto no da es un <b>error</b>
    /// (<see cref="InvalidOperationException"/>), no un hueco silencioso: el cargador ya valida los
    /// marcadores de cada clave (<see cref="GazetteCatalog.MarkersFor"/>), así que llegar aquí con uno
    /// desconocido es un fallo de código o de un catálogo montado a mano, y debe verse en test.
    ///
    /// <para><b>Plurales.</b> <c>{deaths|# baja|# bajas}</c> elige según la cifra de <c>deaths</c>: la
    /// primera opción si vale 1, la segunda en cualquier otro caso; con tres opciones
    /// <c>{deaths|ninguna baja|# baja|# bajas}</c> la primera es el 0. <c>#</c> es la propia cifra y una
    /// opción puede ir vacía. Así ninguna plantilla dice «1 bajas» (ADR 0163).</para>
    /// </summary>
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
                    string marker = template.Substring(i + 1, close - i - 1);
                    text.Append(Substitute(template, marker, facts));
                    i = close + 1;
                    continue;
                }
            }

            text.Append(template[i]);
            i++;
        }

        return text.ToString();
    }

    private static string Substitute(string template, string marker, IReadOnlyDictionary<string, string> facts)
    {
        string[] parts = marker.Split('|');
        if (!facts.TryGetValue(parts[0], out string? value))
        {
            throw new InvalidOperationException(
                $"la plantilla de la Gaceta «{template}» usa el marcador {{{parts[0]}}}, que este contexto no da (ADR 0163)");
        }

        if (parts.Length == 1)
        {
            return value;
        }

        // parts[0] es el hecho; le siguen dos opciones (uno | otros) o tres (cero | uno | otros).
        if (parts.Length is < 3 or > 4 || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
        {
            throw new InvalidOperationException(
                $"el marcador de plural {{{marker}}} de «{template}» necesita una cifra y dos o tres opciones (ADR 0163)");
        }

        string chosen = parts.Length == 4
            ? (count == 0 ? parts[1] : count == 1 ? parts[2] : parts[3])
            : (count == 1 ? parts[1] : parts[2]);
        return chosen.Replace("#", value, StringComparison.Ordinal);
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
