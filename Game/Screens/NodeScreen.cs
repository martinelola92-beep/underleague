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
/// Los cuatro se <b>abren</b> y esperan una decisión (<c>TreatPlayer</c>/<c>ForgePlayer</c>, <c>ExpandRoster</c>,
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

    /// <summary>Jugador cuya mesa del herrero está abierta (ADR 0164); −1 si no hay ninguna.</summary>
    private int _forgePlayer = -1;

    /// <summary>Oro extra elegido en la mesa del herrero (ADR 0164, RF-095b).</summary>
    private int _forgeExtra;

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

        if (Tour.Clinic && _node.Kind == NodeKind.Clinic)
        {
            // Solo captura: la clínica de un mapa recién generado no tiene a quién curar. Dos graves, uno con
            // una prótesis ya puesta (para ver la ranura ocupada) y oro de sobra; se abre la mesa del primero
            // con dos de oro extra, que es lo que haría el jugador tras probar el botón.
            _run.SeedForCapture(seeded =>
            {
                var first = seeded.Roster[1] with { PhysicalState = PhysicalState.SevereInjury };
                var second = seeded.Roster[2] with { PhysicalState = PhysicalState.SevereInjury };
                var first2 = Sim.Run.Systems.Medical.MedicalSystem.Install(
                    first, _run.Systems!.Prostheses.Find("iron_arm")!);
                // Y el portero, que es quien la ficha de Equipo abre por defecto, ya con dos prótesis: la línea
                // de la ficha se captura después de la clínica.
                var keeper = Sim.Run.Systems.Medical.MedicalSystem.Install(
                    Sim.Run.Systems.Medical.MedicalSystem.Install(
                        seeded.Roster[0], _run.Systems!.Prostheses.Find("peg_leg")!),
                    _run.Systems!.Prostheses.Find("iron_arm")!);
                return seeded.WithPlayer(first2).WithPlayer(second).WithPlayer(keeper).WithGold(40);
            });
            _forgePlayer = _run.State!.Roster[1].Id;
            _forgeExtra = 2;
        }

        Rebuild();

        if (Tour.Clinic && _node.Kind == NodeKind.Clinic)
        {
            Tour.Step(this, "clinica-herrero", () => Nav.Go(this, Nav.Team));
        }

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
    /// que cuesta una fracción y lleva sus dos porcentajes escritos, y, para la lesión grave, la mesa del
    /// herrero (ADR 0164), que enseña su tabla de tres resultados antes de confirmar. Verlos antes de elegir es lo que hace
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
            patients.Count == 1
                ? UiText.Get("ui.node.treatSquadOne", economy.ClinicSquadCost, patients.Count, piecemeal)
                : UiText.Get("ui.node.treatSquad", economy.ClinicSquadCost, patients.Count, piecemeal),
            new Rect2(28f, y, 520f, 28f),
            state.Gold >= economy.ClinicSquadCost);
        squad.Pressed += () => Decide(new TreatSquad(), UiText.Plural(patients.Count, "ui.node.treatedSquadOne", "ui.node.treatedSquad"));
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

            // ADR 0164: el herrero solo atiende lesiones graves. Abrir la mesa no cuesta nada y no decide nada:
            // enseña la tabla de tres resultados y deja elegir el oro extra antes de confirmar (RF-095).
            if (patient.PhysicalState == PhysicalState.SevereInjury)
            {
                var prostheses = _run.Systems!.Prostheses;
                int forgePrice = Sim.Run.Systems.Medical.MedicalSystem.BlacksmithBasePrice(economy);
                bool canForge = state.Gold >= forgePrice
                    && Sim.Run.Systems.Medical.MedicalSystem.HasFreeProsthesisSlot(patient, prostheses);
                var forge = Widgets.Button(
                    this,
                    UiText.Get("ui.node.forgeOpen", forgePrice),
                    new Rect2(916f, y, 338f, 28f),
                    canForge);
                forge.Pressed += () =>
                {
                    _forgePlayer = id;
                    _forgeExtra = 0;
                    _message = string.Empty;
                    Rebuild();
                };
            }

            y += 34f;
        }

        if (_forgePlayer >= 0)
        {
            var open = patients.Find(p => p.Id == _forgePlayer);
            if (open is not null && open.PhysicalState == PhysicalState.SevereInjury)
            {
                y = BuildForge(state, economy, open, y + 6f);
            }
            else
            {
                _forgePlayer = -1;
            }
        }

        return y;
    }

    /// <summary>
    /// La mesa del herrero (ADR 0164, RF-095, RF-095b): la tabla de tres resultados con su porcentaje, el oro
    /// extra que se puede invertir —cada botón muestra la tabla que dejaría— y la confirmación. Todos los
    /// números salen de <c>BlacksmithView</c>, la misma función que tira <c>MedicalSystem.Forge</c>: lo que se
    /// lee aquí es lo que se juega.
    /// </summary>
    private float BuildForge(RunState state, Sim.Run.Systems.Economy.EconomyConfig economy, RunPlayer patient, float y)
    {
        var prostheses = _run.Systems!.Prostheses;
        var quote = Sim.Run.View.BlacksmithView.Quote(state, economy, prostheses, patient.Id, _forgeExtra);

        Widgets.Section(this, UiText.Get("ui.node.forgeTitle", patient.Name), new Vector2(28f, y), 1220f);
        y += 26f;
        Widgets.Body(this, UiText.Get("ui.node.forgeCure", quote.Odds.CurePercent), new Vector2(40f, y), 1200f);
        y += 22f;
        Widgets.Body(this, UiText.Get("ui.node.forgeImprove", quote.Odds.ImprovePercent), new Vector2(40f, y), 1200f);
        y += 22f;
        Widgets.Body(this, ForgeRange("ui.node.forgeImproveRange", quote.ImproveRange), new Vector2(60f, y), 1180f, Style.TextDim);
        y += 22f;
        Widgets.Body(this, UiText.Get("ui.node.forgeWorsen", quote.Odds.WorsenPercent), new Vector2(40f, y), 1200f, Style.Hole);
        y += 22f;
        Widgets.Body(this, ForgeRange("ui.node.forgeWorsenRange", quote.WorsenRange), new Vector2(60f, y), 1180f, Style.TextDim);
        y += 22f;

        var slots = new List<string>();
        foreach (string slot in quote.FreeSlots)
        {
            slots.Add(UiText.Get("ui.prosthesis.slot." + slot));
        }

        Widgets.Body(this, UiText.Get("ui.node.forgeSlots", string.Join(", ", slots)), new Vector2(40f, y), 1200f, Style.TextDim);
        y += 22f;
        if (quote.NextMakesAutomaton)
        {
            Widgets.Body(this, UiText.Get("ui.node.forgeAutomaton", patient.Name), new Vector2(40f, y), 1200f, Style.Hole);
            y += 22f;
        }

        Widgets.Body(this, UiText.Get("ui.node.forgeInvest", quote.MaxExtraGold), new Vector2(40f, y), 1200f, Style.TextDim);
        y += 24f;
        for (int extra = 0; extra <= quote.MaxExtraGold; extra++)
        {
            int chosen = extra;
            var option = Sim.Run.View.BlacksmithView.Quote(state, economy, prostheses, patient.Id, extra);
            var invest = Widgets.Button(
                this,
                UiText.Get("ui.node.forgeExtra", extra, option.Odds.CurePercent, option.Odds.ImprovePercent, option.Odds.WorsenPercent),
                new Rect2(40f + (extra * 150f), y, 144f, 28f),
                extra != _forgeExtra && option.Affordable);
            invest.Pressed += () =>
            {
                _forgeExtra = chosen;
                Rebuild();
            };
        }

        y += 38f;
        var confirm = Widgets.Button(
            this,
            UiText.Get("ui.node.forgeConfirm", quote.Price, _forgeExtra),
            new Rect2(40f, y, 420f, 28f),
            quote.CanConfirm);
        confirm.Pressed += () => Forge(patient, quote.ExtraGold);
        var close = Widgets.Button(this, UiText.Get("ui.node.forgeClose"), new Rect2(470f, y, 160f, 28f));
        close.Pressed += () =>
        {
            _forgePlayer = -1;
            Rebuild();
        };
        return y + 34f;
    }

    /// <summary>Rango de magnitudes y atributos posibles de una clase de prótesis con las ranuras libres (RF-012d: la apuesta se conoce entera).</summary>
    private string ForgeRange(string key, Sim.Run.View.ProsthesisRange? range)
    {
        if (range is null)
        {
            return string.Empty;
        }

        var templates = _run.Catalog!.Localization.Get(Data.GameData.Language);
        var names = new List<string>();
        foreach (var attribute in range.Attributes)
        {
            names.Add(templates.Find("attributes", attribute.ToString().ToLowerInvariant()) ?? attribute.ToString());
        }

        return UiText.Get(key, UiText.Signed(range.MinDelta), UiText.Signed(range.MaxDelta), string.Join(", ", names));
    }

    /// <summary>Confirma la mesa del herrero y anuncia lo que ha salido: la curación, o la prótesis con su efecto (ADR 0164).</summary>
    private void Forge(RunPlayer before, int extra)
    {
        var prostheses = _run.Systems!.Prostheses;
        try
        {
            _run.Apply(new ForgePlayer(before.Id, extra));
            var after = _run.State!.GetPlayer(before.Id);
            var result = Sim.Run.View.BlacksmithView.Outcome(before, after, prostheses);
            _message = ForgeMessage(before.Name, result);
            _forgePlayer = -1;
        }
        catch (Exception error)
        {
            _message = UiText.Get("ui.node.error", error.Message);
        }

        Rebuild();
    }

    private string ForgeMessage(string name, Sim.Run.View.BlacksmithResult result)
    {
        if (result.Prosthesis is null)
        {
            return UiText.Get("ui.node.forgeCured", name);
        }

        var templates = _run.Catalog!.Localization.Get(Data.GameData.Language);
        string attribute = templates.Find("attributes", result.Prosthesis.Attribute.ToString().ToLowerInvariant())
            ?? result.Prosthesis.Attribute.ToString();
        string text = UiText.Get(
            result.Kind == Sim.Run.View.BlacksmithOutcomeKind.Improved ? "ui.node.forgeImproved" : "ui.node.forgeWorsened",
            name,
            UiText.Name(result.Prosthesis.Name),
            attribute,
            UiText.Signed(result.Prosthesis.Delta));
        return result.BecameAutomaton ? text + " " + UiText.Get("ui.node.forgeBecameAutomaton", name) : text;
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
                UiText.Get("ui.node.eventOption", option.Name, option.Effect)
                    + (option.NoConsumableSlot ? " · " + UiText.Get("ui.node.eventNoSlot") : string.Empty),
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
