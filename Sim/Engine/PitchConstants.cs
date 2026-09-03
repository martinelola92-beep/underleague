using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Constantes geométricas que <see cref="MatchEngine"/> y <see cref="Utility"/> necesitaban por igual y
/// tenían duplicadas como dos <c>private const</c> independientes (revisión independiente, fase 0):
/// el mismo radio de presión y la misma fila central vivían en dos sitios que alguien podía desincronizar
/// sin que ningún test lo notara. Se unifican aquí una sola vez.
/// </summary>
internal static class PitchConstants
{
    /// <summary>Radio en el que un rival presiona al poseedor o a un receptor (§3.5, §3.7).</summary>
    public const float PressureRadius = 1.0f;

    /// <summary>Fila central del campo.</summary>
    public const float CenterRow = Pitch.Rows / 2f;
}
