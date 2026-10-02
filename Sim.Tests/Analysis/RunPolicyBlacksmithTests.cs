using Underleague.Sim.Analysis;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Medical;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>ADR 0164, RF-095: cuándo la política va al herrero y cuándo al médico.</summary>
public sealed class RunPolicyBlacksmithTests
{
    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    private static (RunState State, RunPlayer Patient) Clinic(int gold, Rarity rarity, int attribute)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7, SystemsTestSupport.Catalog, Systems).WithGold(gold);
        var patient = state.Roster[2] with
        {
            PhysicalState = PhysicalState.SevereInjury,
            Rarity = rarity,
            Attributes = new Attributes(attribute, attribute, attribute, attribute, attribute),
        };
        state = state.WithPlayer(patient);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);
        return (state, patient);
    }

    private static (RunState State, int Forged, int Treated) Visit(RunState state) =>
        RunPolicy.VisitClinicForTest(state, SystemsTestSupport.Catalog, Systems.Economy, Systems, RunPolicyOptions.Default);

    [Fact]
    public void ACommonSevereGoesToTheBlacksmithEvenWhenTheDoctorIsAffordable()
    {
        var economy = Systems.Economy;
        var (state, patient) = Clinic(economy.ClinicCost + 3, Rarity.Common, 90);
        var (after, forged, treated) = Visit(state);
        Assert.Equal(1, forged);
        Assert.Equal(1, treated);
        Assert.Equal(PhysicalState.Healthy, after.GetPlayer(patient.Id).PhysicalState);
        Assert.NotEmpty(after.GetPlayer(patient.Id).Prostheses);
        Assert.True(after.Gold >= 0);
    }

    [Fact]
    public void ARareStarterWorthSevereStillGoesToTheDoctor()
    {
        var economy = Systems.Economy;
        var (state, patient) = Clinic(economy.ClinicCost + 3, Rarity.Legendary, 99);
        var (after, forged, treated) = Visit(state);
        Assert.Equal(0, forged);
        Assert.Equal(1, treated);
        Assert.Empty(after.GetPlayer(patient.Id).Prostheses);
        Assert.Equal(PhysicalState.Healthy, after.GetPlayer(patient.Id).PhysicalState);
    }

    [Fact]
    public void WithGoldBetweenTheBlacksmithAndTheDoctorTheBlacksmithTreatsTheSevereAndTheQuackKeepsNothingToDo()
    {
        var economy = Systems.Economy;
        int between = MedicalSystem.BlacksmithBasePrice(economy) + 1;
        Assert.True(between < economy.ClinicCost);
        var (state, patient) = Clinic(between, Rarity.Legendary, 99);
        var (after, forged, _) = Visit(state);
        Assert.Equal(1, forged);
        Assert.Equal(PhysicalState.Healthy, after.GetPlayer(patient.Id).PhysicalState);
        Assert.NotEmpty(after.GetPlayer(patient.Id).Prostheses);
    }

    [Fact]
    public void AWholePolicyRunSeesTheProsthesisCatalogAndUsesTheBlacksmith()
    {
        // Regla J: el instrumento se valida contra un caso conocido. Antes RecordingSystems devolvía el catálogo
        // vacío y la política jugaba runs enteras sin ver una sola prótesis; con el catálogo real y la
        // preferencia por el herrero, unas cuantas runs tienen que pasar por él.
        var files = TestData.LoadAllFiles();
        var bosses = Underleague.Sim.Run.Bosses.BossCatalog.FromJson(files);
        int forged = 0;
        int installed = 0;
        var results = new List<RunPlayResult>();
        var seen = new Dictionary<int, IReadOnlyList<RunProsthesis>>();
        void Watch(RunState before, MapNode node, MatchSetup setup, Underleague.Sim.Engine.MatchResult result, RunMatchSummary summary)
        {
            // Las prótesis nunca se pierden por jugar (subidas de nivel, lesiones, sustituciones): solo crecen.
            foreach (var player in before.Roster)
            {
                if (seen.TryGetValue(player.Id, out var earlier))
                {
                    Assert.True(
                        player.Prostheses.Take(earlier.Count).SequenceEqual(earlier) && player.Prostheses.Count >= earlier.Count,
                        $"el jugador {player.Id} ha perdido prótesis");
                }

                if (player.Prostheses.Count > 0)
                {
                    Assert.Contains(MedicalSystem.ScrapTag, player.Tags);
                    Assert.Contains(player.SpeciesTag, player.Tags);
                    Assert.Equal(player.Prostheses.Count >= MedicalSystem.ProsthesesForAutomaton, player.Tags.Contains(MedicalSystem.AutomatonTag));
                }

                seen[player.Id] = player.Prostheses;
            }
        }

        for (ulong seed = 1; seed <= 40; seed++)
        {
            seen.Clear();
            var setup = Systems.NewRunSetup("blacksmith_club", Race.Human, files) with { GeneratedQuality = 50 };
            var result = RunPolicy.Play(setup, seed, SystemsTestSupport.Catalog, Systems, bosses, null, Watch);
            forged += result.BlacksmithTreatments;
            installed += result.ProsthesesInstalled;
            results.Add(result);
        }

        Assert.True(forged > 0, $"la política no pasó nunca por el herrero: ¿ve el catálogo de prótesis? (protesis {Systems.Prostheses.All.Count})");
        Assert.True(installed >= 0);

        // Regla J, el censo del techo (ADR 0164, enmienda del 2 oct 2026): contra un caso conocido. Si alguna run
        // acabó con un jugador protésico en la plantilla, el censo lo ve; y nadie lleva más prótesis que ranuras hay.
        var rows = FullRunMetrics.Describe(results, Systems.Economy).ToDictionary(r => r.Name, r => r.Value);
        int withProsthesis = results.Count(r => r.FinalState is { } f && f.Roster.Any(p => p.Prostheses.Count > 0));
        Assert.True(withProsthesis > 0, "ninguna run acabó con un jugador protésico: el censo no tendría a quién medir");
        Assert.True(rows["maxProsthesesOnOnePlayer"] >= 1);
        Assert.True(rows["maxProsthesesOnOnePlayer"] <= Systems.Prostheses.Slots.Count);
        Assert.True(rows["maxAttributeProsthetic"] >= rows["meanBestAttributeProsthetic"]);
        Assert.InRange(rows["maxAttributeProsthetic"], 1, 99);
    }

    [Fact]
    public void TheStructuralCeilingOfTheForgeIsWhatTheDataSays()
    {
        // ADR 0164, game-design-review (degeneración): una prótesis por ranura y los atributos a 99 acotan lo que el
        // herrero puede subir a un jugador. Con el catálogo de hoy el techo de UN atributo es la suma de la mejor
        // mejora de cada ranura que lo toca (fuerza: brazo 8 + mandíbula 6 = 14). Este test no fija un balance, fija
        // la **aritmética del techo**: añadir una prótesis o subir un delta que lo mueva obliga a pasar por la ADR
        // (RT-057) en vez de cambiar un dato en silencio (Regla I).
        var catalog = Systems.Prostheses;
        var ceiling = new Dictionary<AttributeKind, int>();
        foreach (var slot in catalog.Slots)
        {
            var best = catalog.All.Where(p => p.Slot == slot && p.Kind == ProsthesisKind.Improve)
                .GroupBy(p => p.Attribute)
                .Select(g => (Attribute: g.Key, Delta: g.Max(p => p.Delta)));
            foreach (var (attribute, delta) in best)
            {
                ceiling[attribute] = ceiling.GetValueOrDefault(attribute) + delta;
            }
        }

        Assert.Equal(14, ceiling.Values.Max());
        Assert.Equal(AttributeKind.Strength, ceiling.First(kv => kv.Value == 14).Key);
        Assert.Equal(48, ceiling.Values.Sum());
    }

    [Fact]
    public void TheCensusSeesStrengthPlusFourteenFromArmAndJaw()
    {
        // Regla J: caso de valor conocido. Brazo de hierro (+8) y mandíbula de acero (+6) sobre fuerza 50 con el resto
        // por debajo: el mejor atributo del protésico es 64, y el del compañero sin prótesis, el suyo.
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7, SystemsTestSupport.Catalog, Systems);
        var plain = state.Roster[1] with { Attributes = new Attributes(40, 40, 40, 40, 40), Prostheses = Array.Empty<RunProsthesis>() };
        var forged = state.Roster[2] with { Attributes = new Attributes(50, 30, 30, 30, 30), Prostheses = Array.Empty<RunProsthesis>() };
        forged = MedicalSystem.Install(forged, Systems.Prostheses.Find("iron_arm")!);
        forged = MedicalSystem.Install(forged, Systems.Prostheses.Find("steel_jaw")!);
        Assert.Equal(64, forged.Attributes.Strength);

        var census = FullRunMetrics.ProstheticCensus(new[] { plain, forged });
        Assert.Equal(1, census.ProstheticPlayers);
        Assert.Equal(1, census.PlainPlayers);
        Assert.Equal(64, census.MaxProstheticBest);
        Assert.Equal(40, census.MaxPlainBest);
        Assert.Equal(2, census.MaxProstheses);
    }

    [Fact]
    public void TheNoBlacksmithControlMatchesTheRealArmWhereTheForgeNeverRan()
    {
        // Regla J: el control del lote (catálogo de prótesis vacío) es un control de verdad si, en las runs donde el
        // herrero real no llegó a forjar nada, las dos ramas dan la misma run (misma semilla, mismo resultado).
        var files = TestData.LoadAllFiles();
        var bosses = Underleague.Sim.Run.Bosses.BossCatalog.FromJson(files);
        var control = new Dictionary<string, string>(files, StringComparer.Ordinal) { ["prostheses/prostheses.json"] = "{\"prostheses\": []}" };
        var controlSystems = StandardRunSystems.FromJson(control);
        Assert.Empty(controlSystems.Prostheses.All);
        int compared = 0, forgedRuns = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var setup = Systems.NewRunSetup("blacksmith_club", Race.Human, files) with { GeneratedQuality = 50 };
            var real = RunPolicy.Play(setup, seed, SystemsTestSupport.Catalog, Systems, bosses, null);
            var setupControl = controlSystems.NewRunSetup("blacksmith_club", Race.Human, control) with { GeneratedQuality = 50 };
            var without = RunPolicy.Play(setupControl, seed, SystemsTestSupport.Catalog, controlSystems, bosses, null);
            Assert.Equal(0, without.BlacksmithTreatments);
            if (real.BlacksmithTreatments > 0)
            {
                forgedRuns++;
                continue;
            }

            compared++;
            Assert.Equal(without.Outcome, real.Outcome);
            Assert.Equal(without.Matches, real.Matches);
            Assert.Equal(without.Deaths, real.Deaths);
            Assert.Equal(without.GoldEarned, real.GoldEarned);
            Assert.Equal(without.GoldSpentClinic, real.GoldSpentClinic);
            Assert.Equal(without.NodesVisited, real.NodesVisited);
        }

        Assert.True(compared > 0, $"ninguna run sin forja que comparar ({forgedRuns} con forja)");
    }
}
