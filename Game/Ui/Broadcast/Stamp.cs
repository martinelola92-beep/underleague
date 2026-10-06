using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>Tono del sello: qué tipo de suceso proclama, con su color y su marca (UI-002: nunca solo color).</summary>
public enum StampTone
{
    Foul,
    Unseen,
    Yellow,
    MinorInjury,
    Perk,
    Consumable,
    Cancelled,
}

/// <summary>
/// Sello sobre recorte de papel, torcido (N1/N2 de <c>docs/ui/README.md</c> §4): falta, no vista, amarilla,
/// lesión leve sin decisión, perk activado en juego, consumible, anulado. El giro es determinista —función
/// del orden de creación, nunca <see cref="System.Random"/>— para que la misma composición produzca
/// siempre el mismo trazo.
/// <para>
/// <b>La forma vive en <c>Stamp.tscn</c></b> (regla 10 de <c>CLAUDE.md</c>): papel, marcas de cada tono y
/// rótulos son nodos. El código elige qué marca y qué rótulo se enseñan según el tono, pone el texto y su
/// tinta (un color por tono, único override de color de la pieza) y tuerce la raíz. Se crea con
/// <see cref="Create"/>.
/// </para>
/// </summary>
[Tool]
public partial class Stamp : Control
{
    private const string ScenePath = "res://Ui/Broadcast/Stamp.tscn";

    private static int _instanceCount;

    private readonly int _seed;
    private string _text = string.Empty;
    private StampTone _tone;
    private bool _large;

    private bool _bound;
    private Control _paper = null!;
    private Control _paperCancelled = null!;
    private Control _markFoul = null!;
    private Control _markYellow = null!;
    private Control _markCross = null!;
    private CanvasItem _markCancelled = null!;
    private Label _textData = null!;
    private Label _textTitle = null!;
    private Label _textTitleLarge = null!;

    public Stamp()
    {
        _seed = _instanceCount++;
    }

    /// <summary>El sello con su escena. La única forma correcta de crearlo.</summary>
    public static Stamp Create() => GD.Load<PackedScene>(ScenePath).Instantiate<Stamp>();

    public override void _Ready()
    {
        var paper = GetNodeOrNull<Control>("%Papel");
        if (paper is null)
        {
            GD.PushError("Stamp sin su escena: créalo con Stamp.Create(), no con new.");
            return;
        }

        _paper = paper;
        _paperCancelled = GetNode<Control>("%PapelAnulado");
        _markFoul = GetNode<Control>("%MarcaFalta");
        _markYellow = GetNode<Control>("%MarcaAmarilla");
        _markCross = GetNode<Control>("%MarcaCruz");
        _markCancelled = GetNode<CanvasItem>("%MarcaAnulado");
        _textData = GetNode<Label>("%TextoDato");
        _textTitle = GetNode<Label>("%TextoTitular");
        _textTitleLarge = GetNode<Label>("%TextoTitularGrande");
        _bound = true;

        // El giro, determinista: 2-4° a un lado u otro según el orden de creación. Se gira desde la esquina
        // superior izquierda, que es el pivote por defecto.
        // En el editor no se tuerce ni se esconde: el giro no debe acabar guardado en la escena.
        if (Engine.IsEditorHint())
        {
            Show("Sangre caliente", StampTone.Perk, large: false);
            return;
        }

        float mag = 2f + Mathf.Abs(Pregon.Jitter(_seed, 2f));
        float sign = Pregon.Jitter(_seed + 1, 1f) >= 0f ? 1f : -1f;
        Rotation = Mathf.DegToRad(mag * sign);
        Visible = false;
    }

    /// <summary>Muestra el sello con su texto (ya resuelto por el llamador, RT-035) y su tono.</summary>
    public void Show(string text, StampTone tone, bool large)
    {
        _text = text;
        _tone = tone;
        _large = large;
        CustomMinimumSize = large ? new Vector2(220f, 66f) : new Vector2(240f, 42f);
        Size = CustomMinimumSize;
        Visible = true;
        Refresh();
    }

    private (Color Ink, string Glyph) Tone() => _tone switch
    {
        StampTone.Foul => (Pregon.Wax, string.Empty),
        StampTone.Unseen => (Pregon.Sable, string.Empty),
        StampTone.Yellow => (new Color("8a6a12"), string.Empty),
        StampTone.MinorInjury => (Pregon.Blood, string.Empty),
        StampTone.Perk => (Pregon.InkBrown, "✦ "),
        StampTone.Consumable => (Pregon.InkBrown, "❖ "),
        StampTone.Cancelled => (new Color("5a5248"), string.Empty),
        _ => (Pregon.Sable, string.Empty),
    };

    private void Refresh()
    {
        if (!_bound)
        {
            return;
        }

        var (ink, glyph) = Tone();
        _paper.Visible = _tone != StampTone.Cancelled;
        _paperCancelled.Visible = _tone == StampTone.Cancelled;
        _markFoul.Visible = _tone == StampTone.Foul;
        _markYellow.Visible = _tone == StampTone.Yellow;
        _markCross.Visible = _tone == StampTone.MinorInjury;
        _markCancelled.Visible = _tone == StampTone.Cancelled;

        // Perk y consumible son la voz de dato (Barlow), no la voz que proclama (Grenze Gotisch): son un
        // recuento en curso, no un suceso arbitrado. La talla grande/pequeña la decide el rótulo que se
        // enseña, no el fichero — Grenze Gotisch es una sola familia variable para las dos tallas.
        bool dataVoice = _tone is StampTone.Perk or StampTone.Consumable;
        _textData.Visible = dataVoice;
        _textTitle.Visible = !dataVoice && !_large;
        _textTitleLarge.Visible = !dataVoice && _large;

        var label = dataVoice ? _textData : _large ? _textTitleLarge : _textTitle;
        label.Text = glyph + _text;
        label.AddThemeColorOverride("font_color", ink);
    }
}
