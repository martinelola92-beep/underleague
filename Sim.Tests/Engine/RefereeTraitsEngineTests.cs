using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0158 §2, cierra D-22: el motor ignoraba <c>RefereeSetup.Trait</c> por completo. Cada rasgo tiene
/// que producir un efecto MEDIBLE sobre partidos construidos, con la misma semilla y solo el rasgo
/// distinto (game-design-review, pregunta 10).
/// </summary>
public sealed class RefereeTraitsEngineTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Semillas suficientes para que el escenario de referencia produzca cientos de tarjetas (mismo criterio que <c>RefereeAndAbilitiesTests.TheInitialCriterionChangesTheMatch</c>).</summary>
    private const int Seeds = 120;

    private static MatchSetup With(RefereeTrait trait, RefereeSide blindSide = RefereeSide.None) =>
        TestMatches.Reference(Catalog, 1) with
        {
            Referee = new RefereeSetup("test", trait, 0) { BlindSide = blindSide },
        };

    private static int TotalCards(MatchSetup setup)
    {
        int total = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            total += result.Report.YellowCards + result.Report.RedCards;
        }

        return total;
    }

    /// <summary>
    /// Estricto (whistlePercent 95, cardOddsPercent 150) tiene que producir bastantes más tarjetas que
    /// permisivo (55 / 50) sobre el mismo emparejamiento y las mismas semillas.
    /// </summary>
    [Fact]
    public void AStrictRefereeGivesManyMoreCardsThanALenientOne()
    {
        int strictCards = TotalCards(With(RefereeTrait.Strict));
        int lenientCards = TotalCards(With(RefereeTrait.Lenient));

        Assert.True(
            strictCards > lenientCards,
            $"tarjetas con árbitro estricto {strictCards} frente a permisivo {lenientCards}");
    }

    /// <summary>Estricto también tiene que superar a neutro: la tabla no puede ser un no-op.</summary>
    [Fact]
    public void AStrictRefereeGivesMoreCardsThanANeutralOne()
    {
        int strictCards = TotalCards(With(RefereeTrait.Strict));
        int neutralCards = TotalCards(With(RefereeTrait.Neutral));

        Assert.True(
            strictCards > neutralCards,
            $"tarjetas con árbitro estricto {strictCards} frente a neutro {neutralCards}");
    }

    /// <summary>Permisivo también tiene que quedarse por debajo de neutro.</summary>
    [Fact]
    public void ALenientRefereeGivesFewerCardsThanANeutralOne()
    {
        int lenientCards = TotalCards(With(RefereeTrait.Lenient));
        int neutralCards = TotalCards(With(RefereeTrait.Neutral));

        Assert.True(
            lenientCards < neutralCards,
            $"tarjetas con árbitro permisivo {lenientCards} frente a neutro {neutralCards}");
    }

    /// <summary>
    /// Cobarde (ADR 0158 §2): nunca saca roja, ni directa ni por doble amarilla. Se mide sobre el
    /// emparejamiento brutal (mucha entrada dura, RF-064e) para darle al rasgo toda la oportunidad de
    /// fallar. Regla J: primero se comprueba que el instrumento SÍ sabe producir rojas con un árbitro
    /// neutro sobre el mismo emparejamiento -si no, "cero rojas" no demostraría nada del rasgo-.
    /// </summary>
    [Fact]
    public void ACowardlyRefereeNeverShowsRed()
    {
        var brutal = TestMatches.Brutal(Catalog);
        int neutralReds = 0;
        int cowardlyReds = 0;
        const int BrutalSeeds = 30;
        for (ulong seed = 1; seed <= BrutalSeeds; seed++)
        {
            var neutral = brutal with { Referee = brutal.Referee with { Trait = RefereeTrait.Neutral } };
            var cowardly = brutal with { Referee = brutal.Referee with { Trait = RefereeTrait.Cowardly } };
            neutralReds += Simulator.Run(neutral, seed, Catalog, new SimConfig(CollectLog: false)).Report.RedCards;
            cowardlyReds += Simulator.Run(cowardly, seed, Catalog, new SimConfig(CollectLog: false)).Report.RedCards;
        }

        Assert.True(neutralReds > 0, "el emparejamiento brutal tiene que producir alguna roja con árbitro neutro, o el test no mide nada (Regla J)");
        Assert.Equal(0, cowardlyReds);
    }

    /// <summary>
    /// Cobarde tampoco expulsa por doble amarilla: se queda en amarilla (ADR 0158 §2). Se comprueba que
    /// nadie del equipo termina con <c>SentOff</c> pese a acumular dos o más amarillas.
    /// </summary>
    [Fact]
    public void ACowardlyRefereeNeverSendsOffOnASecondYellowEither()
    {
        var brutal = TestMatches.Brutal(Catalog);
        var cowardly = brutal with { Referee = brutal.Referee with { Trait = RefereeTrait.Cowardly } };

        for (ulong seed = 1; seed <= 30; seed++)
        {
            var result = Simulator.Run(cowardly, seed, Catalog, new SimConfig(CollectLog: false));
            Assert.DoesNotContain(
                result.Events,
                e => e.Type == EventType.Card && e.Detail.StartsWith("red", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Tuerto (ADR 0158 §2): 0% de faltas señaladas en su lado ciego, medido por la fila del INFRACTOR
    /// (<c>MatchEvent.Cell.Row</c>, que el motor calcula sobre la posición del actor, ADR 0158). Se aísla
    /// el lado <c>Top</c> (filas de índice menor que <c>Pitch.Rows/2</c>) sobre el emparejamiento brutal,
    /// que reparte el juego por todo el campo.
    /// </summary>
    [Fact]
    public void AOneEyedRefereeNeverCallsAFoulOnHisBlindSide()
    {
        var brutal = TestMatches.Brutal(Catalog);
        var oneEyed = brutal with { Referee = brutal.Referee with { Trait = RefereeTrait.OneEyed, BlindSide = RefereeSide.Top } };
        int center = Pitch.Rows / 2;

        int calledOnBlindSide = 0;
        int unseenOnBlindSide = 0;
        int calledElsewhere = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var result = Simulator.Run(oneEyed, seed, Catalog, new SimConfig(CollectLog: false));
            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Foul)
                {
                    continue;
                }

                bool blind = e.Cell.Row < center;
                bool called = string.Equals(e.Detail, "foul", StringComparison.Ordinal);
                bool unseen = string.Equals(e.Detail, "unseen", StringComparison.Ordinal);
                if (blind && called)
                {
                    calledOnBlindSide++;
                }
                else if (blind && unseen)
                {
                    unseenOnBlindSide++;
                }
                else if (!blind && called)
                {
                    calledElsewhere++;
                }
            }
        }

        Assert.Equal(0, calledOnBlindSide);
        Assert.True(unseenOnBlindSide > 0, "el emparejamiento brutal tiene que producir faltas en el lado ciego, o el test no mide nada (Regla J)");
        Assert.True(calledElsewhere > 0, "el árbitro tuerto tiene que seguir pitando fuera de su lado ciego");
    }

    /// <summary>La fila central (la única con Pitch.Rows impar) no ciega nunca, aunque esté pegada al lado ciego.</summary>
    [Fact]
    public void TheCentreRowIsNeverBlind()
    {
        var brutal = TestMatches.Brutal(Catalog);
        var oneEyed = brutal with { Referee = brutal.Referee with { Trait = RefereeTrait.OneEyed, BlindSide = RefereeSide.Top } };
        int center = Pitch.Rows / 2;

        int calledOnCentre = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var result = Simulator.Run(oneEyed, seed, Catalog, new SimConfig(CollectLog: false));
            calledOnCentre += result.Events.Count(e =>
                e.Type == EventType.Foul && e.Cell.Row == center && string.Equals(e.Detail, "foul", StringComparison.Ordinal));
        }

        Assert.True(calledOnCentre > 0, "la fila central tiene que seguir pitando faltas señaladas");
    }

    /// <summary>
    /// Casero (ADR 0158 §2, enmienda de "casero"): su ×1,5 de criterio SOLO amplifica lo que se mueve EN
    /// CONTRA del jugador (equipo 0, W-15) -a su favor se queda en 100%-. El emparejamiento brutal pone al
    /// agresivo como visitante (equipo 1), así que aquí se invierte: el propio equipo (0) es el que entra
    /// duro, para que las acciones sucias que hay que castigar sean justo las que el casero amplifica.
    /// </summary>
    [Fact]
    public void AHomerRefereeShiftsMoreAgainstThePlayerThanANeutralOne()
    {
        var brutal = TestMatches.Brutal(Catalog);
        // Equipo 0 pasa a ser el agresivo/sucio: las faltas que se castigan son las del propio equipo,
        // que es exactamente lo que RF-061 (enmienda de "casero") dice que el casero amplifica.
        var reversed = new MatchSetup(brutal.Away, brutal.Home, brutal.Referee);

        int neutralBias = 0;
        int homerBias = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var neutral = reversed with { Referee = reversed.Referee with { Trait = RefereeTrait.Neutral, InitialBias = 0 } };
            var homer = reversed with { Referee = reversed.Referee with { Trait = RefereeTrait.Homer, InitialBias = 0 } };
            neutralBias += Simulator.Run(neutral, seed, Catalog, new SimConfig(CollectLog: false)).Report.FinalBias;
            homerBias += Simulator.Run(homer, seed, Catalog, new SimConfig(CollectLog: false)).Report.FinalBias;
        }

        Assert.True(homerBias < neutralBias, $"criterio acumulado casero {homerBias} frente a neutro {neutralBias}");
    }

    /// <summary>
    /// La otra mitad de la enmienda, aislada de la varianza de un partido entero (los agregados por
    /// semilla no sirven aquí: en cuanto el criterio diverge entre dos árbitros, las tiradas posteriores
    /// divergen con él, igual que advierte <c>RefereeAndAbilitiesTests.TheInitialCriterionChangesTheMatch</c>).
    /// <see cref="MatchEngine.ScaledBiasShift"/> es la única función que decide la amplificación del
    /// casero y no consume RNG: se llama directamente, sin jugar ningún tick.
    /// </summary>
    [Fact]
    public void AHomerRefereeDoesNotAmplifyShiftsInThePlayersFavour()
    {
        var setup = With(RefereeTrait.Homer);
        var engine = new MatchEngine(setup, 1, Catalog, SimConfig.Default);

        // team 1 (el rival) comete la falta: el desplazamiento resultante es A FAVOR del jugador
        // (equipo 0), así que el casero no lo toca -se queda igual que un neutro, ×1-.
        Assert.Equal(10, engine.ScaledBiasShift(team: 1, points: 10));

        // team 0 (el jugador) comete la falta: el desplazamiento es EN CONTRA suya, y ahí sí entra el
        // ×1,5 del casero (tuning.referee.traits.homer.biasShiftPercent).
        Assert.Equal(15, engine.ScaledBiasShift(team: 0, points: 10));
    }

    /// <summary>Un árbitro neutro no amplifica nada en ninguna dirección: ×1 siempre.</summary>
    [Fact]
    public void ANeutralRefereeNeverAmplifiesAnyBiasShift()
    {
        var setup = With(RefereeTrait.Neutral);
        var engine = new MatchEngine(setup, 1, Catalog, SimConfig.Default);

        Assert.Equal(10, engine.ScaledBiasShift(team: 0, points: 10));
        Assert.Equal(10, engine.ScaledBiasShift(team: 1, points: 10));
    }

    /// <summary>Estricto y permisivo amplifican/atenúan en las dos direcciones, sin distinguir equipo.</summary>
    [Theory]
    [InlineData(RefereeTrait.Strict, 15)]
    [InlineData(RefereeTrait.Lenient, 5)]
    public void StrictAndLenientScaleBothTeamsEqually(RefereeTrait trait, int expected)
    {
        var setup = With(trait);
        var engine = new MatchEngine(setup, 1, Catalog, SimConfig.Default);

        Assert.Equal(expected, engine.ScaledBiasShift(team: 0, points: 10));
        Assert.Equal(expected, engine.ScaledBiasShift(team: 1, points: 10));
    }
}
