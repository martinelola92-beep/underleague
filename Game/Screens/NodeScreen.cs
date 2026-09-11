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
    /// <summary>Opción de evento pendiente de señalar a quién (ADR 0100); −1 si no hay ninguna.</summary>
    private int _eventOption = -1;
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
            NodeKind.Training => BuildSelfResolving(UiText.Get("ui.node.train"), y),
            NodeKind.Event => BuildEvent(y),
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
        NodeKind.Training => UiText.Get("ui.node.trainTitle"),
        // ADR 0100: el título del evento es el de su carta.
        _ => _run.Event()?.Title ?? UiText.Get("ui.node.eventTitle"),
    };

    private string Description(Sim.Run.Systems.Economy.EconomyConfig economy, RunState state) => _node.Kind switch
    {
        NodeKind.Clinic => UiText.Get("ui.node.clinicBody", economy.ClinicCost),
        NodeKind.Training => UiText.Get("ui.node.trainBody", economy.TrainingExperience, RunRules.YouthExperienceBonusPercent),
        _ => UiText.Get("ui.node.eventBody"),
    };

    /// <summary>
    /// Clínica (RF-094, ADR 0099): tres servicios con su precio delante. La tarifa plana arriba —cura a
    /// todos y no mira cuántos son—, y por cada lesionado dos botones: el garantizado y el del matasanos,
    /// que cuesta una fracción y lleva sus dos porcentajes escritos. Verlos antes de elegir es lo que hace
    /// legítimo que el matasanos pueda matar (RF-012d, ADR 0048).
    /// </summary>
    private float BuildClinic(RunState state, Sim.Run.Systems.Economy.EconomyConfig economy, float y)
    {
        var patients = new List<RunPlayer>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (Sim.Run.Systems.Medical.MedicalSystem.NeedsTreatment(state.Roster[i]))
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

        // Tarifa plana: una sola vez, arriba, con lo que costaría uno a uno al lado para que la cuenta se vea.
        int piecemeal = 0;
        foreach (var patient in patients)
        {
            piecemeal += patient.PhysicalState == PhysicalState.SevereInjury ? economy.ClinicCost : economy.ClinicMinorCost;
        }

        var squad = Widgets.Button(
            this,
            UiText.Get("ui.node.treatSquad", economy.ClinicSquadCost, patients.Count, piecemeal),
            new Rect2(28f, y, 520f, 28f),
            state.Gold >= economy.ClinicSquadCost);
        squad.Pressed += () => Decide(new TreatSquad(), UiText.Get("ui.node.treatedSquad", patients.Count));
        y += 38f;

        foreach (var patient in patients)
        {
            int id = patient.Id;
            string name = patient.Name;
            int full = patient.PhysicalState == PhysicalState.SevereInjury ? economy.ClinicCost : economy.ClinicMinorCost;
            int risky = Sim.Run.Systems.Medical.MedicalSystem.RiskyCost(full, economy);

            var button = Widgets.Button(
                this,
                UiText.Get("ui.node.treat", name, full),
                new Rect2(28f, y, 360f, 28f),
                state.Gold >= full);
            button.Pressed += () => Decide(new TreatPlayer(id), UiText.Get("ui.node.treated", name));

            var quack = Widgets.Button(
                this,
                UiText.Get(
                    "ui.node.treatRisky",
                    risky,
                    economy.ClinicRiskyFailPercent + economy.ClinicRiskyWorsePercent,
                    economy.ClinicRiskyWorsePercent),
                new Rect2(396f, y, 420f, 28f),
                state.Gold >= risky);
            quack.Pressed += () => Decide(new TreatPlayer(id, Risky: true), UiText.Get("ui.node.treatedRisky", name));
            y += 34f;
        }

        return y;
    }

    /// <summary>
    /// Evento (ADR 0100): la carta con sus opciones, cada una con su línea de efecto compuesta y su coste
    /// delante. Las que piden un cuerpo abren la lista de disponibles: el jugador señala a quién, nunca el
    /// juego por él (RF-012d).
    /// </summary>
    private float BuildEvent(float y)
    {
        var view = _run.Event();
        if (view is null)
        {
            return y;
        }

        Widgets.Body(this, view.Description, new Vector2(28f, y), 1220f, Style.TextDim);
        y += 30f;

        foreach (var option in view.Options)
        {
            var button = Widgets.Button(
                this,
                UiText.Get("ui.node.eventOption", option.Name, option.Effect),
                new Rect2(28f, y, 760f, 28f),
                option.Affordable && (!option.NeedsTarget || option.Targets.Count > 0));
            int index = option.Index;
            bool needsTarget = option.NeedsTarget;
            string name = option.Name;
            button.Pressed += () =>
            {
                if (!needsTarget)
                {
                    Decide(new ChooseEventOption(index), UiText.Get("ui.node.eventChosen", name));
                    return;
                }

                _eventOption = index;
                _message = UiText.Get("ui.node.eventPickTarget");
                Rebuild();
            };
            y += 34f;
        }

        if (_eventOption < 0)
        {
            return y;
        }

        // El paso de señalar: la lista de disponibles, uno por botón.
        y += 6f;
        var targets = view.Options[_eventOption].Targets;
        foreach (var target in targets)
        {
            var pick = Widgets.Button(
                this,
                UiText.Get("ui.node.eventTarget", target.Name, target.Detail),
                new Rect2(60f, y, 520f, 26f));
            int option = _eventOption;
            int playerId = target.PlayerId;
            string who = target.Name;
            pick.Pressed += () =>
            {
                _eventOption = -1;
                Decide(new ChooseEventOption(option, playerId), UiText.Get("ui.node.eventChosenOn", who));
            };
            y += 30f;
        }

        return y;
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

        // Solo el entrenamiento se resuelve solo desde la ADR 0100; el evento pide elegir.
        var after = _run.State!;
        _message = UiText.Get("ui.node.trained", after.AvailablePlayerCount, LevelUps(_before!, after));

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
