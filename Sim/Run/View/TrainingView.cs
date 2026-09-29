using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Nodes;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Una posición de destino del cambio de puesto para un jugador concreto: <paramref name="Allowed"/> falso
/// cuando un perk suyo no vale ahí, con <paramref name="Reason"/> ya localizada (RF-012d: la opción inviable se
/// ve y se explica, no se descubre al pulsar).
/// </summary>
public sealed record TrainingDestinationRow(Position Position, string Name, bool Allowed, string Reason);

/// <summary>Un jugador al que se puede señalar en una sesión; en el cambio de puesto lleva sus destinos.</summary>
public sealed record TrainingTargetRow(int PlayerId, string Name, string Detail, IReadOnlyList<TrainingDestinationRow> Destinations);

/// <summary>
/// Una sesión de la carta tal y como la ve el jugador: su nombre, la línea de efecto compuesta (RT-035) y
/// si hace falta señalar a alguien (especialización y cambio de puesto) o elegir posición de destino
/// (solo cambio de puesto). <see cref="Targets"/> ya viene filtrada -sin porteros ni jugadores de nivel 1 en el
/// cambio de puesto, sin quien ya está en 99 en la especialización- para que la interfaz no pueda ofrecer algo
/// que <c>TrainingSystem.Choose</c> rechazaría. <see cref="Available"/> es lo que <c>Choose</c> aceptaría
/// (hay a quién señalar y, en el cambio de puesto, algún destino permitido) y es falso una vez elegida la sesión.
/// </summary>
public sealed record TrainingSessionRow(
    int Index,
    string Name,
    string Effect,
    TrainingSessionKind Kind,
    bool NeedsTarget,
    bool NeedsPosition,
    bool Available,
    IReadOnlyList<TrainingTargetRow> Targets);

/// <summary>
/// La carta del nodo de entrenamiento abierto (ADR 0160). <paramref name="Resolved"/>: la sesión ya se eligió
/// (se elige una vez, <c>RunState.NodeResolvedCounter</c>) y solo queda salir.
/// </summary>
public sealed record TrainingScreenView(int NodeId, int Act, IReadOnlyList<TrainingSessionRow> Sessions, bool Resolved = false);

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
        bool resolved = NodeGuards.IsResolved(state, node);
        var sessions = new List<TrainingSessionRow>(card.Sessions.Count);
        for (int i = 0; i < card.Sessions.Count; i++)
        {
            var session = card.Sessions[i];
            bool needsTarget = session.Kind != TrainingSessionKind.Scrimmage;
            var targets = session.Kind switch
            {
                TrainingSessionKind.Reposition => RepositionTargets(state, catalog, templates, language),
                TrainingSessionKind.Specialization => SpecializationTargets(state, session.Attribute, templates),
                _ => Array.Empty<TrainingTargetRow>(),
            };

            bool available = !resolved && session.Kind switch
            {
                TrainingSessionKind.Scrimmage => true,
                TrainingSessionKind.Reposition => targets.Any(t => t.Destinations.Any(d => d.Allowed)),
                _ => targets.Count > 0,
            };

            sessions.Add(new TrainingSessionRow(
                i,
                Name(session, templates, language),
                Effect(session, economy, templates),
                session.Kind,
                needsTarget,
                session.Kind == TrainingSessionKind.Reposition,
                available,
                targets));
        }

        return new TrainingScreenView(node.Id, node.Act, sessions, resolved);
    }

    private static IReadOnlyList<TrainingTargetRow> SpecializationTargets(RunState state, AttributeKind attribute, DescriptionTemplates templates)
    {
        var rows = new List<TrainingTargetRow>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (!player.IsAvailable || !TrainingSystem.CanSpecialize(player, attribute))
            {
                continue;
            }

            string detail = EventView.Detail(player, templates)
                + Format(templates.Find(Section, "targetAttribute") ?? " {0} {1}", AttributeName(templates, attribute), player.Attributes.Get(attribute).ToString(System.Globalization.CultureInfo.InvariantCulture));
            rows.Add(new TrainingTargetRow(player.Id, player.Name, detail, Array.Empty<TrainingDestinationRow>()));
        }

        return rows;
    }

    /// <summary>
    /// Jugadores de campo disponibles y de nivel 2 o más (sin portero, ADR 0080; a nivel 1 perder un nivel
    /// sale gratis), cada uno con sus tres posibles destinos menos la que ya juega: los permitidos y los que
    /// un perk suyo impide, con el motivo.
    /// </summary>
    private static IReadOnlyList<TrainingTargetRow> RepositionTargets(RunState state, Catalog catalog, DescriptionTemplates templates, string language)
    {
        var rows = new List<TrainingTargetRow>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (!player.IsAvailable || player.Position == Position.Goalkeeper || player.Level < 2)
            {
                continue;
            }

            var destinations = new List<TrainingDestinationRow>(TrainingSystem.FieldPositions.Count);
            for (int p = 0; p < TrainingSystem.FieldPositions.Count; p++)
            {
                var position = TrainingSystem.FieldPositions[p];
                if (position == player.Position)
                {
                    continue;
                }

                string name = templates.Find("positions", position.ToString()) ?? position.ToString();
                string? blocking = TrainingSystem.BlockingPerk(player, position, catalog);
                string reason = blocking is null
                    ? string.Empty
                    : Format(templates.Find(Section, "positionBlocked") ?? "{0} {1}", PerkName(catalog, blocking, language), name);
                destinations.Add(new TrainingDestinationRow(position, name, blocking is null, reason));
            }

            rows.Add(new TrainingTargetRow(player.Id, player.Name, EventView.Detail(player, templates), destinations));
        }

        return rows;
    }

    private static string PerkName(Catalog catalog, string perkId, string language) =>
        catalog.Perks.Find(perkId) is { } perk
            ? (string.Equals(language, "en", StringComparison.Ordinal) ? perk.Name.En : perk.Name.Es)
            : perkId;

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
