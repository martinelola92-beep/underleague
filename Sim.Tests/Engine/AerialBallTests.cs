using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0139 — <b>el balón aéreo, el duelo y la segunda jugada</b>.
///
/// <para>La ADR 0135 le dio altura al balón y la recogida nunca la consultó: un balón a dos casillas del
/// césped se cogía con el pie igual que uno parado. Estos tests fijan las tres reglas que sustituyen a
/// aquello —controlar, cabecear, no llegar— y la que convierte la segunda jugada en algo que ocurre:
/// <b>un balón en disputa no se lo lleva automáticamente el más cercano</b>.</para>
///
/// <para>Ninguno afirma una frecuencia. El encargo de este pass prohíbe calibrar, así que lo que se
/// demuestra es que las ramas existen, que son alcanzables en partidos reales y que tienen consecuencias.
/// Cuántas veces deben ocurrir es la fase siguiente.</para>
/// </summary>
public sealed class AerialBallTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MatchEngine Engine() => TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));

    // ---------------------------------------------------------------- las tres alturas

    /// <summary>
    /// Un balón <b>por encima del alcance del salto</b> no lo toca nadie, aunque lo tengan encima. Es la
    /// regla que hace que un centro y un despeje existan como jugada: si a cualquier altura se pudiera
    /// recoger, pasar por arriba no serviría de nada.
    /// </summary>
    [Fact]
    public void PorEncimaDelAlcanceDelSaltoNadieLoToca()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);

        var spot = new Vec2(8f, 3.5f);
        engine.PlaceForTest(mine, spot);
        engine.SetLooseForTest(spot, new Vec2(0f, 0f), velocityZ: 0f);
        engine.SetBallHeightForTest(3f);

        engine.StepBallForTest();

        Assert.Equal(-1, engine.BallOwnerIdForTest);
    }

    /// <summary>
    /// A ras de suelo el balón se <b>controla</b>: la recogida de toda la vida, sin duelo ni cabezazo.
    /// Sin este caso intacto, la ADR habría cambiado el juego entero en vez de añadirle el aire.
    /// </summary>
    [Fact]
    public void ARasDeSueloElBalonSeControla()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);

        var spot = new Vec2(8f, 3.5f);
        engine.PlaceForTest(mine, spot);
        engine.SetLooseForTest(spot, new Vec2(0f, 0f), velocityZ: 0f);

        engine.StepBallForTest();

        Assert.Equal(engine.PlayerAtForTest(mine).Id, engine.BallOwnerIdForTest);
    }

    /// <summary>
    /// A la altura de un salto, y solo, el jugador <b>cabecea</b>: no se queda el balón, lo prolonga. Es
    /// la mitad que convierte el juego aéreo en fútbol y no en un cambio de dueño — el balón sigue vivo y
    /// hay segunda jugada.
    /// </summary>
    [Fact]
    public void ALaAlturaDeUnSaltoSeCabeceaYElBalonSigueSuelto()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);

        var spot = new Vec2(8f, 3.5f);
        engine.PlaceForTest(mine, spot);
        engine.SetLooseForTest(spot, new Vec2(0f, 0f), velocityZ: 0f);
        engine.SetBallHeightForTest(1.2f);

        engine.StepBallForTest();

        Assert.Equal(-1, engine.BallOwnerIdForTest);
        Assert.True(engine.BallHeightForTest > 0f, "un balón cabeceado no se cae al suelo en el acto");
        Assert.True(
            engine.BallVelocityForTest.X > 0f,
            "el cabezazo tenía que salir hacia donde ataca el que salta (equipo 0 ataca hacia +X)");
    }

    // ---------------------------------------------------------------- la disputa

    /// <summary>
    /// <b>El más cercano ya no gana siempre.</b> Con dos rivales sobre el mismo balón suelto, quién se lo
    /// lleva depende de lo que aportan, no sólo de la geometría — que es lo que el encargo pedía
    /// explícitamente para la segunda jugada.
    ///
    /// <para>Se comprueba de la única forma que no es tautológica: repitiendo la misma disputa con
    /// semillas distintas y viendo que <b>los dos resultados ocurren</b>. Si ganara siempre el más
    /// cercano, el segundo nunca aparecería.</para>
    /// </summary>
    [Fact]
    public void UnBalonDisputadoNoSeLoLlevaSiempreElMasCercano()
    {
        bool homeWon = false;
        bool awayWon = false;

        for (ulong seed = 1; seed <= 40 && !(homeWon && awayWon); seed++)
        {
            var engine = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, seed));
            int mine = engine.OutfieldIndexForTest(0, 0);
            int theirs = engine.OutfieldIndexForTest(1, 0);

            var spot = new Vec2(8f, 3.5f);
            engine.PlaceForTest(mine, spot + new Vec2(0.20f, 0f));
            engine.PlaceForTest(theirs, spot + new Vec2(0.24f, 0f));
            engine.SetLooseForTest(spot, new Vec2(0f, 0f), velocityZ: 0f);

            engine.StepBallForTest();

            int owner = engine.BallOwnerIdForTest;
            homeWon |= owner == engine.PlayerAtForTest(mine).Id;
            awayWon |= owner == engine.PlayerAtForTest(theirs).Id;
        }

        Assert.True(homeWon, "el más cercano no ganó ni una vez: la disputa no está ponderando la distancia");
        Assert.True(awayWon, "ganó siempre el más cercano: la disputa sigue siendo pura geometría");
    }

    // ---------------------------------------------------------------- alcanzable en partido real

    /// <summary>
    /// <b>Las ramas nuevas ocurren en partidos de verdad</b>, no sólo en escenarios montados a mano. Es el
    /// criterio de éxito que el encargo fija para este pass: alcanzabilidad, no frecuencia. El umbral es
    /// «alguna vez en cien partidos», deliberadamente flojo — subirlo sería empezar a calibrar.
    /// </summary>
    [Fact]
    public void EnPartidosRealesHayDespejesYDuelosAereos()
    {
        int clearances = 0;
        int duels = 0;

        for (ulong seed = 1; seed <= 100; seed++)
        {
            var report = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default).Report;

            clearances += report.Clearances[0] + report.Clearances[1];
            duels += report.AerialDuels[0] + report.AerialDuels[1];
        }

        Assert.True(clearances > 0, "en cien partidos no se despejó ni una vez: la rama no es alcanzable");
        Assert.True(duels > 0, "en cien partidos no hubo un solo duelo aéreo: la rama no es alcanzable");
    }
}
