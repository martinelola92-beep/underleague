using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>Ayudantes de los tests del bucle de run: configuración estándar y sistemas de prueba.</summary>
internal static class TestRuns
{
    /// <summary>
    /// Compara dos jugadores campo a campo. No vale <c>Assert.Equal</c>: <see cref="RunPlayer"/> es un
    /// record con listas y diccionarios, y la igualdad generada los compara por referencia.
    /// </summary>
    public static void AssertSamePlayer(RunPlayer expected, RunPlayer actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Race, actual.Race);
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(expected.Rarity, actual.Rarity);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Experience, actual.Experience);
        Assert.Equal(expected.Attributes, actual.Attributes);
        Assert.Equal(expected.Traits, actual.Traits);
        Assert.Equal(expected.Tags, actual.Tags);
        Assert.Equal(expected.SpeciesTag, actual.SpeciesTag);
        Assert.Equal(expected.StyleTag, actual.StyleTag);
        Assert.Equal(expected.Perks, actual.Perks);
        Assert.Equal(expected.Item, actual.Item);
        Assert.Equal(expected.PhysicalState, actual.PhysicalState);
        Assert.Equal(expected.MinorInjuries, actual.MinorInjuries);
        Assert.Equal(expected.Prostheses, actual.Prostheses);
        Assert.Equal(expected.Wage, actual.Wage);
        Assert.Equal(expected.IsMercenary, actual.IsMercenary);
        Assert.Equal(expected.IsYouth, actual.IsYouth);
        Assert.Equal(expected.MatchesBenched, actual.MatchesBenched);
        Assert.Equal(expected.Bonds, actual.Bonds);
        Assert.Equal(expected.Mourning, actual.Mourning);
        Assert.Equal(expected.Counters, actual.Counters);
        Assert.Equal(expected.BondProgress, actual.BondProgress);
    }

    /// <summary>Configuración de run con la instantánea real de /data y plantilla generada.</summary>
    public static RunSetup Setup(int quality = 50, int nodesPerAct = MapGenerator.DefaultPathLength) =>
        new("test_club", Race.Orc, TestData.LoadAllFiles())
        {
            StartingGold = 100,
            GeneratedQuality = quality,
            NodesPerAct = nodesPerAct,
        };

    /// <summary>
    /// Avanza por nodos que no son de partido hasta que haya un partido accesible, y lo devuelve. La
    /// capa de entrada de un acto no siempre tiene uno: depende del sorteo de tipos.
    /// </summary>
    public static (RunState State, MapNode Node) WalkToMatch(RunState state, Catalog catalog, IRunSystems systems)
    {
        for (int i = 0; i < 12; i++)
        {
            var nodes = RunEngine.AvailableNodes(state);
            var match = nodes.FirstOrDefault(n => n.IsMatch);
            if (match is not null)
            {
                return (state, match);
            }

            if (nodes.Count == 0)
            {
                break;
            }

            state = RunEngine.Enter(state, nodes[0].Id, catalog, systems);
        }

        throw new InvalidOperationException("no se ha encontrado ningún nodo de partido accesible");
    }

    /// <summary>
    /// Juega la run entera eligiendo siempre el primer nodo accesible, y devuelve el estado final.
    /// Es la política más tonta posible a propósito: aquí se prueba el bucle, no jugar bien.
    /// </summary>
    public static RunState PlayToTheEnd(RunState state, Catalog catalog, IRunSystems systems, int maxNodes = 60)
    {
        for (int i = 0; i < maxNodes && !RunEngine.Outcome(state).IsOver; i++)
        {
            var nodes = RunEngine.AvailableNodes(state);
            if (nodes.Count == 0)
            {
                break;
            }

            state = RunEngine.Enter(state, nodes[0].Id, catalog, systems);
        }

        return state;
    }
}

/// <summary>
/// <see cref="IRunSystems"/> de prueba: los rivales se generan con la calidad y los rasgos que pida el
/// test, para poder forzar una derrota, una goleada o una carnicería sin depender de la suerte.
/// </summary>
internal sealed class TestRunSystems : IRunSystems
{
    private readonly DefaultRunSystems _inner = DefaultRunSystems.Instance;

    /// <summary>Calidad fija del rival; null para usar la del sistema por defecto.</summary>
    public int? OpponentQuality { get; init; }

    /// <summary>Rasgos añadidos a todos los titulares rivales (por ejemplo, Aggressive y Dirty).</summary>
    public IReadOnlyList<Trait> OpponentTraits { get; init; } = Array.Empty<Trait>();

    public IReadOnlyList<RunReferee> CreateReferees(ulong seed, int count, Catalog catalog) =>
        _inner.CreateReferees(seed, count, catalog);

    public TeamSetup OpponentFor(RunState state, MapNode node, Catalog catalog)
    {
        if (OpponentQuality is not int quality)
        {
            return _inner.OpponentFor(state, node, catalog);
        }

        var rng = RngStreams.Generation(state.Seed, node.Id);
        var extra = new Dictionary<int, IReadOnlyList<Trait>>();
        if (OpponentTraits.Count > 0)
        {
            for (int slot = 0; slot < 7; slot++)
            {
                extra[slot] = OpponentTraits;
            }
        }

        return TeamGenerator.Generate(
            ref rng,
            catalog,
            $"rival_{node.Id}",
            Race.Orc,
            quality,
            DefaultRunSystems.OpponentFirstPlayerId,
            level: node.Act,
            uniformRarity: null,
            styleBySlot: null,
            extraTraitsBySlot: extra.Count > 0 ? extra : null);
    }

    public RefereeSetup RefereeFor(RunState state, MapNode node, Catalog catalog) =>
        _inner.RefereeFor(state, node, catalog);

    public SimConfig MatchConfig(RunState state, MapNode node) => SimConfig.Default with { CollectLog = false };

    public RunState OpenNode(RunState state, MapNode node, Catalog catalog) => _inner.OpenNode(state, node, catalog);

    public RunState AfterMatch(RunState state, MapNode node, RunMatchSummary summary, Catalog catalog) =>
        _inner.AfterMatch(state, node, summary, catalog);

    public RunState ApplyDecision(RunState state, RunDecision decision, Catalog catalog) =>
        _inner.ApplyDecision(state, decision, catalog);

    public IReadOnlyList<string> BossRuleModifiers(RunState state, MapNode node, Catalog catalog) =>
        _inner.BossRuleModifiers(state, node, catalog);
}
