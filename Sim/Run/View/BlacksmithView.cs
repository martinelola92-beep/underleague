using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Medical;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Lo que el jugador ve en la mesa del herrero antes de confirmar (ADR 0164, RF-095, RF-095b): el precio total, la
/// tabla de tres resultados para el oro extra elegido y lo que un fallo le costaría a la identidad del jugador.
/// Los porcentajes salen de <see cref="MedicalSystem.BlacksmithOddsFor"/>, la misma función que tira
/// <see cref="MedicalSystem.Forge"/>: lo que se enseña es lo que se juega.
/// </summary>
/// <param name="ExtraGold">Oro extra invertido en esta vista (0..<paramref name="MaxExtraGold"/>).</param>
/// <param name="MaxExtraGold">Tope de oro extra (rendimiento decreciente, RF-095b).</param>
/// <param name="BasePrice">Precio del servicio sin oro extra.</param>
/// <param name="Price">Precio total: <paramref name="BasePrice"/> + <paramref name="ExtraGold"/>.</param>
/// <param name="Odds">Curación, mejora y empeoramiento en porcentaje entero; suman 100.</param>
/// <param name="Affordable">True si la run tiene el oro.</param>
/// <param name="FreeSlots">Ranuras del cuerpo que siguen libres.</param>
/// <param name="ProsthesesNow">Prótesis que ya lleva el jugador.</param>
/// <param name="NextMakesAutomaton">True si una prótesis nueva sería la tercera y le quitaría su especie (RF-095c).</param>
public sealed record BlacksmithQuote(
    int PlayerId,
    int ExtraGold,
    int MaxExtraGold,
    int BasePrice,
    int Price,
    BlacksmithOdds Odds,
    bool Affordable,
    IReadOnlyList<string> FreeSlots,
    int ProsthesesNow,
    bool NextMakesAutomaton)
{
    /// <summary>True si el herrero puede atender al jugador: le queda una ranura libre.</summary>
    public bool HasFreeSlot => FreeSlots.Count > 0;

    /// <summary>True si <see cref="MedicalSystem.Forge"/> aceptaría esta decisión: hay oro y ranura libre.</summary>
    public bool CanConfirm => Affordable && HasFreeSlot;
}

/// <summary>Qué salió de la mesa del herrero, para anunciarlo (ADR 0164).</summary>
public enum BlacksmithOutcomeKind
{
    /// <summary>Curado sin prótesis.</summary>
    Cured,

    /// <summary>Sale con una prótesis de ventaja.</summary>
    Improved,

    /// <summary>Sale con una prótesis de desventaja.</summary>
    Worsened,
}

/// <summary>Resultado del herrero: la clase, la prótesis instalada y si el jugador ha dejado de ser de su especie.</summary>
public sealed record BlacksmithResult(
    BlacksmithOutcomeKind Kind,
    ProsthesisDefinition? Prosthesis,
    bool BecameAutomaton);

/// <summary>Vista del herrero de la clínica (ADR 0164). Pura: no tira ningún dado.</summary>
public static class BlacksmithView
{
    /// <summary>La cotización del herrero para un jugador con <paramref name="extraGold"/> de oro extra.</summary>
    public static BlacksmithQuote Quote(
        RunState state,
        EconomyConfig economy,
        ProsthesisCatalog prostheses,
        int playerId,
        int extraGold)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(prostheses);
        var player = state.GetPlayer(playerId);
        var odds = MedicalSystem.BlacksmithOddsFor(economy, extraGold);
        int basePrice = MedicalSystem.BlacksmithBasePrice(economy);
        var occupied = MedicalSystem.OccupiedSlots(player);
        var free = prostheses.Slots.Where(slot => !occupied.Contains(slot)).ToList();
        return new BlacksmithQuote(
            playerId,
            extraGold,
            economy.Blacksmith.MaxExtraGold,
            basePrice,
            basePrice + extraGold,
            odds,
            state.Gold >= basePrice + extraGold,
            free,
            player.Prostheses.Count,
            player.Prostheses.Count + 1 >= MedicalSystem.ProsthesesForAutomaton && player.SpeciesTag != MedicalSystem.AutomatonTag);
    }

    /// <summary>Todas las cotizaciones posibles de un jugador, de 0 al tope de oro extra: la tabla que enseña la clínica paso a paso.</summary>
    public static IReadOnlyList<BlacksmithQuote> Quotes(RunState state, EconomyConfig economy, ProsthesisCatalog prostheses, int playerId)
    {
        ArgumentNullException.ThrowIfNull(economy);
        var quotes = new List<BlacksmithQuote>(economy.Blacksmith.MaxExtraGold + 1);
        for (int extra = 0; extra <= economy.Blacksmith.MaxExtraGold; extra++)
        {
            quotes.Add(Quote(state, economy, prostheses, playerId, extra));
        }

        return quotes;
    }

    /// <summary>
    /// Qué ha salido de la mesa, comparando al jugador antes y después de <see cref="MedicalSystem.Forge"/>: sin
    /// prótesis nueva es una curación; con ella, su clase la da el catálogo.
    /// </summary>
    public static BlacksmithResult Outcome(RunPlayer before, RunPlayer after, ProsthesisCatalog prostheses)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(prostheses);
        if (after.Prostheses.Count <= before.Prostheses.Count)
        {
            return new BlacksmithResult(BlacksmithOutcomeKind.Cured, null, false);
        }

        var installed = prostheses.Find(after.Prostheses[^1].Effect)
            ?? throw new InvalidOperationException($"la prótesis '{after.Prostheses[^1].Effect}' no está en el catálogo");
        return new BlacksmithResult(
            installed.Kind == ProsthesisKind.Improve ? BlacksmithOutcomeKind.Improved : BlacksmithOutcomeKind.Worsened,
            installed,
            before.SpeciesTag != MedicalSystem.AutomatonTag && after.SpeciesTag == MedicalSystem.AutomatonTag);
    }
}
