using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Arquetipo de un objeto de equipamiento (RF-077, ADR 0036). Lo que el arquetipo cambia es la
/// <b>magnitud</b> o el <b>riesgo</b>, nunca cuántos atributos toca: eso lo fija la rareza.
/// </summary>
public enum ItemArchetype
{
    /// <summary>Sube los atributos de su rareza con la magnitud de partida, sin contrapartida.</summary>
    Normal,

    /// <summary>
    /// Maldito: sube <b>el doble</b> en cada uno de los atributos de su rareza y baja <b>el doble</b> en
    /// exactamente uno más, elegido por el diseñador (ADR 0036). Qué baja es lo que lo convierte en una
    /// decisión de colocación en vez de en un bono.
    /// </summary>
    Cursed,

    /// <summary>
    /// Frágil: la magnitud de su rareza y, además, una <b>probabilidad de rotura que se resuelve al
    /// terminar cada partido</b> (ADR 0036). Se compensa en el precio, no en los números.
    /// </summary>
    Fragile,

    /// <summary>
    /// Restringido: objeto <b>exclusivo de raza</b> (ADR 0036, equivalente en objetos de los perks
    /// exclusivos de la ADR 0023). No tiene rareza en el dato, sube tres atributos con la magnitud
    /// normal, solo entra en el pool de una run de esa raza y solo funciona sobre un portador de esa
    /// raza (<see cref="ItemDefinition.RequiredTag"/>).
    /// </summary>
    Restricted,
}

/// <summary>
/// Clase de reliquia (ADR 0161 §2): el objeto que un jugador propio deja en el almacén al morir, elegido
/// según lo que destacó en su <see cref="RunCareer"/> —goleador, carnicero, muro— o genérico si no
/// destacó en nada. Marca de datos (<c>relic</c> en <c>data/items/&lt;id&gt;.json</c>), no un arquetipo:
/// una reliquia sigue siendo <see cref="ItemArchetype.Normal"/> en todo lo demás, y lo único que cambia es
/// que no entra en el pool de mercado ni de recompensas (<see cref="ItemCatalog.OfferableTo(Race, int)"/>)
/// y que <see cref="RelicSystem"/> la busca por esta clase.
/// </summary>
public enum RelicKind
{
    /// <summary>Reliquia del goleador (RunCareer.Goals).</summary>
    Scorer,

    /// <summary>Reliquia del carnicero (RunCareer.InjuriesCaused).</summary>
    Butcher,

    /// <summary>Reliquia del muro (RunCareer.TacklesWon).</summary>
    Wall,

    /// <summary>Reliquia genérica: el muerto no destacó en ninguna de las tres anteriores.</summary>
    Generic,
}

/// <summary>
/// Un objeto de equipamiento (RF-075..078), cargado de <c>data/items/&lt;id&gt;.json</c>.
///
/// <para><b>Un objeto sube atributos y nada más</b> (ADR 0036). No tiene disparador, ni condición, ni
/// canal de probabilidad, ni excepciones por rareza: eso es lo que hace un perk. El cargador
/// <b>rechaza</b> cualquier <c>effects</c> en un objeto, que era el formato anterior.</para>
///
/// <para><see cref="Modifier"/> lleva el paquete completo en un solo <see cref="Attributes"/>: las
/// entradas <b>positivas</b> son lo que sube (tantas como exige la rareza) y la única entrada
/// <b>negativa</b>, si la hay, es la contrapartida del maldito. Cero = el objeto no toca ese atributo.
/// La escala (magnitud, multiplicador del maldito y cuántos atributos por rareza) vive en
/// <c>data/equipment/equipment.json</c>, no en código.</para>
/// </summary>
/// <param name="Rarity">
/// Rareza del objeto. Un restringido no la declara en el dato —"no tiene rareza" (ADR 0036)— y el
/// cargador le asigna <see cref="Model.Rarity.Rare"/>, que es exactamente lo que vale: tres atributos
/// con magnitud normal. Se usa solo para precio y para la fracción de venta.
/// </param>
/// <param name="BreakChancePercent">Probabilidad de rotura al terminar el partido (RF-077, solo frágil); 0 en los demás.</param>
/// <param name="Race">Raza a la que pertenece un objeto restringido; null en los universales.</param>
/// <param name="RequiredTag">Etiqueta de especie que el portador debe llevar (restringido); vacía en los universales.</param>
/// <param name="MinAct">
/// Acto nativo (ADR 0051): a partir de qué acto empieza a aparecer en el pool de recompensas y de
/// mercado. Por debajo solo sale <b>fuera de profundidad</b>, con el peso pequeño de
/// <c>data/build/arcs.json</c>.
/// </param>
/// <param name="Frequency">
/// El "commonness" de Angband (ADR 0051): cuánto sale este objeto comparado con uno normal, en
/// porcentaje. Multiplica al peso por valor de la ADR 0038 y a la curva de profundidad.
/// </param>
/// <param name="Relic">
/// Marca de reliquia (ADR 0161 §2); null en un objeto normal. Un objeto marcado no entra en el pool de
/// mercado ni de recompensas (<see cref="ItemCatalog.OfferableTo(Race, int)"/>): solo llega al almacén
/// cuando muere un jugador propio, elegido por <c>RelicSystem.Classify</c>.
/// </param>
public sealed record ItemDefinition(
    string Id,
    LocalizedName Name,
    Rarity Rarity,
    ItemArchetype Archetype,
    Attributes Modifier,
    int BreakChancePercent,
    Race? Race,
    string RequiredTag,
    int MinAct = 1,
    int Frequency = 100,
    RelicKind? Relic = null)
{
    /// <summary>Atributos que el objeto sube (entradas positivas de <see cref="Modifier"/>), en orden fijo.</summary>
    public IReadOnlyList<AttributeKind> Raised => Kinds(positive: true);

    /// <summary>Atributos que el objeto baja (entradas negativas de <see cref="Modifier"/>), en orden fijo.</summary>
    public IReadOnlyList<AttributeKind> Lowered => Kinds(positive: false);

    /// <summary>True si el objeto solo aparece y solo funciona en runs de una raza (ADR 0036).</summary>
    public bool IsRaceExclusive => Archetype == ItemArchetype.Restricted;

    /// <summary>True si el objeto es una reliquia (ADR 0161 §2): fuera del mercado y de las recompensas.</summary>
    public bool IsRelic => Relic is not null;

    private IReadOnlyList<AttributeKind> Kinds(bool positive)
    {
        var result = new List<AttributeKind>(5);
        foreach (var kind in ItemScale.AttributeOrder)
        {
            int value = Modifier.Get(kind);
            if (positive ? value > 0 : value < 0)
            {
                result.Add(kind);
            }
        }

        return result;
    }
}
