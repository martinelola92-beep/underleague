using System.Text;
using Underleague.Sim.Random;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// Mapa por actos (RF-003b, RF-010, RF-011, RF-011b). El test que manda es
/// <see cref="MarketGuarantee_HoldsOnAThousandMaps"/>: la garantía de RF-011b es dura y por
/// construcción, así que si algún día se rompe, se rompe siempre y en el primer mapa.
/// </summary>
public class MapTests
{
    [Fact]
    public void MarketGuarantee_HoldsOnAThousandMaps()
    {
        var failures = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            ulong seed = (ulong)i * 2654435761UL;
            int act = (i % 3) + 1;
            int nodes = MapGenerator.MinPathLength + (i % 3);
            var map = MapGenerator.Generate(seed, act, new MapOptions(nodes));

            var problems = MapInvariants.Violations(map);
            if (problems.Count > 0)
            {
                failures.Add($"semilla {seed}, acto {act}, {nodes} nodos: {string.Join("; ", problems)}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void EveryNode_HasAMarketWithinTwoHops()
    {
        // La misma garantía, escrita nodo a nodo y sin pasar por MapInvariants, para que el test no
        // dependa de que el comprobador esté bien.
        for (int i = 0; i < 200; i++)
        {
            var map = MapGenerator.Generate((ulong)(i + 1), (i % 3) + 1, new MapOptions(MapGenerator.MinPathLength + (i % 3)));
            foreach (var node in map.Nodes)
            {
                int toMarket = MapInvariants.HopsTo(map, node.Id, NodeKind.Market);
                int toBoss = MapInvariants.HopsTo(map, node.Id, NodeKind.Boss);
                Assert.True(
                    toMarket <= MapGenerator.MaxHopsToMarket || toBoss <= MapGenerator.MaxHopsToMarket,
                    $"nodo {node.Id} (capa {node.Layer}, {node.Kind}): mercado a {toMarket}, jefe a {toBoss}");
            }
        }
    }

    [Fact]
    public void SameSeed_SameMap()
    {
        for (int act = 1; act <= 3; act++)
        {
            var a = MapGenerator.Generate(4242, act, MapOptions.Default);
            var b = MapGenerator.Generate(4242, act, MapOptions.Default);
            Assert.Equal(Fingerprint(a), Fingerprint(b));
        }
    }

    [Fact]
    public void DifferentSeed_DifferentMap()
    {
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < 50; i++)
        {
            distinct.Add(Fingerprint(MapGenerator.Generate((ulong)(i + 1), 1, MapOptions.Default)));
        }

        // No se exige que las 50 sean distintas -el espacio de mapas es finito-, pero sí que el
        // generador no esté produciendo siempre lo mismo.
        Assert.True(distinct.Count > 10, $"solo {distinct.Count} mapas distintos en 50 semillas");
    }

    [Fact]
    public void RewardsStream_DoesNotChangeTheMap()
    {
        // RT-022: mapa, partido y recompensas son flujos independientes. Consumir el de recompensas, o
        // cambiar su semilla, no puede mover un solo nodo del mapa.
        const ulong Seed = 777;
        string baseline = Fingerprint(MapGenerator.Generate(Seed, 2, MapOptions.Default));

        var rewards = RngStreams.Rewards(Seed, 0);
        for (int i = 0; i < 1000; i++)
        {
            rewards.Next();
        }

        Assert.Equal(baseline, Fingerprint(MapGenerator.Generate(Seed, 2, MapOptions.Default)));

        var otherRewards = RngStreams.Rewards(Seed + 1, 0);
        for (int i = 0; i < 1000; i++)
        {
            otherRewards.Next();
        }

        Assert.Equal(baseline, Fingerprint(MapGenerator.Generate(Seed, 2, MapOptions.Default)));

        // Y las semillas de partido de los nodos tampoco dependen del mapa: salen del id del nodo.
        Assert.Equal(RngStreams.MatchSeed(Seed, 205), RngStreams.MatchSeed(Seed, 205));
        Assert.NotEqual(RngStreams.MatchSeed(Seed, 205), RngStreams.MatchSeed(Seed, 206));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void PathLength_AndMatchShare_RespectTheBudget(int pathLength)
    {
        for (int act = 1; act <= 3; act++)
        {
            var map = MapGenerator.Generate(99, act, new MapOptions(pathLength));
            Assert.Equal(pathLength, MapInvariants.PathLength(map));

            // RF-003b sobre el PEOR camino: no hay elección de nodos que juegue más partidos que esto.
            int matches = MapInvariants.WorstCaseMatches(map);
            Assert.True(
                matches * 100 <= pathLength * MapGenerator.MaxMatchPercent,
                $"el peor camino juega {matches} partidos de {pathLength} nodos, por encima del {MapGenerator.MaxMatchPercent}% de RF-003b");
            Assert.Equal(1, map.Nodes.Count(n => n.Kind == NodeKind.Boss));
            Assert.Contains(map.Nodes, n => n.Kind == NodeKind.Clinic);

            // ADR 0046: y un nodo de inscripción por acto, por construcción y no por sorteo. Sin él,
            // comprar un hueco de plantilla dependería de que el dado lo ofreciera.
            Assert.Contains(map.Nodes, n => n.Kind == NodeKind.Enrollment);
            Assert.DoesNotContain(map.Nodes, n => n.Kind == NodeKind.Workshop);

            // Y tres mercados por acto: un mercado cada 3 nodos recorridos (RF-011b).
            var marketLayers = map.Nodes.Where(n => n.Kind == NodeKind.Market).Select(n => n.Layer).Distinct().ToList();
            Assert.Equal(3, marketLayers.Count);
        }
    }

    /// <summary>
    /// ADR 0046: el nodo de inscripción es una decisión de <b>ruta</b>. Aparece en una capa de servicios,
    /// así que ir a por un hueco significa no ir al otro servicio de esa capa —la clínica, el
    /// entrenamiento o el evento—, y nunca ocupa una capa entera: el mercado sigue siendo el cuello de
    /// botella que RF-011b garantiza.
    /// </summary>
    [Fact]
    public void TheEnrollmentNodeIsAlwaysAChoiceAgainstAnotherService()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            for (int act = 1; act <= 3; act++)
            {
                var map = MapGenerator.Generate(seed, act, MapOptions.Default);
                var enrollment = map.Nodes.Where(n => n.Kind == NodeKind.Enrollment).ToList();
                Assert.NotEmpty(enrollment);

                foreach (var node in enrollment)
                {
                    var layer = map.Nodes.Where(n => n.Layer == node.Layer).ToList();
                    Assert.True(
                        layer.Count > 1,
                        $"semilla {seed}, acto {act}: el nodo de inscripción {node.Id} ocupa una capa entera y no es una elección");
                    Assert.Contains(layer, n => n.Kind != NodeKind.Enrollment);
                    Assert.DoesNotContain(layer, n => n.IsMatch || n.Kind == NodeKind.Market);
                }
            }
        }
    }

    [Fact]
    public void ARunOfThreeActs_PlaysBetweenEighteenAndTwentyTwoMatches()
    {
        // fase2-diseno.md §10: la duración objetivo de una run son 18-22 partidos. Con el tope de
        // RF-003b, el peor camino de los tres actos tiene que caer dentro de ese rango.
        for (ulong seed = 1; seed <= 20; seed++)
        {
            int matches = 0;
            for (int act = 1; act <= 3; act++)
            {
                matches += MapInvariants.WorstCaseMatches(MapGenerator.Generate(seed, act, MapOptions.Default));
            }

            Assert.InRange(matches, 18, 22);
        }
    }

    [Fact]
    public void Edges_OnlyGoForward()
    {
        for (int i = 0; i < 100; i++)
        {
            var map = MapGenerator.Generate((ulong)(i + 1), (i % 3) + 1, MapOptions.Default);
            foreach (var node in map.Nodes)
            {
                foreach (int target in node.Next)
                {
                    Assert.Equal(node.Layer + 1, map.Get(target).Layer);
                }
            }
        }
    }

    [Fact]
    public void FirstLayer_HasNoEliteMatch()
    {
        for (int i = 0; i < 200; i++)
        {
            var map = MapGenerator.Generate((ulong)(i + 1), (i % 3) + 1, MapOptions.Default);
            foreach (var node in map.Nodes)
            {
                if (node.Layer == 0)
                {
                    Assert.NotEqual(NodeKind.EliteMatch, node.Kind);
                }
            }
        }
    }

    [Fact]
    public void StaticOpponents_AreSpreadOverTheMatchNodes()
    {
        // RF-015: los rivales son fijos por acto; lo aleatorio es en qué nodo cae cada uno.
        var opponents = new[] { "gnash", "ironjaw", "rotfoot", "pale_choir", "sixteen_teeth", "the_hollow" };
        var map = MapGenerator.Generate(31337, 1, new MapOptions(11, opponents));

        var assigned = map.Nodes.Where(n => n.IsMatch).Select(n => n.OpponentId).ToList();
        Assert.All(assigned, id => Assert.Contains(id, opponents));
        Assert.Equal(opponents.Length, assigned.Distinct().Count());
        Assert.All(map.Nodes.Where(n => !n.IsMatch), n => Assert.Equal(string.Empty, n.OpponentId));
    }

    private static string Fingerprint(ActMap map)
    {
        var text = new StringBuilder();
        text.Append(map.Act).Append('|').Append(map.BossNodeId).Append('|');
        text.Append(string.Join(",", map.EntryNodeIds)).Append('|');
        foreach (var node in map.Nodes)
        {
            text.Append(node.Id).Append(':').Append(node.Layer).Append(':').Append(node.IndexInLayer)
                .Append(':').Append(node.Kind).Append(':').Append(node.Difficulty)
                .Append(':').Append(string.Join("-", node.Next)).Append(';');
        }

        return text.ToString();
    }
}
