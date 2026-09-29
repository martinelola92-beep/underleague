using System.Collections.Generic;
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

    [Signal]
    public delegate void SpeedChosenEventHandler(int index);

    [Signal]
    public delegate void PauseToggledEventHandler();

    /// <summary>ADR 0154: el jugador pulsa una de las tres órdenes (0 defensiva, 1 neutra, 2 ofensiva).</summary>
    [Signal]
    public delegate void OrderChosenEventHandler(int index);

    /// <summary>BA-H, RF-082: el jugador pulsa el consumible manual <c>id</c>.</summary>
    [Signal]
    public delegate void ConsumableChosenEventHandler(string id);

    private string _own = string.Empty;
    private string _rival = string.Empty;
    private int _ownScore;
    private int _rivalScore;
    private float _progress;
    private string _rivalResidue = string.Empty;
    private int _speedIndex;
    private bool _paused;

    // Criterio del árbitro (ADR 0158 §6, RF-062, RF-063): SIEMPRE visible, residuo periférico del
    // tablero -no un anuncio del director, por eso vive aquí y no en HeraldBanner/ProclamationBand.
    private int _bias;

    /// <summary>Un "+5"/"−8" en el aire junto al medidor, con su tiempo de vida ya consumido.</summary>
    private readonly record struct BiasFloat(int Value, float Elapsed);

    private const float BiasFloatSeconds = 1.1f;
    private readonly List<BiasFloat> _biasFloats = new();

    private readonly Rect2[] _speedButtons = new Rect2[3];
    private Rect2 _pauseButton;
    private readonly Rect2[] _orderButtons = new Rect2[3];
    private int _orderIndex = 1;

    // ADR 0166: la orden que puso el jugador, distinta de la efectiva mientras dura un grito de orden.
    private int _playerOrderIndex = 1;

    /// <summary>ADR 0166: un grito del entrenador en curso: su nombre, los segundos que le quedan y qué fracción de su duración.</summary>
    public readonly record struct ShoutInfo(string Name, int SecondsLeft, float Fraction);

    private IReadOnlyList<ShoutInfo> _shouts = System.Array.Empty<ShoutInfo>();
    private bool _orderEnabled = true;

    /// <summary>
    /// BA-H, RF-082: un consumible manual equipado, listo para pulsar. <see cref="Used"/> y
    /// <see cref="Enabled"/> son independientes a propósito —el botón se apaga por las dos razones
    /// (RF-085 "se consumen al usarse" y las mismas condiciones que la orden táctica: partido en marcha,
    /// nada pendiente de decidir)— para que el tablero pueda distinguirlas en el rótulo.
    /// </summary>
    public readonly record struct ConsumableButtonInfo(string Id, string ShortName, string Tooltip, bool Used, bool Enabled);

    private IReadOnlyList<ConsumableButtonInfo> _consumables = System.Array.Empty<ConsumableButtonInfo>();
    private Rect2[] _consumableButtons = System.Array.Empty<Rect2>();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0f, DesignHeight);
        MouseFilter = MouseFilterEnum.Stop;

        // _Process solo corre mientras haya un texto flotante vivo (ShowBiasDelta lo reactiva): el
        // tablero no necesita reloj propio para nada más, todo lo demás llega por setter.
        SetProcess(false);
    }

    /// <summary>Anima y expira los "+N"/"−N" del criterio (RF-063); a cualquier velocidad de reproducción, como el resto del residuo del tablero.</summary>
    public override void _Process(double delta)
    {
        for (int i = _biasFloats.Count - 1; i >= 0; i--)
        {
            float elapsed = _biasFloats[i].Elapsed + (float)delta;
            if (elapsed >= BiasFloatSeconds)
            {
                _biasFloats.RemoveAt(i);
            }
            else
            {
                _biasFloats[i] = _biasFloats[i] with { Elapsed = elapsed };
            }
        }

        if (_biasFloats.Count == 0)
        {
            SetProcess(false);
        }

        QueueRedraw();
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

    /// <summary>
    /// La orden táctica con la que juega el equipo (0 defensiva, 1 neutra, 2 ofensiva) y si se puede cambiar
    /// ahora. <paramref name="playerIndex"/> es la que puso el jugador: distinta de <paramref name="index"/>
    /// sólo mientras un grito de orden manda (ADR 0166), y es a la que se vuelve al acabar.
    /// </summary>
    public void SetOrder(int index, bool enabled, int? playerIndex = null)
    {
        _orderIndex = Mathf.Clamp(index, 0, 2);
        _playerOrderIndex = Mathf.Clamp(playerIndex ?? index, 0, 2);
        _orderEnabled = enabled;
        QueueRedraw();
    }

    /// <summary>ADR 0166: los gritos del entrenador en curso, con su cuenta atrás; vacío si no hay ninguno.</summary>
    public void SetShouts(IReadOnlyList<ShoutInfo> shouts)
    {
        _shouts = shouts;
        QueueRedraw();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        QueueRedraw();
    }

    /// <summary>
    /// BA-H, RF-082: los consumibles manuales equipados, en el orden en que se resolverían si dos se
    /// dispararan a la vez (mismo orden que <c>RunEquipment.ForMatch</c>). Vacío si no hay ninguno —el
    /// tablero no reserva sitio si no hay nada que pulsar.
    /// </summary>
    public void SetConsumables(IReadOnlyList<ConsumableButtonInfo> consumables)
    {
        _consumables = consumables;
        QueueRedraw();
    }

    /// <summary>Criterio actual del árbitro, −100..100 (RF-062): el medidor siempre visible del tablero.</summary>
    public void SetBias(int bias)
    {
        _bias = Mathf.Clamp(bias, -100, 100);
        QueueRedraw();
    }

    /// <summary>Un desplazamiento del criterio (RF-063): un "+N"/"−N" breve junto al medidor. Sin efecto si <paramref name="delta"/> es 0.</summary>
    public void ShowBiasDelta(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        _biasFloats.Add(new BiasFloat(delta, 0f));
        SetProcess(true);
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
            return;
        }

        for (int i = 0; i < _orderButtons.Length; i++)
        {
            if (_orderEnabled && _orderButtons[i].HasPoint(button.Position))
            {
                EmitSignal(SignalName.OrderChosen, i);
                AcceptEvent();
                return;
            }
        }

        for (int i = 0; i < _consumableButtons.Length; i++)
        {
            if (_consumables[i].Enabled && _consumableButtons[i].HasPoint(button.Position))
            {
                EmitSignal(SignalName.ConsumableChosen, _consumables[i].Id);
                AcceptEvent();
                return;
            }
        }
    }

    /// <summary>
    /// Tooltip por posición (BA-H): el nombre corto del consumible ya lo dice el rótulo del botón, así
    /// que aquí va su descripción generada (RT-035) — la única forma de leerla entera sin abrir la
    /// pantalla de Equipo. Vacío fuera de un botón, para no tapar nada del resto del tablero.
    /// </summary>
    public override string _GetTooltip(Vector2 atPosition)
    {
        for (int i = 0; i < _consumableButtons.Length; i++)
        {
            if (_consumableButtons[i].HasPoint(atPosition))
            {
                return _consumables[i].Tooltip;
            }
        }

        return string.Empty;
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

        // Nombres centrados pegados al marcador (revisión del revisor, 20 sep 2026): un solo bloque
        // [paño propio][cifra propia][cifra rival][paño rival], centrado en la franja — ya no en los
        // bordes. Los botones de velocidad, a la derecha, quedan lejos de sobra del bloque (no hace falta
        // reservarles sitio: a 1920 de ancho el bloque nunca llega tan lejos).
        const float PanelWidth = 260f;
        const float ScoreWidth = 72f;
        const float PanelScoreGap = 16f;
        float blockWidth = (2f * PanelWidth) + (2f * ScoreWidth) + (2f * PanelScoreGap);
        float blockX = (w - blockWidth) / 2f;
        float ownPanelX = blockX;
        float ownScoreX = ownPanelX + PanelWidth + PanelScoreGap;
        float rivalScoreX = ownScoreX + ScoreWidth;
        float rivalPanelX = rivalScoreX + ScoreWidth + PanelScoreGap;

        DrawTeamPanel(new Vector2(ownPanelX, 10f), ours: true, _own);
        DrawTeamPanel(new Vector2(rivalPanelX, 10f), ours: false, _rival);
        DrawScorePlate(new Vector2(ownScoreX, 8f), _ownScore, seed: 10);
        DrawScorePlate(new Vector2(rivalScoreX, 8f), _rivalScore, seed: 11);

        if (!string.IsNullOrEmpty(_rivalResidue))
        {
            // Junto a su paño (C3), ahora a la derecha del paño rival en vez de "cerca del borde": el paño
            // ya no vive en el borde.
            var at = new Vector2(rivalPanelX + PanelWidth + 12f, 74f);
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
        DrawOrderButtons();
        DrawConsumableButtons();
        DrawShouts();

        // Criterio del árbitro (RF-062, RF-063, ADR 0158 §6): en el hueco entre la orden táctica y el
        // bloque de equipos -328 a blockX-, siempre a la vista, nunca un anuncio del director.
        DrawCriterionMeter(328f + 16f, blockX - 16f);
        DrawBiasFloats(328f + 16f, blockX - 16f);
    }

    /// <summary>
    /// El medidor de criterio (RF-062): −100..100, con color <b>y</b> forma (UI-002) — el marcador
    /// apunta hacia arriba a favor y hacia abajo en contra, además de cambiar entre oro y sangre, así
    /// que un jugador que no distinga los dos rojos y no vea el número igual lee la dirección.
    /// </summary>
    private void DrawCriterionMeter(float left, float right)
    {
        float plaqueW = System.MathF.Max(140f, right - left);
        const float PlaqueY = 14f;
        const float PlaqueH = 46f;
        Pregon.DrawParchment(this, new Vector2(left, PlaqueY), plaqueW, PlaqueH, new Color("4a3321"), Pregon.Sable, seed: 40, amplitude: 1.2f, edgeWidth: 2f);

        string label = UiText.Get("ui.pregon.board.bias", UiText.Signed(_bias));
        Style.DrawText(this, Pregon.DataBold, new Vector2(left + 10f, PlaqueY + 4f), label, Pregon.SizeDataSmall, Pregon.Vellum, plaqueW - 20f);

        float trackX = left + 10f;
        float trackW = plaqueW - 20f;
        float trackY = PlaqueY + 32f;
        const float TrackH = 6f;
        DrawRect(new Rect2(trackX, trackY, trackW, TrackH), Pregon.Sable);

        float centerX = trackX + (trackW / 2f);
        DrawLine(new Vector2(centerX, trackY - 3f), new Vector2(centerX, trackY + TrackH + 3f), Pregon.Vellum, 1.5f);

        float markerX = centerX + ((_bias / 100f) * (trackW / 2f));
        var fillColor = _bias > 0 ? Pregon.Or : _bias < 0 ? Pregon.Blood : Pregon.Vellum;

        if (_bias != 0)
        {
            float fillX0 = System.MathF.Min(centerX, markerX);
            float fillX1 = System.MathF.Max(centerX, markerX);
            DrawRect(new Rect2(fillX0, trackY, fillX1 - fillX0, TrackH), fillColor);
        }

        const float TriSize = 6f;
        var triangle = _bias >= 0
            ? new[]
            {
                new Vector2(markerX, trackY - TriSize - 2f),
                new Vector2(markerX - TriSize, trackY - 2f),
                new Vector2(markerX + TriSize, trackY - 2f),
            }
            : new[]
            {
                new Vector2(markerX, trackY + TrackH + TriSize + 2f),
                new Vector2(markerX - TriSize, trackY + TrackH + 2f),
                new Vector2(markerX + TriSize, trackY + TrackH + 2f),
            };
        DrawColoredPolygon(triangle, fillColor);
    }

    /// <summary>
    /// Los "+N"/"−N" del criterio (RF-063): oro a favor, sangre en contra, suben y se apagan solos en
    /// <see cref="BiasFloatSeconds"/>. Residuo periférico del tablero, no una voz alta del director
    /// (docs/ui/README §2.2): no pausa nada ni compite con un estandarte.
    /// </summary>
    private void DrawBiasFloats(float left, float right)
    {
        if (_biasFloats.Count == 0)
        {
            return;
        }

        float centerX = left + ((right - left) / 2f);
        for (int i = 0; i < _biasFloats.Count; i++)
        {
            var entry = _biasFloats[i];
            float t = Mathf.Clamp(entry.Elapsed / BiasFloatSeconds, 0f, 1f);
            var color = entry.Value >= 0 ? Pregon.Or : Pregon.Blood;
            color.A = 1f - t;
            var at = new Vector2(centerX - 18f, 6f - (t * 16f));
            Style.DrawText(this, Pregon.Score, at, UiText.Signed(entry.Value), Pregon.SizeDataSmall, color);
        }
    }

    /// <summary>
    /// ADR 0154: la botonera de la orden táctica, a la izquierda del tablero, simétrica a la de velocidad.
    /// La vigente, en oro; apagada cuando no se puede cambiar (partido terminado).
    /// </summary>
    private void DrawOrderButtons()
    {
        string[] labels =
        {
            UiText.Get("ui.pregon.order.defensive"), UiText.Get("ui.pregon.order.neutral"), UiText.Get("ui.pregon.order.offensive"),
        };
        float bw = 96f, bh = 46f, gap = 8f;
        float x = 24f;
        for (int i = 0; i < 3; i++)
        {
            _orderButtons[i] = new Rect2(x, 14f, bw, bh);
            bool active = i == _orderIndex;
            var fill = active ? Pregon.Or : new Color("4a3321");
            if (!_orderEnabled && !active)
            {
                fill = fill.Darkened(0.35f);
            }

            Pregon.DrawParchment(this, new Vector2(x, 14f), bw, bh, fill, Pregon.Sable, seed: 30 + i, amplitude: 1.2f, edgeWidth: 2f);

            // ADR 0166: mientras un grito manda, la orden a la que se vuelve al acabar lleva un aro de oro.
            if (i == _playerOrderIndex && _playerOrderIndex != _orderIndex)
            {
                DrawRect(new Rect2(x + 2f, 16f, bw - 4f, bh - 4f), Pregon.Or, filled: false, width: 2f);
            }

            Style.DrawText(this, Pregon.DataBold, new Vector2(x + 10f, 14f + 12f), labels[i], Pregon.SizeDataSmall, active ? Pregon.Sable : Pregon.Vellum, maxWidth: bw - 20f);
            x += bw + gap;
        }
    }

    /// <summary>
    /// BA-H, RF-082: los consumibles manuales equipados, debajo de la botonera de orden (mismo bloque
    /// izquierdo, misma anchura). Como mucho tres —RF-080, el máximo de slots equipados— aunque la
    /// pantalla de Equipo hoy limita a uno solo a la vez (invariante de <c>ConsumablesPanel</c>, no de
    /// <c>/Sim</c>). El botón dorado hasta que se pulsa; ya usado o sin poder pulsarlo ahora, apagado.
    /// </summary>
    private void DrawConsumableButtons()
    {
        if (_consumableButtons.Length != _consumables.Count)
        {
            _consumableButtons = new Rect2[_consumables.Count];
        }

        if (_consumables.Count == 0)
        {
            return;
        }

        // El nombre tiene que caber entero: un consumible cortado a «Venda…» no se reconoce (revisión de
        // capturas). Usado, sigue diciendo cuál era.
        float bw = 270f, bh = 34f, gap = 8f;
        float x = 24f;
        const float Y = 64f;
        for (int i = 0; i < _consumables.Count; i++)
        {
            var info = _consumables[i];
            _consumableButtons[i] = new Rect2(x, Y, bw, bh);
            var fill = info.Used ? new Color("4a3321").Darkened(0.5f) : info.Enabled ? Pregon.Or : new Color("4a3321").Darkened(0.35f);
            Pregon.DrawParchment(this, new Vector2(x, Y), bw, bh, fill, Pregon.Sable, seed: 50 + i, amplitude: 1.2f, edgeWidth: 2f);
            string label = info.Used ? UiText.Get("ui.pregon.consumable.usedLabel", info.ShortName) : info.ShortName;
            var textColor = info.Used ? Pregon.Vellum.Darkened(0.3f) : info.Enabled ? Pregon.Sable : Pregon.Vellum;
            Pregon.DrawTextEllipsized(this, Pregon.DataBold, new Vector2(x + 10f, Y + 5f), label, Pregon.SizeDataSmall, textColor, bw - 16f);
            x += bw + gap;
        }
    }

    /// <summary>
    /// ADR 0166: el grito del entrenador en curso, en la fila de los consumibles y justo tras ellos (el
    /// consumible que lo gritó se ve «· usado» a su izquierda), con el nombre, los segundos que le quedan y
    /// una barra que se vacía. Es la cuenta atrás que dice hasta cuándo el equipo juega distinto.
    /// </summary>
    private void DrawShouts()
    {
        const float Bw = 250f, Bh = 34f, Gap = 8f, Y = 64f;
        float x = 24f + (_consumables.Count * (270f + Gap));
        for (int i = 0; i < _shouts.Count; i++)
        {
            var shout = _shouts[i];
            Pregon.DrawParchment(this, new Vector2(x, Y), Bw, Bh, Pregon.Azur, Pregon.Sable, seed: 60 + i, amplitude: 1.2f, edgeWidth: 2f);
            string label = UiText.Get("ui.pregon.shout.active", shout.Name, shout.SecondsLeft);
            Pregon.DrawTextEllipsized(this, Pregon.DataBold, new Vector2(x + 10f, Y + 4f), label, Pregon.SizeDataSmall, Pregon.Vellum, Bw - 20f);
            float trackW = Bw - 20f;
            DrawRect(new Rect2(x + 10f, Y + Bh - 8f, trackW, 4f), Pregon.Sable);
            DrawRect(new Rect2(x + 10f, Y + Bh - 8f, trackW * Mathf.Clamp(shout.Fraction, 0f, 1f), 4f), Pregon.Or);
            x += Bw + Gap;
        }
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
        Pregon.DrawTextEllipsized(this, Pregon.Titular, new Vector2(textX, at.Y + 12f), name, Pregon.SizeHeader, Pregon.Vellum, 196f);
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
