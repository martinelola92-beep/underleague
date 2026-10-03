using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BM-B (ADR 0180): una resolución que se publica antes de tirarse (<c>TACKLE</c>, <c>DRIBBLE_ATTEMPTED</c>)
/// tiene que mirar a sus participantes <b>después</b> de la publicación. Cada prueba cierra uno de los
/// cuatro casos de la ficha con el flujo real del motor —traza de un fotograma por tick—, porque lo que
/// fallaba eran estados incoherentes que sólo se ven al final del tick.
///
/// <para>Los casos 2 (<c>setState</c> sobre el portador) y 3 (<c>setState</c> sobre el defensor de un
/// regate) <b>no eran defectos</b> y se dejan documentados aquí, con la propiedad que los hace coherentes:
/// nadie acaba un tick derribado y con el balón. Los casos 1 (la repetición iba antes que la original) y 4
/// (la entrada se seguía tirando contra un lesionado que ya no está) sí lo eran.</para>
/// </summary>
public sealed class PreResolutionParticipantsTests : IClassFixture<PreResolutionParticipantsTests.Plays>
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private readonly ITestOutputHelper _output;
    private readonly Plays _plays;

    public PreResolutionParticipantsTests(ITestOutputHelper output, Plays plays)
    {
        _output = output;
        _plays = plays;
    }

    /// <summary>
    /// Partidos ya jugados de la clase (técnica «compartir lo que se repite»). Varias pruebas juegan
    /// exactamente el mismo partido <c>(índice, perk, slot)</c> —la semilla es función pura de esos tres
    /// datos— y lo recorren buscando cosas distintas; el fixture lo juega una vez. xUnit ejecuta las
    /// pruebas de una clase una detrás de otra, así que no hay acceso concurrente, y el fixture se suelta
    /// al terminar la clase. Se guarda <b>sin la traza</b> (pesa varios MB por partido: 1.500 partidos de
    /// <c>ankle_bite</c> con traza pasaban de 3 GB). Los partidos se juegan igual, con <c>Trace: true</c>;
    /// quien necesita la traza (la prueba de «nadie acaba un tick derribado con el balón») llama a
    /// <c>PlayNew</c> y se queda con la traza entera; el resto sólo lee eventos e informe.
    /// </summary>
    public sealed class Plays
    {
        private readonly Dictionary<(int Index, string PerkId, int Slot), MatchResult> _results = new();

        public MatchResult Get(int index, string perkId, int slot)
        {
            lock (_results)
            {
                if (!_results.TryGetValue((index, perkId, slot), out var result))
                {
                    result = PlayNew(index, perkId, slot) with { Trace = null };
                    _results[(index, perkId, slot)] = result;
                }

                return result;
            }
        }
    }

    private MatchResult Play(int index, string perkId, int slot) => _plays.Get(index, perkId, slot);

    private static MatchResult PlayNew(int index, string perkId, int slot)
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
            var trace = PlayNew(i, perkId, slot).Trace!;
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
    /// Caso 1: «si la primera no la gana, vuelve a por ella» (`bull_rush`). Si la original <b>ganó</b> el
    /// balón no hay repetición: no queda portador al que volver, y una entrada sin balón contra otro rival
    /// (60 % de falta) sobre el balón recién ganado hacía que cada entrada ganada acabase perdiéndolo
    /// (revisión independiente). Antes de BM-B la repetición iba primero y la original se tiraba contra un
    /// portador que ya no tenía el balón (`Tackle:won` seguido de `Tackle:missed`).
    /// </summary>
    [Theory]
    [InlineData("charge")]
    [InlineData("bull_rush")]
    public void ATackleThatWonTheBallIsNeverRepeated(string perkId)
    {
        int won = 0;
        int twoTackles = 0;
        for (int i = 0; i < 300; i++)
        {
            var result = Play(i, perkId, 1);
            int owner = Owner(result, perkId);
            foreach (var tick in result.Events.Where(e => e.Type == EventType.Tackle && e.Actor == owner).GroupBy(e => e.Tick))
            {
                var ordered = tick.ToList();
                if (ordered[0].Detail == "won")
                {
                    won++;
                    Assert.Single(ordered);
                }

                if (ordered.Count > 1)
                {
                    twoTackles++;
                }
            }
        }

        _output.WriteLine($"{perkId}: {won} entradas ganadas, ninguna repetida; {twoTackles} ticks con repetición");
        Assert.True(won > 0 && twoTackles > 0, "la prueba no demuestra nada");
    }

    /// <summary>
    /// Arrollador y Embestida en el mismo jugador: la repetición armada por la entrada exterior no se
    /// consume dentro de la entrada que lanza Arrollador desde RECOVERY: como mucho la original más una por perk.
    /// </summary>
    [Fact]
    public void ARepeatDoesNotChainWhenTwoExtraActionPerksShareAPlayer()
    {
        int checkedTicks = 0;
        for (int i = 0; i < 200; i++)
        {
            var homeRng = RngStreams.Generation(1, i);
            var awayRng = RngStreams.Generation(1, 10_000 + i);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
            var players = home.Players.ToList();
            players[1] = players[1] with { Perks = new[] { "charge", "steamroller" } };
            var setup = new MatchSetup(home with { Players = players }, away, Referee);
            var result = Simulator.Run(setup, RngStreams.MatchSeed(1, i), Catalog, new SimConfig(CollectLog: false));
            foreach (var tick in result.Events.Where(e => e.Type == EventType.Tackle && e.Actor == players[1].Id).GroupBy(e => e.Tick))
            {
                checkedTicks++;
                // La original, la repetición de Embestida y la que lanza Arrollador tras ganarla: cada perk arma UNA.
                Assert.InRange(tick.Count(), 1, 3);
            }
        }

        Assert.True(checkedTicks > 0);
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
    /// provocada de <c>ankle_bite</c>), la entrada no se sigue tirando contra él: ni entrada, ni una segunda
    /// lesión sobre el mismo cuerpo. Lo único que sobrevive es <b>la falta</b> del que mordió (BM-C: la mordida que
    /// lesiona también se pita), como mucho una, y del propio mordedor.
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
                    int foulsOnVictim = 0;
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
                            e.Type is EventType.Tackle && e.Opponent == victim,
                            $"slot {slot}, partido {i}, tick {e.Tick}: {e.Type}:{e.Detail} contra el lesionado {victim}");
                        if (e.Type == EventType.Foul && e.Opponent == victim)
                        {
                            Assert.Equal(owner, e.Actor);
                            Assert.True(++foulsOnVictim <= 1, $"slot {slot}, partido {i}, tick {e.Tick}: dos faltas por la misma mordida");
                        }

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
    /// <summary>
    /// BM-C: el corte de BM-B no puede dejar sin silbato justo la mordida que lesiona. La tirada de falta se hace igual
    /// aunque la víctima ya haya salido del campo, así que las mordidas que lesionan se pitan <b>a un ritmo comparable</b>
    /// al de las que no lesionan (las dos llevan el mismo ×4 de cuota). Separa las dos poblaciones por activación:
    /// <c>PERK_TRIGGERED ankle_bite</c> seguido, en el mismo tick, de una <c>INJURY</c> causada por el mordedor, o no.
    /// Además la falta se reanuda donde ocurrió, no en (-1,-1): esa propiedad la cubre el test de «nadie acaba un tick
    /// derribado con el balón» (el balón nunca está fuera del campo) sobre el mismo perk.
    /// </summary>
    [Fact]
    public void TheBiteThatInjuresIsWhistledAtTheSameRateAsTheOneThatDoesNot()
    {
        int injuring = 0, injuringFouled = 0, plain = 0, plainFouled = 0;
        for (int slot = 1; slot <= 5; slot++)
        {
            for (int i = 0; i < 300; i++)
            {
                var result = Play(i, "ankle_bite", slot);
                int owner = Owner(result, "ankle_bite");
                foreach (var tick in result.Events.GroupBy(e => e.Tick))
                {
                    var events = tick.ToList();
                    if (!events.Any(e => e.Type == EventType.PerkTriggered && e.Actor == owner && e.Detail == "ankle_bite"))
                    {
                        continue;
                    }

                    // La lesión del perk se emite en la publicación previa, ANTES de que el mordedor resuelva nada; una
                    // lesión normal de la entrada llega después de su TACKLE/FOUL y no cuenta aquí.
                    bool injured = false;
                    foreach (var e in events)
                    {
                        if (e.Actor == owner && e.Type is EventType.Tackle or EventType.Foul)
                        {
                            break;
                        }

                        if (e.Type == EventType.Injury && e.Opponent == owner)
                        {
                            injured = true;
                            break;
                        }
                    }

                    bool fouled = events.Any(e => e.Type == EventType.Foul && e.Actor == owner);
                    if (injured)
                    {
                        injuring++;
                        injuringFouled += fouled ? 1 : 0;
                    }
                    else
                    {
                        plain++;
                        plainFouled += fouled ? 1 : 0;
                    }
                }
            }
        }

        _output.WriteLine($"mordidas que lesionan: {injuring}, con falta {injuringFouled} ({injuringFouled / (double)injuring:P1}); "
            + $"las que no: {plain}, con falta {plainFouled} ({plainFouled / (double)plain:P1})");
        Assert.True(injuring > 30 && plain > 100, "la prueba necesita las dos poblaciones");
        Assert.True(injuringFouled > 0, "la mordida que lesiona no se pita nunca: el corte de BM-B se salta la tirada de falta");
        Assert.True(
            injuringFouled / (double)injuring >= 0.5 * (plainFouled / (double)plain),
            "la mordida que lesiona se pita mucho menos que la que no lesiona");
    }
}
