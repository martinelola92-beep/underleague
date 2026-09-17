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
/// Fase de revisión de CALIBRACIÓN (19 sep 2026, encargo explícito tras §20.5): estrictamente
/// diagnóstica. No toca código de producción, umbrales ni el circuito de lote — solo mide, con partidos
/// reales, para responder "¿tenemos evidencia suficiente para justificar cambiar algo del protocolo, y
/// qué experimento mínimo la produciría?" para los tres huecos de §19/§20.4 (back_to_back, bulwark_stance,
/// cannon). Ningún test de este fichero cambia ScreeningRunner/ComparativeSafetyCheck/PairedBalanceHarness.
/// </summary>
public sealed class CalibrationDiagnosticsTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Quality = 50;
    private const int Level = 4;
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private readonly ITestOutputHelper _output;
    public CalibrationDiagnosticsTests(ITestOutputHelper output) => _output = output;

    private sealed record CarrierMatchStat(bool CarrierHasTag, int Activations);

    /// <summary>Igual que PairedBalanceHarness.RunWithEligibleCarrier, pero con RAZA explícita (para medir contra la población donde la etiqueta de estilo es común, no la "neutral" por defecto).</summary>
    private static List<CarrierMatchStat> MeasureWithRace(PerkDefinition perk, string requiredTag, Race race, int rosters, ulong seed)
    {
        var stats = new List<CarrierMatchStat>();
        var config = new SimConfig(CollectLog: false, Trace: false);

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

            bool hasTag = home.Players[carrierSlot].HasTag(requiredTag);
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
                long activations = result.Report.PerksSummary
                    .Where(s => string.Equals(s.PerkId, perk.Id, StringComparison.Ordinal) && s.OwnerId == carrierId)
                    .Sum(s => s.Activations);
                stats.Add(new CarrierMatchStat(hasTag, (int)activations));
            }
        }

        return stats;
    }

    // ------------------------------------------------------------------------------------------------
    // 1. back_to_back / bulwark_stance — ¿qué población de roster expone realmente la sinergia?
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void BulwarkStanceOnADwarfRosterVsHumanRoster()
    {
        // data/races/*.json: Human.Bulwark=6, Dwarf.Bulwark=75 (la raza "muro" del catálogo real).
        var perk = Catalog.Perks.All.Single(p => p.Id == "bulwark_stance");
        var human = MeasureWithRace(perk, "Bulwark", Race.Human, rosters: 20, seed: 1);
        var dwarf = MeasureWithRace(perk, "Bulwark", Race.Dwarf, rosters: 20, seed: 1);

        double humanExposure = 100.0 * human.Count(s => s.Activations > 0) / human.Count;
        double dwarfExposure = 100.0 * dwarf.Count(s => s.Activations > 0) / dwarf.Count;

        _output.WriteLine($"bulwark_stance (Human, styleTagWeights.Bulwark=6): {humanExposure:F1}% ({human.Count} partidos)");
        _output.WriteLine($"bulwark_stance (Dwarf, styleTagWeights.Bulwark=75): {dwarfExposure:F1}% ({dwarf.Count} partidos)");
        _output.WriteLine("Conclusión esperada: la exposición debería escalar con el peso de estilo de la raza, no con N.");

        Assert.True(dwarfExposure > humanExposure);
    }

    [Fact]
    public void BackToBackOnADwarfRosterVsHumanRoster()
    {
        // back_to_back necesita un ALIADO (no el propio portador) con la etiqueta Bulwark a 2 casillas en
        // el instante de una entrada — con roster Dwarf, ~75% de los 6 compañeros la llevan (casi
        // garantizado que exista al menos uno en el equipo), frente a ~30% de probabilidad de que exista
        // AL MENOS UNO en un roster Human (1-0.94^6).
        var perk = Catalog.Perks.All.Single(p => p.Id == "back_to_back");
        var human = MeasureWithRace(perk, "Bulwark", Race.Human, rosters: 20, seed: 1);
        var dwarf = MeasureWithRace(perk, "Bulwark", Race.Dwarf, rosters: 20, seed: 1);

        double humanExposure = 100.0 * human.Count(s => s.Activations > 0) / human.Count;
        double dwarfExposure = 100.0 * dwarf.Count(s => s.Activations > 0) / dwarf.Count;

        _output.WriteLine($"back_to_back (Human): {humanExposure:F1}% ({human.Count} partidos, {human.Sum(s => s.Activations)} activaciones)");
        _output.WriteLine($"back_to_back (Dwarf): {dwarfExposure:F1}% ({dwarf.Count} partidos, {dwarf.Sum(s => s.Activations)} activaciones)");
    }

    [Fact]
    public void ShadowMarkerIsTheSameShapeAsBackToBackWithADifferentStyleTag()
    {
        // Comparador interno (mismo lote de 24, no medido aún porque el circuito paró en cannon):
        // shadow_marker usa EXACTAMENTE la misma forma (trigger=TACKLE, nearAlly(actor,'Brute',2),
        // ProbabilityBonus/injuriesPerMatch) que back_to_back, con 'Brute' en vez de 'Bulwark'. Orc es la
        // raza "Brute" (75%), igual que Dwarf es la raza "Bulwark".
        var perk = Catalog.Perks.All.Single(p => p.Id == "shadow_marker");
        Assert.Equal("nearAlly(actor,'Brute',2)", perk.Condition);

        var human = MeasureWithRace(perk, "Brute", Race.Human, rosters: 20, seed: 1);
        var orc = MeasureWithRace(perk, "Brute", Race.Orc, rosters: 20, seed: 1);

        double humanExposure = 100.0 * human.Count(s => s.Activations > 0) / human.Count;
        double orcExposure = 100.0 * orc.Count(s => s.Activations > 0) / orc.Count;

        _output.WriteLine($"shadow_marker (Human, Brute=10): {humanExposure:F1}% ({human.Count} partidos)");
        _output.WriteLine($"shadow_marker (Orc, Brute=75): {orcExposure:F1}% ({orc.Count} partidos)");
        _output.WriteLine("Mismo patrón de forma que back_to_back — confirma que no es un caso aislado, es la familia de perks 'sinergia de estilo'.");
    }

    [Fact]
    public void SafetyNetIsTheSameConditionShapeButWithAnAlwaysPresentPositionTag()
    {
        // safety_net: nearAlly(actor,'Defender',3) — misma forma de condición (nearAlly + trigger propio),
        // pero 'Defender' es una etiqueta de POSICIÓN, siempre presente exactamente 2 veces por plantilla
        // (TeamGenerator.StarterPositions), no una etiqueta de estilo con probabilidad de raza. Sirve como
        // el punto de calibración "de qué exposición hablamos cuando la etiqueta referenciada es común".
        // No es ReadyForScreening hoy (probabilidad 'save' es INFO, sin banda — DesignReview) pero mide lo
        // mismo que interesa aquí: cuántas veces se cumple una condición de proximidad dinámica cuando el
        // ingrediente que hace falta SIEMPRE existe en la plantilla.
        var perk = Catalog.Perks.All.Single(p => p.Id == "safety_net");
        var stats = MeasureWithRace(perk, "Defender", Race.Human, rosters: 20, seed: 1);
        double exposure = 100.0 * stats.Count(s => s.Activations > 0) / stats.Count;

        _output.WriteLine($"safety_net (Human, positionOnly=Goalkeeper, nearAlly 'Defender' SIEMPRE presente): {exposure:F1}% ({stats.Count} partidos)");
        _output.WriteLine("Referencia de 'techo alto': si esto sale bajo también, el problema es 'proximidad dinámica en 2-3 casillas'");
        _output.WriteLine("en sí, no la rareza de la etiqueta — hipótesis a distinguir con este mismo dato.");
    }

    // ------------------------------------------------------------------------------------------------
    // 2. bulwark_stance — ¿el 50% puede ser un criterio universal, o necesita familia de protocolo distinta?
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void CatalogWideStaticStyleTagConditionPerksAllShareTheSameTagsRequiredGap()
    {
        // Los cinco perks reales con condición hasTag(owner/actor, ESTILO) — no son solo bulwark_stance.
        var staticStyleTagPerkIds = new[] { "bruised_knuckles", "brute_boots", "bulwark_stance", "cold_focus", "fine_touch" };
        foreach (var id in staticStyleTagPerkIds)
        {
            var perk = Catalog.Perks.All.Single(p => p.Id == id);
            _output.WriteLine($"{id}: condition=\"{perk.Condition}\" tagsRequired=[{string.Join(",", perk.TagsRequired)}] race={perk.Race}");
            Assert.Empty(perk.TagsRequired); // el patrón es sistemático, no un error aislado de bulwark_stance
            Assert.Null(perk.Race);
        }

        _output.WriteLine("");
        _output.WriteLine("Los cinco SIN excepción dejan tagsRequired vacío pese a exigir una etiqueta de estilo en su condición.");
        _output.WriteLine("Sistemático en 5/5 => patrón de diseño deliberado (RF-068: el perk consulta etiquetas, se deja a la");
        _output.WriteLine("elección del jugador equiparlo donde rinda), NO una inconsistencia de datos aislada de bulwark_stance.");
    }

    // ------------------------------------------------------------------------------------------------
    // 3. cannon — ventana de efecto real y volumen de simulación necesario.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void CannonCarrierTimeInTheEffectiveWindowWithTheBallInPlay()
    {
        const int baseRangeCells = 8;
        const int effectiveRangeCells = 11; // +3 de cannon
        var perk = Catalog.Perks.All.Single(p => p.Id == "cannon");
        var config = new SimConfig(CollectLog: false, Trace: true);
        var race = perk.Race ?? Race.Human;

        long ticksWithBall = 0;
        long ticksInWindow = 0;
        int matches = 0;

        for (int roster = 0; roster < 20; roster++)
        {
            var homeRng = RngStreams.Generation(1, roster);
            var awayRng = RngStreams.Generation(1, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", race, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", race, Quality, 100001, Level);

            int carrierSlot = PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
            if (carrierSlot < 0)
            {
                continue;
            }

            var armedPlayers = home.Players.ToList();
            armedPlayers[carrierSlot] = armedPlayers[carrierSlot] with { Perks = new[] { perk.Id } };
            var armedHome = home with { Players = armedPlayers };
            int carrierId = armedPlayers[carrierSlot].Id;

            ulong matchSeed = RngStreams.MatchSeed(1, roster * 2);
            var setup = new MatchSetup(armedHome, away, Referee);
            var result = Simulator.Run(setup, matchSeed, Catalog, config);
            matches++;
            var trace = result.Trace;
            if (trace is null)
            {
                continue;
            }

            int slot = -1;
            for (int i = 0; i < trace.Players.Count; i++)
            {
                if (trace.Players[i].Id == carrierId)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                continue;
            }

            int team = trace.Players[slot].Team;
            for (int frame = 0; frame < trace.FrameCount; frame++)
            {
                if (!trace.OnPitchAt(frame, slot) || trace.BallOwnerAt(frame) != carrierId)
                {
                    continue;
                }

                ticksWithBall++;
                var pos = trace.PositionAt(frame, slot);
                var goal = Pitch.GoalCenter(team);
                float distance = Vec2.Distance(pos, goal);
                if (distance > baseRangeCells && distance <= effectiveRangeCells)
                {
                    ticksInWindow++;
                }
            }
        }

        double fraction = ticksWithBall > 0 ? 100.0 * ticksInWindow / ticksWithBall : 0.0;
        _output.WriteLine($"cannon: {matches} partidos armados medidos (con traza)");
        _output.WriteLine($"  ticks con balón en posesión del portador = {ticksWithBall}");
        _output.WriteLine($"  de esos, ticks en la ventana de efecto (8,11] casillas del área = {ticksInWindow} ({fraction:F2}%)");
        _output.WriteLine("  esto acota la OPORTUNIDAD real de que el bonus de cannon decida algo: aunque decidiera SIEMPRE que");
        _output.WriteLine("  se da la ventana, la tasa de veces que el portador está en esa ventana con el balón limita cuánto");
        _output.WriteLine("  puede moverse shotsPerMatch por partido.");
    }

    [Fact]
    public void CannonSimulationVolumeNeededForAPlausibleDelta()
    {
        // Estimación analítica (§5.5, misma fórmula, ningún umbral nuevo): con la varianza YA observada de
        // shotsPerMatch (§19.3, run real de cannon), ¿cuántos partidos/brazo harían falta para que un
        // delta candidato (p. ej. 0.5 tiros/partido, una fracción razonable de la banda 7-15) supere
        // 2×error_estándar?
        var perk = Catalog.Perks.All.Single(p => p.Id == "cannon");
        var run = PairedBalanceHarness.RunWithEligibleCarrier(Catalog, perk, rosters: 20, seed: 1);
        var armedShots = run.ArmedMatches.Select(m => (double)m.Shots).ToList();
        double variance = BalancePowerCheck.SampleVariance(armedShots);

        _output.WriteLine($"cannon: varianza observada de shotsPerMatch (n={armedShots.Count}) = {variance:F3}");

        foreach (double candidateDelta in new[] { 0.1, 0.25, 0.5, 1.0 })
        {
            // |delta| >= 2 * sqrt(var/n + var/n) = 2*sqrt(2*var/n)  =>  n >= 8*var/delta^2 (con las dos varianzas iguales, aproximación)
            double nNeeded = 8.0 * variance / (candidateDelta * candidateDelta);
            _output.WriteLine($"  delta candidato={candidateDelta:F2}: n necesario por brazo (aprox., misma varianza en los dos) ≈ {nNeeded:F0} partidos");
        }

        _output.WriteLine("  referencia: el lote de Tuning de §5.2 ya usa 400 partidos/brazo combinando semillas — comparar si eso");
        _output.WriteLine("  bastaría para el delta candidato más pequeño con esta varianza, antes de proponer más muestra todavía.");
    }
}
