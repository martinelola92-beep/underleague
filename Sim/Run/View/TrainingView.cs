using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Nodes;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Una sesión de la carta tal y como la ve el jugador: su nombre, la línea de efecto compuesta (RT-035) y
/// si hace falta señalar a alguien (especialización y cambio de puesto) o elegir posición de destino
/// (solo cambio de puesto). <see cref="Targets"/> ya viene filtrada -sin porteros en el cambio de puesto,
/// ADR 0080/0160- para que la interfaz no pueda ofrecer algo que <c>TrainingSystem.Choose</c> rechazaría.
/// </summary>
public sealed record TrainingSessionRow(
    int Index,
    string Name,
    string Effect,
    TrainingSessionKind Kind,
    bool NeedsTarget,
    bool NeedsPosition,
    IReadOnlyList<EventTargetRow> Targets);

/// <summary>La carta del nodo de entrenamiento abierto (ADR 0160).</summary>
public sealed record TrainingScreenView(int NodeId, int Act, IReadOnlyList<TrainingSessionRow> Sessions);

/// <summary>
/// Compone la carta para <c>/Game</c> (ADR 0160). Las tres sesiones y su coste se ven antes de elegir
/// (RF-012d): la pachanga es gratis, la especialización cuesta la experiencia del resto y el cambio de
/// puesto cuesta un nivel.
/// </summary>
public static class TrainingView
{
    private const string Section = "training";

    public static TrainingScreenView? Build(RunState state, Catalog catalog, EconomyConfig economy, string language)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(economy);
        if (state.PendingNodeId < 0)
        {
            return null;
        }

        var node = state.GetNode(state.PendingNodeId);
        if (node.Kind != NodeKind.Training)
        {
            return null;
        }

        var card = TrainingSystem.Card(node);
        var templates = catalog.Localization.Get(language);
        var allTargets = EventView.Targets(state);
        var outfieldTargets = Outfield(state, allTargets);
        var sessions = new List<TrainingSessionRow>(card.Sessions.Count);
        for (int i = 0; i < card.Sessions.Count; i++)
        {
            var session = card.Sessions[i];
            bool needsTarget = session.Kind != TrainingSessionKind.Scrimmage;
            var candidates = session.Kind switch
            {
                TrainingSessionKind.Reposition => outfieldTargets,
                TrainingSessionKind.Specialization => allTargets,
                _ => Array.Empty<EventTargetRow>(),
            };

            sessions.Add(new TrainingSessionRow(
                i,
                Name(session, templates, language),
                Effect(session, economy, templates),
                session.Kind,
                needsTarget,
                session.Kind == TrainingSessionKind.Reposition,
                candidates));
        }

        return new TrainingScreenView(node.Id, node.Act, sessions);
    }

    /// <summary>Jugadores de campo disponibles (sin portero, ADR 0080): los únicos que pueden cambiar de puesto.</summary>
    private static IReadOnlyList<EventTargetRow> Outfield(RunState state, IReadOnlyList<EventTargetRow> all)
    {
        var rows = new List<EventTargetRow>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            if (state.GetPlayer(all[i].PlayerId).Position != Position.Goalkeeper)
            {
                rows.Add(all[i]);
            }
        }

        return rows;
    }

    private static string Name(TrainingSession session, DescriptionTemplates templates, string language) => session.Kind switch
    {
        TrainingSessionKind.Scrimmage => templates.Find(Section, "scrimmage") ?? "Scrimmage",
        TrainingSessionKind.Reposition => templates.Find(Section, "reposition") ?? "Reposition",
        TrainingSessionKind.Specialization => Format(
            templates.Find(Section, "specialization") ?? "{0}",
            AttributeName(templates, session.Attribute)),
        _ => string.Empty,
    };

    private static string Effect(TrainingSession session, EconomyConfig economy, DescriptionTemplates templates) => session.Kind switch
    {
        TrainingSessionKind.Scrimmage => Format(
            templates.Find(Section, "scrimmageEffect") ?? "{0}",
            economy.TrainingExperience.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        TrainingSessionKind.Specialization => Format(
            templates.Find(Section, "specializationEffect") ?? "{0} {1}",
            TrainingSystem.SpecializationBonus.ToString(System.Globalization.CultureInfo.InvariantCulture),
            AttributeName(templates, session.Attribute)),
        TrainingSessionKind.Reposition => Format(
            templates.Find(Section, "repositionEffect") ?? "{0}",
            TrainingSystem.RepositionLevelCost.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        _ => string.Empty,
    };

    private static string AttributeName(DescriptionTemplates templates, AttributeKind attribute)
    {
        string key = attribute switch
        {
            AttributeKind.Strength => "strength",
            AttributeKind.Speed => "speed",
            AttributeKind.Technique => "technique",
            AttributeKind.Stamina => "stamina",
            AttributeKind.Leash => "leash",
            _ => string.Empty,
        };

        return templates.Find("attributes", key) ?? key;
    }

    private static string Format(string template, params string[] values)
    {
        string text = template;
        for (int i = 0; i < values.Length; i++)
        {
            text = text.Replace("{" + i + "}", values[i], StringComparison.Ordinal);
        }

        return text;
    }
}
