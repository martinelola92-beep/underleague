using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Game.Ui.Knavall;

/// <summary>
/// Los carteles de ayuda de la pantalla de Equipo (ADR 0162), en un solo sitio: cada uno dice qué hace algo
/// <b>en el juego</b>. Todo el texto sale de <see cref="UiText"/> o del catálogo (RT-073), y las
/// descripciones de perk y de consumible del generador de <c>/Sim</c> (RT-035), que ya están escritas para el
/// jugador.
/// </summary>
public static class TeamTips
{
    private static string Inv(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Nombre de un atributo en el idioma del juego (del mismo fichero que las descripciones).</summary>
    public static string AttributeName(TeamState state, AttributeKind kind) => state.Templates.Get("attributes", kind switch
    {
        AttributeKind.Strength => "strength",
        AttributeKind.Speed => "speed",
        AttributeKind.Technique => "technique",
        AttributeKind.Stamina => "stamina",
        _ => "leash",
    });

    /// <summary>Atributo: qué hace y, si un objeto lo cambia, con qué valor se juega de verdad.</summary>
    public static Tip Attribute(TeamState state, AttributeKind kind, int baseValue, int modifier)
    {
        string key = kind switch
        {
            AttributeKind.Strength => "ui.kn.tip.strength",
            AttributeKind.Speed => "ui.kn.tip.speed",
            AttributeKind.Technique => "ui.kn.tip.technique",
            AttributeKind.Stamina => "ui.kn.tip.stamina",
            _ => "ui.kn.tip.leash",
        };

        var lines = new List<TipLine>();
        if (modifier != 0)
        {
            int effective = System.Math.Clamp(baseValue + modifier, 1, 99);
            lines.Add(new TipLine(UiText.Get("ui.kn.tip.modifier", baseValue, UiText.Signed(modifier), effective), modifier > 0 ? Ink.GreenLight : new Color("ff8a7a")));
        }

        return new Tip(AttributeName(state, kind), UiText.Get(key), InkIcons.Of(kind), lines);
    }

    public static Tip Position(TeamState state, Position position) =>
        new(state.Templates.Get("positions", position.ToString()), UiText.Get("ui.kn.tip.pos." + position), InkIcons.Of(position));

    public static Tip Starter(bool starter) => starter
        ? new Tip(UiText.Get("ui.kn.starter"), UiText.Get("ui.kn.tip.starter"), Glyph.Crown)
        : new Tip(UiText.Get("ui.kn.benchStamp"), UiText.Get("ui.kn.tip.bench"), Glyph.Bench);

    public static Tip Level(PlayerDefinition player) => new(
        UiText.Get("ui.kn.level", player.Level),
        UiText.Get(
            "ui.kn.tip.level",
            player.Attributes.Average,
            UiText.Get("ui.card.rarity." + player.Rarity),
            Sim.Progression.Progression.PerkSlots(player.Rarity)),
        Glyph.Perk);

    public static Tip State(PhysicalState state) =>
        new(UiText.Get("ui.state." + state), UiText.Get("ui.kn.tip.state." + state), InkIcons.Of(state));

    public static Tip Trait(TeamState state, Trait trait) =>
        new(state.Catalog.Trait(trait).Name.Es, UiText.Get("ui.kn.tip.trait." + trait), InkIcons.OfTrait(trait.ToString()));

    /// <summary>Perk: su descripción generada, más cuántos de la plantilla cumplen lo que pide (BB-J).</summary>
    public static Tip Perk(TeamState state, PerkDefinition perk, PlayerDefinition owner)
    {
        var lines = new List<TipLine>();
        foreach (var requirement in PerkSquadRequirements.For(perk, state.Players, owner.Id))
        {
            bool met = requirement.Current >= requirement.Required;
            lines.Add(new TipLine(
                UiText.Get("ui.card.perkRequirement", state.Templates.Get("tags", requirement.Tag), requirement.Current, requirement.Required),
                met ? Ink.GreenLight : new Color("ff8a7a")));
        }

        return new Tip(UiText.Name(perk.Name), DescriptionGenerator.Describe(perk, state.Templates), Glyph.Perk, lines);
    }

    public static Tip Racial(TeamState state, PerkDefinition perk) =>
        new(UiText.Name(perk.Name), DescriptionGenerator.Describe(perk, state.Templates) + "\n" + UiText.Get("ui.kn.tip.racial"), Glyph.Racial);

    public static Tip EmptySlot() => new(UiText.Get("ui.kn.freeSlot"), UiText.Get("ui.kn.tip.emptySlot"), Glyph.EmptySlot);

    public static Tip NoItem() => new(UiText.Get("ui.kn.noItem"), UiText.Get("ui.kn.tip.noItem"), Glyph.Boot);

    /// <summary>Objeto: sus modificadores uno por línea con su icono, y si es reliquia, de dónde viene.</summary>
    public static Tip Item(TeamState state, ItemDefinition item)
    {
        string body = UiText.Get("ui.kn.tip.itemRarity", UiText.Get("ui.card.rarity." + item.Rarity));
        if (item.IsRelic)
        {
            body += " " + UiText.Get("ui.kn.tip.relic");
        }

        return new Tip(UiText.Name(item.Name), body, InkIcons.OfItem(item.Id), Modifiers(state, item));
    }

    /// <summary>Las líneas «+20 fuerza» de un objeto, en el orden fijo de los atributos.</summary>
    public static List<TipLine> Modifiers(TeamState state, ItemDefinition item)
    {
        var lines = new List<TipLine>();
        foreach (var kind in ItemScale.AttributeOrder)
        {
            int value = item.Modifier.Get(kind);
            if (value != 0)
            {
                lines.Add(new TipLine(UiText.Signed(value) + " " + AttributeName(state, kind).ToUpperInvariant(), value > 0 ? Ink.GreenLight : new Color("ff8a7a"), InkIcons.Of(kind)));
            }
        }

        return lines;
    }

    public static Tip Consumable(TeamState state, ConsumableDefinition definition, EquippedConsumable? equipped, int owned, string triggerName)
    {
        var lines = new List<TipLine>();
        if (equipped is { Mode: ConsumableMode.Manual })
        {
            lines.Add(new TipLine(UiText.Get("ui.kn.tip.manual"), Ink.Ochre, Glyph.Manual));
        }
        else if (equipped is not null)
        {
            lines.Add(new TipLine(UiText.Get("ui.kn.tip.conditional", triggerName), Ink.Ochre, Glyph.Conditional));
        }
        else
        {
            lines.Add(new TipLine(UiText.Get("ui.kn.tip.pouch", owned), Ink.PaperDark));
        }

        return new Tip(UiText.Name(definition.Name), DescriptionGenerator.DescribeEffects(definition.Effects, state.Templates), InkIcons.Of(definition.Family), lines);
    }

    public static Tip Chest() => new(UiText.Get("ui.kn.chest"), UiText.Get("ui.kn.tip.chest"), Glyph.Chest);

    public static Tip Consumables() => new(UiText.Get("ui.kn.consumables"), UiText.Get("ui.kn.tip.consumables"), Glyph.Potion);
}
