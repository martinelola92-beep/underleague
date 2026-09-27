using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run.Systems.Nodes;

/// <summary>
/// Entrenamiento y evento (RF-011): ninguno de los dos tenía mecánica propia al escribir el paquete X, así
/// que aquí quedó el tratamiento más conservador que cumplía lo que estaba escrito y nada más.
///
/// <para><b>Entrenamiento (la pachanga)</b>: experiencia fija para toda la plantilla disponible (mismo
/// mecanismo de <c>Sim.Progression.Progression</c> que usa un partido, sin RNG: entrenar no es una
/// apuesta). Hasta la <b>ADR 0160</b> era lo único que hacía el nodo, sin preguntar nada (RF-026
/// incumplido: "experiencia dirigida al jugador que el usuario elija"). Desde esa ADR sigue siendo
/// exactamente este método, pero como <b>una de tres sesiones</b> que elige el jugador
/// (<c>Sim.Run.Systems.Nodes.TrainingSystem</c>), no la única salida del nodo.</para>
///
/// <para>El <b>evento</b> estaba aquí y pagaba oro de una banda; desde la <b>ADR 0100</b> es una carta con
/// opciones y vive en <c>Sim.Run.Systems.Events</c>.</para>
/// </summary>
public static class ServiceNodeSystem
{
    /// <summary>
    /// La pachanga (ADR 0160, sesión 0, siempre presente): experiencia de hoy para todos los disponibles,
    /// como el entrenamiento de antes de la ADR -byte a byte el mismo código, para que "la pachanga
    /// equivale al entrenamiento anterior" sea una garantía de diseño y no una coincidencia de prueba-.
    /// </summary>
    public static RunState Training(RunState state, EconomyConfig economy, Data.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(catalog);

        var roster = new List<RunPlayer>(state.Roster);
        for (int i = 0; i < roster.Count; i++)
        {
            var player = roster[i];
            if (!player.IsAvailable)
            {
                continue;
            }

            int experience = economy.TrainingExperience;
            if (player.IsYouth)
            {
                experience = experience * (100 + RunRules.YouthExperienceBonusPercent) / 100;
            }

            int total = player.Experience + experience;
            int level = ProgressionRules.LevelFor(total, catalog.Progression);
            if (level == player.Level)
            {
                roster[i] = player.WithExperience(total);
                continue;
            }

            var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
            definition = ProgressionRules.LevelUp(definition, level, catalog.Progression);
            roster[i] = player with { Experience = total, Level = definition.Level, Attributes = definition.Attributes };
        }

        return state.WithRoster(roster);
    }
}
