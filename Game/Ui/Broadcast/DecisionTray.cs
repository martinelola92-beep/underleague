using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>Quién sale: su puesto y la casilla en la que estaba (docs/ui/README.md §7, BA-I/BB-F).</summary>
public sealed record OutgoingModel(int PlayerId, string Name, string Position, string Square, string State);

/// <summary>
/// Un candidato a entrar: nombre, puesto y estado; <see cref="Recommended"/> marca al sugerido.
/// </summary>
/// <param name="Risk">
/// Qué le pasa a ESTE si recibe la entrada de un perk letal rival en la casilla que queda libre, ya
/// formateado (ADR 0134 C), o vacío si no hay riesgo. Es lo que convierte la elección en una decisión en
/// vez de en un sorteo: el jugador ya veía puesto y estado, pero no lo que arriesgaba al meterlo.
/// </param>
public sealed record CandidateModel(int PlayerId, string Name, string Position, string State, bool Recommended, string Risk);

/// <summary>
/// Una respuesta que no es «entra este» (ADR 0134): dejar el hueco o que el lesionado siga jugando. Va en
/// la misma fila que los candidatos y se navega igual, porque es la misma decisión —a quién expongo— y no
/// un menú aparte.
/// </summary>
/// <param name="Kind"><c>decline</c> o <c>playOn</c>; lo usa la pantalla para saber qué se eligió.</param>
/// <param name="Risk">
/// Lo que arriesga esta respuesta, ya formateado, o vacío. Existe porque «que siga jugando» cuesta mucho
/// más que su subtítulo: el que se queda tocado multiplica su probabilidad de morir (ADR 0134 E). Sin este
/// número la bandeja enseñaría el riesgo de los suplentes y callaría el de la opción que más arriesga,
/// que es justo al revés de lo que hace falta para decidir.
/// </param>
public sealed record TrayOption(string Kind, string Label, string Sub, string Risk = "");

/// <summary>
/// Bandeja de decisión (docs/ui/README.md §7): sustituye a la fila de tiras cuando hay que decidir quién
/// entra — el campo queda visible al 100%, sin tapar nada más. Dice quién sale con su puesto y su casilla,
/// lista los candidatos marcando el recomendado, y se elige con ratón (clic en un candidato o en
/// «Confirmar») y con teclado (flechas para mover la selección, Intro para confirmar).
/// <para>
/// <b>La forma vive en <c>DecisionTray.tscn</c></b> (regla 10 de <c>CLAUDE.md</c>): el papel, el título, la ayuda,
/// la flecha y el botón «Confirmar» son nodos, y cada casilla es una escena (<see cref="TraySlot"/>). El código
/// solo crea las casillas de la fila, reparte su ancho y rellena los textos. Se crea con <see cref="Create"/>.
/// </para>
/// </summary>
[Tool]
public partial class DecisionTray : Control
{
    // 124, no 180 (campo en perspectiva, 19 sep 2026): con el borde cercano del césped anclado a y≈690 de
    // 1280×800 (1035 lógicos), una bandeja de 180 tapaba la línea de banda, un jugador y el balón. La bandeja
    // sustituye a las tiras y el campo tiene que verse al 100 % (docs/ui/README §7): título y fila de
    // casillas se juntan, sin cambiar el contenido.
    public const float DesignHeight = 124f;

    private const string ScenePath = "res://Ui/Broadcast/DecisionTray.tscn";

    [Signal]
    public delegate void ChosenEventHandler(int playerId);

    /// <summary>El jugador eligió una respuesta que no es un candidato: <c>decline</c> o <c>playOn</c>.</summary>
    [Signal]
    public delegate void OptionChosenEventHandler(string kind);

    private OutgoingModel? _outgoing;
    private IReadOnlyList<CandidateModel> _candidates = System.Array.Empty<CandidateModel>();
    private IReadOnlyList<TrayOption> _options = System.Array.Empty<TrayOption>();
    private int _selected;

    private bool _bound;
    private Label _caption = null!;
    private Label _hint = null!;
    private Label _arrow = null!;
    private TraySlot _outgoingSlot = null!;
    private Container _row = null!;
    private Button _confirm = null!;
    private readonly List<TraySlot> _slots = new();

    /// <summary>La bandeja con su escena. La única forma correcta de crearla.</summary>
    public static DecisionTray Create() => GD.Load<PackedScene>(ScenePath).Instantiate<DecisionTray>();

    public override void _Ready()
    {
        var caption = GetNodeOrNull<Label>("%Titulo");
        if (caption is null)
        {
            GD.PushError("DecisionTray sin su escena: créala con DecisionTray.Create(), no con new.");
            return;
        }

        _caption = caption;
        _hint = GetNode<Label>("%Ayuda");
        _arrow = GetNode<Label>("%Flecha");
        _outgoingSlot = GetNode<TraySlot>("%Saliente");
        _row = GetNode<Container>("%Fila");
        _confirm = GetNode<Button>("%Confirmar");
        FocusMode = FocusModeEnum.All;

        // Mismo comportamiento que antes: el botón responde al pulsar, no al soltar, y no roba el foco al
        // teclado (las flechas y Intro los atiende la bandeja).
        _confirm.FocusMode = FocusModeEnum.None;
        _confirm.ActionMode = BaseButton.ActionModeEnum.Press;
        _confirm.Pressed += Confirm;
        _bound = true;

        _caption.Text = UiText.Get("ui.pregon.tray.caption");
        _hint.Text = UiText.Get("ui.pregon.tray.hint");
        _arrow.Text = UiText.Get("ui.pregon.tray.arrow");
        _confirm.Text = UiText.Get("ui.pregon.tray.confirm");

        if (Engine.IsEditorHint())
        {
            FillSample();
        }
        else
        {
            Visible = false;
        }
    }

    /// <summary>Lo que enseña el editor: una sustitución inventada, para ver la bandeja ocupada.</summary>
    private void FillSample()
    {
        _outgoing = new OutgoingModel(7, "Mazka", UiText.Get("ui.pos.Forward"), "D4", UiText.Get("ui.state.MinorInjury"));
        _candidates = new[]
        {
            new CandidateModel(8, "Brakk", UiText.Get("ui.pos.Forward"), UiText.Get("ui.state.Healthy"), Recommended: true, UiText.Get("ui.pregon.tray.candidateRisk", "1,4 %")),
            new CandidateModel(9, "Narg", UiText.Get("ui.pos.Midfielder"), UiText.Get("ui.state.MinorInjury"), Recommended: false, UiText.Get("ui.pregon.tray.candidateRisk", "6,2 %")),
        };
        _options = new[]
        {
            new TrayOption("decline", UiText.Get("ui.pregon.tray.decline"), UiText.Get("ui.pregon.tray.declineSub")),
            new TrayOption("playOn", UiText.Get("ui.pregon.tray.playOn"), UiText.Get("ui.pregon.tray.playOnSub"), UiText.Get("ui.pregon.tray.candidateRisk", "23,7 %")),
        };
        Refresh();
    }

    public void SetOutgoing(OutgoingModel outgoing)
    {
        _outgoing = outgoing;
        Visible = true;
        Refresh();
    }

    public void SetCandidates(IReadOnlyList<CandidateModel> candidates, IReadOnlyList<TrayOption>? options = null)
    {
        _candidates = candidates;
        _options = options ?? System.Array.Empty<TrayOption>();
        _selected = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Recommended)
            {
                _selected = i;
                break;
            }
        }

        Visible = true;
        CallDeferred(Control.MethodName.GrabFocus);
        Refresh();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key)
        {
            if ((key.Keycode == Key.Left || key.Keycode == Key.Right) && Choices > 0)
            {
                int delta = key.Keycode == Key.Left ? -1 : 1;
                _selected = ((_selected + delta) % Choices + Choices) % Choices;
                Refresh();
                AcceptEvent();
            }
            else if (key.Keycode is Key.Enter or Key.KpEnter)
            {
                Confirm();
                AcceptEvent();
            }
        }
    }

    /// <summary>Candidatos más respuestas que no son candidatos: todo se navega en la misma fila.</summary>
    private int Choices => _candidates.Count + _options.Count;

    private void Select(int index)
    {
        _selected = index;
        Refresh();
    }

    private void Confirm()
    {
        if (_selected < 0 || _selected >= Choices)
        {
            return;
        }

        if (_selected < _candidates.Count)
        {
            EmitSignal(SignalName.Chosen, _candidates[_selected].PlayerId);
            return;
        }

        EmitSignal(SignalName.OptionChosen, _options[_selected - _candidates.Count].Kind);
    }

    private void Refresh()
    {
        if (!_bound)
        {
            return;
        }

        _outgoingSlot.Visible = _outgoing is not null;
        if (_outgoing is { } outgoing)
        {
            string outName = UiText.Get("ui.pregon.tray.outLabel", outgoing.Name);
            string outSub = UiText.Get("ui.pregon.tray.outSub", outgoing.Position, outgoing.Square, outgoing.State);
            _outgoingSlot.Set(TraySlotKind.Outgoing, outName, outSub, string.Empty, recommended: false, selected: false);
        }

        // ADR 0134: la fila dejó de ser «los candidatos» para ser «las respuestas», y pueden llegar a cinco
        // (tres suplentes, dejar el hueco y seguir jugando). Se reparte el hueco disponible en vez de
        // suponerlo: la bandeja no puede crecer hacia abajo (DesignHeight tapa el campo). El ancho sale de
        // la fila de la escena, no de una constante: mover o estirar la fila reparte distinto.
        int choices = Choices;
        float gap = _row.GetThemeConstant("separation");
        float slotWidth = choices > 0
            ? Mathf.Clamp((_row.Size.X - (gap * (choices - 1))) / choices, 120f, 260f)
            : 0f;

        while (_slots.Count > choices)
        {
            _slots[^1].QueueFree();
            _slots.RemoveAt(_slots.Count - 1);
        }

        while (_slots.Count < choices)
        {
            int index = _slots.Count;
            var slot = TraySlot.Create();
            slot.Clicked += () => Select(index);
            _row.AddChild(slot);
            _slots.Add(slot);
        }

        for (int i = 0; i < choices; i++)
        {
            var slot = _slots[i];
            slot.CustomMinimumSize = new Vector2(slotWidth, TraySlot.DesignHeight);
            if (i < _candidates.Count)
            {
                var c = _candidates[i];
                string sub = c.Recommended
                    ? UiText.Get("ui.pregon.tray.candidateSubRecommended", c.Position, c.State, UiText.Get("ui.pregon.tray.recommended"))
                    : UiText.Get("ui.pregon.tray.candidateSub", c.Position, c.State);
                slot.Set(TraySlotKind.Candidate, c.Name, sub, c.Risk, c.Recommended, i == _selected);
            }
            else
            {
                var option = _options[i - _candidates.Count];
                slot.Set(TraySlotKind.Option, option.Label, option.Sub, option.Risk, recommended: false, selected: i == _selected);
            }
        }
    }
}
