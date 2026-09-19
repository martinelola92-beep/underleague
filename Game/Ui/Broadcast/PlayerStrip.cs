using Godot;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Datos de una tira de jugador (docs/ui/README.md §7): escudo con dorsal, nombre, «puesto · raza»,
/// recuento de perks y estado físico. <see cref="Subtitle"/> ya viene compuesto por quien la crea (la
/// plantilla <c>ui.pregon.strip.subtitle</c> vive en <see cref="UiText"/>, no aquí — RT-035).
/// </summary>
/// <param name="Off">Muerto o expulsado: la tira se atenúa y la marca de estado lleva una cruz (UI-002).</param>
public sealed record StripModel(int Number, string Name, string Subtitle, int PerkCount, PhysicalState State, bool Off);

/// <summary>
/// Tira de jugador de 232×72 (docs/ui/README.md §7): papel con borde rasgado, escudo con dorsal, nombre en
/// tinta, «puesto · raza» y recuento de perks, y una marca de estado físico por color <b>y</b> forma
/// (sano ●, tocado ▼, grave ■; UI-002). <see cref="Flash"/> es el destello de perk activado (UI-013): ~1 s
/// de resalte dorado, sin bloquear el resto del dibujo.
/// </summary>
public partial class PlayerStrip : Control
{
    public const float DesignWidth = 232f;
    public const float DesignHeight = 72f;

    private StripModel _model = new(0, string.Empty, string.Empty, 0, PhysicalState.Healthy, false);
    private float _flash;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(DesignWidth, DesignHeight);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetModel(StripModel model)
    {
        _model = model;
        QueueRedraw();
    }

    /// <summary>Destello de ~1 s: un perk se acaba de activar en juego (UI-013).</summary>
    public void Flash()
    {
        _flash = 1f;
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(SetFlashLevel), 1f, 0f, 1.0);
    }

    private void SetFlashLevel(float level)
    {
        _flash = level;
        QueueRedraw();
    }

    public override void _Draw()
    {
        int seed = (_model.Number * 31) + _model.Name.Length;
        bool attenuated = _model.Off || _model.State == PhysicalState.Dead;
        var fill = attenuated ? new Color(Pregon.Vellum, 0.55f) : Pregon.Vellum;
        Pregon.DrawParchment(this, Vector2.Zero, DesignWidth, DesignHeight, fill, Pregon.VellumEdge, seed, amplitude: 1.5f, edgeWidth: 1.5f);

        if (_flash > 0f)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(Pregon.Or, 0.45f * _flash), filled: true);
        }

        var inkName = attenuated ? new Color(Pregon.Sable, 0.55f) : Pregon.Sable;
        var inkSub = attenuated ? new Color(Pregon.InkBrown, 0.55f) : Pregon.InkBrown;

        Pregon.DrawShield(this, new Vector2(10f, 10f), 40f, 50f, ours: true);
        Style.DrawText(this, Pregon.Score, new Vector2(15f, 16f), _model.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), Pregon.SizeDataSmall, attenuated ? new Color(Pregon.Vellum, 0.6f) : Pregon.Vellum);

        Pregon.DrawTextEllipsized(this, Pregon.DataBold, new Vector2(58f, 6f), _model.Name, Pregon.SizeData, inkName, 142f);
        Pregon.DrawTextEllipsized(this, Pregon.DataSemiBold, new Vector2(58f, 36f), UiText.Get("ui.pregon.strip.perks", _model.Subtitle, _model.PerkCount), Pregon.SizeDataSmall, inkSub, 142f);

        DrawStateMark(new Vector2(202f, 36f), attenuated);
    }

    private void DrawStateMark(Vector2 center, bool attenuated)
    {
        Color color = _model.State switch
        {
            PhysicalState.MinorInjury => Pregon.Or,
            PhysicalState.SevereInjury => Pregon.Blood,
            PhysicalState.Dead => new Color("6b6258"),
            _ => new Color("3f7a44"),
        };
        if (attenuated)
        {
            color = new Color(color, 0.6f);
        }

        Vector2[] shape = _model.State switch
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

        if (attenuated)
        {
            Style.DrawDownMark(this, center, 11f, Pregon.Sable);
        }
    }
}

/// <summary>Placa de banquillo («Banquillo N»): mismo material que la tira, sin escudo ni estado.</summary>
public partial class BenchPlaque : Control
{
    public const float DesignWidth = 150f;
    public const float DesignHeight = 72f;

    private int _count;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(DesignWidth, DesignHeight);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetCount(int count)
    {
        _count = count;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Pregon.DrawParchment(this, Vector2.Zero, DesignWidth, DesignHeight, Pregon.Vellum, Pregon.VellumEdge, seed: 500, amplitude: 1.5f, edgeWidth: 1.5f);
        Style.DrawText(this, Pregon.DataBold, new Vector2(10f, 20f), UiText.Get("ui.pregon.bench", _count), Pregon.SizeData, Pregon.Sable, maxWidth: DesignWidth - 20f);
    }
}
