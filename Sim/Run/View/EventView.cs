using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Progression;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Events;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Un candidato al que señalar cuando la opción pide un cuerpo (ADR 0100). <paramref name="Detail"/> lleva la
/// posición y el nivel <b>ya localizados</b> (y, en el sacrificio, el perk que pasa). Un segundo señalado del
/// sacrificio depende de quién muera (cada perk tiene sus herederos posibles), así que su fila lleva
/// <paramref name="ForFirstPlayerId"/>: la pantalla solo la enseña con ese primer señalado; -1 = vale con cualquiera.
/// </summary>
public sealed record EventTargetRow(int PlayerId, string Name, string Detail, int ForFirstPlayerId = -1);

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
    IReadOnlyList<EventTargetRow> SecondTargets,
    bool NoConsumableSlot = false);

/// <summary>
/// La carta del nodo de evento abierto. <paramref name="Resolved"/>: la carta ya se eligió (se elige una vez,
/// <c>RunState.NodeResolvedCounter</c>) y solo queda salir; la pantalla no debe ofrecer las opciones.
/// </summary>
public sealed record EventScreenView(
    int NodeId,
    int Act,
    int Gold,
    string Title,
    string Description,
    IReadOnlyList<EventOptionRow> Options,
    bool Resolved = false);

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
        var options = new List<EventOptionRow>(card.Options.Count);
        for (int i = 0; i < card.Options.Count; i++)
        {
            var option = card.Options[i];
            bool sacrifice = option.Effects.Any(e => e.Kind == EventEffectKind.Sacrifice);
            var primary = option.NeedsTarget
                ? Rows(state, catalog, templates, language, EventSystem.EligibleTargets(state, catalog, option, forSecondTarget: false), sacrificeVictim: sacrifice)
                : Array.Empty<EventTargetRow>();
            var second = option.NeedsSecondTarget
                ? SecondRows(state, catalog, templates, language, option, primary, sacrifice)
                : Array.Empty<EventTargetRow>();
            bool viable = !NodeGuards.IsResolved(state, node)
                && EventSystem.IsViable(state, catalog, node, option, items, consumables);

            options.Add(new EventOptionRow(
                i,
                Text(option.Name, language),
                Effect(state, node, option, templates, referee, items, consumables, language),
                option.NeedsTarget,
                option.NeedsSecondTarget,
                viable,
                primary,
                second,
                NoConsumableSlot: !viable && EventSystem.LacksConsumableSlot(state, option)));
        }

        return new EventScreenView(
            node.Id,
            node.Act,
            state.Gold,
            Text(card.Name, language),
            Text(card.Description, language),
            options,
            NodeGuards.IsResolved(state, node));
    }

    /// <summary>Lo que esa opción suma o resta al oro (<see cref="EventSystem.GoldDelta"/>).</summary>
    public static int GoldDelta(EventOption option, RunState state) => EventSystem.GoldDelta(option, state);

    /// <summary>
    /// Filas del segundo objetivo. Para el sacrificio hay una fila <b>por cada primer señalado posible</b>
    /// (los herederos dependen del perk que pasa, que depende de quién muera) y dice qué perk hereda; para el
    /// resto, una lista única sin ese primero (la pantalla también lo excluye).
    /// </summary>
    private static IReadOnlyList<EventTargetRow> SecondRows(
        RunState state, Data.Catalog catalog, DescriptionTemplates templates, string language,
        EventOption option, IReadOnlyList<EventTargetRow> firsts, bool sacrifice)
    {
        if (!sacrifice)
        {
            return Rows(state, catalog, templates, language, EventSystem.EligibleTargets(state, catalog, option, forSecondTarget: true), sacrificeVictim: false);
        }

        var rows = new List<EventTargetRow>();
        for (int i = 0; i < firsts.Count; i++)
        {
            var victim = state.GetPlayer(firsts[i].PlayerId);
            string? perkId = EventSystem.SacrificePerk(state, catalog, victim);
            string perkName = perkId is null ? string.Empty : PerkName(catalog, perkId, language);
            var heirs = EventSystem.EligibleTargets(state, catalog, option, forSecondTarget: true, firstTargetId: victim.Id);
            for (int h = 0; h < heirs.Count; h++)
            {
                string inherits = Format(templates.Find(Section, "sacrificeInherits") ?? " {0}", perkName);
                rows.Add(new EventTargetRow(heirs[h].Id, heirs[h].Name, Detail(heirs[h], templates) + inherits, victim.Id));
            }
        }

        return rows;
    }

    private static IReadOnlyList<EventTargetRow> Rows(
        RunState state, Data.Catalog catalog, DescriptionTemplates templates, string language,
        IReadOnlyList<RunPlayer> players, bool sacrificeVictim)
    {
        var rows = new List<EventTargetRow>(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            string detail = Detail(players[i], templates);
            if (sacrificeVictim && EventSystem.SacrificePerk(state, catalog, players[i]) is { } perkId)
            {
                detail += Format(templates.Find(Section, "sacrificePasses") ?? " {0}", PerkName(catalog, perkId, language));
            }

            rows.Add(new EventTargetRow(players[i].Id, players[i].Name, detail));
        }

        return rows;
    }

    private static string PerkName(Data.Catalog catalog, string perkId, string language) =>
        catalog.Perks.Find(perkId) is { } perk ? Text(perk.Name, language) : perkId;

    /// <summary>«Defensa · nivel 2», localizado (posición y nivel desde <c>data/l10n</c>, RT-035).</summary>
    internal static string Detail(RunPlayer player, DescriptionTemplates templates) => Format(
        templates.Find(Section, "targetDetail") ?? "{0} · {1}",
        templates.Find("positions", player.Position.ToString()) ?? player.Position.ToString(),
        player.Level.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string Effect(
        RunState state, MapNode node, EventOption option, DescriptionTemplates templates, RunReferee? referee,
        ItemCatalog items, ConsumableCatalog consumables, string language)
    {
        if (option.Effects.Count == 0)
        {
            return templates.Find(Section, "nothing") ?? string.Empty;
        }

        var parts = new List<string>(option.Effects.Count);
        for (int i = 0; i < option.Effects.Count; i++)
        {
            parts.Add(EffectPart(state, node, i, option.Effects[i], templates, referee, items, consumables, language));
        }

        return string.Join(" · ", parts);
    }

    private static string EffectPart(
        RunState state, MapNode node, int index, EventEffect effect, DescriptionTemplates templates, RunReferee? referee,
        ItemCatalog items, ConsumableCatalog consumables, string language)
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
            // El objeto o consumible CONCRETO que daría (sorteado con el flujo de la carta, ADR 0159): se
            // nombra antes de elegir (RF-012d). Sin candidato, la opción sale deshabilitada y el nombre queda vacío.
            EventEffectKind.GrantItem => Format(template, EventSystem.ItemFor(state, node, index, effect.Rarity, items) is { } item ? Text(item.Name, language) : string.Empty),
            EventEffectKind.GrantConsumable => Format(template, EventSystem.ConsumableFor(state, node, index, effect.Family, consumables) is { } consumable ? Text(consumable.Name, language) : string.Empty),
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
}
