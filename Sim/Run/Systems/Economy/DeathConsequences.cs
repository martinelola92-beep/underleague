using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Las consecuencias de run de que un jugador propio muera (ADR 0048, ADR 0161, paquete BB), en un solo
/// sitio para que <b>todas</b> las vías de muerte —el partido, el sacrificio del evento del donante (ADR
/// 0159) y el matasanos de la clínica (ADR 0099)— las tengan iguales (revisión independiente, 29 sep 2026:
/// las dos últimas se saltaban el objeto de vuelta al almacén, la reliquia, la Herencia y el oro de muerte):
/// <list type="number">
/// <item>el objeto del muerto <b>vuelve al almacén</b> y suma a <see cref="RunState.ItemsRecoveredCounter"/>
/// (ADR 0048, condición 4) — <see cref="StoreRecovered"/>;</item>
/// <item>una <b>reliquia</b> por cada jugador propio muerto que no sea <b>mercenario</b> (ADR 0161 §2; un
/// mercenario no es del club, RF-111) — <see cref="ApplyRunConsequences"/>;</item>
/// <item><b>Herencia</b>: atributos del muerto a su vinculado (<see cref="InheritanceSystem"/>);</item>
/// <item><b>oro de muerte</b> (Seguro de vida, <see cref="GoldCalculator.DeathGold(RunState, IReadOnlyList{PlayerDeathDetail}, EconomyConfig)"/>)
/// — <see cref="GoldFor"/>.</item>
/// </list>
/// El partido lo llama por partes (<c>MatchResolution</c> recoge los objetos evento a evento, y
/// <c>StandardRunSystems.AfterMatch</c> cobra el oro después de la penalización de derrota, orden que la
/// ADR 0113 fija); las muertes de fuera del partido usan <see cref="Kill"/>, que lo hace todo de una vez.
/// </summary>
public static class DeathConsequences
{
    /// <summary>
    /// Mata a un jugador propio <b>fuera de un partido</b> con todas sus consecuencias de run: lo marca
    /// muerto, devuelve su objeto al almacén, lo saca de la alineación guardada, y aplica reliquia,
    /// Herencia y oro de muerte. <paramref name="catalog"/> e <paramref name="items"/> pueden ser
    /// <c>null</c> (tests de sistemas sueltos): sin catálogo no hay vinculado que heredar, y sin
    /// catálogo de objetos no hay reliquia. <paramref name="cause"/> queda anotada para la esquela de la
    /// Gaceta (ADR 0163): cada llamante decide cómo murió, sin valor por defecto.
    /// </summary>
    public static RunState Kill(
        RunState state, int playerId, PlayerDeathCause cause, Catalog? catalog, EconomyConfig economy, ItemCatalog? items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);
        var victim = state.GetPlayer(playerId);
        int linked = catalog is null ? -1 : LinkedTeammate(state.Lineup, victim, catalog);
        var detail = new PlayerDeathDetail(victim.Id, victim.Perks, linked);

        var recovered = new List<string>(1);
        if (victim.Item is { } itemId)
        {
            recovered.Add(itemId);
            victim = victim with { Item = null };
        }

        state = state.WithPlayer(victim with { PhysicalState = PhysicalState.Dead }).WithDeathCause(victim.Id, cause);
        state = StoreRecovered(state, recovered);
        state = MatchResolution.PruneLineup(state);
        var deaths = new[] { detail };
        state = ApplyRunConsequences(state, deaths, economy, items);
        return state.AddGold(GoldFor(state, deaths, economy));
    }

    /// <summary>
    /// Devuelve al almacén los objetos de los muertos, en orden de id de objeto (RT-041), y los suma al
    /// contador de objetos recuperados (ADR 0048, condición 4).
    /// </summary>
    public static RunState StoreRecovered(RunState state, List<string> recovered)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(recovered);
        recovered.Sort(StringComparer.Ordinal);
        for (int i = 0; i < recovered.Count; i++)
        {
            state = state.WithStockedItem(recovered[i], 1);
        }

        return recovered.Count == 0
            ? state
            : state.WithCounter(RunState.ItemsRecoveredCounter, state.Counter(RunState.ItemsRecoveredCounter) + recovered.Count);
    }

    /// <summary>
    /// Reliquia (ADR 0161 §2) y Herencia por cada muerte, en el orden de la lista (ya determinista,
    /// RT-041). Lee la carrera del muerto ya actualizada. Sin tirada
    /// (<see cref="RelicSystem.Classify"/> es puro); si el catálogo no tiene reliquia de esa clase, esa
    /// muerte se queda sin ella en vez de lanzar. <b>Un mercenario no deja reliquia</b>: no es del club
    /// (RF-111, ADR 0161 revisión). <paramref name="items"/> <c>null</c> = sin reliquias.
    /// </summary>
    public static RunState ApplyRunConsequences(
        RunState state, IReadOnlyList<PlayerDeathDetail> deaths, EconomyConfig economy, ItemCatalog? items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(deaths);
        ArgumentNullException.ThrowIfNull(economy);
        if (deaths.Count == 0)
        {
            return state;
        }

        var next = state;
        if (items is not null)
        {
            for (int i = 0; i < deaths.Count; i++)
            {
                var player = next.GetPlayer(deaths[i].PlayerId);
                if (player.IsMercenary)
                {
                    continue;
                }

                var relic = items.FindRelic(RelicSystem.Classify(player.Career));
                if (relic is not null)
                {
                    next = next.WithStockedItem(relic.Id, 1);
                }
            }
        }

        // Herencia: traspasa atributos ANTES de tocar el oro, porque cambia a otro jugador, no una cifra.
        return InheritanceSystem.Apply(next, deaths, economy);
    }

    /// <summary>Oro de muerte de esas muertes (Seguro de vida). Se cobra se gane o se pierda (ADR 0113).</summary>
    public static int GoldFor(RunState state, IReadOnlyList<PlayerDeathDetail> deaths, EconomyConfig economy) =>
        GoldCalculator.DeathGold(state, deaths, economy).Total;

    /// <summary>
    /// Compañero vinculado de <paramref name="player"/> en la alineación INICIAL de este partido (paquete
    /// BB, §3.2): el primero de sus perks, en el orden ascendente en que ya vienen en
    /// <see cref="RunPlayer.Perks"/> (RT-041), que declare relaciones de vínculo
    /// (<see cref="PerkDefinition.Links"/>), resuelto con la MISMA geometría que usa el motor durante el
    /// partido (<see cref="LinkGeometry"/>): mismas casillas-hogar, mismo desempate por distancia y por
    /// id ascendente. Solo ese primer perk decide -si tiene vínculos declarados pero ninguna relación
    /// resuelve candidato, no se prueba con el siguiente perk-: es una decisión del paquete BB para que
    /// "el vinculado" sea una respuesta única y determinista, no una lista.
    ///
    /// <para>Solo mira <see cref="Lineup"/> (la colocación con la que se empezó el partido o, fuera de partido, la guardada):
    /// un suplente que entra por una sustitución forzada (ADR 0094) no tiene casilla-hogar propia aquí, así
    /// que no puede ser origen ni destino de un traspaso si muere o hereda tras entrar. Es una limitación
    /// conocida, documentada en el informe del paquete BB.</para>
    ///
    /// <para>Devuelve -1 sin comprobar si el candidato sigue vivo: esa comprobación la hace
    /// <see cref="InheritanceSystem"/> contra el estado final de la plantilla, no aquí.</para>
    /// </summary>
    public static int LinkedTeammate(Lineup lineup, RunPlayer player, Catalog catalog)
    {
        var slots = lineup.Slots;
        int selfIndex = -1;
        var homes = new Cell[slots.Count];
        var teams = new int[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            homes[i] = slots[i].HomeCell;
            teams[i] = 0;
            if (slots[i].PlayerId == player.Id)
            {
                selfIndex = i;
            }
        }

        if (selfIndex < 0)
        {
            return -1;
        }

        var perks = player.Perks;
        for (int p = 0; p < perks.Count; p++)
        {
            var perk = catalog.Perks.Find(perks[p]);
            if (perk is null || perk.Links.Count == 0)
            {
                continue;
            }

            for (int r = 0; r < perk.Links.Count; r++)
            {
                int candidate = LinkGeometry.ResolveLink(homes, teams, selfIndex, perk.Links[r]);
                if (candidate >= 0)
                {
                    return slots[candidate].PlayerId;
                }
            }

            return -1;
        }

        return -1;
    }
}
