namespace Underleague.Sim.Run.Systems.Bets;

/// <summary>
/// La apuesta que el jugador ha <b>tomado</b> para un nodo de partido (ADR 0157): lo único de la apuesta que
/// se guarda en el estado de la run (la ofrecida se deriva, W-12). Guarda lo que se le enseñó al tomarla —
/// cantidad, cobro y jugador nombrado— para que se cobre exactamente eso, pase lo que pase con los datos.
/// </summary>
/// <param name="BetId">Id de la ficha en <c>data/bets/bets.json</c>.</param>
/// <param name="NodeId">Nodo de partido al que va la apuesta.</param>
/// <param name="Stake">Oro pagado al tomarla.</param>
/// <param name="PayoutPercent">Cobro bruto, en % de la apuesta, si se cumple.</param>
/// <param name="TargetPlayerId">Jugador rival nombrado (<c>hunt_the_star</c>), -1 si no aplica.</param>
/// <param name="TargetPlayerName">Nombre del jugador nombrado; vacío si no aplica.</param>
public sealed record AcceptedBet(
    string BetId,
    int NodeId,
    int Stake,
    int PayoutPercent,
    int TargetPlayerId,
    string TargetPlayerName)
{
    /// <summary>Oro bruto que se cobra si se cumple.</summary>
    public int Payout => BetSystem.PayoutFor(Stake, PayoutPercent);
}

/// <summary>
/// Cómo terminó una apuesta tomada: lo que <c>RunMatchSummary.Bet</c> y el informe post-partido enseñan
/// (ADR 0157 punto 3). Se resuelve con los hechos del partido, nunca con una tirada nueva.
/// </summary>
/// <param name="Met">True si la condición se cumplió.</param>
/// <param name="GoldPaid">Oro que se ingresó: <see cref="AcceptedBet.Payout"/> si se cumplió, 0 si no. La apuesta ya se pagó al tomarla.</param>
public sealed record BetResult(
    string BetId,
    BetKind Kind,
    int Stake,
    int PayoutPercent,
    int TargetPlayerId,
    string TargetPlayerName,
    bool Met,
    int GoldPaid)
{
    /// <summary>Balance de la apuesta en oro: lo cobrado menos lo apostado (negativo si falló).</summary>
    public int Net => GoldPaid - Stake;
}
