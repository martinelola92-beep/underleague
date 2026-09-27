using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Consumables;

namespace Underleague.Sim.Run.Systems.Events;

/// <summary>
/// Lo que hace una opción de evento (ADR 0100, ampliado por la <b>ADR 0159</b>). Sin tiradas: la apuesta
/// es elegir, no el dado -por eso los ocho efectos nuevos que eligen "cuál" (qué objeto, qué consumible)
/// lo hacen de forma <b>determinista</b> (el de menor id que cumple la rareza o la familia pedida), nunca
/// con un sorteo dentro de la opción; el único efecto que necesita RNG de verdad es <see cref="Recruit"/>,
/// porque un canterano es, como cualquier jugador generado, un nombre y unos atributos sorteados -la MISMA
/// generación procedural que usa el mercado, no una tirada que decida si la opción vale la pena.
/// </summary>
public enum EventEffectKind
{
    /// <summary>Oro fijo, con signo.</summary>
    Gold,

    /// <summary>Porcentaje del oro que se lleva encima, con signo. Es la familia que cobra por atesorar.</summary>
    GoldShare,

    /// <summary>Cura todas las lesiones de la plantilla, como la tarifa plana de la clínica.</summary>
    Heal,

    /// <summary>Experiencia para los titulares del once.</summary>
    Experience,

    /// <summary>Experiencia solo para el jugador señalado.</summary>
    ExperienceTarget,

    /// <summary>Lesiona al jugador señalado: 1 leve, 2 grave.</summary>
    Injure,

    /// <summary>Un objeto de la <see cref="EventEffect.Rarity"/> dada, al almacén (ADR 0159).</summary>
    GrantItem,

    /// <summary>Un consumible de la familia <see cref="EventEffect.Family"/>, al inventario (ADR 0159).</summary>
    GrantConsumable,

    /// <summary>Añade <see cref="EventEffect.Trait"/> al jugador señalado (ADR 0159, RF-022c: máximo 3).</summary>
    GrantTrait,

    /// <summary>Quita <see cref="EventEffect.Trait"/> al jugador señalado (ADR 0159).</summary>
    RemoveTrait,

    /// <summary>±<c>Value</c> a <see cref="EventEffect.Attribute"/> del señalado, permanente (ADR 0159).</summary>
    Attribute,

    /// <summary><c>Value</c> niveles menos al señalado, con su pérdida de atributos (ADR 0159).</summary>
    Level,

    /// <summary>±<c>Value</c> a la memoria del árbitro derivado de la carta (ADR 0159, ADR 0158).</summary>
    RefereeGrudge,

    /// <summary>Un canterano gratis, con hueco en la plantilla (ADR 0159).</summary>
    Recruit,

    /// <summary>
    /// El señalado muere; su mejor perk pasa al segundo señalado (ADR 0159, enmienda de RF-072). Usa
    /// <see cref="EventOption.NeedsSecondTarget"/>: el primer objetivo es quien muere, el segundo quien
    /// hereda.
    /// </summary>
    Sacrifice,
}

/// <summary>
/// Un efecto de una opción. Los campos que no aplican a <see cref="Kind"/> quedan en su valor por
/// defecto -mismo convenio que <c>Sim.Perks.EffectDefinition</c> (Regla G: es la forma que el propio
/// repositorio ya usa para "un registro, muchos tipos"), en vez de inventar un segundo formato para
/// eventos.
/// </summary>
/// <param name="UsesSecondTarget">
/// True si este efecto concreto se aplica al <b>segundo</b> objetivo de la opción
/// (<see cref="EventOption.NeedsSecondTarget"/>) en vez de al primero. Es lo que permite que una sola
/// opción hiera a un espectador distinto del premiado ("Pelea en el vestuario"), sin que
/// <see cref="EventEffectKind.Sacrifice"/> -que ya usa los dos objetivos a su manera- tenga que
/// declararlo.
/// </param>
public sealed record EventEffect(
    EventEffectKind Kind,
    int Value,
    bool UsesSecondTarget = false,
    Rarity Rarity = Rarity.Common,
    ConsumableFamily Family = ConsumableFamily.Medical,
    AttributeKind Attribute = AttributeKind.Strength,
    Trait Trait = Trait.Aggressive);

/// <summary>
/// Una opción de la carta. <see cref="NeedsTarget"/> dice que el jugador elige a quién le toca: es lo que
/// separa «te pasa algo» de «decides a quién se lo haces», y lo que hace que la familia de carne por
/// ventaja sea una decisión de plantilla y no un accidente. <see cref="NeedsSecondTarget"/> (ADR 0159) es
/// lo mismo un peldaño más allá: un SEGUNDO cuerpo, distinto del primero.
/// </summary>
public sealed record EventOption(
    string Id,
    LocalizedName Name,
    IReadOnlyList<EventEffect> Effects,
    bool NeedsTarget,
    bool NeedsSecondTarget = false);

/// <summary>Carta de evento (ADR 0100): lo que se sortea al entrar en un nodo de evento.</summary>
public sealed record EventCard(
    string Id,
    LocalizedName Name,
    LocalizedName Description,
    int MinAct,
    int Weight,
    IReadOnlyList<EventOption> Options);

/// <summary>Catálogo de cartas de evento, ordenado por id (RT-041: el orden nunca depende de un diccionario).</summary>
public sealed class EventCatalog
{
    private readonly IReadOnlyList<EventCard> _cards;

    public EventCatalog(IReadOnlyList<EventCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        _cards = cards.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<EventCard> All => _cards;

    public EventCard? Find(string id)
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (string.Equals(_cards[i].Id, id, StringComparison.Ordinal))
            {
                return _cards[i];
            }
        }

        return null;
    }

    /// <summary>Las cartas que pueden salir en ese acto (<c>minAct</c>), en orden de id.</summary>
    public IReadOnlyList<EventCard> ForAct(int act)
    {
        var cards = new List<EventCard>(_cards.Count);
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].MinAct <= act)
            {
                cards.Add(_cards[i]);
            }
        }

        return cards;
    }
}

/// <summary>Carga <c>data/events/*.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class EventLoader
{
    public static EventCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var cards = new List<EventCard>();
        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!path.StartsWith("events/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            cards.Add(Parse(path, files[path]));
        }

        if (cards.Count == 0)
        {
            throw new DataException("events/", "$", "no se ha encontrado ninguna carta de evento en data/events/");
        }

        return new EventCatalog(cards);
    }

    private static EventCard Parse(string path, string content)
    {
        using var document = ParseJson(path, content);
        var root = Json.Root(path, document);
        var options = new List<EventOption>();
        foreach (var option in root.Prop("options").EnumerateArray())
        {
            var effects = new List<EventEffect>();
            foreach (var effect in option.Prop("effects").EnumerateArray())
            {
                effects.Add(ParseEffect(path, effect));
            }

            bool needsSecondTarget = option.OptionalBool("needsSecondTarget", false)
                || HasKind(effects, EventEffectKind.Sacrifice);
            options.Add(new EventOption(
                option.Str("id"),
                LocalizedNameJson.Read(option.Prop("name")),
                effects,
                option.OptionalBool("needsTarget", false) || needsSecondTarget,
                needsSecondTarget));
        }

        return new EventCard(
            root.Str("id"),
            LocalizedNameJson.Read(root.Prop("name")),
            LocalizedNameJson.Read(root.Prop("description")),
            root.Int("minAct"),
            root.Int("weight"),
            options);
    }

    private static bool HasKind(List<EventEffect> effects, EventEffectKind kind)
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

    private static EventEffect ParseEffect(string path, Json effect)
    {
        var kind = Kind(path, effect.Str("type"));
        return new EventEffect(
            kind,
            effect.Int("value"),
            effect.OptionalBool("usesSecondTarget", false),
            ParseRarity(path, effect),
            ParseFamily(path, effect),
            ParseAttribute(path, effect),
            ParseTrait(path, effect));
    }

    private static Rarity ParseRarity(string path, Json effect)
    {
        var value = effect.TryProp("rarity");
        if (value is null)
        {
            return Rarity.Common;
        }

        return value.Value.AsString() switch
        {
            "common" => Rarity.Common,
            "uncommon" => Rarity.Uncommon,
            "rare" => Rarity.Rare,
            "legendary" => Rarity.Legendary,
            var other => throw new DataException(path, value.Value.Path, $"rareza de efecto desconocida: '{other}'"),
        };
    }

    private static ConsumableFamily ParseFamily(string path, Json effect)
    {
        var value = effect.TryProp("family");
        if (value is null)
        {
            return ConsumableFamily.Medical;
        }

        return value.Value.AsString() switch
        {
            "medical" => ConsumableFamily.Medical,
            "tactical" => ConsumableFamily.Tactical,
            "dirty" => ConsumableFamily.Dirty,
            "supernatural" => ConsumableFamily.Supernatural,
            var other => throw new DataException(path, value.Value.Path, $"familia de consumible desconocida: '{other}'"),
        };
    }

    private static AttributeKind ParseAttribute(string path, Json effect)
    {
        var value = effect.TryProp("attribute");
        if (value is null)
        {
            return AttributeKind.Strength;
        }

        return value.Value.AsString() switch
        {
            "strength" => AttributeKind.Strength,
            "speed" => AttributeKind.Speed,
            "technique" => AttributeKind.Technique,
            "stamina" => AttributeKind.Stamina,
            var other => throw new DataException(
                path, value.Value.Path, $"atributo de efecto desconocido o no permitido (sin correa): '{other}'"),
        };
    }

    private static Trait ParseTrait(string path, Json effect)
    {
        var value = effect.TryProp("trait");
        if (value is null)
        {
            return Trait.Aggressive;
        }

        return value.Value.AsString() switch
        {
            "Aggressive" => Trait.Aggressive,
            "Fast" => Trait.Fast,
            "Scorer" => Trait.Scorer,
            "LongShot" => Trait.LongShot,
            "Cerebral" => Trait.Cerebral,
            "Dirty" => Trait.Dirty,
            "Resilient" => Trait.Resilient,
            "Coward" => Trait.Coward,
            "Leader" => Trait.Leader,
            "Lazy" => Trait.Lazy,
            "Cat" => Trait.Cat,
            "Wall" => Trait.Wall,
            "Rusher" => Trait.Rusher,
            var other => throw new DataException(path, value.Value.Path, $"rasgo de efecto desconocido: '{other}'"),
        };
    }

    private static JsonDocument ParseJson(string path, string content)
    {
        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(path, "$", $"JSON inválido: {ex.Message}");
        }
    }

    private static EventEffectKind Kind(string path, string text) => text switch
    {
        "gold" => EventEffectKind.Gold,
        "goldShare" => EventEffectKind.GoldShare,
        "heal" => EventEffectKind.Heal,
        "experience" => EventEffectKind.Experience,
        "experienceTarget" => EventEffectKind.ExperienceTarget,
        "injure" => EventEffectKind.Injure,
        "grantItem" => EventEffectKind.GrantItem,
        "grantConsumable" => EventEffectKind.GrantConsumable,
        "grantTrait" => EventEffectKind.GrantTrait,
        "removeTrait" => EventEffectKind.RemoveTrait,
        "attribute" => EventEffectKind.Attribute,
        "level" => EventEffectKind.Level,
        "refereeGrudge" => EventEffectKind.RefereeGrudge,
        "recruit" => EventEffectKind.Recruit,
        "sacrifice" => EventEffectKind.Sacrifice,
        var other => throw new DataException(path, "$.options[].effects[].type", $"efecto de evento desconocido: '{other}'"),
    };
}
