using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Game.Screens;

/// <summary>
/// Los <b>nodos simples</b> del mapa: clínica (RF-094), inscripción (ADR 0046), entrenamiento y evento.
/// Los cuatro se parecen tanto que compartir pantalla es lo honesto: cada uno dice <b>qué cuesta</b>,
/// <b>qué hace</b> y pide confirmación, y ninguno esconde el efecto detrás de una pulsación.
/// <para>
/// Hay dos formas de nodo y las dos pasan por aquí. La clínica y la inscripción se <b>abren</b> y esperan
/// decisiones (<c>TreatPlayer</c>, <c>ExpandRoster</c>) hasta que el jugador sale con <c>LeaveNode</c>. El
/// entrenamiento y el evento se resuelven solos al entrar, así que esta pantalla los enseña <b>antes</b>
/// de entrar —para que el jugador vea lo que va a pasar— y enseña después lo que ha pasado.
/// </para>
/// <para>Los costes y los efectos salen de <c>data/economy</c> a través de
/// <c>RunController.Systems.Economy</c>; la pantalla no conoce ni un número del juego (RT-014).</para>
/// </summary>
public partial class NodeScreen : Control
{
    private RunController _run = null!;
    private MapNode _node = null!;
    private bool _entered;
    private string _message = string.Empty;
    private RunState? _before;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Go(this, Nav.Start);
            return;
        }

        _run = run;
        var state = run.State!;

        if (state.Phase == RunPhase.NodeOpen && state.PendingNodeId >= 0)
        {
            _node = state.GetNode(state.PendingNodeId);
            _entered = true;
        }
        else if (run.SelectedNodeId >= 0)
        {
            _node = state.GetNode(run.SelectedNodeId);
            _entered = false;
        }
        else
        {
            Nav.Route(this);
            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var state = _run.State!;
        var economy = _run.Systems!.Economy;

        Widgets.Background(this);
        Widgets.Header(this, Title(), UiText.Get("ui.node.gold", state.Gold));
        Widgets.Panel(this, new Rect2(12f, 52f, 1256f, 690f));

        float y = 72f;
        Widgets.Body(this, Description(economy, state), new Vector2(28f, y), 1220f, Style.TextDim);
        y += 40f;

        y = _node.Kind switch
        {
            NodeKind.Clinic => BuildClinic(state, economy, y),
            NodeKind.Enrollment => BuildEnrollment(state, economy, y),
            NodeKind.Training => BuildSelfResolving(UiText.Get("ui.node.train"), y),
            NodeKind.Event => BuildSelfResolving(UiText.Get("ui.node.eventGo"), y),
            _ => y,
        };

        if (_message.Length > 0)
        {
            Widgets.Body(this, _message, new Vector2(28f, y + 12f), 1220f, Style.Accent);
        }

        var leave = Widgets.Button(this, UiText.Get("ui.node.leave"), new Rect2(28f, 700f, 160f, 28f), Leaveable());
        leave.Pressed += Leave;

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));
    }

    private string Title() => _node.Kind switch
    {
        NodeKind.Clinic => UiText.Get("ui.node.clinicTitle"),
        NodeKind.Enrollment => UiText.Get("ui.node.enrollTitle"),
        NodeKind.Training => UiText.Get("ui.node.trainTitle"),
        _ => UiText.Get("ui.node.eventTitle"),
    };

    private string Description(Sim.Run.Systems.Economy.EconomyConfig economy, RunState state) => _node.Kind switch
    {
        NodeKind.Clinic => UiText.Get("ui.node.clinicBody", economy.ClinicCost),
        NodeKind.Enrollment => UiText.Get(
            "ui.node.enrollBody",
            Math.Max(0, economy.EnrollmentCost(state.Counter(RunState.EnrollmentSlotsCounter))),
            RunRules.MaxRosterSize),
        NodeKind.Training => UiText.Get("ui.node.trainBody", economy.TrainingExperience, RunRules.YouthExperienceBonusPercent),
        _ => UiText.Get("ui.node.eventBody", economy.EventGoldMin, economy.EventGoldMax),
    };

    /// <summary>Clínica: un botón por lesionado grave, con su coste delante (RF-094).</summary>
    private float BuildClinic(RunState state, Sim.Run.Systems.Economy.EconomyConfig economy, float y)
    {
        var patients = new List<RunPlayer>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState == PhysicalState.SevereInjury)
            {
                patients.Add(state.Roster[i]);
            }
        }

        if (patients.Count == 0)
        {
            Widgets.Body(this, UiText.Get("ui.node.clinicNone"), new Vector2(28f, y), 1220f);
            return y + 24f;
        }

        bool affordable = state.Gold >= economy.ClinicCost;
        if (!affordable)
        {
            Widgets.Body(this, UiText.Get("ui.node.clinicPoor", state.Gold, economy.ClinicCost), new Vector2(28f, y), 1220f, Style.Hole);
            y += 22f;
        }

        foreach (var patient in patients)
        {
            var button = Widgets.Button(
                this,
                UiText.Get("ui.node.treat", patient.Name, economy.ClinicCost),
                new Rect2(28f, y, 360f, 28f),
                affordable);
            int id = patient.Id;
            string name = patient.Name;
            button.Pressed += () => Decide(new TreatPlayer(id), UiText.Get("ui.node.treated", name));
            y += 34f;
        }

        return y;
    }

    /// <summary>Inscripción: el hueco de plantilla y su coste creciente (ADR 0046).</summary>
    private float BuildEnrollment(RunState state, Sim.Run.Systems.Economy.EconomyConfig economy, float y)
    {
        Widgets.Body(
            this,
            UiText.Get("ui.node.enrollState", state.RosterSize, state.RosterCapacity, state.EnrollmentSlotsLeft),
            new Vector2(28f, y),
            1220f);
        y += 26f;

        int cost = economy.EnrollmentCost(state.Counter(RunState.EnrollmentSlotsCounter));
        if (cost < 0)
        {
            Widgets.Body(this, UiText.Get("ui.node.enrollFull"), new Vector2(28f, y), 1220f, Style.TextDim);
            return y + 24f;
        }

        var button = Widgets.Button(
            this,
            UiText.Get("ui.node.enrollBuy", cost),
            new Rect2(28f, y, 360f, 28f),
            state.Gold >= cost);
        button.Pressed += () => Decide(new ExpandRoster(), UiText.Get("ui.node.enrolled", state.RosterCapacity + 1));
        return y + 34f;
    }

    /// <summary>Entrenamiento y evento: un botón que entra en el nodo, y después el resultado.</summary>
    private float BuildSelfResolving(string text, float y)
    {
        if (_entered)
        {
            return y;
        }

        var button = Widgets.Button(this, text, new Rect2(28f, y, 240f, 28f));
        button.Pressed += Resolve;
        return y + 34f;
    }

    /// <summary>
    /// Entra en el nodo y cuenta lo que ha pasado. El "antes" se guarda para poder decirlo: el estado es
    /// inmutable, así que basta con quedarse con la referencia anterior.
    /// </summary>
    private void Resolve()
    {
        _before = _run.State;
        _run.Enter(_node.Id);
        _entered = true;

        var after = _run.State!;
        _message = _node.Kind == NodeKind.Training
            ? UiText.Get("ui.node.trained", after.AvailablePlayerCount, LevelUps(_before!, after))
            : UiText.Get("ui.node.evented", after.Gold - _before!.Gold);

        Rebuild();
    }

    private static int LevelUps(RunState before, RunState after)
    {
        int count = 0;
        for (int i = 0; i < after.Roster.Count; i++)
        {
            var player = after.Roster[i];
            var previous = before.FindPlayer(player.Id);
            if (previous is not null && player.Level > previous.Level)
            {
                count++;
            }
        }

        return count;
    }

    private void Decide(RunDecision decision, string message)
    {
        try
        {
            _run.Apply(decision);
            _message = message;
        }
        catch (Exception error)
        {
            _message = UiText.Get("ui.node.error", error.Message);
        }

        Rebuild();
    }

    /// <summary>Se puede salir siempre que el nodo ya se haya resuelto o abierto: nunca se queda atrapado.</summary>
    private bool Leaveable() => _entered || _run.State!.Phase == RunPhase.NodeOpen;

    private void Leave()
    {
        var state = _run.State!;
        if (state.Phase == RunPhase.NodeOpen && state.PendingNodeId >= 0)
        {
            _run.Apply(new LeaveNode());
        }

        _run.SelectedNodeId = -1;
        Nav.Route(this);
    }
}
