using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Market;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Instrumento TEMPORAL para la ADR 0127 (<c>docs/decisiones/0127-fichajes-con-nombre-identidad-autorizada-potencia-contextual.md</c>,
/// "Lo que hay que medir antes de escribir el elenco", punto 1): ¿cuántas ofertas de fichaje ve una run?
/// Es la cifra que dimensiona el elenco de fichajes canónicos.
///
/// <para><b>Por qué esto SÍ necesita jugar runs completas</b>, a diferencia del atajo de
/// <c>_RivalCensusTests</c>: cuántos <b>mercados visita</b> una run depende de cómo evoluciona la
/// plantilla y el oro con partidos reales (una run puede quedarse sin plantilla y terminar antes de
/// llegar a mercados tardíos), así que esa cifra sale de <see cref="RunPolicy.Play"/> jugando de verdad
/// (<c>RunPlayResult.MarketsVisited</c>), con la doctrina <b>Contextual</b> —la misma población que usa
/// <see cref="FullRunMetrics.Describe"/> para <c>matchesPerFullRun</c> y <c>purchasesPerMarket</c>, así
/// que estas cifras son comparables con las ya medidas.</para>
///
/// <para><b>Lo que NO hace falta jugar: el CONTENIDO de una oferta.</b> Se verificó leyendo
/// <see cref="MarketOfferGenerator.Generate"/>: el número de fichajes (<c>market.PlayerOffers</c>, hoy 3)
/// y de mercenarios (<c>market.MercenaryOffers</c>, hoy 1) se generan en un bucle <b>incondicional</b>,
/// sin comprobar hueco de plantilla, oro ni rareza disponible —esos filtros solo actúan al COMPRAR
/// (<see cref="Underleague.Sim.Run.Systems.Market.MarketSystem.Buy"/>), nunca al generar el surtido—, y
/// ni la rareza ni el puesto de un fichaje dependen de nada del estado de la run salvo la raza del club
/// (fija durante toda la run) y el acto (fijo por nodo): <c>Generate</c> es función pura de
/// <c>(semilla, node.Id, node.Act, ClubRace, catálogo, economía)</c>. Por eso el número de ofertas por
/// mercado NO varía —se confirma por lectura de código, no hace falta medirlo—, y el CONTENIDO (rareza,
/// puesto) se puede derivar sin jugar ni un partido, generándolo directamente sobre los mapas ya fijados
/// en <see cref="RunEngine.Start"/> (el mapa se genera entero en <c>Start</c> y no cambia con el juego).</para>
///
/// <para><b>Cómo se elige, por acto, el nodo de mercado "representativo" de cada capa</b> (para el
/// desglose de rareza/puesto, no para el recuento de mercados visitados, que sale de
/// <c>MarketsVisited</c> real): el de <c>Id</c> más bajo entre los de tipo <see cref="NodeKind.Market"/>
/// de esa capa. Es una <b>aproximación, no una medición exacta</b> del contenido realmente visto: cuando
/// una capa tiene dos mercados (raro, <c>MapTests</c> permite 1-2), la política real solo entra en uno —y
/// con la utilidad plana de <see cref="RunPolicy.ChooseNode"/> (mercado = 90, por encima de casi todo
/// salvo la clínica urgente a 100) casi siempre entra en el que haya, así que tomar "uno por capa" es más
/// fiel que contar los dos. El contenido de un mercado (rareza, puesto) es independiente de cuál se elige
/// —la elección no lo consulta, RunPolicy.ChooseNode nunca genera el surtido antes de decidir—, así que
/// el sesgo residual es sobre CUÁNTAS capas contribuyen, nunca sobre la distribución de rareza/puesto
/// dentro de ellas.
///
/// <para>Marcado <c>Diagnostic</c> a propósito: no es una puerta, es una medición de una sola vez para
/// dimensionar el elenco de la ADR 0127. Se borra cuando esa decisión se cierre.</para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class _MarketOfferCensusTests
{
    private const int Runs = 300;
    private const ulong Seed = 1;

    private readonly ITestOutputHelper _output;

    public _MarketOfferCensusTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void CuantasOfertasDeFichajeVeUnaRun()
    {
        var catalog = TestData.LoadCatalog();
        var files = TestData.LoadAllFiles();
        var standard = StandardRunSystems.FromJson(files);
        var bosses = BossCatalog.FromJson(files);
        var races = catalog.Races.Where(r => r.Launch).Select(r => r.Id).OrderBy(r => r).ToList();
        var options = RunPolicyOptions.For(PurchaseDoctrine.Contextual);

        var played = new RunPlayResult[Runs];
        var recruitSample = new List<(Position Position, Rarity Rarity)>[Runs];
        var mercenarySample = new List<Rarity>[Runs];
        var representativeMarketNodes = new int[Runs];

        Parallel.For(0, Runs, i =>
        {
            var threadCatalog = ThreadCatalogs.Current;
            var race = races[i % races.Count];
            var setup = standard.NewRunSetup("market_census_club", race, files) with { GeneratedQuality = 50 };
            ulong seed = Seed + (ulong)i;

            // El mapa entero se fija en Start y no cambia con el juego (RunEngine.Start, RT-013): sirve
            // tanto para jugar la run real (MarketsVisited) como para generar el contenido de sus
            // mercados sin jugar ningún partido.
            var state0 = RunEngine.Start(setup, seed, threadCatalog, standard);
            played[i] = RunPolicy.Play(setup, seed, threadCatalog, standard, bosses, options);

            var recruits = new List<(Position, Rarity)>();
            var mercenaries = new List<Rarity>();
            int repCount = 0;
            for (int act = 1; act <= RunRules.Acts; act++)
            {
                var map = state0.MapOf(act);
                var byLayer = new SortedDictionary<int, MapNode>();
                foreach (var node in map.Nodes)
                {
                    if (node.Kind != NodeKind.Market)
                    {
                        continue;
                    }

                    if (!byLayer.TryGetValue(node.Layer, out var existing) || node.Id < existing.Id)
                    {
                        byLayer[node.Layer] = node;
                    }
                }

                foreach (var node in byLayer.Values)
                {
                    repCount++;
                    var offers = MarketOfferGenerator.Generate(
                        state0, node, threadCatalog, standard.Economy, standard.Items, standard.Consumables);
                    foreach (var recruit in offers.Recruits)
                    {
                        recruits.Add((recruit.Player.Position, recruit.Player.Rarity));
                    }

                    foreach (var mercenary in offers.Mercenaries)
                    {
                        mercenaries.Add(mercenary.Player.Rarity);
                    }
                }
            }

            recruitSample[i] = recruits;
            mercenarySample[i] = mercenaries;
            representativeMarketNodes[i] = repCount;
        });

        // ------------------------------------------------------------------ 1. mercados visitados (real)

        var marketsVisited = played.Select(r => r.MarketsVisited).ToList();
        _output.WriteLine($"=== Mercados visitados por run (política Contextual, {Runs} semillas, jugando de verdad) ===");
        _output.WriteLine(Stats(marketsVisited));
        _output.WriteLine($"distribución: {Distribution(marketsVisited)}");
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            int a = act;
            _output.WriteLine($"  acto {act}: {Stats(played.Select(r => r.MarketsByAct[a - 1]))}");
        }

        _output.WriteLine("");
        _output.WriteLine("Nodos de mercado representativos generados (contexto: cuántos mercados distintos se usaron para el desglose de rareza/puesto, uno por capa; NO es el recuento de visitas real de arriba):");
        _output.WriteLine(Stats(representativeMarketNodes.ToList()));

        // ------------------------------------------------------------------ 2. ofertas de fichaje (recruits, sin canteranos ni mercenarios)

        // market.PlayerOffers se genera en un bucle incondicional (confirmado por lectura de
        // MarketOfferGenerator.Generate): SIEMPRE son 3 por mercado, así que la cifra por run es la
        // multiplicación exacta de un dato medido (MarketsVisited), no una estimación.
        const int playerOffersPerMarket = 3;
        var recruitOffersPerRun = marketsVisited.Select(v => v * playerOffersPerMarket).ToList();
        _output.WriteLine("");
        _output.WriteLine("=== Ofertas de JUGADOR (fichajes de pago, sin canteranos ni mercenarios) por run ===");
        _output.WriteLine($"confirmado por código: market.PlayerOffers ({playerOffersPerMarket}) se genera siempre, sin gating por oro/hueco/rareza -> ofertas = {playerOffersPerMarket} x mercados visitados, exacto");
        _output.WriteLine(Stats(recruitOffersPerRun));

        // ------------------------------------------------------------------ 3. desglose por rareza y por puesto

        var allRecruits = recruitSample.SelectMany(x => x).ToList();
        _output.WriteLine("");
        _output.WriteLine($"=== Desglose de la oferta de fichaje, sobre {allRecruits.Count} ofertas muestreadas de {representativeMarketNodes.Sum()} mercados representativos (estimación de proporciones, ver comentario de clase) ===");
        _output.WriteLine("por rareza: " + PercentBreakdown(allRecruits.Select(r => r.Rarity.ToString())));
        _output.WriteLine("por puesto: " + PercentBreakdown(allRecruits.Select(r => r.Position.ToString())));
        _output.WriteLine("El puesto de portero es EXACTO, no muestreado: market.GoalkeeperOffers = 1 fija el primer fichaje de cada mercado a Goalkeeper (confirmado por código), así que ofertas de portero por run = mercados visitados, uno a uno.");

        // ------------------------------------------------------------------ 4. mercenarios, aparte

        var allMercenaries = mercenarySample.SelectMany(x => x).ToList();
        var mercenaryOffersPerRun = marketsVisited.Select(v => v * 1).ToList(); // market.MercenaryOffers = 1, mismo argumento que en 2.
        _output.WriteLine("");
        _output.WriteLine("=== Ofertas de MERCENARIO por run (RF-110..114, aparte del recuento anterior) ===");
        _output.WriteLine("confirmado por código: market.MercenaryOffers (1) también se genera siempre, sin gating -> ofertas = mercados visitados, exacto");
        _output.WriteLine(Stats(mercenaryOffersPerRun));
        _output.WriteLine("por rareza (estimación de proporciones, misma muestra que el punto 3): " + PercentBreakdown(allMercenaries.Select(r => r.ToString())));

        Assert.True(played.Length == Runs);
    }

    private static string Stats(IEnumerable<int> values)
    {
        var list = values.ToList();
        double mean = list.Average();
        return $"media {mean:F2} · min {list.Min()} · max {list.Max()}";
    }

    private static string Distribution(IEnumerable<int> values)
    {
        var counts = new SortedDictionary<int, int>();
        foreach (int v in values)
        {
            counts[v] = counts.GetValueOrDefault(v) + 1;
        }

        return string.Join(" · ", counts.Select(kv => $"{kv.Key}→{kv.Value}"));
    }

    private static string PercentBreakdown(IEnumerable<string> labels)
    {
        var list = labels.ToList();
        if (list.Count == 0)
        {
            return "(sin muestra)";
        }

        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var label in list)
        {
            counts[label] = counts.GetValueOrDefault(label) + 1;
        }

        return string.Join(" · ", counts.Select(kv => $"{kv.Key} {100.0 * kv.Value / list.Count:F1}%"));
    }
}
