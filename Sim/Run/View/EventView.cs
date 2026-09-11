using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Events;

namespace Underleague.Sim.Run.View;

/// <summary>Un candidato al que señalar cuando la opción pide un cuerpo (ADR 0100).</summary>
public sealed record EventTargetRow(int PlayerId, string Name, string Detail);

/// <summary>
/// Una opción de la carta tal y como la ve el jugador: su nombre, <b>la línea de efecto compuesta</b>
/// (RT-035: no hay texto de efecto escrito a mano) y si hace falta señalar a alguien.
/// </summary>
public sealed record EventOptionRow(
    int Index,
    string Name,
    string Effect,
    bool NeedsTarget,
    bool Affordable,
    IReadOnlyList<EventTargetRow> Targets);

/// <summary>La carta del nodo de evento abierto.</summary>
public sealed record EventScreenView(
    int NodeId,
    int Act,
    int Gold,
    string Title,
    string Description,
    IReadOnlyList<EventOptionRow> Options);

/// <summary>
/// Compone la carta para <c>/Game</c> (ADR 0100). Todo el coste se ve aquí, antes de elegir, que es lo que
/// hace legítimo que una opción cobre oro o deje a alguien lesionado (RF-012d).
/// </summary>
public static class EventView
{
    private const string Section = "eventEffects";

    public static EventScreenView? Build(RunState state, Catalog catalog, EventCatalog events, string language)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(events);
        if (state.PendingNodeId < 0)
        {
            return null;
        }

        var node = state.GetNode(state.PendingNodeId);
        if (node.Kind != NodeKind.Event)
        {
            return null;
        }

        var card = EventSystem.Card(state, node, events);
        var templates = catalog.Localization.Get(language);
        var targets = Targets(state);
        var options = new List<EventOptionRow>(card.Options.Count);
        for (int i = 0; i < card.Options.Count; i++)
        {
            var option = card.Options[i];
            options.Add(new EventOptionRow(
                i,
                Text(option.Name, language),
                Effect(option, templates, state),
                option.NeedsTarget,
                state.Gold + GoldDelta(option, state) >= 0,
                option.NeedsTarget ? targets : Array.Empty<EventTargetRow>()));
        }

        return new EventScreenView(
            node.Id,
            node.Act,
            state.Gold,
            Text(card.Name, language),
            Text(card.Description, language),
            options);
    }

    /// <summary>Lo que esa opción suma o resta al oro, para poder decir si se puede pagar antes de elegir.</summary>
    public static int GoldDelta(EventOption option, RunState state)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(state);
        int delta = 0;
        for (int i = 0; i < option.Effects.Count; i++)
        {
            delta += option.Effects[i].Kind switch
            {
                EventEffectKind.Gold => option.Effects[i].Value,
                EventEffectKind.GoldShare => state.Gold * option.Effects[i].Value / 100,
                _ => 0,
            };
        }

        return delta;
    }

    private static string Effect(EventOption option, DescriptionTemplates templates, RunState state)
    {
        if (option.Effects.Count == 0)
        {
            return templates.Find(Section, "nothing") ?? string.Empty;
        }

        var parts = new List<string>(option.Effects.Count);
        for (int i = 0; i < option.Effects.Count; i++)
        {
            var effect = option.Effects[i];
            string key = effect.Kind switch
            {
                EventEffectKind.Gold => "gold",
                EventEffectKind.GoldShare => "goldShare",
                EventEffectKind.Heal => "heal",
                EventEffectKind.Experience => "experience",
                EventEffectKind.ExperienceTarget => "experienceTarget",
                _ => effect.Value >= 2 ? "injureSevere" : "injureMinor",
            };

            string template = templates.Find(Section, key) ?? key;
            parts.Add(effect.Kind switch
            {
                EventEffectKind.Gold => Format(template, Signed(effect.Value)),
                EventEffectKind.GoldShare => Format(template, Signed(effect.Value)),
                EventEffectKind.Experience or EventEffectKind.ExperienceTarget => Format(template, "+" + effect.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                _ => template,
            });
        }

        return string.Join(" · ", parts);
    }

    private static string Text(LocalizedName name, string language) =>
        string.Equals(language, "en", StringComparison.Ordinal) ? name.En : name.Es;

    private static string Signed(int value) =>
        (value >= 0 ? "+" : string.Empty) + value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(string template, string value) => template.Replace("{0}", value, StringComparison.Ordinal);

    private static IReadOnlyList<EventTargetRow> Targets(RunState state)
    {
        var rows = new List<EventTargetRow>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.IsAvailable)
            {
                rows.Add(new EventTargetRow(player.Id, player.Name, player.Position + " · " + player.Level));
            }
        }

        return rows;
    }
}
