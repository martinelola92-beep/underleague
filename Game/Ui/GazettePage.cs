using Godot;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Ui;

/// <summary>
/// La portada de la Gaceta de fin de run (ADR 0163, RF-122): la run contada como un periódico de humor
/// sobre papel de periódico, con la misma voz visual que <see cref="Newspaper"/> del mapa. No calcula ni
/// decide nada (RT-014): pinta el <see cref="GazetteReport"/> que compone <c>Sim.Run.View.GazetteView</c>,
/// ya con el texto localizado y las variantes elegidas por la semilla de la run.
/// </summary>
public partial class GazettePage : Control
{
    private static readonly Color Newsprint = new("d8d2c2");
    private static readonly Color NewsprintEdge = new("9a8f74");

    private float _headerBottom;
    private float _columnsTop;
    private float _obituariesTop;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        Pregon.DrawParchment(this, Vector2.Zero, Size.X, Size.Y, Newsprint, NewsprintEdge, 41, amplitude: 2.5f);
        if (_headerBottom > 0f)
        {
            DrawLine(new Vector2(18f, _headerBottom), new Vector2(Size.X - 18f, _headerBottom), NewsprintEdge, 1.2f);
        }

        if (_obituariesTop > 0f)
        {
            DrawLine(new Vector2(18f, _obituariesTop), new Vector2(Size.X - 18f, _obituariesTop), NewsprintEdge, 1.2f);
            float middle = Size.X / 2f;
            DrawLine(new Vector2(middle, _columnsTop), new Vector2(middle, _obituariesTop - 8f), NewsprintEdge, 1f);
        }
    }

    /// <summary>Pinta la portada de <paramref name="report"/>: cabecera, titular, MVP, villano y esquelas.</summary>
    public void Bind(GazetteReport report)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        float width = Size.X - 36f;
        var masthead = EssentialLabel.Title(this, report.Masthead, new Vector2(18f, 8f), width, Pregon.Wax, size: 22);
        masthead.HorizontalAlignment = HorizontalAlignment.Center;
        _headerBottom = masthead.Position.Y + masthead.Size.Y + 6f;

        var headline = Widgets.Title(this, report.Headline, new Vector2(18f, _headerBottom + 8f), width, report.Victory ? Style.Text : Style.Hole);
        headline.HorizontalAlignment = HorizontalAlignment.Center;
        var lede = EssentialLabel.Body(this, report.Lede, new Vector2(18f, headline.Position.Y + headline.Size.Y + 4f), width, Style.Text, size: 14);
        lede.HorizontalAlignment = HorizontalAlignment.Center;

        _columnsTop = lede.Position.Y + lede.Size.Y + 12f;
        float columnWidth = (width - 20f) / 2f;
        float leftX = 18f;
        float rightX = 18f + columnWidth + 20f;

        float leftBottom = Column(report.MvpTitle, report.Mvp is null ? null : MvpText(report.Mvp), leftX, columnWidth);
        float rightBottom = report.Villain is null
            ? _columnsTop
            : Column(report.VillainTitle, report.Villain.Line, rightX, columnWidth);
        _obituariesTop = Mathf.Max(leftBottom, rightBottom) + 8f;

        float y = _obituariesTop + 8f;
        var title = EssentialLabel.Title(this, report.ObituariesTitle, new Vector2(18f, y), width, Pregon.Wax, size: 16);
        y += title.Size.Y + 4f;
        if (report.Obituaries.Count == 0)
        {
            EssentialLabel.Body(this, report.ObituariesNone, new Vector2(18f, y), width, Style.TextDim);
        }
        else
        {
            for (int i = 0; i < report.Obituaries.Count; i++)
            {
                var o = report.Obituaries[i];
                var line = EssentialLabel.Title(this, o.Title, new Vector2(18f, y), width, Style.Text);
                y += line.Size.Y;
                var body = EssentialLabel.Body(this, o.Career + " " + o.Epitaph, new Vector2(18f, y), width, Style.TextDim);
                y += body.Size.Y + 5f;
                if (y > Size.Y - 20f)
                {
                    break;
                }
            }
        }

        QueueRedraw();
    }

    private static string MvpText(GazetteMvp mvp) => mvp.Line;

    private float Column(string heading, string? text, float x, float width)
    {
        var title = EssentialLabel.Title(this, heading, new Vector2(x, _columnsTop), width, Pregon.Wax, size: 16);
        float bottom = title.Position.Y + title.Size.Y + 2f;
        if (text is not null)
        {
            var body = EssentialLabel.Body(this, text, new Vector2(x, bottom), width, Style.Text);
            bottom = body.Position.Y + body.Size.Y;
        }

        return bottom;
    }
}
