using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Ui.Broadcast;

namespace Underleague.Game.Ui.Knavall;

/// <summary>
/// Cabecera de la pantalla de Equipo (ADR 0162): un travesaño de madera del que cuelgan los banderines y
/// las pestañas —placas de torneo, no botones—, el escudo del club y su nombre en un cartel de papel
/// rasgado. Avisa hacia fuera de la pestaña pulsada y de «volver».
/// </summary>
public partial class TeamHeader : InkCanvas
{
    /// <summary>Alto de la cabecera a 1280x800.</summary>
    public const float HeaderHeight = 92f;

    private const float TabTop = 30f;

    private readonly List<PlaqueButton> _tabs = new();
    private string _race = string.Empty;
    private string _club = string.Empty;
    private string _count = string.Empty;
    private int _active;

    /// <summary>Se ha pulsado la pestaña de índice dado.</summary>
    public event Action<int>? TabPressed;

    /// <summary>Se ha pulsado VOLVER (solo existe con una run en curso).</summary>
    public event Action? BackPressed;

    /// <summary>Crea las pestañas. <paramref name="tabs"/>: rótulo, icono y cartel de ayuda de cada una.</summary>
    public void Setup(IReadOnlyList<(string Caption, Glyph Glyph, Tip Tip)> tabs, bool withBack)
    {
        const float Width = 158f;
        const float Gap = 8f;
        float right = withBack ? 1150f : 1266f;
        float x = right - (tabs.Count * (Width + Gap)) + Gap;
        for (int i = 0; i < tabs.Count; i++)
        {
            var (caption, glyph, tip) = tabs[i];
            var button = PlaqueButton.Create(this, caption, glyph, PlaqueKind.Tab, new Rect2(x, TabTop, Width, 56f), 200 + i);
            button.Tip = tip;
            button.FontSize = 20;
            int index = i;
            button.Pressed += () => TabPressed?.Invoke(index);
            _tabs.Add(button);
            x += Width + Gap;
        }

        if (withBack)
        {
            var back = PlaqueButton.Create(this, UiText.Get("ui.kn.back"), Glyph.Back, PlaqueKind.Wood, new Rect2(1160f, TabTop, 112f, 56f), 250);
            back.Tip = new Tip(UiText.Get("ui.kn.back"), UiText.Get("ui.kn.backTip"), Glyph.Back);
            back.FontSize = 18;
            back.Pressed += () => BackPressed?.Invoke();
        }

        Active = _active;
    }

    /// <summary>Nombre de raza en grande, club debajo y el recuento de plantilla.</summary>
    public void Bind(string race, string club, int players)
    {
        _race = race.ToUpperInvariant();
        _club = club.ToUpperInvariant();
        _count = UiText.Get("ui.kn.headerCount", players);
        QueueRedraw();
    }

    public int Active
    {
        get => _active;
        set
        {
            _active = value;
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Active = i == value;
                _tabs[i].Position = new Vector2(_tabs[i].Position.X, i == value ? TabTop + 2f : TabTop);
                _tabs[i].Size = new Vector2(_tabs[i].Size.X, i == value ? 60f : 54f);
            }
        }
    }

    public override void _Draw()
    {
        ClearZones();

        // Travesaño de madera a lo ancho, con cuerdas hacia cada placa.
        Ink.Plank(this, new Rect2(-8f, 4f, Size.X + 16f, 22f), 71, Ink.Wood);
        foreach (var tab in _tabs)
        {
            DrawLine(new Vector2(tab.Position.X + 26f, 22f), new Vector2(tab.Position.X + 30f, tab.Position.Y + 8f), Ink.Black, 3f, true);
            DrawLine(new Vector2(tab.Position.X + tab.Size.X - 34f, 22f), new Vector2(tab.Position.X + tab.Size.X - 38f, tab.Position.Y + 8f), Ink.Black, 3f, true);
        }

        // Banderines entre el cartel del club y las pestañas: la feria del torneo, con medida.
        float first = _tabs.Count > 0 ? _tabs[0].Position.X : Size.X;
        var colors = new[] { Ink.Red, Ink.Ochre, Ink.Green, Ink.Black, Ink.Red, Ink.Paper };
        float start = 462f;
        int flags = Mathf.Max(0, (int)((first - start - 8f) / 30f));
        for (int i = 0; i < flags; i++)
        {
            float x = start + (i * 30f);
            float drop = 34f + Pregon.Jitter(i + 5, 6f);
            Ink.Poly(this, new[] { new Vector2(x, 22f), new Vector2(x + 26f, 22f), new Vector2(x + 13f, 22f + drop) }, colors[i % colors.Length], 2.4f);
        }

        // Cartel del club: papel rasgado, algo torcido, con el escudo clavado encima.
        Pregon.Tilted(this, new Vector2(252f, 48f), Mathf.DegToRad(-1.5f), () =>
        {
            var rect = new Rect2(-180f, -42f, 370f, 88f);
            Ink.Slab(this, rect, Ink.Paper, 404, 2.6f, 3.5f, new Vector2(5f, 6f));
            int nameSize = Ink.FitSize(Ink.Display, _race, Ink.SizeHuge, 270f, 26);
            Ink.Text(this, Ink.Display, new Vector2(-104f, -46f), _race, nameSize, Ink.Black);
            Ink.Text(this, Ink.Heavy, new Vector2(-102f, 0f), Ink.Fit(Ink.Heavy, _club, 18, 270f), 18, Ink.Brown);
            Ink.Text(this, Ink.Data, new Vector2(-102f, 20f), _count, 14, Ink.Muted);
        });

        // Escudo: nuestro equipo es azur y oro (docs/ui/README.md §1).
        var shieldAt = new Vector2(18f, 6f);
        var poly = Ink.Shift(Pregon.ShieldPoly(74f, 86f), shieldAt);
        DrawColoredPolygon(Ink.Shift(poly, new Vector2(4f, 5f)), Ink.Shadow);
        Pregon.DrawShield(this, shieldAt, 74f, 86f, ours: true);
        DrawPolyline(Ink.Closed(poly), Ink.Black, 4f, true);
        Pregon.DrawOrla(this, Pregon.ShieldPoly(74f, 86f), shieldAt, 6f, Ink.Paper, 1.6f);
        Ink.Nail(this, shieldAt + new Vector2(37f, 10f));
    }
}
