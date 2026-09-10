namespace Underleague.Sim.Model;

/// <summary>
/// Sustitución forzada (ADR 0094, AZ-F): al final del tick <paramref name="Tick"/> —en el que
/// <paramref name="OutPlayerId"/> dejó el campo por lesión o muerte— entra <paramref name="InPlayerId"/>,
/// un jugador de la plantilla que no estaba alineado. Es parte del <b>estado inicial</b> del partido, como
/// la activación manual de un consumible (RF-082, docs/arquitectura.md): volver a ejecutar el partido con
/// la misma lista reproduce exactamente lo mismo (RT-013, RT-024, RT-061). No existen sustituciones
/// voluntarias ni por expulsión: el motor rechaza una sustitución cuyo jugador saliente no haya salido por
/// lesión o muerte en un tick anterior o igual.
/// </summary>
public sealed record Substitution(int Tick, int OutPlayerId, int InPlayerId);
