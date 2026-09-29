using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Broadcast;
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
            Layout.CenterLegacy(this);
            Widgets.Background(this);
            Widgets.Header(this, UiText.Get("ui.report.title"), UiText.Get("ui.report.none"));
            Widgets.Button(this, UiText.Get("ui.nav.team"), new Rect2(932f, 706f, 132f, 26f)).Pressed += ViewTeam;
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

    /// <summary>Botón "Ver equipo" (AW-N): deja dicho el camino de vuelta y navega. El informe no
    /// cambia con la run: se vuelve a pedir igual (<c>run.PostMatch()</c>), así que no hay nada que
    /// perder al pasar por Equipo.</summary>
    private void ViewTeam()
    {
        Nav.ReturnTo = Nav.Report;
        Nav.Go(this, Nav.Team);
    }

    private void Build()
    {
        Layout.CenterLegacy(this);
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

        Widgets.Button(this, UiText.Get("ui.nav.team"), new Rect2(932f, 706f, 132f, 26f)).Pressed += ViewTeam;
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

            // ADR 0161 §2: la reliquia llega al cofre sin que nadie la vea llegar; el informe lo dice.
            if (casualty.RelicName.Length > 0)
            {
                text += "\n" + UiText.Get("ui.report.relic", casualty.PlayerName, casualty.RelicName);
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
            // Rubrica, como Widgets.Section: es una cabecera de sub-lista sobre el pergamino de la
            // columna de perks, y el dorado de Style.Accent se lee peor ahí que sobre madera.
            AddLine(column, UiText.Get("ui.report.items"), Pregon.Wax);
            foreach (var item in _report.Items)
            {
                var card = new OptionCard();
                column.AddChild(card);
                card.Bind(
                    0,
                    UiText.Get("ui.reward.badgeItem"),

                    // Style.LinkLine es un gris translúcido para el césped de PitchView, no una placa
                    // opaca de distintivo sobre pergamino: se lavaba casi invisible ahí.
                    Style.NeutralBadge,
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
        Widgets.Panel(this, new Rect2(920f, 52f, 348f, 268f));
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
            y += 6f;
            y = Row(y, UiText.Get("ui.report.goldTotal"), Amount(gold.Total), Style.Accent);
        }

        var now = Widgets.Body(this, UiText.Get("ui.report.goldNow", _run.State!.Gold), new Vector2(932f, y + 6f), 324f, Style.TextDim);

        // Botín de liga (ADR 0161 §1): un objeto común más al almacén, además del oro de arriba. Null en
        // cualquier otro nodo o en una liga perdida (PostMatchView.Loot), así que la línea solo aparece
        // cuando de verdad cayó algo.
        if (_report.Loot is { } loot)
        {
            Widgets.Body(
                this,
                UiText.Get("ui.report.loot", loot.ItemName),
                new Vector2(932f, y + 6f + now.Size.Y + 4f),
                324f,
                Style.Accent);
        }

        // ADR 0157: cómo terminó la apuesta que tomaste para este partido, si tomaste una. Va bajo el oro
        // porque es oro, y con el nombre de la apuesta para que el jugador sepa qué cobró o qué perdió.
        if (_report.BetRefunded > 0)
        {
            float refundY = y + 6f + now.Size.Y + (_report.Loot is null ? 0f : 22f) + 8f;
            Widgets.Body(this, UiText.Get("ui.bet.refunded", _report.BetRefunded), new Vector2(932f, refundY), 324f, Style.TextDim);
        }

        if (_report.Bet is { } bet)
        {
            float betY = y + 6f + now.Size.Y + (_report.Loot is null ? 0f : 22f) + 8f;
            var definition = _run.Systems?.Bets.Find(bet.BetId);
            string betName = definition is null ? bet.BetId : UiText.Name(definition.Name);
            Widgets.Section(this, UiText.Get("ui.report.bet"), new Vector2(932f, betY), 320f);
            Widgets.Body(
                this,
                bet.Met ? UiText.Get("ui.report.betWon", bet.GoldPaid, betName) : UiText.Get("ui.report.betLost", bet.Stake, betName),
                new Vector2(932f, betY + 18f),
                324f,
                bet.Met ? Style.LinkCreated : Style.Hole);
        }

        Widgets.Panel(this, new Rect2(920f, 326f, 348f, 124f));
        Widgets.Section(this, UiText.Get("ui.report.referee"), new Vector2(932f, 332f), 320f);
        var referee = _report.Referee;
        Widgets.Body(this, referee.Name, new Vector2(932f, 354f), 324f);
        Widgets.Body(this, UiText.Get("ui.report.refereeBias", referee.InitialBias, referee.FinalBias), new Vector2(932f, 372f), 324f, Style.TextDim);

        // ADR 0158 §5 (la mitad de RF-119 que faltaba): FoulsFor/FoulsAgainst ya CUENTAN las no
        // señaladas (Unseen es un subconjunto, no una cifra aparte), así que "señaladas" se resta aquí
        // en vez de sumarse -sumar habría doblado la falta no vista-. Retira el hueco declarado
        // ui.report.refereeGap.
        Widgets.Body(
            this,
            UiText.Get("ui.report.refereeFouls", referee.FoulsFor - referee.UnseenFoulsFor, referee.FoulsAgainst - referee.UnseenFoulsAgainst),
            new Vector2(932f, 392f),
            324f);
        Widgets.Body(this, UiText.Get("ui.report.refereeCards", referee.CardsFor, referee.CardsAgainst), new Vector2(932f, 410f), 324f);
        Widgets.Body(
            this,
            UiText.Get("ui.report.refereeUnseen", referee.UnseenFoulsFor, referee.UnseenFoulsAgainst),
            new Vector2(932f, 428f),
            324f,
            Style.TextDim);

        Stats();
    }

    /// <summary>
    /// Estadísticas de cada jugador propio en el partido y los apodos que este partido ha dado (ADR 0163).
    /// Las cifras son las de <c>MatchReport.Players</c>, tal cual las compone <c>PostMatchView</c>. Los
    /// apodos ganados van arriba: son lo que el jugador no debe perderse.
    /// </summary>
    private void Stats()
    {
        const float panelTop = 458f;
        Widgets.Panel(this, new Rect2(920f, panelTop, 348f, 240f));
        Widgets.Section(this, UiText.Get("ui.report.stats"), new Vector2(932f, panelTop + 6f), 320f);

        float y = panelTop + 26f;
        int shown = 0;
        foreach (var gain in _report.NicknamesEarned)
        {
            if (shown == 2)
            {
                break;
            }

            string text = gain.PreviousNickname.Length > 0
                ? UiText.Get("ui.report.nicknameUpgraded", gain.PlayerName, gain.Nickname, gain.PreviousNickname)
                : UiText.Get("ui.report.nicknameEarned", gain.PlayerName, gain.Nickname);
            var line = Widgets.Body(this, text, new Vector2(932f, y), 324f, Style.Accent);
            y += line.Size.Y + 2f;
            shown++;
        }

        if (_report.PlayerStats.Count == 0)
        {
            Widgets.Body(this, UiText.Get("ui.report.statsNone"), new Vector2(932f, y), 324f, Style.TextDim);
            return;
        }

        // Una etiqueta por columna, alineada a la derecha: la fuente no es de ancho fijo.
        string[] heads = UiText.Get("ui.report.statsHead").Split(' ');
        for (int c = 0; c < heads.Length; c++)
        {
            StatCell(heads[c], c, y, Style.TextDim);
        }

        y += 15f;

        foreach (var row in _report.PlayerStats)
        {
            if (y > panelTop + 240f - 16f)
            {
                break;
            }

            var name = Widgets.Body(this, Sim.Run.Systems.Nicknames.NicknameSystem.Display(row.PlayerName, row.Nickname), new Vector2(932f, y), 160f);
            name.AutowrapMode = TextServer.AutowrapMode.Off;
            name.ClipText = true;
            name.Size = new Vector2(160f, name.Size.Y);

            int[] values = { row.Goals, row.Assists, row.TacklesWon, row.Fouls, row.InjuriesCaused };
            for (int c = 0; c < values.Length; c++)
            {
                StatCell(Amount(values[c]), c, y, values[c] > 0 ? Style.Text : Style.TextDim);
            }

            y += 15f;
        }
    }

    private void StatCell(string text, int column, float y, Color color)
    {
        var cell = Widgets.Body(this, text, new Vector2(1098f + (column * 32f), y), 30f, color);
        cell.AutowrapMode = TextServer.AutowrapMode.Off;
        cell.HorizontalAlignment = HorizontalAlignment.Right;
        cell.Size = new Vector2(30f, cell.Size.Y);
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
