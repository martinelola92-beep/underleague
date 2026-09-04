using Godot;
using Underleague.Sim.Random;

namespace Underleague.Game;

/// <summary>
/// Prueba de frontera: comprueba que /Game puede consumir /Sim y que el runtime de Godot
/// arranca con net10.0 (D-17, ADR 0008). No es la pantalla de Equipo.
/// </summary>
public partial class Main : Node
{
    public override void _Ready()
    {
        var rng = new Pcg32(42, 54);
        GD.Print($"Underleague: Sim vivo desde Godot. PCG32(42,54) -> {rng.Next():x8}");
    }
}
