using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0094 (AZ-F): la sustitución forzada es parte del estado inicial del partido. Lo que se comprueba es
/// lo que hace legal el mecanismo de RF-082 —volver a ejecutar con la decisión y seguir desde T—: los
/// eventos hasta el tick de la salida son idénticos con y sin la sustitución, el suplente entra al tick
/// siguiente en la casilla del que sale, el estado inicial ilegal se rechaza, y la resolución automática no
/// deja puntos de decisión pendientes.
/// </summary>
public sealed class SubstitutionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly SimConfig Config = new(CollectLog: false);

    /// <summary>
    /// Siete frágiles (como los de <see cref="TestMatches.Brutal"/>, pero siete: con cinco, la primera lesión
    /// deja al equipo por debajo del mínimo y el partido termina antes de que nadie pueda entrar) contra los
    /// siete brutales, y dos suplentes frágiles: un defensa y un delantero (RF-005).
    /// </summary>
    private static MatchSetup WithBench()
    {
        var brutal = TestMatches.Brutal(Catalog);
        var positions = new[] { Position.Goalkeeper, Position.Defender, Position.Defender, Position.Midfielder, Position.Midfielder, Position.Midfielder, Position.Forward };
        var attributes = brutal.Home.Players[0].Attributes;
        PlayerDefinition Fragile(int id, Position position) =>
            new(id, "fragile" + id, Race.Human, position, Rarity.Common, 1, attributes, Array.Empty<Trait>(), new[] { "Neutral", position.ToString() }, PhysicalState.Healthy);
        var starters = positions.Select((position, i) => Fragile(i, position)).ToList();
        var players = new List<PlayerDefinition>(starters) { Fragile(50, Position.Defender), Fragile(51, Position.Forward) };
        var fragile = new TeamSetup("fragile", "fragile", Race.Human, players, Lineup.Default(starters));
        return brutal with { Home = fragile };
    }

    private static (MatchSetup Setup, ulong Seed, MatchResult Result, SubstitutionPoint Point) FirstDecisionPoint()
    {
        var setup = WithBench();
        for (ulong seed = 1; seed < 60; seed++)
        {
            var result = Simulator.Run(setup, seed, Catalog, Config);
            var point = SubstitutionPoints.Pending(setup, result, 0);
            if (point is not null && point.Tick < Catalog.Tuning.RegulationTicks - 30)
            {
                return (setup, seed, result, point);
            }
        }

        throw new Xunit.Sdk.XunitException("ninguna semilla lesiona a un frágil con margen: el escenario Brutal ya no es brutal");
    }

    [Fact]
    public void TheSubstituteEntersTheTickAfterTheDepartureAndTheEventsBeforeAreIdentical()
    {
        var (setup, seed, baseline, point) = FirstDecisionPoint();
        Assert.Equal(0, point.Team);
        Assert.Equal(2, point.Candidates.Count);
        var chosen = point.Candidates[0];

        var home = setup.Home with { Substitutions = new[] { new Substitution(point.Tick, point.OutPlayerId, chosen.Id) } };
        var result = Simulator.Run(setup with { Home = home }, seed, Catalog, Config);

        // Hasta T incluido, el partido es el mismo: es lo que permite descartar la cola y seguir desde T.
        var before = baseline.Events.Where(e => e.Tick <= point.Tick).ToList();
        var after = result.Events.Where(e => e.Tick <= point.Tick).ToList();
        Assert.Equal(before, after);

        var entry = Assert.Single(result.Events, e => e.Type == EventType.Substitution);
        Assert.Equal(point.Tick + 1, entry.Tick);
        Assert.Equal(chosen.Id, entry.Actor);
        Assert.Equal(point.OutPlayerId, entry.Target);
        Assert.Equal(point.Detail, entry.Detail);

        var stats = Assert.Single(result.Report.Players, p => p.PlayerId == chosen.Id);
        Assert.True(stats.TicksOnPitch > 0, "el suplente entró pero no jugó ni un tick");
        var pending = SubstitutionPoints.Pending(setup with { Home = home }, result, 0);
        Assert.True(pending is null || pending.OutPlayerId != point.OutPlayerId, "la salida ya sustituida sigue pendiente");
    }

    [Fact]
    public void AnIllegalSubstitutionIsAnExplicitError()
    {
        var setup = WithBench();
        int starter = setup.Home.Lineup.Slots[1].PlayerId;
        int other = setup.Home.Lineup.Slots[2].PlayerId;

        // Quien entra tiene que estar en el banquillo.
        var lineupIn = setup with { Home = setup.Home with { Substitutions = new[] { new Substitution(10, starter, other) } } };
        Assert.Throws<ArgumentException>(() => Simulator.Run(lineupIn, 1, Catalog, Config));

        // Quien sale tiene que haber salido por lesión o muerte: no hay cambios voluntarios.
        var voluntary = setup with { Home = setup.Home with { Substitutions = new[] { new Substitution(0, starter, 50) } } };
        Assert.Throws<ArgumentException>(() => Simulator.Run(voluntary, 1, Catalog, Config));
    }

    [Fact]
    public void AutomaticResolutionLeavesNoPendingPointAndIsReproducible()
    {
        var (setup, seed, _, _) = FirstDecisionPoint();
        var (resolved, result) = SubstitutionPoints.ResolveAutomatically(setup, seed, Catalog, Config);

        Assert.Null(SubstitutionPoints.Pending(resolved, result, 0));
        Assert.Null(SubstitutionPoints.Pending(resolved, result, 1));
        Assert.InRange(resolved.Home.Substitutions.Count, 1, 2);
        Assert.Equal(result.Events, Simulator.Run(resolved, seed, Catalog, Config).Events);
    }

    /// <summary>
    /// BC-E: la elección del jugador llega como sustitución del estado inicial y el rival sustituyó ANTES de
    /// ese tick. Con la elección fija desde el tick 0 y sin las sustituciones del rival, el partido divergía
    /// antes de T y el motor rechazaba la sustitución (9,4 % de los puntos del jugador en 400 runs). Ahora la
    /// elección es la respuesta a su punto y se aplica al llegar a él, después de las del rival.
    /// </summary>
    [Fact]
    public void ThePlayerChoiceIsAppliedAfterEarlierRivalSubstitutions()
    {
        var (setup, seed, point, resolved) = PlayerPointAfterRivalSubstitution();
        var other = point.Candidates.First(c => c.Id != SubstitutionPolicy.Default(point, setup.Home.Players.First(p => p.Id == point.OutPlayerId)).Id);
        var choice = setup with { Home = setup.Home with { Substitutions = new[] { new Substitution(point.Tick, point.OutPlayerId, other.Id) } } };

        var (final, result) = SubstitutionPoints.ResolveAutomatically(choice, seed, Catalog, Config, static team => team == 1);

        Assert.Contains(new Substitution(point.Tick, point.OutPlayerId, other.Id), final.Home.Substitutions);
        foreach (var earlier in resolved.Away.Substitutions.Where(s => s.Tick < point.Tick))
        {
            Assert.Contains(earlier, final.Away.Substitutions);
        }

        // Hasta T el partido es el mismo que el que vio el jugador cuando se le abrió la ventana.
        var shown = Simulator.Run(resolved, seed, Catalog, Config).Events.Where(e => e.Tick <= point.Tick).ToList();
        Assert.Equal(shown, result.Events.Where(e => e.Tick <= point.Tick).ToList());
        Assert.Contains(result.Events, e => e.Type == EventType.Substitution && e.Actor == other.Id && e.Tick == point.Tick + 1);

        // El camino de la run (RunEngine.ResolveMatch: política para los dos equipos) aplica la misma elección.
        var (run, _) = SubstitutionPoints.ResolveAutomatically(choice, seed, Catalog, Config);
        Assert.Contains(new Substitution(point.Tick, point.OutPlayerId, other.Id), run.Home.Substitutions);
    }

    /// <summary>
    /// BC-E (revisión): una elección que no responde a ningún punto de decisión no se descarta en silencio,
    /// es un error explícito (ADR 0094, RT-032).
    /// </summary>
    [Fact]
    public void AChoiceThatAnswersNoDecisionPointIsAnExplicitError()
    {
        var (setup, seed, point, _) = PlayerPointAfterRivalSubstitution();
        var displaced = setup with { Home = setup.Home with { Substitutions = new[] { new Substitution(point.Tick + 1, point.OutPlayerId, point.Candidates[0].Id) } } };
        Assert.Throws<ArgumentException>(() => SubstitutionPoints.ResolveAutomatically(displaced, seed, Catalog, Config, static team => team == 1));
        Assert.Throws<ArgumentException>(() => SubstitutionPoints.ResolveAutomatically(displaced, seed, Catalog, Config));
    }

    /// <summary>
    /// BC-F: el dorsal no depende de quién entra. Los titulares conservan su número con y sin sustitución, y
    /// el suplente lleva el que le toca por su orden en la plantilla (el segundo suplente, el 9).
    /// </summary>
    [Fact]
    public void ASubstitutionDoesNotRenumberTheTeam()
    {
        var (setup, seed, _, point) = FirstDecisionPoint();
        var traced = Config with { Trace = true };
        var before = Simulator.Run(setup, seed, Catalog, traced).Trace!;
        var home = setup.Home with { Substitutions = new[] { new Substitution(point.Tick, point.OutPlayerId, 51) } };
        var after = Simulator.Run(setup with { Home = home }, seed, Catalog, traced).Trace!;

        foreach (var player in before.Players.Where(p => p.Team == 0))
        {
            Assert.Equal(player.Number, after.Players.Single(p => p.Team == 0 && p.Id == player.Id).Number);
        }

        Assert.Equal(9, after.Players.Single(p => p.Team == 0 && p.Id == 51).Number);
        var numbers = after.Players.Where(p => p.Team == 0).Select(p => p.Number).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    /// <summary>
    /// Dos equipos frágiles y sucios con dos suplentes cada uno: se lesionan los dos. Busca un punto de
    /// decisión del jugador (equipo 0) con alguna sustitución del rival anterior, resolviendo al rival con la
    /// política como hace la pantalla de Partido (<c>MatchPlaybacks.Of</c>).
    /// </summary>
    private static (MatchSetup Setup, ulong Seed, SubstitutionPoint Point, MatchSetup Resolved) PlayerPointAfterRivalSubstitution()
    {
        var positions = new[] { Position.Goalkeeper, Position.Defender, Position.Defender, Position.Midfielder, Position.Midfielder, Position.Midfielder, Position.Forward };
        var fragile = new Attributes(1, 50, 50, 1, 99);
        var traits = new[] { Trait.Aggressive, Trait.Dirty };
        TeamSetup Team(string id, int first)
        {
            PlayerDefinition Make(int pid, Position position) =>
                new(pid, id + pid, Race.Human, position, Rarity.Common, 1, fragile, traits, new[] { "Neutral", position.ToString(), "Aggressive", "Dirty" }, PhysicalState.Healthy);
            var starters = positions.Select((position, i) => Make(first + i, position)).ToList();
            var players = new List<PlayerDefinition>(starters) { Make(first + 50, Position.Defender), Make(first + 51, Position.Forward) };
            return new TeamSetup(id, id, Race.Human, players, Lineup.Default(starters));
        }

        var setup = new MatchSetup(Team("home", 0), Team("away", 100), new RefereeSetup("Neutral", RefereeTrait.Neutral, 0));
        for (ulong seed = 1; seed < 400; seed++)
        {
            var (resolved, result) = SubstitutionPoints.ResolveAutomatically(setup, seed, Catalog, Config, static team => team == 1);
            var point = SubstitutionPoints.Pending(resolved, result, 0);
            if (point is not null && point.Candidates.Count == 2 && resolved.Away.Substitutions.Any(s => s.Tick < point.Tick))
            {
                return (setup, seed, point, resolved);
            }
        }

        throw new Xunit.Sdk.XunitException("ninguna semilla da un punto del jugador después de una sustitución del rival: el escenario ya no lesiona a los dos");
    }

    [Fact]
    public void TheDefaultPolicyPrefersTheSamePositionAndThenQuality()
    {
        var weak = new Attributes(30, 30, 30, 30, 30);
        var strong = new Attributes(70, 70, 70, 70, 70);
        PlayerDefinition Make(int id, Position position, Attributes attributes) =>
            new(id, "p" + id, Race.Human, position, Rarity.Common, 1, attributes, Array.Empty<Trait>(), new[] { "Neutral", position.ToString() }, PhysicalState.Healthy);

        var outDefender = Make(1, Position.Defender, weak);
        var candidates = new[] { Make(10, Position.Forward, strong), Make(11, Position.Defender, weak), Make(12, Position.Defender, weak) };
        var point = new SubstitutionPoint(0, 5, 1, "injury", candidates);

        Assert.Equal(11, SubstitutionPolicy.Default(point, outDefender).Id);
        var outMidfielder = Make(2, Position.Midfielder, weak);
        Assert.Equal(10, SubstitutionPolicy.Default(point, outMidfielder).Id);
    }
}
