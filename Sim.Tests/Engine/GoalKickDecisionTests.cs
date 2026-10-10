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
        foreach (var (trace, events, frame, taker) in GoalKicks(60))
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
    /// Revisión independiente de BX-8: el bono de peligro se quita SÓLO en el saque de puerta. En el saque el despeje
    /// queda en base más presión; con el balón en juego en su área (tras blocar) sigue cobrando el peligro, que ahí sí
    /// puede ser un rechace. Umbrales: base 150, presión 3 por centésima (tope 450 sin peligro); el portero en su área
    /// tiene peligro de 90 y pico (volcado de BX-8), así que con el bono pasa de 500.
    /// </summary>
    [Fact]
    public void OnlyTheGoalKickDropsTheDangerBonusOfTheClearance()
    {
        var context = Catalog.Ai.Context;
        int withoutDanger = context.ClearBase + (context.ClearPressureBonusPerCenti * 100);
        int goalKicks = 0;
        int openPlay = 0;

        // El balón en juego se toma en un blocaje (estado Holding, la parada con el balón en las manos): el portero que
        // recoge un balón que ha salido va a sacar de puerta, y su primera decisión ya es el saque (medido, semilla 4).
        for (ulong seed = 1; seed <= 20 && (goalKicks < 5 || openPlay < 5); seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var trace = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false, Trace: true)).Trace!;
            int? goalKickAt = null;
            int? openPlayAt = null;
            for (int f = 1; f < trace.FrameCount && (goalKickAt is null || openPlayAt is null); f++)
            {
                int taker = trace.RestartTakerAt(f);
                if (goalKickAt is null && trace.RestartAt(f) == RestartKind.GoalKick && taker >= 0 && trace.Players[taker].Role == Position.Goalkeeper)
                {
                    goalKickAt = f;
                }

                int owner = trace.BallOwnerAt(f);
                if (openPlayAt is null && owner >= 0 && trace.BallOwnerAt(f - 1) != owner && trace.Players[owner].Role == Position.Goalkeeper
                    && trace.RestartAt(f) == RestartKind.None && trace.PhaseAt(f) == MatchPhase.OpenPlay
                    && trace.StateAt(f, owner) == PlayerState.Holding)
                {
                    openPlayAt = f;
                }
            }

            if (goalKickAt is { } g && ClearAt(setup, seed, trace, g, trace.RestartTakerAt(g)) is { Rejected: false } kick)
            {
                goalKicks++;
                Assert.True(kick.Context <= withoutDanger, $"semilla {seed}: el despeje del saque de puerta cobra {kick.Context}, más que base y presión ({withoutDanger})");
            }

            if (openPlayAt is { } o && ClearAt(setup, seed, trace, o, trace.BallOwnerAt(o)) is { Rejected: false } play)
            {
                openPlay++;
                Assert.True(play.Context > withoutDanger + 50, $"semilla {seed}: el despeje del portero en juego cobra {play.Context}; sin el bono de peligro no pasaría de {withoutDanger}");
            }
        }

        Assert.True(goalKicks >= 5 && openPlay >= 5, $"muestra corta: {goalKicks} saques y {openPlay} balones en juego del portero");
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
        Assert.True(beyond > 0, $"ninguno de {longPasses} saques largos del portero pasó de {longRange} casillas: ¿tiene tope?");
    }

    private static UtilityRow? ClearAt(MatchSetup setup, ulong seed, MatchTrace trace, int frame, int player)
    {
        var dump = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false, DumpUtility: (trace.Players[player].Id, trace.TickAt(frame)))).Report.UtilityDump;
        return dump?.Rows.FirstOrDefault(r => r.Action == PlayerAction.Clear);
    }
}
