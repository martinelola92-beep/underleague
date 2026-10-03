using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Market;

namespace Underleague.Sim.Run.Systems.Nodes;

/// <summary>
/// La inscripción (ADR 0046, amplía RF-011; **ADR 0097**): paga oro y <b>amplía la plantilla en un
/// hueco</b>, desde la base de <see cref="RunRules.BaseRosterSize"/> hasta el techo de
/// <see cref="RunRules.MaxRosterSize"/> (RF-020). Desde la ADR 0097 <b>no es un nodo del mapa sino una
/// compra más del mercado</b>: el derecho a tener un cuerpo más compite en el mismo mostrador que el perk
/// y el objeto, que es donde la decisión se ve.
///
/// <para>El coste es <b>creciente</b> y vive en datos (<c>economy.enrollmentCosts</c>): el primer hueco
/// es caro y el segundo bastante más, del orden de la mitad del oro de una run entera entre los dos
/// (ADR 0044). No compra un jugador —eso sigue siendo cosa del mercado—: compra el <b>derecho</b> a
/// tener uno más, y por eso compite con la clínica y no con el fichaje: <i>¿curo al que tengo o me
/// traigo a otro?</i></para>
///
/// <para>También vive aquí <see cref="Release"/>, el descarte. Es la otra mitad de la regla: con la
/// plantilla llena, fichar exige vender —solo en el mercado, RF-114f— o descartar, que se puede en
/// cualquier sitio y no paga nada.</para>
/// </summary>
public static class EnrollmentSystem
{
    /// <summary>
    /// Compra el siguiente hueco de plantilla en el mercado abierto (ADR 0097). Lanza si no queda hueco
    /// que comprar (ya se está en el techo de 12) o si el oro no llega.
    /// </summary>
    public static RunState Expand(RunState state, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);

        NodeGuards.RequireOpen(state, NodeKind.Market, "ampliar la plantilla");

        int bought = state.Counter(RunState.EnrollmentSlotsCounter);
        int cost = economy.EnrollmentCost(bought);
        if (cost < 0)
        {
            throw new InvalidOperationException(
                $"la plantilla ya está en su techo de {RunRules.MaxRosterSize} (RF-020): no quedan huecos que inscribir");
        }

        if (state.Gold < cost)
        {
            throw new ArgumentException(
                $"el hueco de plantilla cuesta {cost} de oro y la run solo tiene {state.Gold}",
                nameof(state));
        }

        return state
            .AddGold(-cost)
            .WithCounter(RunState.EnrollmentSlotsCounter, bought + 1);
    }

    /// <summary>
    /// Descarta a un jugador (RF-020, ADR 0046): sale de la plantilla y deja su hueco libre, sin cobrar
    /// nada. No se puede descartar a un muerto —ya no ocupa plantilla, y su sitio es el memorial de
    /// RF-122— ni dejar los disponibles por debajo del mínimo de RF-002b, que sería perder la run por
    /// una decisión de menú.
    /// </summary>
    public static RunState Release(RunState state, ReleasePlayer decision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);

        var player = state.GetPlayer(decision.PlayerId);
        if (player.PhysicalState == PhysicalState.Dead)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está muerto: no ocupa plantilla y no hay nada que descartar (RF-093, RF-122)",
                nameof(decision));
        }

        if (player.IsAvailable && state.AvailablePlayerCount <= RunRules.MinimumAvailablePlayers)
        {
            throw new ArgumentException(
                $"descartar a {player.Id} dejaría {state.AvailablePlayerCount - 1} disponibles y el mínimo es "
                    + $"{RunRules.MinimumAvailablePlayers} (RF-002b)",
                nameof(decision));
        }

        return state.WithoutPlayer(player.Id);
    }

    /// <summary>
    /// Hace sitio para un fichaje que sustituye a uno propio (BX-5). <b>No es una regla nueva</b>: es la de siempre, aplicada
    /// a quien se elige soltar. Con <paramref name="economy"/> (el mercado) se <b>vende</b> si el veto de la ADR 0108 lo
    /// permite —<see cref="MarketSystem.CanSell"/>, al precio de <see cref="MarketSystem.SalePrice"/>— y, si no, se
    /// <b>descarta</b> sin cobrar (<see cref="Release"/>); sin él (una recompensa, donde no hay mercado) siempre se descarta.
    ///
    /// <para>Sólo vale con la plantilla <b>llena</b> (con hueco, soltar a alguien sería elegir perder un cuerpo por nada) y
    /// nunca con un muerto. No aplica el mínimo de RF-002b de <see cref="Release"/> porque quien llega ocupa su sitio al
    /// instante y entra sano: los disponibles no bajan. Quien llama lo hace dentro de la misma decisión, así que si el
    /// fichaje falla después el estado no se ha tocado.</para>
    /// </summary>
    public static RunState MakeRoomFor(RunState state, int replacePlayerId, EconomyConfig? economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.HasRosterSpace)
        {
            throw new ArgumentException(
                "no hay nada que sustituir: la plantilla tiene hueco (BX-5, RF-020)", nameof(replacePlayerId));
        }

        var player = state.GetPlayer(replacePlayerId);
        if (player.PhysicalState == PhysicalState.Dead)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está muerto: no ocupa plantilla y no se puede soltar (RF-093, RF-122)",
                nameof(replacePlayerId));
        }

        var without = state.WithoutPlayer(player.Id);
        return economy is not null && MarketSystem.CanSell(player)
            ? without.AddGold(MarketSystem.SalePrice(player, economy))
            : without;
    }
}
