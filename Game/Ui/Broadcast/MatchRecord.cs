using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Acta del encuentro (N4 de <c>docs/ui/README.md</c> §4): los dos escudos, los nombres, el resultado y un
/// pie de crónica. Se muestra al final del partido, sobre un velo que apaga el campo por debajo (el
/// partido ya terminó; lo que queda a la vista es residuo, no juego).
/// </summary>
public partial class MatchRecord : Control
{
    private string _own = string.Empty;
    private string _rival = string.Empty;
    private int _ownScore;
    private int _rivalScore;
    private string _footer = string.Empty;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    public void Show(string ownTeam, string rivalTeam, int ownScore, int rivalScore, string footer)
    {
        _own = ownTeam;
        _rival = rivalTeam;
        _ownScore = ownScore;
        _rivalScore = rivalScore;
        _footer = footer;
        Visible = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0f, 0f, 0f, 0.55f), filled: true);

        float sw = System.Math.Min(820f, size.X - 80f);
        float sh = System.Math.Min(560f, size.Y - 80f);
        var sheetAt = new Vector2((size.X - sw) / 2f, (size.Y - sh) / 2f);
        Pregon.DrawParchment(this, sheetAt, sw, sh, Pregon.Vellum, Pregon.VellumEdge, seed: 950, amplitude: 3.5f, edgeWidth: 2f);
        DrawLine(sheetAt + new Vector2(60f, 110f), sheetAt + new Vector2(sw - 60f, 110f), Pregon.InkBrown, 1.5f);
        DrawLine(sheetAt + new Vector2(60f, 116f), sheetAt + new Vector2(sw - 60f, 116f), Pregon.InkBrown, 1f);

        Pregon.DrawFittedTitle(this, Pregon.Fell, sheetAt + new Vector2(0f, 24f), UiText.Get("ui.pregon.record.title"), Pregon.SizeTitleSmall - 6, Pregon.Sable, sw);

        Pregon.DrawShield(this, sheetAt + new Vector2((sw / 2f) - 300f, 150f), 110f, 138f, ours: true);
        Pregon.DrawShield(this, sheetAt + new Vector2((sw / 2f) + 190f, 150f), 110f, 138f, ours: false);
        string score = $"{_ownScore} — {_rivalScore}";
        Pregon.DrawTextCentered(this, Pregon.Score, sheetAt + new Vector2((sw / 2f) - 170f, 130f), score, Pregon.SizeTitleSmall, Pregon.Sable, 340f);

        string line = _ownScore >= _rivalScore
            ? UiText.Get("ui.pregon.record.winner", _own, _rival)
            : UiText.Get("ui.pregon.record.winner", _rival, _own);
        Pregon.DrawTextCentered(this, Pregon.Fell, sheetAt + new Vector2(0f, 320f), line, Pregon.SizeHeader, Pregon.Azur, sw);
        Pregon.DrawWrappedText(this, Pregon.FellItalic, sheetAt + new Vector2(0f, 380f), _footer, Pregon.SizeBody, Pregon.InkBrown, sw, centered: true);
    }
}
