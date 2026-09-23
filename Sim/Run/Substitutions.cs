using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run;

/// <summary>
/// Punto de decisión de sustitución (ADR 0094): en el tick <paramref name="Tick"/> el jugador
/// <paramref name="OutPlayerId"/> del equipo <paramref name="Team"/> dejó el campo (<paramref name="Detail"/>
/// <c>injury</c> o <c>death</c>) y hay <paramref name="Candidates"/> en el banquillo que podrían entrar.
///
/// <para><b>Admite tres respuestas</b> (ADR 0134): que entre un candidato, que se quede el hueco
/// (<c>MatchDecisions.Declines</c>) o —solo si <paramref name="CanPlayOn"/>— que el lesionado no salga
/// (<c>MatchDecisions.PlayOns</c>). Todo lo que la interfaz necesita para presentarlas viaja aquí ya
/// calculado: <c>/Game</c> pinta, no decide (RT-014).</para>
/// </summary>
/// <param name="OutPosition">Puesto en el que jugaba el que sale.</param>
/// <param name="OutCell">Casilla que deja vacía.</param>
/// <param name="CanPlayOn">
/// Si cabe responder «que siga jugando» (ADR 0134 E): la lesión fue <b>leve</b> y el que la sufre no
/// arrastraba una grave sin tratar —ese moriría (RF-093 vía 1)—. Falso siempre en una muerte y en una
/// lesión grave, que apartan del campo sin remedio (RF-092).
/// </param>
/// <param name="CandidateRisks">
/// Por candidato y en el mismo orden que <paramref name="Candidates"/>: probabilidad en base 10.000 de que
/// <b>muera si recibe la entrada</b> de algún perk letal rival, en la casilla que deja el que sale
/// (<c>Lethality.ChanceAgainst</c>, RF-012c).
///
/// <para>No es el número del Ojeo. Aquel (<c>Lethality.MarkedRisks</c>) es <b>conjunto</b>: el perk letal
/// marca a los peor parados del once y enseña <b>cero a los demás</b>, así que por candidato sería
/// inservible —cuatro de cada cinco a cero— y además engañoso. Este responde a la pregunta que se hace
/// quien elige, y es lo único que sigue siendo exacto desde el paquete AY (ver <c>Lethality</c>, pendiente
/// AY-A). El texto visible <b>tiene que decir la condición</b> («si recibe la entrada: X %»), nunca «puede
/// morir: X %», que es la frase del Ojeo y significa otra cosa.</para>
/// </param>
/// <param name="DefaultCandidateId">
/// El que elegiría <see cref="SubstitutionPolicy.Default"/>, o -1 si no hay candidatos. Se enseña marcado
/// para que el jugador tenga una opción por defecto sin tener que razonarla, no para aplicarla sola.
/// </param>
/// <param name="PlayOnRisk">
/// Lo que de verdad cuesta quedarse, y no es el −15 % (ADR 0134 E): probabilidad en base 10.000 de que
/// <b>muera si recibe la entrada</b> el que se queda, <b>ya tocado</b>.
///
/// <para>Es el mismo número que <see cref="CandidateRisks"/> pero con <c>hurtInThisMatch: true</c>, y eso
/// lo multiplica por <c>lethality.minorInjuryPercent</c> —hoy ×8—. Además, como el perk letal marca al de
/// mayor probabilidad, quedarse tocado suele convertir al que se queda <b>en el marcado</b>. Antes del
/// apartado E esa rama era inalcanzable: toda lesión sacaba del campo, así que un tocado no podía volver a
/// recibir una entrada. La abre esta ADR, y por eso <b>tiene que ir en la bandeja con su número</b>: sin
/// él, quedarse se anuncia como «−15 % en sus atributos» y se cobra como «multiplica por ocho su
/// probabilidad de morir», que es exactamente el daño no anunciado que prohíbe RF-012d y la regla 11 del
/// <c>CLAUDE.md</c>. 0 si el rival no lleva ningún perk letal o si no cabe quedarse.</para>
/// </param>
/// <param name="PlayOnImmune">
/// El que se queda es inmune a la penalización de lesión leve (<c>ImmunityKind.MinorInjuryPenalty</c>, ADR
/// 0026: los no-muertos), así que <b>no paga el −15 %</b> y la bandeja no debe anunciárselo. Sigue pagando
/// <see cref="PlayOnRisk"/>: la inmunidad es a la penalización de atributos, no a la muerte.
/// </param>
public sealed record SubstitutionPoint(
    int Team,
    int Tick,
    int OutPlayerId,
    string Detail,
    IReadOnlyList<PlayerDefinition> Candidates,
    Position OutPosition,
    Cell OutCell,
    bool CanPlayOn,
    IReadOnlyList<int> CandidateRisks,
    int DefaultCandidateId,
    int PlayOnRisk,
    bool PlayOnImmune);

/// <summary>
/// Lo que comparten <c>/Game</c> (abrir la ventana) y <see cref="RunEngine"/> (resolverla sola): encontrar el
/// primer punto de decisión sin sustitución en un partido ya jugado, y volver a jugarlo con la decisión
/// añadida hasta que no quede ninguno. Es el mecanismo de RF-082 aplicado a la sustitución.
/// </summary>
public static class SubstitutionPoints
{
    /// <summary>
    /// Primer punto de decisión pendiente del equipo, o <c>null</c>. Un jugador cuenta como salido cuando
    /// hay un evento <c>INJURY</c> o <c>DEATH</c> suyo y el informe lo confirma (lesionado o muerto); los
    /// candidatos son los de la plantilla no alineados, sanos o con lesión leve, y no usados ya.
    ///
    /// <para><b>Un punto ya respondido deja de pender</b>, y cada respuesta lo consigue a su manera
    /// (ADR 0134): la sustitución porque queda en <c>side.Substitutions</c>; «que siga jugando» sin ningún
    /// registro, porque el que no deja el campo no tiene <c>LeftPitchTick</c> y el filtro de arriba ya no
    /// lo ve; y «que se quede el hueco» por <paramref name="declined"/>, que es la única que necesita
    /// decirse aparte —no produce ningún hecho observable en el partido—.</para>
    ///
    /// <para>Sin candidatos no hay punto, aunque la lesión fuera leve: abrir la ventana solo para ofrecer
    /// «que siga jugando» con el banquillo vacío es una decisión real pero toca la resolución automática
    /// (la política no tendría a quién meter), así que queda fuera de la ADR 0134.</para>
    /// </summary>
    public static SubstitutionPoint? Pending(
        MatchSetup setup,
        MatchResult result,
        int team,
        Catalog catalog,
        IReadOnlyList<DeclinedSubstitution>? declined = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(catalog);
        var side = team == 0 ? setup.Home : setup.Away;
        var events = result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Team != team || (e.Type != EventType.Injury && e.Type != EventType.Death))
            {
                continue;
            }

            // Un INJURY cancelado por un perk queda en el registro sin que nadie salga: lo que cuenta es el
            // tick en el que el informe dice que el jugador dejó el campo.
            if (LeftPitchTick(result.Report, e.Actor) != e.Tick || !WasOnPitch(side, e.Actor) || HasSubstitution(side, e.Actor)
                || WasDeclined(declined, e.Tick, e.Actor))
            {
                continue;
            }

            var candidates = Candidates(side);
            if (candidates.Count == 0)
            {
                return null;
            }

            var outPlayer = FindPlayer(side, e.Actor);
            bool died = DiedAt(events, e.Actor, e.Tick);

            // Seguir jugando solo cabe con la lesión LEVE (ADR 0134 E). El detalle del evento INJURY es el
            // que el motor emitió, "minor" o "severe"; y quien salió al campo arrastrando una grave sin
            // tratar muere al volver a lesionarse (RF-093 vía 1), así que tampoco puede quedarse.
            bool canPlayOn = !died
                && e.Type == EventType.Injury
                && e.Detail == "minor"
                && outPlayer.PhysicalState != PhysicalState.SevereInjury;

            var cell = CellOf(side, e.Actor);
            return new SubstitutionPoint(
                team,
                e.Tick,
                e.Actor,
                died ? "death" : "injury",
                candidates,
                outPlayer.Position,
                cell,
                canPlayOn,
                CandidateRisks(candidates, cell, team == 0 ? setup.Away : setup.Home, catalog),
                SubstitutionPolicy.Default(candidates, outPlayer).Id,
                canPlayOn ? PlayOnRisk(outPlayer, cell, team == 0 ? setup.Away : setup.Home, catalog) : 0,
                canPlayOn && Underleague.Sim.Progression.Progression.HasImmunity(
                    outPlayer, catalog, Underleague.Sim.Perks.ImmunityKind.MinorInjuryPenalty));
        }

        return null;
    }

    private static bool WasDeclined(IReadOnlyList<DeclinedSubstitution>? declined, int tick, int playerId)
    {
        if (declined is null)
        {
            return false;
        }

        for (int i = 0; i < declined.Count; i++)
        {
            if (declined[i].Tick == tick && declined[i].OutPlayerId == playerId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Casilla del que sale, o la del portero si no estaba en el once inicial (entró de suplente).</summary>
    private static Cell CellOf(TeamSetup side, int playerId)
    {
        var slots = side.Lineup.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].PlayerId == playerId)
            {
                return slots[i].HomeCell;
            }
        }

        // Un suplente que entró ocupa la casilla del que sustituyó, y esa sí está en el once inicial.
        var substitutions = side.Substitutions;
        for (int i = substitutions.Count - 1; i >= 0; i--)
        {
            if (substitutions[i].InPlayerId == playerId)
            {
                return CellOf(side, substitutions[i].OutPlayerId);
            }
        }

        return RunLineup.GoalkeeperCell;
    }

    /// <summary>
    /// Por candidato, la probabilidad en base 10.000 de <b>morir si recibe la entrada</b> de un perk letal
    /// rival en la casilla que queda libre (<c>Lethality.ChanceAgainst</c>, RF-012c). Se compone sobre todos
    /// los portadores como «que acierte al menos uno», en aritmética entera (RT-023).
    ///
    /// <para>Deliberadamente NO es <c>MarkedRisks</c>, que es el número del Ojeo: aquel reparte el riesgo
    /// por el once y devuelve cero a quien no queda marcado, así que por candidato mentiría. Ver el doc de
    /// <see cref="SubstitutionPoint.CandidateRisks"/>.</para>
    /// </summary>
    /// <summary>
    /// Lo que arriesga el que se queda (ADR 0134 E): igual que <see cref="CandidateRisks"/> pero con
    /// <c>hurtInThisMatch: true</c>, porque acaba de lesionarse y sigue en el campo. Ese booleano vale hoy
    /// un ×8 (<c>lethality.minorInjuryPercent</c>), así que es el coste dominante de quedarse —muy por
    /// encima del −15 % de atributos— y tiene que llegar a la pantalla con su número.
    /// </summary>
    private static int PlayOnRisk(PlayerDefinition outPlayer, Cell cell, TeamSetup opponent, Catalog catalog)
    {
        var carriers = Underleague.Sim.Perks.Lethality.CarriersOf(opponent, catalog);
        if (carriers.Count == 0)
        {
            return 0;
        }

        var lethality = catalog.Tuning.Injury.Lethality;
        int statePercent = Underleague.Sim.Perks.Lethality.StatePercent(
            lethality, outPlayer.PhysicalState, hurtInThisMatch: true);

        int survives = 10000;
        for (int j = 0; j < carriers.Count; j++)
        {
            int chance = Underleague.Sim.Perks.Lethality.ChanceAgainst(
                lethality, carriers[j], statePercent, outPlayer.Attributes.Stamina, cell);
            survives = survives * (10000 - Math.Clamp(chance, 0, 10000)) / 10000;
        }

        return 10000 - survives;
    }

    private static IReadOnlyList<int> CandidateRisks(
        IReadOnlyList<PlayerDefinition> candidates, Cell cell, TeamSetup opponent, Catalog catalog)
    {
        var risks = new int[candidates.Count];
        var carriers = Underleague.Sim.Perks.Lethality.CarriersOf(opponent, catalog);
        if (carriers.Count == 0)
        {
            return risks;
        }

        var lethality = catalog.Tuning.Injury.Lethality;
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            int statePercent = Underleague.Sim.Perks.Lethality.StatePercent(
                lethality, candidate.PhysicalState, hurtInThisMatch: false);

            int survives = 10000;
            for (int j = 0; j < carriers.Count; j++)
            {
                int chance = Underleague.Sim.Perks.Lethality.ChanceAgainst(
                    lethality, carriers[j], statePercent, candidate.Attributes.Stamina, cell);
                survives = survives * (10000 - Math.Clamp(chance, 0, 10000)) / 10000;
            }

            risks[i] = 10000 - survives;
        }

        return risks;
    }

    /// <summary>
    /// Juega el partido y, mientras un equipo al que <paramref name="usesPolicy"/> dé el visto bueno tenga un
    /// punto de decisión pendiente, añade la sustitución de la política por defecto y vuelve a jugar. Converge
    /// en tantas vueltas como suplentes hay. Las sustituciones que ya trae <paramref name="setup"/> se tratan
    /// como respuestas del jugador a su punto de decisión y se aplican al llegar a él, sin política (BC-E).
    /// Devuelve el estado inicial final (con las sustituciones) y su
    /// resultado, que es el que se aplica a la run y el que se reproduce (RT-024).
    /// </summary>
    public static (MatchSetup Setup, MatchResult Result) ResolveAutomatically(
        MatchSetup setup,
        ulong seed,
        Catalog catalog,
        SimConfig config,
        Func<int, bool>? usesPolicy = null,
        IReadOnlyList<DeclinedSubstitution>? declines = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(config);
        usesPolicy ??= static _ => true;

        // ADR 0134 D: «que se quede el hueco» es una respuesta más, y se consume como las demás —al llegar
        // cronológicamente a su punto—, no de entrada. Un punto ya rechazado deja de pender porque se le
        // pasa a Pending la lista de los YA consumidos; si se le pasara la lista entera desde el principio,
        // un rechazo que el partido no llegara a pedir se descartaría en silencio, y la ADR 0094 dice
        // explícitamente que eso es un error. Solo las tiene el equipo del jugador, que es el local (W-15).
        var consumed = new List<DeclinedSubstitution>();

        // BC-E: las sustituciones que ya trae el estado inicial (las que eligió el jugador en /Game) son
        // RESPUESTAS a un punto de decisión, no hechos fijos desde el tick 0. Se retiran del estado inicial y
        // se aplican cuando la resolución, en orden cronológico, llega a ese punto. Con ellas fijas desde el
        // principio, una sustitución del rival anterior a T —que se resuelve después— hacía divergir el
        // partido antes de T, el que sale ya no se lesionaba en T y el motor rechazaba la sustitución (9,4 %
        // de los puntos del jugador). Sin sustituciones de entrada (todo /Balance) nada cambia.
        var answers = Answers(setup, declines);
        var used = new bool[answers.Count];
        if (answers.Count > 0)
        {
            setup = setup with
            {
                Home = setup.Home with { Substitutions = Array.Empty<Substitution>() },
                Away = setup.Away with { Substitutions = Array.Empty<Substitution>() },
            };
        }

        var result = Simulator.Run(setup, seed, catalog, config);
        for (int round = 0; round < 32; round++)
        {
            // Siempre el punto MÁS TEMPRANO de los dos equipos: una sustitución en T solo cambia el partido
            // después de T, así que las anteriores siguen siendo válidas y las posteriores (calculadas sobre
            // un futuro que ya no existe) se descartan y se vuelven a resolver en la vuelta siguiente.
            SubstitutionPoint? point = null;
            int pointAnswer = -1;
            for (int team = 0; team < 2; team++)
            {
                var candidate = Pending(setup, result, team, catalog, consumed);
                if (candidate is null)
                {
                    continue;
                }

                int answer = AnswerFor(answers, used, candidate);
                if (answer < 0 && !usesPolicy(team))
                {
                    continue;
                }

                if (point is null || candidate.Tick < point.Tick)
                {
                    point = candidate;
                    pointAnswer = answer;
                }
            }

            if (point is null)
            {
                // Una respuesta que el partido no llegó a pedir no se descarta en silencio: el llamador pidió
                // una sustitución que no corresponde a ningún punto de decisión, y eso es un error explícito
                // (ADR 0094: «lo demás es ArgumentException»; RT-032).
                for (int i = 0; i < answers.Count; i++)
                {
                    if (!used[i])
                    {
                        var orphan = answers[i];
                        throw new ArgumentException(
                            orphan.IsDecline
                                ? $"el rechazo de sustitución del equipo {orphan.Team} en el tick {orphan.Tick} (sale {orphan.OutPlayerId}) no corresponde a ningún punto de decisión del partido"
                                : $"la sustitución del equipo {orphan.Team} en el tick {orphan.Tick} (sale {orphan.OutPlayerId}, entra {orphan.InPlayerId}) no corresponde a ningún punto de decisión del partido");
                    }
                }

                return (setup, result);
            }

            // «Que se quede el hueco» (ADR 0134 D): no entra nadie, así que no hay nada que añadir al estado
            // inicial ni nada que volver a simular —el partido ya se jugó con esa casilla vacía—. Solo se
            // anota como consumido para que el punto deje de pender en la vuelta siguiente.
            if (pointAnswer >= 0 && answers[pointAnswer].IsDecline)
            {
                used[pointAnswer] = true;
                consumed.Add(new DeclinedSubstitution(point.Tick, point.OutPlayerId));
                continue;
            }

            var side = point.Team == 0 ? setup.Home : setup.Away;
            var outPlayer = FindPlayer(side, point.OutPlayerId);
            int chosenId;
            if (pointAnswer >= 0)
            {
                used[pointAnswer] = true;
                chosenId = answers[pointAnswer].InPlayerId;
            }
            else
            {
                chosenId = SubstitutionPolicy.Default(point, outPlayer).Id;
            }
            setup = setup with
            {
                Home = WithSubstitution(setup.Home, point.Team == 0 ? new Substitution(point.Tick, point.OutPlayerId, chosenId) : null, point.Tick),
                Away = WithSubstitution(setup.Away, point.Team == 1 ? new Substitution(point.Tick, point.OutPlayerId, chosenId) : null, point.Tick),
            };
            result = Simulator.Run(setup, seed, catalog, config);
        }

        throw new InvalidOperationException("la resolución automática de sustituciones no converge (ADR 0094)");
    }

    /// <summary>
    /// Una respuesta del jugador a un punto de decisión, ya sea «entra este» o «que se quede el hueco»
    /// (ADR 0134). Es bookkeeping privado de la resolución, no un modelo: por eso aquí sí conviven las dos
    /// en una lista —hay que consumirlas en orden cronológico contra los mismos puntos— mientras que en
    /// <c>MatchDecisions</c> viajan separadas. <see cref="InPlayerId"/> no significa nada si
    /// <see cref="IsDecline"/>.
    ///
    /// <para>«Que siga jugando» no aparece: no es una respuesta que esta resolución consuma, sino parte del
    /// estado inicial que el motor ejecuta en su tick, y el punto deja de pender solo.</para>
    /// </summary>
    private readonly record struct Answer(int Team, int Tick, int OutPlayerId, int InPlayerId, bool IsDecline);

    /// <summary>Las respuestas que trae el estado inicial, con su equipo, en el orden de la plantilla.</summary>
    private static List<Answer> Answers(MatchSetup setup, IReadOnlyList<DeclinedSubstitution>? declines)
    {
        var answers = new List<Answer>();
        for (int i = 0; i < setup.Home.Substitutions.Count; i++)
        {
            var s = setup.Home.Substitutions[i];
            answers.Add(new Answer(0, s.Tick, s.OutPlayerId, s.InPlayerId, IsDecline: false));
        }

        for (int i = 0; i < setup.Away.Substitutions.Count; i++)
        {
            var s = setup.Away.Substitutions[i];
            answers.Add(new Answer(1, s.Tick, s.OutPlayerId, s.InPlayerId, IsDecline: false));
        }

        // Solo el equipo del jugador rechaza: el rival lo resuelve siempre con la política (W-15, el jugador
        // es el local).
        if (declines is not null)
        {
            for (int i = 0; i < declines.Count; i++)
            {
                answers.Add(new Answer(0, declines[i].Tick, declines[i].OutPlayerId, -1, IsDecline: true));
            }
        }

        return answers;
    }

    /// <summary>
    /// La respuesta a <paramref name="point"/>: mismo equipo, mismo tick, mismo jugador que sale y —si es
    /// una sustitución— un candidato que siga siendo legal. Si el partido ya no llega a ese punto, o el
    /// elegido dejó de ser candidato, no hay respuesta: el punto queda para la política o, si el equipo no la
    /// usa, pendiente, y la respuesta sin usar hace fallar la resolución al terminar. Devuelve su índice, o -1.
    ///
    /// <para>Un rechazo no tiene candidato que validar: «nadie» sigue siendo legal siempre.</para>
    /// </summary>
    private static int AnswerFor(List<Answer> answers, bool[] used, SubstitutionPoint point)
    {
        for (int i = 0; i < answers.Count; i++)
        {
            var answer = answers[i];
            if (used[i] || answer.Team != point.Team || answer.Tick != point.Tick || answer.OutPlayerId != point.OutPlayerId)
            {
                continue;
            }

            if (answer.IsDecline)
            {
                return i;
            }

            for (int j = 0; j < point.Candidates.Count; j++)
            {
                if (point.Candidates[j].Id == answer.InPlayerId)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>Las sustituciones del equipo hasta <paramref name="tick"/> incluido, más <paramref name="added"/> si la hay.</summary>
    private static TeamSetup WithSubstitution(TeamSetup side, Substitution? added, int tick)
    {
        var kept = new List<Substitution>(side.Substitutions.Count + 1);
        for (int i = 0; i < side.Substitutions.Count; i++)
        {
            if (side.Substitutions[i].Tick <= tick)
            {
                kept.Add(side.Substitutions[i]);
            }
        }

        if (added is not null)
        {
            kept.Add(added);
        }

        return kept.Count == side.Substitutions.Count && added is null ? side : side with { Substitutions = kept };
    }

    private static int LeftPitchTick(MatchReport report, int playerId)
    {
        var players = report.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].PlayerId == playerId)
            {
                return players[i].LeftPitchTick;
            }
        }

        return -1;
    }

    /// <summary>
    /// Si ese jugador murió de verdad en ese tick. Una muerte <b>anulada</b> por un perk ("Prohibido
    /// morir") queda en el registro con el detalle sufijado <c>:cancelled</c> y no cuenta: el jugador
    /// sigue vivo, y si además deja el campo es por la lesión, que es lo que hay que decirle a quien
    /// abre la ventana de sustitución.
    /// </summary>
    private static bool DiedAt(IReadOnlyList<MatchEvent> events, int playerId, int tick)
    {
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Type == EventType.Death
                && e.Actor == playerId
                && e.Tick == tick
                && !e.Detail.EndsWith(":cancelled", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Si ese jugador estaba en el campo: en el once inicial **o** habiendo entrado por una sustitución
    /// anterior de este mismo partido.
    ///
    /// <para>BA-B: antes solo miraba <c>Lineup.Slots</c>, que es el once de salida. Un suplente que ya había
    /// entrado no figura ahí, así que **al lesionarse él no se abría ninguna ventana de sustitución** y el
    /// partido se quedaba esperando una decisión que el jugador no podía tomar — con la única salida de
    /// cerrar el juego, y con guardado ironman (RT-061) eso es perder la run. Se veía sobre todo en un jefe,
    /// que es donde hay más bajas por partido.</para>
    /// </summary>
    private static bool WasOnPitch(TeamSetup side, int playerId)
    {
        if (IsLinedUp(side, playerId))
        {
            return true;
        }

        var substitutions = side.Substitutions;
        for (int i = 0; i < substitutions.Count; i++)
        {
            if (substitutions[i].InPlayerId == playerId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Si ese jugador salió en el once inicial. Lo usa <c>Candidates</c> para no proponer a un titular.</summary>
    private static bool IsLinedUp(TeamSetup side, int playerId)
    {
        var slots = side.Lineup.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].PlayerId == playerId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSubstitution(TeamSetup side, int outPlayerId)
    {
        var substitutions = side.Substitutions;
        for (int i = 0; i < substitutions.Count; i++)
        {
            if (substitutions[i].OutPlayerId == outPlayerId)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<PlayerDefinition> Candidates(TeamSetup side)
    {
        var candidates = new List<PlayerDefinition>();
        var players = side.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (IsLinedUp(side, player.Id) || player.PhysicalState is PhysicalState.SevereInjury or PhysicalState.Dead)
            {
                continue;
            }

            bool used = false;
            for (int j = 0; j < side.Substitutions.Count; j++)
            {
                used |= side.Substitutions[j].InPlayerId == player.Id;
            }

            if (!used)
            {
                candidates.Add(player);
            }
        }

        candidates.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return candidates;
    }

    private static PlayerDefinition FindPlayer(TeamSetup side, int playerId)
    {
        for (int i = 0; i < side.Players.Count; i++)
        {
            if (side.Players[i].Id == playerId)
            {
                return side.Players[i];
            }
        }

        throw new ArgumentException($"el jugador {playerId} no está en la plantilla de '{side.Id}'");
    }
}

/// <summary>Política por defecto de sustitución (ADR 0094): sano antes que tocado, misma posición que el que sale, y después el de mayor calidad. Empates por id ascendente.</summary>
public static class SubstitutionPolicy
{
    public static PlayerDefinition Default(SubstitutionPoint point, PlayerDefinition outPlayer)
    {
        ArgumentNullException.ThrowIfNull(point);
        return Default(point.Candidates, outPlayer);
    }

    /// <summary>
    /// Igual, sobre la lista de candidatos suelta. Existe porque <see cref="SubstitutionPoints.Pending"/>
    /// necesita saber cuál es el recomendado <b>para construir el propio punto</b> (ADR 0134: se enseña
    /// marcado), y pedirle el punto que está construyendo sería circular.
    /// </summary>
    public static PlayerDefinition Default(IReadOnlyList<PlayerDefinition> candidates, PlayerDefinition outPlayer)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(outPlayer);
        // Orden: sano antes que tocado (un lesionado leve que salta al campo multiplica su tirada letal, ADR
        // 0048: la política no manda a nadie a morir sin que lo pida la interfaz), misma posición, calidad, id.
        PlayerDefinition? best = null;
        int bestRank = int.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            int rank = (candidate.PhysicalState == PhysicalState.Healthy ? 1_000_000 : 0)
                + (candidate.Position == outPlayer.Position ? 100_000 : 0)
                + Quality(candidate);
            if (best is null || rank > bestRank || (rank == bestRank && candidate.Id < best.Id))
            {
                best = candidate;
                bestRank = rank;
            }
        }

        return best ?? throw new ArgumentException("el punto de decisión no tiene candidatos", nameof(candidates));
    }

    private static int Quality(PlayerDefinition player)
    {
        var a = player.Attributes;
        return a.Strength + a.Speed + a.Technique + a.Stamina + a.Leash;
    }
}
