using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Run;

namespace Underleague.Game.Screens;

/// <summary>
/// La pantalla que aparece cuando la que tocaba <b>todavía no está escrita</b>: partido, informe,
/// recompensa y mercado se escriben en paralelo a este esqueleto de navegación.
/// <para>
/// No es un cartel de "vuelve luego": una escena que falta no puede dejar la run bloqueada, así que hace
/// lo mínimo para que se pueda seguir jugando. Si lo que falta es la <b>pantalla de partido</b>, juega el
/// partido —entrar en el nodo lo resuelve entero— y enseña el marcador; si lo que falta es un nodo con
/// decisiones, deja salir de él. Cuando el fichero de verdad aparezca, la navegación lo encontrará sola y
/// esta pantalla dejará de verse.
/// </para>
/// </summary>
public partial class PlaceholderScreen : Control
{
    private RunController _run = null!;
    private string _missing = string.Empty;
    private string _result = string.Empty;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Go(this, Nav.Start);
            return;
        }

        _run = run;
        _missing = Nav.Missing;
        Build();
    }

    private void Build()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        Widgets.Background(this);
        Widgets.Header(this, UiText.Get("ui.nav.pendingTitle"), _missing);
        Widgets.Panel(this, new Rect2(12f, 52f, 1256f, 690f));

        float y = 76f;
        Widgets.Body(this, UiText.Get("ui.nav.pendingBody", _missing), new Vector2(28f, y), 1220f);
        y += 40f;

        bool match = _missing == Nav.Match && _run.SelectedNodeId >= 0;
        if (match)
        {
            Widgets.Body(this, UiText.Get("ui.nav.pendingMatch"), new Vector2(28f, y), 1220f, Style.TextDim);
            y += 30f;
            var play = Widgets.Button(this, UiText.Get("ui.scout.start"), new Rect2(28f, y, 200f, 28f));
            play.Pressed += PlayMatch;
            y += 40f;
        }

        if (_result.Length > 0)
        {
            Widgets.Body(this, _result, new Vector2(28f, y), 1220f, Style.Accent);
            y += 40f;
        }

        var next = Widgets.Button(this, UiText.Get("ui.node.leave"), new Rect2(28f, 700f, 200f, 28f));
        next.Pressed += Continue;

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));

        // En el recorrido de capturas, esta pantalla se comporta como el jugador que sigue adelante: juega
        // el partido si es lo que falta y, si no, cierra el nodo. Así el recorrido llega hasta el mapa
        // otra vez y comprueba de paso que la vuelta completa —mapa, ojeo, partido, nodo abierto, mapa—
        // funciona de verdad.
        if (Tour.Active)
        {
            Tour.Step(this, match ? "partido-provisional" : "pendiente", match ? PlayMatch : Continue);
        }
    }

    /// <summary>Juega el partido de forma provisional: el resultado, sin log ni informe (eso es del otro paquete).</summary>
    private void PlayMatch()
    {
        int nodeId = _run.SelectedNodeId;
        _run.Enter(nodeId);

        var summary = _run.LastMatch!.Summary;
        _result = $"{summary.GoalsFor} - {summary.GoalsAgainst} · "
            + (summary.Won ? UiText.Get("ui.end.victory") : UiText.Get("ui.end.defeat")).ToLowerInvariant()
            + $" · {summary.OwnInjuries} lesiones · {summary.OwnDeaths} muertes";
        _missing = string.Empty;
        Build();
    }

    /// <summary>
    /// Seguir. Un nodo abierto que nadie puede resolver —el mercado o la recompensa mientras su pantalla
    /// no exista— se cierra con <c>LeaveNode</c>, que es renunciar a lo que ofrecía: provisional y
    /// declarado, no un atajo que decida por el jugador.
    /// </summary>
    private void Continue()
    {
        var state = _run.State!;
        if (!_run.Outcome().IsOver && state.Phase == RunPhase.NodeOpen && state.PendingNodeId >= 0)
        {
            _run.Apply(new LeaveNode());
        }

        Nav.Route(this);
    }
}
