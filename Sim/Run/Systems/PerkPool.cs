using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// De dónde sale un perk (ADR 0051, ADR 0055). No es una etiqueta descriptiva: decide qué entra en el
/// pool. Un <b>maestro solo se compra</b>, nunca se regala tras una victoria, que es la palanca con la que
/// la ADR 0055 hace que el mercado sea parte del núcleo de la build: si el objetivo de una línea solo está
/// a la venta, una build que se salta el mercado se queda a medias <b>por definición</b>, sin recortar
/// nada más.
/// </summary>
public enum PerkSource
{
    /// <summary>Elección tras ganar un partido (RF-071). No ofrece maestros.</summary>
    Reward,

    /// <summary>Surtido de un nodo de mercado (RF-114e). Es la única vía a un maestro.</summary>
    Market,
}

/// <summary>Por qué un perk no se puede cobrar ahora mismo (ADR 0051). La frase la pone la interfaz (RT-073).</summary>
public enum PerkAvailability
{
    /// <summary>Se puede cobrar.</summary>
    Available,

    /// <summary>Nadie de la plantilla puede llevarlo: sin slot libre, ya lo lleva, o no cumple sus etiquetas.</summary>
    NoCarrier,

    /// <summary>Es un maestro y la run todavía no lleva los perks de su línea que exige (ADR 0051).</summary>
    Unmet,

    /// <summary>Es un maestro y solo se compra: no se puede cobrar como recompensa (ADR 0055).</summary>
    MarketOnly,

    /// <summary>Un maestro ya aceptado cerró su línea para el resto de la run (ADR 0051).</summary>
    Closed,
}

/// <summary>
/// Pool de perks del mercado y de las recompensas (RF-071, RF-114). Reutiliza
/// <c>Sim.Generation.PerkAssignment.Eligible</c> (mismo filtro por raza -ADR 0023-, posición y etiquetas
/// que usan los perks iniciales de la plantilla) para no reinventar la regla de elegibilidad.
///
/// <para><b>Arcos de build y profundidad nativa</b> (ADR 0051). Qué entra en el pool depende además de
/// tres cosas nuevas: el <b>acto</b> —cada perk declara el suyo y por debajo solo sale fuera de
/// profundidad, con un peso pequeño—, las <b>líneas cerradas</b> por un maestro ya aceptado, y, en un
/// maestro, cuánto le falta a la run para cumplirlo. Un maestro se ofrece cuando le falta <b>como mucho
/// una pieza</b>: así el jugador ve el objetivo antes de alcanzarlo —y puede ir al mercado a por la
/// pieza que le falta, que es el papel que la ADR le devuelve— sin que el surtido se llene de opciones
/// imposibles.</para>
/// </summary>
public static class PerkPool
{
    /// <summary>
    /// Piezas que le pueden faltar a un maestro para entrar en el pool (ADR 0051). Con 1, el maestro
    /// aparece cuando le falta una: es lo que convierte "me falta la tercera pieza de la línea" en una
    /// decisión de mercado en vez de en un misterio.
    /// </summary>
    public const int MasterPreviewSlack = 1;

    /// <summary>
    /// Perks que pueden ofrecerse en esta run: los del pool de la raza del club (ADR 0023, sin la
    /// habilidad racial, que no ocupa slot) que además tienen al menos un portador posible en la
    /// plantilla actual, no están cerrados por un maestro ya aceptado y pueden aparecer en este acto.
    /// Orden de id ordinal ascendente.
    /// </summary>
    public static IReadOnlyList<PerkDefinition> Offerable(
        RunState state, Catalog catalog, int act, PerkSource source = PerkSource.Market)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);

        var closed = ClosedBy(state, catalog);
        var held = HeldPerkIds(state);
        var offerable = new List<PerkDefinition>();
        foreach (var perk in catalog.Perks.AvailableTo(state.ClubRace))
        {
            if (IsRaceAbility(perk, state, catalog) || closed.Blocks(perk))
            {
                continue;
            }

            // ADR 0055: un maestro solo se compra. La recompensa da volumen de build; el mercado da lo que
            // la termina, y por eso saltárselo cuesta la mitad de la build en vez de un poco de calidad.
            if (perk.IsMaster && source == PerkSource.Reward)
            {
                continue;
            }

            if (DepthWeightPercent(perk, catalog, act) <= 0)
            {
                continue;
            }

            // Un maestro entra cuando le falta como mucho una pieza de su línea; uno al que le faltan
            // dos no es un objetivo todavía, es ruido en el surtido.
            if (perk.Requires is { } requirement
                && FamilyCount(held, catalog, requirement.Family) + MasterPreviewSlack < requirement.Count)
            {
                continue;
            }

            if (EligibleCarriers(state, perk, catalog).Count > 0)
            {
                offerable.Add(perk);
            }
        }

        return offerable;
    }

    /// <summary>
    /// Peso del perk en el pool de ese acto: el de la ADR 0038 (inversamente proporcional a su valor
    /// medido) modulado por la profundidad nativa de la ADR 0051. Nunca baja de 1 mientras el perk pueda
    /// salir: un peso de cero lo sacaría del pool por la puerta de atrás, y eso ya lo decide
    /// <see cref="Offerable"/>.
    /// </summary>
    public static int OfferWeight(RunState state, PerkDefinition perk, Catalog catalog, int valueWeight, int act)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(catalog);
        int percent = DepthWeightPercent(perk, catalog, act);
        int weight = valueWeight * percent / 100 * perk.Frequency / 100;

        // ADR 0055: un maestro al que todavía le falta una pieza es un **anuncio**, no una compra. Pesa
        // poco a propósito: enseña el objetivo sin quitarle el sitio a las apariciones que sí cierran el
        // arco. Medido antes de este ajuste: 5,3 apariciones por run y solo 0,13 comprables.
        if (perk.Requires is { } requirement && !Meets(state, catalog, requirement))
        {
            weight = weight * catalog.Perks.Arcs.Depth.MasterPreviewPercent / 100;
        }

        return Math.Max(1, weight);
    }

    /// <summary>
    /// Peso relativo del perk en ese acto, en porcentaje (ADR 0051). Cero = no puede salir. Un
    /// <b>maestro</b> no aparece nunca por debajo de su acto nativo: la sorpresa fuera de profundidad es
    /// para el catálogo suelto, y un maestro en el acto 1 sería un objetivo que nadie puede cumplir.
    /// </summary>
    public static int DepthWeightPercent(PerkDefinition perk, Catalog catalog, int act)
    {
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(catalog);
        if (perk.IsMaster && act < perk.MinAct)
        {
            return 0;
        }

        return catalog.Perks.Arcs.Depth.WeightPercent(perk.MinAct, act);
    }

    /// <summary>
    /// Si el perk se puede cobrar ahora mismo y, si no, por qué (ADR 0051). Es la regla, no un adorno de
    /// la interfaz: la comprueban tanto la recompensa (RF-071) como el mercado (RF-114e) antes de
    /// asignar.
    /// </summary>
    public static PerkAvailability Availability(
        RunState state, PerkDefinition perk, Catalog catalog, PerkSource source = PerkSource.Market)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(catalog);

        if (ClosedBy(state, catalog).Blocks(perk))
        {
            return PerkAvailability.Closed;
        }

        if (perk.IsMaster && source == PerkSource.Reward)
        {
            return PerkAvailability.MarketOnly;
        }

        if (perk.Requires is { } requirement && !Meets(state, catalog, requirement))
        {
            return PerkAvailability.Unmet;
        }

        return EligibleCarriers(state, perk, catalog).Count > 0
            ? PerkAvailability.Available
            : PerkAvailability.NoCarrier;
    }

    /// <summary>
    /// Comprueba que el perk se puede cobrar y lanza con el motivo si no (ADR 0051). Es el guardián que
    /// comparten la recompensa (RF-071) y el mercado (RF-114e): la regla del arco se hace cumplir en un
    /// solo sitio, así que no hay forma de conseguir un maestro por una vía y no por la otra.
    /// </summary>
    public static void Require(
        RunState state, PerkDefinition perk, Catalog catalog, PerkSource source = PerkSource.Market)
    {
        var availability = Availability(state, perk, catalog, source);
        if (availability is PerkAvailability.Unmet or PerkAvailability.Closed or PerkAvailability.MarketOnly)
        {
            throw new InvalidOperationException(PerkPoolMessages.Why(perk, availability));
        }
    }

    /// <summary>True si la run ya lleva los perks de la línea que el maestro exige (ADR 0051).</summary>
    public static bool Meets(RunState state, Catalog catalog, MasterRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(requirement);
        return FamilyCount(HeldPerkIds(state), catalog, requirement.Family) >= requirement.Count;
    }

    /// <summary>
    /// Perks <b>distintos</b> de esa línea que la plantilla lleva ahora mismo (ADR 0051). Se cuentan ids,
    /// no portadores: dos jugadores con el mismo perk son una pieza de la línea, no dos.
    /// </summary>
    public static int FamilyHeld(RunState state, Catalog catalog, string family)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        return FamilyCount(HeldPerkIds(state), catalog, family);
    }

    /// <summary>
    /// Lo que los maestros ya aceptados han cerrado en esta run (ADR 0051). El bloqueo es <b>permanente</b>
    /// y mira hacia adelante: lo que ya se lleva sigue funcionando —un perk no se puede retirar (RF-072)—
    /// y lo que desaparece es la posibilidad de conseguir más de esa línea.
    /// </summary>
    public static ClosedLines ClosedBy(RunState state, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);

        var families = new SortedSet<string>(StringComparer.Ordinal);
        var perks = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string id in HeldPerkIds(state))
        {
            var definition = catalog.Perks.Find(id);
            if (definition is null || !definition.Blocks.Any)
            {
                continue;
            }

            foreach (string family in definition.Blocks.Families)
            {
                families.Add(family);
            }

            foreach (string blocked in definition.Blocks.Perks)
            {
                perks.Add(blocked);
            }
        }

        return new ClosedLines(families, perks);
    }

    /// <summary>Ids de perk que lleva la plantilla, sin repetir y en orden ordinal (RT-041).</summary>
    public static IReadOnlyList<string> HeldPerkIds(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var held = new SortedSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            for (int j = 0; j < player.Perks.Count; j++)
            {
                held.Add(player.Perks[j]);
            }
        }

        return held.ToList();
    }

    /// <summary>
    /// Jugadores de la plantilla que pueden recibir <paramref name="perk"/> ahora mismo: no está muerto,
    /// cumple posición y etiquetas requeridas/prohibidas (mismo filtro que
    /// <c>PerkAssignment.Eligible</c>), tiene un slot libre para su rareza (RF-023) y no lo lleva ya.
    /// Orden de id ascendente (RT-041).
    /// </summary>
    public static IReadOnlyList<int> EligibleCarriers(RunState state, PerkDefinition perk, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(catalog);

        var carriers = new List<int>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            if (player.Perks.Count >= ProgressionRules.PerkSlots(player.Rarity))
            {
                continue;
            }

            if (player.Perks.Contains(perk.Id))
            {
                continue;
            }

            var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
            if (!PerkAssignment.Eligible(definition, catalog).Any(p => string.Equals(p.Id, perk.Id, StringComparison.Ordinal)))
            {
                continue;
            }

            carriers.Add(player.Id);
        }

        return carriers;
    }

    /// <summary>Añade el perk indicado al jugador indicado, ordenado ordinalmente (mismo orden que <c>PerkAssignment</c>).</summary>
    public static RunPlayer WithPerk(RunPlayer player, string perkId)
    {
        ArgumentNullException.ThrowIfNull(player);
        var perks = new List<string>(player.Perks) { perkId };
        perks.Sort(StringComparer.Ordinal);
        return player with { Perks = perks };
    }

    private static int FamilyCount(IReadOnlyList<string> held, Catalog catalog, string family)
    {
        if (string.IsNullOrEmpty(family))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < held.Count; i++)
        {
            var definition = catalog.Perks.Find(held[i]);
            if (definition is not null && string.Equals(definition.Family, family, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsRaceAbility(PerkDefinition perk, RunState state, Catalog catalog) =>
        string.Equals(perk.Id, catalog.Race(state.ClubRace).Ability, StringComparison.Ordinal);
}

/// <summary>
/// Lo que un maestro ya aceptado cerró en esta run (ADR 0051): líneas enteras y perks concretos, los dos
/// en orden ordinal para que cualquier recorrido sea determinista (RT-041).
/// </summary>
public sealed class ClosedLines
{
    private readonly SortedSet<string> _families;
    private readonly SortedSet<string> _perks;

    internal ClosedLines(SortedSet<string> families, SortedSet<string> perks)
    {
        _families = families;
        _perks = perks;
    }

    /// <summary>Líneas cerradas, en orden ordinal.</summary>
    public IReadOnlyList<string> Families => _families.ToList();

    /// <summary>Perks concretos cerrados, en orden ordinal.</summary>
    public IReadOnlyList<string> Perks => _perks.ToList();

    /// <summary>True si ese perk ya no se puede conseguir en esta run.</summary>
    public bool Blocks(PerkDefinition perk)
    {
        ArgumentNullException.ThrowIfNull(perk);
        return (perk.HasFamily && _families.Contains(perk.Family)) || _perks.Contains(perk.Id);
    }
}

/// <summary>
/// Por qué /Sim rechaza cobrar un perk (ADR 0051). Es un mensaje de reglas, no de interfaz: la pantalla
/// lo enseña tal cual cuando el motor lanza, y la interfaz tiene su propio texto localizado para el caso
/// normal, en el que el bloqueo se anuncia ANTES de aceptar (RF-012d).
/// </summary>
public static class PerkPoolMessages
{
    public static string Why(PerkDefinition perk, PerkAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(perk);
        return availability switch
        {
            PerkAvailability.Unmet => perk.Requires is { } r
                ? $"'{perk.Id}' es un perk maestro: exige llevar ya {r.Count} perks de la línea '{r.Family}' (ADR 0051)"
                : $"'{perk.Id}' no cumple lo que exige (ADR 0051)",
            PerkAvailability.Closed =>
                $"'{perk.Id}' pertenece a una línea que un perk maestro cerró en esta run (ADR 0051): el bloqueo es permanente",
            PerkAvailability.MarketOnly =>
                $"'{perk.Id}' es un perk maestro y solo se compra en el mercado (ADR 0055): la recompensa por victoria no los da",
            PerkAvailability.NoCarrier =>
                $"nadie de la plantilla puede llevar '{perk.Id}' (sin slot libre, ya lo lleva, o no cumple sus etiquetas)",
            _ => string.Empty,
        };
    }
}
