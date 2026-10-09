using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Tablero de madera de la franja superior (docs/ui/README.md §7): paños heráldicos con el nombre de cada
/// equipo, dos placas de resultado, la placa de residuo del rival junto a su nombre (C3), la barra de
/// progreso del partido, el medidor de criterio, la orden táctica, los consumibles, los gritos y los
/// botones de velocidad x1/x4/x16 y pausa.
/// <para>
/// <b>La forma vive en <c>BroadcastBoard.tscn</c>, no aquí</b> (piloto del 4 oct 2026, CLAUDE.md regla 10):
/// cada pieza es un nodo de la escena que el revisor mueve, reestiliza o cambia por su sprite en el editor
/// de Godot. Este script solo rellena los nodos con nombre único (<c>%Nombre</c>) con textos y estados, así
/// que cualquier nodo que se añada a la escena sin uno de esos nombres es decoración y el código no lo
/// toca. Guía en <c>docs/ui/editar-en-godot.md</c>.
/// </para>
/// <para>
/// Se crea siempre con <see cref="Create"/>: un <c>new BroadcastBoard()</c> no tiene escena y se queda vacío.
/// <c>[Tool]</c> para que el editor lo enseñe relleno con datos de ejemplo.
/// </para>
/// </summary>
[Tool]
public partial class BroadcastBoard : Control
{
    /// <summary>Alto de diseño: tablero, placas colgantes y barra de progreso, sin la grada.</summary>
    public const float DesignHeight = 116f;

    private const string ScenePath = "res://Ui/Broadcast/BroadcastBoard.tscn";

    [Signal]
    public delegate void SpeedChosenEventHandler(int index);

    [Signal]
    public delegate void PauseToggledEventHandler();

    /// <summary>ADR 0154: el jugador pulsa una de las tres órdenes (0 defensiva, 1 neutra, 2 ofensiva).</summary>
    [Signal]
    public delegate void OrderChosenEventHandler(int index);

    /// <summary>BA-H, RF-082: el jugador pulsa el consumible manual <c>id</c>.</summary>
    [Signal]
    public delegate void ConsumableChosenEventHandler(string id);

    /// <summary>ADR 0166: un grito del entrenador en curso: su nombre, los segundos que le quedan y qué fracción de su duración.</summary>
    public readonly record struct ShoutInfo(string Name, int SecondsLeft, float Fraction);

    /// <summary>
    /// BA-H, RF-082: un consumible manual equipado, listo para pulsar. <see cref="Used"/> y
    /// <see cref="Enabled"/> son independientes a propósito —el botón se apaga por las dos razones
    /// (RF-085 "se consumen al usarse" y las mismas condiciones que la orden táctica: partido en marcha,
    /// nada pendiente de decidir)— y el rótulo dice cuál.
    /// </summary>
    public readonly record struct ConsumableButtonInfo(string Id, string ShortName, string Tooltip, bool Used, bool Enabled);

    /// <summary>La escena de un botón de consumible; una instancia por consumible manual equipado.</summary>
    [Export]
    public PackedScene? ConsumableScene { get; set; }

    /// <summary>La escena de la etiqueta de un grito en curso; una instancia por grito.</summary>
    [Export]
    public PackedScene? ShoutScene { get; set; }

    private string _own = string.Empty;
    private string _rival = string.Empty;
    private int _ownScore;
    private int _rivalScore;
    private float _progress;
    private int _minute;
    private bool _goldenGoal;
    private string _rivalResidue = string.Empty;
    private int _speedIndex;
    private bool _paused;
    private int _bias;
    private int _orderIndex = 1;

    // ADR 0166: la orden que puso el jugador, distinta de la efectiva mientras dura un grito de orden.
    private int _playerOrderIndex = 1;
    private bool _orderEnabled = true;
    private IReadOnlyList<ShoutInfo> _shouts = System.Array.Empty<ShoutInfo>();
    private IReadOnlyList<ConsumableButtonInfo> _consumables = System.Array.Empty<ConsumableButtonInfo>();

    private bool _bound;
    private Label _ownName = null!;
    private Label _rivalName = null!;
    private Label _ownScoreLabel = null!;
    private Label _rivalScoreLabel = null!;
    private Control _residue = null!;
    private Label _residueText = null!;
    private ProgressBar _progressBar = null!;
    private Label? _clockText;
    private Label _criterion = null!;
    private BiasTrack _biasTrack = null!;
    private readonly Button[] _speedButtons = new Button[3];
    private Button _pauseButton = null!;
    private readonly Button[] _orderButtons = new Button[3];
    private readonly Control?[] _orderRings = new Control?[3];
    private Container _actionRow = null!;
    private readonly List<Button> _consumableButtons = new();
    private readonly List<Control> _shoutTags = new();

    /// <summary>El tablero con su escena. La única forma correcta de crearlo.</summary>
    public static BroadcastBoard Create() => GD.Load<PackedScene>(ScenePath).Instantiate<BroadcastBoard>();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0f, DesignHeight);
        MouseFilter = MouseFilterEnum.Stop;
        _bound = Bind();
        if (!_bound)
        {
            return;
        }

        if (Engine.IsEditorHint())
        {
            FillSample();
        }

        Refresh();
    }

    private bool Bind()
    {
        string[] speeds = { "%X1", "%X4", "%X16" };
        string[] orders = { "%Defensa", "%Neutro", "%Ataque" };
        var ownName = GetNodeOrNull<Label>("%NombrePropio");
        if (ownName is null)
        {
            GD.PushError("BroadcastBoard sin su escena: créalo con BroadcastBoard.Create(), no con new.");
            return false;
        }

        _ownName = ownName;
        _rivalName = GetNode<Label>("%NombreRival");
        _ownScoreLabel = GetNode<Label>("%GolesPropios");
        _rivalScoreLabel = GetNode<Label>("%GolesRival");
        _residue = GetNode<Control>("%Residuo");
        _residueText = GetNode<Label>("%ResiduoTexto");
        _progressBar = GetNode<ProgressBar>("%Progreso");

        // BX-14: opcional para no romper una escena del tablero anterior al reloj.
        _clockText = GetNodeOrNull<Label>("%RelojTexto");
        _criterion = GetNode<Label>("%Criterio");
        _biasTrack = GetNode<BiasTrack>("%PistaCriterio");
        _pauseButton = GetNode<Button>("%Pausa");
        _actionRow = GetNode<Container>("%FilaAcciones");

        string[] speedKeys = { "ui.pregon.speed.x1", "ui.pregon.speed.x4", "ui.pregon.speed.x16" };
        string[] orderKeys = { "ui.pregon.order.defensive", "ui.pregon.order.neutral", "ui.pregon.order.offensive" };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            _speedButtons[i] = PrepareButton(GetNode<Button>(speeds[i]), UiText.Get(speedKeys[i]));
            _speedButtons[i].Pressed += () => Choose(SignalName.SpeedChosen, index);
            _orderButtons[i] = PrepareButton(GetNode<Button>(orders[i]), UiText.Get(orderKeys[i]));
            _orderButtons[i].Pressed += () => ChooseOrder(index);
            _orderRings[i] = _orderButtons[i].GetNodeOrNull<Control>("Aro");
        }

        PrepareButton(_pauseButton, UiText.Get("ui.pregon.speed.pause"));
        _pauseButton.Pressed += () =>
        {
            EmitSignal(SignalName.PauseToggled);
            Refresh();
        };
        return true;
    }

    /// <summary>
    /// Los botones del tablero son interruptores cuyo estado manda el código: el toggle_mode da el aspecto
    /// «encendido» (estilo <c>pressed</c>) y tras cada pulsación se vuelve a pintar el estado real. Sin foco
    /// de teclado, para que la barra espaciadora no pulse el último botón tocado.
    /// </summary>
    private static Button PrepareButton(Button button, string text)
    {
        button.ToggleMode = true;
        button.FocusMode = FocusModeEnum.None;
        button.Text = text;
        return button;
    }

    private void Choose(StringName signal, int index)
    {
        EmitSignal(signal, index);
        Refresh();
    }

    private void ChooseOrder(int index)
    {
        if (_orderEnabled)
        {
            EmitSignal(SignalName.OrderChosen, index);
        }

        Refresh();
    }

    /// <summary>Lo que enseña el editor: un partido inventado, para ver cada pieza ocupada.</summary>
    private void FillSample()
    {
        _own = "Altos Hornos FC";
        _rival = "Yunque Verde";
        _ownScore = 1;
        _progress = 0.45f;
        _minute = 41;
        _rivalResidue = "−2 · +1";
        _bias = -23;
        _consumables = new[]
        {
            new ConsumableButtonInfo("sample_a", "Vendaje de campaña", string.Empty, false, true),
            new ConsumableButtonInfo("sample_b", "Amuleto de la suerte", string.Empty, true, false),
        };
        _shouts = new[] { new ShoutInfo("¡A por él!", 12, 0.6f) };
    }

    public void SetTeams(string own, string rival)
    {
        _own = own;
        _rival = rival;
        Refresh();
    }

    public void SetScore(int own, int rival)
    {
        _ownScore = own;
        _rivalScore = rival;
        Refresh();
    }

    public void SetProgress(float t)
    {
        _progress = Mathf.Clamp(t, 0f, 1f);
        Refresh();
    }

    /// <summary>
    /// El reloj del partido (BX-14): el minuto del <b>reloj</b> —el que se para mientras el equipo vuelve para
    /// sacar de centro (BC-A)— y si el partido está en la turba, que no tiene reloj sino gol de oro.
    /// </summary>
    public void SetClock(int minute, bool goldenGoal)
    {
        _minute = Mathf.Clamp(minute, 0, 90);
        _goldenGoal = goldenGoal;
        Refresh();
    }

    public void SetRivalResidue(string text)
    {
        _rivalResidue = text;
        Refresh();
    }

    public void SetSpeedIndex(int i)
    {
        _speedIndex = Mathf.Clamp(i, 0, 2);
        Refresh();
    }

    /// <summary>
    /// La orden táctica con la que juega el equipo (0 defensiva, 1 neutra, 2 ofensiva) y si se puede cambiar
    /// ahora. <paramref name="playerIndex"/> es la que puso el jugador: distinta de <paramref name="index"/>
    /// sólo mientras un grito de orden manda (ADR 0166), y es a la que se vuelve al acabar.
    /// </summary>
    public void SetOrder(int index, bool enabled, int? playerIndex = null)
    {
        _orderIndex = Mathf.Clamp(index, 0, 2);
        _playerOrderIndex = Mathf.Clamp(playerIndex ?? index, 0, 2);
        _orderEnabled = enabled;
        Refresh();
    }

    /// <summary>ADR 0166: los gritos del entrenador en curso, con su cuenta atrás; vacío si no hay ninguno.</summary>
    public void SetShouts(IReadOnlyList<ShoutInfo> shouts)
    {
        _shouts = shouts;
        Refresh();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        Refresh();
    }

    /// <summary>
    /// BA-H, RF-082: los consumibles manuales equipados, en el orden en que se resolverían si dos se
    /// dispararan a la vez (mismo orden que <c>RunEquipment.ForMatch</c>). Vacío si no hay ninguno.
    /// </summary>
    public void SetConsumables(IReadOnlyList<ConsumableButtonInfo> consumables)
    {
        _consumables = consumables;
        Refresh();
    }

    /// <summary>Criterio actual del árbitro, −100..100 (RF-062): el medidor siempre visible del tablero.</summary>
    public void SetBias(int bias)
    {
        _bias = Mathf.Clamp(bias, -100, 100);
        Refresh();
    }

    /// <summary>Un desplazamiento del criterio (RF-063): un "+N"/"−N" breve junto al medidor. Sin efecto si <paramref name="delta"/> es 0.</summary>
    public void ShowBiasDelta(int delta)
    {
        if (_bound)
        {
            _biasTrack.AddFloat(delta);
        }
    }

    /// <summary>Vuelca el estado en los nodos. Antes de <see cref="_Ready"/> no hay nodos: el estado espera.</summary>
    private void Refresh()
    {
        if (!_bound)
        {
            return;
        }

        _ownName.Text = _own;
        _rivalName.Text = _rival;
        _ownScoreLabel.Text = _ownScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _rivalScoreLabel.Text = _rivalScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _residue.Visible = !string.IsNullOrEmpty(_rivalResidue);
        _residueText.Text = _rivalResidue;
        _progressBar.Value = _progress;
        if (_clockText is not null)
        {
            _clockText.Text = _goldenGoal
                ? UiText.Get("ui.pregon.board.clockMob")
                : UiText.Get("ui.pregon.board.clock", _minute);
        }
        _criterion.Text = UiText.Get("ui.pregon.board.bias", UiText.Signed(_bias));
        _biasTrack.Bias = _bias;

        for (int i = 0; i < 3; i++)
        {
            _speedButtons[i].SetPressedNoSignal(i == _speedIndex && !_paused);

            bool active = i == _orderIndex;
            _orderButtons[i].SetPressedNoSignal(active);

            // La vigente sigue encendida aunque no se pueda cambiar; las demás se apagan (partido terminado).
            _orderButtons[i].Disabled = !_orderEnabled && !active;
            if (_orderRings[i] is { } ring)
            {
                // ADR 0166: mientras un grito manda, la orden a la que se vuelve al acabar lleva un aro de oro.
                ring.Visible = i == _playerOrderIndex && _playerOrderIndex != _orderIndex;
            }
        }

        _pauseButton.SetPressedNoSignal(_paused);
        RefreshConsumables();
        RefreshShouts();
    }

    /// <summary>
    /// BA-H, RF-082 (ADR 0172): un botón por consumible manual, en la fila de acciones bajo la orden táctica.
    /// Encendido (estilo <c>pressed</c>) mientras se puede pulsar; usado o sin poder pulsarlo ahora, apagado,
    /// y usado sigue diciendo cuál era. El nombre entero en el tooltip no: ahí va su descripción (RT-035).
    /// </summary>
    private void RefreshConsumables()
    {
        while (_consumableButtons.Count > _consumables.Count)
        {
            _consumableButtons[^1].QueueFree();
            _consumableButtons.RemoveAt(_consumableButtons.Count - 1);
        }

        while (_consumableButtons.Count < _consumables.Count && ConsumableScene is not null)
        {
            int index = _consumableButtons.Count;
            var button = ConsumableScene.Instantiate<Button>();
            button.ToggleMode = true;
            button.FocusMode = FocusModeEnum.None;
            button.Pressed += () => ChooseConsumable(index);
            _actionRow.AddChild(button);
            _actionRow.MoveChild(button, index);
            _consumableButtons.Add(button);
        }

        for (int i = 0; i < _consumableButtons.Count; i++)
        {
            var info = _consumables[i];
            var button = _consumableButtons[i];
            button.Text = info.Used ? UiText.Get("ui.pregon.consumable.usedLabel", info.ShortName) : info.ShortName;
            button.TooltipText = info.Tooltip;
            button.Disabled = info.Used || !info.Enabled;
            button.SetPressedNoSignal(!button.Disabled);
        }
    }

    private void ChooseConsumable(int index)
    {
        if (index < _consumables.Count && _consumables[index].Enabled && !_consumables[index].Used)
        {
            EmitSignal(SignalName.ConsumableChosen, _consumables[index].Id);
        }

        Refresh();
    }

    /// <summary>
    /// ADR 0166: el grito del entrenador en curso, en la fila de acciones justo tras los consumibles (el que lo
    /// gritó se ve «· usado» a su izquierda), con el nombre, los segundos que le quedan y una barra que se vacía.
    /// </summary>
    private void RefreshShouts()
    {
        while (_shoutTags.Count > _shouts.Count)
        {
            _shoutTags[^1].QueueFree();
            _shoutTags.RemoveAt(_shoutTags.Count - 1);
        }

        while (_shoutTags.Count < _shouts.Count && ShoutScene is not null)
        {
            var tag = ShoutScene.Instantiate<Control>();
            _actionRow.AddChild(tag);
            _shoutTags.Add(tag);
        }

        for (int i = 0; i < _shoutTags.Count; i++)
        {
            var shout = _shouts[i];

            // ADR 0167: -1 = hasta el final (lo que impone la turba al entrar).
            _shoutTags[i].GetNode<Label>("%Texto").Text = shout.SecondsLeft < 0
                ? UiText.Get("ui.pregon.shout.untilEnd", shout.Name)
                : UiText.Get("ui.pregon.shout.active", shout.Name, shout.SecondsLeft);
            _shoutTags[i].GetNode<Godot.Range>("%Cuenta").Value = Mathf.Clamp(shout.Fraction, 0f, 1f);
        }
    }
}
