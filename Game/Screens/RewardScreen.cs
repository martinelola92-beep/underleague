using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>Recompensa</b> (RF-071, RF-071b, RF-072, ADR 0043, ADR 0049): las opciones del nodo con
/// su descripción generada, a quién asignarlas, el reroll con su coste creciente y el botón de rechazar.
/// <para>
/// Cuántas opciones hay no lo decide la pantalla: lo dice el tipo de nodo (dos en liga, tres en élite,
/// dos elecciones de tres en el jefe), y la pantalla lo <b>explica</b> en una línea, porque la diferencia
/// entre la ruta segura y la peligrosa es justamente esa y el jugador tiene que poder verla.
/// </para>
/// <para>
/// La advertencia de RF-072 —un perk asignado no se puede retirar ni transferir— aparece <b>junto a la
/// lista de portadores</b>, en el momento de decidir, no en un tutorial: es una decisión que dura toda la
/// run y se toma con un clic.
/// </para>
/// </summary>
public partial class RewardScreen : Control
{
    private readonly List<OptionCard> _cards = new();

    private RunController _run = null!;
    private RewardScreenView _view = null!;
    private Label _error = null!;
    private int _selected = -1;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Route(this);
            return;
        }

        _run = run;
        Rebuild();
    }

    /// <summary>Vuelve a pedir la recompensa al controlador y repinta desde cero, sin selección.</summary>
    private void Rebuild()
    {
        var view = _run.Reward();
        if (view is null)
        {
            Leave();
            return;
        }

        _view = view;
        _selected = -1;
        Render();
    }

    /// <summary>
    /// Repinta la pantalla con la selección actual. Se rehace entera en vez de sincronizar dos estados:
    /// la pantalla es barata de construir y así no hay ninguna forma de que lo que se ve y lo que está
    /// elegido se separen.
    /// </summary>
    private void Render()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _cards.Clear();
        Build();

        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Selected = i == _selected;
        }
    }

    private void Build()
    {
        Widgets.Background(this);
        Widgets.Header(
            this,
            UiText.Get("ui.reward.title"),
            UiText.Get(
                "ui.reward.subtitle",
                UiText.Get("ui.kind." + _view.NodeKind),
                _view.PicksTaken + 1,
                _view.Picks,
                _view.Gold));

        Widgets.Body(this, OptionsExplanation(), new Vector2(16f, 56f), 1248f, Style.TextDim);

        int count = Math.Max(1, _view.Options.Count);
        float width = (1256f - ((count - 1) * 16f)) / count;
        for (int i = 0; i < _view.Options.Count; i++)
        {
            var option = _view.Options[i];
            float x = 12f + (i * (width + 16f));

            var card = new OptionCard
            {
                Position = new Vector2(x, 84f),
                Size = new Vector2(width, 196f),
            };
            AddChild(card);
            card.Bind(
                i,
                BadgeOf(option.Kind),
                ColorOf(option.Kind),
                option.Name,
                UiText.Get("ui.card.rarity." + option.Rarity),
                option.Headline,
                option.Description,
                Notes(option),
                alwaysOpen: true);
            card.Activated += Select;
            _cards.Add(card);

            // Una opción que no necesita portador se cobra con este mismo botón: obligar a un segundo
            // clic para confirmar lo que ya se ha elegido es una pantalla que estorba (UI-003).
            var button = Widgets.Button(
                this,
                UiText.Get(option.NeedsCarrier ? "ui.reward.assign" : "ui.reward.choose"),
                new Rect2(x, 288f, width, 26f),
                option.Block == RewardBlock.None);
            int index = i;
            bool needsCarrier = option.NeedsCarrier;
            button.Pressed += () =>
            {
                if (needsCarrier)
                {
                    Select(index);
                    return;
                }

                _selected = index;
                Choose(-1);
            };
        }

        Carriers();

        Widgets.Body(this, UiText.Get("ui.reward.declineHint"), new Vector2(12f, 666f), 1256f, Style.TextDim);
        Widgets.Button(this, UiText.Get("ui.reward.decline"), new Rect2(12f, 690f, 220f, 26f)).Pressed += Decline;
        var reroll = Widgets.Button(
            this,
            UiText.Get("ui.reward.reroll", _view.RerollCost),
            new Rect2(244f, 690f, 260f, 26f),
            _view.CanReroll);
        reroll.Pressed += Reroll;

        string rerollNote = _view.RerollUsedHere
            ? UiText.Get("ui.reward.rerollUsed")
            : _view.CanReroll ? string.Empty : UiText.Get("ui.reward.rerollPoor", _view.RerollCost, _view.Gold);
        Widgets.Body(this, rerollNote, new Vector2(516f, 696f), 740f, Style.TextDim);

        _error = Widgets.Body(this, string.Empty, new Vector2(12f, 726f), 1256f, Style.Hole);
        Widgets.InputHelp(this, UiText.Get("ui.input.mouseReward"), UiText.Get("ui.input.padPending"));
    }

    /// <summary>
    /// Panel de asignación: solo aparece cuando hay una opción elegida que necesita portador. La
    /// advertencia de irreversibilidad (RF-072) va encima de la lista, no debajo.
    /// </summary>
    private void Carriers()
    {
        Widgets.Panel(this, new Rect2(12f, 326f, 1256f, 330f));
        Widgets.Section(this, UiText.Get("ui.reward.carrier"), new Vector2(24f, 332f), 400f);

        if (_selected < 0)
        {
            Widgets.Body(this, UiText.Get("ui.reward.pick"), new Vector2(24f, 354f), 1232f, Style.TextDim);
            return;
        }

        var option = _view.Options[_selected];
        if (!option.NeedsCarrier)
        {
            Widgets.Body(this, option.Name + " · " + option.Description, new Vector2(24f, 354f), 1232f);
            Widgets.Body(this, UiText.Get("ui.reward.noCarrierNeeded"), new Vector2(24f, 386f), 1232f, Style.TextDim);
            Widgets.Button(this, UiText.Get("ui.reward.choose"), new Rect2(24f, 410f, 260f, 26f), option.Block == RewardBlock.None)
                .Pressed += () => Choose(-1);
            return;
        }

        Widgets.Body(
            this,
            UiText.Get(option.Kind == RewardKind.Perk ? "ui.reward.carrierPerk" : "ui.reward.carrierItem"),
            new Vector2(24f, 352f),
            1232f,
            option.Kind == RewardKind.Perk ? Style.Hole : Style.TextDim);

        if (option.Carriers.Count == 0)
        {
            Widgets.Body(this, UiText.Get("ui.reward.blockCarrier"), new Vector2(24f, 378f), 1232f, Style.TextDim);
            return;
        }

        for (int i = 0; i < option.Carriers.Count; i++)
        {
            var carrier = option.Carriers[i];
            float x = 24f + ((i % 3) * 412f);
            float y = 378f + ((i / 3) * 30f);
            if (y > 620f)
            {
                break;
            }

            var button = Widgets.Button(
                this,
                UiText.Get(
                    "ui.reward.carrierRow",
                    carrier.Name,
                    UiText.Get("ui.pos." + carrier.Position),
                    carrier.Level,
                    UiText.Get("ui.state." + carrier.PhysicalState),
                    option.Kind == RewardKind.Perk
                        ? UiText.Get(carrier.FreeSlots == 1 ? "ui.reward.slot" : "ui.reward.slots", carrier.FreeSlots)
                        : carrier.CurrentItemId is not null
                            ? UiText.Get("ui.reward.carrierHasItem", carrier.CurrentItemName)
                            : UiText.Get("ui.reward.carrierNoItem")),
                new Rect2(x, y, 400f, 26f));

            int playerId = carrier.PlayerId;
            button.Pressed += () => Choose(playerId);
        }
    }

    private void Select(int index)
    {
        _selected = _selected == index ? -1 : index;
        Render();
    }

    private void Choose(int carrierId) => Decide(new ChooseReward(_selected, carrierId));

    private void Decline() => Decide(new DeclineReward());

    private void Reroll() => Decide(new RerollRewards());

    /// <summary>
    /// Manda la decisión al controlador. Si <c>/Sim</c> la rechaza, el motivo se enseña en pantalla: un
    /// error de reglas es información para el jugador, no una excepción silenciosa.
    /// </summary>
    private void Decide(RunDecision decision)
    {
        try
        {
            _run.Apply(decision);
        }
        catch (Exception error)
        {
            _error.Text = UiText.Get("ui.reward.error", error.Message);
            return;
        }

        Rebuild();
    }

    /// <summary>Cerrado el nodo (todas las elecciones resueltas), se vuelve al mapa.</summary>
    private void Leave()
    {
        if (_run.State is { Phase: RunPhase.NodeOpen, PendingNodeId: >= 0 })
        {
            _run.Apply(new LeaveNode());
        }

        Nav.Route(this);
    }

    private string OptionsExplanation() => _view.NodeKind switch
    {
        NodeKind.EliteMatch => UiText.Get("ui.reward.optionsElite"),
        NodeKind.Boss => UiText.Get("ui.reward.optionsBoss"),
        _ => UiText.Get("ui.reward.optionsLeague"),
    };

    private IReadOnlyList<string> Notes(RewardOptionView option)
    {
        var notes = new List<string>();
        if (option.Block == RewardBlock.NoCarrier)
        {
            notes.Add(UiText.Get("ui.reward.blockCarrier"));
        }
        else if (option.Block == RewardBlock.RosterFull)
        {
            notes.Add(UiText.Get("ui.reward.blockRoster", _run.State!.RosterSize, _run.State!.RosterCapacity));
        }

        return notes;
    }

    private static string BadgeOf(RewardKind kind) => kind switch
    {
        RewardKind.Perk => UiText.Get("ui.reward.badgePerk"),
        RewardKind.Player => UiText.Get("ui.reward.badgePlayer"),
        _ => UiText.Get("ui.reward.badgeItem"),
    };

    private static Color ColorOf(RewardKind kind) => kind switch
    {
        RewardKind.Perk => Style.Accent,
        RewardKind.Player => Style.Of(Sim.Model.Position.Midfielder),
        _ => Style.Of(Sim.Model.Position.Defender),
    };
}
