using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Consumables;

namespace Underleague.Sim.Run;

/// <summary>
/// Decisiones del jugador <b>dentro</b> de un partido, que viajan como estado inicial (docs/arquitectura.md,
/// ADR 0094): la activación manual de un consumible (RF-082) y lo que el jugador responde a un punto de
/// decisión de sustitución. Vacías por defecto: <c>/Balance</c> y los tests no traen ninguna y
/// <see cref="RunEngine.EnterMatch"/> resuelve los puntos de decisión con la política por defecto.
///
/// <para><b>Un punto de decisión admite tres respuestas</b> (ADR 0134), y cada una vive en su propia lista
/// porque son cosas distintas, no variantes de la misma con un centinela dentro —el patrón por el que sigue
/// abierto el pendiente BE-F—:</para>
/// <list type="bullet">
///   <item><see cref="Substitutions"/>: entra este del banquillo (AZ-F).</item>
///   <item><see cref="Declines"/>: no entra nadie, la casilla se queda vacía. Es la inferioridad voluntaria
///   de RF-002d ejercida en mitad del partido. <b>No produce ningún hecho que el motor deba ejecutar</b>, así
///   que se queda en esta capa y no llega a <c>TeamSetup</c>: el motor no se entera de que existe.</item>
///   <item><see cref="PlayOns"/>: el lesionado <b>leve</b> no deja el campo (ADR 0134 E). Esta sí viaja a
///   <c>TeamSetup.PlayOns</c>, porque cambia lo que el motor hace en el tick de la lesión.</item>
/// </list>
/// </summary>
public sealed record MatchDecisions(
    IReadOnlyList<ManualActivation> ManualActivations,
    IReadOnlyList<Substitution> Substitutions)
{
    public static MatchDecisions None { get; } = new(Array.Empty<ManualActivation>(), Array.Empty<Substitution>());

    /// <summary>
    /// Puntos de decisión que el jugador respondió con «que se quede el hueco» (ADR 0134 D). Se declaran
    /// como propiedad <c>init</c> y no como parámetro posicional por la misma razón que
    /// <c>TeamSetup.Consumables</c>: las construcciones existentes siguen valiendo sin tocarlas.
    /// </summary>
    public IReadOnlyList<DeclinedSubstitution> Declines { get; init; } = Array.Empty<DeclinedSubstitution>();

    /// <summary>
    /// Lesionados leves que el jugador decidió no retirar (ADR 0134 E). Es la única de las tres respuestas
    /// que el motor necesita conocer, y llega a él por <c>TeamSetup.PlayOns</c>.
    /// </summary>
    public IReadOnlyList<PlayOn> PlayOns { get; init; } = Array.Empty<PlayOn>();
}

/// <summary>
/// «Que se quede el hueco» (ADR 0134 D): en el tick <paramref name="Tick"/> salió
/// <paramref name="OutPlayerId"/> y el jugador ha decidido <b>no</b> meter a nadie, aunque hubiera
/// banquillo. El equipo sigue el resto del partido con una casilla vacía, que es lo que RF-002d llama una
/// decisión legítima —aquí ejercida durante el partido en vez de antes—.
///
/// <para>Razón para tomarla: el que entra también puede lesionarse y morir, y la plantilla es el recurso
/// central de la run. Es un canje entre jugar en inferioridad y no poner otro cuerpo en riesgo.</para>
///
/// <para>No es un <see cref="Substitution"/> con un valor especial dentro: un rechazo no produce ningún
/// hecho que el motor ejecute, y meterlo en la lista de sustituciones habría obligado a cada consumidor de
/// esa lista —motor, validación, replay, vista de momentos— a acordarse de excluirlo.</para>
/// </summary>
public sealed record DeclinedSubstitution(int Tick, int OutPlayerId);
