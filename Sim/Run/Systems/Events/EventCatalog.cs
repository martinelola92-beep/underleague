using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Events;

/// <summary>Lo que hace una opción de evento (ADR 0100). Sin tiradas: la apuesta es elegir, no el dado.</summary>
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
}

/// <summary>Un efecto de una opción.</summary>
public sealed record EventEffect(EventEffectKind Kind, int Value);

/// <summary>
/// Una opción de la carta. <see cref="NeedsTarget"/> dice que el jugador elige a quién le toca: es lo que
/// separa «te pasa algo» de «decides a quién se lo haces», y lo que hace que la familia de carne por
/// ventaja sea una decisión de plantilla y no un accidente.
/// </summary>
public sealed record EventOption(string Id, LocalizedName Name, IReadOnlyList<EventEffect> Effects, bool NeedsTarget);

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
                effects.Add(new EventEffect(Kind(path, effect.Str("type")), effect.Int("value")));
            }

            options.Add(new EventOption(
                option.Str("id"),
                LocalizedNameJson.Read(option.Prop("name")),
                effects,
                option.OptionalBool("needsTarget", false)));
        }

        return new EventCard(
            root.Str("id"),
            LocalizedNameJson.Read(root.Prop("name")),
            LocalizedNameJson.Read(root.Prop("description")),
            root.Int("minAct"),
            root.Int("weight"),
            options);
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
        var other => throw new DataException(path, "$.options[].effects[].type", $"efecto de evento desconocido: '{other}'"),
    };
}
