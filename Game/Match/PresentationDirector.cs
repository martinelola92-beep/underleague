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
/// <param name="FateSlowScale">Escala de tiempo de la reproducción durante la cámara lenta de la tirada del destino (ADR 0171): 0,5 = a mitad de velocidad.</param>
/// <param name="Fate">Duración de la voz de la tirada del destino a 1×: la cámara lenta y, tras ella, lo que dura el resultado en pantalla.</param>
/// <param name="FateCompressed">Duración de la voz de la tirada del destino a x4 (sin cámara lenta).</param>
/// <param name="Hold">
/// Pausa breve a 1× ante un suceso que detiene el juego y no tiene congelado propio (BB-D, ADR 0173): falta pitada,
/// tarjeta, lesión que para el partido. <b>Provisional, sin medir</b> (Regla H): menos de la mitad del sello más
/// corto (<see cref="N1"/>), para que el sello siga en pantalla cuando el juego se reanuda; cuesta ~3 pausas por
/// partido, 1,8 s de reloj de pared (1,6 %). BX-17 (9 oct): 0,6 → 1,4 s, el revisor no tenía tiempo de ver quién; sigue
/// por debajo del sello más corto (N1 = 1,6 s), así que el sello aún está en pantalla al reanudarse el juego.
/// </param>
/// <param name="HoldLeadFrames">
/// BX-15: cuántos fotogramas sigue corriendo el partido tras el suceso antes de la pausa breve, para que se vea la
/// entrada y la caída antes del silbato (la caída toca el suelo a 0,03-0,30 s, BV-B). La pantalla lo acorta si algún
/// implicado sale del campo antes (<see cref="PresentationDirector"/>, <c>holdLead</c>). <b>Provisional, sin medir.</b>
/// </param>
/// <param name="FateSpin">BX-16: lo que gira la ruleta del destino a 1×, con el partido congelado. Provisional.</param>
/// <param name="FateResult">BX-16: lo que se queda la ruleta parada enseñando el resultado. Provisional.</param>
public sealed record DirectorTimings(
    double N1,
    double N2,
    double N3,
    double N4,
    double GoalFreeze,
    double GoalCompressed,
    double N4CompressedX4,
    double N4CompressedX16,
    double VoiceExpiry,
    double FateSlowScale,
    double Fate,
    double FateCompressed,
    double Hold,
    int HoldLeadFrames = 6,
    double FateSpin = 2.6,
    double FateResult = 1.6)
{
    /// <summary>
    /// Los valores provisionales del encargo (docs/ui/README.md §4/§6), con los de BX-17 (9 oct): sellos y pausa más
    /// largos porque «pasan cosas demasiado rápido, sale un cartel 1 segundo». Sin medir (Regla H).
    /// </summary>
    public static DirectorTimings Default { get; } = new(
        N1: 1.6,
        N2: 2.2,
        N3: 3.0,
        N4: 4.0,
        GoalFreeze: 2.0,
        GoalCompressed: 1.0,
        N4CompressedX4: 1.5,
        N4CompressedX16: 1.0,
        VoiceExpiry: 1.5,
        FateSlowScale: 0.5,

        // Cámara lenta (los fotogramas de adelanto de la tirada, a 15/s, a la escala de arriba: ~1,07 s reales)
        // más 1,5 s de resultado en pantalla. Provisional, como el resto: es ritmo, no balance (ADR 0171).
        Fate: (MatchMomentView.FateLeadFrames / 15d / 0.5) + 1.5,
        FateCompressed: 1.2,
        Hold: 1.4);
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
/// <param name="TimeScale">
/// Qué fracción de la velocidad normal debe avanzar el reloj de reproducción de la pantalla (1 = normal).
/// Es la cámara lenta de la tirada del destino (ADR 0171); el director sólo la pide, la pantalla la aplica.
/// </param>
/// <param name="Held">
/// El suceso cuya pausa breve tiene la reproducción congelada (BB-D, ADR 0173), o null. Es lo que la pantalla lee
/// para que el residuo —tablero, tiras, residuo del rival— ya refleje el suceso mientras el campo sigue un
/// fotograma por detrás (principio 5), igual que con la voz alta que pausa.
/// </param>
/// <param name="FateSpin">BX-16: 0..1 lo que lleva girada la ruleta del destino con el partido congelado; −1 si no gira.</param>
/// <param name="FateSettled">BX-16: la ruleta ya se paró y enseña el resultado.</param>
public sealed record DirectorFrame(
    bool Frozen,
    int DisplayFrame,
    MatchMoment? Voice,
    float VoiceProgress,
    MatchMoment? Stamp,
    float StampProgress,
    bool AwaitingDecision,
    double TimeScale = 1d,
    MatchMoment? Held = null,
    float FateSpin = -1f,
    bool FateSettled = false);

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
    private readonly Func<MatchMoment, bool>? _stopsPlay;

    private int _nextMomentIndex;

    /// <summary>El fotograma de la última búsqueda (<see cref="Seek"/>): lo anterior a él está pasado, salvo una tirada del destino a medias.</summary>
    private int _seekFrame;

    private bool IsPending(MatchMoment moment) =>
        moment.Frame >= _seekFrame || (moment.Kind == MomentKind.Fate && moment.LastFrame >= _seekFrame)
        || (_holdLead is not null && PresentAt(moment, 1) >= _seekFrame);

    private MatchMoment? _stamp;
    private double _stampElapsed;
    private double _stampDuration;

    private MatchMoment? _voice;
    private double _voiceElapsed;
    private double _voiceDuration;
    private bool _voiceCompressed;
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
    /// La pausa breve en curso (BB-D, ADR 0173): el suceso que la provocó, lo que le queda y los dos fotogramas
    /// entre los que salta al terminar (el anterior al suceso, donde se congela, y el del suceso). Es un
    /// congelado <b>aparte</b> del de la voz que pausa (<see cref="_frozen"/>): no es una voz, no ocupa el
    /// escenario, y por eso no compite con ninguna.
    /// </summary>
    private MatchMoment? _held;

    private double _holdLeft;
    private int _holdAtFrame;
    private int _holdToFrame;

    /// <summary>
    /// La velocidad del último <see cref="Advance"/> que llegó a procesar momentos (revisión del
    /// orquestador, 19 sep 2026): <see cref="FinishVoice"/> la necesita para volver a evaluar la cola con
    /// la velocidad de <b>ahora</b>, no con la que había cuando cada una se encoló.
    /// </summary>
    private int _lastSpeed = 1;

    /// <summary>BX-15: fotogramas de margen tras el suceso antes de su pausa, por momento (0 si no se adelanta nada).</summary>
    private readonly Func<MatchMoment, int>? _holdLead;

    /// <summary>BX-16: la ruleta del destino en curso (partido congelado en el fotograma anterior a la tirada).</summary>
    private bool _fateHolding;

    private double _fateElapsed;
    private int _fateFrame;

    /// <param name="stopsPlay">
    /// Si un suceso detiene el juego de verdad (<see cref="PlayStops.Holds"/>): lo decide la pantalla, que tiene la
    /// traza, y el director sólo la pregunta. Null = ninguna pausa breve (el director de antes).
    /// </param>
    /// <param name="holdLead">
    /// BX-15: cuántos fotogramas, como mucho <see cref="DirectorTimings.HoldLeadFrames"/>, puede seguir corriendo el
    /// partido tras un suceso que lo detiene antes de presentarlo, sin que ninguno de sus implicados haya salido del
    /// campo (un lesionado grave desaparece en el tick del suceso). Lo decide la pantalla, que tiene la traza.
    /// Null = sin margen: la pausa en el fotograma anterior al suceso, como en la ADR 0173.
    /// </param>
    public PresentationDirector(
        IReadOnlyList<MatchMoment> moments, DirectorTimings timings, Func<MatchMoment, bool>? stopsPlay = null, Func<MatchMoment, int>? holdLead = null)
    {
        _moments = moments ?? throw new ArgumentNullException(nameof(moments));
        _timings = timings ?? throw new ArgumentNullException(nameof(timings));
        _stopsPlay = stopsPlay;
        _holdLead = holdLead;
    }

    /// <summary>
    /// BX-15: el fotograma en que se presenta un momento a esta velocidad. Un suceso que detiene el juego, a 1×, se
    /// presenta unos fotogramas después de ocurrir —se ve la entrada y la caída, y después pitan—; todo lo demás,
    /// en su fotograma.
    /// </summary>
    private int PresentAt(MatchMoment moment, int speed)
    {
        if (speed != 1 || _holdLead is null || _stopsPlay is null || moment.Decision || !_stopsPlay(moment))
        {
            return moment.Frame;
        }

        int lead = Math.Clamp(_holdLead(moment), 0, _timings.HoldLeadFrames);

        // El margen nunca pasa por encima del momento siguiente: si otro suceso cae dentro, la pausa se presenta antes
        // que él (revisión independiente: una decisión o una voz que pausa detrás congelaría por detrás de la pausa).
        int index = IndexOf(moment);
        for (int next = index + 1; next >= 1 && next < _moments.Count; next++)
        {
            if (_moments[next].Frame > moment.Frame)
            {
                lead = Math.Min(lead, Math.Max(_moments[next].Frame - moment.Frame - 1, 0));
                break;
            }
        }

        return moment.Frame + lead;
    }

    private int IndexOf(MatchMoment moment)
    {
        for (int i = 0; i < _moments.Count; i++)
        {
            if (ReferenceEquals(_moments[i], moment))
            {
                return i;
            }
        }

        return -1;
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
        _held = null;
        _holdLeft = 0d;
        _holdToFrame = 0;
        _fateHolding = false;
        _fateElapsed = 0d;

        // ADR 0171: una tirada del destino que ya ha empezado (su Frame es 8 fotogramas anterior al de la tirada)
        // sigue viva si la búsqueda cae dentro de ella —típicamente, la decisión de sustitución de una lesión
        // reanuda en el fotograma siguiente al suyo—; se cuenta desde su fotograma de la tirada, no desde el de arranque.
        _seekFrame = frame;
        int index = 0;
        while (index < _moments.Count && !IsPending(_moments[index]))
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
        _held = null;
        _holdLeft = 0d;
        _holdToFrame = 0;
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

        // BX-16: la ruleta del destino gira con el partido congelado; la voz termina cuando la ruleta ha enseñado el
        // resultado, no por su reloj (que contaba la cámara lenta de antes). Cambiar de velocidad la deja terminar ya.
        if (_fateHolding)
        {
            _fateElapsed += realDelta;
            if (speed == 1 && _fateElapsed < _timings.FateSpin + _timings.FateResult)
            {
                return BuildFrame(_fateFrame, frozen: true);
            }

            // Al terminar se salta al fotograma de los dados, no al anterior: con el resultado ya enseñado, ninguna
            // orden puede caer antes de la tirada y volverla a tirar (ADR 0183, revisión independiente).
            _fateHolding = false;
            _voiceElapsed = _voiceDuration;
            frame = Math.Max(frame, _fateFrame + 1);
        }

        if (_voice is not null)
        {
            _voiceElapsed += realDelta;
            if (_voiceElapsed >= _voiceDuration && !(_voice.Kind == MomentKind.Fate && FateWheelPending(frame)))
            {
                FinishVoice();
            }
        }

        if (_frozen)
        {
            return BuildFrame(_frozenAtFrame, frozen: true);
        }

        // BX-16: la cámara lenta de la tirada llega al fotograma anterior a los dados y ahí se congela la ruleta.
        if (FateWheelPending(frame) && _voice is { } fateVoice)
        {
            _fateHolding = true;
            _fateWheelFor = fateVoice;
            _fateElapsed = 0d;
            _fateFrame = Math.Max(fateVoice.LastFrame - 1, 0);
            return BuildFrame(_fateFrame, frozen: true);
        }

        // La pausa breve (BB-D, ADR 0173): mientras dure, el campo se queda en el fotograma anterior al suceso.
        // Cambiar de velocidad la corta en el acto: a x4 y x16 la presentación se degrada (docs/ui/README §6) y
        // no se queda nadie mirando un fotograma fijo.
        bool holdEnded = false;
        if (_held is not null)
        {
            _holdLeft -= realDelta;
            if (speed == 1 && _holdLeft > 0d)
            {
                return BuildFrame(_holdAtFrame, frozen: true);
            }

            holdEnded = true;
            _held = null;
        }

        // La voz que tenía la reproducción congelada (o la pausa breve) acaba de terminar en esta misma
        // llamada: el reloj salta al fotograma del suceso (no al que pasó la pantalla, que se quedó atrás
        // congelado en el anterior) antes de seguir mirando si algún otro momento cae dentro de ese salto.
        int displayFrame = wasFrozen
            ? Math.Max(frame, _unfreezeToFrame)
            : holdEnded ? Math.Max(frame, _holdToFrame) : frame;
        while (_nextMomentIndex < _moments.Count && PresentAt(_moments[_nextMomentIndex], speed) <= displayFrame)
        {
            var moment = _moments[_nextMomentIndex];
            _nextMomentIndex++;
            if (!IsPending(moment))
            {
                continue;
            }

            // BX-16: una entrada puede tirar dos dados al mismo jugador en el mismo tick (muerte y grave), cada uno su
            // momento. La ruleta de la primera ya cuenta la decisiva (la pantalla la elige entre las del tick), así que
            // la segunda queda en residuo en vez de esperar en la cola y caducar.
            if (moment.Kind == MomentKind.Fate && IsSameRoll(moment))
            {
                continue;
            }

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

                if (StartHold(moment, speed))
                {
                    displayFrame = _holdAtFrame;
                    break;
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

            bool stageFree = _voice is null && _voiceQueue.Count == 0;
            if (_voice is null)
            {
                StartVoice(moment, presentation, speed);
            }
            else
            {
                _voiceQueue.Add(new QueuedVoice(moment, 0d));
            }

            // Una tarjeta roja (N3) es una voz alta que no congela sola: su pausa breve la da el escenario
            // libre. Con otra voz en pantalla o en cola no hay pausa: una sola voz alta a la vez.
            if (stageFree && StartHold(moment, speed, ownVoice: true))
            {
                displayFrame = _holdAtFrame;
                break;
            }
        }

        return BuildFrame(displayFrame, _frozen || _held is not null);
    }

    /// <summary>
    /// Abre la pausa breve de este suceso si toca (BB-D, ADR 0173): a 1x, con la duracion configurada, si la
    /// pantalla dice que el juego se ha detenido de verdad, y sin ninguna voz alta en el escenario (una sola voz
    /// alta a la vez, docs/ui/README §2). No la abre ninguna presentacion que ya congele por si sola (gol,
    /// muerte, final, decision): esas no pasan por aqui. <paramref name="ownVoice"/>: la voz que hay en el
    /// escenario es la del propio suceso, que acaba de arrancar.
    /// </summary>
    private bool StartHold(MatchMoment moment, int speed, bool ownVoice = false)
    {
        bool stageBusy = _voice is not null && !ownVoice;

        // BX-15: con margen, la pausa cae DESPUÉS del suceso (la caída ya se ha visto) y la reproducción sigue desde
        // ese mismo fotograma; sin margen, en el anterior al suceso y salta al suyo, como en la ADR 0173.
        int at = PresentAt(moment, speed);
        int freeze = at > moment.Frame ? at : moment.FreezeFrame;
        int resume = at > moment.Frame ? at : moment.Frame;

        // Dos pausas encadenadas no hacen retroceder la imagen: si el fotograma de congelado de esta queda atrás
        // del fotograma al que la anterior acaba de saltar (_holdToFrame), no hay pausa.
        if (_stopsPlay is null || speed != 1 || _timings.Hold <= 0d || stageBusy || freeze < _holdToFrame
            || !_stopsPlay(moment))
        {
            return false;
        }

        _held = moment;
        _holdLeft = _timings.Hold;
        _holdAtFrame = freeze;
        _holdToFrame = resume;
        return true;
    }

    private void StartVoice(MatchMoment moment, MomentPresentation presentation, int speed)
    {
        _voice = moment;
        _voiceElapsed = 0d;
        _voiceCompressed = presentation.Compressed;
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
            _voiceCompressed = presentation.Compressed;
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
        // ADR 0171: la tirada del destino tiene su propio ritmo (cámara lenta + resultado); nunca es N4 ni gol.
        if (moment.Kind == MomentKind.Fate)
        {
            return presentation.Compressed ? _timings.FateCompressed : _timings.Fate;
        }

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

    /// <summary>
    /// BX-16: la voz es una tirada del destino presentada entera (1×, sin comprimir), aún no ha girado su ruleta y la
    /// reproducción ya ha llegado al fotograma anterior a los dados.
    /// </summary>
    private bool FateWheelPending(int frame) =>
        !_fateHolding && _lastSpeed == 1 && _voice is { Kind: MomentKind.Fate } fate && !_voiceCompressed
        && _fateWheelFor != fate && frame >= fate.LastFrame - 1 && frame <= fate.LastFrame;

    /// <summary>La tirada cuya ruleta ya giró: cada tirada gira una vez.</summary>
    private MatchMoment? _fateWheelFor;

    private bool IsSameRoll(MatchMoment fate)
    {
        bool Same(MatchMoment? other) => other is { Kind: MomentKind.Fate } o && !ReferenceEquals(o, fate)
            && o.LastFrame == fate.LastFrame && o.LeadPlayerId == fate.LeadPlayerId && o.Team == fate.Team;
        return Same(_voice) || Same(_fateWheelFor);
    }

    private DirectorFrame BuildFrame(int displayFrame, bool frozen)
    {
        float voiceProgress = _voice is null || _voiceDuration <= 0d
            ? 0f
            : (float)Math.Clamp(_voiceElapsed / _voiceDuration, 0d, 1d);
        float stampProgress = _stamp is null || _stampDuration <= 0d
            ? 0f
            : (float)Math.Clamp(_stampElapsed / _stampDuration, 0d, 1d);

        // ADR 0171: la cámara lenta dura hasta el fotograma de la tirada, y sólo si la voz de la tirada se
        // presenta completa (a x4 va comprimida y no frena nada). Congelado no hay reloj que escalar.
        double timeScale = !frozen && _lastSpeed == 1 && _voice is { Kind: MomentKind.Fate } fate && !_voiceCompressed && displayFrame < fate.LastFrame
            ? _timings.FateSlowScale
            : 1d;
        float fateSpin = _fateHolding ? (float)Math.Clamp(_fateElapsed / _timings.FateSpin, 0d, 1d) : -1f;
        bool fateSettled = _fateHolding && _fateElapsed >= _timings.FateSpin;
        return new DirectorFrame(frozen, displayFrame, _voice, voiceProgress, _stamp, stampProgress, _awaitingDecision, timeScale, _held, fateSpin, fateSettled);
    }

    private sealed record QueuedVoice(MatchMoment Moment, double Waited);
}
