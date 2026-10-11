using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BX-8 (partida del revisor del 3 oct): el saque de puerta es la decisión que escribe la ADR 0141 §2 —«corto si hay
/// alguien seguro, largo si no» y el despeje si no hay opción segura—, no un despeje por defecto. Medido antes del
/// arreglo con 200 partidos de referencia: el portero despejaba el 89 % de los saques (el peligro, que mide dónde está
/// el balón, le daba +620 en un balón muerto) y su «pase corto» iba a 9,7 casillas de mediana porque la condición del
/// alcance le eximía también en el pase corto. Después: despeje 5 %, pase largo 93 %, corto 2 %.
/// </summary>
public sealed class GoalKickDecisionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static IEnumerable<(MatchTrace Trace, IReadOnlyList<MatchEvent> Events, int Frame, int Taker)> GoalKicks(int matches)
    {
        for (ulong seed = 1; seed <= (ulong)matches; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true));
            var trace = result.Trace!;
            for (int f = 0; f + 1 < trace.FrameCount; f++)
            {
                if (trace.RestartAt(f) == RestartKind.GoalKick && trace.RestartAt(f + 1) != RestartKind.GoalKick && trace.RestartTakerAt(f) >= 0)
                {
                    yield return (trace, result.Events, f + 1, trace.RestartTakerAt(f));
                }
            }
        }
    }

    [Fact]
    public void TheGoalKickIsNotAClearanceByDefault()
    {
        int kicks = 0;
        int clears = 0;
        foreach (var (trace, _, frame, taker) in GoalKicks(60))
        {
            kicks++;
            if (trace.ActionAt(frame, taker) == PlayerAction.Clear)
            {
                clears++;
            }
        }

        Assert.True(kicks >= 60, $"la muestra debía tener saques de puerta: {kicks}");
        Assert.True(clears * 100 < kicks * 20, $"el portero despejó {clears} de {kicks} saques de puerta (antes de BX-8, el 89 %)");
    }

    [Fact]
    public void AKeeperShortPassStaysWithinShortRange()
    {
        float shortRange = Catalog.Ai.Context.ShortPassMaxCells;
        int checkedPasses = 0;

        // 200 partidos: el corto es el 2 % de los saques, y con BX-8c 60 partidos se quedaron sin ninguno.
        foreach (var (trace, events, frame, taker) in GoalKicks(200))
        {
            if (trace.ActionAt(frame, taker) != PlayerAction.ShortPass)
            {
                continue;
            }

            int takerId = trace.Players[taker].Id;
            int tick = trace.TickAt(frame);
            var attempt = events.FirstOrDefault(e => e.Tick >= tick && e.Tick < tick + 60 && e.Type == EventType.PassAttempted && e.Actor == takerId);
            if (attempt is null)
            {
                continue;
            }

            // Distancia en el fotograma de la DECISIÓN, que es donde se filtra el alcance: al golpear, unos ticks
            // después (el armado del pase), el receptor ya se ha movido.
            int receiver = Enumerable.Range(0, trace.Players.Count).First(p => trace.Players[p].Id == attempt.Target);
            float distance = Vec2.Distance(trace.PositionAt(frame, taker), trace.PositionAt(frame, receiver));
            Assert.True(distance <= shortRange + 0.01f, $"pase corto del portero a {distance:0.0} casillas (alcance {shortRange})");
            checkedPasses++;
        }

        Assert.True(checkedPasses > 0, "la muestra debía tener algún pase corto de saque de puerta");
    }
    /// <summary>
    /// El bono de peligro del despeje se quita SÓLO cuando nadie puede amenazar: con el balón muerto (saque de puerta y
    /// falta a favor, BX-8 y BX-8b) y con el portero dueño del balón en su área, que la cierra (ADR 0152, BX-8c): tras
    /// blocar y también al recogerlo suelto o de un pase atrás, en el mismo tick. Ahí el
    /// despeje queda en base más presión (tope 450). El control del otro lado: un jugador de campo con el balón en su
    /// área, en juego y sin rival a menos del radio de presión, sí lo cobra —su despeje pasa de la base en al menos 100—.
    /// </summary>
    [Fact]
    public void OnlyWhenNobodyCanThreatenDoesTheClearanceDropTheDangerBonus()
    {
        var context = Catalog.Ai.Context;
        int withoutDanger = context.ClearBase + (context.ClearPressureBonusPerCenti * 100);
        int goalKicks = 0;
        int caught = 0;
        int pickedUp = 0;
        int outfield = 0;

        // El blocaje se toma en el estado Holding (la parada con el balón en las manos): el portero que recoge un balón
        // que ha salido va a sacar de puerta, y su primera decisión ya es el saque (medido, semilla 4). Tope de 120 semillas y no
        // 40 desde BX-10b (11 oct 2026): las recogidas se quedaron en 3 con 40 al mover las semillas; el barrido para en cuanto
        // los cuatro casos tienen muestra.
        for (ulong seed = 1; seed <= 120 && (goalKicks < 5 || caught < 5 || pickedUp < 5 || outfield < 5); seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var trace = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false, Trace: true)).Trace!;
            int? goalKickAt = null;
            int? caughtAt = null;
            int? pickedUpAt = null;
            int? outfieldAt = null;
            for (int f = 1; f < trace.FrameCount && (goalKickAt is null || caughtAt is null || pickedUpAt is null || outfieldAt is null); f++)
            {
                int taker = trace.RestartTakerAt(f);
                if (goalKickAt is null && trace.RestartAt(f) == RestartKind.GoalKick && taker >= 0 && trace.Players[taker].Role == Position.Goalkeeper)
                {
                    goalKickAt = f;
                }

                int owner = trace.BallOwnerAt(f);
                if (owner < 0 || trace.RestartAt(f) != RestartKind.None || trace.PhaseAt(f) != MatchPhase.OpenPlay)
                {
                    continue;
                }

                if (caughtAt is null && trace.BallOwnerAt(f - 1) != owner && trace.Players[owner].Role == Position.Goalkeeper
                    && trace.StateAt(f, owner) == PlayerState.Holding && Pitch.IsInArea(trace.BallAt(f), trace.Players[owner].Team))
                {
                    caughtAt = f;
                }

                // Sin una reanudación en los fotogramas de alrededor: el sacador de una falta decide justo cuando la
                // traza ya la da por cerrada, y ese despeje, con el balón muerto, no cobra el peligro (BX-8b).
                // Recogida suelta o pase atrás: el portero decide en el mismo tick en que se hace con el balón, que es
                // donde un área publicada al principio del tick todavía constaba como abierta (revisión de BX-8c).
                if (pickedUpAt is null && trace.BallOwnerAt(f - 1) != owner && trace.Players[owner].Role == Position.Goalkeeper
                    && trace.StateAt(f, owner) != PlayerState.Holding && f >= 2 && trace.RestartAt(f - 1) == RestartKind.None
                    && Pitch.IsInArea(trace.BallAt(f), trace.Players[owner].Team))
                {
                    pickedUpAt = f;
                }

                if (outfieldAt is null && trace.Players[owner].Role != Position.Goalkeeper && f >= 2
                    && trace.RestartAt(f - 1) == RestartKind.None && trace.RestartAt(f - 2) == RestartKind.None
                    && Pitch.IsInArea(trace.BallAt(f), trace.Players[owner].Team) && NearestRival(trace, f, owner) > PitchConstants.PressureRadius + 0.2f)
                {
                    outfieldAt = f;
                }
            }

            if (goalKickAt is { } g && ClearAt(setup, seed, trace, g, trace.RestartTakerAt(g)) is { Rejected: false } kick)
            {
                goalKicks++;
                Assert.True(kick.Context <= withoutDanger, $"semilla {seed}: el despeje del saque de puerta cobra {kick.Context}, más que base y presión ({withoutDanger})");
            }

            // La primera decisión es al acabar el Holding: mientras sostiene el balón no decide.
            int? decideAt = null;
            if (caughtAt is { } c0)
            {
                int keeper = trace.BallOwnerAt(c0);
                for (int f = c0; f < trace.FrameCount && trace.BallOwnerAt(f) == keeper; f++)
                {
                    if (trace.StateAt(f, keeper) != PlayerState.Holding)
                    {
                        decideAt = f;
                        break;
                    }
                }
            }

            if (decideAt is { } c && ClearAt(setup, seed, trace, c, trace.BallOwnerAt(c)) is { Rejected: false } held)
            {
                caught++;
                Assert.True(held.Context <= withoutDanger, $"semilla {seed}: el despeje del portero tras blocar cobra {held.Context}, más que base y presión ({withoutDanger})");
            }

            if (pickedUpAt is { } u && ClearAt(setup, seed, trace, u, trace.BallOwnerAt(u)) is { Rejected: false } picked)
            {
                pickedUp++;
                Assert.True(picked.Context <= withoutDanger, $"semilla {seed}, tick {trace.TickAt(u)}: el despeje del portero que recoge el balón en su área cobra {picked.Context}, más que base y presión ({withoutDanger})");
            }

            if (outfieldAt is { } o && ClearAt(setup, seed, trace, o, trace.BallOwnerAt(o)) is { Rejected: false } play)
            {
                outfield++;
                Assert.True(play.Context >= context.ClearBase + 100, $"semilla {seed}, tick {trace.TickAt(o)}: el despeje de un jugador de campo en su área cobra {play.Context}; sin rival que apriete, sólo el bono de peligro lo separa de la base ({context.ClearBase})");
            }
        }

        Assert.True(goalKicks >= 5 && caught >= 5 && pickedUp >= 5 && outfield >= 5, $"muestra corta: {goalKicks} saques, {caught} blocajes, {pickedUp} recogidas y {outfield} balones de campo en el área");
    }

    /// <summary>
    /// BX-8c, el resultado: tras blocar dentro de su área, el equipo del portero se queda el balón (primer dueño
    /// distinto del portero, o quien saca la reanudación siguiente). Con este instrumento, 133 de 408 (33 %) cuando el
    /// despeje cobraba el peligro —despejaba el 86 %— y 260 de 409 (64 %) sin él. El suelo del 50 % separa las dos.
    /// </summary>
    [Fact]
    public void TheKeepersTeamKeepsTheBallAfterACatchOftenEnough()
    {
        int catches = 0;
        int kept = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true)).Trace!;
            for (int f = 1; f < trace.FrameCount; f++)
            {
                int keeper = trace.BallOwnerAt(f);
                if (keeper < 0 || trace.BallOwnerAt(f - 1) == keeper || trace.Players[keeper].Role != Position.Goalkeeper
                    || trace.StateAt(f, keeper) != PlayerState.Holding || trace.RestartAt(f) != RestartKind.None
                    || !Pitch.IsInArea(trace.BallAt(f), trace.Players[keeper].Team))
                {
                    continue;
                }

                int next = -1;
                for (int g = f + 1; g < trace.FrameCount && g < f + 200; g++)
                {
                    if (trace.RestartAt(g) != RestartKind.None)
                    {
                        next = trace.RestartTakerAt(g);
                        break;
                    }

                    int owner = trace.BallOwnerAt(g);
                    if (owner >= 0 && owner != keeper)
                    {
                        next = owner;
                        break;
                    }
                }

                catches++;
                if (next >= 0 && trace.Players[next].Team == trace.Players[keeper].Team)
                {
                    kept++;
                }

                f += 10;
            }
        }

        Assert.True(catches >= 200, $"muestra corta: {catches} blocajes");
        Assert.True(kept * 100 >= catches * 50, $"el equipo del portero conservó {kept} de {catches} blocajes ({100.0 * kept / catches:F0} %; con el bono de peligro, 133 de 408)");
    }

    private static float NearestRival(MatchTrace trace, int frame, int player)
    {
        float best = float.MaxValue;
        for (int q = 0; q < trace.Players.Count; q++)
        {
            if (trace.Players[q].Team != trace.Players[player].Team && trace.OnPitchAt(frame, q))
            {
                best = Math.Min(best, Vec2.Distance(trace.PositionAt(frame, player), trace.PositionAt(frame, q)));
            }
        }

        return best;
    }

    /// <summary>
    /// BX-8b, hermano de BX-8: en la falta a favor dentro del tercio propio el balón también está muerto y el peligro
    /// (que mide dónde está el balón) tampoco describe amenaza: la barrera deja al rival a dos casillas. Medido con una
    /// sonda de 200 partidos antes del arreglo: el sacador despejaba 12 de 63 faltas desde su tercio y conservaba el
    /// 33 %, frente al 93 % del pase corto desde las mismas posiciones; después, 0 de 65. El techo del 5 % separa las dos
    /// versiones sin exigir el cero, porque el despeje sigue siendo legal (ADR 0143) y es el último recurso de la
    /// ADR 0141 §2.
    /// </summary>
    [Fact]
    public void AFreeKickInTheOwnThirdIsNotAClearanceByDefault()
    {
        int kicks = 0;
        int clears = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true)).Trace!;
            for (int f = 0; f + 1 < trace.FrameCount; f++)
            {
                int taker = trace.RestartTakerAt(f);
                if (trace.RestartAt(f) != RestartKind.FreeKick || trace.RestartAt(f + 1) == RestartKind.FreeKick || taker < 0
                    || Pitch.ZoneOf(trace.PositionAt(f, taker), trace.Players[taker].Team) != Zone.Own)
                {
                    continue;
                }

                kicks++;
                if (trace.ActionAt(f + 1, taker) == PlayerAction.Clear)
                {
                    clears++;
                }
            }
        }

        Assert.True(kicks >= 40, $"la muestra debía tener faltas a favor en el tercio propio: {kicks}");
        Assert.True(clears * 100 < kicks * 5, $"el sacador despejó {clears} de {kicks} faltas en su tercio (antes de BX-8b, 12 de 63)");
    }

    /// <summary>
    /// El síntoma del revisor medido donde se ve: quién se queda el balón tras el saque de puerta. Antes de BX-8 el rival
    /// se lo quedaba el 92 % de las veces y con el arreglo el equipo del portero conserva ~38 % (sonda de 200 partidos de
    /// BX-8.md). Este test cuenta otra cosa —el primer dueño distinto del sacador, en 60 partidos—: sin el arreglo da 5 de
    /// 100 (medido). El suelo, 25 %, separa las dos versiones con margen para el ruido.
    /// </summary>
    [Fact]
    public void TheKeepersTeamKeepsTheBallAfterAGoalKickOftenEnough()
    {
        int kicks = 0;
        int kept = 0;
        foreach (var (trace, _, frame, taker) in GoalKicks(60))
        {
            int team = trace.Players[taker].Team;
            for (int f = frame; f < trace.FrameCount && f < frame + 120; f++)
            {
                int owner = trace.BallOwnerAt(f);
                if (trace.RestartAt(f) != RestartKind.None && f > frame)
                {
                    break;
                }

                if (owner >= 0 && owner != taker)
                {
                    kicks++;
                    if (trace.Players[owner].Team == team)
                    {
                        kept++;
                    }

                    break;
                }
            }
        }

        Assert.True(kicks >= 60, $"la muestra debía tener saques de puerta resueltos: {kicks}");
        Assert.True(kept * 100 >= kicks * 25, $"el equipo del portero conservó {kept} de {kicks} saques de puerta (sin el arreglo de BX-8, 5 de 100)");
    }

    /// <summary>
    /// Decisión del revisor (10 oct 2026): el pase largo del portero NO tiene tope —«el saque de puerta llega a donde
    /// llega», <c>fase1b-diseno.md:320</c>, ADR 0030—. Lo que protege: que algún saque largo vaya más allá del alcance
    /// del pase largo de los demás. Si un cambio le pone el tope, este test se pone rojo.
    /// </summary>
    [Fact]
    public void TheKeepersLongGoalKickHasNoRangeCap()
    {
        float longRange = Catalog.Ai.Context.LongPassMaxCells;
        int longPasses = 0;
        int beyond = 0;
        foreach (var (trace, events, frame, taker) in GoalKicks(60))
        {
            if (trace.ActionAt(frame, taker) != PlayerAction.LongPass)
            {
                continue;
            }

            int takerId = trace.Players[taker].Id;
            int tick = trace.TickAt(frame);
            var attempt = events.FirstOrDefault(e => e.Tick >= tick && e.Tick < tick + 60 && e.Type == EventType.PassAttempted && e.Actor == takerId);
            if (attempt is null)
            {
                continue;
            }

            longPasses++;
            int receiver = Enumerable.Range(0, trace.Players.Count).First(p => trace.Players[p].Id == attempt.Target);
            if (Vec2.Distance(trace.PositionAt(frame, taker), trace.PositionAt(frame, receiver)) > longRange + 0.01f)
            {
                beyond++;
            }
        }

        Assert.True(longPasses > 0, "la muestra debía tener saques largos del portero");
        // Hoy pasan de 8 casillas el 93 % de los saques largos (305 de 328 en 200 partidos, revisión independiente): exigir sólo
        // uno dejaba verde un tope casi total. La mitad es una cota que un tope real no puede cumplir.
        Assert.True(beyond * 2 >= longPasses, $"sólo {beyond} de {longPasses} saques largos del portero pasaron de {longRange} casillas: ¿tiene tope?");
    }

    /// <summary>
    /// BX-8, segunda revisión: el saque largo del portero no busca al más adelantado por sistema. Más allá del alcance del
    /// pase largo de los demás, cada casilla de más deja de sumar avance (sin tope: si no hay nadie mejor, sigue llegando
    /// a donde llega). Medido con este mismo instrumento sobre 200 partidos: 126 de 326 saques largos conservados antes
    /// (39 %) y 161 de 321 después (50 %). El suelo, 44 %, queda a unos dos errores típicos de cada lado.
    /// </summary>
    [Fact]
    public void TheKeepersLongGoalKickGoesOutForTheRivalLessOften()
    {
        int kicks = 0;
        int kept = 0;
        foreach (var (trace, _, frame, taker) in GoalKicks(200))
        {
            if (trace.ActionAt(frame, taker) != PlayerAction.LongPass)
            {
                continue;
            }

            int team = trace.Players[taker].Team;
            for (int f = frame; f < trace.FrameCount && f < frame + 150; f++)
            {
                int owner = trace.BallOwnerAt(f);
                if (owner >= 0 && owner != taker)
                {
                    kicks++;
                    kept += trace.Players[owner].Team == team ? 1 : 0;
                    break;
                }

                // Un saque que sale del campo se lo queda quien saca la reanudación: el balón largo al más adelantado se
                // iba por la línea de fondo (112 de 325 saques largos acababan en reanudación antes del arreglo, ~51
                // después). Sin contarlas, las dos versiones conservaban lo mismo (56 % y 58 %): es aquí donde se perdía.
                if (f > frame && trace.RestartAt(f) != RestartKind.None)
                {
                    int restartTaker = trace.RestartTakerAt(f);
                    if (restartTaker >= 0)
                    {
                        kicks++;
                        kept += trace.Players[restartTaker].Team == team ? 1 : 0;
                    }

                    break;
                }
            }
        }

        Assert.True(kicks >= 150, $"muestra corta: {kicks} saques largos resueltos");
        Assert.True(kept * 100 >= kicks * 44, $"el equipo del portero conservó {kept} de {kicks} saques largos ({100.0 * kept / kicks:F0} %; antes del arreglo, 126 de 326, el 39 %)");
    }

    private static UtilityRow? ClearAt(MatchSetup setup, ulong seed, MatchTrace trace, int frame, int player)
    {
        var dump = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false, DumpUtility: (trace.Players[player].Id, trace.TickAt(frame)))).Report.UtilityDump;
        return dump?.Rows.FirstOrDefault(r => r.Action == PlayerAction.Clear);
    }
}
