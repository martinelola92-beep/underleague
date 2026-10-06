using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Trompeta de heraldo (<see cref="Pregon.DrawTrumpet"/>) como nodo propio: el origen del dibujo es la esquina
/// superior izquierda del nodo. Relleno provisional: se oculta y se pone un <see cref="TextureRect"/> al lado.
/// </summary>
[Tool]
[GlobalClass]
public partial class InkTrumpet : Control
{
    private float _length = 240f;
    private float _degrees = 2f;
    private bool _mirror;

    /// <summary>Largo del tubo, en píxeles.</summary>
    [Export]
    public float Length { get => _length; set { _length = value; QueueRedraw(); } }

    /// <summary>Giro alrededor del origen, en grados.</summary>
    [Export]
    public float Degrees { get => _degrees; set { _degrees = value; QueueRedraw(); } }

    [Export]
    public bool Mirror { get => _mirror; set { _mirror = value; QueueRedraw(); } }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw() => Pregon.DrawTrumpet(this, Vector2.Zero, _length, _degrees, _mirror);
}
