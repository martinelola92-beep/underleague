using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Analysis.Detectors;

/// <summary>Un caso detectado: dónde empieza, cuánto dura y de qué tamaño es.</summary>
/// <param name="Frame">Fotograma de inicio (la traza tiene uno por tick).</param>
/// <param name="Tick">Tick del motor de ese fotograma.</param>
/// <param name="Length">Fotogramas que dura el episodio (1 si es puntual).</param>
/// <param name="Magnitude">La medida que ordena los casos: «el peor» es el de mayor magnitud.</param>
/// <param name="Note">Quién y qué, para localizarlo en la hoja de contacto.</param>
internal sealed record Hit(int Frame, int Tick, int Length, double Magnitude, string Note);

/// <summary>
/// Un detector por síntoma notificado por el revisor, todos sobre la <b>traza de /Sim</b> (RT-098): baratos,
/// deterministas y sin Godot. Cada uno nace de la ficha de <c>docs/pendientes/</c> que cita y reutiliza la
/// definición que esa ficha midió cuando la tenía; los umbrales que no vienen de una medición llevan la
/// etiqueta <b>provisional, sin medir</b> (Regla H). Se validan en <c>SymptomDetectorsValidationTests</c>
/// contra casos de respuesta conocida (Regla J) antes de creerse ninguna cifra del barrido.
/// </summary>
internal static class SymptomDetectors
{
    // ---- Umbrales. Procedencia en el comentario de cada uno (Regla H). ----

    /// <summary>BB-K, ficha: «racha de ≥ 4 inversiones de rumbo consecutivas». Medido (2000 partidos).</summary>
    public const int DanceMinReversals = 4;

    /// <summary>BB-K, ficha: ambos módulos de desplazamiento por fotograma &gt; 0,02 casillas. Medido.</summary>
    public const float DanceMinStep = 0.02f;

    /// <summary>BB-K, ficha: 98,3 % de los episodios tenían un compañero a menos de 1 casilla. Medido.</summary>
    public const float DanceMateRadius = 1.0f;

    /// <summary>BC-G, ficha: «balón suelto y quieto ≥ 15 ticks en juego abierto». Medido (1000 partidos).</summary>
    public const int LooseBallMinTicks = 15;

    /// <summary>BC-G: el balón «quieto» se mueve menos que esto por tick. Provisional, sin medir.</summary>
    public const float LooseBallStill = 0.02f;

    /// <summary>BO-A, ficha: «tramos de más de 3 s» = 45 ticks, rival a menos de 1 casilla. Medido (200 partidos).</summary>
    public const int StuckMinTicks = 45;

    public const float StuckRadius = 1.0f;

    /// <summary>
    /// BB-A / BB-L: salto de un jugador entre dos ticks. BA-K fijó 0,6 casillas como corte de «salto» en el
    /// render; el paso máximo legítimo es ~0,16 casillas/tick (BV-A H4), así que 0,6 es 4× el paso.
    /// </summary>
    public const float JumpCells = 0.6f;

    /// <summary>BH-A: ventana sin balón en movimiento en juego abierto. 150 ticks = 10 s. Provisional, sin medir.</summary>
    public const int FreezeMinTicks = 150;

    public const float FreezeBallRange = 1.0f;

    /// <summary>BA-E, ficha: «sin ángulo» = apertura &lt; 0,5 (<c>ApertureCenti</c> &lt; 50). Medido.</summary>
    public const float LowAperture = 0.5f;

    /// <summary>BN-A: compañeros a menos de 2 casillas del portero con balón; ficha: «≥ 2 a &lt; 2». Medido.</summary>
    public const float GoalkeeperCrowdRadius = 2.0f;

    public const int GoalkeeperCrowdMates = 2;

    /// <summary>BG-G2: balón suelto sin que ningún compañero de campo lo persiga durante ≥ 15 ticks (= BC-G).</summary>
    public const int EmbraceMinTicks = LooseBallMinTicks;

    /// <summary>BA-J: compañeros de campo no delanteros aún en campo contrario al soltar el portero. Provisional, sin medir.</summary>
    public const int RetreatMaxStragglers = 1;

    /// <summary>
    /// BF-C: alcance de una entrada. Medido en <c>RestartClearanceTests</c> (BB-B): «max(tackleDistanceMaxCells,
    /// blockReachMaxCells) + margen = 1,3 casillas».
    /// </summary>
    public const float TackleReach = 1.3f;

    /// <summary>BB-C: la celebración debe darse en campo contrario o a la altura del tiro; provisional, sin medir.</summary>
    public const int CelebrationProbeTicks = 10;

    // ----------------------------------------------------------------------------------------------------

    /// <summary>BB-K: dos compañeros «bailan» — racha de inversiones de rumbo con un compañero encima.</summary>
    public static IReadOnlyList<Hit> Dance(DetectorTrace t)
    {
        var hits = new List<Hit>();
        for (int p = 0; p < t.Players; p++)
        {
            int run = 0;
            int runStart = 0;
            for (int f = 2; f <= t.Frames; f++)
            {
                bool reversal = false;
                if (f < t.Frames && t.On(f, p) && t.On(f - 1, p) && t.On(f - 2, p))
                {
                    var a = Sub(t.Pos(f - 1, p), t.Pos(f - 2, p));
                    var b = Sub(t.Pos(f, p), t.Pos(f - 1, p));
                    reversal = Len(a) > DanceMinStep && Len(b) > DanceMinStep && Dot(a, b) < 0f;
                }

                if (reversal)
                {
                    if (run == 0)
                    {
                        runStart = f;
                    }

                    run++;
                    continue;
                }

                if (run >= DanceMinReversals && NearestMate(t, runStart, p) < DanceMateRadius)
                {
                    hits.Add(new Hit(runStart, t.Tick[runStart], run, run, $"jugador {t.Id[p]}, acción {ActionName(t, runStart, p)}"));
                }

                run = 0;
            }
        }

        return hits;
    }

    /// <summary>BC-G: balón suelto y quieto en juego abierto durante ≥ 15 ticks.</summary>
    public static IReadOnlyList<Hit> LooseBall(DetectorTrace t)
    {
        var hits = new List<Hit>();
        int run = 0;
        int start = 0;
        for (int f = 1; f <= t.Frames; f++)
        {
            bool still = f < t.Frames && t.Owner[f] < 0 && !t.Flight[f] && t.Phase[f] == MatchPhase.OpenPlay
                && t.Restart[f] == RestartKind.None && Len(Sub(t.Ball(f), t.Ball(f - 1))) < LooseBallStill;
            if (still)
            {
                if (run == 0)
                {
                    start = f;
                }

                run++;
                continue;
            }

            if (run >= LooseBallMinTicks)
            {
                var b = t.Ball(start);
                hits.Add(new Hit(start, t.Tick[start], run, run, $"balón en ({b.X:0.0},{b.Y:0.0})"));
            }

            run = 0;
        }

        return hits;
    }

    /// <summary>
    /// BA-J: al soltar el portero un balón atrapado, ¿cuántos compañeros de campo del que tiró, que no sean
    /// delanteros, siguen en campo contrario? (la ficha deja al delantero presionando y pide replegar al resto).
    /// </summary>
    public static IReadOnlyList<Hit> NoRetreatAfterSave(DetectorTrace t, out List<double> stragglersPerSave)
    {
        stragglersPerSave = new List<double>();
        var hits = new List<Hit>();
        foreach (var e in t.Events)
        {
            if (e.Type != EventType.Save || e.Detail != "held")
            {
                continue;
            }

            int f0 = t.FrameOfTick(e.Tick);
            int gk = t.Owner[f0];
            if (gk < 0 || t.Role[gk] != Position.Goalkeeper)
            {
                continue;
            }

            int release = f0;
            while (release < t.Frames - 1 && t.Owner[release] == gk && t.Phase[release] != MatchPhase.Finished)
            {
                release++;
            }

            int shooterTeam = 1 - t.Team[gk];
            int dir = Pitch.AttackDirection(shooterTeam);
            int stragglers = 0;
            for (int p = 0; p < t.Players; p++)
            {
                if (t.Team[p] != shooterTeam || t.Role[p] is Position.Goalkeeper or Position.Forward || !t.On(release, p))
                {
                    continue;
                }

                float x = t.X[t.Slot(release, p)];
                float attackX = dir > 0 ? x : Pitch.Columns - x;
                if (attackX > Pitch.Columns / 2f)
                {
                    stragglers++;
                }
            }

            stragglersPerSave.Add(stragglers);
            if (stragglers > RetreatMaxStragglers)
            {
                hits.Add(new Hit(release, t.Tick[release], release - f0, stragglers, $"parada del {t.Id[gk]}, {stragglers} sin replegar"));
            }
        }

        return hits;
    }

    /// <summary>BF-C: placaje sin balón (<c>offBall*</c>) de un delantero.</summary>
    public static IReadOnlyList<Hit> ForwardOffBallHit(DetectorTrace t, out int allOffBall)
    {
        allOffBall = 0;
        var hits = new List<Hit>();
        foreach (var e in t.Events)
        {
            if (e.Type != EventType.Tackle || !e.Detail.StartsWith("offBall", StringComparison.Ordinal))
            {
                continue;
            }

            allOffBall++;
            int p = t.IndexOfId(e.Actor);
            if (p >= 0 && t.Role[p] == Position.Forward)
            {
                hits.Add(new Hit(t.FrameOfTick(e.Tick), e.Tick, 1, 1, $"delantero {e.Actor} sobre {e.Opponent} ({e.Detail})"));
            }
        }

        return hits;
    }

    /// <summary>
    /// BF-C, la forma que se ve en la decisión: un delantero elige <c>Block</c>, o <c>Tackle</c> sin que ningún
    /// rival lleve el balón: «pega» a quien no lleva el balón.
    /// Un episodio es una racha de ticks consecutivos. Existe porque el placaje sin balón del delantero está
    /// cerrado a propósito (ADR 0133) y los eventos <c>offBall*</c> dan 0 siempre: el síntoma vive en la elección.
    /// </summary>
    public static IReadOnlyList<Hit> ForwardTackleChoice(DetectorTrace t, out int forwardChoiceFrames)
    {
        forwardChoiceFrames = 0;
        var hits = new List<Hit>();
        for (int p = 0; p < t.Players; p++)
        {
            if (t.Role[p] != Position.Forward)
            {
                continue;
            }

            int run = 0;
            int start = 0;
            for (int f = 0; f <= t.Frames; f++)
            {
                bool choice = false;
                if (f < t.Frames && t.On(f, p) && t.Phase[f] == MatchPhase.OpenPlay)
                {
                    int action = t.Action[t.Slot(f, p)];
                    if (action == (int)PlayerAction.Tackle || action == (int)PlayerAction.Block)
                    {
                        // «Pega sin balón»: Block (derribar a quien no lleva el balón) o Tackle sin que un
                        // rival lleve el balón (lo tiene un compañero, o está suelto): no hay portador al que entrar.
                        int owner = t.Owner[f];
                        bool rivalCarrier = owner >= 0 && t.Team[owner] != t.Team[p];
                        choice = action == (int)PlayerAction.Block || !rivalCarrier;
                    }
                }

                if (choice)
                {
                    forwardChoiceFrames++;
                    if (run == 0)
                    {
                        start = f;
                    }

                    run++;
                    continue;
                }

                if (run > 0)
                {
                    hits.Add(new Hit(start, t.Tick[start], run, run, $"delantero {t.Id[p]} elige pegar sin portador a su alcance"));
                }

                run = 0;
            }
        }

        return hits;
    }

    /// <summary>
    /// BB-G2: el portero hace de perseguidor fuera de su área. Dos formas: (a) elige <c>ChaseBall</c> con el
    /// balón fuera de su área; (b) «abrazo mortal», balón suelto y quieto fuera del área, el portero es el
    /// compañero más cercano y ningún compañero de campo lo persigue durante ≥ 15 ticks.
    /// </summary>
    public static IReadOnlyList<Hit> GoalkeeperChaser(DetectorTrace t, out int chaseFrames)
    {
        chaseFrames = 0;
        var hits = new List<Hit>();
        int[] gks = Enumerable.Range(0, t.Players).Where(p => t.Role[p] == Position.Goalkeeper).ToArray();

        foreach (int gk in gks)
        {
            for (int f = 0; f < t.Frames; f++)
            {
                if (t.On(f, gk) && t.Action[t.Slot(f, gk)] == (int)PlayerAction.ChaseBall
                    && !Pitch.IsInArea(t.Ball(f), t.Team[gk]) && t.Phase[f] == MatchPhase.OpenPlay)
                {
                    chaseFrames++;
                }
            }

            int run = 0;
            int start = 0;
            for (int f = 1; f <= t.Frames; f++)
            {
                bool embrace = false;
                if (f < t.Frames && t.Owner[f] < 0 && !t.Flight[f] && t.Phase[f] == MatchPhase.OpenPlay && t.Restart[f] == RestartKind.None
                    && Len(Sub(t.Ball(f), t.Ball(f - 1))) < LooseBallStill && t.On(f, gk) && !Pitch.IsInArea(t.Ball(f), t.Team[gk]))
                {
                    var ball = t.Ball(f);
                    float gkDist = Vec2.Distance(t.Pos(f, gk), ball);
                    bool nearest = true;
                    bool anyChasing = false;
                    for (int p = 0; p < t.Players; p++)
                    {
                        if (p == gk || t.Team[p] != t.Team[gk] || !t.On(f, p))
                        {
                            continue;
                        }

                        if (Vec2.Distance(t.Pos(f, p), ball) < gkDist)
                        {
                            nearest = false;
                        }

                        if (t.Action[t.Slot(f, p)] == (int)PlayerAction.ChaseBall)
                        {
                            anyChasing = true;
                        }
                    }

                    embrace = nearest && !anyChasing;
                }

                if (embrace)
                {
                    if (run == 0)
                    {
                        start = f;
                    }

                    run++;
                    continue;
                }

                if (run >= EmbraceMinTicks)
                {
                    hits.Add(new Hit(start, t.Tick[start], run, run, $"portero {t.Id[gk]} cercano, nadie del equipo persigue"));
                }

                run = 0;
            }
        }

        return hits;
    }

    /// <summary>BN-A: ≥ 2 compañeros a menos de 2 casillas del portero mientras tiene el balón.</summary>
    public static IReadOnlyList<Hit> GoalkeeperCrowd(DetectorTrace t)
    {
        var hits = new List<Hit>();
        int f = 0;
        while (f < t.Frames)
        {
            int gk = t.Owner[f];
            if (gk < 0 || t.Role[gk] != Position.Goalkeeper)
            {
                f++;
                continue;
            }

            int start = f;
            int maxMates = 0;
            while (f < t.Frames && t.Owner[f] == gk)
            {
                int mates = 0;
                for (int p = 0; p < t.Players; p++)
                {
                    if (p != gk && t.Team[p] == t.Team[gk] && t.On(f, p) && Vec2.Distance(t.Pos(f, p), t.Pos(f, gk)) < GoalkeeperCrowdRadius)
                    {
                        mates++;
                    }
                }

                maxMates = Math.Max(maxMates, mates);
                f++;
            }

            if (maxMates >= GoalkeeperCrowdMates)
            {
                string context = t.Restart[start] != RestartKind.None ? t.Restart[start].ToString() : "juego abierto";
                hits.Add(new Hit(start, t.Tick[start], f - start, maxMates, $"portero {t.Id[gk]}, {maxMates} compañeros a <2 ({context})"));
            }
        }

        return hits;
    }

    /// <summary>BO-A: mismo portador y mismo rival a menos de 1 casilla durante &gt; 3 s.</summary>
    public static IReadOnlyList<Hit> CarrierStuck(DetectorTrace t)
    {
        var hits = new List<Hit>();
        int carrier = -1;
        int rival = -1;
        int run = 0;
        int start = 0;
        for (int f = 0; f <= t.Frames; f++)
        {
            int c = -1;
            int r = -1;
            if (f < t.Frames && t.Owner[f] >= 0 && t.Phase[f] == MatchPhase.OpenPlay)
            {
                c = t.Owner[f];
                float best = StuckRadius;
                for (int p = 0; p < t.Players; p++)
                {
                    if (t.Team[p] != t.Team[c] && t.On(f, p) && Vec2.Distance(t.Pos(f, p), t.Pos(f, c)) < best)
                    {
                        best = Vec2.Distance(t.Pos(f, p), t.Pos(f, c));
                        r = p;
                    }
                }
            }

            if (c >= 0 && r >= 0 && c == carrier && r == rival)
            {
                run++;
                continue;
            }

            if (run > StuckMinTicks)
            {
                hits.Add(new Hit(start, t.Tick[start], run, run, $"portador {t.Id[carrier]} y rival {t.Id[rival]}"));
            }

            carrier = c;
            rival = r;
            run = (c >= 0 && r >= 0) ? 1 : 0;
            start = f;
        }

        return hits;
    }

    /// <summary>
    /// BB-A / BB-L: saltos de más de 0,6 casillas de un jugador en el campo entre dos ticks, separando los que
    /// el motor hace a propósito (reposición en un <c>TeamsReset</c>, el sacador que se coloca sobre el balón)
    /// de los que nadie explica. Devuelve sólo los inexplicados; <paramref name="explained"/> cuenta los otros
    /// (es el control positivo: una traza real DEBE tener algunos) y <paramref name="restartJumps"/> los que ocurren
    /// con una reanudación en marcha (el motor aparta o coloca gente a propósito).
    /// </summary>
    public static IReadOnlyList<Hit> Teleports(DetectorTrace t, out int explained, out int leavingJumps, out int restartJumps)
    {
        explained = 0;
        restartJumps = 0;
        leavingJumps = 0;
        var hits = new List<Hit>();
        var resetTicks = new HashSet<int>();
        foreach (var e in t.Events)
        {
            if (e.Type is EventType.TeamsReset or EventType.MatchStart or EventType.Substitution)
            {
                resetTicks.Add(e.Tick);
            }
        }

        for (int f = 1; f < t.Frames; f++)
        {
            for (int p = 0; p < t.Players; p++)
            {
                if (!t.On(f - 1, p))
                {
                    continue;
                }

                bool stays = t.On(f, p);
                float jump = Vec2.Distance(t.Pos(f - 1, p), t.Pos(f, p));
                if (!stays)
                {
                    continue;
                }

                if (jump <= JumpCells)
                {
                    // BB-L: el último paso antes de abandonar el campo no debe ser un salto.
                    continue;
                }

                bool reset = resetTicks.Contains(t.Tick[f]) || t.Phase[f] == MatchPhase.Kickoff || t.Phase[f - 1] == MatchPhase.Kickoff
                    || t.Phase[f] == MatchPhase.Penalty || t.Phase[f - 1] == MatchPhase.Penalty;
                bool taker = t.Taker[f] == p || t.Taker[f - 1] == p;
                if (reset || taker)
                {
                    explained++;
                    continue;
                }

                // Durante una reanudación el motor aparta a los jugadores del balón (barrera, ADR 0090/0115) y
                // pone al sacador sobre él: movimiento a propósito, aunque de más de 0,6 casillas en un tick.
                if (t.Restart[f] != RestartKind.None || t.Restart[f - 1] != RestartKind.None)
                {
                    restartJumps++;
                    continue;
                }

                hits.Add(new Hit(f, t.Tick[f], 1, jump, $"jugador {t.Id[p]} salta {jump:0.00} casillas ({t.State[t.Slot(f, p)]})"));
            }

            // BB-L en la traza: el que abandona el campo (lesión) no debe moverse >0,6 en su último tick.
            for (int p = 0; p < t.Players; p++)
            {
                if (t.On(f - 1, p) && !t.On(f, p) && f >= 2 && t.On(f - 2, p)
                    && Vec2.Distance(t.Pos(f - 2, p), t.Pos(f - 1, p)) > JumpCells)
                {
                    leavingJumps++;
                    hits.Add(new Hit(f - 1, t.Tick[f - 1], 1, Vec2.Distance(t.Pos(f - 2, p), t.Pos(f - 1, p)),
                        $"jugador {t.Id[p]} sale del campo tras un salto"));
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// BB-B: el saque de centro (y, aparte, cualquier reanudación) lo roba un rival antes de que salga el
    /// balón: durante la ventana de reanudación el dueño del balón es un rival del sacador, o hay un
    /// <c>Tackle</c> contra él. <paramref name="kickoffOnly"/> restringe al saque de centro.
    /// </summary>
    public static IReadOnlyList<Hit> StealBeforeRestart(DetectorTrace t, bool kickoffOnly)
    {
        var hits = new List<Hit>();
        for (int f = 0; f < t.Frames; f++)
        {
            var kind = t.Restart[f];
            int taker = t.Taker[f];
            if (kind == RestartKind.None || taker < 0 || kind == RestartKind.Penalty || (kickoffOnly && kind != RestartKind.Kickoff))
            {
                continue;
            }

            int owner = t.Owner[f];
            if (owner >= 0 && t.Team[owner] != t.Team[taker])
            {
                hits.Add(new Hit(f, t.Tick[f], 1, 1, $"{kind}: el balón del sacador {t.Id[taker]} es de {t.Id[owner]}"));
                // Un solo caso por ventana: salta hasta que el sacador cambie.
                while (f + 1 < t.Frames && t.Restart[f + 1] == kind && t.Taker[f + 1] == taker)
                {
                    f++;
                }
            }
        }

        foreach (var e in t.Events)
        {
            if (e.Type != EventType.Tackle || e.Detail.StartsWith("offBall", StringComparison.Ordinal))
            {
                continue;
            }

            int f = t.FrameOfTick(e.Tick);
            int taker = t.Taker[f];
            if (t.Restart[f] == RestartKind.None || taker < 0 || t.Restart[f] == RestartKind.Penalty
                || (kickoffOnly && t.Restart[f] != RestartKind.Kickoff) || e.Opponent != t.Id[taker])
            {
                continue;
            }

            // La entrada que PITA la falta abre la ventana de reanudación en su mismo tick: es la causa del saque,
            // no un robo del saque. Sólo cuenta una entrada con la ventana ya abierta en el tick anterior.
            if (f == 0 || t.Restart[f - 1] != t.Restart[f] || t.Taker[f - 1] != taker)
            {
                continue;
            }

            hits.Add(new Hit(f, e.Tick, 1, 1, $"{t.Restart[f]}: entrada {e.Detail} de {e.Actor} al sacador"));
        }

        return hits;
    }

    /// <summary>
    /// BB-C: la celebración del goleador (a) no salta de posición y (b) transcurre donde marcó, no en su
    /// campo. Devuelve los casos de (a) y (b) con la magnitud en casillas.
    /// </summary>
    public static IReadOnlyList<Hit> Celebration(DetectorTrace t)
    {
        var hits = new List<Hit>();
        foreach (var e in t.Events)
        {
            if (e.Type != EventType.Goal)
            {
                continue;
            }

            int p = t.IndexOfId(e.Actor);
            if (p < 0)
            {
                continue;
            }

            int f0 = t.FrameOfTick(e.Tick);
            int dir = Pitch.AttackDirection(t.Team[p]);
            int celebrating = 0;
            for (int f = f0 + 1; f < Math.Min(t.Frames, f0 + 1 + CelebrationProbeTicks); f++)
            {
                if (t.State[t.Slot(f, p)] != PlayerState.Celebrating)
                {
                    continue;
                }

                celebrating++;
                float jump = Vec2.Distance(t.Pos(f - 1, p), t.Pos(f, p));
                if (jump > JumpCells)
                {
                    hits.Add(new Hit(f, t.Tick[f], 1, jump, $"goleador {e.Actor} celebrando salta {jump:0.00}"));
                    break;
                }

                float attackX = dir > 0 ? t.X[t.Slot(f, p)] : Pitch.Columns - t.X[t.Slot(f, p)];
                if (celebrating == CelebrationProbeTicks / 2 && attackX < Pitch.Columns / 2f)
                {
                    hits.Add(new Hit(f, t.Tick[f], 1, (Pitch.Columns / 2f) - attackX, $"goleador {e.Actor} celebra en su campo (x={t.X[t.Slot(f, p)]:0.0})"));
                    break;
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// BH-A: congelación. (a) ≥ 150 ticks seguidos de juego abierto sin que el balón salga de un radio de
    /// 1 casilla ni suceda ningún evento; (b) cualquier tick con el dueño del balón fuera del campo (BB-O).
    /// </summary>
    public static IReadOnlyList<Hit> Freeze(DetectorTrace t, out int ownerOffPitchFrames)
    {
        ownerOffPitchFrames = 0;
        var hits = new List<Hit>();
        for (int f = 0; f < t.Frames; f++)
        {
            if (t.Owner[f] >= 0 && !t.On(f, t.Owner[f]))
            {
                ownerOffPitchFrames++;
            }
        }

        var eventTicks = new HashSet<int>();
        foreach (var e in t.Events)
        {
            if (e.Type is not (EventType.MatchStart or EventType.MatchEnd))
            {
                eventTicks.Add(e.Tick);
            }
        }

        int start = 0;
        for (int f = 1; f <= t.Frames; f++)
        {
            bool ok = f < t.Frames && t.Phase[f] == MatchPhase.OpenPlay && !eventTicks.Contains(t.Tick[f])
                && Vec2.Distance(t.Ball(f), t.Ball(start)) < FreezeBallRange;
            if (ok)
            {
                continue;
            }

            int run = f - start;
            if (run >= FreezeMinTicks && t.Phase[start] == MatchPhase.OpenPlay)
            {
                hits.Add(new Hit(start, t.Tick[start], run, run, $"{run} ticks sin evento ni balón en movimiento"));
            }

            start = f;
        }

        if (ownerOffPitchFrames > 0)
        {
            hits.Add(new Hit(0, t.Tick[0], ownerOffPitchFrames, ownerOffPitchFrames, "dueño del balón fuera del campo"));
        }

        return hits;
    }

    /// <summary>
    /// BA-E: goles sin ángulo. Un gol es «sin ángulo» si el último tiro de su autor salió con apertura
    /// &lt; 0,5. Devuelve los goles sin ángulo; <paramref name="shots"/> y <paramref name="lowApertureShots"/>
    /// cuentan todos los tiros.
    /// </summary>
    public static IReadOnlyList<Hit> GoalsWithoutAngle(DetectorTrace t, out int shots, out int lowApertureShots, out int goals)
    {
        shots = 0;
        lowApertureShots = 0;
        goals = 0;
        var hits = new List<Hit>();
        var lastShotAperture = new Dictionary<int, (double Aperture, Vec2 Pos)>();
        foreach (var e in t.Events)
        {
            int p = t.IndexOfId(e.Actor);
            if (e.Type == EventType.Shot && p >= 0)
            {
                var pos = t.Pos(t.FrameOfTick(e.Tick), p);
                // Al disparar, el tirador está en el punto de tiro: la apertura se mide desde ahí (ApertureCenti).
                double ap = Aperture(pos, Pitch.GoalCenter(t.Team[p]));
                lastShotAperture[e.Actor] = (ap, pos);
                shots++;
                if (ap < LowAperture)
                {
                    lowApertureShots++;
                }
            }
            else if (e.Type == EventType.Goal && p >= 0)
            {
                goals++;
                if (lastShotAperture.TryGetValue(e.Actor, out var s) && s.Aperture < LowAperture)
                {
                    hits.Add(new Hit(t.FrameOfTick(e.Tick), e.Tick, 1, 1 - s.Aperture,
                        $"gol de {e.Actor} desde ({s.Pos.X:0.0},{s.Pos.Y:0.0}), apertura {s.Aperture:0.00}"));
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// BB-I: un perk <c>box_predator</c> (trigger <c>SHOT</c>) anunciado. (a) sin <c>Shot</c> del mismo
    /// actor en el mismo tick: el aviso no corresponde a un tiro (imposible por datos); (b) con el tiro
    /// bloqueado en el mismo tick o el siguiente: la hipótesis de la ficha, el tiro no llega a leerse.
    /// </summary>
    public static IReadOnlyList<Hit> PerkWithoutShot(DetectorTrace t, string perkId, out int triggers, out int blockedAtOnce)
    {
        triggers = 0;
        blockedAtOnce = 0;
        var hits = new List<Hit>();
        foreach (var e in t.Events)
        {
            if (e.Type != EventType.PerkTriggered || e.Detail != perkId)
            {
                continue;
            }

            triggers++;
            bool shot = t.Events.Any(s => s.Type == EventType.Shot && s.Actor == e.Actor && s.Tick == e.Tick);
            if (!shot)
            {
                hits.Add(new Hit(t.FrameOfTick(e.Tick), e.Tick, 1, 1, $"{perkId} de {e.Actor} sin tiro en ese tick"));
                continue;
            }

            if (t.Events.Any(s => s.Type == EventType.ShotBlocked && s.Actor == e.Actor && s.Tick >= e.Tick && s.Tick <= e.Tick + 1))
            {
                blockedAtOnce++;
            }
        }

        return hits;
    }

    // ----------------------------------------------------------------------------------------------------

    private static string ActionName(DetectorTrace t, int frame, int p)
        => t.Action[t.Slot(frame, p)] is var a and >= 0 ? ((PlayerAction)a).ToString() : "?";

    private static float NearestMate(DetectorTrace t, int frame, int p)
    {
        float best = float.MaxValue;
        for (int q = 0; q < t.Players; q++)
        {
            if (q != p && t.Team[q] == t.Team[p] && t.On(frame, q))
            {
                best = Math.Min(best, Vec2.Distance(t.Pos(frame, p), t.Pos(frame, q)));
            }
        }

        return best;
    }

    private static double Aperture(Vec2 point, Vec2 goal)
    {
        float d = Vec2.Distance(point, goal);
        return d <= 0.001f ? 1.0 : Math.Min(1.0, Math.Abs(goal.X - point.X) / d);
    }

    private static Vec2 Sub(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

    private static float Len(Vec2 a) => MathF.Sqrt((a.X * a.X) + (a.Y * a.Y));

    private static float Dot(Vec2 a, Vec2 b) => (a.X * b.X) + (a.Y * b.Y);
}
