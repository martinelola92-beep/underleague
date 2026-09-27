using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Progression;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Events;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Run.View;

/// <summary>Un candidato al que señalar cuando la opción pide un cuerpo (ADR 0100).</summary>
public sealed record EventTargetRow(int PlayerId, string Name, string Detail);

/// <summary>
/// Una opción de la carta tal y como la ve el jugador: su nombre, <b>la línea de efecto compuesta</b>
/// (RT-035: no hay texto de efecto escrito a mano) y si hace falta señalar a alguien. Desde la
/// <b>ADR 0159</b> puede pedir un <b>segundo</b> cuerpo (<see cref="SecondTargets"/>, el sacrificio y
/// cualquier opción que hiera a un espectador distinto del premiado), y <see cref="Affordable"/> ya no
/// es solo "hay oro": es <b>viable</b> —hay oro, hay a quién señalar, hay hueco de plantilla o de perk—,
/// para que la interfaz deshabilite en vez de dejar que el jugador pulse algo que <c>EventSystem.Choose</c>
/// rechazaría (RF-012d: la opción inviable se ve, no se descubre al pulsar).
/// </summary>
public sealed record EventOptionRow(
    int Index,
    string Name,
    string Effect,
    bool NeedsTarget,
    bool NeedsSecondTarget,
    bool Affordable,
    IReadOnlyList<EventTargetRow> Targets,
    IReadOnlyList<EventTargetRow> SecondTargets);

/// <summary>La carta del nodo de evento abierto.</summary>
public sealed record EventScreenView(
    int NodeId,
    int Act,
    int Gold,
    string Title,
    string Description,
    IReadOnlyList<EventOptionRow> Options);

/// <summary>
/// Compone la carta para <c>/Game</c> (ADR 0100, ADR 0159). Todo el coste se ve aquí, antes de elegir, que
/// es lo que hace legítimo que una opción cobre oro, deje a alguien lesionado, le quite un rasgo o un
/// nivel, o se lleve a alguien por delante (RF-012d).
/// </summary>
public static class EventView
{
    private const string Section = "eventEffects";

    public static EventScreenView? Build(
        RunState state, Catalog catalog, EventCatalog events, ItemCatalog items, ConsumableCatalog consumables, string language)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(consumables);
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
        var referee = EventSystem.ReferenceReferee(state, node);
        var allTargets = Targets(state);
        var options = new List<EventOptionRow>(card.Options.Count);
        for (int i = 0; i < card.Options.Count; i++)
        {
            var option = card.Options[i];
            var primary = option.NeedsTarget ? Filter(state, allTargets, option, catalog, forSecondTarget: false) : Array.Empty<EventTargetRow>();
            var second = option.NeedsSecondTarget ? Filter(state, allTargets, option, catalog, forSecondTarget: true) : Array.Empty<EventTargetRow>();
            bool viable = state.Gold + GoldDelta(option, state) >= 0
                && (!option.NeedsTarget || primary.Count > 0)
                && (!option.NeedsSecondTarget || second.Count > 0)
                && EffectsResolvable(state, node, option, items, consumables);

            options.Add(new EventOptionRow(
                i,
                Text(option.Name, language),
                Effect(option, templates, referee, language),
                option.NeedsTarget,
                option.NeedsSecondTarget,
                viable,
                primary,
                second));
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

    /// <summary>
    /// Los efectos de la ADR 0159 que no dependen de un jugador señalado pero sí de que exista algo que
    /// entregar: un objeto de esa rareza, un consumible de esa familia, o hueco de plantilla para el
    /// canterano. Es la mitad de la viabilidad que <see cref="Filter"/> no cubre.
    /// </summary>
    private static bool EffectsResolvable(
        RunState state, MapNode node, EventOption option, ItemCatalog items, ConsumableCatalog consumables)
    {
        for (int i = 0; i < option.Effects.Count; i++)
        {
            var effect = option.Effects[i];
            switch (effect.Kind)
            {
                case EventEffectKind.GrantItem when EventSystem.BestItem(state, node, effect.Rarity, items) is null:
                    return false;
                case EventEffectKind.GrantConsumable when EventSystem.BestConsumable(effect.Family, consumables) is null:
                    return false;
                case EventEffectKind.Recruit when !state.HasRosterSpace:
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Candidatos de la plantilla disponible que esa opción puede señalar como primer (o segundo, ADR
    /// 0159) objetivo: quien ya tiene el rasgo que <c>grantTrait</c> daría, o quien no tiene el rasgo que
    /// <c>removeTrait</c> quitaría, o quien no tiene perks que <c>sacrifice</c> pudiera llevarse, queda
    /// fuera de la lista -exactamente lo que <c>EventSystem.Choose</c> rechazaría, pero visto ANTES de
    /// pulsar (RF-012d), no al intentarlo.
    /// </summary>
    private static IReadOnlyList<EventTargetRow> Filter(
        RunState state, IReadOnlyList<EventTargetRow> all, EventOption option, Catalog catalog, bool forSecondTarget)
    {
        var rows = new List<EventTargetRow>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            var player = state.GetPlayer(all[i].PlayerId);
            if (Eligible(player, option, catalog, forSecondTarget))
            {
                rows.Add(all[i]);
            }
        }

        return rows;
    }

    private static bool Eligible(RunPlayer player, EventOption option, Catalog catalog, bool forSecondTarget)
    {
        bool isSacrificeRecipient = forSecondTarget && HasKind(option.Effects, EventEffectKind.Sacrifice);
        if (isSacrificeRecipient && player.Perks.Count >= Progression.Progression.PerkSlots(player.Rarity))
        {
            return false;
        }

        for (int i = 0; i < option.Effects.Count; i++)
        {
            var effect = option.Effects[i];
            if (effect.UsesSecondTarget != forSecondTarget)
            {
                continue;
            }

            switch (effect.Kind)
            {
                case EventEffectKind.GrantTrait:
                    if (player.Traits.Contains(effect.Trait) || player.Traits.Count >= RunRules.MaxTraits)
                    {
                        return false;
                    }

                    break;
                case EventEffectKind.RemoveTrait:
                    if (!player.Traits.Contains(effect.Trait))
                    {
                        return false;
                    }

                    break;
                case EventEffectKind.Sacrifice:
                    // El primer señalado es quien muere: solo tiene sentido sobre alguien con algo que dar.
                    if (!forSecondTarget && player.Perks.Count == 0)
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    private static bool HasKind(IReadOnlyList<EventEffect> effects, EventEffectKind kind)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static string Effect(EventOption option, DescriptionTemplates templates, RunReferee? referee, string language)
    {
        if (option.Effects.Count == 0)
        {
            return templates.Find(Section, "nothing") ?? string.Empty;
        }

        var parts = new List<string>(option.Effects.Count);
        for (int i = 0; i < option.Effects.Count; i++)
        {
            parts.Add(EffectPart(option.Effects[i], templates, referee, language));
        }

        return string.Join(" · ", parts);
    }

    private static string EffectPart(EventEffect effect, DescriptionTemplates templates, RunReferee? referee, string language)
    {
        string key = effect.Kind switch
        {
            EventEffectKind.Gold => "gold",
            EventEffectKind.GoldShare => "goldShare",
            EventEffectKind.Heal => "heal",
            EventEffectKind.Experience => "experience",
            EventEffectKind.ExperienceTarget => "experienceTarget",
            EventEffectKind.Injure => effect.Value >= 2 ? "injureSevere" : "injureMinor",
            EventEffectKind.GrantItem => "grantItem" + Suffix(effect.Rarity),
            EventEffectKind.GrantConsumable => "grantConsumable" + Suffix(effect.Family),
            EventEffectKind.GrantTrait => "grantTrait",
            EventEffectKind.RemoveTrait => "removeTrait",
            EventEffectKind.Attribute => "attribute" + Suffix(effect.Attribute),
            EventEffectKind.Level => "level",
            EventEffectKind.RefereeGrudge => "refereeGrudge",
            EventEffectKind.Recruit => "recruit",
            EventEffectKind.Sacrifice => "sacrifice",
            _ => "unknown",
        };

        // Un efecto que "usa el segundo objetivo" (ADR 0159) lo dice: la variante *Second, si la
        // plantilla la define, deja claro que le pasa a OTRO jugador y no al premiado. Sin variante,
        // cae en la de siempre (por ejemplo sacrifice, que ya lo dice por su propia clave).
        string template = (effect.UsesSecondTarget ? templates.Find(Section, key + "Second") : null)
            ?? templates.Find(Section, key)
            ?? key;

        return effect.Kind switch
        {
            EventEffectKind.Gold or EventEffectKind.GoldShare => Format(template, Signed(effect.Value)),
            EventEffectKind.Experience or EventEffectKind.ExperienceTarget => Format(template, "+" + effect.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            EventEffectKind.Attribute => Format(template, Signed(effect.Value)),
            EventEffectKind.Level => Format(template, effect.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            EventEffectKind.RefereeGrudge => Format(template, Signed(effect.Value), RefereeName(referee, language)),
            EventEffectKind.GrantTrait or EventEffectKind.RemoveTrait => Format(template, TraitName(templates, effect.Trait)),
            _ => template,
        };
    }

    private static string RefereeName(RunReferee? referee, string language) =>
        referee?.Name ?? string.Empty;

    private static string TraitName(DescriptionTemplates templates, Trait trait) =>
        templates.Find("tags", trait.ToString()) ?? trait.ToString();

    private static string Suffix(Rarity rarity) => rarity switch
    {
        Rarity.Common => "Common",
        Rarity.Uncommon => "Uncommon",
        Rarity.Rare => "Rare",
        Rarity.Legendary => "Legendary",
        _ => string.Empty,
    };

    private static string Suffix(ConsumableFamily family) => family switch
    {
        ConsumableFamily.Medical => "Medical",
        ConsumableFamily.Tactical => "Tactical",
        ConsumableFamily.Dirty => "Dirty",
        ConsumableFamily.Supernatural => "Supernatural",
        _ => string.Empty,
    };

    private static string Suffix(AttributeKind attribute) => attribute switch
    {
        AttributeKind.Strength => "Strength",
        AttributeKind.Speed => "Speed",
        AttributeKind.Technique => "Technique",
        AttributeKind.Stamina => "Stamina",
        AttributeKind.Leash => "Leash",
        _ => string.Empty,
    };

    private static string Text(LocalizedName name, string language) =>
        string.Equals(language, "en", StringComparison.Ordinal) ? name.En : name.Es;

    private static string Signed(int value) =>
        (value >= 0 ? "+" : string.Empty) + value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Formatea con hasta tantos huecos posicionales como valores se den (<c>{0}</c>, <c>{1}</c>...).
    /// Sigue siendo RT-035: la plantilla vive en <c>data/l10n</c>, esto solo rellena huecos.
    /// </summary>
    private static string Format(string template, params string[] values)
    {
        string text = template;
        for (int i = 0; i < values.Length; i++)
        {
            text = text.Replace("{" + i + "}", values[i], StringComparison.Ordinal);
        }

        return text;
    }

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
