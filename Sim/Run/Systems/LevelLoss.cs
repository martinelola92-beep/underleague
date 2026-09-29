using Underleague.Sim.Data;
using Underleague.Sim.Model;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Perder niveles (efecto <c>level</c> de los eventos, ADR 0159; cambio de puesto del entrenamiento, ADR
/// 0160): la misma aritmética que sube un nivel al revés (RF-027) y la experiencia acotada al mínimo del
/// nivel nuevo, en un solo sitio para que las dos mecánicas no discrepen (Regla I).
/// </summary>
internal static class LevelLoss
{
    /// <summary>
    /// El jugador con <paramref name="levels"/> niveles menos (mínimo 1). Si ya está en el nivel 1 no cambia:
    /// por eso las opciones que cuestan un nivel solo señalan a jugadores de nivel 2 o más
    /// (<see cref="CanLose"/>).
    /// </summary>
    public static RunPlayer Apply(RunPlayer player, int levels, Catalog catalog)
    {
        var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
        var down = ProgressionRules.LevelDown(definition, levels, catalog.Progression);
        if (down.Level == player.Level)
        {
            return player;
        }

        return player with
        {
            Level = down.Level,
            Attributes = down.Attributes,
            Experience = ProgressionRules.ExperienceAfterLevelLoss(player.Experience, down.Level, catalog.Progression),
        };
    }

    /// <summary>Si perder un nivel le cuesta algo: a nivel 1 la opción sale gratis, así que no se ofrece.</summary>
    public static bool CanLose(RunPlayer player) => player.Level >= 2;
}
