using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// La pista del medidor de criterio del árbitro (RF-062, ADR 0158 §6) y los «+N»/«−N» que sueltan sus
/// desplazamientos (RF-063). Es la única parte del tablero que sigue dibujada por código: es un indicador
/// que se mueve con el dato, no un fondo. La placa que la rodea y el rótulo sí son nodos de la escena.
/// <para>
/// Color <b>y</b> forma (UI-002): el marcador apunta arriba a favor y abajo en contra además de cambiar
/// entre oro y sangre. La pista se dibuja a 10 px del borde superior del nodo; el triángulo y los textos
/// flotantes salen de su rectángulo a propósito.
/// </para>
/// </summary>
[Tool]
[GlobalClass]
public partial class BiasTrack : Control
{
    private const float TrackY = 10f;
    private const float TrackH = 6f;
    private const float FloatSeconds = 1.1f;

    private readonly record struct BiasFloat(int Value, float Elapsed);

    private readonly List<BiasFloat> _floats = new();
    private int _bias;

    /// <summary>Criterio actual, −100..100.</summary>
    public int Bias
    {
        get => _bias;
        set
        {
            _bias = Mathf.Clamp(value, -100, 100);
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        // Solo corre mientras haya un texto flotante vivo (AddFloat lo reactiva).
        SetProcess(false);
    }

    /// <summary>Un desplazamiento del criterio: un «+N»/«−N» breve sobre la pista. Sin efecto si es 0.</summary>
    public void AddFloat(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        _floats.Add(new BiasFloat(delta, 0f));
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        for (int i = _floats.Count - 1; i >= 0; i--)
        {
            float elapsed = _floats[i].Elapsed + (float)delta;
            if (elapsed >= FloatSeconds)
            {
                _floats.RemoveAt(i);
            }
            else
            {
                _floats[i] = _floats[i] with { Elapsed = elapsed };
            }
        }

        if (_floats.Count == 0)
        {
            SetProcess(false);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float trackW = Size.X;
        DrawRect(new Rect2(0f, TrackY, trackW, TrackH), Pregon.Sable);

        float centerX = trackW / 2f;
        DrawLine(new Vector2(centerX, TrackY - 3f), new Vector2(centerX, TrackY + TrackH + 3f), Pregon.Vellum, 1.5f);

        float markerX = centerX + ((_bias / 100f) * (trackW / 2f));
        var fillColor = _bias > 0 ? Pregon.Or : _bias < 0 ? Pregon.Blood : Pregon.Vellum;
        if (_bias != 0)
        {
            float x0 = System.MathF.Min(centerX, markerX);
            float x1 = System.MathF.Max(centerX, markerX);
            DrawRect(new Rect2(x0, TrackY, x1 - x0, TrackH), fillColor);
        }

        const float Tri = 6f;
        var triangle = _bias >= 0
            ? new[]
            {
                new Vector2(markerX, TrackY - Tri - 2f), new Vector2(markerX - Tri, TrackY - 2f), new Vector2(markerX + Tri, TrackY - 2f),
            }
            : new[]
            {
                new Vector2(markerX, TrackY + TrackH + Tri + 2f), new Vector2(markerX - Tri, TrackY + TrackH + 2f), new Vector2(markerX + Tri, TrackY + TrackH + 2f),
            };
        DrawColoredPolygon(triangle, fillColor);

        // Residuo periférico, no una voz del director (docs/ui/README §2.2): sube por encima de la placa y se apaga.
        for (int i = 0; i < _floats.Count; i++)
        {
            var entry = _floats[i];
            float t = Mathf.Clamp(entry.Elapsed / FloatSeconds, 0f, 1f);
            var color = entry.Value >= 0 ? Pregon.Or : Pregon.Blood;
            color.A = 1f - t;
            Style.DrawText(this, Pregon.Score, new Vector2(centerX - 18f, -30f - (t * 16f)), UiText.Signed(entry.Value), Pregon.SizeDataSmall, color);
        }
    }
}
