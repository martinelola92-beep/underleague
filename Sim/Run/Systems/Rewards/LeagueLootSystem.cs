using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Run.Systems.Rewards;

/// <summary>
/// Botín de liga (ADR 0161 §1): ganar un partido de <b>LIGA</b> da, además del oro, un objeto
/// <b>común</b> al almacén, sorteado con el flujo de recompensas del nodo (RT-022). Élite y jefe no
/// cambian.
///
/// <para><b>Función pura y determinista</b>, deliberadamente separada de
/// <c>StandardRunSystems.AfterMatch</c> (que aplica el botín al almacén): con el mismo (semilla, nodo,
/// acto, raza, catálogo) elige siempre el mismo objeto, así que <c>PostMatchView</c> —que solo lo enseña,
/// sin tocar el estado— puede recalcularlo por su cuenta sin que nadie tenga que guardarlo en
/// <c>RunMatchSummary</c> ni ensanchar la interfaz de <c>IRunSystems.AfterMatch</c> para pasarlo de vuelta.
/// </para>
///
/// <para><b>Flujo de RNG propio, sin colisionar con nada</b> (RT-022): <see cref="LootRollStream"/> está
/// muy por encima de cualquier reroll de recompensa posible (RF-071b: uno por nodo, así que el flujo de
/// <c>RewardSystem</c> nunca pasa de <c>NodeRerolls + 2·100</c>) y separado del desplazamiento con el que
/// <c>EquipmentSystem.ProcessFragileItems</c> tira las roturas (<c>FragileRollStream = 5000</c>): jugar un
/// partido de liga, repetir su tirada de recompensa (que no tiene, RF-071 no aplica a picks=0) o romper un
/// objeto frágil no puede desplazar el dado del botín, ni al revés.</para>
/// </summary>
public static class LeagueLootSystem
{
    /// <summary>Ver la nota de flujo de RNG de la clase.</summary>
    private const int LootRollStream = 6000;

    /// <summary>True si este nodo, con este resultado, da botín de liga (ADR 0161 §1).</summary>
    public static bool AppliesTo(NodeKind kind, bool won) => kind == NodeKind.LeagueMatch && won;

    /// <summary>
    /// El objeto común que gana ese nodo de liga. Solo llamar cuando <see cref="AppliesTo"/> es true.
    /// Si el pool de comunes de la raza/acto quedara vacío (no debería: el catálogo garantiza universales
    /// en acto 1), cae al pool entero de <see cref="ItemCatalog.OfferableTo(Race, int)"/> antes que fallar
    /// una victoria sin poder pagarla.
    /// </summary>
    public static ItemDefinition Pick(ulong seed, int nodeId, int act, Race clubRace, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var offerable = items.OfferableTo(clubRace, act);
        var pool = new List<ItemDefinition>(offerable.Count);
        for (int i = 0; i < offerable.Count; i++)
        {
            if (offerable[i].Rarity == Rarity.Common)
            {
                pool.Add(offerable[i]);
            }
        }

        if (pool.Count == 0)
        {
            pool = new List<ItemDefinition>(offerable);
        }

        if (pool.Count == 0)
        {
            throw new InvalidOperationException(
                $"no hay ningún objeto ofertable para el botín de liga (raza {clubRace}, acto {act}): data/items/ no puede quedar vacío para una run jugable");
        }

        var rng = OfferStream.For(seed, nodeId, LootRollStream);
        var weights = new List<int>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            weights.Add(items.DepthWeight(pool[i], ItemPricing.OfferWeight(pool[i], items.Scale), act));
        }

        return pool[WeightedPick.Index(ref rng, weights)];
    }
}
