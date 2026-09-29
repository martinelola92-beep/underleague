using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Progression;

/// <summary>Experiencia concedida a un jugador tras un partido (RF-025).</summary>
public sealed record ExperienceAward(int PlayerId, int Experience);

/// <summary>
/// Progresión entre partidos (RF-023, RF-025, RF-027). Público y **puro**: no toca el estado de la run,
/// no hace E/S, no usa aleatoriedad; recibe datos y devuelve datos. La campaña de <c>/Balance</c> y, en
/// fase 2, el estado de la run son quienes aplican el resultado.
/// </summary>
public static class Progression
{
    /// <summary>Nivel máximo alcanzable por cualquier jugador, sea cual sea su rareza (RF-023).</summary>
    public const int MaxLevel = 8;

    /// <summary>Slots de perk por rareza (RF-023): la rareza es techo de perks, nunca techo de nivel.</summary>
    public static int PerkSlots(Rarity rarity) => rarity switch
    {
        Rarity.Common => 2,
        Rarity.Uncommon => 3,
        Rarity.Rare => 4,
        Rarity.Legendary => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
    };

    /// <summary>Perks con los que un jugador entra en la plantilla según su rareza (RF-023).</summary>
    public static int InitialPerks(Rarity rarity) => rarity switch
    {
        Rarity.Common => 0,
        Rarity.Uncommon => 1,
        Rarity.Rare => 2,
        Rarity.Legendary => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
    };

    /// <summary>
    /// Reparto de experiencia tras un partido (RF-025): 100% a los que jugaron y
    /// <c>benchSharePercent</c> a los suplentes. Devuelve una entrada por jugador con premio, ordenada
    /// por id ascendente; los que no aparecen en ninguna de las dos listas no reciben nada.
    /// </summary>
    public static IReadOnlyList<ExperienceAward> AwardExperience(
        IReadOnlyList<int> playedIds,
        IReadOnlyList<int> benchIds,
        ProgressionTuning tuning,
        int? matchExperienceOverride = null)
    {
        ArgumentNullException.ThrowIfNull(playedIds);
        ArgumentNullException.ThrowIfNull(benchIds);
        ArgumentNullException.ThrowIfNull(tuning);

        int matchExperience = matchExperienceOverride ?? tuning.MatchExperience;
        int benchExperience = matchExperience * tuning.BenchSharePercent / 100;

        var awards = new List<ExperienceAward>(playedIds.Count + benchIds.Count);
        for (int i = 0; i < playedIds.Count; i++)
        {
            awards.Add(new ExperienceAward(playedIds[i], matchExperience));
        }

        for (int i = 0; i < benchIds.Count; i++)
        {
            // Un jugador no puede cobrar dos veces: si está en las dos listas manda haber jugado.
            if (!Contains(playedIds, benchIds[i]))
            {
                awards.Add(new ExperienceAward(benchIds[i], benchExperience));
            }
        }

        awards.Sort(static (a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return awards;
    }

    /// <summary>
    /// Reparto de experiencia tras un partido (RF-025) teniendo en cuenta los perks que la modifican
    /// **fuera** del partido (efecto <c>modifyExperience</c>): la habilidad racial Adaptables de los
    /// humanos (RF-031b, ADR 0026) y cualquier perk u objeto futuro que use el mismo canal.
    /// <para>
    /// Es una sobrecarga y no un cambio de la existente porque el reparto base -quién cobra y cuánto- es
    /// una regla del calendario y no del jugador: aquí solo se aplica, al final, el multiplicador de cada
    /// uno. El resultado sigue ordenado por id ascendente y sigue siendo entero (RT-023).
    /// </para>
    /// </summary>
    public static IReadOnlyList<ExperienceAward> AwardExperience(
        IReadOnlyList<PlayerDefinition> played,
        IReadOnlyList<PlayerDefinition> bench,
        Catalog catalog,
        ProgressionTuning tuning,
        int? matchExperienceOverride = null)
    {
        ArgumentNullException.ThrowIfNull(played);
        ArgumentNullException.ThrowIfNull(bench);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(tuning);

        var flat = AwardExperience(
            played.Select(p => p.Id).ToArray(),
            bench.Select(p => p.Id).ToArray(),
            tuning,
            matchExperienceOverride);

        var awards = new List<ExperienceAward>(flat.Count);
        for (int i = 0; i < flat.Count; i++)
        {
            var award = flat[i];
            var definition = Find(played, award.PlayerId) ?? Find(bench, award.PlayerId);
            int percent = definition is null ? 100 : ExperiencePercent(definition, catalog);
            awards.Add(award with { Experience = award.Experience * percent / 100 });
        }

        return awards;
    }

    /// <summary>
    /// Porcentaje de experiencia del jugador: 100 más la suma de los <c>modifyExperience</c> de su
    /// habilidad racial y de los perks que lleva. Nunca baja de 0.
    /// </summary>
    public static int ExperiencePercent(PlayerDefinition player, Catalog catalog)
    {
        int percent = 100;
        foreach (var perk in ActivePerks(player, catalog))
        {
            for (int e = 0; e < perk.Effects.Count; e++)
            {
                if (perk.Effects[e].Type == EffectType.ModifyExperience)
                {
                    percent += perk.Effects[e].Value;
                }
            }
        }

        return percent < 0 ? 0 : percent;
    }

    /// <summary>
    /// True si el jugador tiene esa inmunidad fuera del partido (efecto <c>immunity</c>, ADR 0026): la
    /// habilidad No sienten nada de los no-muertos concede <see cref="ImmunityKind.Mourning"/> (RF-104) y
    /// <see cref="ImmunityKind.MinorInjuryPenalty"/> (RF-035). Es el único sitio donde la capa de campaña
    /// tiene que preguntar por ellas.
    /// </summary>
    public static bool HasImmunity(PlayerDefinition player, Catalog catalog, ImmunityKind kind)
    {
        foreach (var perk in ActivePerks(player, catalog))
        {
            for (int e = 0; e < perk.Effects.Count; e++)
            {
                if (perk.Effects[e].Type == EffectType.Immunity && perk.Effects[e].Immunity == kind)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Perks que surten efecto sobre el jugador: su habilidad racial (asignada por raza, sin ocupar slot)
    /// más los que lleva, descartando los exclusivos de otra raza (ADR 0023 §4). Orden determinista:
    /// primero la habilidad, luego los perks en el orden en que están en el jugador.
    /// </summary>
    private static IEnumerable<PerkDefinition> ActivePerks(PlayerDefinition player, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(catalog);

        string ability = catalog.Race(player.Race).Ability;
        if (ability.Length > 0 && catalog.Perks.Find(ability) is { } racial)
        {
            yield return racial;
        }

        for (int i = 0; i < player.Perks.Count; i++)
        {
            var perk = catalog.Perks.Find(player.Perks[i]);
            if (perk is null || (perk.Race is { } required && !player.HasTag(required.ToString())))
            {
                continue;
            }

            yield return perk;
        }
    }

    private static PlayerDefinition? Find(IReadOnlyList<PlayerDefinition> players, int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
            {
                return players[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Nivel correspondiente a una experiencia acumulada. La tabla
    /// <c>tuning.progression.experiencePerLevel</c> es acumulada: su entrada i es la experiencia mínima
    /// del nivel i+1. El resultado nunca pasa de <see cref="MaxLevel"/> (RF-023).
    /// </summary>
    public static int LevelFor(int experience, ProgressionTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var table = tuning.ExperiencePerLevel;
        int level = 1;
        for (int i = 0; i < table.Count; i++)
        {
            if (experience >= table[i])
            {
                level = i + 1;
            }
        }

        return level > MaxLevel ? MaxLevel : level;
    }

    /// <summary>
    /// Atributos de un jugador en el nivel indicado (RF-027): <c>attributesPerLevel</c> por nivel por
    /// encima del 1 a cada atributo **salvo la correa**, que es disciplina posicional y no nivel (§2.6 de
    /// fase 0). Subir de nivel nunca otorga perks.
    /// </summary>
    public static Attributes AttributesAtLevel(Attributes levelOne, int level, ProgressionTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        int steps = Math.Clamp(level, 1, MaxLevel) - 1;
        int bonus = steps * tuning.AttributesPerLevel;
        return Attributes.Clamp(new Attributes(
            levelOne.Strength + bonus,
            levelOne.Speed + bonus,
            levelOne.Technique + bonus,
            levelOne.Stamina + bonus,
            levelOne.Leash));
    }

    /// <summary>
    /// Sube al jugador al nivel indicado aplicando la diferencia de niveles a sus atributos actuales
    /// (RF-027). Si <paramref name="newLevel"/> no es mayor que el actual, devuelve el jugador sin tocar.
    /// </summary>
    public static PlayerDefinition LevelUp(PlayerDefinition player, int newLevel, ProgressionTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(tuning);

        int target = Math.Clamp(newLevel, 1, MaxLevel);
        if (target <= player.Level)
        {
            return player;
        }

        int bonus = (target - player.Level) * tuning.AttributesPerLevel;
        var attributes = Attributes.Clamp(new Attributes(
            player.Attributes.Strength + bonus,
            player.Attributes.Speed + bonus,
            player.Attributes.Technique + bonus,
            player.Attributes.Stamina + bonus,
            player.Attributes.Leash));

        return player with { Level = target, Attributes = attributes };
    }

    /// <summary>
    /// Baja al jugador <paramref name="levels"/> niveles (mínimo 1), revirtiendo la misma aritmética que
    /// <see cref="LevelUp"/> (RF-027): resta <c>attributesPerLevel</c> por nivel perdido a todos los
    /// atributos salvo la correa. Es lo que necesitan dos mecánicas nuevas con el mismo coste ("pierde un
    /// nivel"): el efecto <c>level</c> de eventos (ADR 0159) y el cambio de puesto del entrenamiento (ADR
    /// 0160). Si <paramref name="levels"/> no baja del nivel actual, devuelve el jugador sin tocar.
    /// </summary>
    public static PlayerDefinition LevelDown(PlayerDefinition player, int levels, ProgressionTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(tuning);

        int target = Math.Clamp(player.Level - Math.Max(0, levels), 1, MaxLevel);
        if (target >= player.Level)
        {
            return player;
        }

        int bonus = (player.Level - target) * tuning.AttributesPerLevel;
        var attributes = Attributes.Clamp(new Attributes(
            player.Attributes.Strength - bonus,
            player.Attributes.Speed - bonus,
            player.Attributes.Technique - bonus,
            player.Attributes.Stamina - bonus,
            player.Attributes.Leash));

        return player with { Level = target, Attributes = attributes };
    }

    /// <summary>
    /// Experiencia mínima para alcanzar <paramref name="level"/> (el mismo umbral que usa
    /// <see cref="LevelFor"/> en sentido inverso: la entrada <c>level-1</c> de la tabla acumulada, que es la
    /// experiencia mínima del nivel <c>level</c>); 0 para el nivel 1. Arreglo de la revisión independiente
    /// (29 sep 2026): antes leía la entrada <c>level-2</c>, un nivel por debajo de lo que dice
    /// <see cref="LevelFor"/>. <see cref="LevelDown"/> no toca la experiencia del jugador -solo el nivel y los
    /// atributos-, así que quien pierde niveles tiene que acotarla con <see cref="ExperienceAfterLevelLoss"/>
    /// o la próxima vez que <see cref="LevelFor"/> la lea el jugador "sube" solo, deshaciendo la pérdida
    /// (Regla I).
    /// </summary>
    public static int MinExperienceForLevel(int level, ProgressionTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        int clamped = Math.Clamp(level, 1, MaxLevel);
        var table = tuning.ExperiencePerLevel;
        int index = clamped - 1;
        return index < table.Count ? table[index] : (table.Count > 0 ? table[^1] : 0);
    }

    /// <summary>
    /// Experiencia con la que queda un jugador que acaba de <b>perder niveles</b> y está ahora en
    /// <paramref name="newLevel"/>: como mucho la mínima de ese nivel (se pierde el progreso que llevara
    /// hacia el siguiente). Con eso <see cref="LevelFor"/> devuelve <paramref name="newLevel"/> y la
    /// próxima experiencia ganada no deshace la pérdida (Regla I); no sube nunca la que ya tuviera por
    /// debajo.
    /// </summary>
    public static int ExperienceAfterLevelLoss(int experience, int newLevel, ProgressionTuning tuning) =>
        Math.Min(experience, MinExperienceForLevel(newLevel, tuning));

    /// <summary>
    /// Suma al jugador los contadores que los perks acumulativos ganaron en un partido (RF-070, §6).
    /// Solo se aplican las entradas cuyo <c>PlayerId</c> coincide; el resto se ignora.
    /// </summary>
    public static PlayerDefinition ApplyCounterDeltas(PlayerDefinition player, IReadOnlyList<PlayerCounterDelta> deltas)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(deltas);

        var counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in player.Counters)
        {
            counters[name] = value;
        }

        bool changed = false;
        for (int i = 0; i < deltas.Count; i++)
        {
            var delta = deltas[i];
            if (delta.PlayerId != player.Id || delta.Delta == 0)
            {
                continue;
            }

            counters[delta.Counter] = (counters.TryGetValue(delta.Counter, out int current) ? current : 0) + delta.Delta;
            changed = true;
        }

        return changed ? player with { Counters = counters } : player;
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
