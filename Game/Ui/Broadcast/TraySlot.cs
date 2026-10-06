using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>De qué es la casilla de la bandeja: quién sale, un candidato o una respuesta que no es un candidato.</summary>
public enum TraySlotKind
{
    Outgoing,
    Candidate,
    Option,
}

/// <summary>
/// Una casilla de la <see cref="DecisionTray"/> (docs/ui/README.md §7): el que sale, un candidato o una respuesta
/// («dejar el hueco», «que siga jugando»). Su forma vive en <c>TraySlot.tscn</c> (regla 10 de <c>CLAUDE.md</c>):
/// el papel de cada tipo y el aro de selección son nodos y el código solo enseña el que toca y rellena los
/// textos. Una casilla por elección; la bandeja las crea con <see cref="Create"/> y las coloca en su fila.
/// </summary>
[Tool]
public partial class TraySlot : Control
{
    public const float DesignHeight = 76f;

    private const string ScenePath = "res://Ui/Broadcast/TraySlot.tscn";

    /// <summary>El jugador pulsó la casilla (solo las que se pueden elegir reaccionan).</summary>
    [Signal]
    public delegate void ClickedEventHandler();

    private bool _bound;
    private Control _paperOutgoing = null!;
    private Control _paperCandidate = null!;
    private Control _paperOption = null!;
    private Control _ring = null!;
    private Control _shield = null!;
    private Label _name = null!;
    private Label _sub = null!;
    private Label _subRecommended = null!;
    private Label _risk = null!;

    /// <summary>La casilla con su escena. La única forma correcta de crearla.</summary>
    public static TraySlot Create() => GD.Load<PackedScene>(ScenePath).Instantiate<TraySlot>();

    public override void _Ready()
    {
        var name = GetNodeOrNull<Label>("%Nombre");
        if (name is null)
        {
            GD.PushError("TraySlot sin su escena: créala con TraySlot.Create(), no con new.");
            return;
        }

        _name = name;
        _paperOutgoing = GetNode<Control>("%FondoSaliente");
        _paperCandidate = GetNode<Control>("%FondoCandidato");
        _paperOption = GetNode<Control>("%FondoOpcion");
        _ring = GetNode<Control>("%Seleccion");
        _shield = GetNode<Control>("%Escudo");
        _sub = GetNode<Label>("%Subtitulo");
        _subRecommended = GetNode<Label>("%SubtituloRecomendado");
        _risk = GetNode<Label>("%Riesgo");
        _bound = true;

        if (Engine.IsEditorHint())
        {
            Set(TraySlotKind.Candidate, "Brakk", "delantero · sano · recomendado", "riesgo 1,4 %", recommended: true, selected: true);
        }
    }

    /// <summary>
    /// Rellena la casilla. <paramref name="third"/> es la línea de riesgo (ADR 0134), en el rojo del daño: vacía
    /// si no hay riesgo. Las respuestas que no son candidatos no llevan escudo.
    /// </summary>
    public void Set(TraySlotKind kind, string name, string sub, string third, bool recommended, bool selected)
    {
        if (!_bound)
        {
            return;
        }

        _paperOutgoing.Visible = kind == TraySlotKind.Outgoing;
        _paperCandidate.Visible = kind == TraySlotKind.Candidate;
        _paperOption.Visible = kind == TraySlotKind.Option;
        _shield.Visible = kind != TraySlotKind.Option;
        _ring.Visible = selected;
        MouseFilter = kind == TraySlotKind.Outgoing ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;

        _name.Text = name;
        _sub.Visible = !recommended;
        _subRecommended.Visible = recommended;
        _sub.Text = sub;
        _subRecommended.Text = sub;

        // El riesgo va en su propia línea: es el dato por el que se cambia de opinión, y perdido dentro del
        // subtítulo no se lee (RF-012c, RF-012d).
        _risk.Visible = third.Length > 0;
        _risk.Text = third;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            EmitSignal(SignalName.Clicked);
            AcceptEvent();
        }
    }
}
