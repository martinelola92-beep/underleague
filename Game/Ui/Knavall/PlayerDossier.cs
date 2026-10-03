using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Game.Ui.Knavall;

/// <summary>
/// Ficha del jugador señalado, la protagonista de la pantalla de Equipo (ADR 0162): retrato grande, nombre
/// en rótulo, puesto, estilo y nivel; atributos como icono + cifra + barra + modificador; rasgos y perks como
/// carteles con su explicación en el cartel de ayuda (no en párrafos); el objeto equipado como un objeto; y
/// las tres acciones del cofre (ADR 0161 §3) como placas.
/// <para>
/// No decide nada: recibe el estado y lo que está elegido en el cofre, y avisa hacia fuera de lo que se
/// pulsa. Las reglas (un objeto por jugador, el desplazado vuelve al cofre) las valida <c>/Sim</c> a través de
/// <see cref="TeamState"/>, como antes.
/// </para>
/// </summary>
public partial class PlayerDossier : InkCanvas
{
    private const float Margin = 18f;

    private readonly List<PlaqueButton> _targets = new();
    private TeamState? _state;
    private PlayerDefinition? _player;
    private ItemDefinition? _item;
    private ItemDefinition? _chestPick;
    private bool _picking;

    private PlaqueButton _equip = null!;
    private PlaqueButton _store = null!;
    private PlaqueButton _pass = null!;
    private PlaqueButton _cancel = null!;

    /// <summary>EQUIPAR: poner al jugador el objeto elegido en el cofre.</summary>
    public event Action? EquipPressed;

    /// <summary>GUARDAR EN EL COFRE.</summary>
    public event Action? StorePressed;

    /// <summary>PASAR A OTRO: abrir o cerrar la lista de destinatarios.</summary>
    public event Action? PassToggled;

    /// <summary>Destinatario elegido en la lista de PASAR A OTRO.</summary>
    public event Action<int>? TransferTo;

    public override void _Ready()
    {
        base._Ready();
        _equip = PlaqueButton.Create(this, UiText.Get("ui.kn.equip"), Glyph.Shirt, PlaqueKind.Primary, new Rect2(), 11);
        _equip.Tip = new Tip(UiText.Get("ui.kn.equip"), UiText.Get("ui.kn.tip.equip"), Glyph.Shirt);
        _equip.Pressed += () => EquipPressed?.Invoke();

        _store = PlaqueButton.Create(this, UiText.Get("ui.kn.store"), Glyph.Chest, PlaqueKind.Paper, new Rect2(), 12);
        _store.Tip = new Tip(UiText.Get("ui.kn.store").Replace("\n", " "), UiText.Get("ui.kn.tip.store"), Glyph.Chest);
        _store.FontSize = 16;
        _store.Pressed += () => StorePressed?.Invoke();

        _pass = PlaqueButton.Create(this, UiText.Get("ui.kn.pass"), Glyph.Swap, PlaqueKind.Paper, new Rect2(), 13);
        _pass.Tip = new Tip(UiText.Get("ui.kn.pass"), UiText.Get("ui.kn.tip.pass"), Glyph.Swap);
        _pass.FontSize = 17;
        _pass.Pressed += () => PassToggled?.Invoke();

        _cancel = PlaqueButton.Create(this, UiText.Get("ui.kn.cancel"), Glyph.None, PlaqueKind.Wood, new Rect2(), 14);
        _cancel.FontSize = 15;
        _cancel.Pressed += () => PassToggled?.Invoke();
        LayoutButtons();
    }

    /// <summary>
    /// Rellena la ficha. <paramref name="chestPick"/> es el objeto elegido en el cofre (o null): es lo que
    /// EQUIPAR pondría. <paramref name="picking"/> abre la lista de destinatarios de PASAR A OTRO.
    /// </summary>
    public void Bind(TeamState state, int playerId, string chestPick, bool picking)
    {
        _state = state;
        _player = playerId >= 0 ? state.Find(playerId) : null;
        _item = _player is null ? null : state.EquippedItemOf(_player.Id);
        _chestPick = chestPick.Length > 0 ? state.Item(chestPick) : null;
        _picking = picking && _item is not null;
        LayoutButtons();
        RebuildTargets();
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationResized && _equip is not null)
        {
            LayoutButtons();
        }
    }

    private void LayoutButtons()
    {
        if (_equip is null)
        {
            return;
        }

        float y = Size.Y - 70f;
        float width = (Size.X - (Margin * 2f) - 16f) / 3f;
        _equip.Position = new Vector2(Margin, y);
        _equip.Size = new Vector2(width, 56f);
        _store.Position = new Vector2(Margin + width + 8f, y);
        _store.Size = new Vector2(width, 56f);
        _pass.Position = new Vector2(Margin + ((width + 8f) * 2f), y);
        _pass.Size = new Vector2(width, 56f);

        bool has = _player is not null;
        _equip.Visible = has;
        _store.Visible = has;
        _pass.Visible = has;
        _equip.Disabled = !has || _chestPick is null;

        // EQUIPAR lleva dibujado lo que pondría: el objeto elegido en el cofre, no una camiseta genérica.
        _equip.Glyph = _chestPick is { } pick ? InkIcons.OfItem(pick.Id) : Glyph.Shirt;
        _equip.Tip = _chestPick is { } chosen
            ? new Tip(UiText.Get("ui.kn.equip") + ": " + UiText.Name(chosen.Name), UiText.Get("ui.kn.tip.equip"), InkIcons.OfItem(chosen.Id), TeamTips.Modifiers(_state!, chosen))
            : new Tip(UiText.Get("ui.kn.equip"), UiText.Get("ui.kn.pickItem") + ". " + UiText.Get("ui.kn.tip.equip"), Glyph.Shirt);
        _store.Disabled = !has || _item is null;
        _pass.Disabled = !has || _item is null;
        _pass.Active = _picking;
        _pass.Kind = _picking ? PlaqueKind.Tab : PlaqueKind.Paper;
        _cancel.Visible = _picking;
    }

    private void RebuildTargets()
    {
        foreach (var button in _targets)
        {
            RemoveChild(button);
            button.QueueFree();
        }

        _targets.Clear();
        if (!_picking || _state is null || _player is null)
        {
            return;
        }

        var area = ItemBox();
        float columnWidth = (area.Size.X - 24f) / 3f;
        int index = 0;
        foreach (var candidate in _state.Players)
        {
            if (candidate.Id == _player.Id || candidate.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            int column = index % 3;
            int row = index / 3;
            var rect = new Rect2(area.Position.X + 8f + (column * (columnWidth + 4f)), area.Position.Y + 34f + (row * 34f), columnWidth, 36f);
            var button = PlaqueButton.Create(this, candidate.Name, Glyph.None, PlaqueKind.Paper, rect, 40 + index);
            button.FontSize = 15;
            int targetId = candidate.Id;
            button.Pressed += () => TransferTo?.Invoke(targetId);
            _targets.Add(button);
            index++;
        }

        _cancel.Position = new Vector2(area.End.X - 118f, area.Position.Y - 2f);
        _cancel.Size = new Vector2(112f, 34f);
        MoveChild(_cancel, GetChildCount() - 1);
    }

    /// <summary>
    /// La caja del objeto. Baja y se achica cuando no se está eligiendo a quién pasarlo, para que quepa el
    /// bloque de estadísticas de la run (ADR 0163) entre los perks y ella; al elegir vuelve a su tamaño.
    /// </summary>
    private Rect2 ItemBox() => _picking
        ? new Rect2(Margin, Size.Y - 206f, Size.X - (Margin * 2f), 128f)
        : new Rect2(Margin, Size.Y - 162f, Size.X - (Margin * 2f), 84f);

    public override void _Draw()
    {
        ClearZones();
        var sheet = new Rect2(Vector2.Zero, Size - new Vector2(8f, 8f));
        Ink.Sheet(this, sheet, 4242);

        if (_player is null || _state is null)
        {
            string text = UiText.Get("ui.kn.nobody").ToUpperInvariant();
            float width = Ink.Width(Ink.Display, text, 24);
            Ink.Text(this, Ink.Display, new Vector2((Size.X - width) / 2f, Size.Y / 2f - 20f), text, 24, Ink.Muted);
            return;
        }

        var player = _player;
        float y = DrawHead(player);
        y = DrawAttributes(player, y + 2f);
        DrawBadges(player, y + 4f);
        DrawRunStats(player, y + 4f + 76f);
        DrawItem();

        if (_picking)
        {
            var area = ItemBox().Grow(4f);
            Ink.Plank(this, area, 99, Ink.WoodDark, nails: false);
            Ink.Outlined(this, Ink.Heavy, area.Position + new Vector2(14f, 8f), UiText.Get("ui.kn.passTo").ToUpperInvariant(), 17, Ink.Paper);
        }
    }

    // ------------------------------------------------------------------ cabecera

    private float DrawHead(PlayerDefinition player)
    {
        const float Side = 150f;
        var photo = new Rect2(Margin + 4f, Margin + 4f, Side, Side);

        // Cartulina roja torcida detrás del retrato, como una foto pegada en el cartel del torneo.
        Pregon.Tilted(this, photo.GetCenter(), Mathf.DegToRad(-4f), () =>
        {
            var back = new Rect2(-(Side / 2f) - 8f, -(Side / 2f) - 8f, Side + 16f, Side + 16f);
            Ink.Slab(this, back, Ink.Red, player.Id + 7, 2f, 3f, new Vector2(5f, 6f));
        });
        Portrait.Draw(this, photo, player.Race, player.Position, player.Id, dimmed: player.PhysicalState is PhysicalState.SevereInjury or PhysicalState.Dead);
        Zone(photo, TeamTips.Level(player));

        float left = photo.End.X + 22f;
        float width = Size.X - left - Margin - 8f;

        // Sello de titular o suplente, arriba a la derecha.
        bool starter = _state!.IsStarter(player.Id);
        string stamp = UiText.Get(starter ? "ui.kn.starter" : "ui.kn.benchStamp").ToUpperInvariant();
        var stampAt = new Vector2(Size.X - Margin - 58f, Margin + 18f);
        Ink.Stamp(this, stampAt, stamp, starter ? Ink.Red : Ink.Muted, 7f, 16, new Color(Ink.Paper, 0.9f));
        Zone(new Rect2(stampAt - new Vector2(56f, 16f), new Vector2(112f, 32f)), TeamTips.Starter(starter));

        // Nombre en dos renglones de rótulo: nombre y apodo.
        string[] words = player.Name.ToUpperInvariant().Split(' ', 2);
        float y = Margin + 6f;
        foreach (string word in words)
        {
            int size = Ink.FitSize(Ink.Display, word, Ink.SizeTitle + 2, width - (y < Margin + 30f ? 96f : 0f), 20);
            Ink.Text(this, Ink.Display, new Vector2(left, y), word, size, Ink.Black);
            y += Ink.Display.GetHeight(size) - 12f;
        }

        // El apodo que la carrera le ha ganado (ADR 0163), en el renglón que sigue al nombre.
        string nickname = _state.NicknameOf(player.Id).ToUpperInvariant();
        if (nickname.Length > 0)
        {
            int nickSize = Ink.FitSize(Ink.Heavy, "«" + nickname + "»", 22, width, 14);
            Ink.Text(this, Ink.Heavy, new Vector2(left, y + 2f), "«" + nickname + "»", nickSize, Ink.RedDark);
            Zone(new Rect2(left, y, width, 26f), TeamTips.Nickname(_state, player.Id));
            y += 26f;
        }

        // Puesto · estilo · nivel, con el icono del puesto delante.
        y += 12f;
        InkIcons.Draw(this, InkIcons.Of(player.Position), new Vector2(left + 12f, y + 11f), 24f);
        string position = _state.Templates.Get("positions", player.Position.ToString()).ToUpperInvariant();
        string style = _state.Catalog.Style(player.StyleTag).Name.Es.ToUpperInvariant();
        string level = UiText.Get("ui.kn.level", player.Level).ToUpperInvariant();
        float x = left + 30f;
        Ink.Text(this, Ink.Heavy, new Vector2(x, y), position, 17, Ink.Brown);
        Zone(new Rect2(left, y - 2f, Ink.Width(Ink.Heavy, position, 17) + 32f, 26f), TeamTips.Position(_state, player.Position));
        x += Ink.Width(Ink.Heavy, position, 17);
        string rest = "  ·  " + style + "  ·  ";
        Ink.Text(this, Ink.Heavy, new Vector2(x, y), rest, 17, Ink.Brown);
        x += Ink.Width(Ink.Heavy, rest, 17);
        Ink.Text(this, Ink.Heavy, new Vector2(x, y), level, 17, Ink.Brown);
        Zone(new Rect2(x, y - 2f, Ink.Width(Ink.Heavy, level, 17), 26f), TeamTips.Level(player));

        // Estado físico. La carrera va en su propio bloque, más abajo (ADR 0163).
        y += 30f;
        InkIcons.Draw(this, InkIcons.Of(player.PhysicalState), new Vector2(left + 11f, y + 9f), 20f, player.PhysicalState == PhysicalState.Healthy ? Ink.GreenLight : null);
        string state = UiText.Get("ui.state." + player.PhysicalState).ToUpperInvariant();
        Ink.Text(this, Ink.Heavy, new Vector2(left + 28f, y), state, 15, Ink.Brown);
        Zone(new Rect2(left, y - 2f, Ink.Width(Ink.Heavy, state, 15) + 30f, 22f), TeamTips.State(player.PhysicalState));

        return Mathf.Max(photo.End.Y + 6f, y + 26f);
    }

    // ------------------------------------------------------------------ atributos

    private float DrawAttributes(PlayerDefinition player, float top)
    {
        const float Row = 29f;
        float barLeft = Margin + 196f;
        float barRight = Size.X - Margin - 78f;
        float y = top;
        foreach (var kind in new[] { AttributeKind.Strength, AttributeKind.Speed, AttributeKind.Technique, AttributeKind.Stamina, AttributeKind.Leash })
        {
            int value = player.Attributes.Get(kind);
            int modifier = _item?.Modifier.Get(kind) ?? 0;
            int effective = Math.Clamp(value + modifier, 1, 99);
            float mid = y + (Row / 2f);

            InkIcons.Draw(this, InkIcons.Of(kind), new Vector2(Margin + 18f, mid), 26f);
            string name = TeamTips.AttributeName(_state!, kind).ToUpperInvariant();
            Ink.Text(this, Ink.Heavy, new Vector2(Margin + 40f, mid - 11f), name, 17, Ink.Black);

            string figure = effective.ToString(System.Globalization.CultureInfo.InvariantCulture);
            float figureWidth = Ink.Width(Ink.Heavy, figure, Ink.SizeFigure + 2);
            var figureColor = modifier < 0 ? Ink.Red : Ink.Black;
            Ink.Text(this, Ink.Heavy, new Vector2(barLeft - 12f - figureWidth, mid - 15f), figure, Ink.SizeFigure + 2, figureColor);

            Ink.Bar(this, new Rect2(barLeft, mid - 8f, barRight - barLeft, 16f), value, modifier);
            Ink.Modifier(this, new Vector2(barRight + 12f, mid - 11f), modifier, 19);

            Zone(new Rect2(Margin, y, Size.X - (Margin * 2f), Row), TeamTips.Attribute(_state!, kind, value, modifier));
            y += Row;
        }

        return y;
    }

    // ------------------------------------------------------------------ rasgos y perks

    private void DrawBadges(PlayerDefinition player, float top)
    {
        float left = Margin + 84f;
        float right = Size.X - Margin - 8f;

        Label(UiText.Get("ui.kn.traits"), new Vector2(Margin + 2f, top + 7f));
        var traits = new List<(Tip Tip, Glyph Glyph, Color Paint, bool Empty)>();
        foreach (var trait in player.Traits)
        {
            var tip = TeamTips.Trait(_state!, trait);
            traits.Add((tip, tip.Glyph, Ink.Red, false));
        }

        string ability = _state!.Catalog.Race(player.Race).Ability;
        if (_state.Catalog.Perks.Find(ability) is { } racial)
        {
            // La habilidad racial es de nacimiento, como un rasgo, y no ocupa hueco de perk: va con los rasgos.
            traits.Add((TeamTips.Racial(_state, racial), Glyph.Racial, Ink.Green, false));
        }

        BadgeRow(traits, left, right, top);

        float perksTop = top + 38f;
        Label(UiText.Get("ui.kn.perks"), new Vector2(Margin + 2f, perksTop + 7f));
        var perks = new List<(Tip Tip, Glyph Glyph, Color Paint, bool Empty)>();
        foreach (string id in player.Perks)
        {
            if (_state!.Catalog.Perks.Find(id) is { } perk)
            {
                perks.Add((TeamTips.Perk(_state, perk, player), Glyph.Perk, Ink.Ochre, false));
            }
        }

        int slots = Sim.Progression.Progression.PerkSlots(player.Rarity);
        for (int i = player.Perks.Count; i < slots; i++)
        {
            perks.Add((TeamTips.EmptySlot(), Glyph.EmptySlot, Ink.Muted, true));
        }

        BadgeRow(perks, left, right, perksTop);
        DrawProstheses(player, perksTop + 36f);
    }

    /// <summary>
    /// Una línea discreta con las prótesis del jugador y lo que cambian (ADR 0164): «Prótesis · pata de palo
    /// (−12 velocidad), brazo de hierro (+8 fuerza)». Sin prótesis no se dibuja nada.
    /// </summary>
    private void DrawProstheses(PlayerDefinition player, float top)
    {
        var installed = _state!.ProsthesesOf(player.Id);
        if (installed.Count == 0)
        {
            return;
        }

        // ADR 0187 (RF-012d): el tope se ve antes de llegar a él y después de pasarse.
        var (count, crippled) = _state.ProsthesisCapOf(player.Id);

        var parts = new List<string>();
        foreach (var prosthesis in installed)
        {
            // El nombre con humor lleva su coletilla entre paréntesis; en una línea discreta basta lo de antes.
            string name = UiText.Name(prosthesis.Name);
            int cut = name.IndexOf(" (", StringComparison.Ordinal);
            parts.Add((cut > 0 ? name[..cut] : name) + " (" + UiText.Signed(prosthesis.Delta) + " "
                + TeamTips.AttributeName(_state, prosthesis.Attribute).ToLowerInvariant() + ")");
        }

        Label(UiText.Get("ui.kn.prostheses"), new Vector2(Margin + 2f, top + 7f));
        float left = Margin + 116f;

        // Con tres prótesis la lista no cabe en una línea sin encoger la letra hasta no leerse: se parte en dos.
        var lines = new List<string>();
        if (parts.Count >= 3)
        {
            int firstLine = (parts.Count + 1) / 2;
            lines.Add(string.Join(", ", parts.GetRange(0, firstLine)) + ",");
            lines.Add(string.Join(", ", parts.GetRange(firstLine, parts.Count - firstLine)));
        }
        else
        {
            lines.Add(string.Join(", ", parts));
        }

        for (int i = 0; i < lines.Count; i++)
        {
            int size = Ink.FitSize(Ink.Heavy, lines[i], 15, Size.X - Margin - 8f - left, 11);
            Ink.Text(this, Ink.Heavy, new Vector2(left, top + 10f + (18f * i)), lines[i], size, Ink.Muted);
        }

        float capTop = top + 10f + (18f * lines.Count);
        if (count >= RunRules.MaxProstheses)
        {
            string cap = UiText.Get(crippled ? "ui.kn.crippled" : "ui.kn.prosthesisCap", count, RunRules.MaxProstheses);
            Ink.Text(this, Ink.Heavy, new Vector2(left, capTop), cap, 13, crippled ? Ink.Red : Ink.Muted);
        }
    }

    /// <summary>
    /// Estadísticas de la carrera en la run (ADR 0163, RF-122): las cifras de <see cref="RunCareer"/>, ocho
    /// casillas de cifra grande y rótulo. Solo si ha pisado el campo: un jugador sin partidos no tiene nada
    /// que contar y el bloque no ocupa sitio.
    /// </summary>
    private void DrawRunStats(PlayerDefinition player, float top)
    {
        if (_state!.CareerOf(player.Id) is not { Matches: > 0 } career)
        {
            return;
        }

        Label(UiText.Get("ui.kn.run"), new Vector2(Margin + 2f, top));
        (string Key, int Value)[] cells =
        {
            ("ui.kn.runMatches", career.Matches),
            ("ui.kn.runGoals", career.Goals),
            ("ui.kn.runAssists", career.Assists),
            ("ui.kn.runTacklesWon", career.TacklesWon),
            ("ui.kn.runFouls", career.Fouls),
            ("ui.kn.runCards", career.Cards),
            ("ui.kn.runInjuriesCaused", career.InjuriesCaused),
            ("ui.kn.runInjuriesSuffered", career.InjuriesSuffered),
        };

        // Cifra y rótulo en la misma línea («6 goles»), dos filas de cuatro: es lo que cabe entre los perks y
        // el objeto sin achicar ninguno de los dos.
        float left = Margin + 4f;
        float cell = (Size.X - (Margin * 2f) - 8f) / 4f;
        for (int i = 0; i < cells.Length; i++)
        {
            float x = left + ((i % 4) * cell);
            float y = top + 22f + ((i / 4) * 24f);
            string figure = cells[i].Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Ink.Text(this, Ink.Heavy, new Vector2(x, y), figure, 20, cells[i].Value > 0 ? Ink.Black : Ink.Muted);
            float figureWidth = Ink.Width(Ink.Heavy, figure, 20);
            string caption = Ink.Fit(Ink.Data, UiText.Get(cells[i].Key), 13, cell - figureWidth - 10f);
            Ink.Text(this, Ink.Data, new Vector2(x + figureWidth + 4f, y + 5f), caption, 13, Ink.Muted);
            if (i == 6 && career.DeathsCaused > 0)
            {
                // Las muertes causadas cuentan aparte de las lesiones: son otro hecho, y el más pesado.
                Ink.Text(this, Ink.Heavy, new Vector2(x + 2f, y + 22f), UiText.Get(career.DeathsCaused == 1 ? "ui.kn.runDeath" : "ui.kn.runDeaths", career.DeathsCaused), 13, Ink.Red);
            }
        }
    }

    private void Label(string text, Vector2 at) =>
        Ink.Text(this, Ink.Display, at, text.ToUpperInvariant(), 17, Ink.RedDark);

    /// <summary>
    /// Una fila de carteles que siempre cabe: si los rótulos no caben a 16 px se encogen hasta 13, y si aun
    /// así no caben se recortan por igual. Nunca se cae un cartel de la fila sin decirlo.
    /// </summary>
    private void BadgeRow(List<(Tip Tip, Glyph Glyph, Color Paint, bool Empty)> badges, float left, float right, float top)
    {
        if (badges.Count == 0)
        {
            return;
        }

        const float Chrome = 46f;
        const float Gap = 10f;
        float available = right - left - (Gap * (badges.Count - 1));
        int size = 16;
        float Natural(int s)
        {
            float total = 0f;
            foreach (var badge in badges)
            {
                total += badge.Empty ? 40f : Ink.Width(Ink.Heavy, Caption(badge), s) + Chrome;
            }

            return total;
        }

        while (size > 13 && Natural(size) > available)
        {
            size--;
        }

        float share = available / badges.Count;
        bool squeeze = Natural(size) > available;
        float x = left;
        int index = 0;
        foreach (var badge in badges)
        {
            string caption = Caption(badge);
            float textWidth = Ink.Width(Ink.Heavy, caption, size);
            if (squeeze && textWidth + Chrome > share)
            {
                caption = Ink.Fit(Ink.Heavy, caption, size, share - Chrome);
                textWidth = Ink.Width(Ink.Heavy, caption, size);
            }

            var rect = new Rect2(x, top, badge.Empty ? 40f : textWidth + Chrome, 34f);
            if (badge.Empty)
            {
                var edge = Ink.Closed(Ink.Rough(rect.Grow(-2f), 1f, (int)x));
                for (int i = 0; i < edge.Length - 1; i++)
                {
                    Style.DrawDashed(this, edge[i], edge[i + 1], Ink.Muted, 2f, 5f);
                }

                InkIcons.Draw(this, Glyph.EmptySlot, rect.GetCenter(), 22f);
            }
            else
            {
                Ink.Brush(this, rect, badge.Paint, (index * 31) + (int)x);
                InkIcons.Draw(this, badge.Glyph, new Vector2(x + 19f, top + 17f), 24f, badge.Glyph == Glyph.Perk ? Ink.Paper : null);
                var at = new Vector2(x + 36f, top + 17f - (Ink.Heavy.GetHeight(size) / 2f));
                if (badge.Paint == Ink.Ochre)
                {
                    Ink.Text(this, Ink.Heavy, at, caption, size, Ink.Black);
                }
                else
                {
                    Ink.Outlined(this, Ink.Heavy, at, caption, size, Ink.Paper, Ink.Black, 4);
                }
            }

            Zone(rect, badge.Tip);
            x = rect.End.X + Gap;
            index++;
        }

        static string Caption((Tip Tip, Glyph Glyph, Color Paint, bool Empty) badge) => badge.Tip.Title.ToUpperInvariant();
    }

    // ------------------------------------------------------------------ objeto

    private void DrawItem()
    {
        var box = ItemBox();
        Ink.Text(this, Ink.Display, new Vector2(box.Position.X + 2f, box.Position.Y - 26f), UiText.Get("ui.kn.item").ToUpperInvariant(), 18, Ink.RedDark);
        Ink.Slab(this, box, Ink.PaperWarm, 5150, 1.6f, 3f, new Vector2(3f, 4f));

        var art = new Rect2(box.Position + new Vector2(10f, 8f), new Vector2(120f, box.Size.Y - 16f));
        if (_item is null)
        {
            var edge = Ink.Closed(Ink.Rough(art, 1f, 17));
            for (int i = 0; i < edge.Length - 1; i++)
            {
                Style.DrawDashed(this, edge[i], edge[i + 1], Ink.Muted, 2.2f, 6f);
            }

            InkIcons.Draw(this, Glyph.EmptySlot, art.GetCenter(), 46f);
            Ink.Text(this, Ink.Display, new Vector2(art.End.X + 18f, box.Position.Y + 22f), UiText.Get("ui.kn.noItem").ToUpperInvariant(), 26, Ink.Brown);
            Ink.Text(this, Ink.Plain, new Vector2(art.End.X + 18f, box.Position.Y + 62f), UiText.Get("ui.kn.noItemHint"), 15, Ink.Muted);
            Zone(box, TeamTips.NoItem());
        }
        else
        {
            var item = _item;

            // Mancha de pintura ocre detrás del objeto, que lo hace protagonista del recuadro.
            var splash = Pregon.Burst(52f, 40f, 11, art.GetCenter());
            DrawColoredPolygon(Ink.Shift(splash, new Vector2(3f, 3f)), new Color(0f, 0f, 0f, 0.25f));
            DrawColoredPolygon(splash, Ink.Ochre);
            InkIcons.Draw(this, InkIcons.OfItem(item.Id), art.GetCenter(), 88f);
            if (item.IsRelic)
            {
                Ink.Stamp(this, art.Position + new Vector2(28f, 10f), UiText.Get("ui.kn.relic").ToUpperInvariant(), Ink.RedDark, -12f, 12, Ink.Paper);
            }

            float left = art.End.X + 16f;
            string name = UiText.Name(item.Name).ToUpperInvariant();
            int nameSize = Ink.FitSize(Ink.Display, name, 22, box.End.X - left - 20f, 15);
            float nameWidth = Ink.Width(Ink.Display, name, nameSize);
            Ink.Brush(this, new Rect2(left - 6f, box.Position.Y + 10f, nameWidth + 24f, Ink.Display.GetHeight(nameSize) + 2f), Ink.Ochre, 311);
            Ink.Text(this, Ink.Display, new Vector2(left + 6f, box.Position.Y + 10f), name, nameSize, Ink.Black);

            float y = box.Position.Y + 18f + Ink.Display.GetHeight(nameSize);
            var modifiers = TeamTips.Modifiers(_state!, item);

            // Hasta dos modificadores en columna; tres o cuatro (los malditos) en dos columnas, para que
            // la contrapartida negativa nunca se quede fuera del recuadro.
            int perColumn = modifiers.Count <= 2 ? 2 : (modifiers.Count + 1) / 2;
            float columnWidth = (box.End.X - left - 16f) / 2f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var line = modifiers[i];
                float cx = left + ((i / perColumn) * columnWidth);
                float cy = y + ((i % perColumn) * 22f);
                InkIcons.Draw(this, line.Glyph, new Vector2(cx + 9f, cy + 10f), 19f);
                var color = line.Color == Ink.GreenLight ? Ink.Green : Ink.Red;
                Ink.Text(this, Ink.Heavy, new Vector2(cx + 24f, cy), line.Text, 17, color);
            }

            Zone(box, TeamTips.Item(_state!, item));
        }
    }
}
