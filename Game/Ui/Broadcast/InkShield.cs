using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Escudo heráldico de tinta (<see cref="Pregon.DrawShield"/>) como nodo propio, ocupando todo su rectángulo.
/// Relleno provisional: para poner un escudo dibujado, se oculta este nodo y se añade un
/// <see cref="TextureRect"/> al lado — el código no lo toca.
/// </summary>
[Tool]
[GlobalClass]
public partial class InkShield : Control
{
    private bool _ours = true;

    /// <summary>Propio (azur con banda de oro) o rival (gules con palo de sable): UI-002, color y forma.</summary>
    [Export]
    public bool Ours { get => _ours; set { _ours = value; QueueRedraw(); } }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw() => Pregon.DrawShield(this, Vector2.Zero, Size.X, Size.Y, _ours);
}
