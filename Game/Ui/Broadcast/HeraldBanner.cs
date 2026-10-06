using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Estandarte de pregón (N3 de <c>docs/ui/README.md</c> §4): un <b>pergamino colgado</b> —boceto del
/// revisor, 20 sep 2026—, no un gonfalón de tela: dos varales de madera arriba y abajo (rollos con los
/// cabos sobresaliendo), cuerpo de pergamino claro con cintas y filete en el color del equipo protagonista
/// (azur y oro el propio, gules y sable el rival), escudo con trompetas cruzadas en la cabecera y una
/// corona pequeña al pie. «Se hace saber», título («GOL», dominando la composición), cuerpo en cursiva y
/// pie. Cubre gol, roja y lesión grave — los tres comparten formato, solo cambian el color de las cintas y
/// los cuatro textos, que llegan ya resueltos por quien lo muestra (RT-035: nada de texto de efecto
/// escrito a mano aquí). Su forma vive en <c>HeraldBanner.tscn</c> (regla 10 de <c>CLAUDE.md</c>): el código solo rellena los textos y enseña el grupo del equipo protagonista (<c>%Propio</c> o <c>%Rival</c>: cintas, escudo, corona y títulos de su color). Se crea con <see cref="Create"/>.
/// La posición (lado contrario al suceso) la decide <c>BroadcastScreen.PositionBanner</c>, sin cambios.
/// </summary>
[Tool]
public partial class HeraldBanner : Control
{
    public const float DesignWidth = 500f;
    public const float DesignHeight = 640f;

    private const string ScenePath = "res://Ui/Broadcast/HeraldBanner.tscn";

    private bool _ours;
    private string _header = string.Empty;
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _footer = string.Empty;

    private bool _bound;
    private Control _oursGroup = null!;
    private Control _rivalGroup = null!;
    private Label _headerLabel = null!;
    private Label _bodyLabel = null!;
    private Label _footerLabel = null!;

    /// <summary>El estandarte con su escena. La única forma correcta de crearlo.</summary>
    public static HeraldBanner Create() => GD.Load<PackedScene>(ScenePath).Instantiate<HeraldBanner>();

    public override void _Ready()
    {
        var ours = GetNodeOrNull<Control>("%Propio");
        if (ours is null)
        {
            GD.PushError("HeraldBanner sin su escena: créalo con HeraldBanner.Create(), no con new.");
            return;
        }

        _oursGroup = ours;
        _rivalGroup = GetNode<Control>("%Rival");
        _headerLabel = GetNode<Label>("%Cabecera");
        _bodyLabel = GetNode<Label>("%Texto");
        _footerLabel = GetNode<Label>("%Pie");
        _bound = true;

        if (Engine.IsEditorHint())
        {
            Show(ours: true, UiText.Get("ui.pregon.banner.said"), "Gol", "de Mazka Comecráneos,\nal minuto sesenta y tres", "¡Viva Nuestro F. C.!");
        }
        else
        {
            Visible = false;
        }
    }

    public void Show(bool ours, string header, string title, string body, string footer)
    {
        _ours = ours;
        _header = header;
        _title = title;
        _body = body;
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

        // El grupo del equipo protagonista: cintas, filete, escudo, corona y títulos en su color.
        _oursGroup.Visible = _ours;
        _rivalGroup.Visible = !_ours;
        var group = _ours ? _oursGroup : _rivalGroup;

        // «GOL» con el peso del boceto del revisor: ~55% del ancho del pergamino y a tamaño grande; los
        // títulos de más de 4 letras usan el rótulo pequeño y todo el ancho de la columna de texto.
        bool shortTitle = _title.Length <= 4;
        var big = group.GetNode<Label>("Titulo");
        var small = group.GetNode<Label>("TituloLargo");
        big.Visible = shortTitle;
        small.Visible = !shortTitle;
        if (shortTitle)
        {
            FitText.Fit(big, _title, group.GetNode<Control>("Cuerpo").Size.X * 0.55f);
        }
        else
        {
            FitText.Fit(small, _title);
        }

        FitText.Fit(_headerLabel, _header);
        _bodyLabel.Text = _body;
        FitText.Fit(_footerLabel, _footer);
    }
}
