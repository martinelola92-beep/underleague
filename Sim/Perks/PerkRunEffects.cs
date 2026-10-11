namespace Underleague.Sim.Perks;

/// <summary>
/// Lo que un perk hace <b>a la run</b> y no al partido, ya con sus cifras, para describirlo (RT-035). Hay perks cuyo único
/// efecto en <c>/data/perks</c> es <c>addCounter</c>: el contador deja constancia, y lo que el jugador gana lo fija una
/// tabla de <c>data/economy</c> (oro por contador o por muerte, ADR 0113; reventa; herencia). Sin esto la descripción sólo
/// podía decir «+1 al contador boxOfficeMatches», un identificador interno que no dice qué se gana.
///
/// <para>Vive en <c>Sim/Perks</c> y no sabe de economía: lo arma <c>PerkRunEffectsBuilder</c> (Sim/Run) a partir de las
/// tablas, igual que <see cref="PerkCatalog"/> se le pasa al generador para traducir los maestros. Sin él (null), la
/// descripción es la de antes.</para>
/// </summary>
public sealed class PerkRunEffects
{
    private readonly Dictionary<string, PerkRunEffect> _byPerk;

    public PerkRunEffects(IReadOnlyDictionary<string, PerkRunEffect> byPerk)
    {
        ArgumentNullException.ThrowIfNull(byPerk);
        _byPerk = new Dictionary<string, PerkRunEffect>(byPerk, StringComparer.Ordinal);
    }

    /// <summary>Sin ningún efecto de run conocido.</summary>
    public static PerkRunEffects None { get; } = new(new Dictionary<string, PerkRunEffect>(StringComparer.Ordinal));

    public int Count => _byPerk.Count;

    /// <summary>El efecto de run del perk, o null si no tiene.</summary>
    public PerkRunEffect? For(string perkId) => _byPerk.TryGetValue(perkId, out var effect) ? effect : null;
}

/// <summary>Un efecto de run descrito: la clave de plantilla de la sección <c>runEffects</c> y sus cifras.</summary>
/// <param name="TemplateKey"><c>counterGold</c>, <c>deathGold</c>, <c>resale</c> o <c>inheritance</c>.</param>
/// <param name="WithTrigger">
/// Si la frase lleva el disparador delante («al marcar, el club cobra…»). La reventa y la herencia no dependen del
/// momento en que el contador sube, así que se dicen solas.
/// </param>
/// <param name="Values">Marcadores de la plantilla (<c>{gold}</c>, <c>{percent}</c>, <c>{max}</c>) con su valor.</param>
public sealed record PerkRunEffect(string TemplateKey, bool WithTrigger, IReadOnlyList<KeyValuePair<string, int>> Values);
