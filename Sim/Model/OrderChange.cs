namespace Underleague.Sim.Model;

/// <summary>
/// Cambio de la orden táctica de un equipo <b>durante</b> el partido (ADR 0154): desde el tick
/// <paramref name="Tick"/>, el equipo juega con <paramref name="Order"/>. Viaja como estado inicial, igual
/// que las sustituciones (ADR 0094): volver a simular con la misma lista reproduce el mismo partido, y un
/// cambio en el tick T no altera nada anterior a T, así que la pantalla puede reanudar desde ahí.
/// </summary>
public sealed record OrderChange(int Tick, Underleague.Sim.Engine.Mentality Order);
