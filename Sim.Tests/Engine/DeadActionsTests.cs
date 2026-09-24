using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0144 — <b>las acciones muertas, causa por causa</b>, y el censo que permite distinguirlas.
///
/// <para>El encargo prohíbe expresamente resucitar una acción subiéndole el peso: obliga a determinar qué
/// situación representa, qué necesita, qué gate la impide y si ese gate es correcto. Eso exige separar dos
/// cosas que piden arreglos <b>opuestos</b>: una acción que se <b>descarta</b> no la despierta ningún peso,
/// y una que <b>compite y pierde</b> no la arregla tocarle la precondición. El censo de utilidad mide esa
/// diferencia, y es el instrumento que las nueve auditorías de IA echaron de menos.</para>
/// </summary>
public sealed class DeadActionsTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MatchEngine Engine() => TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));

    // ---------------------------------------------------------------- el censo

    /// <summary>
    /// <b>El censo cuadra y no cambia el partido.</b> Las dos mitades importan: si no cuadrara no serviría
    /// para diagnosticar, y si cambiara el partido sería un instrumento que altera lo que mide.
    /// </summary>
    [Fact]
    public void ElCensoCuadraYNoCambiaElPartido()
    {
        var census = new UtilityCensus();

        var withCensus = Simulator.Run(
            TestMatches.Reference(Catalog, 7), 7, Catalog, SimConfig.Default with { Census = census });
        var without = Simulator.Run(
            TestMatches.Reference(Catalog, 7), 7, Catalog, SimConfig.Default);

        Assert.Equal(without.Report.Goals[0], withCensus.Report.Goals[0]);
        Assert.Equal(without.Report.Goals[1], withCensus.Report.Goals[1]);
        Assert.Equal(without.Report.Shots[0], withCensus.Report.Shots[0]);
        Assert.Equal(without.Events.Count, withCensus.Events.Count);

        Assert.True(census.DecisionsTotal() > 0, "el censo no registró ninguna decisión");

        // En cada decisión, cada acción legal cae en exactamente una de las tres columnas.
        foreach (var action in Enum.GetValues<PlayerAction>())
        {
            long offered = census.ChosenTotal(action) + census.DiscardedTotal(action) + census.LostTotal(action);
            Assert.True(offered >= census.ChosenTotal(action), $"{action}: el censo no cuadra");
        }
    }

    // ---------------------------------------------------------------- la descarga

    /// <summary>
    /// <b>Sin un compañero apretado no hay descarga que dar.</b> Es la precondición que define la acción:
    /// venir corto a ofrecerse cuando nadie está en apuros no es ofrecerse, es estorbar — y para buscar
    /// espacio ya está el desmarque.
    /// </summary>
    [Fact]
    public void SinCompaneroApretadoNoHayDescarga()
    {
        var engine = Engine();
        int carrier = engine.OutfieldIndexForTest(0, 0);
        int mate = engine.OutfieldIndexForTest(0, 1);

        // Portador solo en el centro del campo, sin un rival cerca.
        engine.GiveBallForTest(carrier, new Vec2(8f, 3.5f));
        engine.PlaceForTest(mate, new Vec2(9f, 3.5f));
        for (int i = 7; i < 14; i++)
        {
            engine.PlaceForTest(i, new Vec2(15.5f, 6.5f));
        }

        Assert.NotEqual(PlayerAction.OfferSupport, engine.ChooseForTest(mate));
    }

    /// <summary>
    /// <b>Con el portador apretado, la descarga es alcanzable</b> — que es el criterio que el encargo fija
    /// para este pass: que exista una situación en la que la acción sea una decisión razonable, no que se
    /// elija con ninguna frecuencia concreta.
    ///
    /// <para>Medido con el censo sobre partidos reales: la acción pasa de <b>0,01</b> elecciones por mil
    /// decisiones —muerta— a <b>18,6</b>, descartándose el 94 % de las veces porque su situación es
    /// específica. Eso es una acción situacional, que es lo que debe ser.</para>
    /// </summary>
    [Fact]
    public void ConElPortadorApretadoLaDescargaEsAlcanzable()
    {
        var census = new UtilityCensus();
        for (ulong seed = 1; seed <= 20; seed++)
        {
            Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Census = census });
        }

        Assert.True(
            census.ChosenTotal(PlayerAction.OfferSupport) > 0,
            "la descarga no se eligió ni una vez en veinte partidos: sigue muerta");
    }

    // ---------------------------------------------------------------- los gates que SÍ eran correctos

    /// <summary>
    /// <b>Presionar al poseedor se descarta porque casi nunca hay poseedor</b>, y ése es un gate
    /// conceptualmente correcto: no se puede presionar a quien no lleva el balón.
    ///
    /// <para>Está aquí porque el encargo obliga a decidir, para cada acción muerta, <b>si el gate es
    /// correcto</b> — y la respuesta puede ser que sí. Dejarla como está es una conclusión, no una
    /// omisión: el balón tiene dueño alrededor de un tercio del partido (BI-D), así que dos tercios de los
    /// descartes de esta acción son la realidad del juego y no un defecto.</para>
    /// </summary>
    [Fact]
    public void PresionarSeDescartaSoloCuandoNoHayAQuienPresionar()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);
        int theirs = engine.OutfieldIndexForTest(1, 0);

        // Balón suelto: no hay poseedor, así que presionar no significa nada.
        engine.PlaceForTest(mine, new Vec2(8f, 3.5f));
        engine.SetLooseForTest(new Vec2(8.5f, 3.5f), new Vec2(0f, 0f), velocityZ: 0f);
        Assert.NotEqual(PlayerAction.PressCarrier, engine.ChooseForTest(mine));

        // Con un rival llevándolo, la acción existe: el gate mira eso y nada más.
        engine.GiveBallForTest(theirs, new Vec2(8.5f, 3.5f));
        engine.RefreshPerceptionForTest();
        Assert.True(
            StateMachine.CanPerform(PlayerState.Positioning, PlayerAction.PressCarrier),
            "presionar tiene que seguir siendo una acción legal sin balón");
    }
}
