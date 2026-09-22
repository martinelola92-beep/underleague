using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
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

    /// <summary>Radio del medallón grande de la ficha expandida (UI-012). El de la tira es fijo en <see cref="DrawStrip"/>.</summary>
    private const float PortraitRadius = 22f;

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
    private int _perkSlots;
    private int _perkCount;
    private string _headline = string.Empty;
    private bool _expanded;
    private bool _selected;
    private bool _bench;
    private float _flash;
    private float _lastWidth;
    private DropHighlight _drop;
    private DropSlotKind _dropSlot;

    /// <summary>
    /// Estado de destino de arrastre (encargo mercado-arrastrar, UI-006): mientras el Mercado tiene una
    /// carta de artículo cogida, cada ficha de la plantilla se pinta como <see cref="Valid"/> o
    /// <see cref="Invalid"/> según si ese jugador puede llevarlo (<c>MarketRow.Carriers</c>), <b>todas a la
    /// vez</b> y no solo la que el ratón toca — es la respuesta visual a "qué huecos aceptan esto" antes de
    /// soltar. <see cref="None"/> fuera de un arrastre.
    /// </summary>
    public enum DropHighlight
    {
        None,
        Valid,
        Invalid,
    }

    /// <summary>Qué hueco de la tira se resalta mientras <see cref="Drop"/> es <see cref="DropHighlight.Valid"/>.</summary>
    public enum DropSlotKind
    {
        None,
        Perk,
        Item,
    }

    /// <summary>Ver <see cref="DropHighlight"/>. La pantalla lo fija sobre <b>todas</b> las fichas a la vez al coger un artículo.</summary>
    public DropHighlight Drop
    {
        get => _drop;
        set
        {
            if (_drop == value)
            {
                return;
            }

            _drop = value;
            QueueRedraw();
        }
    }

    /// <summary>Hueco que <see cref="Drop"/> señala (perk u objeto); <see cref="DropSlotKind.None"/> si no aplica.</summary>
    public DropSlotKind TargetSlot
    {
        get => _dropSlot;
        set
        {
            if (_dropSlot == value)
            {
                return;
            }

            _dropSlot = value;
            QueueRedraw();
        }
    }

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

                // Cuántos de la plantilla llevan ya la etiqueta que cuenta el perk (RF-012d, BB-J): se sabe
                // antes de comprar o de alinear a nadie, no solo tras leer su descripción.
                foreach (var requirement in PerkSquadRequirements.For(perk, state.Players, player.Id))
                {
                    perkLines.Add(
                        "  " + UiText.Get(
                            "ui.card.perkRequirement",
                            templates.Get("tags", requirement.Tag),
                            requirement.Current,
                            requirement.Required));
                }
            }
        }

        int slots = Sim.Progression.Progression.PerkSlots(player.Rarity);
        for (int i = player.Perks.Count; i < slots; i++)
        {
            perkLines.Add(UiText.Get("ui.card.perkSlot"));
        }

        // Huecos de perk y de objeto, ocupados y libres: se ven sin expandir la ficha (decisión del
        // revisor, 20 sep 2026), no solo leyendo la sección de perks de la ficha expandida.
        _perkSlots = slots;
        _perkCount = player.Perks.Count;

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

        // Carrera (RF-122, ADR 0124, F1 §6): la única memoria de las tres del hito que se lee en esta
        // ficha -RivalHistory y RivalCredits son de rival y viven en el cartel del nodo (ScoutScreen), no
        // aquí-. Sin partidos jugados no hay historia que contar (regla dura de la ADR: "un partido cuenta
        // solo si se pisó el campo"), así que la sección entera desaparece en vez de enseñar una fila de
        // ceros.
        if (state.CareerOf(player.Id) is { Matches: > 0 } career)
        {
            // No Compact: la línea junta hasta cinco cifras y se sale del ancho de una sola línea con una
            // carrera larga (14 partidos, goles, entradas, lesiones y muertes causadas a la vez); envuelve
            // igual que VÍNCULOS.
            _sections.Add(new Section(UiText.Get("ui.card.career"), CareerLines(career)));
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

        // Destino de arrastre (encargo mercado-arrastrar): un tinte por encima del fondo de siempre, no
        // en vez de él, para que la ficha siga siendo la misma ficha —color de raza, hueco de perk y de
        // objeto— y solo cambie si acepta lo que se está arrastrando (UI-002: color y borde juntos).
        if (_drop == DropHighlight.Valid)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(Style.LinkCreated, 0.22f));
            DrawRect(new Rect2(Vector2.Zero, Size), Style.LinkCreated, false, 2f);
        }
        else if (_drop == DropHighlight.Invalid)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(Style.LinkBroken, 0.16f));
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

        // Medallón grande (UI-010): el mismo retrato de la tira, a más tamaño, junto a la cabecera.
        float headlineLeft = Padding + (PortraitRadius * 2f) + 8f;
        var headlineLines = Style.Wrap(font, _headline, Style.TextSmall, width - headlineLeft - Padding);
        float headlineHeight = headlineLines.Count * LineHeight;
        float portraitBlockHeight = Mathf.Max(headlineHeight, PortraitRadius * 2f);

        Medallion.Draw(this, new Vector2(Padding + PortraitRadius, y + (portraitBlockHeight / 2f)), PortraitRadius, _player.Race, _player.Position, _player.Id);

        float headlineY = y + Mathf.Max(0f, (portraitBlockHeight - headlineHeight) / 2f);
        foreach (string line in headlineLines)
        {
            Style.DrawText(this, font, new Vector2(headlineLeft, headlineY), line, Style.TextSmall, Style.TextDim);
            headlineY += LineHeight;
        }

        y += portraitBlockHeight + 4f;
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

            // Rubrica, no dorado: es una cabecera de subsección sobre el pergamino de la ficha
            // expandida, y el dorado de Style.Accent se lee peor ahí que sobre madera.
            Style.DrawText(this, font, new Vector2(Padding, y), section.Title, Style.TextSmall, Pregon.Wax);

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

    /// <summary>
    /// La tira de 24 px de UI-011, idéntica en los tres estados: es el ancla visual del componente
    /// (UI-010). Retrato en medallón (<see cref="Medallion"/>), nombre, huecos de perk y de objeto —
    /// ocupados y libres, decisión del revisor 20 sep 2026— y estado físico por color y forma.
    /// </summary>
    private void DrawStrip(Font font, float width)
    {
        if (_player is null)
        {
            return;
        }

        Medallion.Draw(this, new Vector2(13f, 12f), 10f, _player.Race, _player.Position, _player.Id);

        const float StateZoneWidth = 24f;
        const float StateGap = 6f;
        float slotsWidth = (_perkSlots * 8f) + 14f;
        float slotsRight = width - StateZoneWidth - StateGap;
        float slotsLeft = slotsRight - slotsWidth;

        float nameLeft = 30f;
        Style.DrawText(this, font, new Vector2(nameLeft, 5f), _player.Name, Style.TextSmall, Style.Text, slotsLeft - nameLeft - 6f);

        // Hueco de perk: punto lleno (dorado) si está ocupado, anillo hueco si está libre. Hueco de
        // objeto: mismo criterio pero en cuadrado, para que la forma —no solo el relleno— diga si es un
        // perk o el objeto (UI-002).
        for (int i = 0; i < _perkSlots; i++)
        {
            DrawSlotDot(new Vector2(slotsLeft + (i * 8f) + 4f, 12f), 3f, i < _perkCount);
        }

        DrawItemSlot(new Vector2(slotsRight - 5f, 12f), 4f, _item is not null);

        // Anillo extra sobre el hueco al que apunta un arrastre válido (encargo mercado-arrastrar): el
        // hueco de perk señala el siguiente libre; el de objeto, el cuadrado, se ocupe o no —un objeto
        // siempre sustituye al anterior, nunca hace falta uno vacío.
        if (_drop == DropHighlight.Valid && _dropSlot == DropSlotKind.Perk && _perkCount < _perkSlots)
        {
            DrawArc(new Vector2(slotsLeft + (_perkCount * 8f) + 4f, 12f), 5f, 0f, Mathf.Tau, 12, Style.LinkCreated, 2f);
        }
        else if (_drop == DropHighlight.Valid && _dropSlot == DropSlotKind.Item)
        {
            var center = new Vector2(slotsRight - 5f, 12f);
            DrawRect(new Rect2(center - new Vector2(6f, 6f), new Vector2(12f, 12f)), Style.LinkCreated, false, 2f);
        }

        var stateColor = Style.Of(_player.PhysicalState);
        DrawRect(new Rect2(width - StateZoneWidth, 3f, 8f, Style.CollapsedHeight - 6f), stateColor);
        Style.DrawStateIcon(this, new Vector2(width - 10f, 12f), 4f, _player.PhysicalState, stateColor);
    }

    /// <summary>Hueco de perk (UI-002: relleno = ocupado, anillo hueco = libre; nunca solo un color distinto).</summary>
    private void DrawSlotDot(Vector2 center, float radius, bool filled)
    {
        if (filled)
        {
            DrawCircle(center, radius, Style.Accent);
        }
        else
        {
            DrawArc(center, radius, 0f, Mathf.Tau, 12, Style.Line, 1f);
        }
    }

    /// <summary>Hueco de objeto: mismo criterio que <see cref="DrawSlotDot"/> pero en cuadrado, para distinguirlo por forma.</summary>
    private void DrawItemSlot(Vector2 center, float half, bool filled)
    {
        var rect = new Rect2(center - new Vector2(half, half), new Vector2(half * 2f, half * 2f));
        if (filled)
        {
            DrawRect(rect, Style.Accent);
        }
        else
        {
            DrawRect(rect, Style.Line, false, 1f);
        }
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

            // Misma cuenta que el bloque de cabecera de _Draw: el alto que gana el medallón grande
            // cuando el nombre no llena las dos líneas que le da su ancho reducido.
            float headlineLeft = Padding + (PortraitRadius * 2f) + 8f;
            int headlineLineCount = Style.Wrap(font, _headline, Style.TextSmall, width - headlineLeft - Padding).Count;
            float portraitBlockHeight = Mathf.Max(headlineLineCount * LineHeight, PortraitRadius * 2f);
            height += 6f + portraitBlockHeight + 4f;
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
    /// Una línea compacta con la carrera (RF-122): partidos siempre, y el resto de cifras solo si valen
    /// algo -"las cifras que valen cero se omiten, salvo partidos" (encargo F1 §6). Prioriza lo que cuenta
    /// una historia (goles, entradas ganadas, lesiones y muertes causadas) en vez de volcar los once
    /// campos de <see cref="RunCareer"/>.
    /// </summary>
    private static List<string> CareerLines(RunCareer career)
    {
        var parts = new List<string> { Plural(career.Matches, "ui.card.careerMatch", "ui.card.careerMatches") };

        if (career.Goals > 0)
        {
            parts.Add(Plural(career.Goals, "ui.card.careerGoal", "ui.card.careerGoals"));
        }

        if (career.TacklesWon > 0)
        {
            parts.Add(Plural(career.TacklesWon, "ui.card.careerTackleWon", "ui.card.careerTacklesWon"));
        }

        if (career.InjuriesCaused > 0)
        {
            parts.Add(Plural(career.InjuriesCaused, "ui.card.careerInjuryCaused", "ui.card.careerInjuriesCaused"));
        }

        if (career.DeathsCaused > 0)
        {
            parts.Add(Plural(career.DeathsCaused, "ui.card.careerDeathCaused", "ui.card.careerDeathsCaused"));
        }

        return new List<string> { string.Join(" · ", parts) };
    }

    private static string Plural(int value, string singularKey, string pluralKey) =>
        value == 1 ? UiText.Get(singularKey) : UiText.Get(pluralKey, value);

    /// <summary>
    /// Bloque de la ficha expandida. <paramref name="Compact"/> pone título y valor en la misma línea;
    /// <paramref name="Rich"/> marca las secciones de texto generado, cuyas líneas pueden nombrar una
    /// zona de inicio y entonces se pintan con un <see cref="RichTextLabel"/> en vez de a mano.
    /// </summary>
    private sealed record Section(string Title, IReadOnlyList<string> Lines, bool Compact = false, bool Rich = false);
}
