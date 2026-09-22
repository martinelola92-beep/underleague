using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Forense de los cinco casos del primer screening real (§18.3, encargo del 19 sep 2026): SIN cambiar
/// ningún umbral/banda/regla, determina la causa de cada escalada leyendo el camino de código y midiendo
/// cantidades ya definidas (oportunidades de disparo, media/varianza/error estándar) sobre los MISMOS
/// datos que ya produjo el screening real — no es una segunda pasada de decisión, es instrumentación de
/// diagnóstico. No modifica <c>ScreeningRunner</c>/`PairedBalanceHarness` ni ningún umbral existente.
/// </summary>
public sealed class ScreeningForensicsTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const Race NeutralRace = Race.Human;
    private const int Quality = 50;
    private const int Level = 4;
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private readonly ITestOutputHelper _output;
    public ScreeningForensicsTests(ITestOutputHelper output) => _output = output;

    private sealed record CarrierMatchStat(int CarrierTackles, bool CarrierHasBulwarkTag, int Activations);

    /// <summary>
    /// Mismo patrón exacto de generación/selección de portador que
    /// <c>PairedBalanceHarness.RunWithEligibleCarrier</c> (mismas semillas, mismo criterio de
    /// elegibilidad), pero conservando el <c>PlayerMatchStats</c> del portador — el harness de producción
    /// solo devuelve <c>MatchSummary</c> agregado del partido, no basta para "oportunidades por jugador"
    /// (punto 1 del forense). Solo lectura de resultados ya definidos, sin tocar ninguna regla.
    /// </summary>
    private static List<CarrierMatchStat> MeasureCarrierOpportunities(PerkDefinition perk, int rosters, ulong seed)
    {
        var stats = new List<CarrierMatchStat>();
        var config = new SimConfig(CollectLog: false, Trace: false);
        var race = perk.Race ?? NeutralRace;

        for (int roster = 0; roster < rosters; roster++)
        {
            var homeRng = RngStreams.Generation(seed, roster);
            var awayRng = RngStreams.Generation(seed, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", race, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", race, Quality, 100001, Level);

            int carrierSlot = PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
            if (carrierSlot < 0)
            {
                continue;
            }

            bool hasBulwark = home.Players[carrierSlot].HasTag("Bulwark");
            var armedPlayers = home.Players.ToList();
            armedPlayers[carrierSlot] = armedPlayers[carrierSlot] with { Perks = new[] { perk.Id } };
            var armedHome = home with { Players = armedPlayers };
            int carrierId = armedPlayers[carrierSlot].Id;

            for (int direction = 0; direction < 2; direction++)
            {
                bool subjectAway = direction == 1;
                ulong matchSeed = RngStreams.MatchSeed(seed, (roster * 2) + direction);
                var setup = subjectAway ? new MatchSetup(away, armedHome, Referee) : new MatchSetup(armedHome, away, Referee);
                var result = Simulator.Run(setup, matchSeed, Catalog, config);
                var playerStats = result.Report.Players.Single(p => p.PlayerId == carrierId);
                long activations = result.Report.PerksSummary
                    .Where(s => string.Equals(s.PerkId, perk.Id, StringComparison.Ordinal) && s.OwnerId == carrierId)
                    .Sum(s => s.Activations);
                // Denominador de "% de intentos de entrada que activan el efecto": el disparador es el
                // evento TACKLE, que publican por igual la entrada al portador y la sin balón, así que la
                // exposición son los dos contadores sumados (ADR 0125 D1 los separó; leer solo uno inflaría
                // la tasa hasta ~3x si el portador del perk es defensa).
                stats.Add(new CarrierMatchStat(playerStats.Tackles + playerStats.OffBallTackles, hasBulwark, (int)activations));
            }
        }

        return stats;
    }

    // ------------------------------------------------------------------------------------------------
    // 0. Hallazgo transversal: ¿a qué POSICIÓN cae el portador de los cinco perks escalados? Ninguno de
    //    los cinco declara positionOnly/tagsRequired — PerkAssignment.Eligible/FindEligible recorren los 7
    //    titulares en orden fijo (i=0..6) y devuelven el PRIMERO elegible; TeamGenerator.StarterPositions[0]
    //    es SIEMPRE Goalkeeper (Sim/Generation/TeamGenerator.cs). Si el portero es sistemáticamente el
    //    elegido para estos cinco, es la explicación estructural común a varios de los cinco casos.
    // ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("back_to_back")]
    [InlineData("bulwark_stance")]
    [InlineData("blood_scent")]
    [InlineData("bloodhound")]
    [InlineData("cannon")]
    public void CarrierPositionForEachEscalatedPerk(string perkId)
    {
        // ANTES del arreglo de §19 (commit de este mismo forense): los cinco caían en Goalkeeper (slot 0)
        // porque FindEligible recorría 0..6 y devolvía el primero — confirmado con este mismo test antes
        // de tocar PairedBalanceHarness.cs. DESPUÉS del arreglo (preferir campo, cf. FindEligibleCarrierSlot):
        // los cinco deberían caer en un jugador de campo, ya que ninguno de los cinco restringe posición.
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        var race = perk.Race ?? NeutralRace;
        var homeRng = RngStreams.Generation(1, 0);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", race, Quality, 1, Level);

        int carrierSlot = PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
        Assert.True(carrierSlot >= 0, $"{perkId} debería encontrar un portador elegible en la primera plantilla generada");
        var position = home.Players[carrierSlot].Position;
        _output.WriteLine($"{perkId}: portador en roster #0 (semilla 1) -> slot {carrierSlot}, posición {position}");

        Assert.NotEqual(Position.Goalkeeper, position); // regresión del arreglo de §19: ya no cae en portería si hay opción de campo
    }

    // ------------------------------------------------------------------------------------------------
    // 1. back_to_back / bulwark_stance: exposición baja — ¿accidental, de roster, de condición rara, o
    //    intencionada por diseño?
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void BackToBackOpportunityIsGatedByATeammateTagAndProximityNotJustOwnTackles()
    {
        // back_to_back: trigger=TACKLE, condition="nearAlly(actor,'Bulwark',2)" (dinámica: exige un
        // ALIADO con la etiqueta Bulwark a 2 casillas EN EL MOMENTO del tackle propio). La "oportunidad"
        // no es solo "el portador entra" — es "el portador entra Y hay un aliado Bulwark cerca". No hay
        // forma de medir "cerca en el momento" sin trazar (fuera de alcance de este forense barato); lo
        // que SÍ se puede medir sin traza es la primera condición necesaria: ¿existe siquiera un aliado
        // con la etiqueta Bulwark en la plantilla? (§races: Human styleTagWeights.Bulwark = 6/100).
        var perk = Catalog.Perks.All.Single(p => p.Id == "back_to_back");
        var stats = MeasureCarrierOpportunities(perk, rosters: 20, seed: 1);

        int totalTackleOpportunities = stats.Sum(s => s.CarrierTackles);
        int totalActivations = stats.Sum(s => s.Activations);
        int matchesWithTackle = stats.Count(s => s.CarrierTackles > 0);
        int matchesWithActivation = stats.Count(s => s.Activations > 0);

        _output.WriteLine($"back_to_back: {stats.Count} partidos armados medidos");
        _output.WriteLine($"  oportunidades de disparo del trigger (Tackles del portador, suma) = {totalTackleOpportunities}");
        _output.WriteLine($"  activaciones reales del efecto (condición nearAlly cumplida) = {totalActivations}");
        _output.WriteLine($"  % de intentos de entrada del portador que activan el efecto = {(totalTackleOpportunities > 0 ? 100.0 * totalActivations / totalTackleOpportunities : 0):F2}%");
        _output.WriteLine($"  partidos con >=1 intento de entrada del portador = {matchesWithTackle}/{stats.Count}");
        _output.WriteLine($"  partidos con >=1 activación = {matchesWithActivation}/{stats.Count}");
        _output.WriteLine("  nota: la 'oportunidad' real es más estrecha que 'el portador entra' — exige además un aliado con");
        _output.WriteLine("  la etiqueta Bulwark A 2 CASILLAS en ese instante; sin traza no se mide ese segundo filtro aquí.");

        Assert.True(totalTackleOpportunities > 0, "el portador debería intentar entradas alguna vez en 40 partidos");
    }

    [Fact]
    public void BulwarkStanceExposureCeilingMatchesTheRaceStyleTagWeightNotTheEligibilityFilter()
    {
        // bulwark_stance: trigger=MATCH_START, condition="hasTag(owner,'Bulwark')" (estática: se evalúa
        // UNA VEZ al empezar el partido). tagsRequired=[] en el propio perk (verificado en data/perks/):
        // PerkAssignment.Eligible NO exige la etiqueta Bulwark para asignar el perk (solo comprueba
        // tagsRequired/tagsForbidden/positionOnly, nunca el texto de `condition`) — así que el portador
        // elegido por el harness (y por el juego real, MISMA función) puede no tener la etiqueta que su
        // propia condición exige.
        var perk = Catalog.Perks.All.Single(p => p.Id == "bulwark_stance");
        Assert.Empty(perk.TagsRequired); // confirma la inconsistencia estructural citada arriba

        var stats = MeasureCarrierOpportunities(perk, rosters: 20, seed: 1);
        int matchesWithBulwarkTag = stats.Count(s => s.CarrierHasBulwarkTag);
        int matchesWithActivation = stats.Count(s => s.Activations > 0);

        _output.WriteLine($"bulwark_stance: {stats.Count} partidos armados medidos");
        _output.WriteLine($"  partidos donde el portador YA tenía la etiqueta Bulwark al generarse = {matchesWithBulwarkTag}/{stats.Count} ({100.0 * matchesWithBulwarkTag / stats.Count:F1}%)");
        _output.WriteLine($"  partidos con >=1 activación = {matchesWithActivation}/{stats.Count} ({100.0 * matchesWithActivation / stats.Count:F1}%)");
        _output.WriteLine("  referencia: data/races/human.json declara styleTagWeights.Bulwark = 6 (de 100) — el techo de exposición");
        _output.WriteLine("  de este perk, con CUALQUIER número de partidos, está acotado por esa probabilidad base, no por el");
        _output.WriteLine("  suelo de exposición del protocolo (50%) ni por ningún fallo de medición.");

        // La activación solo puede ocurrir en partidos donde el portador ya tenía la etiqueta (condición
        // estática evaluada una vez, sin margen para activarse sin ella).
        Assert.True(matchesWithActivation <= matchesWithBulwarkTag);
    }

    // ------------------------------------------------------------------------------------------------
    // 2. blood_scent / bloodhound: SAFETY_LIMIT — ¿el gate es interpretable con 40 partidos?
    // ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("blood_scent")]
    [InlineData("bloodhound")]
    public void SafetyLimitStatisticsAreConsistentWithSamplingNoiseAtThisSampleSize(string perkId)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        // Mismos parámetros EXACTOS que usó ScreeningRunner para este perk (20 plantillas, semilla 1,
        // exposición 100% así que no hubo remuestreo) — se reutiliza el harness de producción tal cual,
        // sin ninguna modificación, solo se leen los partidos ya generados con más detalle.
        var run = PairedBalanceHarness.RunWithEligibleCarrier(Catalog, perk, rosters: 20, seed: 1);

        var armedInjuries = run.ArmedMatches.Select(m => (double)m.Injuries).ToList();
        var controlInjuries = run.ControlMatches.Select(m => (double)m.Injuries).ToList();

        double meanArmed = armedInjuries.Average();
        double varianceArmed = BalancePowerCheck.SampleVariance(armedInjuries);
        double standardErrorArmed = Math.Sqrt(varianceArmed / armedInjuries.Count);

        double meanControl = controlInjuries.Average();
        double varianceControl = BalancePowerCheck.SampleVariance(controlInjuries);
        double combinedStandardError = BalancePowerCheck.StandardError(varianceArmed, armedInjuries.Count, varianceControl, controlInjuries.Count);
        double delta = meanArmed - meanControl;
        bool powerSufficient = BalancePowerCheck.HasSufficientPower(delta, varianceArmed, armedInjuries.Count, varianceControl, controlInjuries.Count);

        const double bandFloor = 0.30; // MatchMetrics.InjuriesPerMatch, ADR 0082
        double distanceToFloorInStandardErrors = (bandFloor - meanArmed) / standardErrorArmed;

        _output.WriteLine($"{perkId}: n_armado={armedInjuries.Count} n_control={controlInjuries.Count}");
        _output.WriteLine($"  media armada = {meanArmed:F4}, varianza armada = {varianceArmed:F4}, error estándar armado = {standardErrorArmed:F4}");
        _output.WriteLine($"  media control = {meanControl:F4}, varianza control = {varianceControl:F4}");
        _output.WriteLine($"  delta (armado-control) = {delta:F4}, error estándar combinado (Welch) = {combinedStandardError:F4}");
        _output.WriteLine($"  ¿delta armado/control distinguible del ruido (power-check, 2×SE)? {powerSufficient}");
        _output.WriteLine($"  suelo de la banda RT-056 (0.30) está a {distanceToFloorInStandardErrors:F2} errores estándar de la media armada");
        _output.WriteLine("  (menos de ~2 significa que 'por debajo del suelo' es indistinguible de una fluctuación de muestreo normal)");
        _output.WriteLine("  procedencia de la banda 0.30-0.90: ADR 0082 (docs/decisiones/), calibrada sobre 'tres semillas de 500");
        _output.WriteLine("  partidos' (1500 partidos de referencia) sin ningún perk — población de auto-juego neutro, no armado/control");
        _output.WriteLine("  de un perk concreto; el check de SAFETY_LIMIT de Screening la aplica aquí sobre 40 partidos armados.");
    }

    [Theory]
    [InlineData("blood_scent")]
    [InlineData("bloodhound")]
    public void ArmedAndControlInjurySequencesForTargetSelectionPerks(string perkId)
    {
        // blood_scent y bloodhound dieron la MISMA media/varianza exactas (0.25/0.1923) — sospechoso de
        // que ninguno de los dos mecanismos mueve Injuries EN ABSOLUTO en esta muestra concreta, igual que
        // se confirmó con cannon/shotsPerMatch. Si armado==control partido a partido, el 0.25 no es "el
        // efecto del perk", es el nivel base de esta semilla/roster a n=40 — completamente independiente
        // del perk, y el SAFETY_LIMIT sería una lectura sobre el AZAR de la semilla, no sobre el perk.
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        var run = PairedBalanceHarness.RunWithEligibleCarrier(Catalog, perk, rosters: 20, seed: 1);
        var armedInjuries = run.ArmedMatches.Select(m => m.Injuries).ToList();
        var controlInjuries = run.ControlMatches.Select(m => m.Injuries).ToList();
        bool identical = armedInjuries.SequenceEqual(controlInjuries);

        _output.WriteLine($"{perkId}: injuries armado = [{string.Join(",", armedInjuries)}]");
        _output.WriteLine($"{perkId}: injuries control = [{string.Join(",", controlInjuries)}]");
        _output.WriteLine($"¿idénticas partido a partido? {identical}");
    }

    [Fact]
    public void BloodScentAndBloodhoundShareTheSameSafetyLimitRootCause()
    {
        // Los dos son TargetSelection (modifyTackleBias/modifyMarkBias): cambian A QUIÉN se entra/marca,
        // no SI se entra ni CUÁNTAS veces — no hay vía causal declarada por la que cualquiera de los dos
        // debiera mover el total de lesiones del partido (§4.2/§8: sin vía causal documentada para esta
        // categoría → si el efecto fuera real y replicado, sería SYSTEMIC_REGRESSION, no un
        // SAFETY_LIMIT esperado por diseño). Confirmado con el propio efecto de cada perk.
        var bloodScent = Catalog.Perks.All.Single(p => p.Id == "blood_scent");
        var bloodhound = Catalog.Perks.All.Single(p => p.Id == "bloodhound");

        Assert.Equal(PerkBalanceCategory.TargetSelection, PerkBalanceClassifier.Classify(bloodScent).Category);
        Assert.Equal(PerkBalanceCategory.TargetSelection, PerkBalanceClassifier.Classify(bloodhound).Category);
        Assert.Equal(EffectType.ModifyTackleBias, bloodScent.Effects[0].Type);
        Assert.Equal(EffectType.ModifyMarkBias, bloodhound.Effects[0].Type);
    }

    // ------------------------------------------------------------------------------------------------
    // 3. cannon — auditoría causal de la métrica primaria.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void CannonPrimaryEffectIsShootRangeNotShotFrequency()
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "cannon");
        var effect = perk.Effects.Single();
        Assert.Equal(EffectType.ModifyTraitScalar, effect.Type);
        Assert.Equal(TraitScalarKind.ShootRangeBonusCells, effect.Scalar);

        // Auditoría causal real (no del _doc, del motor): Sim/Engine/Utility.cs EvaluateShoot (ADR 0030
        // §1) consume ShootRangeBonusCells DIRECTAMENTE en la utilidad de disparar — "rangeCenti =
        // (ShootBaseRangeCells + p.ShootRangeBonusCells) * 100"; más allá de rangeCenti se resta
        // "ShootBeyondRangePenaltyPerCell * (distancia - rango)". No hay corte binario (el comentario del
        // propio motor lo dice: "nadie tiene prohibido tirar de lejos, simplemente casi nadie debería
        // querer") — es una RAMPA de utilidad, no una puerta. shotsPerMatch SÍ es la métrica causalmente
        // correcta: si el efecto mueve algo, es "el jugador decide tirar en vez de otra acción" (RF-098,
        // la propia tabla de utilidad), y eso es exactamente lo que shotsPerMatch cuenta.
        _output.WriteLine("cannon: EvaluateShoot usa ShootRangeBonusCells para desplazar dónde EMPIEZA la rampa de penalización");
        _output.WriteLine("por distancia (ADR 0030 §1) — no hay puerta binaria. shotsPerMatch SÍ es causalmente válida: el efecto,");
        _output.WriteLine("si existe, se manifiesta como 'Shoot gana la comparación de utilidad frente a Pass/Dribble/etc. en la");
        _output.WriteLine("ventana de distancia ampliada', y eso es justo lo que cuenta shotsPerMatch. No se cambia la métrica.");
    }

    [Fact]
    public void CannonEffectiveWindowIsNarrowGivenTheRealWeights()
    {
        // data/ai/weights.json: shootBaseRangeCells=8, shootBeyondRangePenaltyPerCell=300,
        // shootDistancePenaltyPerCell=50, shootInRangeBonus=388. cannon da +3 casillas de rango (8->11):
        // SOLO en la banda de distancia 8-11 casillas cambia algo (dentro de 8 ya no había rampa extra sin
        // cannon; más allá de 11 la rampa vuelve a aplicar igual con o sin cannon). En esa banda estrecha
        // el efecto es grande (evita hasta 300*3=900 de penalización de rampa, frente a un shootInRangeBonus
        // base de 388 — un salto de utilidad considerable), pero SOLO si el portador está exactamente en
        // esa banda de distancia Y la decisión de disparar ya es competitiva frente a otras acciones.
        const int baseRangeCells = 8;
        const int bonusCells = 3;
        const int beyondRangePenaltyPerCell = 300;
        int effectiveRangeCells = baseRangeCells + bonusCells;

        _output.WriteLine($"cannon: ventana de efecto = distancia en ({baseRangeCells}, {effectiveRangeCells}] casillas del área.");
        _output.WriteLine($"  utilidad evitada en el borde (distancia={effectiveRangeCells}): {beyondRangePenaltyPerCell * bonusCells} puntos (frente a shootInRangeBonus=388)");
        _output.WriteLine("  hipótesis a validar (no confirmable sin más datos): el portador elegible en las 40 plantillas medidas");
        _output.WriteLine("  (Sim.Analysis.PerkAssignment.Eligible, sin restricción de posición) puede no pasar suficiente tiempo");
        _output.WriteLine("  exactamente en esa banda con el balón en juego como para que la rampa evitada llegue a decidir nada.");

        Assert.Equal(11, effectiveRangeCells);
    }

    [Fact]
    public void ExactZeroDeltaOn160MatchesMeansTheSampleNeverEnteredTheEffectiveWindowNotThatTheMetricIsWrong()
    {
        // CORRECCIÓN de una hipótesis anterior de este mismo forense: un delta EXACTAMENTE 0.0000 en 160
        // partidos NO demuestra "métrica mal elegida" — la auditoría causal (ver los dos tests de arriba)
        // confirma que shotsPerMatch SÍ es la métrica correcta para shootRangeBonusCells. Lo que sí
        // demuestra la secuencia partido a partido IDÉNTICA es que, en esta muestra concreta (este
        // portador, estas 40 plantillas, semilla 1), la ventana de distancia 8-11 casillas donde el efecto
        // podría cambiar algo simplemente NUNCA resultó decisiva — ni una sola vez en 40 partidos. Eso es
        // "potencia/tamaño de muestra insuficiente para una ventana de efecto estrecha", no un bug de
        // tooling.
        var perk = Catalog.Perks.All.Single(p => p.Id == "cannon");
        var run = PairedBalanceHarness.RunWithEligibleCarrier(Catalog, perk, rosters: 20, seed: 1);
        var armedShots = run.ArmedMatches.Select(m => m.Shots).ToList();
        var controlShots = run.ControlMatches.Select(m => m.Shots).ToList();

        _output.WriteLine($"cannon: shots armado por partido = [{string.Join(",", armedShots)}]");
        _output.WriteLine($"cannon: shots control por partido = [{string.Join(",", controlShots)}]");
        bool identicalPerMatch = armedShots.SequenceEqual(controlShots);
        _output.WriteLine($"¿la secuencia de tiros por partido es IDÉNTICA partido a partido (no solo la media)? {identicalPerMatch}");

        if (identicalPerMatch)
        {
            _output.WriteLine("CONCLUSIÓN (revisada): la métrica es causalmente válida (test de arriba); lo que esto demuestra es");
            _output.WriteLine("que, con el portador y las plantillas de esta muestra, el mecanismo nunca llegó a su ventana de");
            _output.WriteLine("efecto (8-11 casillas de distancia, con una decisión de tiro ya competitiva). Clasificación: potencia");
            _output.WriteLine("estadística insuficiente para un efecto de ventana estrecha, agravado porque el harness no controla");
            _output.WriteLine("ni reporta la posición/distancia típica del portador elegible (hueco de auditoría, no de clasificación).");
        }
    }
}
