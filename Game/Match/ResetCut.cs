using System.Collections.Generic;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Game.Match;

/// <summary>
/// La cortinilla del reinicio tras gol (ADR 0151): cuando la reproducción cruza un
/// <see cref="EventType.TeamsReset"/>, se detiene en el último fotograma de la celebración, funde a negro,
/// cambia al fotograma del reinicio —el motor ya ha colocado a todos— y vuelve.
///
/// <para><b>No decide nada del partido</b> (RT-014): el salto lo hace <c>/Sim</c> y lo anuncia con un
/// evento; esto sólo elige qué fotograma se enseña mientras dura el fundido y cuánto velo hay encima. Es
/// una pausa de presentación, como la congelación del gol del director, y no gasta ticks del motor.</para>
///
/// <para>Sin nodo propio y sin Godot: la pantalla le pasa el fotograma que quiere enseñar y pinta lo que
/// devuelve, así que la misma pieza vale para cualquier pantalla que reproduzca una traza.</para>
/// </summary>
public sealed class ResetCut
{
    /// <summary>
    /// Duraciones a x1, en segundos: <b>provisionales, sin medir</b> (regla H). Salen de la ADR 0151
    /// («muy breve»); se ajustan viéndolo y la cifra final se anota en la ADR.
    /// </summary>
    private const double FadeOutSeconds = 0.15;
    private const double BlackSeconds = 0.10;
    private const double FadeInSeconds = 0.15;

    /// <summary>A esta velocidad o más, corte seco (ADR 0151 punto 7: x16 «ir al resultado»).</summary>
    private const int HardCutSpeed = 16;

    private enum Phase
    {
        Idle,
        Out,
        Black,
        In,
    }

    private readonly List<int> _frames = new();
    private Phase _phase;
    private int _target;
    private double _elapsed;

    /// <summary>Opacidad del velo negro en este instante, de 0 a 1.</summary>
    public float Veil { get; private set; }

    /// <summary>True mientras la cortinilla retiene la reproducción.</summary>
    public bool Active => _phase != Phase.Idle;

    /// <summary>Lee los fotogramas de reinicio del partido. Se vuelve a llamar si el partido se re-simula.</summary>
    public void Bind(IReadOnlyList<MatchEvent>? events, MatchTrace? trace)
    {
        _frames.Clear();
        Reset();
        if (events is null || trace is null)
        {
            return;
        }

        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Type == EventType.TeamsReset)
            {
                _frames.Add(trace.FrameOfTick(events[i].Tick));
            }
        }
    }

    /// <summary>Un salto de la barra o de una captura no es reproducción: se corta cualquier fundido en curso.</summary>
    public void Reset()
    {
        _phase = Phase.Idle;
        _elapsed = 0d;
        Veil = 0f;
    }

    /// <summary>
    /// El fotograma que hay que enseñar, dado el que se enseñaba (<paramref name="current"/>) y al que la
    /// reproducción quiere avanzar (<paramref name="candidate"/>).
    /// </summary>
    public int Filter(int current, int candidate, double delta, int speed)
    {
        if (_phase == Phase.Idle)
        {
            // Se arranca UN FOTOGRAMA ANTES de cruzar: mientras se enseña reset-1, la vista 3D ya interpola
            // hacia reset con el resto del reloj, y quien salta más de 0,6 casillas aparece en su sitio
            // nuevo a mitad de ese fotograma (BA-K). Arrancando al llegar a reset-1 el fundido empieza con
            // la interpolación a cero y nada del reinicio se ve antes del negro (revisión independiente).
            int crossed = FirstResetIn(current, candidate + 1);
            if (crossed < 0)
            {
                return candidate;
            }

            if (speed >= HardCutSpeed)
            {
                return candidate;
            }

            _phase = Phase.Out;
            _target = crossed;
            _elapsed = 0d;
            Veil = 0f;
            return crossed - 1;
        }

        // A x4 dura la cuarta parte (ADR 0151 punto 7).
        _elapsed += delta * speed;
        switch (_phase)
        {
            case Phase.Out:
                if (_elapsed < FadeOutSeconds)
                {
                    Veil = (float)(_elapsed / FadeOutSeconds);
                    return _target - 1;
                }

                _phase = Phase.Black;
                _elapsed = 0d;
                Veil = 1f;
                return _target;

            case Phase.Black:
                Veil = 1f;
                if (_elapsed >= BlackSeconds)
                {
                    _phase = Phase.In;
                    _elapsed = 0d;
                }

                return _target;

            default:
                if (_elapsed < FadeInSeconds)
                {
                    Veil = 1f - (float)(_elapsed / FadeInSeconds);
                    return _target;
                }

                Reset();
                return _target;
        }
    }

    /// <summary>Primer reinicio con <c>current &lt; f &lt;= limit</c>, o -1.</summary>
    private int FirstResetIn(int current, int limit)
    {
        for (int i = 0; i < _frames.Count; i++)
        {
            if (_frames[i] > current && _frames[i] <= limit)
            {
                return _frames[i];
            }
        }

        return -1;
    }
}
