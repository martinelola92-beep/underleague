using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BV-A H8 (docs/pendientes/BV-A.md, ADR 0184): <b>una colocación se sostiene</b>. Sin ella, el 42,4 % de las
/// inversiones de rumbo a velocidad de carrera se deshacían en ≤ 4 ticks (sonda limpia): jugadores que van y vienen.
///
/// <para>Las tres causas CONFIRMED por la sonda (<see cref="OscillationProbeTests"/>): <c>CoverSpace</c> y
/// <c>Retreat</c> alternándose por un empate que el propio movimiento invierte; <c>CoverSpace</c> descartada al
/// llegar al borde exterior de la zona y recuperada en cuanto el jugador se aleja 0,25 casillas; y
/// <c>FindSpace</c> cambiando de hueco porque el anterior no estaba entre sus candidatos.</para>
/// </summary>
public sealed class PositioningHoldTests
{
    private const int Matches = 40;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public PositioningHoldTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Regla J, el instrumento con respuesta conocida: una carrera recta no tiene inversiones, un zigzag de ida
    /// y vuelta las tiene todas y se deshacen, un giro de 90° no es una inversión y un paso de andar no cuenta.
    /// </summary>
    [Fact]
    public void TheReversalInstrumentAnswersTheKnownCases()
    {
        var run = new Vec2(0.14f, 0f);
        Assert.False(OscillationProbeTests.IsReversal(run, run), "carrera recta");
        Assert.True(OscillationProbeTests.IsReversal(run, new Vec2(-0.14f, 0f)), "media vuelta a carrera");
        Assert.True(OscillationProbeTests.Undoes(run, new Vec2(-0.14f, 0f)), "volver a la dirección de antes deshace la inversión");
        Assert.False(OscillationProbeTests.IsReversal(run, new Vec2(0f, 0.14f)), "un giro de 90° no es una inversión");
        Assert.False(OscillationProbeTests.IsReversal(run, new Vec2(-0.05f, 0f)), "un paso de andar no es carrera");
        Assert.False(OscillationProbeTests.Undoes(new Vec2(0.01f, 0f), new Vec2(-0.14f, 0f)), "un empujón de 0,01 no deshace nada");
    }

    /// <summary>
    /// El arreglo, contra su control. Sonda limpia (sin porteros, cortes ni inversiones legítimas) sobre 40 partidos
    /// de referencia, semillas 1-40, deterministas: con la sostenida apagada el 42,4 % de las inversiones se deshacen
    /// en ≤ 4 ticks (0,164 inversiones por jugador y segundo); con <c>positioningHoldBonus</c> = 50, el 14,6 %
    /// (0,095/s). El tope del 15 % es el objetivo del encargo, no un margen: 50 es la menor dosis que lo cumple
    /// (40 da 15,4 %), así que cualquier cambio que empeore la oscilación lo pone en rojo, que es lo que debe hacer.
    /// </summary>
    [Fact]
    public void ReversalsAreRarelyUndoneOnceAPositioningHolds()
    {
        Assert.True(Catalog.Ai.Context.PositioningHoldBonus > 0, "los datos reales debían traer la sostenida encendida");

        // El control, sobre el motor sin arranque (ADR 0185): el arranque también quita la oscilación —es otro remedio del
        // mismo síntoma—, y con él encendido el control dejaba de ver la que la sostenida tiene que quitar (medido: 27,6 %).
        var off = OscillationProbeTests.Measure(AccelerationTests.WithAccel(OscillationProbeTests.WithHold(Catalog, 0), 0), 1, Matches);
        // Los datos, sobre 120 partidos (10 oct 2026, BX-10): con 40 el porcentaje bailaba ±1 punto de un conjunto de
        // semillas a otro (11,9 / 12,7 / 14,3 % en 1-40, 41-80 y 81-120 con el mismo motor) y el tope está a esa distancia.
        // Con la intercepción en la máxima aproximación: 15,8 / 13,5 / 14,9 %, 14,7 % juntos, contra 13,0 % antes.
        // BX-8 (el saque de puerta) la sube en los cuatro bloques de 120 partidos (+1,3 de media, 15,8 % en 1-120): este
        // test queda ROJO en la rama de BX-8 a propósito, y la muestra NO se amplía para esconderlo (revisión
        // independiente, 10 oct). Se trata en BX-13.
        var on = OscillationProbeTests.Measure(Catalog, 1, 3 * Matches);
        OscillationProbeTests.Report(_output, "sin sostener", off);
        OscillationProbeTests.Report(_output, "datos", on);

        Assert.True(off.UndonePercent >= 35.0, $"control: sin sostener el instrumento debía ver la oscilación (≥ 35 %), vio {off.UndonePercent:F1} %");
        Assert.True(on.UndonePercent < 15.0, $"inversiones deshechas en ≤ 4 ticks: {on.UndonePercent:F1} % (tope 15 %)");
        Assert.True(on.PerPlayerSecond < 0.75 * off.PerPlayerSecond, $"y menos inversiones en total: {on.PerPlayerSecond:F3}/s contra {off.PerPlayerSecond:F3}/s");
    }


    /// <summary>Quién lleva el balón en un escenario montado.</summary>
    public enum Holder
    {
        Loose,
        Own,
        Rival,
    }

    /// <summary>
    /// La regla, en escenarios montados: un jugador que viene de una colocación se pone en una rejilla de puntos, con
    /// el balón suelto, en un compañero o en un rival, y se compara lo que elige sin y con sostenida. En NINGÚN punto
    /// la sostenida cambia una decisión que no sea entre dos colocaciones —si sin ella gana perseguir, entrar,
    /// presionar, bloquear o pasar, con ella gana lo mismo—, y cuando cambia algo es para quedarse con la que traía.
    /// Y existe al menos un punto donde un defensa que cubría sigue cubriendo en vez de replegarse.
    /// </summary>
    [Theory]
    [InlineData(PlayerAction.CoverSpace)]
    [InlineData(PlayerAction.Retreat)]
    [InlineData(PlayerAction.MarkOpponent)]
    [InlineData(PlayerAction.FindSpace)]
    [InlineData(PlayerAction.OfferSupport)]
    public void TheHoldOnlyEverSwapsOnePositioningForAnother(PlayerAction current)
    {
        int changed = 0, kept = 0, nonPositioning = 0;
        foreach (var holder in new[] { Holder.Loose, Holder.Own, Holder.Rival })
        {
            foreach (int who in new[] { 0, 2, 5 })
            {
                foreach (var ball in new[] { new Vec2(12f, 3.5f), new Vec2(6f, 1.5f), new Vec2(4f, 5.5f) })
                {
                    for (int ix = 0; ix <= 14; ix += 2)
                    {
                        for (int iy = 0; iy <= 6; iy += 2)
                        {
                            var at = new Vec2(0.75f + (ix * 0.5f), 0.5f + iy);
                            var off = Choose(OscillationProbeTests.WithHold(Catalog, 0), who, at, ball, holder, current, holder == Holder.Own);
                            var on = Choose(Catalog, who, at, ball, holder, current, holder == Holder.Own);
                            if (!IsPositioning(off))
                            {
                                nonPositioning++;
                            }

                            if (off == on)
                            {
                                continue;
                            }

                            changed++;
                            Assert.True(
                                IsPositioning(off) && IsPositioning(on),
                                $"{holder}, jugador {who} en {at}, balón en {ball}: la sostenida cambió {off} por {on}");
                            Assert.Equal(current, on);
                            if (current == PlayerAction.CoverSpace && off == PlayerAction.Retreat)
                            {
                                kept++;
                            }
                        }
                    }
                }
            }
        }

        _output.WriteLine($"viniendo de {current}: la sostenida cambia {changed} decisiones; sin ella ganaba una acción que no es de colocación en {nonPositioning} (intactas)");
        Assert.True(nonPositioning > 0, "la rejilla debía incluir puntos donde gana perseguir, entrar, presionar, bloquear o pasar");
        if (current == PlayerAction.CoverSpace)
        {
            Assert.True(kept > 0, "la rejilla debía encontrar un punto en el que la sostenida mantiene la cobertura frente al repliegue");
        }
    }

    /// <summary>
    /// Revisión independiente (ADR 0184): <c>FindSpace</c> en curso NO resiste a perseguir ni a bloquear. Con el
    /// balón en un compañero, un jugador que buscaba hueco elige exactamente lo mismo con y sin sostenida siempre
    /// que sin ella gane bloquear o perseguir: la sostenida del hueco sólo elige el sitio, no puntúa.
    /// </summary>
    [Fact]
    public void ARunningFindSpaceNeverResistsChasingOrBlocking()
    {
        int checkedPoints = 0;
        foreach (int who in new[] { 2, 3, 5 })
        {
            foreach (var ball in new[] { new Vec2(9f, 3.5f), new Vec2(11f, 1.5f), new Vec2(7f, 5.5f) })
            {
                foreach (var holder in new[] { Holder.Own, Holder.Loose })
                {
                    for (int ix = 0; ix <= 28; ix++)
                    {
                        for (int iy = 0; iy <= 6; iy++)
                        {
                            var at = new Vec2(0.75f + (ix * 0.5f), 0.5f + iy);
                            var off = Choose(OscillationProbeTests.WithHold(Catalog, 0), who, at, ball, holder, PlayerAction.FindSpace, holder == Holder.Own);
                            if (off is not (PlayerAction.ChaseBall or PlayerAction.Block))
                            {
                                continue;
                            }

                            checkedPoints++;
                            var on = Choose(Catalog, who, at, ball, holder, PlayerAction.FindSpace, holder == Holder.Own);
                            Assert.Equal(off, on);
                        }
                    }
                }
            }
        }

        _output.WriteLine($"puntos donde gana perseguir o bloquear viniendo de FindSpace: {checkedPoints}");
        Assert.True(checkedPoints > 0, "la rejilla debía encontrar puntos donde gana perseguir o bloquear");
    }

    /// <summary>
    /// Revisión independiente (ADR 0184): al recuperar el balón se decide desde cero. Un jugador que cubría,
    /// replegaba o marcaba SIN balón y ahora tiene el balón su equipo elige exactamente lo mismo con y sin
    /// sostenida, y en la rejilla pasa a buscar hueco u ofrecerse.
    /// </summary>
    [Theory]
    [InlineData(PlayerAction.CoverSpace)]
    [InlineData(PlayerAction.Retreat)]
    [InlineData(PlayerAction.MarkOpponent)]
    public void WinningTheBallBreaksTheHold(PlayerAction current)
    {
        int attacking = 0;
        foreach (int who in new[] { 0, 2, 5 })
        {
            foreach (var ball in new[] { new Vec2(9f, 3.5f), new Vec2(5f, 1.5f) })
            {
                for (int ix = 0; ix <= 14; ix++)
                {
                    for (int iy = 0; iy <= 6; iy++)
                    {
                        var at = new Vec2(0.75f + (ix * 0.5f), 0.5f + iy);
                        var off = Choose(OscillationProbeTests.WithHold(Catalog, 0), who, at, ball, Holder.Own, current, choseWithBall: false);
                        var on = Choose(Catalog, who, at, ball, Holder.Own, current, choseWithBall: false);
                        Assert.Equal(off, on);
                        if (on is PlayerAction.FindSpace or PlayerAction.OfferSupport)
                        {
                            attacking++;
                        }
                    }
                }
            }
        }

        _output.WriteLine($"viniendo de {current} sin balón, al recuperarlo busca hueco u se ofrece en {attacking} puntos");
        Assert.True(attacking > 0, "al recuperar el balón alguien debía pasar a buscar hueco u ofrecerse");
    }

    /// <summary>
    /// Hermano latente de la ADR 0184: quien recogía el balón a mitad de un bloqueo se quedaba en <c>Blocking</c>, la
    /// decisión no encontraba acción legal, caía al repliegue de reserva y lo dejaba en <c>Positioning</c> con el balón
    /// (sin pases ni regate): el atasco de 414 ticks de la semilla 70. Con la sostenida de los datos, en 200 partidos
    /// de referencia ese repliegue de reserva se disparaba 2 veces, siempre desde <c>Blocking</c>; ahora ninguna.
    /// </summary>
    [Fact]
    public void NoDecisionFallsBackToTheReserveRetreat()
    {
        var census = new UtilityCensus();
        for (ulong seed = 1; seed <= 200; seed++)
        {
            Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Census = census });
        }

        Assert.Equal(0L, census.Fallbacks.Sum());
    }

    private static bool IsPositioning(PlayerAction action) =>
        action is PlayerAction.CoverSpace or PlayerAction.Retreat or PlayerAction.MarkOpponent
            or PlayerAction.FindSpace or PlayerAction.OfferSupport;

    /// <summary>
    /// Lo que la sostenida NO toca: si gana una acción que no es de colocación —aquí perseguir un balón suelto
    /// al lado—, gana igual aunque el jugador estuviera cubriendo.
    /// </summary>
    [Fact]
    public void TheHoldNeverStopsAPlayerFromGoingForTheBall()
    {
        var off = Choose(OscillationProbeTests.WithHold(Catalog, 0), 0, new Vec2(3f, 2.5f), new Vec2(3.4f, 2.5f), Holder.Loose, PlayerAction.CoverSpace, false);
        var on = Choose(Catalog, 0, new Vec2(3f, 2.5f), new Vec2(3.4f, 2.5f), Holder.Loose, PlayerAction.CoverSpace, false);
        Assert.Equal(PlayerAction.ChaseBall, off);
        Assert.Equal(PlayerAction.ChaseBall, on);
    }

    /// <summary>
    /// Regla J sobre una traza REAL: las inversiones que la sonda atribuye a un cambio de acción las confirma un
    /// instrumento independiente, el volcado de utilidad RT-098, en el mismo tick y con la misma acción elegida.
    /// </summary>
    [Fact]
    public void TheProbeAgreesWithTheUtilityDumpOnARealTrace()
    {
        var catalog = OscillationProbeTests.WithHold(Catalog, 0);
        const ulong seed = 1;
        var setup = TestMatches.Reference(catalog, seed);
        var trace = Simulator.Run(setup, seed, catalog, SimConfig.Default with { Trace = true }).Trace!;
        var census = new OscillationProbeTests.Census { Matches = 1, Samples = new() };
        OscillationProbeTests.Accumulate(trace, census);

        var switches = census.Samples.Where(s => s.Cause == OscillationProbeTests.Cause.ActionSwitch).Take(5).ToList();
        Assert.True(switches.Count >= 3, $"la semilla 1 debía tener inversiones por cambio de acción, tiene {switches.Count}");
        foreach (var (player, frame, _) in switches)
        {
            int id = trace.Players[player].Id;
            int tick = trace.TickAt(frame);
            var dump = Simulator.Run(setup, seed, catalog, SimConfig.Default with { DumpUtility = (id, tick) }).Report.UtilityDump;
            Assert.NotNull(dump);
            Assert.Equal(tick, dump!.Tick);
            Assert.Equal(trace.ActionAt(frame, player), dump.Chosen);
            Assert.NotEqual(trace.ActionAt(frame - 1, player), dump.Chosen);
        }
    }

    /// <summary>
    /// Escenario: el jugador <paramref name="who"/> (orden de campo del local) en <paramref name="at"/>, viniendo de
    /// <paramref name="current"/>, con el balón suelto, en un compañero o en un rival en <paramref name="ball"/>.
    /// </summary>
    private static PlayerAction Choose(Catalog catalog, int who, Vec2 at, Vec2 ball, Holder holder, PlayerAction current, bool choseWithBall)
    {
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        switch (holder)
        {
            case Holder.Own:
                engine.GiveBallForTest(engine.OutfieldIndexForTest(0, who == 4 ? 3 : 4), ball);
                break;
            case Holder.Rival:
                engine.GiveBallForTest(engine.OutfieldIndexForTest(1, 5), ball);
                break;
            default:
                engine.ParkBallForTest(ball);
                break;
        }

        int index = engine.OutfieldIndexForTest(0, who);
        engine.PlaceForTest(index, at);
        var player = engine.PlayerAtForTest(index);
        player.EnterState(PlayerState.Positioning, 0);
        player.CurrentAction = current;
        player.ChoseWithBall = choseWithBall;
        return engine.ChooseForTest(index);
    }
}
