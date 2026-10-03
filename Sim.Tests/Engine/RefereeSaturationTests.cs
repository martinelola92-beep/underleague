using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0158 §3/§6, revisión independiente (27 sep 2026): "el criterio pesa" es de sensación, no de
/// décima, pero tiene un objetivo verificable -que un partido sucio lleve el criterio más allá de ±30- y
/// un techo -que no sature en ±100 de forma sistemática-. Con las magnitudes 5/3/3/2/5/3/8 el |criterio|
/// medio de un partido brutal con árbitro neutro llegaba a 56, saturando en ±100 muchas veces; con
/// 3/2/2/1/3/2/5 la mayoría de partidos se quedaba por debajo de ±30. Las cifras actuales
/// (<c>data/sim/tuning.json</c>) son el punto intermedio medido aquí.
/// </summary>
public sealed class RefereeSaturationTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// 150, la muestra de la calibración. Hasta la ADR 0184 eran 60 «sin perder la conclusión», pero con 60 la mayoría se
    /// decide por uno o dos partidos: el motor de antes daba 33 de 60 y 76 de 150 (50,7 %); con la sostenida y las reglas
    /// de BV-B (ADR 0186), 29 de 60 y 83 de 150 (55,3 %, |criterio| medio 38,2). La conclusión que pide la ADR 0158 §3 se
    /// sostiene con la muestra de su calibración, y el test tarda unos segundos más.
    /// </summary>
    private const int Seeds = 150;

    [Fact]
    public void ABrutalMatchWithANeutralRefereeMostlyGoesBeyondThirtyAndStaysBelowSixtyOnAverage()
    {
        var setup = TestMatches.Brutal(Catalog);
        int over30 = 0;
        long sumAbs = 0;

        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            int abs = Math.Abs(result.Report.FinalBias);
            sumAbs += abs;
            if (abs > 30)
            {
                over30++;
            }
        }

        double meanAbs = (double)sumAbs / Seeds;

        Assert.True(
            over30 > Seeds / 2,
            $"solo {over30} de {Seeds} partidos superaron ±30 de criterio final; el objetivo de la ADR 0158 §3 pide mayoría");
        Assert.True(
            meanAbs < 60,
            $"el |criterio| final medio fue {meanAbs:F1}, por encima del techo de 60 (ADR 0158 §3, revisión independiente)");
    }
}
