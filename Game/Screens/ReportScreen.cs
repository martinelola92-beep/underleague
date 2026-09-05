using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// <b>Informe post-partido</b> (RF-119): la pantalla obligatoria que explica <b>por qué</b> pasó lo que
/// pasó. Es el principal vehículo de aprendizaje del jugador, así que todo lo que lista viene con su
/// causa: cada perk con su descripción generada y lo que cayó en sus activaciones, cada baja con el
/// minuto y quién la provocó, y cada moneda con el escalón del que sale.
/// <para>
/// Orden de lectura deliberado: primero <b>las bajas</b> —las muertes arriba del todo y en rojo, porque
/// son irreversibles (RF-093)—, después los perks, y solo al final el oro. Un informe que empieza por el
/// dinero enseña a mirar el dinero.
/// </para>
/// <para>La pantalla no calcula nada (RT-014): el informe lo compone <c>Sim.Run.View.PostMatchView</c>.</para>
/// </summary>
public partial class ReportScreen : Control
{
    private RunController _run = null!;
    private PostMatchReport _report = null!;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Route(this);
            return;
        }

        _run = run;
        var report = run.PostMatch();
        if (report is null)
        {
            Widgets.Background(this);
            Widgets.Header(this, UiText.Get("ui.report.title"), UiText.Get("ui.report.none"));
            Widgets.Button(this, UiText.Get("ui.report.continue"), new Rect2(1076f, 706f, 180f, 26f)).Pressed += Continue;
            return;
        }

        _report = report;
        Build();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_cancel"))
        {
            Continue();
        }
    }

    private void Continue() => Nav.Route(this);

    private void Build()
    {
        Widgets.Background(this);
        Widgets.Header(
            this,
            UiText.Get("ui.report.title"),
            UiText.Get(
                "ui.report.subtitle",
                _report.OwnTeamName,
                _report.GoalsFor,
                _report.GoalsAgainst,
                _report.RivalTeamName,
                UiText.Get("ui.kind." + _report.NodeKind),
                _report.Minutes));

        var banner = Widgets.Title(
            this,
            UiText.Get(_report.Won ? "ui.report.victory" : "ui.report.defeat"),
            new Vector2(1060f, 12f),
            200f);
        banner.HorizontalAlignment = HorizontalAlignment.Right;
        banner.AddThemeColorOverride("font_color", _report.Won ? Style.LinkCreated : Style.Hole);

        Casualties();
        Perks();
        Gold();

        Widgets.Button(this, UiText.Get("ui.report.continue"), new Rect2(1076f, 706f, 180f, 26f)).Pressed += Continue;
        Widgets.InputHelp(this, UiText.Get("ui.input.mouseReport"), UiText.Get("ui.input.padPending"));
    }

    /// <summary>Columna izquierda de 376 px: bajas y tarjetas. Las muertes van primero y en rojo.</summary>
    private void Casualties()
    {
        Widgets.Panel(this, new Rect2(12f, 52f, Widgets.CardColumnWidth, 400f));
        Widgets.Section(this, UiText.Get("ui.report.casualties"), new Vector2(24f, 58f), 350f);

        float y = 80f;
        if (_report.Casualties.Count == 0)
        {
            Widgets.Body(this, UiText.Get("ui.report.casualtiesNone"), new Vector2(24f, y), 352f, Style.TextDim);
        }
        else
        {
            // Las muertes primero, aunque hayan pasado después: es la única baja que no se deshace.
            y = CasualtyBlock(y, death: true);
            y = CasualtyBlock(y, death: false);
        }

        Widgets.Panel(this, new Rect2(12f, 464f, Widgets.CardColumnWidth, 276f));
        Widgets.Section(this, UiText.Get("ui.report.cards"), new Vector2(24f, 470f), 350f);
        float cardY = 492f;
        if (_report.Cards.Count == 0)
        {
            Widgets.Body(this, UiText.Get("ui.report.cardsNone"), new Vector2(24f, cardY), 352f, Style.TextDim);
        }
        else
        {
            foreach (var card in _report.Cards)
            {
                var color = card.Red ? Style.Hole : Style.Of(Sim.Model.PhysicalState.MinorInjury);
                Widgets.Body(
                    this,
                    UiText.Get(
                        "ui.report.cardRow",
                        card.PlayerName,
                        UiText.Get(card.Red ? "ui.report.cardRed" : "ui.report.cardYellow"),
                        card.Minute)
                    + " · " + UiText.Get(card.Side == MatchSide.Own ? "ui.report.sideOwn" : "ui.report.sideRival"),
                    new Vector2(24f, cardY),
                    352f,
                    card.Side == MatchSide.Own ? color : Style.TextDim);
                cardY += 18f;
                if (cardY > 716f)
                {
                    break;
                }
            }
        }
    }

    private float CasualtyBlock(float y, bool death)
    {
        foreach (var casualty in _report.Casualties)
        {
            bool isDeath = casualty.Kind == CasualtyKind.Death;
            if (isDeath != death || y > 420f)
            {
                continue;
            }

            string text = isDeath
                ? UiText.Get("ui.report.deathRow", casualty.PlayerName, casualty.Minute)
                : UiText.Get(
                    "ui.report.injuryRow",
                    casualty.PlayerName,
                    UiText.Get(casualty.Kind == CasualtyKind.SevereInjury ? "ui.state.SevereInjury" : "ui.state.MinorInjury"),
                    casualty.Minute);

            if (casualty.Cause.Length > 0)
            {
                text += " · " + UiText.Get("ui.report.cause", casualty.Cause);
            }

            var color = casualty.Kind switch
            {
                CasualtyKind.Death => Style.Hole,
                CasualtyKind.SevereInjury => Style.Of(Sim.Model.PhysicalState.SevereInjury),
                _ => Style.Of(Sim.Model.PhysicalState.MinorInjury),
            };

            // El alto lo mide la propia etiqueta: una baja con causa larga envuelve y la siguiente no se
            // le puede montar encima.
            var label = Widgets.Body(this, text, new Vector2(24f, y), 352f, color);
            y += label.Size.Y + 4f;
        }

        return y;
    }

    /// <summary>
    /// Columna central: los perks que se activaron, con su descripción generada debajo. La descripción
    /// está aquí a propósito: el informe es donde se aprende qué hace un perk, y leerla junto a lo que
    /// cayó en sus activaciones es lo que convierte la lista en una explicación.
    /// </summary>
    private void Perks()
    {
        Widgets.Panel(this, new Rect2(400f, 52f, 508f, 688f));
        Widgets.Section(this, UiText.Get("ui.report.perks"), new Vector2(412f, 58f), 480f);

        var scroll = new ScrollContainer
        {
            Position = new Vector2(408f, 80f),
            Size = new Vector2(496f, 652f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        column.CustomMinimumSize = new Vector2(488f, 0f);
        scroll.AddChild(column);

        if (_report.Perks.Count == 0)
        {
            AddLine(column, UiText.Get("ui.report.perksNone"), Style.TextDim);
        }

        foreach (var perk in _report.Perks)
        {
            var card = new OptionCard();
            column.AddChild(card);
            card.Bind(
                0,
                UiText.Get("ui.reward.badgePerk"),
                Style.Accent,
                UiText.Get("ui.report.perkRow", perk.PerkName, perk.OwnerName),
                UiText.Get(perk.Activations == 1 ? "ui.report.activationOne" : "ui.report.activations", perk.Activations),
                Contribution(perk),
                perk.Description,
                alwaysOpen: true);
        }

        if (_report.Items.Count > 0)
        {
            AddLine(column, UiText.Get("ui.report.items"), Style.Accent);
            foreach (var item in _report.Items)
            {
                var card = new OptionCard();
                column.AddChild(card);
                card.Bind(
                    0,
                    UiText.Get("ui.reward.badgeItem"),
                    Style.LinkLine,
                    UiText.Get("ui.report.itemRow", item.ItemName, item.OwnerName),
                    string.Empty,
                    item.Restricted ? UiText.Get("ui.report.itemRestricted") : string.Empty,
                    item.Description,
                    alwaysOpen: true);
            }
        }
    }

    /// <summary>Contribución medible del perk (RF-119); si no cayó nada, se dice, no se deja en blanco.</summary>
    private static string Contribution(PerkReportRow perk)
    {
        var parts = new List<string>();
        Add(parts, perk.Goals, "ui.report.cGoal", "ui.report.cGoals");
        Add(parts, perk.InjuriesCaused, "ui.report.cInjury", "ui.report.cInjuries");
        Add(parts, perk.Recoveries, "ui.report.cRecovery", "ui.report.cRecoveries");
        Add(parts, perk.Saves, "ui.report.cSave", "ui.report.cSaves");
        Add(parts, perk.Cancellations, "ui.report.cCancel", "ui.report.cCancels");

        return parts.Count == 0
            ? UiText.Get("ui.report.contribNone")
            : UiText.Get("ui.report.contribution", string.Join(", ", parts));

        static void Add(List<string> into, int value, string one, string many)
        {
            if (value == 1)
            {
                into.Add(UiText.Get(one));
            }
            else if (value > 1)
            {
                into.Add(UiText.Get(many, value));
            }
        }
    }

    /// <summary>Columna derecha: el desglose del oro y el apartado del árbitro (RF-119, RF-114g..i).</summary>
    private void Gold()
    {
        Widgets.Panel(this, new Rect2(920f, 52f, 348f, 360f));
        Widgets.Section(this, UiText.Get("ui.report.gold"), new Vector2(932f, 58f), 320f);

        float y = 80f;
        if (_report.Gold is not { } gold)
        {
            Widgets.Body(this, UiText.Get("ui.report.goldNone"), new Vector2(932f, y), 324f, Style.TextDim);
            y += 24f;
        }
        else
        {
            // El desglose se lee como la cuenta que el jugador haría: una base, un multiplicador que da
            // un subtotal, y dos sumas. Enseñar la dificultad como diferencia daría números negativos
            // cuando el multiplicador baja de 100, que es lo contrario de explicar de dónde sale el oro.
            y = Row(y, UiText.Get("ui.report.goldBase", gold.Act), Amount(gold.ActBase));
            y = Row(
                y,
                UiText.Get("ui.report.goldDifficulty", gold.Difficulty, gold.DifficultyPercent),
                "= " + Amount(gold.AfterDifficulty));
            y = Row(y, UiText.Get("ui.report.goldNode", UiText.Get("ui.kind." + gold.NodeKind), gold.NodeBonusPercent), "+ " + Amount(gold.NodeBonus));
            y = Row(
                y,
                UiText.Get(
                    gold.ObjectiveMet ? "ui.report.goldObjective" : "ui.report.goldObjectiveFailed",
                    UiText.Get("ui.objective." + gold.Objective)),
                "+ " + Amount(gold.ObjectiveBonus));
            y += 6f;
            y = Row(y, UiText.Get("ui.report.goldTotal"), Amount(gold.Total), Style.Accent);
        }

        Widgets.Body(this, UiText.Get("ui.report.goldNow", _run.State!.Gold), new Vector2(932f, y + 6f), 324f, Style.TextDim);

        Widgets.Panel(this, new Rect2(920f, 424f, 348f, 316f));
        Widgets.Section(this, UiText.Get("ui.report.referee"), new Vector2(932f, 430f), 320f);
        var referee = _report.Referee;
        Widgets.Body(this, referee.Name, new Vector2(932f, 452f), 324f);
        Widgets.Body(this, UiText.Get("ui.report.refereeBias", referee.InitialBias, referee.FinalBias), new Vector2(932f, 470f), 324f, Style.TextDim);
        Widgets.Body(this, UiText.Get("ui.report.refereeFouls", referee.FoulsFor, referee.FoulsAgainst), new Vector2(932f, 490f), 324f);
        Widgets.Body(this, UiText.Get("ui.report.refereeCards", referee.CardsFor, referee.CardsAgainst), new Vector2(932f, 508f), 324f);
        Widgets.Body(this, UiText.Get("ui.report.refereeGap"), new Vector2(932f, 534f), 324f, Style.TextDim);
    }

    private float Row(float y, string text, string gold, Color? color = null)
    {
        Widgets.Body(this, text, new Vector2(932f, y), 240f, color ?? Style.Text);
        var amount = Widgets.Body(this, gold, new Vector2(1176f, y), 80f, color ?? Style.Accent);
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        return y + (text.Length > 40 ? 32f : 18f);
    }

    private static string Amount(int gold) => gold.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void AddLine(VBoxContainer column, string text, Color color)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(480f, 0f),
        };
        label.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        label.AddThemeColorOverride("font_color", color);
        column.AddChild(label);
    }
}
