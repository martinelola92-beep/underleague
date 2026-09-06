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

    /// <summary>
    /// True en la pantalla de Partido: la leyenda deja de hablar de zonas de colocación y pasa a explicar
    /// las fichas del campo —equipo, balón, poseedor— y el <b>estado</b> de cada jugador (RT-089c), que es
    /// lo que hay que poder leer sin esfuerzo para ver el comportamiento. Ocupa dos filas.
    /// </summary>
    public bool MatchMode { get; set; }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        float x = 0f;

        if (MatchMode)
        {
            DrawMatch(font);
            return;
        }

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

    /// <summary>
    /// Dos filas: arriba quién es quién en el campo, abajo los once estados de la máquina de estados del
    /// jugador. Las filas se rellenan de izquierda a derecha y saltan solas cuando se acaba el ancho.
    /// </summary>
    private void DrawMatch(Font font)
    {
        float x = Dot(font, 0f, 3f, Style.TeamOwn, Style.TeamOwn, UiText.Get("ui.match.legendOwn"));
        x = Dot(font, x, 3f, Style.TeamRival, Style.Background, UiText.Get("ui.match.legendRival"));
        x = Dot(font, x, 3f, Style.Ball, Style.Ball, UiText.Get("ui.match.legendBall"), small: true);
        x = Dot(font, x, 3f, Style.TeamOwn, Style.Carrier, UiText.Get("ui.match.legendCarrier"));
        x = Swatch(font, x, Style.ZoneFill, Style.ZoneEdge, false, UiText.Get("ui.match.legendZone"));

        // Las dos capas de marcaje: punteada la asignación de la posesión, continua el que está yendo a
        // por su par ahora mismo (ADR 0022). Se distinguen por trazo, no por intensidad (UI-002).
        x = Line(font, x, 4f, new Color(Style.TeamOwn, 0.55f), true, UiText.Get("ui.match.legendMark"));
        Line(font, x, 4f, new Color(Style.TeamOwn, 0.9f), false, UiText.Get("ui.match.legendMarking"));

        // Orden de lectura, no orden del enum: Blocking va al final de PlayerState para no mover los
        // valores de los estados anteriores (ADR 0030 §2), pero en la leyenda su sitio es al lado de
        // Tackling, que es la acción de la que es gemela.
        x = 0f;
        foreach (var state in MatchStates)
        {
            string label = UiText.Get("ui.pstate." + state);
            float width = 30f + font.GetStringSize(label, HorizontalAlignment.Left, -1f, Style.TextSmall).X + 14f;
            if (x + width > Size.X && x > 0f)
            {
                return;
            }

            x = Dot(font, x, 25f, Style.Panel, Style.Of(state), label);
        }
    }

    /// <summary>Los once estados del jugador en el orden en el que se leen, no en el del enum.</summary>
    private static readonly Underleague.Sim.Engine.PlayerState[] MatchStates =
    {
        Underleague.Sim.Engine.PlayerState.Positioning,
        Underleague.Sim.Engine.PlayerState.Chasing,
        Underleague.Sim.Engine.PlayerState.Dribbling,
        Underleague.Sim.Engine.PlayerState.Passing,
        Underleague.Sim.Engine.PlayerState.Shooting,
        Underleague.Sim.Engine.PlayerState.Tackling,
        Underleague.Sim.Engine.PlayerState.Blocking,
        Underleague.Sim.Engine.PlayerState.KnockedDown,
        Underleague.Sim.Engine.PlayerState.Injured,
        Underleague.Sim.Engine.PlayerState.Celebrating,
        Underleague.Sim.Engine.PlayerState.SentOff,
    };

    /// <summary>Muestra circular: relleno de equipo o de balón, anillo del estado, y su etiqueta al lado.</summary>
    private float Dot(Font font, float x, float y, Color fill, Color ring, string label, bool small = false)
    {
        var center = new Vector2(x + 9f, y + 8f);
        float radius = small ? 4f : 8f;
        DrawCircle(center, radius, fill);
        DrawArc(center, radius + 2f, 0f, Mathf.Tau, 20, ring, 2f);
        Style.DrawText(this, font, new Vector2(x + 24f, y + 1f), label, Style.TextSmall, Style.TextDim);
        return x + 30f + font.GetStringSize(label, HorizontalAlignment.Left, -1f, Style.TextSmall).X + 14f;
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

    private float Line(Font font, float x, Color color, bool dashed, string label) =>
        Line(font, x, 12f, color, dashed, label);

    /// <summary>Muestra de línea a una altura dada: la leyenda del partido tiene sus filas más juntas.</summary>
    private float Line(Font font, float x, float y, Color color, bool dashed, string label)
    {
        var a = new Vector2(x, y + 7f);
        var b = new Vector2(x + 22f, y + 7f);
        if (dashed)
        {
            Style.DrawDashed(this, a, b, color, 2.5f, 3f);
        }
        else
        {
            DrawLine(a, b, color, 2.5f);
        }

        Style.DrawText(this, font, new Vector2(x + 28f, y), label, Style.TextSmall, Style.TextDim);
        return x + 34f + font.GetStringSize(label, HorizontalAlignment.Left, -1f, Style.TextSmall).X + 22f;
    }
}
