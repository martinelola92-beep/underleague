using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Banda de pregón a lo ancho (N3 de <c>docs/ui/README.md</c> §4): turba y árbitro que abandona el campo,
/// <b>sin congelar</b> el partido. Es la única presentación de N3 que no es un gonfalón vertical: se lee de
/// un vistazo mientras el mundo sigue corriendo por debajo.
/// <para>
/// <b>La forma vive en <c>ProclamationBand.tscn</c></b> (regla 10 de <c>CLAUDE.md</c>): fondo, filetes y
/// rótulos son nodos; la trompeta y el lacre de la tirada del destino (<see cref="FateSeal"/>) siguen por
/// código. Se crea con <see cref="Create"/>.
/// </para>
/// </summary>
[Tool]
public partial class ProclamationBand : Control
{
    public const float DesignHeight = 100f;

    private const string ScenePath = "res://Ui/Broadcast/ProclamationBand.tscn";

    private string _header = string.Empty;
    private string _body = string.Empty;

    // ADR 0171, la tirada del destino: el sello de lacre que gira a la derecha de la banda. Sin él (turba,
    // consumible) la banda es la de siempre.
    private bool _fate;
    private string _sealText = string.Empty;
    private float _sealAngle;
    private FateOutcome _outcome;

    private bool _bound;
    private Label _headerLabel = null!;
    private Label _bodyLabel = null!;
    private Control _seal = null!;
    private FateSeal _sealDraw = null!;
    private Label _sealPercent = null!;
    private Label _sealResult = null!;

    /// <summary>La banda con su escena. La única forma correcta de crearla.</summary>
    public static ProclamationBand Create() => GD.Load<PackedScene>(ScenePath).Instantiate<ProclamationBand>();

    public override void _Ready()
    {
        var header = GetNodeOrNull<Label>("%Cabecera");
        if (header is null)
        {
            GD.PushError("ProclamationBand sin su escena: créala con ProclamationBand.Create(), no con new.");
            return;
        }

        _headerLabel = header;
        _bodyLabel = GetNode<Label>("%Texto");
        _seal = GetNode<Control>("%Sello");
        _sealDraw = GetNode<FateSeal>("%Lacre");
        _sealPercent = GetNode<Label>("%Porcentaje");
        _sealResult = GetNode<Label>("%Resultado");
        _bound = true;

        if (Engine.IsEditorHint())
        {
            ShowFate(UiText.Get("ui.pregon.fate.header"), "La muerte tira los dados · 23 %", "23 %", 1f, FateOutcome.Saved);
        }
        else
        {
            Visible = false;
        }
    }

    /// <summary>Cómo acabó la tirada del destino que enseña el sello.</summary>
    public enum FateOutcome
    {
        /// <summary>Los dados aún ruedan: el sello gira.</summary>
        Rolling,

        /// <summary>Se ha salvado: el sello se para con un aspa verde... es decir, con la marca de «a salvo».</summary>
        Saved,

        /// <summary>Ha caído la desgracia: el sello se para con la marca de sangre.</summary>
        Hit,
    }

    public void Show(string header, string body)
    {
        _header = header;
        _body = body;
        _fate = false;
        Visible = true;
        Refresh();
    }

    /// <summary>
    /// La tirada del destino (ADR 0171): el pregón con el porcentaje real y el sello que gira. Se vuelve a llamar
    /// cada fotograma que cambie algo (<paramref name="spin"/> 0..1 mientras rueda, luego el resultado).
    /// </summary>
    public void ShowFate(string header, string body, string sealText, float spin, FateOutcome outcome)
    {
        _header = header;
        _body = body;
        _fate = true;
        _sealText = sealText;

        // Tres vueltas y media que frenan al final (ease-out cúbico): un sello que se para, no que se corta.
        float eased = 1f - Mathf.Pow(1f - Mathf.Clamp(spin, 0f, 1f), 3f);
        _sealAngle = eased * Mathf.Tau * 3.5f;
        _outcome = outcome;
        Visible = true;
        Refresh();
    }

    private void Refresh()
    {
        if (!_bound)
        {
            return;
        }

        _headerLabel.Text = _header;
        _bodyLabel.Text = _body;
        _seal.Visible = _fate;
        if (!_fate)
        {
            return;
        }

        _sealDraw.SetState(_sealAngle, _outcome);

        // Rodando enseña el porcentaje; al parar, la marca de «a salvo» (✓) o de desgracia (✗), más grande.
        bool rolling = _outcome == FateOutcome.Rolling;
        _sealPercent.Visible = rolling;
        _sealResult.Visible = !rolling;
        _sealPercent.Text = _sealText;
        _sealResult.Text = _outcome == FateOutcome.Saved ? "✓" : "✗";
    }
}
