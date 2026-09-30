using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BC-D (ADR 0181): «Último hombre». Con un tiro rival a puerta en vuelo, el defensa se interpone y con
/// un % fijo se queda con el balón. El disparador es el tiro que ya se sabe que va a puerta
/// (<c>SHOT_ON_TARGET</c>), así que el uso no se gasta en tiros que van fuera.
/// </summary>
public sealed class GuardShotTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private readonly ITestOutputHelper _output;

    public GuardShotTests(ITestOutputHelper output) => _output = output;

    private static string Guard(int value) => $$"""[{ "type": "guardShot", "target": "owner", "value": {{value}} }]""";

    [Theory]
    [InlineData("SHOT_ON_TARGET", "opposingTeam", 75, true)]
    [InlineData("SHOT", "opposingTeam", 75, false)]        // el tiro aún no sabe si va a puerta
    [InlineData("SHOT_ON_TARGET", "actor", 75, false)]     // el propio tirador no se interpone
    [InlineData("SHOT_ON_TARGET", "opposingTeam", 0, false)]
    [InlineData("SHOT_ON_TARGET", "opposingTeam", 101, false)]
    public void TheLoaderOnlyAcceptsAGuardAgainstAnEnemyShotOnTarget(string trigger, string scope, int value, bool valid)
    {
        string json = TestPerks.Json("g", trigger, Guard(value), scope: scope);
        if (valid)
        {
            Assert.NotNull(TestPerks.Load("g", json));
        }
        else
        {
            Assert.Throws<DataException>(() => TestPerks.Load("g", json));
        }
    }

    private static (MatchEngine Engine, MatchPlayer Guard, MatchPlayer Shooter) OnTargetShot(int value, ulong seed)
    {
        var catalog = TestPerks.CatalogWith(
            ("guard", TestPerks.Json("guard", "SHOT_ON_TARGET", Guard(value), scope: "opposingTeam", positionOnly: "Defender")));
        var setup = TestPerks.Match(catalog, 1, (101, new[] { "guard" })); // defensa visitante
        var engine = new MatchEngine(setup, seed, catalog, new SimConfig(CollectLog: false));
        var guard = engine.PlayerById(101)!;
        var shooter = engine.PlayerById(6)!;
        for (int i = 0; i < 14; i++)
        {
            var p = engine.PlayerAtForTest(i);
            if (!ReferenceEquals(p, guard) && !ReferenceEquals(p, shooter))
            {
                engine.PlaceForTest(i, new Vec2(p.Team == 0 ? 0.5f : 15.5f, 0.5f));
            }
        }

        engine.PlaceForTest(shooter.Index, new Vec2(10f, 3.5f));
        engine.PlaceForTest(guard.Index, new Vec2(14f, 6f));
        engine.GiveBallForTest(shooter.Index, shooter.Position);
        return (engine, guard, shooter);
    }

    /// <summary>
    /// Con el 100 % el defensa se queda con el balón siempre que el tiro va a puerta: el evento del bloqueo
    /// dice «guard», el balón es suyo y el tiro deja de estar en vuelo. Sin el perk en la partida real esto
    /// no ocurre nunca.
    /// </summary>
    [Fact]
    public void AGuardAtOneHundredPercentTakesEveryShotOnTarget()
    {
        int taken = 0;
        int onTarget = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            var (engine, guard, shooter) = OnTargetShot(100, (ulong)seed);
            engine.LaunchShotForTest(shooter.Index);
            if (engine.EventsForTest.Any(e => e.Type == EventType.Shot && e.Detail == "onTarget"))
            {
                onTarget++;
                Assert.Contains(engine.EventsForTest, e => e.Type == EventType.ShotBlocked && e.Detail == "guard" && e.Actor == guard.Id);
                Assert.Equal(guard.Id, engine.BallOwnerIdForTest);
                Assert.False(engine.BallInFlightForTest);
                taken++;
            }
            else
            {
                Assert.DoesNotContain(engine.EventsForTest, e => e.Detail == "guard");
                Assert.True(engine.BallInFlightForTest);
            }
        }

        _output.WriteLine($"{onTarget} tiros a puerta de 40, {taken} parados por el último hombre");
        Assert.True(onTarget > 0 && onTarget < 40, "la prueba necesita tiros a puerta y tiros fuera");
        Assert.Equal(onTarget, taken);
    }

    /// <summary>
    /// En partidos reales el perk sólo se activa cuando un tiro rival va a puerta, una vez por partido, y
    /// cuando se queda con el balón el evento y la recuperación lo dicen.
    /// </summary>
    [Fact]
    public void InRealMatchesItOnlyActivatesOnAnEnemyShotOnTargetAndHitsAboutItsChance()
    {
        int activations = 0;
        int stops = 0;
        for (int nth = 0; nth < 3; nth++)
        {
            for (int i = 0; i < 250; i++)
            {
                var homeRng = RngStreams.Generation(1, i);
                var awayRng = RngStreams.Generation(1, 10_000 + i);
                var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
                var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
                var players = home.Players.ToList();
                int slot = Enumerable.Range(0, players.Count).Where(k => players[k].Position == Position.Defender).ElementAt(nth);
                players[slot] = players[slot] with { Perks = new[] { "last_man" } };
                var setup = new MatchSetup(home with { Players = players }, away, Referee);
                var result = Simulator.Run(setup, RngStreams.MatchSeed(1, i), Catalog, new SimConfig(CollectLog: false));
                int owner = players[slot].Id;

                int perMatch = 0;
                foreach (var e in result.Events.Where(e => e.Type == EventType.PerkTriggered && e.Detail == "last_man"))
                {
                    perMatch++;
                    activations++;
                    Assert.Contains(
                        result.Events,
                        s => s.Tick == e.Tick && s.Type == EventType.Shot && s.Detail == "onTarget" && s.Team == 1);
                    if (result.Events.Any(s => s.Tick == e.Tick && s.Type == EventType.ShotBlocked && s.Detail == "guard" && s.Actor == owner))
                    {
                        stops++;
                        Assert.Contains(result.Events, s => s.Tick == e.Tick && s.Type == EventType.Recovery && s.Detail == "guard" && s.Actor == owner);
                    }
                }

                Assert.InRange(perMatch, 0, 1);
            }
        }

        _output.WriteLine($"Último hombre: {activations} activaciones en 750 partidos, {stops} paradas");
        Assert.True(activations > 100, "el perk tiene que activarse en muchos partidos");

        // La probabilidad sale del dato (hoy 20 %, ADR 0181 enmendada): ±15 puntos cubren el azar de sobra.
        int chance = Catalog.Perks.Get("last_man").Effects.Single().Value;
        Assert.InRange(stops * 100 / activations, chance - 15, chance + 15);
    }

    /// <summary>
    /// Revisión independiente: si el defensa <b>no puede actuar</b> (está tirado, así que <c>CanTouchBall</c> falla),
    /// la activación no cuenta: ni cartel, ni uso gastado. Con el uso intacto, el mismo defensa sí actúa en el tiro
    /// siguiente, cuando ya está en pie (el perk tiene un uso por partido).
    /// </summary>
    [Fact]
    public void AGuardWhoCannotTouchTheBallDoesNotSpendItsUseNorItsPoster()
    {
        int checkedShots = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var (engine, guard, shooter) = OnTargetShot(100, (ulong)seed);
            guard.EnterState(PlayerState.KnockedDown, 100);
            engine.LaunchShotForTest(shooter.Index);
            if (!engine.EventsForTest.Any(e => e.Type == EventType.Shot && e.Detail == "onTarget"))
            {
                continue;
            }

            checkedShots++;
            Assert.DoesNotContain(engine.EventsForTest, e => e.Type == EventType.PerkTriggered);
            Assert.DoesNotContain(engine.EventsForTest, e => e.Type == EventType.ShotBlocked && e.Detail == "guard");

            // El defensa se levanta y el siguiente tiro a puerta sí lo encuentra con el uso entero.
            guard.EnterState(PlayerState.Positioning, 0);
            engine.PlaceForTest(guard.Index, new Vec2(14f, 6f));
            engine.GiveBallForTest(shooter.Index, new Vec2(10f, 3.5f));
            engine.PlaceForTest(shooter.Index, new Vec2(10f, 3.5f));
            for (int tries = 0; tries < 30 && !engine.EventsForTest.Any(e => e.Type == EventType.PerkTriggered); tries++)
            {
                engine.LaunchShotForTest(shooter.Index);
                if (!engine.EventsForTest.Any(e => e.Type == EventType.PerkTriggered))
                {
                    engine.PlaceForTest(guard.Index, new Vec2(14f, 6f));
                    engine.GiveBallForTest(shooter.Index, new Vec2(10f, 3.5f));
                }
            }

            Assert.Single(engine.EventsForTest, e => e.Type == EventType.PerkTriggered);
        }

        _output.WriteLine($"{checkedShots} tiros a puerta con el defensa tirado");
        Assert.True(checkedShots > 0, "la prueba necesita algún tiro a puerta");
    }

    /// <summary>
    /// Con la tirada fallida (1 %) el defensa queda plantado en la trayectoria y el tiro <b>sigue</b>: ni balón
    /// suyo ni evento «guard». Es una activación real (cartel y uso gastado) y el tiro lo resuelve el motor como
    /// siempre hasta su final (gol, parada, bloqueo…).
    /// </summary>
    [Fact]
    public void AFailedRollLeavesTheGuardOnTheLineAndTheShotCarriesOn()
    {
        int onTarget = 0, resolved = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            var (engine, _, shooter) = OnTargetShot(1, (ulong)seed);
            engine.LaunchShotForTest(shooter.Index);
            if (!engine.EventsForTest.Any(e => e.Type == EventType.Shot && e.Detail == "onTarget"))
            {
                continue;
            }

            onTarget++;
            Assert.Single(engine.EventsForTest, e => e.Type == EventType.PerkTriggered);
            if (engine.EventsForTest.Any(e => e.Type == EventType.ShotBlocked && e.Detail == "guard"))
            {
                continue; // el 1 % que sí sale: no es de lo que trata esta prueba
            }

            Assert.True(engine.BallInFlightForTest, "con la tirada fallida el tiro sigue en vuelo");
            for (int k = 0; k < 40 && engine.BallInFlightForTest; k++)
            {
                engine.StepFlightForTest();
            }

            Assert.False(engine.BallInFlightForTest, "el tiro no termina");
            Assert.Contains(engine.EventsForTest, e => e.Type is EventType.Goal or EventType.Save or EventType.ShotBlocked or EventType.ShotPost);
            resolved++;
        }

        _output.WriteLine($"{onTarget} tiros a puerta con el 1 %; seguidos y resueltos por el motor: {resolved}");
        Assert.True(onTarget > 50, "la prueba necesita tiros a puerta");
        Assert.True(resolved > 0.9 * onTarget);
    }

    /// <summary>
    /// En partidos reales, tras una tirada fallida el defensa sigue siendo un defensa más: el bloqueo genérico del
    /// motor puede pararle el tiro desde su sitio en la trayectoria. Se cuentan los bloqueos <c>blocked</c> del
    /// portador en los diez ticks siguientes a una activación sin parada suya.
    /// </summary>
    [Fact]
    public void AfterAFailedRollTheGenericBlockStillActsOnTheGuard()
    {
        int failed = 0, genericBlocks = 0;
        for (int nth = 0; nth < 3; nth++)
        {
            for (int i = 0; i < 250; i++)
            {
                var homeRng = RngStreams.Generation(1, i);
                var awayRng = RngStreams.Generation(1, 10_000 + i);
                var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
                var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
                var players = home.Players.ToList();
                int slot = Enumerable.Range(0, players.Count).Where(k => players[k].Position == Position.Defender).ElementAt(nth);
                players[slot] = players[slot] with { Perks = new[] { "last_man" } };
                var setup = new MatchSetup(home with { Players = players }, away, Referee);
                var result = Simulator.Run(setup, RngStreams.MatchSeed(1, i), Catalog, new SimConfig(CollectLog: false));
                int owner = players[slot].Id;

                foreach (var e in result.Events.Where(e => e.Type == EventType.PerkTriggered && e.Detail == "last_man"))
                {
                    if (result.Events.Any(s => s.Tick == e.Tick && s.Type == EventType.ShotBlocked && s.Detail == "guard" && s.Actor == owner))
                    {
                        continue;
                    }

                    failed++;
                    genericBlocks += result.Events.Any(s => s.Tick >= e.Tick && s.Tick <= e.Tick + 10
                        && s.Type == EventType.ShotBlocked && s.Detail == "blocked" && s.Actor == owner) ? 1 : 0;
                }
            }
        }

        _output.WriteLine($"activaciones sin parada del último hombre: {failed}; bloqueos genéricos suyos tras ellas: {genericBlocks}");
        Assert.True(failed > 20, "la prueba necesita tiradas fallidas");
        Assert.True(genericBlocks > 0, "el bloqueo genérico no llega a actuar sobre el defensa que falló la tirada");
    }
}
