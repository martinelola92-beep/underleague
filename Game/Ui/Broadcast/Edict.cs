using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Bando de muerte con lacre (N4 de <c>docs/ui/README.md</c> §4): «Se hace saber el fallecimiento de»,
/// nombre y cuerpo, a la izquierda; el sello de lacre en la esquina. Es el segundo tiempo de la muerte —
/// el campo ya contó el primero (cuerpo y mancha) antes de que este bando entre.
/// <para>
/// <b>La forma vive en <c>Edict.tscn</c></b> (regla 10 de <c>CLAUDE.md</c>): la hoja, las líneas y los rótulos
/// son nodos; la trompeta (<see cref="InkTrumpet"/>) y el lacre en estrella (<see cref="InkBurst"/>) siguen
/// por código. Se crea con <see cref="Create"/>.
/// </para>
/// </summary>
[Tool]
public partial class Edict : Control
{
    public const float DesignWidth = 600f;
    public const float DesignHeight = 700f;

    private const string ScenePath = "res://Ui/Broadcast/Edict.tscn";

    private string _name = string.Empty;
    private string _body = string.Empty;

    private bool _bound;
    private Label _said = null!;
    private Label _deathOf = null!;
    private Label _nameLabel = null!;
    private Label _bodyLabel = null!;

    /// <summary>El bando con su escena. La única forma correcta de crearlo.</summary>
    public static Edict Create() => GD.Load<PackedScene>(ScenePath).Instantiate<Edict>();

    public override void _Ready()
    {
        var said = GetNodeOrNull<Label>("%SeHaceSaber");
        if (said is null)
        {
            GD.PushError("Edict sin su escena: créalo con Edict.Create(), no con new.");
            return;
        }

        _said = said;
        _deathOf = GetNode<Label>("%ElFallecimientoDe");
        _nameLabel = GetNode<Label>("%Nombre");
        _bodyLabel = GetNode<Label>("%Texto");
        _bound = true;

        if (Engine.IsEditorHint())
        {
            Show("Mazka Comecráneos", "delantero, n.º 7, orco; rematado por «Sed de tuétano»\nde Grunk, del Rival, al minuto sesenta y tres");
        }
        else
        {
            Visible = false;
        }
    }

    /// <summary>«Se hace saber el fallecimiento de» y «el fallecimiento de» son furniture (ui.pregon.edict.*, RT-035); <paramref name="name"/> y <paramref name="body"/> llegan ya resueltos.</summary>
    public void Show(string name, string body)
    {
        _name = name;
        _body = body;
        Visible = true;
        Refresh();
    }

    private void Refresh()
    {
        if (!_bound)
        {
            return;
        }

        _said.Text = UiText.Get("ui.pregon.edict.said");
        _deathOf.Text = UiText.Get("ui.pregon.edict.deathOf");

        // El nombre baja de tamaño hasta caber en una línea; lo que aun así no quepa se recorta con «…».
        FitText.Fit(_nameLabel, _name);
        _bodyLabel.Text = _body;
    }
}
