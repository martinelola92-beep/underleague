using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Data;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>fin de run</b>: victoria o derrota <b>con su causa</b>, actos superados, plantilla
/// final y caídos.
/// <para>
/// La causa no es un adorno. Solo hay dos formas de perder (RF-002b) y las dos son consecuencia de
/// decisiones que el jugador tomó viendo los números: perder contra un jefe, o quedarse por debajo de
/// cinco disponibles. Decir cuál de las dos ha sido es lo que cierra el bucle de aprendizaje, igual que
/// el informe post-partido lo cierra dentro del partido (RF-119).
/// </para>
/// <para>Las cifras las da <c>Sim.Run.RunSummary</c>, que las deriva del historial de nodos; la pantalla
/// no cuenta nada (RT-014).</para>
/// </summary>
public partial class RunEndScreen : Control
{
    private readonly List<PlayerCard> _cards = new();

    private int _expanded = -1;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Go(this, Nav.Start);
            return;
        }

        var state = run.State!;
        var catalog = run.Catalog!;
        var outcome = run.Outcome();
        bool victory = outcome.Kind == RunOutcomeKind.Victory;

        Layout.CenterLegacy(this);
        Widgets.Background(this);
        Widgets.Header(
            this,
            UiText.Get(victory ? "ui.end.victory" : "ui.end.defeat"),
            UiText.Get("ui.end.seed", state.Seed, catalog.Race(state.ClubRace).Name.Es));

        // La portada: la run contada como un periódico de humor (ADR 0163, RF-122). La causa de la derrota
        // (RF-002b) y las cifras siguen dichas con todas las letras bajo la cabecera de la pantalla.
        Widgets.Body(this, Cause(outcome, victory), new Vector2(412f, 60f), 830f, victory ? Style.Accent : Style.Hole);
        Widgets.Body(
            this,
            UiText.Get(
                "ui.end.stats",
                RunSummary.ActsCleared(state),
                RunRules.Acts,
                RunSummary.NodesVisited(state),
                RunSummary.MatchesWon(state),
                RunSummary.MatchesPlayed(state),
                state.Gold),
            new Vector2(412f, 78f),
            830f,
            Style.TextDim);

        var systems = run.Systems;
        if (systems is not null && !systems.Gazette.IsEmpty)
        {
            var page = new GazettePage { Position = new Vector2(400f, 102f), Size = new Vector2(860f, 592f) };
            AddChild(page);
            page.Bind(GazetteView.Build(state, catalog, systems.Nicknames, systems.Rivals, systems.Gazette, GameData.Language, systems.Nemesis));
        }

        BuildRoster(run, state, catalog);

        var again = Widgets.Button(this, UiText.Get("ui.end.again"), new Rect2(412f, 700f, 200f, 30f));
        again.Pressed += () =>
        {
            run.Abandon();
            Nav.Go(this, Nav.Start);
        };

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));

        if (Tour.Active)
        {
            Tour.Step(this, "fin-de-run", null);
        }
    }

    private static string Cause(RunOutcome outcome, bool victory) => victory
        ? UiText.Get("ui.end.victoryBody")
        : outcome.Cause switch
        {
            DefeatCause.BossMatchLost => UiText.Get("ui.end.causeBoss"),
            DefeatCause.NotEnoughPlayers => UiText.Get("ui.end.causePlayers", RunRules.MinimumAvailablePlayers),
            _ => string.Empty,
        };

    /// <summary>La plantilla final con la que se acaba, muertos incluidos: es el retrato de la run.</summary>
    private void BuildRoster(RunController run, RunState state, Sim.Data.Catalog catalog)
    {
        Widgets.Panel(this, new Rect2(12f, 52f, Widgets.CardColumnWidth, 690f));
        Widgets.Section(this, UiText.Get("ui.end.roster"), new Vector2(24f, 60f), 340f);

        var list = new VBoxContainer
        {
            Position = new Vector2(22f, 82f),
            Size = new Vector2(356f, 650f),
        };
        list.AddThemeConstantOverride("separation", 3);
        AddChild(list);

        var team = TeamState.FromRun(run);
        var scene = GD.Load<PackedScene>("res://Scenes/PlayerCard.tscn");
        foreach (var player in team.Players)
        {
            var card = scene.Instantiate<PlayerCard>();
            list.AddChild(card);
            card.Bind(team, player, System.Array.Empty<string>());
            card.Activated += OnCardActivated;
            _cards.Add(card);
        }
    }

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
}
