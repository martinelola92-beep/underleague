using System.Collections.Generic;
using System.Globalization;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Data;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Bets;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>ojeo</b> (RF-012b): el informe completo del rival antes de decidir. Es opcional y
/// gratuito, y es el sitio donde se cumple o se rompe el principio rector del juego —todo lo malo que
/// pase en el partido tenía que ser previsible (RF-012d)—, así que enseña las cuatro cosas que hacen
/// previsible una muerte:
/// <list type="number">
/// <item>la <b>plantilla íntegra</b> del rival, con la misma ficha que la propia (UI-010) y su build;</item>
/// <item>el <b>árbitro</b> con su rasgo (RF-061);</item>
/// <item>los <b>perks letales destacados</b> (RF-013): desde la ADR 0048 un jugador sano también puede
/// morir, y lo único que separa eso del azar injusto es que se sepa antes;</item>
/// <item>el <b>riesgo por titular</b>, con su número (RF-012c), que es el mismo que tirará el motor y que
/// se mueve con a quién alineas, en qué estado y en qué casilla: por eso el botón de alinear está aquí,
/// al lado del número.</item>
/// </list>
/// <para>Nada de esto lo calcula la pantalla (RT-014): el partido lo arma <c>RunEngine.BuildMatch</c> —el
/// mismo que se va a jugar—, los perks letales los lista <c>Sim.Perks.Scouting</c> y los riesgos,
/// <c>RunEngine.LethalRisks</c>.</para>
/// <para>AW-O: el botón que empieza el partido (<see cref="BuildConfirmBar"/>) no vive con "Volver" y
/// "Alinear" arriba del todo —eso es navegación, se puede pulsar sin haber leído nada—, sino pegado al
/// bloque de riesgo y avisos que se acaba de enseñar, con separador y color de acento propios, para que
/// pulsarlo se sienta como confirmar el informe, no como un trámite previo a él.</para>
/// </summary>
public partial class ScoutScreen : Control
{
    private readonly List<PlayerCard> _cards = new();

    private RunController _run = null!;
    private int _nodeId = -1;
    private int _expanded = -1;
    private TeamState _rival = null!;
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun || run.SelectedNodeId < 0)
        {
            Nav.Route(this);
            return;
        }

        _run = run;
        _nodeId = run.SelectedNodeId;

        var state = run.State!;
        var catalog = run.Catalog!;
        var node = state.GetNode(_nodeId);

        // Solo bajo --tour-rivalry (Tour.Rivalry, capturas): siembra un reencuentro y un knaveo de prueba
        // sin tocar RunController.State -state es una copia local, RunState es inmutable-, para que el
        // cartel de reencuentro quede regresionado igual que la carrera (EnsureTestCareer).
        if (Tour.Rivalry)
        {
            state = EnsureTestRivalry(state, node);
        }

        var (setup, _, _) = RunEngine.BuildMatch(state, _nodeId, catalog, run.Engine);

        Layout.CenterLegacy(this);
        Widgets.Background(this);
        Widgets.Header(this, UiText.Get("ui.scout.title"), UiText.Get(
            "ui.scout.subtitle",
            UiText.Get("ui.kind." + node.Kind),
            node.Difficulty,
            UiText.Get("ui.difficulty." + (node.Difficulty <= 0 ? 5 : node.Difficulty))));

        var rivalTeam = _run.Systems?.Rivals.Find(node.OpponentId);
        BuildRivalList(state, node, catalog, setup.Away, rivalTeam);
        BuildReport(state, catalog, node, setup);
        BuildButtons();

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));

        if (Tour.Rivalry)
        {
            // Captura y sale sin seguir a Equipo: --tour-rivalry existe solo para esta una captura, y
            // continuar reescribiría equipo-run.png con la misma copia local ya usada más arriba (BE-B: no
            // tocar ficheros que no pide este encargo).
            Tour.Step(this, "ojeo-reencuentro", null);
        }
        else if (Tour.Active)
        {
            Tour.Step(this, "ojeo", () => Nav.Go(this, Nav.Team));
        }
    }

    /// <summary>
    /// La plantilla rival, con la misma ficha que la propia y el mismo patrón de inspección (UI-001,
    /// UI-010). Encabezada por su <b>nombre</b> y su línea de ojeo (RF-015): antes de esto, la sección se
    /// llamaba genéricamente "plantilla rival" y no había ni rastro del nombre del equipo, ni siquiera el
    /// id de datos ("act1_elf_swiftwing") que sí se ve en el marcador del partido.
    /// <para>
    /// F1 §6 (ADR 0124, memoria del rival): si ya se ha jugado contra este clan, una línea dice cuántas
    /// veces y cómo salió la última (<see cref="RivalHistory"/>) y, si hay un hecho que contar, el par
    /// knaveador-víctima más destacado (<see cref="RivalCredits"/>). <b>Nunca en el nodo de jefe</b>
    /// (<paramref name="node"/>.Kind == <see cref="NodeKind.Boss"/>): guarda un <c>opponentId</c> fantasma
    /// que no representa un enfrentamiento real contra ese clan -mismo motivo por el que
    /// <see cref="RivalHistory.Encounters"/> lo excluye por dentro.
    /// </para>
    /// </summary>
    private void BuildRivalList(RunState state, MapNode node, Sim.Data.Catalog catalog, TeamSetup away, RivalTeam? rivalTeam)
    {
        Widgets.Panel(this, new Rect2(12f, 52f, Widgets.CardColumnWidth, 690f));
        Widgets.Section(this, UiText.Get("ui.scout.rival"), new Vector2(24f, 60f), 340f);

        float listTop = 82f;
        if (rivalTeam is not null)
        {
            Widgets.Body(this, rivalTeam.Name.Es, new Vector2(24f, 80f), 340f, Style.Accent);
            var descriptionLabel = Widgets.Body(this, rivalTeam.Description.Es, new Vector2(24f, 98f), 340f, Style.TextDim);
            listTop = 98f + descriptionLabel.Size.Y + 10f;

            if (node.Kind != NodeKind.Boss)
            {
                listTop = BuildNemesisLines(state, node, listTop);
                listTop = BuildRivalryLines(state, node.OpponentId, rivalTeam, listTop);
            }
        }

        _rival = TeamState.Of(catalog, away);
        _list = new VBoxContainer
        {
            Position = new Vector2(22f, listTop),
            Size = new Vector2(356f, 742f - listTop),
        };
        _list.AddThemeConstantOverride("separation", 3);
        AddChild(_list);

        var scene = GD.Load<PackedScene>("res://Scenes/PlayerCard.tscn");
        var players = new List<PlayerDefinition>(away.Players);
        players.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        var nemeses = NemesisLinesOf(state, node);
        foreach (var player in players)
        {
            var card = scene.Instantiate<PlayerCard>();
            foreach (var nemesis in nemeses)
            {
                // Por puesto, no por nombre (revisión de la ADR 0165): un fichaje puede llamarse como un némesis.
                if (player.Id == Underleague.Sim.Run.Systems.Rivals.RivalTeamBuilder.OpponentFirstPlayerId + nemesis.Slot)
                {
                    card.NemesisTitle = nemesis.Title;
                }
            }

            _list.AddChild(card);
            card.Bind(_rival, player, System.Array.Empty<string>());
            card.Activated += OnCardActivated;
            _cards.Add(card);
        }
    }

    private System.Collections.Generic.IReadOnlyList<Underleague.Sim.Run.View.NemesisLine> NemesisLinesOf(RunState state, MapNode node) =>
        _run.Systems is null
            ? System.Array.Empty<Underleague.Sim.Run.View.NemesisLine>()
            : Underleague.Sim.Run.View.NemesisView.ForNode(state, _run.Systems.Nemesis, node, GameData.Language);

    /// <summary>
    /// ADR 0165: los némesis que juegan en este clan, con su título y a quién mataron y en qué acto, en lacre.
    /// Antes del reencuentro: es lo primero que el jugador tiene que saber de este rival. Sin némesis, el bloque
    /// desaparece. Devuelve la <c>y</c> siguiente.
    /// </summary>
    private float BuildNemesisLines(RunState state, MapNode node, float y)
    {
        var lines = NemesisLinesOf(state, node);
        foreach (var line in lines)
        {
            string key = line.Kills > 1 ? "ui.scout.nemesisMany" : "ui.scout.nemesis";
            var label = Widgets.Body(
                this,
                UiText.Get(key, line.Name, line.Title, line.VictimName, line.Act, line.Kills - 1),
                new Vector2(24f, y),
                340f,
                Pregon.Wax);
            y += label.Size.Y + 4f;
        }

        return lines.Count > 0 ? y + 6f : y;
    }

    /// <summary>
    /// F1 §6: reencuentro y, si sale barato, el par knaveador-víctima más destacado. La primera vez que se
    /// ve a un clan no hay nada nuevo que decir (<see cref="RivalHistory.HasFaced"/> falso), así que el
    /// bloque entero desaparece -no se enseña una línea vacía. Devuelve la <c>y</c> siguiente.
    /// </summary>
    private float BuildRivalryLines(RunState state, string opponentId, RivalTeam rivalTeam, float y)
    {
        // ADR 0165: el reencuentro es con el clan, en cualquier acto, no con el fichero del acto.
        var encounters = _run.Systems is { } systems
            ? RivalHistory.AgainstClan(state, systems.Rivals, rivalTeam.ClanId)
            : RivalHistory.Against(state, opponentId);
        if (encounters.Count == 0)
        {
            return y;
        }

        var last = encounters[^1];
        string result = UiText.Get(last.Result == NodeResult.Won ? "ui.scout.rivalRepeatWon" : "ui.scout.rivalRepeatLost");
        var repeatLabel = Widgets.Body(
            this,
            UiText.Get("ui.scout.rivalRepeat", rivalTeam.Name.Es, encounters.Count + 1, result),
            new Vector2(24f, y),
            340f,
            Style.Accent);
        y += repeatLabel.Size.Y + 4f;

        // Sin cronología real (RivalCredits.MostNotable no es "lo último que pasó", ver su comentario): el
        // hecho más severo/repetido contra este clan, si lo hay y se puede resolver a nombres sin cruzar
        // una frontera fea (el índice ya viene validado por MatchResolution, pero el catálogo de rivales
        // puede haber cambiado entre versiones de datos, así que se comprueba el rango igualmente).
        if (RivalCredits.MostNotable(state, opponentId) is { } credit
            && credit.RivalIndex >= 0 && credit.RivalIndex < rivalTeam.Players.Count
            && state.FindPlayer(credit.OwnPlayerId) is { } ownPlayer)
        {
            string rivalPlayerName = rivalTeam.Players[credit.RivalIndex].Name;
            string creditKey = credit.Kind switch
            {
                RivalCreditKind.CausedInjury => "ui.scout.rivalCreditCausedInjury",
                RivalCreditKind.CausedDeath => "ui.scout.rivalCreditCausedDeath",
                RivalCreditKind.SufferedInjury => "ui.scout.rivalCreditSufferedInjury",
                _ => "ui.scout.rivalCreditSufferedDeath",
            };
            var creditLabel = Widgets.Body(
                this,
                UiText.Get(creditKey, ownPlayer.Name, rivalPlayerName),
                new Vector2(24f, y),
                340f,
                Style.TextDim);
            y += creditLabel.Size.Y + 4f;
        }

        return y + 6f;
    }

    /// <summary>
    /// <b>Solo para la captura de verificación</b>, y solo bajo <c>--tour-rivalry</c>
    /// (<see cref="Tour.Rivalry"/>): mismo apaño que <c>TeamScreen.EnsureTestCareer</c> pero para el
    /// cartel de reencuentro. Un <c>--tour</c> normal nunca puede enseñar la línea de reencuentro ni la de
    /// knaveo: el mapa recién generado no ha jugado nada todavía. No toca <c>RunController.State</c> -el
    /// guardado real de la run-: <see cref="RunState"/> es inmutable, así que esto solo construye una copia
    /// local a partir del estado que <see cref="_Ready"/> ya iba a leer, con un encuentro previo contra
    /// este mismo nodo (<see cref="RunState.WithNodeCompleted"/>) y un hecho de
    /// <see cref="RunState.RivalCreditPrefix"/> real, en el formato exacto que documenta esa constante -el
    /// mismo que escribe <c>MatchResolution</c> (BE-B)-. No hace nada si el nodo no tiene rival de catálogo
    /// o es el jefe: mismas guardas que la producción (<see cref="MapNode.OpponentId"/>,
    /// <see cref="NodeKind.Boss"/>).
    /// </summary>
    private static RunState EnsureTestRivalry(RunState state, MapNode node)
    {
        if (node.OpponentId.Length == 0 || node.Kind == NodeKind.Boss)
        {
            return state;
        }

        var withEncounter = state.WithNodeCompleted(node.Id, node.Kind, NodeResult.Won);
        string key = RunState.RivalCreditPrefix + node.OpponentId + ":0:"
            + withEncounter.Roster[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":causedInjury";
        return withEncounter.WithCounter(key, 3);
    }

    /// <summary>Activar expande una ficha y solo una (UI-012), igual que en Equipo.</summary>
    private void OnCardActivated(int playerId)
    {
        _expanded = _expanded == playerId ? -1 : playerId;
        foreach (var card in _cards)
        {
            bool current = card.PlayerId == _expanded;
            card.Expanded = current;
            card.Selected = current;
        }
    }

    private void BuildReport(RunState state, Sim.Data.Catalog catalog, MapNode node, MatchSetup setup)
    {
        Widgets.Panel(this, new Rect2(396f, 52f, 872f, 690f));

        float y = 62f;
        var profile = Scouting.Profile(setup.Away, catalog);

        y = Block(UiText.Get("ui.scout.build"), new List<string>
        {
            UiText.Get(
                "ui.scout.buildLine",
                catalog.Race(profile.Race).Name.Es,
                profile.AverageLevel,
                Top(catalog, profile)),
        }, y);

        y = Block(UiText.Get("ui.scout.referee"), RefereeLines(node), y);

        // RF-013: los perks letales, destacados. Si no hay ninguno, se dice: la ausencia de amenaza es
        // información igual de accionable que la amenaza.
        var threats = Scouting.LethalPerks(setup.Away, catalog);
        var lethalLines = new List<string>();
        var templates = catalog.Localization.Get(GameData.Language);
        foreach (var threat in threats)
        {
            var perk = catalog.Perks.Get(threat.PerkId);
            lethalLines.Add(UiText.Get(
                "ui.scout.lethalLine",
                threat.PlayerName,
                threat.PerkName.Es,
                DescriptionGenerator.Describe(perk, templates)));
        }

        if (lethalLines.Count == 0)
        {
            lethalLines.Add(UiText.Get("ui.scout.lethalNone"));
        }

        y = Block(UiText.Get("ui.scout.lethal"), lethalLines, y, threats.Count > 0 ? Style.Hole : Style.Text);

        // El número de RF-012c, por titular. Es el mismo que el motor tira, no una estimación.
        var risks = RunEngine.LethalRisks(state, node.Id, catalog, _run.Engine);
        var riskLines = new List<string>();
        foreach (var risk in risks)
        {
            if (risk.Risk <= 0)
            {
                continue;
            }

            var player = state.FindPlayer(risk.PlayerId);
            riskLines.Add(UiText.Get("ui.scout.riskLine", player?.Name ?? "?", Percent(risk.Risk)));
        }

        if (riskLines.Count == 0)
        {
            riskLines.Add(UiText.Get("ui.scout.riskNone"));
        }

        y = Block(UiText.Get("ui.scout.risk"), riskLines, y, riskLines.Count > 0 && risks.Count > 0 ? Style.Text : Style.TextDim);

        // Lo que hay que advertir antes de confirmar (RF-012d, RF-002d, RF-093), y el once con el que se va
        // a jugar de verdad (ADR 0134): los dos salen de RunLineup.Effective, no de la alineación guardada.
        var effective = RunLineup.Effective(state);
        var warnings = RunEngine.LineupWarnings(state, node.Id, catalog, _run.Engine);
        var warningLines = new List<string>();
        foreach (var warning in warnings)
        {
            var player = state.FindPlayer(warning.PlayerId);
            warningLines.Add(warning.Kind switch
            {
                // ADR 0134: ahora significa inferioridad REAL —ni con el banquillo se llega a siete—, así
                // que el número sale del once efectivo y no está fijo como antes.
                LineupWarningKind.Shorthanded => UiText.Get(
                    "ui.scout.warnShorthanded",
                    effective.Lineup.Slots.Count.ToString(CultureInfo.InvariantCulture)),
                LineupWarningKind.FilledFromBench => UiText.Get("ui.scout.warnFilled", player?.Name ?? "?"),
                LineupWarningKind.SevereInjuryDeathRisk => UiText.Get("ui.scout.warnSevere", player?.Name ?? "?"),
                _ => UiText.Get("ui.scout.warnLethal", player?.Name ?? "?", Percent(warning.Risk)),
            });
        }

        if (warningLines.Count > 0)
        {
            y = Block(UiText.Get("ui.scout.warnings"), warningLines, y, Style.Accent);
        }

        // ADR 0157: el corredor ofrece una apuesta por nodo de partido. Va justo antes de confirmar: es una
        // decisión previa al partido, igual que alinear.
        y = BuildBookie(state, node, y);

        // AW-O: el botón de empezar va pegado a lo que se acaba de leer, no arriba del todo con "Volver"
        // y "Alinear" (BuildButtons) — para que confirmar se sienta como confirmar el riesgo y los
        // avisos, no como un paso de trámite anterior a ellos.
        y = BuildConfirmBar(y);

        // El once con el que se juega, que es lo que el jugador cambia si el número no le gusta. Hasta la
        // ADR 0134 esta lista enseñaba la alineación GUARDADA, así que decía una cosa y saltaba al campo
        // otra; ahora es el once efectivo y el que entra de oficio va marcado, porque de eso trataba BC-H.
        var starters = new List<string>();
        foreach (var slot in effective.Lineup.Slots)
        {
            var player = state.FindPlayer(slot.PlayerId);
            if (player is not null)
            {
                bool filled = effective.FilledIds.Contains(player.Id);
                starters.Add($"{player.Name} · {UiText.Get("ui.pos." + player.Position)} · "
                    + $"{UiText.Get("ui.state." + player.PhysicalState)} · ({slot.HomeCell.Column},{slot.HomeCell.Row})"
                    + (filled ? " · " + UiText.Get("ui.scout.startersFilled") : string.Empty));
            }
        }

        Block(UiText.Get("ui.scout.starters"), starters, y);
    }

    /// <summary>
    /// «EL CORREDOR» (ADR 0157, RF-114h): la apuesta que se ofrece en este nodo, con su condición, lo que
    /// cuesta y lo que paga, y los botones de tomarla o retirarla. La oferta se deriva de (semilla, nodo)
    /// (<see cref="BetSystem.OfferFor(RunState, MapNode, IRunSystems, Sim.Data.Catalog)"/>): la pantalla no la
    /// calcula ni la guarda. La frecuencia medida se enseña en palabras, no en porcentajes internos: lo que el
    /// jugador necesita es saber si es una apuesta rara o corriente. Devuelve la <c>y</c> siguiente.
    /// </summary>
    private float BuildBookie(RunState state, MapNode node, float y)
    {
        Widgets.Section(this, UiText.Get("ui.scout.bet"), new Vector2(412f, y), 830f);
        y += 18f;

        var offer = BetSystem.OfferFor(state, node, _run.Engine, _run.Catalog!);
        var definition = offer is null ? null : _run.Systems!.Bets.Find(offer.BetId);
        if (offer is null || definition is null)
        {
            var none = Widgets.Body(this, UiText.Get("ui.scout.betNone"), new Vector2(412f, y), 830f, Style.TextDim);
            return y + none.Size.Y + 14f;
        }

        var name = Widgets.Body(this, UiText.Name(definition.Name), new Vector2(412f, y), 830f, Style.Accent);
        y += name.Size.Y + 2f;

        string condition = UiText.Name(definition.Condition).Replace("{player}", offer.TargetPlayerName, System.StringComparison.Ordinal);
        var conditionLabel = Widgets.Body(this, condition, new Vector2(412f, y), 830f);
        y += conditionLabel.Size.Y + 2f;

        var price = Widgets.Body(
            this,
            UiText.Get("ui.scout.betPrice", offer.Stake, offer.Payout) + " " + UiText.Get(FrequencyKey(definition.FrequencyBasisPointsFor(node.Difficulty))),
            new Vector2(412f, y),
            830f,
            Style.TextDim);
        y += price.Size.Y + 6f;

        bool taken = state.Bet is { } bet && bet.NodeId == node.Id;
        if (taken)
        {
            var mine = Widgets.Body(this, UiText.Get("ui.scout.betTaken", state.Bet!.Stake, state.Bet.Payout), new Vector2(412f, y), 830f, Style.LinkCreated);
            y += mine.Size.Y + 4f;
            var withdraw = Widgets.Button(this, UiText.Get("ui.scout.betWithdraw"), new Rect2(412f, y, 220f, 28f));
            withdraw.Pressed += OnWithdrawBet;
        }
        else
        {
            // Sin oro no se puede: el botón se apaga y se dice cuánto falta, en vez de dejar que el clic falle.
            bool affordable = state.Gold >= offer.Stake;
            var take = Widgets.Button(this, UiText.Get("ui.scout.betTake"), new Rect2(412f, y, 220f, 28f), enabled: affordable);
            take.Pressed += OnTakeBet;
            if (!affordable)
            {
                Widgets.Body(this, UiText.Get("ui.scout.betNoGold", offer.Stake, state.Gold), new Vector2(644f, y + 4f), 598f, Style.Hole);
            }
        }

        return y + 28f + 14f;
    }

    /// <summary>Frecuencia medida (centésimas de punto) en palabras: rara, de vez en cuando, a menudo, casi la mitad.</summary>
    private static string FrequencyKey(int basisPoints) => basisPoints switch
    {
        < 500 => "ui.scout.betFreq.rare",
        < 1200 => "ui.scout.betFreq.sometimes",
        < 2500 => "ui.scout.betFreq.often",
        _ => "ui.scout.betFreq.veryOften",
    };

    private void OnTakeBet()
    {
        _run.Apply(new TakeBet(_nodeId));
        Nav.Go(this, Nav.Scout);
    }

    private void OnWithdrawBet()
    {
        _run.Apply(new DeclineBet());
        Nav.Go(this, Nav.Scout);
    }

    /// <summary>
    /// AW-O: separador fino y botón de confirmación en acento, pegados al bloque de riesgo y avisos que
    /// acaban de leerse. Antes el botón vivía en la fila de navegación de arriba (<see cref="BuildButtons"/>),
    /// junto a "Volver" y "Alinear", con el mismo peso visual que ellos y antes de leer una sola línea del
    /// informe: eso lo hacía sentir un paso de trámite, no la confirmación explícita que pide el revisor.
    /// </summary>
    private float BuildConfirmBar(float y)
    {
        // Una raya, no una tarjeta: parchment:false, si no ParchmentPanel intentaría rasgar un borde
        // sobre un rectángulo de 1 px de alto.
        Widgets.Panel(this, new Rect2(412f, y, 830f, 1f), Style.Line, parchment: false);
        y += 14f;

        var start = Widgets.Button(this, UiText.Get("ui.scout.start"), new Rect2(412f, y, 220f, 32f));

        // Sin tinte dorado: Style.Accent es un dorado pensado para leerse sobre madera o como una cifra
        // suelta, no como texto de botón sobre la placa de pergamino (BuildLegacyTheme ya le da tinta
        // oscura legible); el borde de foco en dorado del Theme ya distingue este botón cuando toca.
        start.Pressed += StartMatch;

        return y + 32f + 16f;
    }

    /// <summary>
    /// La ficha del árbitro (ADR 0158 §6, RF-061, RF-012b): nombre, rasgo, la línea que explica lo que
    /// hace su rasgo -compuesta desde plantilla, RT-035-, su muletilla y si se acuerda de ti. Retira el
    /// hueco declarado <c>ui.scout.refereeGap</c>: los rasgos y la memoria ya existen.
    /// </summary>
    private List<string> RefereeLines(MapNode node)
    {
        var referee = _run.Referee(node);
        var lines = new List<string>();
        if (referee is null)
        {
            return lines;
        }

        lines.Add(UiText.Get("ui.scout.refereeLine", referee.Name, UiText.Get("ui.refereeTrait." + referee.Trait)));
        if (referee.TraitLine.Length > 0)
        {
            lines.Add(referee.TraitLine);
        }

        if (referee.Catchphrase.Length > 0)
        {
            lines.Add(UiText.Get("ui.scout.refereeCatchphrase", referee.Catchphrase));
        }

        // Memoria (ADR 0158 §4): 0 y 0 a la vez significa que nunca ha pitado un partido de esta run -no
        // hay diferencia entre "se acuerda con criterio 0" y "no te conoce", así que se enseña lo segundo.
        // La memoria es sólo lo que el jugador ha hecho delante de él (ADR 0158 §4); el sesgo de casero
        // es innato y se dice aparte, para no presentar como rencor lo que no se ha ganado.
        lines.Add(referee.Grudge == 0
            ? UiText.Get("ui.scout.refereeMemoryNone")
            : UiText.Get("ui.scout.refereeMemoryKnown", UiText.Signed(referee.Grudge)));
        if (referee.InitialBias != 0)
        {
            lines.Add(UiText.Get("ui.scout.refereeStart", UiText.Signed(referee.InitialBias)));
        }

        if (referee.BlindSide != RefereeSide.None)
        {
            lines.Add(UiText.Get(referee.BlindSide == RefereeSide.Top
                ? "ui.scout.refereeBlindTop"
                : "ui.scout.refereeBlindBottom"));
        }

        return lines;
    }

    /// <summary>Las etiquetas que más se repiten: es lo que hace reconocible a un rival (RF-015).</summary>
    private static string Top(Sim.Data.Catalog catalog, TeamProfile profile)
    {
        var parts = new List<string>();
        for (int i = 0; i < profile.Styles.Count && i < 2; i++)
        {
            parts.Add(catalog.Style(profile.Styles[i].Style).Name.Es + " x" + profile.Styles[i].Count);
        }

        for (int i = 0; i < profile.Traits.Count && i < 2; i++)
        {
            parts.Add(catalog.Trait(profile.Traits[i].Trait).Name.Es + " x" + profile.Traits[i].Count);
        }

        return string.Join(", ", parts);
    }

    /// <summary>Probabilidad en base 10.000 escrita como porcentaje con un decimal (RF-012c).</summary>
    private static string Percent(int risk) => UiText.Get("ui.risk.percent", risk / 100, (risk % 100) / 10);

    /// <summary>Un bloque de informe: título en acento y sus líneas debajo. Devuelve la y siguiente.</summary>
    private float Block(string title, IReadOnlyList<string> lines, float y, Color? color = null)
    {
        Widgets.Section(this, title, new Vector2(412f, y), 830f);
        y += 18f;
        foreach (string line in lines)
        {
            var label = Widgets.Body(this, line, new Vector2(412f, y), 830f, color);
            y += label.Size.Y + 2f;
        }

        return y + 12f;
    }

    /// <summary>
    /// Navegación, no confirmación: "Volver" y "Alinear" se pueden pulsar sin haber leído nada. El botón
    /// que empieza el partido vive aparte, pegado al informe (<see cref="BuildConfirmBar"/>, AW-O).
    /// </summary>
    private void BuildButtons()
    {
        var lineup = Widgets.Button(this, UiText.Get("ui.scout.lineup"), new Rect2(1002f, 58f, 120f, 28f));
        lineup.Pressed += () => Nav.Go(this, Nav.Team);

        var back = Widgets.Button(this, UiText.Get("ui.nav.back"), new Rect2(886f, 58f, 100f, 28f));
        back.Pressed += () =>
        {
            _run.SelectedNodeId = -1;
            Nav.Go(this, Nav.Map);
        };
    }

    /// <summary>
    /// Empezar. La pantalla de partido es la que entra en el nodo: entrar lo resuelve entero y es ella
    /// quien tiene que enseñar lo que pasó (RF-119).
    /// </summary>
    private void StartMatch()
    {
        _run.SelectedNodeId = _nodeId;
        Nav.Go(this, Nav.MatchView);
    }
}
