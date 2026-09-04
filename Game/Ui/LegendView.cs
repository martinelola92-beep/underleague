using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Leyenda del campo. Existe porque la ADR 0029 exige que las dos capas se distingan <b>por forma</b> y
/// no solo por color (UI-002): la leyenda enseña las dos muestras con su borde, continuo y punteado con
/// trama, para que la lectura no dependa de percibir dos tonos de azul.
/// </summary>
public partial class LegendView : Control
{
    /// <summary>True cuando la pantalla está en modo de cobertura: la leyenda cambia de contenido.</summary>
    public bool CoverageMode { get; set; }

    /// <summary>True mientras se mueve a un jugador: se añaden las muestras de vínculo creado y roto.</summary>
    public bool Moving { get; set; }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        float x = 0f;

        if (CoverageMode)
        {
            x = Swatch(font, x, Style.CoverageColor(1, 3), Style.CoverageColor(1, 3), false, UiText.Get("ui.team.coverageLegend") + " 1");
            x = Swatch(font, x, Style.CoverageColor(3, 3), Style.CoverageColor(3, 3), false, UiText.Get("ui.team.coverageLegend") + " 3+");
            Swatch(font, x, new Color(Style.Hole, 0.32f), Style.Hole, true, UiText.Get("ui.team.coverageHole"));
            return;
        }

        x = Swatch(font, x, Style.ZoneFill, Style.ZoneEdge, false, UiText.Get("ui.team.legendZone"));
        x = Swatch(font, x, Style.MarginFill, Style.MarginEdge, true, UiText.Get("ui.team.legendMargin"));
        x = Line(font, x, Style.LinkLine, false, UiText.Get("ui.team.legendLink"));

        if (Moving)
        {
            x = Line(font, x, Style.LinkCreated, false, UiText.Get("ui.team.legendCreated"));
            Line(font, x, Style.LinkBroken, true, UiText.Get("ui.team.legendBroken"));
        }
    }

    private float Swatch(Font font, float x, Color fill, Color edge, bool dashed, string label)
    {
        var rect = new Rect2(x, 4f, 22f, 16f);
        DrawRect(rect, fill);
        if (dashed)
        {
            Style.DrawHatch(this, rect, new Color(edge, 0.45f), 5f);
            Style.DrawDashed(this, rect.Position, rect.Position + new Vector2(rect.Size.X, 0f), edge, 2f, 3f);
            Style.DrawDashed(this, rect.Position + new Vector2(0f, rect.Size.Y), rect.Position + rect.Size, edge, 2f, 3f);
        }
        else
        {
            DrawRect(rect, edge, false, 2f);
        }

        Style.DrawText(this, font, new Vector2(x + 28f, 5f), label, Style.TextSmall, Style.TextDim);
        return x + 34f + font.GetStringSize(label, HorizontalAlignment.Left, -1f, Style.TextSmall).X + 22f;
    }

    private float Line(Font font, float x, Color color, bool dashed, string label)
    {
        var a = new Vector2(x, 12f);
        var b = new Vector2(x + 22f, 12f);
        if (dashed)
        {
            Style.DrawDashed(this, a, b, color, 2.5f, 3f);
        }
        else
        {
            DrawLine(a, b, color, 2.5f);
        }

        Style.DrawText(this, font, new Vector2(x + 28f, 5f), label, Style.TextSmall, Style.TextDim);
        return x + 34f + font.GetStringSize(label, HorizontalAlignment.Left, -1f, Style.TextSmall).X + 22f;
    }
}
