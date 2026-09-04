using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Escala del equipamiento (ADR 0036) y tabla de valor marginal por atributo (ADR 0038), cargadas de
/// <c>data/equipment/equipment.json</c>. Es la pieza de infraestructura que las dos ADR necesitan:
///
/// <list type="bullet">
/// <item>la <b>magnitud</b> de un objeto y <b>cuántos atributos</b> toca según su rareza, que es lo que
/// el cargador de objetos comprueba de un vistazo (ADR 0036);</item>
/// <item>el <b>valor marginal</b> de cada atributo, con el que el precio de un objeto se
/// <b>calcula</b> en vez de derivarse de su rareza (ADR 0038).</item>
/// </list>
///
/// <para>La tabla de valor marginal se mide, no se inventa: sale de <c>docs/balance/fase1b-resultados.md</c>
/// (puntos de tasa de victoria por cada +20 repartidos entre los diez jugadores, en milésimas para
/// mantener la aritmética entera de RT-023) y hay que <b>remedirla cuando cambie el motor</b>.</para>
/// </summary>
public sealed record ItemScale(
    int AttributeBonus,
    int CursedMultiplier,
    IReadOnlyDictionary<Rarity, int> AttributesByRarity,
    int RestrictedAttributes,
    Attributes MarginalValueMilli,
    int FragilePricePercent,
    int FragileOfferWeightPercent)
{
    /// <summary>Ruta del fichero dentro de la instantánea de <c>/data</c>.</summary>
    public const string Path = "equipment/equipment.json";

    /// <summary>Orden fijo de los atributos en objetos y descripciones (mismo que <c>PlayerGenerator</c>).</summary>
    public static readonly AttributeKind[] AttributeOrder =
    {
        AttributeKind.Strength, AttributeKind.Speed, AttributeKind.Technique, AttributeKind.Stamina, AttributeKind.Leash,
    };

    /// <summary>Cuántos atributos sube un objeto de esa rareza (ADR 0036: común 1, poco común 2, raro 3).</summary>
    public int AttributesFor(Rarity rarity) =>
        AttributesByRarity.TryGetValue(rarity, out int count)
            ? count
            : throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "rareza sin número de atributos declarado en data/equipment/equipment.json");

    /// <summary>
    /// Valor de un objeto: la suma de sus modificadores de atributo ponderada por el valor marginal de
    /// cada uno (ADR 0038). La contrapartida del maldito entra con su signo, así que un maldito que baja
    /// algo caro vale —y cuesta— menos.
    /// </summary>
    public int ValueOf(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        int value = 0;
        foreach (var kind in AttributeOrder)
        {
            value += item.Modifier.Get(kind) * MarginalValueMilli.Get(kind);
        }

        return value;
    }

    /// <summary>
    /// Valor del objeto <b>medio</b> de esa rareza: el que cuesta exactamente el precio base de su banda.
    /// Es el divisor de la fórmula de precio de la ADR 0038.
    /// </summary>
    public int ReferenceValue(Rarity rarity) => AttributesFor(rarity) * AttributeBonus * AverageMarginalValueMilli;

    /// <summary>Media de la tabla de valor marginal, en milésimas: la vara con la que se normaliza el precio.</summary>
    public int AverageMarginalValueMilli
    {
        get
        {
            int total = 0;
            foreach (var kind in AttributeOrder)
            {
                total += MarginalValueMilli.Get(kind);
            }

            return total / AttributeOrder.Length;
        }
    }

    /// <summary>Carga la escala de la instantánea de <c>/data</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
    public static ItemScale FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!files.TryGetValue(Path, out var content))
        {
            throw new DataException(Path, "$", "fichero requerido ausente: la escala del equipamiento (ADR 0036) vive en datos, no en código");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(Path, "$", $"JSON inválido: {ex.Message}");
        }

        using (document)
        {
            var root = Json.Root(Path, document);
            int bonus = root.Int("attributeBonus");
            int cursed = root.Int("cursedMultiplier");

            var byRarity = new Dictionary<Rarity, int>();
            var node = root.Prop("attributesByRarity");
            foreach (var (key, rarity) in RarityKeys)
            {
                byRarity[rarity] = node.Prop(key).AsInt();
            }

            return new ItemScale(
                bonus,
                cursed,
                byRarity,
                root.Int("restrictedAttributes"),
                ReadAttributes(root.Prop("marginalValuePerAttribute")),
                root.Int("fragilePricePercent"),
                root.Int("fragileOfferWeightPercent"));
        }
    }

    private static readonly (string Key, Rarity Rarity)[] RarityKeys =
    {
        ("common", Rarity.Common), ("uncommon", Rarity.Uncommon), ("rare", Rarity.Rare), ("legendary", Rarity.Legendary),
    };

    private static Attributes ReadAttributes(Json node) => new(
        node.Prop("strength").AsInt(),
        node.Prop("speed").AsInt(),
        node.Prop("technique").AsInt(),
        node.Prop("stamina").AsInt(),
        node.Prop("leash").AsInt());
}
