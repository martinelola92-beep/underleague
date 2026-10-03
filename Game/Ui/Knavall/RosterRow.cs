using Godot;
using Underleague.Game.Data;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Game.Ui.Knavall;

/// <summary>
/// Fila de la plantilla en la pantalla de Equipo (ADR 0162): <b>retrato grande</b>, insignia de nivel, nombre,
/// puesto y un icono de papel y otro de estado. Nada más: el resto vive en la ficha del centro. El jugador
/// señalado sobresale en ocre; el que se está moviendo al campo lleva borde rojo discontinuo.
/// <para>
/// Sustituye a <see cref="PlayerCard"/> <b>solo en Equipo</b>: la ficha compartida sigue en Mercado, Ojeo y
/// Fin de run hasta que el lenguaje se extienda a esas pantallas. Mantiene su contrato con la pantalla
/// —<see cref="ActivatedEventHandler"/>, <see cref="Flash"/>, <see cref="Selected"/>— para que la lógica de
/// selección y de coger y soltar no cambie.
/// </para>
/// </summary>
public partial class RosterRow : InkCanvas
{
    private TeamState? _state;
    private PlayerDefinition? _player;
    private ItemDefinition? _item;
    private bool _starter;
    private bool _selected;
    private bool _held;
    private float _flash;

    /// <summary>La fila ha sido activada: un clic o el botón de acción del mando (UI-001, mismo gesto).</summary>
    [Signal]
    public delegate void ActivatedEventHandler(int playerId);

    /// <summary>Se ha empezado a arrastrar a este jugador (BX-3): la pantalla lo coge, como al pulsar.</summary>
    [Signal]
    public delegate void DragStartedEventHandler(int playerId);

    /// <summary>Se ha soltado a <paramref name="draggedId"/> sobre esta fila (BX-3).</summary>
    [Signal]
    public delegate void DroppedOnEventHandler(int draggedId, int targetId);

    /// <summary>
    /// La pantalla dice si soltar a ese jugador sobre este otro tiene efecto (BX-3). Es solo para el resalte: lo que pasa
    /// de verdad al soltar lo resuelve <c>/Sim</c>.
    /// </summary>
    public System.Func<int, int, bool>? CanDrop { get; set; }

    private bool _dropHover;

    public int PlayerId => _player?.Id ?? -1;

    // ---- Arrastrar y soltar (BX-3) ----------------------------------------------------------------

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_player is null)
        {
            return default;
        }

        EmitSignal(SignalName.DragStarted, _player.Id);
        SetDragPreview(LineupDrag.Preview(_player.Name));
        return LineupDrag.Pack(_player.Id);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool valid = _player is not null && LineupDrag.TryUnpack(data, out int dragged) && (CanDrop?.Invoke(dragged, _player.Id) ?? false);
        if (valid != _dropHover)
        {
            _dropHover = valid;
            QueueRedraw();
        }

        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _dropHover = false;
        QueueRedraw();
        if (_player is not null && LineupDrag.TryUnpack(data, out int dragged))
        {
            EmitSignal(SignalName.DroppedOn, dragged, _player.Id);
        }
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            QueueRedraw();
        }
    }

    /// <summary>El jugador está cogido para soltarlo en el campo.</summary>
    public bool Held
    {
        get => _held;
        set
        {
            _held = value;
            QueueRedraw();
        }
    }

    public void Bind(TeamState state, PlayerDefinition player)
    {
        _state = state;
        _player = player;
        _item = state.EquippedItemOf(player.Id);
        _starter = state.IsStarter(player.Id);
        QueueRedraw();
    }

    /// <summary>Estado reactivo (UI-013): la fila destella en ocre y se apaga sola.</summary>
    public void Flash()
    {
        _flash = 1f;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Ready()
    {
        base._Ready();
        SetProcess(false);
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if ((what == NotificationDragEnd || what == NotificationMouseExit) && _dropHover)
        {
            _dropHover = false;
            QueueRedraw();
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

    public override void _Draw()
    {
        ClearZones();
        if (_player is null || _state is null)
        {
            return;
        }

        var player = _player;
        float w = Size.X;
        float h = Size.Y;
        int seed = (player.Id * 37) + 5;
        bool hover = HoverKey == "row";

        var body = new Rect2(0f, 2f, w - (_selected ? 0f : 10f), h - 5f);
        var fill = _selected ? Ink.Ochre : hover ? Ink.Paper : Ink.PaperWarm;
        Ink.Slab(this, body, fill, seed, 1.2f, _selected ? 3.2f : 2.4f, _selected ? new Vector2(5f, 5f) : new Vector2(2f, 3f));
        if (_flash > 0f)
        {
            DrawColoredPolygon(Ink.Rough(body, 1.2f, seed), new Color(Ink.Ochre, _flash * 0.7f));
        }

        if (_dropHover)
        {
            // BX-3: el destino del arrastre. Verde sobre el papel, grueso, para distinguirlo de lo «cogido» (rojo, discontinuo).
            DrawPolyline(Ink.Closed(Ink.Rough(body.Grow(3f), 1f, seed + 7)), Ink.GreenLight, 5f, true);
        }

        if (_held)
        {
            var dashed = Ink.Closed(Ink.Rough(body.Grow(2f), 1f, seed + 3));
            for (int i = 0; i < dashed.Length - 1; i++)
            {
                Style.DrawDashed(this, dashed[i], dashed[i + 1], Ink.Red, 3f, 6f);
            }
        }

        Zone(new Rect2(Vector2.Zero, Size), null, () => EmitSignal(SignalName.Activated, player.Id), "row");

        // Retrato: lo más grande de la fila.
        float side = h - 10f;
        var portrait = new Rect2(6f, 4f, side, side);
        Portrait.Draw(this, portrait, player.Race, player.Position, player.Id, dimmed: player.PhysicalState is PhysicalState.SevereInjury or PhysicalState.Dead);

        // BX-4: el dorsal fijo de la run (el mismo que luce en el campo y en las tiras del partido), en la esquina
        // opuesta al nivel, sobre una cartela negra para que no se confunda con él.
        if (player.ShirtNumber > 0)
        {
            string number = UiText.Get("ui.kn.shirt", player.ShirtNumber);
            float numberWidth = Ink.Width(Ink.Heavy, number, Ink.SizeSmall);
            var chip = new Rect2(portrait.Position.X, portrait.Position.Y, numberWidth + 8f, Ink.Heavy.GetHeight(Ink.SizeSmall) + 2f);
            DrawRect(chip, Ink.Black);
            Ink.Text(this, Ink.Heavy, chip.Position + new Vector2(4f, 1f), number, Ink.SizeSmall, Ink.Paper);
        }

        // Nivel sobre la esquina del retrato.
        float badge = Mathf.Clamp(h * 0.2f, 9f, 12f);
        var badgeCenter = new Vector2(portrait.End.X - 1f, portrait.End.Y - badge + 2f);
        Ink.LevelBadge(this, badgeCenter, badge, player.Level);
        Zone(new Rect2(portrait.Position, portrait.Size + new Vector2(badge, 0f)), TeamTips.Level(player));

        // Iconos de la derecha: papel (posición) y estado físico.
        float stateX = w - (_selected ? 22f : 32f);
        float roleX = stateX - 34f;
        float mid = h / 2f;
        float roleSide = Mathf.Min(h * 0.5f, 26f);
        InkIcons.Draw(this, InkIcons.Of(player.Position), new Vector2(roleX, mid), roleSide);
        Zone(new Rect2(roleX - 16f, mid - 16f, 32f, 32f), TeamTips.Position(_state, player.Position));

        InkIcons.Draw(this, InkIcons.Of(player.PhysicalState), new Vector2(stateX, mid), Mathf.Min(h * 0.4f, 21f), StateTint(player.PhysicalState));
        Zone(new Rect2(stateX - 14f, mid - 14f, 28f, 28f), TeamTips.State(player.PhysicalState));

        // Nombre y puesto.
        float left = portrait.End.X + badge + 6f;
        float textWidth = roleX - 18f - left;
        int nameSize = Ink.FitSize(Ink.Heavy, player.Name, h >= 52f ? 18 : 16, textWidth, 14);
        float nameY = mid - Ink.Heavy.GetHeight(nameSize) + 1f;
        Ink.Text(this, Ink.Heavy, new Vector2(left, nameY), Ink.Fit(Ink.Heavy, player.Name, nameSize, textWidth), nameSize, Ink.Black);

        string sub = _state.Templates.Get("positions", player.Position.ToString()).ToUpperInvariant();

        float subY = mid + 1f;
        Ink.Text(this, Ink.Data, new Vector2(left, subY), Ink.Fit(Ink.Data, sub, Ink.SizeSmall, textWidth - 40f), Ink.SizeSmall, _selected ? Ink.Brown : Ink.Muted);

        // Huecos de perk y objeto (decisión del revisor, 20 sep 2026: se ven sin abrir la ficha), en pequeño
        // y juntos al final de la segunda línea para que no compitan con el retrato.
        float slotsX = left + Mathf.Min(Ink.Width(Ink.Data, sub, Ink.SizeSmall), textWidth - 40f) + 8f;
        float slotsY = subY + (Ink.Data.GetHeight(Ink.SizeSmall) / 2f);
        int slots = Sim.Progression.Progression.PerkSlots(player.Rarity);
        float x = slotsX;
        for (int i = 0; i < slots; i++)
        {
            if (i < player.Perks.Count)
            {
                Ink.Disc(this, new Vector2(x + 3f, slotsY), 3.2f, Ink.Red, 1.2f);
            }
            else
            {
                DrawArc(new Vector2(x + 3f, slotsY), 3.2f, 0f, Mathf.Tau, 12, Ink.Muted, 1.2f, true);
            }

            x += 9f;
        }

        if (_item is not null)
        {
            InkIcons.Draw(this, InkIcons.OfItem(_item.Id), new Vector2(x + 7f, slotsY), 15f);
            x += 16f;
        }

        Zone(new Rect2(slotsX - 2f, slotsY - 8f, x - slotsX + 4f, 16f), new Tip(UiText.Get("ui.kn.perks"), UiText.Get("ui.kn.tip.slots"), Glyph.Perk));

        if (_held)
        {
            Ink.Stamp(this, new Vector2(w - 70f, 12f), UiText.Get("ui.kn.moving").ToUpperInvariant(), Ink.Red, -4f, 12, Ink.Paper);
        }
    }

    private static Color StateTint(PhysicalState state) => state switch
    {
        PhysicalState.Healthy => Ink.GreenLight,
        PhysicalState.MinorInjury => Ink.Ochre,
        PhysicalState.SevereInjury => Ink.Red,
        _ => Pregon.Sable,
    };
}
