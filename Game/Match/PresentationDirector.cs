using System;
using System.Collections.Generic;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Match;

/// <summary>
/// Duraciones reales del director, en segundos (ADR 0119/0120, <c>docs/ui/README.md</c> §4/§6): todas
/// <b>provisionales</b> hasta el prototipo animado con arte. Es la parte del ritmo que vive en
/// <c>/Game</c> — el agrupador y el nivel de cada momento son de <c>Sim.Run.View.MatchMomentView</c> y no
/// cambian aquí.
/// </summary>
/// <param name="N1">Duración de un sello N1 (detalle) a 1×.</param>
/// <param name="N2">Duración de un sello N2 (notable) a 1×.</param>
/// <param name="N3">Duración genérica de una voz alta N3 a 1× (gol usa <see cref="GoalFreeze"/>, no esta).</param>
/// <param name="N4">Duración de una voz alta N4 a 1×.</param>
/// <param name="GoalFreeze">Congelado del gol a 1× (docs/ui/README.md §5: "1 s de balón muerto natural" más el tiempo de leer el estandarte).</param>
/// <param name="GoalCompressed">Duración del gol comprimido (x4).</param>
/// <param name="N4CompressedX4">Duración de la N4 comprimida a x4.</param>
/// <param name="N4CompressedX16">Duración de la N4 comprimida a x16.</param>
/// <param name="VoiceExpiry">Cuánto puede esperar en la cola una voz alta que no pausa antes de descartarse.</param>
public sealed record DirectorTimings(
    double N1,
    double N2,
    double N3,
    double N4,
    double GoalFreeze,
    double GoalCompressed,
    double N4CompressedX4,
    double N4CompressedX16,
    double VoiceExpiry)
{
    /// <summary>Los valores provisionales del encargo (docs/ui/README.md §4/§6).</summary>
    public static DirectorTimings Default { get; } = new(
        N1: 1.0,
        N2: 1.5,
        N3: 3.0,
        N4: 4.0,
        GoalFreeze: 2.0,
        GoalCompressed: 1.0,
        N4CompressedX4: 1.5,
        N4CompressedX16: 1.0,
        VoiceExpiry: 1.5);
}

/// <summary>
/// Lo que el director tiene activo en un instante de pantalla, para que <c>BroadcastScreen</c> pinte sin
/// decidir nada por su cuenta (RT-014): a qué fotograma se enseña el campo, si está congelado, qué sello y
/// qué voz alta están en pantalla y si hay una decisión pendiente de resolver.
/// </summary>
/// <param name="Frozen">Si la reproducción no debe avanzar este fotograma: <c>DisplayFrame</c> se queda fijo.</param>
/// <param name="DisplayFrame">El fotograma que hay que mostrar. Con <see cref="Frozen"/>, la pantalla escribe este valor en su propio reloj (C.2: se congela en el fotograma anterior al suceso).</param>
/// <param name="Voice">El momento en la voz alta (N3/N4), o null si no hay ninguno activo.</param>
/// <param name="VoiceProgress">0..1: cuánto de su duración lleva mostrado <see cref="Voice"/>.</param>
/// <param name="Stamp">El momento en el canal de sellos (N1/N2), o null si no hay ninguno activo.</param>
/// <param name="StampProgress">0..1: cuánto de su duración lleva mostrado <see cref="Stamp"/>.</param>
/// <param name="AwaitingDecision">
/// Hay una decisión de sustitución (ADR 0094) congelando la reproducción hasta que la pantalla llame a
/// <see cref="PresentationDirector.Resolve"/> tras jugar la elección del jugador.
/// </param>
public sealed record DirectorFrame(
    bool Frozen,
    int DisplayFrame,
    MatchMoment? Voice,
    float VoiceProgress,
    MatchMoment? Stamp,
    float StampProgress,
    bool AwaitingDecision);

/// <summary>
/// Director de presentación de la retransmisión (ADR 0119 «En <c>/Game</c>»): recibe los momentos ya
/// agrupados y clasificados por <see cref="MatchMomentView"/> y decide, fotograma de pantalla a fotograma
/// de pantalla, qué se está presentando y si la reproducción tiene que congelarse. No conoce Godot ni
/// ningún tipo de motor — es una clase C# corriente, para poder razonar sobre ella (y, si algún día hace
/// falta, probarla) sin levantar el motor.
///
/// <para><b>No decide nada del partido</b> (RT-014): los momentos y sus niveles ya vienen resueltos por
/// <c>/Sim</c>; aquí solo hay reloj real, una cola de un hueco y una máquina de estados de dos canales
/// (sellos y voz alta). El residuo de cada suceso —marcador, tiras, bandeja— lo lee <c>BroadcastScreen</c>
/// directamente de la traza y del log en el fotograma que <see cref="Advance"/> devuelve; el director no
/// lo produce ni lo recuerda (ADR 0119, «Residuo»).</para>
/// </summary>
public sealed class PresentationDirector
{
    private readonly IReadOnlyList<MatchMoment> _moments;
    private readonly DirectorTimings _timings;

    private int _nextMomentIndex;

    private MatchMoment? _stamp;
    private double _stampElapsed;
    private double _stampDuration;

    private MatchMoment? _voice;
    private double _voiceElapsed;
    private double _voiceDuration;
    private readonly List<QueuedVoice> _voiceQueue = new();

    private bool _frozen;
    private int _frozenAtFrame;

    /// <summary>
    /// El fotograma del suceso (<see cref="MatchMoment.Frame"/>) de la voz que tiene congelada la
    /// reproducción: adonde salta <see cref="DirectorFrame.DisplayFrame"/> en el instante en que termina
    /// (C.2: se venía mostrando <see cref="_frozenAtFrame"/>, el anterior al suceso).
    /// </summary>
    private int _unfreezeToFrame;

    private bool _awaitingDecision;

    /// <summary>
    /// La velocidad del último <see cref="Advance"/> que llegó a procesar momentos (revisión del
    /// orquestador, 19 sep 2026): <see cref="FinishVoice"/> la necesita para volver a evaluar la cola con
    /// la velocidad de <b>ahora</b>, no con la que había cuando cada una se encoló.
    /// </summary>
    private int _lastSpeed = 1;

    public PresentationDirector(IReadOnlyList<MatchMoment> moments, DirectorTimings timings)
    {
        _moments = moments ?? throw new ArgumentNullException(nameof(moments));
        _timings = timings ?? throw new ArgumentNullException(nameof(timings));
    }

    /// <summary>
    /// Todo momento con <c>Frame &lt; frame</c> se da por pasado: nunca se presenta hacia atrás. Vacía la
    /// cola y los dos canales — se usa al recargar la reproducción (una decisión resuelta, un retroceso de
    /// la barra en el modo de depuración) para empezar desde cero sin arrastrar nada del director viejo.
    /// </summary>
    public void Seek(int frame)
    {
        _stamp = null;
        _stampElapsed = 0d;
        _voice = null;
        _voiceElapsed = 0d;
        _voiceQueue.Clear();
        _frozen = false;
        _awaitingDecision = false;

        int index = 0;
        while (index < _moments.Count && _moments[index].Frame < frame)
        {
            index++;
        }

        _nextMomentIndex = index;
    }

    /// <summary>
    /// La pantalla llama aquí tras resolver una decisión (jugado el partido con la sustitución, momentos
    /// reconstruidos, director nuevo con <see cref="Seek"/> ya hecho a partir del tick siguiente). En un
    /// director recién creado no hay nada que resolver, pero el método es seguro de llamar siempre: deja
    /// el director libre para seguir avanzando.
    /// </summary>
    public void Resolve()
    {
        _awaitingDecision = false;
        _frozen = false;
    }

    /// <summary>
    /// Avanza el reloj real y devuelve qué hay que enseñar. <paramref name="frame"/> es el fotograma al
    /// que la pantalla querría llegar si nada la frenara (su propio reloj de reproducción, como en
    /// <c>MatchScreen</c>); mientras el director devuelva <c>Frozen</c>, la pantalla no debe avanzar ese
    /// reloj y debe seguir pasando el mismo <paramref name="frame"/> en la siguiente llamada.
    /// </summary>
    public DirectorFrame Advance(int frame, double realDelta, int speed)
    {
        if (_awaitingDecision)
        {
            return BuildFrame(_frozenAtFrame, frozen: true);
        }

        _lastSpeed = speed;
        TickStamp(realDelta);
        TickVoiceQueue(realDelta);

        bool wasFrozen = _frozen;
        if (_voice is not null)
        {
            _voiceElapsed += realDelta;
            if (_voiceElapsed >= _voiceDuration)
            {
                FinishVoice();
            }
        }

        if (_frozen)
        {
            return BuildFrame(_frozenAtFrame, frozen: true);
        }

        // La voz que tenía la reproducción congelada acaba de terminar en esta misma llamada: el reloj
        // salta al fotograma del suceso (no al que pasó la pantalla, que se quedó atrás congelado en el
        // anterior) antes de seguir mirando si algún otro momento cae dentro de ese salto.
        int displayFrame = wasFrozen ? Math.Max(frame, _unfreezeToFrame) : frame;
        while (_nextMomentIndex < _moments.Count && _moments[_nextMomentIndex].Frame <= displayFrame)
        {
            var moment = _moments[_nextMomentIndex];
            _nextMomentIndex++;

            if (moment.Decision)
            {
                // La decisión congela, pero no deja la pantalla en blanco (docs/ui/README §4: «si hay
                // decisión, bandeja en la misma pausa»; muerte = bando + bandeja): arranca también su
                // propia presentación, sin duración — se queda hasta que la pantalla llame a Resolve()
                // tras jugar la sustitución elegida, no hasta que expire un cronómetro.
                _awaitingDecision = true;
                _frozen = true;
                _frozenAtFrame = moment.FreezeFrame;
                displayFrame = moment.FreezeFrame;

                // Una decisión retira toda voz que no sea la suya, aunque ella misma vaya al canal de
                // sellos (revisión del orquestador, 19 sep 2026): con la reproducción congelada del todo
                // no tiene sentido que siga en pantalla una voz de un momento anterior («Comienza el
                // partido» junto a la bandeja) — la decisión tiene prioridad narrativa siempre.
                _voiceQueue.Clear();
                _voice = null;
                _voiceElapsed = 0d;

                if (moment.Level <= 2)
                {
                    _stamp = moment;
                    _stampElapsed = 0d;
                    _stampDuration = double.PositiveInfinity;
                }
                else
                {
                    _voice = moment;
                    _voiceDuration = double.PositiveInfinity;
                }

                break;
            }

            var presentation = MatchMomentView.Present(moment, speed);
            if (!presentation.Shown)
            {
                // Residuo sin presentación (RT-014 no aplica: no es una regla del partido, es que a esta
                // velocidad no toca enseñarlo). El estado que deja se sigue leyendo de la traza y del log.
                continue;
            }

            if (moment.Level <= 2)
            {
                _stamp = moment;
                _stampElapsed = 0d;
                _stampDuration = _timings.N2;
                if (moment.Level == 1)
                {
                    _stampDuration = _timings.N1;
                }

                continue;
            }

            if (presentation.Pauses)
            {
                // Una voz que pausa se abre paso: si había otra en curso o en cola, se pierde sin drama —
                // el residuo no lo produce el director (ADR 0119) y la que pausa tiene prioridad narrativa.
                _voiceQueue.Clear();
                StartVoice(moment, presentation, speed);
                _frozen = true;
                _frozenAtFrame = moment.FreezeFrame;
                _unfreezeToFrame = moment.Frame;
                displayFrame = moment.FreezeFrame;
                break;
            }

            if (_voice is null)
            {
                StartVoice(moment, presentation, speed);
            }
            else
            {
                _voiceQueue.Add(new QueuedVoice(moment, 0d));
            }
        }

        return BuildFrame(displayFrame, _frozen);
    }

    private void StartVoice(MatchMoment moment, MomentPresentation presentation, int speed)
    {
        _voice = moment;
        _voiceElapsed = 0d;
        _voiceDuration = VoiceDuration(moment, presentation, speed);
    }

    /// <summary>Termina la voz activa y, si hay cola, arranca la primera que no haya caducado.</summary>
    private void FinishVoice()
    {
        _voice = null;
        _voiceElapsed = 0d;

        // Si la voz que acaba pausaba, la reproducción sigue justo desde el fotograma del suceso (C.2):
        // el motor ya recolocó o retiró al implicado en ese fotograma, así que es ahí donde se reanuda.
        if (_frozen)
        {
            _frozen = false;
        }

        while (_voiceQueue.Count > 0 && _voice is null)
        {
            var next = _voiceQueue[0];
            _voiceQueue.RemoveAt(0);

            // La duración (y si toca enseñarla) se recalcula con _lastSpeed, la velocidad del último
            // Advance: si cambió mientras esperaba en la cola, se presenta con las reglas de ahora, no con
            // las que había cuando se encoló.
            var presentation = MatchMomentView.Present(next.Moment, _lastSpeed);
            if (!presentation.Shown)
            {
                // A la velocidad de ahora ya no toca enseñarla: queda en residuo, como cualquier momento
                // que Present() descarta en el bucle principal, y se prueba la siguiente de la cola.
                continue;
            }

            _voice = next.Moment;
            _voiceElapsed = 0d;
            _voiceDuration = VoiceDuration(next.Moment, presentation, _lastSpeed);
        }
    }

    private void TickStamp(double realDelta)
    {
        if (_stamp is null)
        {
            return;
        }

        _stampElapsed += realDelta;
        if (_stampElapsed >= _stampDuration)
        {
            _stamp = null;
            _stampElapsed = 0d;
        }
    }

    private void TickVoiceQueue(double realDelta)
    {
        for (int i = _voiceQueue.Count - 1; i >= 0; i--)
        {
            var waited = _voiceQueue[i].Waited + realDelta;
            if (waited > _timings.VoiceExpiry)
            {
                _voiceQueue.RemoveAt(i);
            }
            else
            {
                _voiceQueue[i] = _voiceQueue[i] with { Waited = waited };
            }
        }
    }

    /// <summary>
    /// La duración de una voz alta. El nivel 4 manda siempre (revisión del orquestador, 19 sep 2026): un
    /// gol de oro que termina el partido en el mismo momento es, ante todo, el final (N4, la duración más
    /// larga), no un gol corriente — <see cref="ShowRecord"/> ya lo presenta como acta, no como
    /// estandarte. <see cref="MatchMoment.HasGoal"/> (no <see cref="MatchMoment.Kind"/>) solo decide la
    /// duración del gol cuando el nivel no llega a 4: un gol fusionado con una tarjeta del mismo nivel
    /// sigue siendo, ante todo, un gol.
    /// </summary>
    private double VoiceDuration(MatchMoment moment, MomentPresentation presentation, int speed)
    {
        if (moment.Level >= 4)
        {
            return presentation.Compressed
                ? (speed == 16 ? _timings.N4CompressedX16 : _timings.N4CompressedX4)
                : _timings.N4;
        }

        if (moment.HasGoal)
        {
            return presentation.Compressed ? _timings.GoalCompressed : _timings.GoalFreeze;
        }

        return _timings.N3;
    }

    private DirectorFrame BuildFrame(int displayFrame, bool frozen)
    {
        float voiceProgress = _voice is null || _voiceDuration <= 0d
            ? 0f
            : (float)Math.Clamp(_voiceElapsed / _voiceDuration, 0d, 1d);
        float stampProgress = _stamp is null || _stampDuration <= 0d
            ? 0f
            : (float)Math.Clamp(_stampElapsed / _stampDuration, 0d, 1d);
        return new DirectorFrame(frozen, displayFrame, _voice, voiceProgress, _stamp, stampProgress, _awaitingDecision);
    }

    private sealed record QueuedVoice(MatchMoment Moment, double Waited);
}
