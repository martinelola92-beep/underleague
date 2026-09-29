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
/// Los cuatro se <b>abren</b> y esperan una decisión (<c>TreatPlayer</c>, <c>ExpandRoster</c>,
/// <c>ChooseEventOption</c>, <c>ChooseTrainingSession</c>) hasta que el jugador sale con
/// <c>LeaveNode</c>: entrar no resuelve nada por sí solo. El entrenamiento se resolvía solo hasta la
/// ADR 0160; el evento, hasta la ADR 0100.
/// </para>
/// <para>Los costes y los efectos salen de <c>data/economy</c> a través de
/// <c>RunController.Systems.Economy</c>; la pantalla no conoce ni un número del juego (RT-014).</para>
/// </summary>
public partial class NodeScreen : Control
{
    private static readonly Sim.Model.Position[] FieldPositions =
        { Sim.Model.Position.Defender, Sim.Model.Position.Midfielder, Sim.Model.Position.Forward };

    private RunController _run = null!;
    private MapNode _node = null!;
    private string _message = string.Empty;

    /// <summary>Opción de evento pendiente de señalar (ADR 0100); −1 si no hay ninguna.</summary>
    private int _eventOption = -1;

    /// <summary>Primer señalado de la opción de evento en curso (ADR 0159); −1 si aún no se ha elegido.</summary>
    private int _eventTarget = -1;

    private string _eventTargetName = string.Empty;

    /// <summary>Sesión de entrenamiento pendiente de señalar (ADR 0160); −1 si no hay ninguna.</summary>
    private int _trainSession = -1;

    /// <summary>Señalado de la sesión de entrenamiento en curso; −1 si aún no se ha elegido.</summary>
    private int _trainTarget = -1;

    private string _trainTargetName = string.Empty;

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
        }
        else if (run.SelectedNodeId >= 0)
        {
            _node = state.GetNode(run.SelectedNodeId);
        }
        else
        {
            Nav.Route(this);
            return;
        }

        Rebuild();

        if (Tour.Event)
        {
            Tour.Step(this, "evento", null);
        }

        if (Tour.Training)
        {
            Tour.Step(this, "entrenamiento", null);
        }
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

        Layout.CenterLegacy(this);
        Widgets.Background(this);
        Widgets.Header(this, Title(), UiText.WithBetRefund(UiText.Get("ui.node.gold", state.Gold), state));
        Widgets.Panel(this, new Rect2(12f, 52f, 1256f, 690f));

        float y = 72f;
        Widgets.Body(this, Description(economy), new Vector2(28f, y), 1220f, Style.TextDim);
        y += 40f;

        y = _node.Kind switch
        {
            NodeKind.Clinic => BuildClinic(state, economy, y),
            NodeKind.Training => BuildTraining(y),
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

    private string Description(Sim.Run.Systems.Economy.EconomyConfig economy) => _node.Kind switch
    {
        NodeKind.Clinic => UiText.Get("ui.node.clinicBody", economy.ClinicCost),
        NodeKind.Training => UiText.Get("ui.node.trainBody"),
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
    /// Evento (ADR 0100, segundo objetivo desde la ADR 0159): la carta con sus opciones, cada una con su
    /// línea de efecto compuesta y su coste delante. Las que piden un cuerpo abren la lista de
    /// disponibles; las que piden dos (el sacrificio y cualquier opción que reparta premio y daño) piden
    /// el segundo después del primero, excluyéndolo de la lista. El jugador señala siempre a quién, nunca
    /// el juego por él (RF-012d).
    /// </summary>
    private float BuildEvent(float y)
    {
        var view = _run.Event();
        if (view is null)
        {
            // BA-A: si la carta no llega, se dice por qué en vez de dejar la pantalla muda. El botón de
            // volver al mapa está siempre activo, así que esto informa pero ya no encierra a nadie.
            Widgets.Body(this, UiText.Get("ui.node.eventNone"), new Vector2(28f, y), 1220f, Style.TextDim);
            return y + 30f;
        }

        Widgets.Body(this, view.Description, new Vector2(28f, y), 1220f, Style.TextDim);
        y += 30f;

        foreach (var option in view.Options)
        {
            var button = Widgets.Button(
                this,
                UiText.Get("ui.node.eventOption", option.Name, option.Effect),
                new Rect2(28f, y, 760f, 28f),
                option.Affordable);
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
                _eventTarget = -1;
                _message = UiText.Get("ui.node.eventPickTarget");
                Rebuild();
            };
            y += 34f;
        }

        if (_eventOption < 0)
        {
            return y;
        }

        var chosen = view.Options[_eventOption];
        y += 6f;

        if (_eventTarget < 0)
        {
            // Paso del primer señalado.
            foreach (var target in chosen.Targets)
            {
                var pick = Widgets.Button(
                    this,
                    UiText.Get("ui.node.eventTarget", target.Name, target.Detail),
                    new Rect2(60f, y, 520f, 26f));
                int index = _eventOption;
                bool needsSecond = chosen.NeedsSecondTarget;
                int playerId = target.PlayerId;
                string who = target.Name;
                pick.Pressed += () =>
                {
                    if (!needsSecond)
                    {
                        _eventOption = -1;
                        Decide(new ChooseEventOption(index, playerId), UiText.Get("ui.node.eventChosenOn", who));
                        return;
                    }

                    _eventTarget = playerId;
                    _eventTargetName = who;
                    _message = UiText.Get("ui.node.eventPickSecondTarget");
                    Rebuild();
                };
                y += 30f;
            }

            return y;
        }

        // Paso del segundo señalado (ADR 0159): la misma lista, sin el ya elegido.
        foreach (var target in chosen.SecondTargets)
        {
            if (target.PlayerId == _eventTarget)
            {
                continue;
            }

            var pick = Widgets.Button(
                this,
                UiText.Get("ui.node.eventTarget", target.Name, target.Detail),
                new Rect2(60f, y, 520f, 26f));
            int index = _eventOption;
            int firstId = _eventTarget;
            string firstName = _eventTargetName;
            int secondId = target.PlayerId;
            string secondName = target.Name;
            pick.Pressed += () =>
            {
                _eventOption = -1;
                _eventTarget = -1;
                Decide(new ChooseEventOption(index, firstId, secondId), UiText.Get("ui.node.eventChosenOnTwo", firstName, secondName));
            };
            y += 30f;
        }

        return y;
    }

    /// <summary>
    /// Entrenamiento (ADR 0160): tres sesiones con su línea de efecto compuesta. La pachanga se elige de
    /// un botón; la especialización pide a quién; el cambio de puesto pide a quién y después el puesto de
    /// destino, nunca portero en ninguna dirección (ADR 0080).
    /// </summary>
    private float BuildTraining(float y)
    {
        var view = _run.Training();
        if (view is null)
        {
            Widgets.Body(this, UiText.Get("ui.node.trainNone"), new Vector2(28f, y), 1220f, Style.TextDim);
            return y + 30f;
        }

        foreach (var session in view.Sessions)
        {
            var button = Widgets.Button(
                this,
                UiText.Get("ui.node.trainOption", session.Name, session.Effect),
                new Rect2(28f, y, 760f, 28f),
                !session.NeedsTarget || session.Targets.Count > 0);
            int index = session.Index;
            bool needsTarget = session.NeedsTarget;
            string name = session.Name;
            button.Pressed += () =>
            {
                if (!needsTarget)
                {
                    Decide(new ChooseTrainingSession(index), UiText.Get("ui.node.trainChosen", name));
                    return;
                }

                _trainSession = index;
                _trainTarget = -1;
                _message = UiText.Get("ui.node.trainPickTarget");
                Rebuild();
            };
            y += 34f;
        }

        if (_trainSession < 0)
        {
            return y;
        }

        var chosen = view.Sessions[_trainSession];
        y += 6f;

        if (_trainTarget < 0)
        {
            foreach (var target in chosen.Targets)
            {
                var pick = Widgets.Button(
                    this,
                    UiText.Get("ui.node.trainTarget", target.Name, target.Detail),
                    new Rect2(60f, y, 520f, 26f));
                int index = _trainSession;
                bool needsPosition = chosen.NeedsPosition;
                int playerId = target.PlayerId;
                string who = target.Name;
                pick.Pressed += () =>
                {
                    if (!needsPosition)
                    {
                        _trainSession = -1;
                        Decide(new ChooseTrainingSession(index, playerId), UiText.Get("ui.node.trainChosenOn", who));
                        return;
                    }

                    _trainTarget = playerId;
                    _trainTargetName = who;
                    _message = UiText.Get("ui.node.trainPickPosition");
                    Rebuild();
                };
                y += 30f;
            }

            return y;
        }

        // Paso del puesto de destino (solo cambio de puesto): nunca portero (ADR 0080).
        foreach (var position in FieldPositions)
        {
            var pick = Widgets.Button(this, UiText.Get("ui.pos." + position), new Rect2(60f, y, 120f, 26f));
            int index = _trainSession;
            int playerId = _trainTarget;
            string who = _trainTargetName;
            var destination = position;
            pick.Pressed += () =>
            {
                _trainSession = -1;
                _trainTarget = -1;
                Decide(new ChooseTrainingSession(index, playerId, destination), UiText.Get("ui.node.trainChosenOn", who));
            };
            y += 30f;
        }

        return y;
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
    /// <summary>
    /// **Siempre se puede volver al mapa** (BA-A). Antes esto exigía <c>_entered || Phase == NodeOpen</c>, y
    /// <c>_entered</c> se recalcula en cada <c>Rebuild</c>: si al resolver una carta de evento la fase dejaba
    /// de ser <c>NodeOpen</c> con el nodo todavía seleccionado, quedaba <c>_entered</c> a false, la vista de
    /// evento a null —así que la pantalla no dibujaba ni descripción ni opciones— y el botón de salir
    /// deshabilitado. El jugador se quedaba encerrado y, con guardado ironman (RT-061), la única salida era
    /// cerrar el juego y perder la run.
    ///
    /// <para>Habilitarlo siempre no puede corromper nada: <see cref="Leave"/> solo emite <c>LeaveNode</c> si
    /// de verdad hay un nodo abierto, y en cualquier otro caso se limita a volver al mapa. La regla es que
    /// **de una pantalla de nodo siempre se sale**, pase lo que pase con su contenido.</para>
    /// </summary>
    private static bool Leaveable() => true;

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
