using Underleague.Sim.Engine;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// BE-C (docs/pendientes/BE-C.md): <b>el umbral de incomparecencia del partido y el mínimo de jugadores
/// disponibles de la run tienen que ser el mismo número.</b>
///
/// <para><b>Qué vigila.</b> Los dos números miden lo mismo —cuándo un equipo deja de poder jugar— y viven
/// en ficheros distintos sin nada que los ate. Separarlos haría que un equipo siguiera jugando un partido
/// que la run ya considera perdido, o al revés, y no lo notaría ningún test. Este es ese test.</para>
///
/// <para><b>Qué NO demuestra, dicho aquí porque se llegó a afirmar lo contrario.</b> Que los dos umbrales
/// coincidan <b>no</b> implica que no exista ventana de partido posterior a <c>defeatTick</c>. El
/// argumento que se dio —los del campo son subconjunto de los disponibles— es <b>falso</b>:
/// <c>RunLineup.CanStart</c> deja salir al lesionado grave marcado (RF-093 vía 1) y
/// <c>RunState.IsAvailable</c> no lo cuenta, así que puede haber siete en el campo con cinco disponibles.
/// Esta es una condición <b>necesaria y no suficiente</b>: útil, pero no el invariante entero. Lo que
/// falta está en <c>docs/pendientes/BE-C.md</c>.</para>
/// </summary>
public sealed class ForfeitThresholdTests
{
    [Fact]
    public void TheMatchForfeitThresholdMatchesTheRunMinimum()
    {
        Assert.Equal(RunRules.MinimumAvailablePlayers, MatchEngine.MinimumPlayersOnPitch);
    }
}
