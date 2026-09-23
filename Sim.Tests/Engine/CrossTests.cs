using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0136: <b>centrar</b> — un pase alto al área rival que un compañero mejor colocado remata <b>sin
/// controlar</b>.
///
/// <para><b>Qué demuestra cada bloque, y por qué no basta el lote.</b> El lote de `/Balance` mide
/// agregados: puede decir cuántos centros hay y cuántos goles salen de ellos, pero no puede decir que un
/// centro concreto vuele por encima de quien lo interceptaría, ni que el que lo recibe no llegue a ser
/// dueño del balón ni un tick. Esas son afirmaciones sobre <b>un</b> vuelo y se comprueban colocando el
/// vuelo a mano. Lo que sí es del lote —si el centro mejora la apertura media de los tiros de BA-E— vive
/// en la medición, no aquí.</para>
///
/// <para>El reparto de atributos del remate (fuerza contra técnica) se fija aquí como <b>invariante de
/// dato</b>, no como un resultado estadístico: es una decisión de diseño de la ADR, y un test que la
/// midiera con goles estaría midiendo la calibración, que sí puede moverse.</para>
/// </summary>
public sealed class CrossTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MatchEngine Engine() => TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));

    // ------------------------------------------------------------------ el alcance es una esfera

    /// <summary>
    /// Con el balón <b>en el suelo</b>, el alcance esférico devuelve exactamente la distancia en el plano.
    /// No es una obviedad: es lo que garantiza que los tres pases rasos sigan dando el mismo número que
    /// antes de existir la altura, y por tanto que cualquier desviación del lote sea atribuible al centro
    /// y no a haber tocado la intercepción de todo el mundo.
    /// </summary>
    [Fact]
    public void OnTheGroundTheSphericalReachIsExactlyThePlaneDistance()
    {
        var from = new Vec2(4.25f, 2.75f);
        var ball = new Vec2(5.5f, 3.5f);

        Assert.Equal(Vec2.Distance(from, ball), MatchEngine.ReachDistance(from, ball, 0f));
        Assert.Equal(Vec2.Distance(from, ball), MatchEngine.ReachDistance(from, ball, -0.5f));
    }

    /// <summary>Esfera y no cilindro (decisión 3 de la ADR 0135): cuanto más alto va el balón, más lejos queda.</summary>
    [Fact]
    public void TheHigherTheBallTheFurtherAwayItIs()
    {
        var from = new Vec2(8f, 3.5f);
        var ball = new Vec2(8.5f, 3.5f);

        float ground = MatchEngine.ReachDistance(from, ball, 0f);
        float low = MatchEngine.ReachDistance(from, ball, 0.5f);
        float high = MatchEngine.ReachDistance(from, ball, 1.5f);

        Assert.True(low > ground, "subir el balón tenía que alejarlo");
        Assert.True(high > low, "el alcance no crece con la altura: es un cilindro, no una esfera");
        Assert.Equal(1.5f, MatchEngine.ReachDistance(from, from, 1.5f), 4);
    }

    // ------------------------------------------------------------------ el centro vuela alto

    /// <summary>
    /// Un centro pasa <b>por encima</b> del radio de intercepción a mitad de vuelo, y un pase corto entre
    /// los mismos dos puntos no se despega del suelo. Es la única diferencia física entre los dos, y es la
    /// razón de ser de la acción: sin ella el centro sería un pase largo con otro nombre.
    /// </summary>
    [Fact]
    public void ACrossFliesOverTheInterceptionRadiusAndAPassDoesNot()
    {
        float radius = Catalog.Tuning.Pass.InterceptRadiusCells;

        float crossPeak = PeakHeightOf(PlayerAction.Cross);
        float passPeak = PeakHeightOf(PlayerAction.ShortPass);

        Assert.True(
            crossPeak > radius,
            $"el centro no supera el radio de intercepción: pico {crossPeak:F3} contra {radius:F2} casillas");
        Assert.Equal(0f, passPeak);
    }

    /// <summary>
    /// El centro <b>vuelve al suelo</b> en el destino. Importa porque la llegada se compara en el plano
    /// (revisión de arquitectura): si el balón llegara alto, el rematador no podría alcanzarlo nunca y la
    /// acción no existiría, sólo su primera mitad.
    /// </summary>
    [Fact]
    public void ACrossComesBackDownWhereItLands()
    {
        var engine = Engine();
        LaunchCross(engine, PlayerAction.Cross);

        float last = 0f;
        for (int i = 0; i < 200 && engine.BallInFlightForTest && !engine.BallIsShotForTest; i++)
        {
            engine.StepFlightForTest();
            last = engine.BallHeightForTest;
        }

        Assert.True(last < 0.05f, $"el centro llegó por el aire, a {last:F3} casillas de altura");
    }

    // ------------------------------------------------------------------ el remate

    /// <summary>
    /// El que recibe un centro <b>remata sin controlar</b>: al acabar el vuelo del centro el balón está
    /// otra vez en vuelo y es un <b>tiro</b>, y en ningún momento del proceso ha tenido dueño a la vista
    /// del partido. Es la mitad que convierte la acción en una jugada en vez de en un pase largo.
    /// </summary>
    [Fact]
    public void TheReceiverVolleysTheCrossInsteadOfControllingIt()
    {
        var engine = Engine();
        LaunchCross(engine, PlayerAction.Cross);

        Assert.True(engine.BallIsCrossForTest, "el vuelo no se marcó como centro");

        bool sawAnOwner = false;
        for (int i = 0; i < 200 && !engine.BallIsShotForTest; i++)
        {
            engine.StepFlightForTest();
            if (!engine.BallInFlightForTest)
            {
                break;
            }

            sawAnOwner |= engine.BallOwnerIdForTest >= 0;
        }

        Assert.True(engine.BallIsShotForTest, "el centro llegó y nadie lo remató");
        Assert.False(sawAnOwner, "el rematador llegó a controlar el balón antes de rematar");
        Assert.False(engine.BallIsCrossForTest, "el vuelo siguió marcado como centro después del remate");
    }

    /// <summary>
    /// <b>Lo que hace que el centro exista, comprobado de verdad.</b> Los tests de arriba miden el
    /// <b>pico</b> del vuelo; éste pone un defensa <b>en el pasillo</b> y comprueba lo único que importa:
    /// que a media altura <b>no se puede interceptar</b> —la esfera no llega— y que al salir y al llegar,
    /// con el balón bajo, <b>sí</b>. Sin esta pareja, «el centro vuela por encima del que lo
    /// interceptaría» sería una afirmación de la ADR sin nada detrás (lo señaló la revisión independiente).
    /// </summary>
    [Fact]
    public void ADefenderInTheLaneCannotReachTheCrossMidFlightButCanAtBothEnds()
    {
        float radius = Catalog.Tuning.Pass.InterceptRadiusCells;
        var engine = Engine();
        LaunchCross(engine, PlayerAction.Cross);

        var heights = new List<(float Height, float PlaneDistance)>();
        for (int i = 0; i < 200 && engine.BallInFlightForTest && !engine.BallIsShotForTest; i++)
        {
            engine.StepFlightForTest();
            if (!engine.BallInFlightForTest)
            {
                break;
            }

            // Un defensa justo debajo del balón: la distancia en el plano es cero, así que lo único que
            // puede impedir la intercepción es la altura.
            heights.Add((engine.BallHeightForTest, 0f));
        }

        Assert.True(heights.Count >= 4, $"el vuelo duró {heights.Count} ticks: muy corto para probar nada");

        bool blockedSomewhere = false;
        bool reachableAtSomeEnd = false;
        for (int i = 0; i < heights.Count; i++)
        {
            float reach = MatchEngine.ReachDistance(new Vec2(0f, 0f), new Vec2(0f, 0f), heights[i].Height);
            bool reachable = reach < radius;
            if (i > heights.Count / 4 && i < heights.Count * 3 / 4)
            {
                Assert.False(
                    reachable,
                    $"a media altura ({heights[i].Height:F3}) un defensa justo debajo alcanzaba el balón: el centro no vuela por encima de nadie");
                blockedSomewhere = true;
            }
            else if (reachable)
            {
                reachableAtSomeEnd = true;
            }
        }

        Assert.True(blockedSomewhere, "el vuelo no tuvo tramo central que comprobar");
        Assert.True(
            reachableAtSomeEnd,
            "el centro nunca bajó lo bastante como para poder ser interceptado: sería un pase invulnerable, no un centro");
    }

    /// <summary>
    /// Un pase raso, en cambio, <b>siempre</b> es alcanzable por un defensa que lo tenga encima. Es la
    /// otra mitad de la comprobación: la esfera no debilita la intercepción del pase normal.
    /// </summary>
    [Fact]
    public void AGroundPassIsAlwaysReachableByADefenderOnTopOfIt()
    {
        var engine = Engine();
        LaunchCross(engine, PlayerAction.ShortPass);

        for (int i = 0; i < 200 && engine.BallInFlightForTest && !engine.BallIsShotForTest; i++)
        {
            engine.StepFlightForTest();
            if (!engine.BallInFlightForTest)
            {
                break;
            }

            float reach = MatchEngine.ReachDistance(new Vec2(0f, 0f), new Vec2(0f, 0f), engine.BallHeightForTest);
            Assert.True(reach < Catalog.Tuning.Pass.InterceptRadiusCells);
        }
    }

    /// <summary>
    /// <b>Un centro cuyo receptor se cae degenera a pase, no a centro fantasma.</b> Entre la decisión y el
    /// lanzamiento pasan cinco ticks (<c>states.passingTicks</c>), y si el receptor deja de estar en el
    /// campo <c>LaunchPass</c> lo sustituye por el compañero más adelantado — que no cumple <b>ninguna</b>
    /// de las dos precondiciones duras. La revisión independiente encontró que con el sustituto el vuelo
    /// seguía saliendo alto y el sustituto remataba. Ya no.
    /// </summary>
    [Fact]
    public void ACrossWhoseReceiverIsGoneDegradesToAGroundPass()
    {
        var engine = Engine();
        int passer = engine.OutfieldIndexForTest(0, 0);
        int chosen = engine.OutfieldIndexForTest(0, 1);
        int other = engine.OutfieldIndexForTest(0, 2);

        // El receptor elegido se cae del campo antes de que el pase salga.
        engine.SendOffForTest(chosen);
        engine.ForcePassForTest(
            team: 0,
            passerIndex: passer,
            passerAt: new Vec2(15.5f, 0.5f),
            receiverIndex: other,
            receiverAt: new Vec2(13.5f, Pitch.Rows / 2f),
            action: PlayerAction.Cross,
            intendedReceiverIndex: chosen);

        Assert.False(engine.BallIsCrossForTest, "el centro salió igual con un receptor que no era el elegido");
        Assert.Equal(0f, PeakOfCurrentFlight(engine));
    }

    // ------------------------------------------------------------------ la identidad del remate, fijada en el dato

    /// <summary>
    /// <b>El tiro es colocar y el remate es llegar y empujarla.</b> La decisión de la ADR 0136 se fija
    /// aquí como invariante del dato publicado, no como un resultado medido: si algún día alguien iguala
    /// los factores, las dos acciones vuelven a ser la misma con otro nombre y este test lo dice antes de
    /// que lo diga un lote.
    /// </summary>
    [Fact]
    public void TheVolleyLeansOnStrengthAndTheShotOnTechnique()
    {
        var shot = Catalog.Tuning.Shot;
        var cross = Catalog.Tuning.Cross;

        Assert.True(shot.TechniqueFactor > shot.StrengthFactor, "el tiro tiene que apoyarse en la técnica");
        Assert.True(cross.VolleyStrengthFactor > cross.VolleyTechniqueFactor, "el remate tiene que apoyarse en la fuerza");
        Assert.True(cross.VolleyStrengthFactor > shot.StrengthFactor, "el remate tiene que dar MÁS fuerza que el tiro");
        Assert.True(cross.VolleyTechniqueFactor < shot.TechniqueFactor, "el remate tiene que dar MENOS técnica que el tiro");
        Assert.True(cross.VolleyOffTargetPenalty > 0, "rematar sin controlar tiene que costar puntería");
    }

    /// <summary>
    /// La comba del centro tiene que superar el radio de intercepción, o la acción no hace nada que un
    /// pase largo no hiciera. Es la relación entre dos valores de <c>/data</c> que viven en ficheros
    /// distintos, así que nadie la ve al mover uno de los dos.
    /// </summary>
    [Fact]
    public void TheCrossArcClearsTheInterceptionRadius()
    {
        float peak = Catalog.Tuning.Cross.PeakHeightCellsMilli / 1000f;
        float radius = Catalog.Tuning.Pass.InterceptRadiusCells;

        Assert.True(
            peak > radius,
            $"el centro no se levanta lo suficiente: {peak:F3} contra {radius:F2}");
    }

    /// <summary>
    /// Un centro es un balón <b>largo</b> al área: por debajo de <c>crossMinCells</c> no hay centro. Sin
    /// esa precondición la acción se colaría dentro del área rival como un pase corto con comba, que es
    /// otra cosa y no la que decidió la ADR.
    /// </summary>
    [Fact]
    public void ACrossIsALongBallAndHasAMinimumDistance()
    {
        var context = Catalog.Ai.Context;

        Assert.True(context.CrossMinCells > 0f, "un centro sin distancia mínima no es un centro");
        Assert.True(
            context.CrossMinCells < context.CrossMaxCells,
            $"el alcance del centro está vacío: {context.CrossMinCells} .. {context.CrossMaxCells}");
    }

    // ------------------------------------------------------------------ la apertura, que es la métrica de BA-E

    /// <summary>
    /// La apertura es la misma magnitud con la que se midió BA-E: <b>100 de frente</b> a la portería y
    /// <b>0 desde la propia línea de fondo</b>. Se fija aquí porque es la cifra con la que se va a juzgar
    /// si el centro funcionó, y una métrica que cambia de definición entre la medición del problema y la
    /// de la solución no demuestra nada.
    /// </summary>
    [Fact]
    public void ApertureIsOneInFrontOfGoalAndZeroFromTheByline()
    {
        var goal = Pitch.GoalCenter(0);

        Assert.Equal(100, Utility.ApertureCenti(new Vec2(12f, Pitch.Rows / 2f), goal));
        Assert.Equal(0, Utility.ApertureCenti(new Vec2(Pitch.Columns, 0.5f), goal));
        Assert.True(
            Utility.ApertureCenti(new Vec2(14f, 1f), goal) < Utility.ApertureCenti(new Vec2(14f, 3.5f), goal),
            "desde la banda la apertura tiene que ser peor que desde el centro");
    }

    // ------------------------------------------------------------------ el centro ocurre de verdad

    /// <summary>
    /// En partidos de referencia se centra, y los centros acaban en remate. Sin esto todo lo demás sería
    /// una mecánica correcta que no se activa nunca — que es exactamente lo que este proyecto llama
    /// «sin evidencia de activación» y no da por bueno.
    /// </summary>
    [Fact]
    public void CrossesHappenAndEndInVolleys()
    {
        int crosses = 0;
        int volleyed = 0;
        int crossEvents = 0;

        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false));
            crosses += result.Report.Crosses[0] + result.Report.Crosses[1];
            volleyed += result.Report.CrossesVolleyed[0] + result.Report.CrossesVolleyed[1];
            foreach (var e in result.Events)
            {
                if (e.Type == EventType.Cross && e.Detail == "attempted")
                {
                    crossEvents++;
                }
            }
        }

        Assert.True(crosses > 0, "no se centró ni una vez en 60 partidos");
        Assert.True(volleyed > 0, "se centró pero no se remató ni una vez en 60 partidos");
        Assert.Equal(crosses, crossEvents);
        Assert.True(volleyed <= crosses, $"más remates ({volleyed}) que centros ({crosses})");
    }

    /// <summary>
    /// Un centro no puede existir sin rematador: con la zona de remate a cero no se centra nunca. Es la
    /// comprobación de que la precondición dura <b>descarta</b>, y no sólo penaliza — sin ella el centro
    /// sería un pase largo disponible en todo el campo.
    /// </summary>
    [Fact]
    public void WithNoFinishingZoneNobodyEverCrosses()
    {
        var catalog = WithCrossRange(targetGoalDistance: 0f);
        int crosses = 0;

        for (ulong seed = 1; seed <= 30; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, new SimConfig(CollectLog: false));
            crosses += result.Report.Crosses[0] + result.Report.Crosses[1];
        }

        Assert.Equal(0, crosses);
    }

    // ------------------------------------------------------------------ el pase raso no hereda altura

    /// <summary>
    /// <b>Un pase declara su altura, no la hereda.</b> <c>FlightArc</c> y <c>FlightTargetZ</c> los escribía
    /// sólo el tiro y nadie los limpiaba al recuperar el balón, así que el primer pase después de una
    /// parada volaba con la comba del tiro parado. Era inerte mientras ninguna regla leía la altura; con la
    /// intercepción en esfera de esta misma ADR se habría convertido en pases rasos ininterceptables.
    /// </summary>
    [Fact]
    public void AGroundPassNeverInheritsTheArcOfAPreviousCross()
    {
        var engine = Engine();
        LaunchCross(engine, PlayerAction.Cross);
        Assert.True(PeakOfCurrentFlight(engine) > 0f, "el centro no voló: el escenario no prueba nada");

        // Mismo motor, mismo balón, ahora un pase corto: tiene que salir raso.
        LaunchCross(engine, PlayerAction.ShortPass);
        Assert.Equal(0f, PeakOfCurrentFlight(engine));
    }

    // ------------------------------------------------------------------ utillería

    private static float PeakHeightOf(PlayerAction action)
    {
        var engine = Engine();
        LaunchCross(engine, action);
        return PeakOfCurrentFlight(engine);
    }

    private static float PeakOfCurrentFlight(MatchEngine engine)
    {
        float peak = 0f;
        for (int i = 0; i < 200 && engine.BallInFlightForTest && !engine.BallIsShotForTest; i++)
        {
            engine.StepFlightForTest();
            peak = MathF.Max(peak, engine.BallHeightForTest);
        }

        return peak;
    }

    /// <summary>
    /// Escenario fijo: el centrador en el cordel —la posición exacta de la que se queja BA-E— y el
    /// rematador de frente a la portería, sin nadie encima. El resto del campo se aparta para que el vuelo
    /// sea el único suceso: lo que se prueba es el vuelo, no la utilidad.
    /// </summary>
    private static void LaunchCross(MatchEngine engine, PlayerAction action)
    {
        int passer = engine.OutfieldIndexForTest(0, 0);
        int receiver = engine.OutfieldIndexForTest(0, 1);

        for (int offset = 0; offset < 6; offset++)
        {
            engine.PlaceForTest(engine.OutfieldIndexForTest(1, offset), new Vec2(2f, 0.5f + offset));
        }

        engine.ForcePassForTest(
            team: 0,
            passerIndex: passer,
            passerAt: new Vec2(15.5f, 0.5f),
            receiverIndex: receiver,
            receiverAt: new Vec2(13.5f, Pitch.Rows / 2f),
            action: action);
    }

    private static Catalog WithCrossRange(float targetGoalDistance) =>
        Catalog with
        {
            Ai = Catalog.Ai.WithContext(
                Catalog.Ai.Context with { CrossTargetGoalDistanceCells = targetGoalDistance }),
        };
}
