using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Gameplay AI Foundations Pass, P1 y P2 — la percepción compartida del equipo, <b>proteger</b> el balón y
/// <b>despejar</b>.
///
/// <para><b>Qué demuestran estos tests y qué NO.</b> El encargo del revisor prohíbe expresamente calibrar
/// este paquete contra lotes: lo que hay que demostrar no es una frecuencia —cuántas veces se protege o se
/// despeja— sino que las ramas <b>existen, son alcanzables y tienen consecuencias</b>. Por eso cada test
/// monta la situación futbolística a mano y pregunta por el comportamiento, nunca por un porcentaje. Una
/// afirmación del tipo «se protege N veces por partido» sería exactamente el tipo de test que este paquete
/// no debe escribir, porque fijaría como diseño una distribución que todavía no se ha decidido.</para>
/// </summary>
public sealed class ShieldAndClearTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MatchEngine Engine() => TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));

    /// <summary>
    /// Esquina a la que se manda a todo el que no participa en el escenario. Está <b>fuera del alcance del
    /// pase largo</b> (8 casillas) desde la casilla-hogar del delantero, que es donde se montan las pruebas
    /// del portador: si no lo estuviera, el escenario mediría «proteger contra pasar a alguien» en vez de
    /// «proteger cuando no hay a quién pasar», que es la situación que se quiere montar.
    /// </summary>
    private static readonly Vec2 OffStage = new(15.5f, 6.5f);

    /// <summary>
    /// El portador se monta en la <b>casilla-hogar del delantero</b> y no en un punto elegido a ojo. Es
    /// deliberado: la zona de acción (ADR 0028) penaliza y puede descartar los destinos lejos de casa, así
    /// que un escenario montado donde el jugador no puede estar no mediría la acción sino la correa. Y el
    /// hogar del delantero está lejos de su propia portería, así que el peligro es bajo y «despejar» no
    /// compite —que es lo que se quiere aislar aquí—.
    /// </summary>
    private static int Forward(MatchEngine engine)
    {
        for (int offset = 0; offset < 6; offset++)
        {
            int index = engine.OutfieldIndexForTest(0, offset);
            if (index >= 0 && engine.PlayerAtForTest(index).Role == Position.Forward)
            {
                return index;
            }
        }

        throw new InvalidOperationException("la plantilla de referencia no trae ningún delantero");
    }

    /// <summary>Aparta del escenario a todos menos a los índices indicados, para que el escenario sea el escenario.</summary>
    private static void IsolateAllBut(MatchEngine engine, params int[] keep)
    {
        for (int i = 0; i < 14; i++)
        {
            bool kept = false;
            for (int k = 0; k < keep.Length; k++)
            {
                kept |= keep[k] == i;
            }

            if (!kept)
            {
                engine.PlaceForTest(i, OffStage);
            }
        }
    }


    /// <summary>
    /// Comprueba que la decisión que se está examinando es <b>de verdad</b> una decisión de portador.
    ///
    /// <para>Existe por un fallo real de estos mismos tests: <c>SetOwner</c> termina decidiendo, así que el
    /// jugador quedaba ya ejecutando un tiro, ningún estado de acción permite acciones, y
    /// <c>Utility.Choose</c> devolvía su repliegue de reserva. Los dos tests en negativo —«no elige
    /// proteger», «no elige despejar»— pasaban <b>por el motivo equivocado</b>. Afirmar que la acción
    /// elegida es una acción con balón cierra ese agujero para siempre.</para>
    /// </summary>
    private static void AssertDecidedAsCarrier(PlayerAction chosen)
    {
        Assert.True(
            StateMachine.CanPerform(PlayerState.Dribbling, chosen),
            $"el escenario no llegó a ser una decisión de portador: eligió {chosen}, que no es una acción con balón");
    }

    // ---------------------------------------------------------------- P1: la percepción compartida

    /// <summary>
    /// La presión es del <b>rival más cercano</b> y se apaga con la distancia. Es el hecho sobre el que se
    /// apoyan proteger, despejar y —más adelante— el saque del portero: si no fuera un hecho compartido,
    /// cada uno de ellos lo recalcularía por su cuenta y podrían contradecirse.
    /// </summary>
    [Fact]
    public void LaPresionSaleDelRivalMasCercanoYSeApagaConLaDistancia()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);
        int theirs = engine.OutfieldIndexForTest(1, 0);

        engine.PlaceForTest(mine, new Vec2(8f, 3.5f));
        engine.PlaceForTest(theirs, new Vec2(8.2f, 3.5f));
        engine.RefreshPerceptionForTest();
        int close = engine.PressureForTest(mine);

        engine.PlaceForTest(theirs, new Vec2(12f, 3.5f));
        engine.RefreshPerceptionForTest();
        int far = engine.PressureForTest(mine);

        Assert.True(close > 0, "un rival pegado tenía que presionar");
        Assert.Equal(0, far);
        Assert.True(engine.OpennessForTest(mine) > 0, "sin nadie cerca hay que estar libre");
    }

    /// <summary>
    /// El peligro es del <b>equipo</b> y mira a la portería propia: el mismo balón es peligro para uno y
    /// oportunidad para el otro. Sin esto, «despejar» no tendría forma de distinguir sacar el balón de la
    /// línea de gol de regalarlo en el centro del campo.
    /// </summary>
    [Fact]
    public void ElPeligroEsDelEquipoYMiraASuPropiaPorteria()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);

        engine.GiveBallForTest(mine, new Vec2(1f, 3.5f));
        engine.RefreshPerceptionForTest();

        Assert.True(engine.DangerForTest(0) > engine.DangerForTest(1),
            "con el balón en su propia área, el peligro del equipo 0 tenía que ser mayor");
    }

    // ---------------------------------------------------------------- P2: proteger

    /// <summary>
    /// <b>Sin nadie apretando no se protege.</b> Es la precondición dura de la acción y describe la
    /// situación futbolística, no una calibración: proteger el balón que nadie te disputa no significa
    /// nada. Sin ella, «proteger» sería una forma de quedarse quieto impunemente.
    /// </summary>
    [Fact]
    public void SinPresionProtegerNiSiquieraSeConsidera()
    {
        var engine = Engine();
        int mine = Forward(engine);
        IsolateAllBut(engine, mine);
        engine.GiveBallForTest(mine, engine.PlayerAtForTest(mine).EffectiveHome);

        var chosen = engine.ChooseForTest(mine);
        AssertDecidedAsCarrier(chosen);
        Assert.NotEqual(PlayerAction.Shield, chosen);
    }

    /// <summary>
    /// Con un rival encima, proteger <b>es alcanzable</b>: existe al menos una situación en la que la
    /// utilidad la elige. Es el criterio de éxito que el encargo pide para este paquete —que la rama
    /// exista y se pueda llegar a ella—, no que se elija con ninguna frecuencia concreta.
    /// </summary>
    [Fact]
    public void ConUnRivalEncimaProtegerEsAlcanzable()
    {
        var engine = Engine();
        int mine = Forward(engine);
        int theirs = engine.OutfieldIndexForTest(1, 0);
        IsolateAllBut(engine, mine, theirs);

        var spot = engine.PlayerAtForTest(mine).EffectiveHome;
        engine.GiveBallForTest(mine, spot);
        engine.PlaceForTest(theirs, spot + new Vec2(0.15f, 0f));

        var chosen = engine.ChooseForTest(mine);
        Assert.True(chosen is PlayerAction.Shield,
            $"con el balón, un rival encima y ningún compañero cerca, proteger tenía que ganar; eligió {chosen} "
            + $"(estado {engine.StateForTest(mine)}, presión {engine.PressureForTest(mine)}, dueño {engine.BallOwnerIdForTest})");
    }

    /// <summary>
    /// <b>El que protege resiste con su fuerza, y por eso se le quita peor el balón.</b> Es la mitad que
    /// convierte la acción en identidad y no en un bono plano: si resistiera con la técnica, proteger
    /// sería otra cosa que se le da mejor al técnico, y ya hay una —el regate—.
    /// </summary>
    [Fact]
    public void ProtegerCuestaQuitarleElBalonAlPortador()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);
        int theirs = engine.OutfieldIndexForTest(1, 0);

        var carrier = engine.PlayerAtForTest(mine);
        var tackler = engine.PlayerAtForTest(theirs);

        int normal = engine.TackleWinChance(tackler, carrier);
        engine.ForceShieldForTest(mine);
        int shielded = engine.TackleWinChance(tackler, carrier);

        Assert.True(shielded < normal,
            $"proteger tenía que abaratar la pérdida del balón, no encarecerla: {shielded} contra {normal}");
    }

    // ---------------------------------------------------------------- P4: despejar

    /// <summary>
    /// Un despeje <b>vuela alto, no tiene destinatario y acaba suelto</b>. Las tres cosas a la vez son lo
    /// que lo distingue de un pase largo fallado, y las tres son la entrada del duelo aéreo y de la
    /// segunda jugada.
    /// </summary>
    [Fact]
    public void ElDespejeVuelaAltoSinDestinatarioYAcabaSuelto()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);
        IsolateAllBut(engine, mine);

        engine.GiveBallForTest(mine, new Vec2(1.5f, 3.5f));
        engine.ForceClearForTest(mine);

        Assert.True(engine.BallInFlightForTest, "el despeje tenía que poner el balón en vuelo");
        Assert.True(engine.BallIsClearanceForTest, "el vuelo tenía que estar marcado como despeje");
        Assert.False(engine.BallIsShotForTest);
        Assert.False(engine.BallIsCrossForTest);

        bool roseOffTheGround = false;
        for (int i = 0; i < 200 && engine.BallInFlightForTest; i++)
        {
            engine.StepFlightForTest();
            roseOffTheGround |= engine.BallHeightForTest > 0.5f;
        }

        Assert.True(roseOffTheGround, "un despeje que no se despega del suelo no es un despeje");
        Assert.False(engine.BallInFlightForTest, "el vuelo tenía que terminar");
        Assert.Equal(-1, engine.BallOwnerIdForTest);
    }

    /// <summary>
    /// El despeje <b>aleja</b> el balón de la portería propia. Es su razón de ser: si no lo alejara, sería
    /// tirar el balón, no despejarlo.
    /// </summary>
    [Fact]
    public void ElDespejeAlejaElBalonDeLaPorteriaPropia()
    {
        var engine = Engine();
        int mine = engine.OutfieldIndexForTest(0, 0);
        IsolateAllBut(engine, mine);

        var from = new Vec2(1.5f, 3.5f);
        engine.GiveBallForTest(mine, from);
        engine.ForceClearForTest(mine);

        var ownGoal = Pitch.GoalCenter(1);
        Assert.True(
            Vec2.Distance(engine.FlightTargetForTest, ownGoal) > Vec2.Distance(from, ownGoal),
            "el despeje tenía que acabar más lejos de la portería propia de lo que empezó");
    }

    /// <summary>
    /// <b>El fuerte la manda más lejos.</b> Es donde se nota quién despeja, y es el motivo de que la
    /// decisión de despejar no mire la fuerza: la fuerza no decide <i>si</i> despejas, decide <i>cuánto</i>.
    /// </summary>
    [Fact]
    public void LaFuerzaDecideCuantoLlegaElDespeje()
    {
        var engine = Engine();

        int strongest = -1;
        int weakest = -1;
        for (int offset = 0; offset < 6; offset++)
        {
            int index = engine.OutfieldIndexForTest(0, offset);
            if (index < 0)
            {
                break;
            }

            int strength = engine.PlayerAtForTest(index).Strength;
            if (strongest < 0 || strength > engine.PlayerAtForTest(strongest).Strength)
            {
                strongest = index;
            }

            if (weakest < 0 || strength < engine.PlayerAtForTest(weakest).Strength)
            {
                weakest = index;
            }
        }

        Assert.True(engine.PlayerAtForTest(strongest).Strength > engine.PlayerAtForTest(weakest).Strength,
            "la plantilla de referencia tenía que traer fuerzas distintas para poder comparar");

        var from = new Vec2(2f, 3.5f);

        engine.GiveBallForTest(weakest, from);
        engine.ForceClearForTest(weakest);
        float weakReach = engine.FlightTargetForTest.X;

        engine.GiveBallForTest(strongest, from);
        engine.ForceClearForTest(strongest);
        float strongReach = engine.FlightTargetForTest.X;

        Assert.True(strongReach > weakReach,
            $"el fuerte tenía que mandarla más lejos: {strongReach} contra {weakReach}");
    }

    /// <summary>
    /// <b>Sin peligro no se despeja.</b> Simétrico de la precondición de proteger: despejar desde el
    /// círculo central no es prudencia, es regalar el balón, y la utilidad no debe poder confundirlas.
    /// </summary>
    [Fact]
    public void SinPeligroDespejarNiSiquieraSeConsidera()
    {
        var engine = Engine();
        int mine = Forward(engine);
        IsolateAllBut(engine, mine);

        engine.GiveBallForTest(mine, engine.PlayerAtForTest(mine).EffectiveHome);

        var chosen = engine.ChooseForTest(mine);
        AssertDecidedAsCarrier(chosen);
        Assert.NotEqual(PlayerAction.Clear, chosen);
    }
}
