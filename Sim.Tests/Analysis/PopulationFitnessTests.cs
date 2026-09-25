using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Los ocho criterios de aceptación de §22.7 para el analizador de adecuación de población, más los
/// casos-frontera que §22 exige tratar explícitamente. Todo puro: ni un solo partido simulado.
/// </summary>
public sealed class PopulationFitnessTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;
    public PopulationFitnessTests(ITestOutputHelper output) => _output = output;

    private static PopulationFitnessResult Analyze(string perkId, Race race, Position? role) =>
        PopulationFitness.Analyze(Catalog.Perks.All.Single(p => p.Id == perkId), Catalog, race, role);

    // --- Criterio 1: puro y barato (los 94 sin simular nada) ------------------------------------------

    [Fact]
    public void AnalyzesTheWholeCatalogWithoutSimulatingAnything()
    {
        foreach (var perk in Catalog.Perks.All)
        {
            var result = PopulationFitness.Analyze(perk, Catalog, Race.Human, Position.Defender);
            Assert.True(Enum.IsDefined(result.PopulationCheck));
            Assert.True(Enum.IsDefined(result.PositionCheck));
            Assert.True(Enum.IsDefined(result.Diagnose()));
        }
    }

    // --- Criterio 2: reproduce los 11 casos ya medidos en §21 ------------------------------------------

    [Theory]
    [InlineData("bulwark_stance", "Bulwark", "Dwarf")]
    [InlineData("back_to_back", "Bulwark", "Dwarf")]
    // shadow_marker (nearAlly(actor,'Brute',2)) se borró del catálogo (revisor, 18 sep 2026). No se
    // sustituye: back_to_back, dos líneas más arriba, ya ejercita nearAlly() en esta misma batería — lo
    // único que shadow_marker aportaba era la etiqueta 'Brute' en vez de 'Bulwark', irrelevante para lo
    // que este test comprueba (que la extracción de estilo funciona, no qué etiqueta concreta lleva).
    [InlineData("fine_touch", "Fine", "Elf")]
    [InlineData("brute_boots", "Brute", "Orc")]
    [InlineData("blood_tithe", "Brute", "Orc")]
    [InlineData("first_touch_school", "Fine", "Elf")]
    // fine_orchestra (teammatesWithTag(owner,'Fine') > 2) se borró del catálogo (revisor, 18 sep 2026). No
    // se sustituye: first_touch_school, la línea de arriba, ya ejercita teammatesWithTag()+'Fine' —
    // fine_orchestra era la misma forma con un umbral distinto (>2 en vez de >1), que este test no distingue.
    [InlineData("bruised_knuckles", "Brute", "Orc")]
    [InlineData("crowd_control", "Fine", "Elf")]
    public void StyleGatedPerksAreDiagnosedAsWrongPopulationOnHumanAndAdequateOnTheirAffineRace(
        string perkId, string style, string affineRace)
    {
        var expectedStyle = Enum.Parse<StyleTag>(style);
        var expectedRace = Enum.Parse<Race>(affineRace);

        var onHuman = Analyze(perkId, Race.Human, Position.Defender);
        Assert.Equal(expectedStyle, onHuman.RequiredStyle);
        Assert.Equal(expectedRace, onHuman.AffineRace);
        Assert.Equal(PopulationVerdict.WrongPopulation, onHuman.PopulationCheck);
        Assert.Equal(ExposureDiagnosis.WrongPopulation, onHuman.Diagnose());

        var onAffine = Analyze(perkId, expectedRace, Position.Defender);
        Assert.Equal(PopulationVerdict.Adequate, onAffine.PopulationCheck);
    }

    [Theory]
    [InlineData("cold_focus", PlayerAction.Shoot)]      // §21.6: ni con Undead sube — trigger SHOT
    [InlineData("crowd_control", PlayerAction.Dribble)] // §21.6: 0% incluso con Elf — trigger DRIBBLE_ATTEMPTED
    public void ActionTriggeredPerksMeasuredOnADefenderAreDiagnosedAsWrongPosition(string perkId, PlayerAction action)
    {
        var onDefender = Analyze(perkId, Race.Human, Position.Defender);

        Assert.Equal(TriggerActorRole.PerformsAction, onDefender.ActorRole);
        Assert.Equal(action, onDefender.RequiredAction);
        Assert.Equal(Position.Forward, onDefender.RequiredRole); // argmax de data/ai/weights.json
        Assert.Equal(PositionVerdict.WrongPosition, onDefender.PositionCheck);

        var onForward = Analyze(perkId, Race.Human, Position.Forward);
        Assert.Equal(PositionVerdict.Adequate, onForward.PositionCheck);
    }

    [Fact]
    public void CannonPositionDependencyComesFromTheEffectNotTheTriggerBecauseItActivatesAtMatchStart()
    {
        // El caso que destapó el hueco de §22 al escribir los criterios de aceptación: cannon se dispara
        // en MATCH_START (exposición 100% medida en §18), así que el eje del DISPARADOR no ve nada. Lo que
        // ata el perk a un rol es que su shootRangeBonusCells solo lo consume la lógica de tiro
        // (Utility.cs:1016/1291) — el segundo eje, el del EFECTO.
        var onDefender = Analyze("cannon", Race.Human, Position.Defender);

        Assert.Equal(TriggerActorRole.NoCarrierAction, onDefender.ActorRole);
        Assert.Null(onDefender.RequiredAction);                  // el disparador no exige nada
        Assert.Equal(PlayerAction.Shoot, onDefender.EffectAction); // el efecto sí
        Assert.Equal(Position.Forward, onDefender.RequiredRole);
        Assert.Equal(PositionVerdict.WrongPosition, onDefender.PositionCheck);
        Assert.Equal(ExposureDiagnosis.WrongPosition, onDefender.Diagnose());

        Assert.Equal(PositionVerdict.Adequate, Analyze("cannon", Race.Human, Position.Forward).PositionCheck);
    }

    [Fact]
    public void LastDitchIsAlsoEffectDrivenButItsRoleIsTheDefenderItWasAlreadyMeasuredOn()
    {
        // El rol que consume la acción es el Defensa. El analizador debe decir "adecuado", no "mal rol".
        //
        // El ejemplo cambia de perk, no de propiedad: hasta el 25 sep 2026 esto se comprobaba con
        // `own_third_anchor`, que pasó a derribar y ya no tiene acción de efecto. `last_ditch` es la
        // misma forma —modifyProbability(tackle) condicionado a la zona propia— y el eje que este test
        // protege, que el rol sale del EFECTO y no del disparador, lo sigue cubriendo además su pareja
        // `cannon`, que sigue en MATCH_START.
        var result = Analyze("last_ditch", Race.Human, Position.Defender);

        Assert.Equal(TriggerActorRole.PerformsAction, result.ActorRole);
        Assert.Equal(PlayerAction.Tackle, result.EffectAction);
        Assert.Equal(Position.Defender, result.RequiredRole); // argmax de Tackle = Defensa (255)
        Assert.Equal(PositionVerdict.Adequate, result.PositionCheck);
    }

    [Fact]
    public void CrowdControlHasBothAxesWrongAndReportsBothSeparately()
    {
        // Único caso medido con los DOS ejes mal: el desempate documentado reporta población primero,
        // pero el registro conserva los dos veredictos (§22.3).
        var result = Analyze("crowd_control", Race.Human, Position.Defender);

        Assert.Equal(PopulationVerdict.WrongPopulation, result.PopulationCheck);
        Assert.Equal(PositionVerdict.WrongPosition, result.PositionCheck);
        Assert.Equal(ExposureDiagnosis.WrongPopulation, result.Diagnose());
    }

    // --- Criterio 8 y casos fijos ---------------------------------------------------------------------

    [Fact]
    public void SafetyNetPositionIsFixedByThePerkNotByTheHarness()
    {
        var result = Analyze("safety_net", Race.Human, Position.Goalkeeper);
        Assert.Equal(PositionVerdict.FixedByPerk, result.PositionCheck);
        Assert.NotEqual(ExposureDiagnosis.WrongPosition, result.Diagnose());
    }

    [Fact]
    public void IronGatePopulationIsFixedByThePerkOwnRace()
    {
        var result = Analyze("iron_gate", Race.Dwarf, Position.Defender);
        Assert.Equal(PopulationVerdict.FixedByPerk, result.PopulationCheck);
        Assert.NotEqual(ExposureDiagnosis.WrongPopulation, result.Diagnose());
    }

    [Theory]
    [InlineData("iron_price")]
    [InlineData("iron_gate")]
    public void InjuryTriggeredPerksTreatTheCarrierAsVictimAndAreNeverWrongPosition(string perkId)
    {
        // §22.2/criterio 8: el actor de un INJURY es la VÍCTIMA (MatchEngine.cs:2568). El portador no
        // ejecuta nada, y no hay dato del que derivar qué rol lo sufre más -> Unknown, nunca WrongPosition.
        var result = Analyze(perkId, Race.Human, Position.Defender);

        Assert.Equal(TriggerActorRole.SuffersAction, result.ActorRole);
        Assert.Equal(PositionVerdict.Unknown, result.PositionCheck);
        Assert.NotEqual(ExposureDiagnosis.WrongPosition, result.Diagnose());
    }

    // --- Criterio 6 (especificidad): los que NO dependen de estilo ni de acción ------------------------

    [Theory]
    [InlineData("own_third_anchor")] // zona propia + TACKLE
    [InlineData("last_ditch")]       // zona + TACKLE
    [InlineData("game_management")]  // marcador + TACKLE
    public void ZoneOrScoreGatedTacklePerksNeedNoStyleAndAreAdequateOnADefender(string perkId)
    {
        // Especificidad (criterio 6): el analizador tiene que saber CALLARSE. Estos tres se activan por
        // zona o marcador, no por estilo, y su acción (tackle) ya la ejecuta el Defensa con el que se
        // miden: ni población ni posición son la causa de nada.
        var result = Analyze(perkId, Race.Human, Position.Defender);

        Assert.Null(result.RequiredStyle);
        Assert.Equal(PopulationVerdict.NoStyleRequired, result.PopulationCheck);
        Assert.Equal(Position.Defender, result.RequiredRole); // argmax de Tackle = Defensa (255)
        Assert.Equal(PositionVerdict.Adequate, result.PositionCheck);
        Assert.Equal(ExposureDiagnosis.GenuinelyRare, result.Diagnose());
    }

    // --- Criterio 4: no inventa ------------------------------------------------------------------------

    [Fact]
    public void NoPerkGetsAStyleVerdictFromAnUnmappedConditionFunction()
    {
        foreach (var perk in Catalog.Perks.All)
        {
            var (style, known) = PopulationFitness.ExtractRequiredStyle(perk.Condition);
            if (style is null)
            {
                continue;
            }

            Assert.True(known);
            bool usesMappedFunction =
                perk.Condition.Contains("hasTag", StringComparison.Ordinal)
                || perk.Condition.Contains("nearAlly", StringComparison.Ordinal)
                || perk.Condition.Contains("nearOpponent", StringComparison.Ordinal)
                || perk.Condition.Contains("teammatesWithTag", StringComparison.Ordinal);
            Assert.True(usesMappedFunction, $"{perk.Id}: veredicto de estilo sin una función NCalc mapeada — {perk.Condition}");
        }
    }

    [Fact]
    public void UnmappedTriggersStayUnknownInsteadOfGuessing()
    {
        // Un disparador cuyo sitio de emisión no se ha leído (p. ej. GOAL, RECOVERY) no puede producir
        // ni una acción ni un rol: Unknown, que se proyecta a LOW_EXPOSURE, el fallback honesto.
        var (role, action, directRole) = PopulationFitness.ClassifyTrigger(Underleague.Sim.Events.EventType.Goal);

        Assert.Equal(TriggerActorRole.Unknown, role);
        Assert.Null(action);
        Assert.Null(directRole);
    }

    // --- Criterio 5: invariante de seguridad ------------------------------------------------------------

    [Fact]
    public void EveryDiagnosisIsNonPassForAllNinetyFourPerks()
    {
        // Los cuatro motivos son etiquetas de POR QUÉ la exposición quedó baja. Ninguno es un estado de
        // aprobación: ninguno coincide con el estado que ScreeningRunner usa para SCREENING_PASS.
        var passState = BalanceState.Validating;

        foreach (var perk in Catalog.Perks.All)
        {
            foreach (var role in Enum.GetValues<Position>())
            {
                var diagnosis = PopulationFitness.Analyze(perk, Catalog, Race.Human, role).Diagnose();
                Assert.Contains(diagnosis, new[]
                {
                    ExposureDiagnosis.WrongPopulation, ExposureDiagnosis.WrongPosition,
                    ExposureDiagnosis.GenuinelyRare, ExposureDiagnosis.LowExposure,
                });
                Assert.NotEqual(passState.ToString(), diagnosis.ToString());
            }
        }
    }

    // --- Criterio 7: cero constantes nuevas -------------------------------------------------------------

    [Fact]
    public void VerdictsAreOrdinalSoTheyDoNotDependOnAnyNewNumericThreshold()
    {
        // La prueba de que no hay un corte inventado: el veredicto de población de un perk de estilo es
        // "¿es esta la raza de peso máximo?", así que TODA raza que no sea la afín da WrongPopulation,
        // por muy alto que sea su peso — no hay un "suficientemente alto" en ninguna parte.
        var perk = Catalog.Perks.All.Single(p => p.Id == "bulwark_stance");
        foreach (var race in Catalog.Races)
        {
            var result = PopulationFitness.Analyze(perk, Catalog, race.Id, Position.Defender);
            var expected = race.Id == Race.Dwarf ? PopulationVerdict.Adequate : PopulationVerdict.WrongPopulation;
            Assert.Equal(expected, result.PopulationCheck);
            _output.WriteLine($"{race.Id}: peso Bulwark={result.WeightInTestedRace} -> {result.PopulationCheck}");
        }
    }
}
