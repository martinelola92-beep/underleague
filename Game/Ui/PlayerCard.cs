using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Game.Ui;

/// <summary>
/// Ficha de jugador (UI-010..UI-014). <b>Es el mismo componente</b> que usarán Alineación, Partido y
/// Mercado: por eso es una escena propia (<c>res://Scenes/PlayerCard.tscn</c>) que solo recibe datos por
/// <see cref="Bind"/> y solo avisa hacia fuera con la señal <see cref="ActivatedEventHandler"/>. No sabe
/// nada de la cuadrícula ni de la pantalla que la contiene.
/// <para>Tres estados:</para>
/// <list type="bullet">
/// <item><b>Colapsada</b> (UI-011): tira de 24 px con retrato, icono de posición, nombre y barra de
/// estado físico. Nada más.</item>
/// <item><b>Expandida</b> (UI-012): nivel, los cinco atributos, rasgos, perks con su descripción
/// generada (RT-035), objeto, vínculos, estado y salario. La pantalla garantiza que solo hay una.</item>
/// <item><b>Reactiva</b> (UI-013): <see cref="Flash"/> hace destellar la tira. En el partido lo dispara
/// la activación de un perk; aquí, que el jugador cambie de casilla.</item>
/// </list>
/// </summary>
public partial class PlayerCard : Control
{
    private const int Padding = 8;
    private const int AttributeRow = 15;
    private const int LineHeight = 14;
    private const int SectionGap = 4;

    private readonly List<Section> _sections = new();
    private readonly List<(string Label, int Value, AttributeKind Kind)> _attributes = new();

    /// <summary>
    /// Etiquetas con BBCode para las líneas que nombran una zona de inicio. Se reutilizan de una llamada
    /// a <see cref="Bind"/> a la siguiente en vez de crearlas y destruirlas: destruir un nodo en mitad de
    /// un hover dejaría la señal de "he dejado de mirar" sin emitir y el campo teñido para siempre.
    /// </summary>
    private readonly List<RichTextLabel> _hints = new();

    private TeamState? _state;
    private PlayerDefinition? _player;
    private ItemDefinition? _item;
    private string _headline = string.Empty;
    private bool _expanded;
    private bool _selected;
    private bool _bench;
    private float _flash;
    private float _lastWidth;

    /// <summary>Centro de la primera frase de zona marcada y qué nombra; null si la ficha no marca ninguna.</summary>
    private Vector2? _hintPoint;
    private (string Kind, string Value) _hintPayload;

    /// <summary>La ficha ha sido activada: un clic o el botón de acción del mando (UI-001, mismo gesto).</summary>
    [Signal]
    public delegate void ActivatedEventHandler(int playerId);

    /// <summary>
    /// El ratón está sobre el nombre de una zona de inicio dentro de una descripción, o ha dejado de
    /// estarlo (AW-F). <paramref name="kind"/> es <c>"zone"</c> o <c>"flank"</c>, y <paramref name="value"/>
    /// el nombre del valor (<c>"AttackingThird"</c>, <c>"LeftFlank"</c>...); los dos vacíos cuando se deja
    /// de mirar. La ficha no sabe qué se hace con eso: la zona referida no depende de a quién pertenezca
    /// la ficha, así que quien la pinte es la pantalla.
    /// </summary>
    [Signal]
    public delegate void ZoneHintEventHandler(string kind, string value);

    /// <summary>Id del jugador que muestra; -1 si no se ha llamado a <see cref="Bind"/>.</summary>
    public int PlayerId => _player?.Id ?? -1;

    /// <summary>Estado expandido (UI-012). Solo una ficha expandida a la vez: lo impone la pantalla.</summary>
    public bool Expanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }

            _expanded = value;
            Relayout();
        }
    }

    /// <summary>Marca de selección: el jugador cuya zona se está pintando en el campo.</summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            QueueRedraw();
        }
    }

    /// <summary>Rellena la ficha. Todo el texto sale del catálogo o de <see cref="UiText"/> (RT-073).</summary>
    public void Bind(TeamState state, PlayerDefinition player, IReadOnlyList<string> links)
    {
        _state = state;
        _player = player;
        _item = state.EquippedItemOf(player.Id);
        _bench = !state.IsStarter(player.Id);
        _sections.Clear();
        _attributes.Clear();

        var catalog = state.Catalog;
        var templates = state.Templates;

        _headline = string.Join(" · ", new[]
        {
            UiText.Get("ui.card.average") + " " + player.Attributes.Average,
            UiText.Get("ui.card.level", player.Level),
            UiText.Get("ui.card.rarity." + player.Rarity),
            catalog.Race(player.Race).Name.Es,
            templates.Get("positions", player.Position.ToString()),
            UiText.Get("ui.card.style") + " " + catalog.Style(player.StyleTag).Name.Es,
            _bench ? UiText.Get("ui.card.bench") : string.Empty,
        }).TrimEnd(' ', '·');

        _attributes.Add((templates.Get("attributes", "strength"), player.Attributes.Strength, AttributeKind.Strength));
        _attributes.Add((templates.Get("attributes", "speed"), player.Attributes.Speed, AttributeKind.Speed));
        _attributes.Add((templates.Get("attributes", "technique"), player.Attributes.Technique, AttributeKind.Technique));
        _attributes.Add((templates.Get("attributes", "stamina"), player.Attributes.Stamina, AttributeKind.Stamina));
        _attributes.Add((templates.Get("attributes", "leash"), player.Attributes.Leash, AttributeKind.Leash));

        var traits = new List<string>();
        foreach (var trait in player.Traits)
        {
            traits.Add(catalog.Trait(trait).Name.Es);
        }

        _sections.Add(new Section(UiText.Get("ui.card.traits"), new List<string> { string.Join(", ", traits) }, Compact: true));

        var perkLines = new List<string>();
        foreach (string id in player.Perks)
        {
            var perk = catalog.Perks.Find(id);
            if (perk is not null)
            {
                perkLines.Add(perk.Name.Es + ": " + DescriptionGenerator.Describe(perk, templates));
            }
        }

        int slots = Sim.Progression.Progression.PerkSlots(player.Rarity);
        for (int i = player.Perks.Count; i < slots; i++)
        {
            perkLines.Add(UiText.Get("ui.card.perkSlot"));
        }

        // Perks y habilidad racial son las dos secciones cuyo texto sale del generador (RT-035) y las dos
        // únicas donde puede aparecer el nombre de un tercio o de una banda: son las que se pintan con
        // BBCode para que esa frase tenga tooltip y resalte la zona en el campo (AW-F).
        _sections.Add(new Section(UiText.Get("ui.card.perks"), perkLines, Rich: true));

        string ability = catalog.Race(player.Race).Ability;
        if (catalog.Perks.Find(ability) is { } racial)
        {
            _sections.Add(new Section(
                UiText.Get("ui.card.ability"),
                new List<string> { racial.Name.Es + ": " + DescriptionGenerator.Describe(racial, templates) },
                Rich: true));
        }

        _sections.Add(new Section(UiText.Get("ui.card.links"), links.Count > 0 ? new List<string>(links) : new List<string> { UiText.Get("ui.team.linksNone") }));

        if (_item is { } item)
        {
            _sections.Add(new Section(
                UiText.Get("ui.card.item"),
                new List<string> { item.Name.Es + ": " + ItemDescriptions.Describe(item, TeamState.Language) }));
        }
        else
        {
            _sections.Add(new Section(UiText.Get("ui.card.item"), new List<string> { UiText.Get("ui.card.itemNone") }, Compact: true));
        }

        _sections.Add(new Section(UiText.Get("ui.card.state"), new List<string> { UiText.Get("ui.state." + player.PhysicalState) }, Compact: true));
        _sections.Add(new Section(UiText.Get("ui.card.salary"), new List<string> { UiText.Get("ui.card.salaryNone") }, Compact: true));

        Relayout();
    }

    /// <summary>Estado reactivo (UI-013): la tira destella. Se apaga sola.</summary>
    public void Flash()
    {
        _flash = 1f;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(0f, Style.CollapsedHeight);
        SetProcess(false);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && !Mathf.IsEqualApprox(_lastWidth, Size.X))
        {
            _lastWidth = Size.X;
            Relayout();
        }
    }

    public override void _Process(double delta)
    {
        _flash -= (float)delta * 1.6f;
        if (_flash <= 0f)
        {
            _flash = 0f;
            SetProcess(false);
        }

        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _player is not null)
        {
            EmitSignal(SignalName.Activated, _player.Id);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        if (_player is null || _state is null)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        float width = Size.X;
        var background = _expanded ? Style.PanelSoft : Style.Panel;
        DrawRect(new Rect2(Vector2.Zero, Size), background);

        if (_selected)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Style.Accent, false, 1f);
        }

        if (_flash > 0f)
        {
            DrawRect(new Rect2(Vector2.Zero, new Vector2(width, Style.CollapsedHeight)), new Color(Style.Accent, _flash * 0.55f));
        }

        DrawStrip(font, width);

        if (!_expanded)
        {
            return;
        }

        float y = Style.CollapsedHeight + 6f;
        float textWidth = width - (Padding * 2);

        foreach (string line in Style.Wrap(font, _headline, Style.TextSmall, textWidth))
        {
            Style.DrawText(this, font, new Vector2(Padding, y), line, Style.TextSmall, Style.TextDim);
            y += LineHeight;
        }

        y += 4f;
        float deltaWidth = _item is not null ? 30f : 0f;
        foreach (var (label, value, kind) in _attributes)
        {
            Style.DrawText(this, font, new Vector2(Padding, y), label, Style.TextSmall, Style.Text);
            float barLeft = Padding + 86f;
            float barWidth = width - barLeft - Padding - 26f - deltaWidth;
            DrawRect(new Rect2(barLeft, y + 4f, barWidth, 7f), Style.Line);
            DrawRect(new Rect2(barLeft, y + 4f, barWidth * value / 99f, 7f), Style.Of(_player.Position));
            Style.DrawText(this, font, new Vector2(width - Padding - deltaWidth - 20f, y), value.ToString(System.Globalization.CultureInfo.InvariantCulture), Style.TextSmall, Style.Text);

            int delta = _item?.Modifier.Get(kind) ?? 0;
            if (delta != 0)
            {
                string deltaText = (delta > 0 ? "+" : string.Empty) + delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var deltaColor = delta > 0 ? Style.LinkCreated : Style.LinkBroken;
                Style.DrawText(this, font, new Vector2(width - Padding - deltaWidth, y), deltaText, Style.TextSmall, deltaColor);
            }

            y += AttributeRow;
        }

        foreach (var section in _sections)
        {
            y += SectionGap;
            Style.DrawText(this, font, new Vector2(Padding, y), section.Title, Style.TextSmall, Style.Accent);

            if (section.Compact)
            {
                float left = Padding + font.GetStringSize(section.Title, HorizontalAlignment.Left, -1f, Style.TextSmall).X + 8f;
                Style.DrawText(this, font, new Vector2(left, y), section.Lines[0], Style.TextSmall, Style.Text, width - left - Padding);
                y += LineHeight;
                continue;
            }

            y += LineHeight;
            foreach (string entry in section.Lines)
            {
                var wrapped = Style.Wrap(font, entry, Style.TextSmall, textWidth - 8f);

                // Una línea que nombra una zona la dibuja su RichTextLabel, colocado por Relayout con
                // esta misma cuenta: aquí sólo se salta su hueco, que ocupa exactamente lo mismo.
                if (section.Rich && ZoneHintText.Mentions(entry, _state.Templates))
                {
                    y += wrapped.Count * LineHeight;
                    continue;
                }

                foreach (string line in wrapped)
                {
                    Style.DrawText(this, font, new Vector2(Padding + 8f, y), line, Style.TextSmall, Style.Text);
                    y += LineHeight;
                }
            }
        }
    }

    /// <summary>La tira de 24 px de UI-011, idéntica en los tres estados: es el ancla visual del componente.</summary>
    private void DrawStrip(Font font, float width)
    {
        if (_player is null)
        {
            return;
        }

        var color = Style.Of(_player.Position);
        DrawCircle(new Vector2(14f, 12f), 8f, color);
        Style.DrawPositionIcon(this, new Vector2(14f, 12f), 4f, _player.Position, Style.Background);
        Style.DrawPositionIcon(this, new Vector2(32f, 12f), 5f, _player.Position, Style.TextDim);

        Style.DrawText(this, font, new Vector2(44f, 5f), _player.Name, Style.TextSmall, Style.Text, width - 44f - 34f);

        var stateColor = Style.Of(_player.PhysicalState);
        DrawRect(new Rect2(width - 24f, 3f, 8f, Style.CollapsedHeight - 6f), stateColor);
        Style.DrawStateIcon(this, new Vector2(width - 10f, 12f), 4f, _player.PhysicalState, stateColor);
    }

    /// <summary>
    /// Recalcula el alto según el estado; la lista de la pantalla se recoloca sola. Recorre las secciones
    /// con <b>la misma cuenta</b> que <see cref="_Draw"/> —de ahí sale el alto de la ficha— y aprovecha
    /// el recorrido para colocar las etiquetas de las líneas con zona sobre el hueco que el dibujo deja.
    /// El alto de esas etiquetas <b>no</b> se le pregunta al <c>RichTextLabel</c>: se le impone el que
    /// dicta <c>Style.Wrap</c>, y por eso el texto va ya partido en líneas y con el autoajuste apagado.
    /// Es la única forma de que la ficha mida lo mismo lleve marcado o no.
    /// </summary>
    private void Relayout()
    {
        float height = Style.CollapsedHeight;
        int used = 0;
        _hintPoint = null;
        _hintPayload = (string.Empty, string.Empty);
        if (_expanded && _state is not null)
        {
            var font = GetThemeDefaultFont();
            float width = Size.X > 0f ? Size.X : 356f;
            float textWidth = width - (Padding * 2);
            height += 6f + (Style.Wrap(font, _headline, Style.TextSmall, textWidth).Count * LineHeight) + 4f;
            height += _attributes.Count * AttributeRow;
            foreach (var section in _sections)
            {
                height += SectionGap + LineHeight;
                if (section.Compact)
                {
                    continue;
                }

                foreach (string entry in section.Lines)
                {
                    var wrapped = Style.Wrap(font, entry, Style.TextSmall, textWidth - 8f);
                    if (section.Rich && ZoneHintText.Markup(entry, wrapped, _state.Templates) is { } markup)
                    {
                        var label = HintLabel(used++, font);
                        label.Text = markup;
                        label.Position = new Vector2(Padding + 8f, height);
                        label.Size = new Vector2(textWidth - 8f, wrapped.Count * LineHeight);
                        label.Visible = true;
                        if (_hintPoint is null)
                        {
                            LocateHint(font, entry, wrapped, height);
                        }
                    }

                    height += wrapped.Count * LineHeight;
                }
            }

            height += Padding;
        }

        for (int i = used; i < _hints.Count; i++)
        {
            _hints[i].Visible = false;
        }

        CustomMinimumSize = new Vector2(0f, height);
        QueueRedraw();
    }

    /// <summary>
    /// Etiqueta BBCode número <paramref name="index"/>, creándola si hace falta. Se configura para que
    /// escriba <b>igual</b> que <c>Style.DrawText</c>: misma fuente, mismo cuerpo, sin recuadro y con la
    /// separación de línea ajustada para que cada línea avance los <see cref="LineHeight"/> píxeles que
    /// cuenta el resto de la ficha. El ratón la atraviesa (<see cref="MouseFilterEnum.Pass"/>) para que
    /// un clic sobre el texto siga colapsando la ficha como sobre cualquier otra parte de ella.
    /// </summary>
    private RichTextLabel HintLabel(int index, Font font)
    {
        while (_hints.Count <= index)
        {
            var created = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = false,
                ScrollActive = false,
                AutowrapMode = TextServer.AutowrapMode.Off,
                MouseFilter = MouseFilterEnum.Pass,
                Visible = false,
            };

            created.AddThemeFontOverride("normal_font", font);
            created.AddThemeFontSizeOverride("normal_font_size", Style.TextSmall);
            created.AddThemeColorOverride("default_color", Style.Text);
            created.AddThemeConstantOverride("line_separation", Mathf.RoundToInt(LineHeight - font.GetHeight(Style.TextSmall)));
            created.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            created.MetaHoverStarted += OnMetaHoverStarted;
            created.MetaHoverEnded += OnMetaHoverEnded;
            AddChild(created);
            _hints.Add(created);
        }

        return _hints[index];
    }

    /// <summary>
    /// Guarda dónde cae la primera frase de zona de la ficha. Sólo lo usa la secuencia de capturas: no
    /// hay evento de "ratón encima" que inyectar como se inyecta una acción de mando, así que la captura
    /// lleva el puntero de verdad hasta este punto y deja que el hover ocurra solo.
    /// </summary>
    private void LocateHint(Font font, string entry, IReadOnlyList<string> wrapped, float top)
    {
        if (_state is null)
        {
            return;
        }

        var spans = ZoneHintText.Spans(entry, _state.Templates);
        if (spans.Count == 0
            || !ZoneHintText.TryLocate(entry, wrapped, spans[0], out int line, out string prefix, out string phrase))
        {
            return;
        }

        float left = font.GetStringSize(prefix, HorizontalAlignment.Left, -1f, Style.TextSmall).X;
        float span = font.GetStringSize(phrase, HorizontalAlignment.Left, -1f, Style.TextSmall).X;
        _hintPoint = new Vector2(Padding + 8f + left + (span / 2f), top + (line * LineHeight) + (LineHeight / 2f));
        _hintPayload = (spans[0].Kind, spans[0].Key);
    }

    /// <summary>Punto y carga de la primera frase de zona marcada (solo para la secuencia de capturas).</summary>
    public bool TryZoneHint(out Vector2 globalPoint, out string kind, out string value)
    {
        globalPoint = Vector2.Zero;
        (kind, value) = _hintPayload;
        if (_hintPoint is not { } point)
        {
            return false;
        }

        globalPoint = GetGlobalTransformWithCanvas() * point;
        return true;
    }

    private void OnMetaHoverStarted(Variant meta)
    {
        if (ZoneHintText.TryParse(meta.AsString(), out string kind, out string key))
        {
            EmitSignal(SignalName.ZoneHint, kind, key);
        }
    }

    private void OnMetaHoverEnded(Variant meta)
    {
        _ = meta;
        EmitSignal(SignalName.ZoneHint, string.Empty, string.Empty);
    }

    /// <summary>
    /// Bloque de la ficha expandida. <paramref name="Compact"/> pone título y valor en la misma línea;
    /// <paramref name="Rich"/> marca las secciones de texto generado, cuyas líneas pueden nombrar una
    /// zona de inicio y entonces se pintan con un <see cref="RichTextLabel"/> en vez de a mano.
    /// </summary>
    private sealed record Section(string Title, IReadOnlyList<string> Lines, bool Compact = false, bool Rich = false);
}
