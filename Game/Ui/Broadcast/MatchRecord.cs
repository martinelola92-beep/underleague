using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Acta del encuentro (N4 de <c>docs/ui/README.md</c> §4): los dos escudos, los nombres, el resultado y un
/// pie de crónica. Se muestra al final del partido, sobre un velo que apaga el campo por debajo (el
/// partido ya terminó; lo que queda a la vista es residuo, no juego).
/// <para>
/// <b>La forma vive en <c>MatchRecord.tscn</c></b> (regla 10 de <c>CLAUDE.md</c>): velo, hoja, líneas y rótulos
/// son nodos y los escudos son <see cref="InkShield"/>. La hoja es de 820×560 centrada en la pantalla.
/// Se crea con <see cref="Create"/>.
/// </para>
/// </summary>
[Tool]
public partial class MatchRecord : Control
{
    private const string ScenePath = "res://Ui/Broadcast/MatchRecord.tscn";

    private string _own = string.Empty;
    private string _rival = string.Empty;
    private int _ownScore;
    private int _rivalScore;
    private string _footer = string.Empty;

    private bool _bound;
    private Label _title = null!;
    private Label _scoreLabel = null!;
    private Label _winner = null!;
    private Label _footerLabel = null!;

    /// <summary>El acta con su escena. La única forma correcta de crearla.</summary>
    public static MatchRecord Create() => GD.Load<PackedScene>(ScenePath).Instantiate<MatchRecord>();

    public override void _Ready()
    {
        var title = GetNodeOrNull<Label>("%Titulo");
        if (title is null)
        {
            GD.PushError("MatchRecord sin su escena: créala con MatchRecord.Create(), no con new.");
            return;
        }

        _title = title;
        _scoreLabel = GetNode<Label>("%Resultado");
        _winner = GetNode<Label>("%Ganador");
        _footerLabel = GetNode<Label>("%Pie");
        _bound = true;

        if (Engine.IsEditorHint())
        {
            Show("Altos Hornos FC", "Yunque Verde", 1, 0, "Goles de Mazka · un expulsado · un muerto\nLa crónica completa, en la gaceta de mañana");
        }
        else
        {
            Visible = false;
        }
    }

    public void Show(string ownTeam, string rivalTeam, int ownScore, int rivalScore, string footer)
    {
        _own = ownTeam;
        _rival = rivalTeam;
        _ownScore = ownScore;
        _rivalScore = rivalScore;
        _footer = footer;
        Visible = true;
        Refresh();
    }

    private void Refresh()
    {
        if (!_bound)
        {
            return;
        }

        FitText.Fit(_title, UiText.Get("ui.pregon.record.title"));
        _scoreLabel.Text = $"{_ownScore} — {_rivalScore}";
        _winner.Text = _ownScore >= _rivalScore
            ? UiText.Get("ui.pregon.record.winner", _own, _rival)
            : UiText.Get("ui.pregon.record.winner", _rival, _own);
        _footerLabel.Text = _footer;
    }
}
