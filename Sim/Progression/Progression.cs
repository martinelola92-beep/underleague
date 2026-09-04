using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

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
        Rarity.Rare => 3,
        Rarity.Legendary => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
    };

    /// <summary>Perks con los que un jugador entra en la plantilla según su rareza (RF-023).</summary>
    public static int InitialPerks(Rarity rarity) => rarity switch
    {
        Rarity.Common => 0,
        Rarity.Rare => 1,
        Rarity.Legendary => 2,
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
