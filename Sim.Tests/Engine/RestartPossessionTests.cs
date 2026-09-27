using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0155 (decisión del revisor): una reanudación es del equipo que la saca. Durante la cuenta atrás de
/// un saque de banda, córner, falta o saque de puerta, ese equipo se coloca para recibir —busca hueco— en
/// vez de defender su propio saque. Se mide POR TIPO: el agregado de la primera versión escondía que el
/// saque de puerta y el de banda no cambiaban (revisión independiente).
/// </summary>
public sealed class RestartPossessionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// MEDIDO (40 semillas, 27 sep 2026): con la regla, banda 91,6 %, puerta 96,6 %, falta 97,2 %, córner
    /// 100 %; con la primera versión (sólo una de las dos posesiones), puerta 11 % y banda 15 %; sin regla,
    /// 0-6 %. 70 separa con margen la regla completa de las otras dos.
    /// </summary>
    private const double MinReceivingPercent = 70.0;

    private readonly ITestOutputHelper _output;

    public RestartPossessionTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheTakingTeamGetsReadyToReceiveInEveryKindOfRestart()
    {
        var totals = Measure();
        foreach (var kind in new[] { RestartKind.ThrowIn, RestartKind.GoalKick, RestartKind.FreeKick, RestartKind.Corner })
        {
            if (!totals.TryGetValue(kind, out var t) || t.Frames < 100)
            {
                continue;
            }

            double share = 100.0 * t.Receiving / t.Frames;
            _output.WriteLine($"{kind,-9} {share:0.0} % preparándose para recibir ({t.Frames} fotogramas)");
            Assert.True(share >= MinReceivingPercent, $"{kind}: sólo el {share:0.0} % del equipo que saca se prepara para recibir");
        }

        // Sin muestra no se mide nada: la primera versión se saltaba en silencio los tipos con menos de 100
        // fotogramas (revisión independiente). Banda, puerta y falta tienen que estar.
        foreach (var kind in new[] { RestartKind.ThrowIn, RestartKind.GoalKick, RestartKind.FreeKick })
        {
            Assert.True(totals.GetValueOrDefault(kind).Frames > 1000, $"casi no hay {kind} en la muestra");
        }
    }

    /// <summary>El penalti queda fuera de la regla: su colocación la gobierna la ADR 0143.</summary>
    [Fact]
    public void ThePenaltyIsLeftOut()
    {
        var totals = Measure();
        if (totals.TryGetValue(RestartKind.Penalty, out var t) && t.Frames > 100)
        {
            Assert.True(100.0 * t.Receiving / t.Frames < 20.0, "el penalti ha entrado en la regla de la ADR 0155 sin decidirlo");
        }
    }

    private static Dictionary<RestartKind, (long Frames, long Receiving)> Measure()
    {
        var totals = new Dictionary<RestartKind, (long Frames, long Receiving)>();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true }).Trace!;
            for (int f = 0; f < trace.FrameCount; f++)
            {
                var kind = trace.RestartAt(f);
                int taker = trace.RestartTakerAt(f);
                if (kind is RestartKind.None or RestartKind.Kickoff || taker < 0)
                {
                    continue;
                }

                int team = trace.Players[taker].Team;
                for (int i = 0; i < trace.Players.Count; i++)
                {
                    if (i == taker || trace.Players[i].Team != team || trace.Players[i].Role == Position.Goalkeeper || !trace.OnPitchAt(f, i))
                    {
                        continue;
                    }

                    var (frames, receiving) = totals.GetValueOrDefault(kind);
                    bool ready = trace.ActionAt(f, i) is PlayerAction.FindSpace or PlayerAction.OfferSupport;
                    totals[kind] = (frames + 1, receiving + (ready ? 1 : 0));
                }
            }
        }

        return totals;
    }
}
