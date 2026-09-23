using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.View;

/// <summary>
/// El evento que encabeza un momento: junto con <see cref="MatchMoment.Level"/> decide qué frase, qué
/// canal y qué animación le corresponde en <c>/Game</c>. Un evento anulado conserva el <c>Kind</c> de su
/// tipo base (<see cref="MatchMoment.Cancelled"/> dice si lo fue), no tiene uno propio.
/// </summary>
public enum MomentKind
{
    Kickoff,
    Foul,
    Consumable,
    Substitution,
    Yellow,
    MinorInjury,
    Goal,
    Red,
    SevereInjury,
    Mob,
    RefereeLeaves,
    Death,
    FullTime,
}

/// <summary>
/// Un tramo narrable del partido: uno o más eventos que el jugador lee como un solo suceso (ADR 0119).
/// </summary>
/// <param name="Frame">Fotograma de la traza (<see cref="MatchTrace.FrameOfTick"/>) del primer evento del momento.</param>
/// <param name="FreezeFrame">
/// <c>Math.Max(Frame - 1, 0)</c>: el fotograma <b>anterior</b> al suceso, con el que se congela la
/// pantalla antes de presentarlo (C.2 de la ADR 0119) — en <see cref="Frame"/> el motor ya recolocó o
/// retiró al jugador implicado.
/// </param>
/// <param name="LastFrame">Fotograma del último evento del momento.</param>
/// <param name="Level">Nivel narrativo 1..4 (N1..N4): el máximo de sus eventos.</param>
/// <param name="Kind">El tipo del evento que encabeza el momento (el de mayor nivel; el último en empate).</param>
/// <param name="Team">Equipo del evento que encabeza el momento.</param>
/// <param name="LeadPlayerId">Actor del evento que encabeza el momento; -1 si no tiene.</param>
/// <param name="Pauses">Congela la reproducción a 1x (gol, muerte, final, lesión grave propia, o decisión).</param>
/// <param name="Decision">
/// Contiene un origen de decisión de sustitución (ADR 0094) del equipo del jugador: el momento queda
/// cerrado para que la re-simulación con la sustitución elegida no lo cambie.
/// </param>
/// <param name="EventIndices">Índices en <c>MatchResult.Events</c> de los eventos del momento, ascendentes.</param>
/// <param name="Cancelled">El evento que encabeza el momento (<see cref="Kind"/>) fue anulado por un perk.</param>
/// <param name="HasGoal">El momento contiene un Goal no anulado, aunque no sea el que lo encabece.</param>
public sealed record MatchMoment(
    int Frame,
    int FreezeFrame,
    int LastFrame,
    int Level,
    MomentKind Kind,
    int Team,
    int LeadPlayerId,
    bool Pauses,
    bool Decision,
    IReadOnlyList<int> EventIndices,
    bool Cancelled,
    bool HasGoal);

/// <summary>Un aviso de perk (<see cref="MatchFlashView"/>) ubicado respecto a los momentos que lo rodean.</summary>
/// <param name="Flash">El aviso original.</param>
/// <param name="MomentIndex">
/// Índice en <see cref="MatchMoments.Moments"/> del momento que lo absorbe (mismo jugador y cerca en el
/// tiempo), o -1 si no hay ninguno: entonces se presenta en su propio canal.
/// </param>
public sealed record MomentMark(MatchFlash Flash, int MomentIndex);

/// <summary>Salida completa de <see cref="MatchMomentView.Build"/>: los momentos del partido y sus marcas de perk.</summary>
public sealed record MatchMoments(IReadOnlyList<MatchMoment> Moments, IReadOnlyList<MomentMark> Marks);

/// <summary>
/// Cómo se presenta un momento a una velocidad dada (<see cref="MatchMomentView.Present"/>).
/// </summary>
/// <param name="Shown">Si el momento se presenta a esta velocidad.</param>
/// <param name="Compressed">Si se presenta en su forma comprimida (sin la locución completa).</param>
/// <param name="Pauses">Si, presentándose, congela la reproducción.</param>
public sealed record MomentPresentation(bool Shown, bool Compressed, bool Pauses);

/// <summary>
/// Agrupa la secuencia de eventos de un partido ya jugado en momentos narrables (ADR 0119).
///
/// <para><b>Por qué existe.</b> La dirección de UI (<c>docs/ui/README.md</c>) exige que la retransmisión
/// no reaccione evento a evento —un gol, el final que le sigue y quién lo marcó son un solo suceso, no
/// tres— y que esa agrupación se pueda medir (RT-084 prohíbe probar interfaz, así que la lógica de
/// agrupar no puede vivir en <c>/Game</c>). Es la misma forma que <see cref="MatchFlashView"/> y
/// <see cref="MatchLogView"/>: eventos + traza → filas ordenadas, sin Godot (RT-011) y sin reloj
/// (RT-012). El ritmo real —duraciones en segundos, cola de voz, caducidad— es de <c>/Game</c>; aquí solo
/// hay fotogramas y niveles.</para>
///
/// <para><b>No cambia el partido.</b> Es una vista pura sobre <c>MatchResult</c> ya resuelto (RT-024): con
/// o sin ella el partido es idéntico. La única regla de segundo orden que le afecta es la sustitución
/// (ADR 0094): re-jugar el partido con la decisión elegida no puede cambiar los momentos anteriores al
/// tick de la decisión, así que el momento que contiene el origen se cierra ahí (regla 4).</para>
///
/// <para><b>Núcleo sin traza.</b> <see cref="Group"/> y <see cref="Absorb"/> son <c>internal</c> y solo
/// piden funciones <c>tick → fotograma</c> y <c>fotograma → tick</c>, no una <see cref="MatchTrace"/>
/// entera: así <c>Sim.Tests</c> los prueba con eventos construidos a mano y la identidad como conversión,
/// sin tener que jugar un partido para cada caso de borde de la fusión.</para>
/// </summary>
public static class MatchMomentView
{
    /// <summary>Ventana de fusión: 1 s a 15 ticks/s (RT-020).</summary>
    public const int FusionWindowTicks = 15;

    /// <summary>
    /// Primer segundo del partido: los perks que se disparan aquí son estado inicial (P1 de la fase A de
    /// medición), no un acontecimiento, así que sus marcas se descartan.
    /// </summary>
    public const int KickoffTicks = 15;

    private const string CancelledSuffix = ":cancelled";

    /// <summary>Cadenas fijas de fusión: si el momento ya contiene <c>From</c> (sin anular), <c>To</c> se fusiona sin compartir persona.</summary>
    private static readonly (MomentKind From, MomentKind To)[] Chains =
    {
        (MomentKind.Goal, MomentKind.FullTime),
        (MomentKind.Mob, MomentKind.RefereeLeaves),
        (MomentKind.Death, MomentKind.FullTime),
        (MomentKind.MinorInjury, MomentKind.FullTime),
        (MomentKind.SevereInjury, MomentKind.FullTime),
        (MomentKind.Yellow, MomentKind.FullTime),
        (MomentKind.Red, MomentKind.FullTime),
    };

    /// <summary>
    /// Agrupa los eventos de un partido ya jugado en momentos, y ubica las marcas de perk respecto a
    /// ellos. Exige <c>result.Trace</c> (<c>SimConfig.Trace = true</c>): sin traza no hay fotograma al
    /// que atar un momento.
    /// </summary>
    /// <param name="playerTeam">Equipo del jugador (0 local, 1 visitante): decide pausa propia y decisión.</param>
    /// <param name="declined">
    /// Puntos ya respondidos con «que se quede el hueco» (ADR 0134 D). <b>Hay que pasarlos</b>: un rechazo
    /// no cambia el partido —no entra nadie, no se vuelve a simular—, así que sin ellos el punto sigue
    /// pendiente, la reproducción vuelve a parar en ese tick y la bandeja se reabre, o peor, se abre
    /// enseñando el punto siguiente en el tick del anterior.
    /// </param>
    public static MatchMoments Build(
        MatchSetup setup,
        MatchResult result,
        Catalog catalog,
        int playerTeam = 0,
        IReadOnlyList<DeclinedSubstitution>? declined = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(catalog);
        if (result.Trace is null)
        {
            throw new ArgumentException(
                "MatchMomentView.Build necesita result.Trace (juega el partido con SimConfig.Trace = true)",
                nameof(result));
        }

        var trace = result.Trace;
        var events = result.Events;
        var pending = SubstitutionPoints.Pending(setup, result, playerTeam, catalog, declined);

        var moments = Group(events, trace.FrameOfTick, playerTeam, pending);
        var flashes = MatchFlashView.Build(events, trace, catalog);
        int cutoffTick = FindMatchStartTick(events) + KickoffTicks;
        var marks = Absorb(flashes, trace.TickAt, events, moments, cutoffTick);

        return new MatchMoments(moments, marks);
    }

    /// <summary>
    /// Política de velocidad (C7 de la ADR 0119): a más velocidad, menos momentos se presentan y siempre
    /// comprimidos. Las duraciones reales no están aquí, son de <c>/Game</c>.
    /// </summary>
    /// <param name="speed">1, 4 o 16; cualquier otro valor es un error explícito (RT-032).</param>
    public static MomentPresentation Present(MatchMoment moment, int speed)
    {
        ArgumentNullException.ThrowIfNull(moment);
        switch (speed)
        {
            case 1:
                return new MomentPresentation(true, false, moment.Pauses);

            case 4:
            {
                // Un gol encabezado por una N3 posterior en la misma fusión (por ejemplo el árbitro que
                // se va tras el gol) no deja de ser un gol: HasGoal mira el momento entero, no solo quién
                // lo encabeza.
                bool shown = moment.HasGoal || moment.Level == 4 || moment.Decision;
                bool pauses = shown && (moment.HasGoal || moment.Level == 4 || moment.Decision);
                return new MomentPresentation(shown, shown, pauses);
            }

            case 16:
            {
                bool shown = moment.Level == 4 || moment.Decision;
                return new MomentPresentation(shown, shown, shown);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "la velocidad de reproducción debe ser 1, 4 o 16");
        }
    }

    /// <summary>
    /// El núcleo de la regla 1-4: clasifica y fusiona los eventos en momentos, ordenados. Sin traza:
    /// <paramref name="frameOfTick"/> es la única conversión que necesita (RT-020: un tick es un fotograma).
    /// </summary>
    internal static IReadOnlyList<MatchMoment> Group(
        IReadOnlyList<MatchEvent> events, Func<int, int> frameOfTick, int playerTeam, SubstitutionPoint? pending)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(frameOfTick);

        var builders = new List<MomentBuilder>();
        for (int i = 0; i < events.Count; i++)
        {
            var matchEvent = events[i];
            var classification = Classify(matchEvent, playerTeam);
            if (classification is null)
            {
                continue;
            }

            var (kind, level, pauses, cancelled) = classification.Value;
            var last = builders.Count > 0 ? builders[^1] : null;
            bool fuses = kind != MomentKind.Kickoff && last is not null && CanFuse(last, matchEvent, kind, playerTeam);

            MomentBuilder target;
            if (fuses)
            {
                target = last!;
            }
            else
            {
                target = new MomentBuilder();
                builders.Add(target);
            }

            target.Add(i, matchEvent, kind, level, pauses, cancelled);
            if (IsDecisionOrigin(matchEvent, playerTeam, events, pending))
            {
                target.MarkDecision(matchEvent.Actor, matchEvent.Tick);
            }
        }

        var moments = new List<MatchMoment>(builders.Count);
        for (int i = 0; i < builders.Count; i++)
        {
            moments.Add(builders[i].ToMoment(frameOfTick));
        }

        // Orden determinista (RT-041): fotograma ascendente y, en empate, por su primer índice de evento.
        // Ya llegan así (los eventos están en orden y cada momento nuevo abre en el primero sin fusionar),
        // pero se ordena explícito para no depender de esa propiedad implícita.
        moments.Sort(static (a, b) => a.Frame != b.Frame
            ? a.Frame.CompareTo(b.Frame)
            : a.EventIndices[0].CompareTo(b.EventIndices[0]));

        return moments;
    }

    /// <summary>
    /// El núcleo de la regla 5: ubica los avisos de perk (ya filtrados de fuera) respecto a los momentos.
    /// Cada marca se absorbe en el primer momento (que no sea el Kickoff) que comparte jugador y cuyo
    /// tramo <c>[Frame - FusionWindowTicks, LastFrame + FusionWindowTicks]</c> contiene su fotograma.
    /// </summary>
    internal static IReadOnlyList<MomentMark> Absorb(
        IReadOnlyList<MatchFlash> flashes,
        Func<int, int> tickOfFrame,
        IReadOnlyList<MatchEvent> events,
        IReadOnlyList<MatchMoment> moments,
        int cutoffTick)
    {
        ArgumentNullException.ThrowIfNull(flashes);
        ArgumentNullException.ThrowIfNull(tickOfFrame);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(moments);

        var marks = new List<MomentMark>(flashes.Count);
        for (int f = 0; f < flashes.Count; f++)
        {
            var flash = flashes[f];
            if (tickOfFrame(flash.Frame) < cutoffTick)
            {
                continue;
            }

            int momentIndex = -1;
            for (int m = 0; m < moments.Count; m++)
            {
                var moment = moments[m];
                if (moment.Kind == MomentKind.Kickoff
                    || flash.Frame < moment.Frame - FusionWindowTicks
                    || flash.Frame > moment.LastFrame + FusionWindowTicks)
                {
                    continue;
                }

                if (ContainsPlayer(events, moment, flash.PlayerId))
                {
                    momentIndex = m;
                    break;
                }
            }

            marks.Add(new MomentMark(flash, momentIndex));
        }

        return marks;
    }

    /// <summary>
    /// Clasificación de un evento (regla 1): nivel, tipo de momento, si pausa por sí solo, y si el evento
    /// venía anulado. Null si el evento no narra nada (N0: intentos, inicio/fin de jugada, PERK_TRIGGERED).
    /// Un evento anulado conserva el <c>Kind</c> de su tipo base, con nivel 1 y sin pausa; no participa en
    /// cadenas fijas (eso lo aplica <see cref="MomentBuilder.Add"/>, que solo anota en <c>Kinds</c> lo no
    /// anulado).
    /// </summary>
    private static (MomentKind Kind, int Level, bool Pauses, bool Cancelled)? Classify(MatchEvent matchEvent, int playerTeam)
    {
        string detail = StripCancelled(matchEvent.Detail, out bool cancelled);
        var baseline = BaseClassification(matchEvent.Type, detail, matchEvent.Team, playerTeam);
        if (baseline is null)
        {
            return null;
        }

        return cancelled
            ? (baseline.Value.Kind, 1, false, true)
            : (baseline.Value.Kind, baseline.Value.Level, baseline.Value.Pauses, false);
    }

    /// <summary>La tabla de tipo base de la regla 1, sin mirar todavía si el evento venía anulado.</summary>
    private static (MomentKind Kind, int Level, bool Pauses)? BaseClassification(EventType type, string detail, int team, int playerTeam) => type switch
    {
        EventType.MatchStart => (MomentKind.Kickoff, 3, false),
        EventType.Foul => (MomentKind.Foul, 1, false),
        EventType.ConsumableUsed => (MomentKind.Consumable, 1, false),
        EventType.Substitution => (MomentKind.Substitution, 1, false),
        EventType.Card when detail == "yellow" => (MomentKind.Yellow, 2, false),
        EventType.Card when detail == "red" => (MomentKind.Red, 3, false),
        EventType.Injury when detail == "minor" => (MomentKind.MinorInjury, 2, false),
        EventType.Injury when detail == "severe" => (MomentKind.SevereInjury, 3, team == playerTeam),
        EventType.Goal when detail is "goal" or "goldenGoal" => (MomentKind.Goal, 3, true),
        EventType.MobStart => (MomentKind.Mob, 3, false),
        EventType.RefereeLeaves => (MomentKind.RefereeLeaves, 3, false),
        EventType.Death => (MomentKind.Death, 4, true),
        EventType.MatchEnd => (MomentKind.FullTime, 4, true),
        _ => null,
    };

    /// <summary>Detail sin el sufijo ":cancelled", y si lo llevaba.</summary>
    private static string StripCancelled(string detail, out bool cancelled)
    {
        if (detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
        {
            cancelled = true;
            return detail[..^CancelledSuffix.Length];
        }

        cancelled = false;
        return detail;
    }

    /// <summary>
    /// Si un evento clasificado se fusiona con el último momento abierto (regla 2). El Kickoff no admite
    /// uniones. Un momento cerrado por decisión (regla 4) admite, además de la sustitución que la
    /// responde, cualquier evento del mismo tick que el origen: ese tick es idéntico en la re-simulación
    /// (la sustitución entra en T+1), así que admitirlo no rompe la garantía de que lo anterior a la
    /// decisión no cambia. Es lo que evita que una lesión y la muerte del mismo jugador en el mismo tick
    /// —dos orígenes de decisión seguidos— se partan en dos momentos con dos pausas.
    /// </summary>
    private static bool CanFuse(MomentBuilder moment, MatchEvent matchEvent, MomentKind kind, int playerTeam)
    {
        if (moment.Kind == MomentKind.Kickoff)
        {
            return false;
        }

        if (moment.Closed)
        {
            if (moment.DecisionTicks.Contains(matchEvent.Tick))
            {
                return true;
            }

            return kind == MomentKind.Substitution
                && matchEvent.Team == playerTeam
                && moment.DecisionActors.Contains(matchEvent.Target);
        }

        if (matchEvent.Tick - moment.LastTick > FusionWindowTicks)
        {
            return false;
        }

        return SharesPerson(moment.PersonSet, matchEvent) || FormsChain(moment.Kinds, kind);
    }

    /// <summary>Si el evento comparte a alguien (actor, objetivo o rival, con valor >= 0) con el momento.</summary>
    private static bool SharesPerson(List<int> personSet, MatchEvent matchEvent)
    {
        return (matchEvent.Actor >= 0 && personSet.Contains(matchEvent.Actor))
            || (matchEvent.Target >= 0 && personSet.Contains(matchEvent.Target))
            || (matchEvent.Opponent >= 0 && personSet.Contains(matchEvent.Opponent));
    }

    /// <summary>Si el momento ya contiene (sin anular) el origen de alguna cadena fija que termina en <paramref name="incoming"/>.</summary>
    private static bool FormsChain(List<MomentKind> present, MomentKind incoming)
    {
        for (int i = 0; i < Chains.Length; i++)
        {
            if (Chains[i].To == incoming && present.Contains(Chains[i].From))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Un Injury o Death (no cancelado) del equipo del jugador es origen de decisión (regla 4) si ya tiene
    /// una sustitución que lo responde, o si es el punto de decisión todavía pendiente.
    /// </summary>
    private static bool IsDecisionOrigin(
        MatchEvent matchEvent, int playerTeam, IReadOnlyList<MatchEvent> events, SubstitutionPoint? pending)
    {
        if ((matchEvent.Type != EventType.Injury && matchEvent.Type != EventType.Death)
            || matchEvent.Team != playerTeam
            || matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        if (HasRespondingSubstitution(events, playerTeam, matchEvent.Actor))
        {
            return true;
        }

        return pending is not null && pending.Tick == matchEvent.Tick && pending.OutPlayerId == matchEvent.Actor;
    }

    /// <summary>Si ya existe, en toda la secuencia, la sustitución que responde a que ese jugador saliera.</summary>
    private static bool HasRespondingSubstitution(IReadOnlyList<MatchEvent> events, int team, int outPlayerId)
    {
        for (int i = 0; i < events.Count; i++)
        {
            var candidate = events[i];
            if (candidate.Type == EventType.Substitution && candidate.Team == team && candidate.Target == outPlayerId)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindMatchStartTick(IReadOnlyList<MatchEvent> events)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Type == EventType.MatchStart)
            {
                return events[i].Tick;
            }
        }

        return 0;
    }

    /// <summary>Si algún evento del momento tiene a ese jugador como actor, objetivo o rival.</summary>
    private static bool ContainsPlayer(IReadOnlyList<MatchEvent> events, MatchMoment moment, int playerId)
    {
        var indices = moment.EventIndices;
        for (int i = 0; i < indices.Count; i++)
        {
            var candidate = events[indices[i]];
            if (candidate.Actor == playerId || candidate.Target == playerId || candidate.Opponent == playerId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Momento en construcción: acumula lo que <see cref="MatchMomentView.CanFuse"/> necesita mirar
    /// (persona, tipos presentes sin anular, ticks y actores de decisión) además de los campos del
    /// momento final.
    /// </summary>
    private sealed class MomentBuilder
    {
        public readonly List<int> EventIndices = new();
        public readonly List<int> PersonSet = new();
        public readonly List<MomentKind> Kinds = new();
        public readonly List<int> DecisionActors = new();
        public readonly List<int> DecisionTicks = new();

        public int FirstTick;
        public int LastTick;
        public MomentKind Kind;
        public int HeadLevel = -1;
        public int HeadTeam;
        public int HeadActor;
        public bool HeadCancelled;
        public bool HasGoal;
        public bool Pauses;
        public bool Decision;
        public bool Closed;

        public void Add(int eventIndex, MatchEvent matchEvent, MomentKind kind, int level, bool pauses, bool cancelled)
        {
            if (EventIndices.Count == 0)
            {
                FirstTick = matchEvent.Tick;
            }

            EventIndices.Add(eventIndex);
            LastTick = matchEvent.Tick;
            Pauses |= pauses;

            if (!cancelled)
            {
                // Un evento anulado no forma cadena fija (regla 2 de la corrección): ni encadena por sí
                // mismo ni deja que otro se fusione después creyendo que aquí hubo un gol o una muerte de
                // verdad.
                Kinds.Add(kind);

                if (kind == MomentKind.Goal)
                {
                    HasGoal = true;
                }
            }

            if (matchEvent.Actor >= 0)
            {
                PersonSet.Add(matchEvent.Actor);
            }

            if (matchEvent.Target >= 0)
            {
                PersonSet.Add(matchEvent.Target);
            }

            if (matchEvent.Opponent >= 0)
            {
                PersonSet.Add(matchEvent.Opponent);
            }

            // Regla 3: el nivel del momento es el máximo y lo encabeza el ÚLTIMO evento en empate, así
            // que ">=" en vez de ">" hace que cada evento con el nivel más alto visto hasta ahora
            // reemplace al anterior.
            if (level >= HeadLevel)
            {
                HeadLevel = level;
                Kind = kind;
                HeadTeam = matchEvent.Team;
                HeadActor = matchEvent.Actor;
                HeadCancelled = cancelled;
            }
        }

        public void MarkDecision(int actorId, int tick)
        {
            Decision = true;
            Closed = true;
            if (!DecisionActors.Contains(actorId))
            {
                DecisionActors.Add(actorId);
            }

            if (!DecisionTicks.Contains(tick))
            {
                DecisionTicks.Add(tick);
            }
        }

        public MatchMoment ToMoment(Func<int, int> frameOfTick)
        {
            int frame = frameOfTick(FirstTick);
            int freezeFrame = Math.Max(frame - 1, 0);
            int lastFrame = frameOfTick(LastTick);
            return new MatchMoment(
                frame, freezeFrame, lastFrame, HeadLevel, Kind, HeadTeam, HeadActor, Pauses || Decision, Decision,
                EventIndices, HeadCancelled, HasGoal);
        }
    }
}
