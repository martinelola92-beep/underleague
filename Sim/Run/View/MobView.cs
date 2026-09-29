using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Mobs;

namespace Underleague.Sim.Run.View;

/// <summary>
/// El tipo de turba de un partido, listo para enseñar (ADR 0167): su nombre y lo que hace, con el texto generado
/// desde sus efectos (RT-035). Se anuncia el tipo, nunca la víctima.
/// </summary>
/// <param name="Id">Id de datos del tipo.</param>
/// <param name="Name">Nombre en el idioma pedido («Salta uno»).</param>
/// <param name="Text">Lo que hace, en el idioma pedido.</param>
public sealed record MobLine(string Id, string Name, string Text);

/// <summary>Traduce el tipo de turba de un nodo a lo que enseñan el ojeo, el mapa y el pregón (ADR 0167).</summary>
public static class MobView
{
    private const string Effects = "effects";

    /// <summary>El tipo de turba del partido de ese nodo, o null si no es un partido o no hay catálogo.</summary>
    public static MobLine? For(RunState state, MapNode node, MobCatalog mobs, Catalog catalog, string language = "es")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(mobs);
        return mobs.For(state.Seed, node) is { } type ? Describe(type, catalog, language) : null;
    }

    /// <summary>Nombre y texto de un tipo de turba.</summary>
    public static MobLine Describe(MobType type, Catalog catalog, string language = "es")
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(catalog);
        var templates = catalog.Localization.Get(language);
        var parts = new List<string>(type.Effects.Count);
        for (int i = 0; i < type.Effects.Count; i++)
        {
            parts.Add(templates.Get(Effects, type.Effects[i] switch
            {
                MobEffectKind.Injure => "mobInjure",
                MobEffectKind.PressBoth => "mobPressBoth",
                _ => "mobTheirOffensive",
            }));
        }

        string text = parts.Count == 0 ? templates.Get(Effects, "mobPlain") : string.Join("; ", parts);
        return new MobLine(type.Id, string.Equals(language, "en", StringComparison.Ordinal) ? type.Name.En : type.Name.Es, text);
    }
}
