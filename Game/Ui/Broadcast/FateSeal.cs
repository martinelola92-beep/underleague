using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// El lacre de la tirada del destino (ADR 0171): disco de cera con un anillo y ocho muescas que giran mientras
/// los dados ruedan. Color y forma juntos (UI-002): el resultado se lee también por la marca, que son los
/// rótulos hermanos de la escena. Se dibuja por código porque las muescas se mueven con el dato; el disco es
/// el radio de la mitad del menor lado del nodo. Relleno provisional: se oculta y se pone un
/// <see cref="TextureRect"/> con el lacre.
/// </summary>
[Tool]
[GlobalClass]
public partial class FateSeal : Control
{
    private float _angle;
    private ProclamationBand.FateOutcome _outcome;

    /// <summary>Ángulo de las muescas y resultado de la tirada.</summary>
    public void SetState(float angle, ProclamationBand.FateOutcome outcome)
    {
        _angle = angle;
        _outcome = outcome;
        QueueRedraw();
    }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var centre = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f;
        DrawCircle(centre + new Vector2(0f, 4f), radius, new Color(0f, 0f, 0f, 0.25f));
        var wax = _outcome == ProclamationBand.FateOutcome.Saved ? new Color("4f6b2a") : Pregon.Wax;
        DrawCircle(centre, radius, wax);
        DrawArc(centre, radius - 6f, 0f, Mathf.Tau, 48, Pregon.Vellum, 2f, true);

        // Las 8 muescas del canto giran con el sello mientras rueda; al parar quedan quietas.
        for (int i = 0; i < 8; i++)
        {
            float a = _angle + (i * Mathf.Tau / 8f);
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(centre + (dir * (radius - 4f)), centre + (dir * (radius + 6f)), Pregon.Sable, 3f);
        }
    }
}
