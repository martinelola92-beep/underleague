using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Tablero de madera de la franja superior (docs/ui/README.md §7): paños heráldicos con el nombre de cada
/// equipo, dos placas de resultado, la placa de residuo del rival junto a su nombre (C3), la barra de
/// progreso del partido y los botones de velocidad x1/x4/x16 y pausa.
/// <para>
/// Se dibuja entero en <see cref="_Draw"/> a partir del <see cref="Control.Size"/> que le dé quien lo
/// coloque — no asume un ancho fijo — y expone su estado con setters simples: quien lo usa (la galería
/// hoy, el director de partido más adelante) no toca ni un <see cref="Label"/> ni un <see cref="Button"/>
/// por dentro.
/// </para>
/// </summary>
public partial class BroadcastBoard : Control
{
    /// <summary>Alto de diseño: tablero, placas colgantes y barra de progreso, sin la grada.</summary>
    public const float DesignHeight = 116f;

    /// <summary>Ancho reservado en el borde derecho a los cuatro botones de velocidad y pausa.</summary>
    private const float SpeedZoneWidth = 336f;

    [Signal]
    public delegate void SpeedChosenEventHandler(int index);

    [Signal]
    public delegate void PauseToggledEventHandler();

    private string _own = string.Empty;
    private string _rival = string.Empty;
    private int _ownScore;
    private int _rivalScore;
    private float _progress;
    private string _rivalResidue = string.Empty;
    private int _speedIndex;
    private bool _paused;

    private readonly Rect2[] _speedButtons = new Rect2[3];
    private Rect2 _pauseButton;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0f, DesignHeight);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void SetTeams(string own, string rival)
    {
        _own = own;
        _rival = rival;
        QueueRedraw();
    }

    public void SetScore(int own, int rival)
    {
        _ownScore = own;
        _rivalScore = rival;
        QueueRedraw();
    }

    public void SetProgress(float t)
    {
        _progress = Mathf.Clamp(t, 0f, 1f);
        QueueRedraw();
    }

    public void SetRivalResidue(string text)
    {
        _rivalResidue = text;
        QueueRedraw();
    }

    public void SetSpeedIndex(int i)
    {
        _speedIndex = Mathf.Clamp(i, 0, 2);
        QueueRedraw();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button)
        {
            return;
        }

        for (int i = 0; i < _speedButtons.Length; i++)
        {
            if (_speedButtons[i].HasPoint(button.Position))
            {
                EmitSignal(SignalName.SpeedChosen, i);
                AcceptEvent();
                return;
            }
        }

        if (_pauseButton.HasPoint(button.Position))
        {
            EmitSignal(SignalName.PauseToggled);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        float w = Size.X;
        float boardHeight = 68f;
        Pregon.DrawParchment(this, new Vector2(0f, 6f), w, boardHeight, new Color("4a3321"), Pregon.Sable, seed: 1, amplitude: 2f, edgeWidth: 2.5f);
        for (int i = 1; i < 4; i++)
        {
            float y = 6f + (i * 17f);
            DrawLine(new Vector2(8f, y), new Vector2(w - 8f, y + Pregon.Jitter(400 + i, 2f)), new Color("3a2718"), 1.5f);
        }

        // El paño rival deja sitio a los botones de velocidad (SpeedZoneWidth) — si se pegan al borde
        // derecho como el propio, los botones lo tapan por encima (BB-broadcast, visto en la galería).
        float rivalX = w - 260f - SpeedZoneWidth;
        DrawTeamPanel(new Vector2(12f, 10f), ours: true, _own);
        DrawTeamPanel(new Vector2(rivalX, 10f), ours: false, _rival);
        DrawScorePlate(new Vector2((w / 2f) - 84f, 8f), _ownScore, seed: 10);
        DrawScorePlate(new Vector2((w / 2f) + 12f, 8f), _rivalScore, seed: 11);

        if (!string.IsNullOrEmpty(_rivalResidue))
        {
            var at = new Vector2(rivalX + 152f, 74f);
            Pregon.DrawParchment(this, at, 108f, 26f, Pregon.Vellum, Pregon.VellumEdge, seed: 12, amplitude: 1f, edgeWidth: 1.5f);
            Style.DrawText(this, Pregon.DataBold, at + new Vector2(6f, 3f), _rivalResidue, Pregon.SizeDataSmall, Pregon.Gules);
        }

        float barY = 90f;
        float barWidth = System.Math.Min(560f, w - 320f);
        var barPos = new Vector2((w - barWidth) / 2f, barY);
        Pregon.DrawParchment(this, barPos, barWidth, 8f, new Color("4a3321"), Pregon.Sable, seed: 13, amplitude: 1f, edgeWidth: 1.5f);
        if (_progress > 0f)
        {
            DrawColoredPolygon(new[]
            {
                barPos, barPos + new Vector2(barWidth * _progress, 0f),
                barPos + new Vector2(barWidth * _progress, 8f), barPos + new Vector2(0f, 8f),
            }, Pregon.Or);
        }

        DrawSpeedButtons(w);
    }

    private void DrawTeamPanel(Vector2 at, bool ours, string name)
    {
        var pts = Pregon.Swallowtail(260f, 66f, 12f);
        var shifted = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            shifted[i] = pts[i] + at;
        }

        DrawColoredPolygon(shifted, ours ? Pregon.Azur : Pregon.Gules);
        var closed = new Vector2[pts.Length + 1];
        System.Array.Copy(shifted, closed, pts.Length);
        closed[pts.Length] = shifted[0];
        DrawPolyline(closed, Pregon.Sable, 2f, true);
        Pregon.DrawOrla(this, pts, at, 6f, Pregon.Or);

        float shieldX = ours ? at.X + 14f : at.X + 260f - 48f;
        Pregon.DrawShield(this, new Vector2(shieldX, at.Y + 6f), 34f, 42f, ours);

        float textX = ours ? at.X + 50f : at.X + 12f;
        Pregon.DrawTextEllipsized(this, Pregon.Fell, new Vector2(textX, at.Y + 12f), name, Pregon.SizeHeader, Pregon.Vellum, 196f);
    }

    private void DrawScorePlate(Vector2 at, int score, int seed)
    {
        Pregon.DrawParchment(this, at, 72f, 84f, Pregon.Vellum, Pregon.VellumEdge, seed, amplitude: 1.5f, edgeWidth: 2f);
        Style.DrawText(this, Pregon.Score, at + new Vector2(18f, 12f), score.ToString(System.Globalization.CultureInfo.InvariantCulture), Pregon.SizeTitleSmall, Pregon.Sable);
    }

    private void DrawSpeedButtons(float w)
    {
        string[] labels = { UiText.Get("ui.pregon.speed.x1"), UiText.Get("ui.pregon.speed.x4"), UiText.Get("ui.pregon.speed.x16") };
        float bw = 64f, bh = 46f, gap = 8f;
        float x = w - 24f - bw;
        _pauseButton = new Rect2(x, 14f, bw, bh);
        Pregon.DrawParchment(this, new Vector2(x, 14f), bw, bh, _paused ? Pregon.Or : new Color("4a3321"), Pregon.Sable, seed: 20, amplitude: 1.2f, edgeWidth: 2f);
        Style.DrawText(this, Pregon.DataBold, new Vector2(x + 20f, 14f + 12f), UiText.Get("ui.pregon.speed.pause"), Pregon.SizeDataSmall, _paused ? Pregon.Sable : Pregon.Vellum);

        for (int i = 2; i >= 0; i--)
        {
            x -= bw + gap;
            _speedButtons[i] = new Rect2(x, 14f, bw, bh);
            bool active = i == _speedIndex && !_paused;
            Pregon.DrawParchment(this, new Vector2(x, 14f), bw, bh, active ? Pregon.Or : new Color("4a3321"), Pregon.Sable, seed: 21 + i, amplitude: 1.2f, edgeWidth: 2f);
            Style.DrawText(this, Pregon.DataBold, new Vector2(x + 12f, 14f + 12f), labels[i], Pregon.SizeDataSmall, active ? Pregon.Sable : Pregon.Vellum);
        }
    }
}
