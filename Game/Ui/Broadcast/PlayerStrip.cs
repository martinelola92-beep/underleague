using Godot;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Datos de una tira de jugador (docs/ui/README.md §7): escudo con dorsal, nombre, «puesto · raza»,
/// recuento de perks y estado físico. <see cref="Subtitle"/> ya viene compuesto por quien la crea (la
/// plantilla <c>ui.pregon.strip.subtitle</c> vive en <see cref="UiText"/>, no aquí — RT-035).
/// </summary>
/// <param name="Off">Muerto o expulsado: la tira se atenúa y la marca de estado lleva una cruz (UI-002).</param>
/// <param name="Energy">Energía que le queda, 0-100 (ADR 0142), para la barra de fatiga.</param>
public sealed record StripModel(int Number, string Name, string Subtitle, int PerkCount, PhysicalState State, bool Off, int Energy = 100);

/// <summary>
/// Tira de jugador de 232×72 (docs/ui/README.md §7): papel con borde rasgado, escudo con dorsal, nombre en
/// tinta, «puesto · raza» y recuento de perks, una marca de estado físico por color <b>y</b> forma (sano ●,
/// tocado ▼, grave ■; UI-002) y la barra de fatiga. <see cref="Flash"/> es el destello de perk activado
/// (UI-013): ~1 s de resalte dorado, sin bloquear el resto.
/// <para>
/// <b>La forma vive en <c>PlayerStrip.tscn</c></b> (regla 10 de <c>CLAUDE.md</c>; guía en
/// <c>docs/ui/editar-en-godot.md</c>): el código solo rellena los nodos con nombre único. Se crea siempre con
/// <see cref="Create"/>. Atenuada (muerto o expulsado) se enseña el fondo «apagado» en lugar del normal.
/// </para>
/// </summary>
[Tool]
public partial class PlayerStrip : Control
{
    public const float DesignWidth = 232f;
    public const float DesignHeight = 72f;

    private const string ScenePath = "res://Ui/Broadcast/PlayerStrip.tscn";

    private StripModel _model = new(0, string.Empty, string.Empty, 0, PhysicalState.Healthy, false);

    private bool _bound;
    private Control _background = null!;
    private Control _backgroundOff = null!;
    private Control _flash = null!;
    private Label _number = null!;
    private Label _name = null!;
    private Label _subtitle = null!;
    private StripStateMark _stateMark = null!;
    private Control _energy = null!;
    private ProgressBar _energyGreen = null!;
    private ProgressBar _energyGold = null!;
    private ProgressBar _energyBlood = null!;

    /// <summary>La tira con su escena. La única forma correcta de crearla.</summary>
    public static PlayerStrip Create() => GD.Load<PackedScene>(ScenePath).Instantiate<PlayerStrip>();

    public override void _Ready()
    {
        var background = GetNodeOrNull<Control>("%Fondo");
        if (background is null)
        {
            GD.PushError("PlayerStrip sin su escena: créala con PlayerStrip.Create(), no con new.");
            return;
        }

        _background = background;
        _backgroundOff = GetNode<Control>("%FondoApagado");
        _flash = GetNode<Control>("%Destello");
        _number = GetNode<Label>("%Dorsal");
        _name = GetNode<Label>("%Nombre");
        _subtitle = GetNode<Label>("%Subtitulo");
        _stateMark = GetNode<StripStateMark>("%MarcaEstado");
        _energy = GetNode<Control>("%Energia");
        _energyGreen = GetNode<ProgressBar>("%EnergiaVerde");
        _energyGold = GetNode<ProgressBar>("%EnergiaOro");
        _energyBlood = GetNode<ProgressBar>("%EnergiaSangre");
        _bound = true;
        _flash.Visible = false;

        if (Engine.IsEditorHint())
        {
            _model = new StripModel(
                7, "MAZKA COMECRÁNEOS", UiText.Get("ui.pregon.strip.subtitle", UiText.Get("ui.pos.Forward"), "orco"), 2, PhysicalState.MinorInjury, false, 60);
        }

        Refresh();
    }

    public void SetModel(StripModel model)
    {
        _model = model;
        Refresh();
    }

    /// <summary>Destello de ~1 s: un perk se acaba de activar en juego (UI-013).</summary>
    public void Flash()
    {
        SetFlashLevel(1f);
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(SetFlashLevel), 1f, 0f, 1.0);
    }

    private void SetFlashLevel(float level)
    {
        if (!_bound)
        {
            return;
        }

        _flash.Visible = level > 0f;
        _flash.Modulate = new Color(1f, 1f, 1f, level);
    }

    private void Refresh()
    {
        if (!_bound)
        {
            return;
        }

        bool attenuated = _model.Off || _model.State == PhysicalState.Dead;
        _background.Visible = !attenuated;
        _backgroundOff.Visible = attenuated;

        // Atenuada: la tinta pierde fuerza (alfa), no cambia de color; el color es el de la escena.
        float ink = attenuated ? 0.55f : 1f;
        _number.Text = _model.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _number.SelfModulate = new Color(1f, 1f, 1f, attenuated ? 0.6f : 1f);
        _name.Text = _model.Name;
        _name.SelfModulate = new Color(1f, 1f, 1f, ink);
        _subtitle.Text = UiText.Get("ui.pregon.strip.perks", _model.Subtitle, _model.PerkCount);
        _subtitle.SelfModulate = new Color(1f, 1f, 1f, ink);
        _stateMark.SetState(_model.State, attenuated);

        // La barra de fatiga (ADR 0142): verde con más de la mitad, oro hasta un cuarto y sangre por debajo,
        // para ver de un vistazo quién está fundido. Sale de la traza (RT-014): aquí no se calcula nada.
        _energy.Modulate = new Color(1f, 1f, 1f, attenuated ? 0.45f : 1f);
        _energyGreen.Visible = _model.Energy > 50;
        _energyGold.Visible = _model.Energy is <= 50 and > 25;
        _energyBlood.Visible = _model.Energy <= 25;
        double value = Mathf.Clamp(_model.Energy, 0, 100);
        _energyGreen.Value = value;
        _energyGold.Value = value;
        _energyBlood.Value = value;
    }
}
