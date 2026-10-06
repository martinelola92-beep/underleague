using Godot;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Marca de estado físico de una tira de jugador, por color <b>y</b> forma (UI-002): sano ●, tocado ▼, grave
/// ■, muerto con una cruz encima. Es geometría que cambia con el dato, así que sigue dibujada por código;
/// se centra en su rectángulo. Relleno provisional: se oculta y se ponen sprites al lado.
/// </summary>
[Tool]
[GlobalClass]
public partial class StripStateMark : Control
{
    private PhysicalState _state = PhysicalState.Healthy;
    private bool _attenuated;

    /// <summary>El estado que se enseña y si la tira está atenuada (muerto o expulsado).</summary>
    public void SetState(PhysicalState state, bool attenuated)
    {
        _state = state;
        _attenuated = attenuated;
        QueueRedraw();
    }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var center = Size / 2f;
        Color color = _state switch
        {
            PhysicalState.MinorInjury => Pregon.Or,
            PhysicalState.SevereInjury => Pregon.Blood,
            PhysicalState.Dead => new Color("6b6258"),
            _ => new Color("3f7a44"),
        };
        if (_attenuated)
        {
            color = new Color(color, 0.6f);
        }

        Vector2[] shape = _state switch
        {
            PhysicalState.MinorInjury => new[] { new Vector2(0, -11), new Vector2(11, 9), new Vector2(-11, 9) },
            PhysicalState.SevereInjury => new[] { new Vector2(-10, -10), new Vector2(10, -10), new Vector2(10, 10), new Vector2(-10, 10) },
            _ => Pregon.Burst(11f, 11f, 8, Vector2.Zero),
        };

        var shifted = new Vector2[shape.Length];
        for (int i = 0; i < shape.Length; i++)
        {
            shifted[i] = shape[i] + center;
        }

        DrawColoredPolygon(shifted, color);
        DrawLine(shifted[^1], shifted[0], Pregon.Sable, 1.5f);
        for (int i = 1; i < shifted.Length; i++)
        {
            DrawLine(shifted[i - 1], shifted[i], Pregon.Sable, 1.5f);
        }

        if (_attenuated)
        {
            Style.DrawDownMark(this, center, 11f, Pregon.Sable);
        }
    }
}
