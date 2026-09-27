using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Referees;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Ficha del árbitro de un nodo de partido (ADR 0158 §6, RF-061, RF-012b): nombre, rasgo, la línea que
/// explica lo que hace su rasgo -compuesta desde plantilla, RT-035, nunca escrita a mano en C#-, su
/// muletilla y la memoria que tiene del jugador. Es presentación pura sobre datos ya calculados
/// (<c>IRunSystems.RefereeFor</c>, <c>RunReferee.Grudge</c>): no decide nada del partido (RT-014).
/// </summary>
/// <param name="Name">Nombre en el idioma pedido.</param>
/// <param name="Trait">Rasgo del árbitro (ADR 0158 §2).</param>
/// <param name="TraitLine">Línea compuesta desde <c>data/l10n/&lt;lang&gt;/templates.json</c>, sección <c>refereeTraits</c>.</param>
/// <param name="Catchphrase">Muletilla en el idioma pedido.</param>
/// <param name="Grudge">Memoria contra el jugador ahora mismo (-40..40, ADR 0158 §4); 0 si nunca ha pitado un partido de la run.</param>
/// <param name="InitialBias">Criterio con el que sale al campo si pita este nodo (<c>Grudge</c> + el -20 del casero, acotado).</param>
/// <param name="BlindSide">Media banda que no ve; <see cref="RefereeSide.None"/> salvo con <see cref="RefereeTrait.OneEyed"/>.</param>
public sealed record RefereeCardView(
    string Name,
    RefereeTrait Trait,
    string TraitLine,
    string Catchphrase,
    int Grudge,
    int InitialBias,
    RefereeSide BlindSide);

/// <summary>Compone la ficha del árbitro para /Game (ADR 0158 §6: ojeo y mapa).</summary>
public static class RefereeView
{
    private const string Section = "refereeTraits";

    /// <summary>
    /// Ficha del árbitro que pitaría el nodo indicado, tal y como lo decide <c>systems.RefereeFor</c>. El
    /// <c>Grudge</c> mostrado sale del árbitro de <see cref="RunState.Referees"/> que coincide por nombre
    /// con el que devuelve <c>RefereeFor</c> (mismo criterio de correspondencia que
    /// <c>PostMatchView.Referee</c> y <c>MatchResolution.ApplyRefereeMemory</c>); si no hay coincidencia
    /// (plantel sin catálogo de datos, por ejemplo en pruebas) se enseña 0.
    /// </summary>
    public static RefereeCardView For(RunState state, MapNode node, StandardRunSystems systems, Catalog catalog, string language)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(systems);
        ArgumentNullException.ThrowIfNull(catalog);

        var setup = systems.RefereeFor(state, node, catalog);
        var runReferee = FindByName(state.Referees, setup.Name);
        var definition = runReferee is null ? null : systems.Referees.Find(runReferee.DefinitionId);

        var templates = catalog.Localization.Get(language);
        string traitLine = templates.Find(Section, TraitKey(setup.Trait)) ?? string.Empty;

        return new RefereeCardView(
            definition is null ? setup.Name : Text(definition.Name, language),
            setup.Trait,
            traitLine,
            definition is null ? string.Empty : Text(definition.Catchphrase, language),
            runReferee?.Grudge ?? 0,
            setup.InitialBias,
            setup.BlindSide);
    }

    private static RunReferee? FindByName(IReadOnlyList<RunReferee> referees, string name)
    {
        for (int i = 0; i < referees.Count; i++)
        {
            if (string.Equals(referees[i].Name, name, StringComparison.Ordinal))
            {
                return referees[i];
            }
        }

        return null;
    }

    private static string TraitKey(RefereeTrait trait) => trait switch
    {
        RefereeTrait.Neutral => "neutral",
        RefereeTrait.Strict => "strict",
        RefereeTrait.Lenient => "lenient",
        RefereeTrait.Homer => "homer",
        RefereeTrait.OneEyed => "oneEyed",
        RefereeTrait.Cowardly => "cowardly",
        RefereeTrait.Corrupt => "corrupt",
        RefereeTrait.Incorruptible => "incorruptible",
        _ => throw new ArgumentOutOfRangeException(nameof(trait), trait, "rasgo de árbitro desconocido"),
    };

    private static string Text(LocalizedName name, string language) =>
        string.Equals(language, "en", StringComparison.Ordinal) ? name.En : name.Es;
}
