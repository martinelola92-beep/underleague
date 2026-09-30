using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BM-B (ADR 0176): una resolución que se publica antes de tirarse (<c>TACKLE</c>, <c>DRIBBLE_ATTEMPTED</c>)
/// tiene que mirar a sus participantes <b>después</b> de la publicación. Cada prueba cierra uno de los
/// cuatro casos de la ficha con el flujo real del motor —traza de un fotograma por tick—, porque lo que
/// fallaba eran estados incoherentes que sólo se ven al final del tick.
///
/// <para>Los casos 2 (<c>setState</c> sobre el portador) y 3 (<c>setState</c> sobre el defensor de un
/// regate) <b>no eran defectos</b> y se dejan documentados aquí, con la propiedad que los hace coherentes:
/// nadie acaba un tick derribado y con el balón. Los casos 1 (la repetición iba antes que la original) y 4
/// (la entrada se seguía tirando contra un lesionado que ya no está) sí lo eran.</para>
/// </summary>
public sealed class PreResolutionParticipantsTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private readonly ITestOutputHelper _output;

    public PreResolutionParticipantsTests(ITestOutputHelper output) => _output = output;

    private static MatchResult Play(int index, string perkId, int slot)
    {
        var homeRng = RngStreams.Generation(1, index);
        var awayRng = RngStreams.Generation(1, 10_000 + index);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
        var players = home.Players.ToList();
        players[slot] = players[slot] with { Perks = new[] { perkId } };
        var setup = new MatchSetup(home with { Players = players }, away, Referee);
        return Simulator.Run(setup, RngStreams.MatchSeed(1, index), Catalog, new SimConfig(CollectLog: false, Trace: true));
    }

    private static int Owner(MatchResult result, string perkId) =>
        result.Report.PerksSummary.Where(s => s.PerkId == perkId).Select(s => s.OwnerId).FirstOrDefault();

    /// <summary>
    /// La propiedad que hace coherentes los cuatro casos: <b>al terminar un tick nadie está derribado con
    /// el balón en los pies, y el balón nunca está en (-1,-1)</b> —la posición de quien ha salido del
    /// campo—. Antes de BM-B la incumplían Toro y Embestida (7 de 144 activaciones acababan el tick con el
    /// que entra en el suelo y el balón en su poder: la repetición fallaba primero, lo tumbaba, y la
    /// entrada original se seguía tirando con él caído) y Morder el tobillo (5 de 61 lesiones dejaban el
    /// balón en (-1,-1): la falta reanudaba sobre el jugador que ya no estaba).
    /// </summary>
    [Theory]
    [InlineData("charge", 1)]
    [InlineData("bull_rush", 1)]
    [InlineData("steamroller", 1)]
    [InlineData("ankle_bite", 1)]
    [InlineData("ankle_bite", 2)]
    [InlineData("own_third_anchor", 1)]
    [InlineData("nutmeg", 4)]
    public void NobodyEndsATickKnockedDownWithTheBallAndTheBallNeverLeavesThePitch(string perkId, int slot)
    {
        const int matches = 120;
        int knockedDownWithBall = 0;
        int ballAtTheVoid = 0;
        int frames = 0;
        for (int i = 0; i < matches; i++)
        {
            var trace = Play(i, perkId, slot).Trace!;
            for (int f = 0; f < trace.FrameCount; f++)
            {
                frames++;
                int holder = trace.BallOwnerAt(f);
                if (holder >= 0 && trace.StateAt(f, holder) == PlayerState.KnockedDown)
                {
                    knockedDownWithBall++;
                }

                var ball = trace.BallAt(f);
                if (ball.X < 0f && ball.Y < 0f)
                {
                    ballAtTheVoid++;
                }
            }
        }

        _output.WriteLine($"{perkId} (slot {slot}): {frames} fotogramas, derribado con balón {knockedDownWithBall}, balón en (-1,-1) {ballAtTheVoid}");
        Assert.Equal(0, knockedDownWithBall);
        Assert.Equal(0, ballAtTheVoid);
    }

    /// <summary>
    /// Caso 1: la repetición de una entrada va <b>después</b> de la original. Si la original ganó el balón,
    /// el rival al que se lo quitó está en el suelo y la segunda ya no tiene un portador al que entrar: sólo
    /// puede ser una entrada sin balón contra otro rival, y nunca una disputa del balón contra el que
    /// acababa de perderlo. Antes de BM-B la repetición iba primero y la original se tiraba contra un
    /// portador que ya no tenía el balón (`Tackle:won` seguido de `Tackle:missed`).
    /// </summary>
    [Theory]
    [InlineData("charge")]
    [InlineData("bull_rush")]
    public void ARepeatedTackleAfterAWonOneIsNeverAnotherDisputeForTheSameBall(string perkId)
    {
        int wonThenRepeated = 0;
        int twoTackles = 0;
        for (int i = 0; i < 300; i++)
        {
            var result = Play(i, perkId, 1);
            int owner = Owner(result, perkId);
            foreach (var tick in result.Events.Where(e => e.Type == EventType.Tackle && e.Actor == owner).GroupBy(e => e.Tick))
            {
                var ordered = tick.ToList();
                if (ordered.Count < 2)
                {
                    continue;
                }

                twoTackles++;
                if (ordered[0].Detail == "won")
                {
                    wonThenRepeated++;
                    Assert.StartsWith("offBall", ordered[1].Detail, StringComparison.Ordinal);
                }
            }
        }

        _output.WriteLine($"{perkId}: {twoTackles} ticks con dos entradas, {wonThenRepeated} con la primera ganada");
        Assert.True(twoTackles > 0, "el perk no llega a repetir ninguna entrada: la prueba no demuestra nada");
    }

    /// <summary>
    /// Caso 1: «vuelve a por ella». Si la primera entrada falla sin falta, la segunda es contra el mismo
    /// portador (que sigue con el balón), y el que entra no cae entre las dos. La secuencia que el jugador
    /// lee —«falla, y vuelve a entrar»— existe y acaba en una de las dos cosas que dice el diseño.
    /// </summary>
    [Theory]
    [InlineData("charge")]
    [InlineData("bull_rush")]
    public void AMissedTackleIsRetakenAtTheSameCarrier(string perkId)
    {
        int retaken = 0;
        for (int i = 0; i < 300; i++)
        {
            var result = Play(i, perkId, 1);
            int owner = Owner(result, perkId);
            foreach (var tick in result.Events.Where(e => e.Type == EventType.Tackle && e.Actor == owner).GroupBy(e => e.Tick))
            {
                var ordered = tick.ToList();
                if (ordered.Count >= 2 && ordered[0].Detail == "missed")
                {
                    retaken++;

                    // El mismo portador, salvo que la propia entrada lo haya lesionado y ya no esté: entonces
                    // «vuelve a por ella» busca al siguiente en pie.
                    bool carrierLeft = result.Events.Any(e =>
                        e.Tick == tick.Key && e.Type is EventType.Injury or EventType.Death && e.Actor == ordered[0].Opponent);
                    if (!carrierLeft)
                    {
                        Assert.Equal(ordered[0].Opponent, ordered[1].Opponent);
                        Assert.Contains(ordered[1].Detail, new[] { "won", "missed", "foul" });
                    }
                }
            }
        }

        _output.WriteLine($"{perkId}: {retaken} entradas falladas y repetidas contra el mismo portador");
        Assert.True(retaken > 0, "no hay ninguna entrada fallada que se repita: la prueba no demuestra nada");
    }

    /// <summary>
    /// Caso 4: si la publicación previa saca del campo al que iba a recibir la entrada (la lesión
    /// provocada de <c>ankle_bite</c>), la entrada no se sigue tirando contra él: ni entrada, ni falta, ni
    /// una segunda lesión sobre el mismo cuerpo.
    /// </summary>
    [Fact]
    public void AVictimInjuredByAPerkDoesNotTakePartInTheTackleThatInjuredHim()
    {
        int perkInjuries = 0;
        for (int slot = 1; slot <= 5; slot++)
        {
            for (int i = 0; i < 300; i++)
            {
                var result = Play(i, "ankle_bite", slot);
                int owner = Owner(result, "ankle_bite");
                foreach (var tick in result.Events.GroupBy(e => e.Tick))
                {
                    int victim = -1;
                    bool ownerActedFirst = false;
                    foreach (var e in tick)
                    {
                        if (victim < 0 && !ownerActedFirst && e.Type == EventType.Injury && e.Opponent == owner)
                        {
                            victim = e.Actor;
                            perkInjuries++;
                            continue;
                        }

                        if (e.Actor == owner && e.Type is EventType.Tackle or EventType.Foul)
                        {
                            ownerActedFirst = true;
                        }

                        if (victim < 0)
                        {
                            continue;
                        }

                        Assert.False(
                            e.Type is EventType.Tackle or EventType.Foul && e.Opponent == victim,
                            $"slot {slot}, partido {i}, tick {e.Tick}: {e.Type}:{e.Detail} contra el lesionado {victim}");
                        Assert.False(
                            e.Type is EventType.Injury or EventType.Death && e.Actor == victim,
                            $"slot {slot}, partido {i}, tick {e.Tick}: una segunda {e.Type} sobre el mismo jugador {victim}");
                    }
                }
            }
        }

        _output.WriteLine($"lesiones provocadas por el perk en el lote: {perkInjuries}");
        Assert.True(perkInjuries > 10, "la prueba necesita lesiones provocadas para demostrar algo");
    }
}
