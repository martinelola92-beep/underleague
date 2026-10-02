using Underleague.Sim.Analysis;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Bets;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>ADR 0157 (enmienda del 2 oct 2026), Regla J: el instrumento de apuestas se valida antes de creerse la medida.</summary>
public sealed class BetHitCensusTests
{
    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    private static RunPlayResult Play(ulong seed, BetDoctrine doctrine)
    {
        var files = TestData.LoadAllFiles();
        var bosses = BossCatalog.FromJson(files);
        var setup = Systems.NewRunSetup("blacksmith_club", Race.Human, files) with { GeneratedQuality = 50 };
        return RunPolicy.Play(setup, seed, SystemsTestSupport.Catalog, Systems, bosses, RunPolicyOptions.Default with { BetDoctrine = doctrine });
    }

    [Fact]
    public void TheCellLayoutCoversEveryBetKind()
    {
        Assert.Equal(RunPolicy.BetCellCount, Enum.GetValues<BetKind>().Length * 5);
    }

    [Fact]
    public void KnownCellsGiveTheKnownCensus()
    {
        // Valor conocido: 10 apuestas BloodBeforeGoals en dificultad 3, 4 cumplidas, neto +7; el resto vacío.
        var cells = new int[RunPolicy.BetCellCount * 3];
        int cell = (((int)BetKind.BloodBeforeGoals * 5) + 2) * 3;
        cells[cell] = 10;
        cells[cell + 1] = 4;
        cells[cell + 2] = 7;
        var run = Play(1, BetDoctrine.Never) with { BetCells = cells };
        var census = BetHitCensus.Compute(new[] { run, run }, Systems.Bets);

        var hit = census.Single(c => c.Kind == BetKind.BloodBeforeGoals && c.Difficulty == 3);
        Assert.Equal(20, hit.Taken);
        Assert.Equal(8, hit.Met);
        Assert.Equal(14, hit.NetGold);
        Assert.Equal(40.0, hit.MeasuredPercent, 6);
        Assert.Equal(Systems.Bets.Find(BetKind.BloodBeforeGoals)!.FrequencyBasisPointsFor(3) / 100.0, hit.AnnouncedPercent, 6);
        Assert.Equal(Math.Sqrt(0.4 * 0.6 / 20) * 100.0, hit.StdErrPercent, 6);
        Assert.Equal(0, census.Where(c => c != hit).Sum(c => c.Taken));
        Assert.Equal(Enum.GetValues<BetKind>().Length * 5, census.Count);
    }

    [Fact]
    public void TakingABetDoesNotTouchTheMatchRngOfTheFirstMatch()
    {
        // Regla J, el control: la oferta sale de su propio flujo (OfferStream) y tomarla solo mueve oro, así que el
        // PRIMER partido de la run (antes de que el oro distinto cambie ninguna compra) es el mismo con Never y con
        // Blind: mismos eventos, mismos ticks. Si no lo fuera, apostar consumiría RNG compartido.
        var files = TestData.LoadAllFiles();
        var bosses = BossCatalog.FromJson(files);
        for (ulong seed = 1; seed <= 6; seed++)
        {
            string First(BetDoctrine doctrine)
            {
                string? first = null;
                var setup = Systems.NewRunSetup("blacksmith_club", Race.Human, files) with { GeneratedQuality = 50 };
                RunPolicy.Play(
                    setup,
                    seed,
                    SystemsTestSupport.Catalog,
                    Systems,
                    bosses,
                    RunPolicyOptions.Default with { BetDoctrine = doctrine },
                    (before, node, matchSetup, result, summary) =>
                        first ??= string.Join(";", result.Events.Select(e => $"{e.Type}@{e.Tick}:{e.Actor}:{e.Team}")));
                return first!;
            }

            Assert.Equal(First(BetDoctrine.Never), First(BetDoctrine.Blind));
        }
    }

    [Fact]
    public void NeverNeverBetsAndTheBlindPolicyBetsForReal()
    {
        // Control y caso positivo: la doctrina Never no deja rastro; la Blind apuesta, y su libro cuadra por
        // construcción (apuestas, cumplidas y oro neto por celda suman lo que dicen los totales de la run).
        int blindBets = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var never = Play(seed, BetDoctrine.Never);
            Assert.Equal(0, never.BetsTaken);
            Assert.Null(never.BetCells);

            var blind = Play(seed, BetDoctrine.Blind);
            blindBets += blind.BetsTaken;
            if (blind.BetCells is { } cells)
            {
                int taken = 0, net = 0;
                for (int c = 0; c < RunPolicy.BetCellCount; c++)
                {
                    taken += cells[c * 3];
                    net += cells[(c * 3) + 2];
                    Assert.InRange(cells[(c * 3) + 1], 0, cells[c * 3]);
                }

                Assert.Equal(blind.BetsTaken, taken);
                Assert.Equal(blind.BetNetGold, net);
            }
            else
            {
                Assert.Equal(0, blind.BetsTaken);
            }
        }

        Assert.True(blindBets > 0, "la política Blind no apostó ni una vez en 12 runs");
    }
}
