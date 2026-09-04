using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Economy;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run.Systems.Nodes;

/// <summary>
/// Entrenamiento y evento (RF-011): ninguno de los dos tiene mecánica propia en <c>requisitos.md</c> ni
/// en <c>fase2-diseno.md</c>, así que el paquete X les da el tratamiento más conservador que cumple lo
/// que sí está escrito y nada más:
/// <list type="bullet">
/// <item><b>Entrenamiento</b>: experiencia fija para toda la plantilla disponible (mismo mecanismo de
/// <c>Sim.Progression.Progression</c> que usa un partido, sin RNG: entrenar no es una apuesta).</item>
/// <item><b>Evento</b>: oro dentro de una banda (RF-114j: "las otras fuentes de oro son la venta... y
/// determinados eventos"), sorteado con <c>RngStreams.Rewards(seed, node.Id)</c> -no el flujo de partido
/// (RT-022)-, así que es reproducible y no altera ningún partido.</item>
/// </list>
/// Los dos se resuelven solos, sin decisión del jugador (contrato de <c>IRunSystems.OpenNode</c>).
/// </summary>
public static class ServiceNodeSystem
{
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

    public static RunState Event(RunState state, MapNode node, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(economy);

        var rng = RngStreams.Rewards(state.Seed, node.Id);
        int gold = economy.EventGoldMin + rng.Range(0, economy.EventGoldMax - economy.EventGoldMin + 1);
        return state.AddGold(gold);
    }
}
