using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Tests;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// La familia médica de RF-084 protege a los propios, no desarma al rival (CAT-A de docs/pendientes.md).
///
/// <para>El motor tiene dos canales distintos para la lesión de una entrada
/// (<c>MatchEngine</c>: <c>Odds(tackler, Injure)</c> contra <c>Odds(victim, Injury)</c>): <b>injure</b> es
/// la probabilidad de que el ENTRANTE lesione, e <b>injury</b> la de que la VÍCTIMA se lesione. Un
/// consumible no tiene portador y alcanza a todo su equipo sobre el campo
/// (<c>EffectEngine.ResolveConsumables</c>), así que la elección de canal decide a QUÉ EQUIPO protege:
/// con <c>injure</c> el vendaje reducía a la mitad la capacidad de los propios de lesionar al rival —el
/// efecto de un consumible sucio invertido—, y con <c>injury</c> reduce la de los propios de lesionarse,
/// que es lo que pide la familia médica.</para>
///
/// <para>Es la clase de test que la regla 9 del proyecto pide para esto: la unitaria no distingue los dos
/// canales porque los dos "hacen algo", y solo la medida sobre muchos partidos enseña a quién protege.</para>
/// </summary>
public sealed class MedicalConsumableTests
{
    private const int Matches = 600;

    private static readonly ConsumableCatalog Consumables =
        StandardRunSystems.FromJson(TestData.LoadAllFiles()).Consumables;

    /// <summary>
    /// El dato, no el motor: <c>field_bandage</c> tiene que escribir el canal de la VÍCTIMA. Es la
    /// guarda barata de CAT-A —cambiarlo de vuelta a <c>injure</c> invierte a quién protege sin romper
    /// ningún esquema, porque los dos canales son válidos en <c>consumables.schema.json</c>.
    /// </summary>
    [Fact]
    public void TheFieldBandage_WritesTheVictimChannel()
    {
        var bandage = Consumables.Find("field_bandage");
        Assert.NotNull(bandage);
        var effect = Assert.Single(bandage!.Effects);
        Assert.Equal(EffectType.ModifyProbability, effect.Type);
        Assert.Equal(ProbabilityKind.Injury, effect.Probability);
        // El cargador ya ha convertido el -100 de /data en el multiplicador de cuota de la ADR 0050 P1,
        // en base 10.000: k = 1 / (1 + 100/100) = 0,5, es decir la MITAD de cuota, no "cero lesiones".
        Assert.Equal(ProbabilityScale.Neutral / 2, effect.Value);
    }

    /// <summary>
    /// Y el efecto medido: el vendaje del local baja las lesiones DEL LOCAL y deja las del visitante
    /// donde estaban. Con el canal equivocado las dos desigualdades se invierten, que es lo que hace
    /// que este test valga y la unitaria no.
    /// </summary>
    [Fact]
    public void TheFieldBandage_LowersItsOwnInjuriesAndNotTheOpponents()
    {
        var (baseHome, baseAway) = Play(withBandage: false);
        var (bandagedHome, bandagedAway) = Play(withBandage: true);

        // El local se lesiona menos. El margen es holgado a propósito: la mitad de cuota sobre el canal
        // de la víctima mueve el recuento muy por encima del ruido de 600 partidos.
        Assert.True(
            bandagedHome < baseHome * 0.85,
            $"el vendaje tiene que bajar las lesiones del local: {baseHome} -> {bandagedHome}");

        // El visitante no. Banda del ±20% alrededor del recuento base: no se pide igualdad exacta porque
        // menos lesiones en el local cambian quién sigue en el campo y eso mueve un poco al rival.
        Assert.InRange(bandagedAway, baseAway * 0.8, baseAway * 1.2);
    }

    /// <summary>
    /// Juega <see cref="Matches"/> partidos de referencia y devuelve las lesiones sufridas por cada
    /// equipo. Sigue el patrón de arnés del proyecto: <c>Parallel.For</c> por índice con la semilla
    /// función pura del índice, un catálogo por hilo (<see cref="ThreadCatalogs"/>) y la reducción
    /// después, en orden.
    /// </summary>
    private static (int Home, int Away) Play(bool withBandage)
    {
        var injuries = new (int Home, int Away)[Matches];
        Parallel.For(0, Matches, i =>
        {
            var catalog = ThreadCatalogs.Current;
            ulong seed = RngStreams.MatchSeed(1, i);
            var setup = TestMatches.Reference(catalog, seed);
            if (withBandage)
            {
                var bandage = Consumables.Find("field_bandage")!;
                setup = setup with
                {
                    Home = setup.Home with
                    {
                        Consumables = new[]
                        {
                            new MatchConsumable(bandage.Id, bandage.Rarity, bandage.Effects, ConsumableTrigger.Manual)
                            {
                                ManualTick = 0,
                            },
                        },
                    },
                };
            }

            var report = Simulator.Run(setup, seed, catalog, new SimConfig(CollectLog: false)).Report;
            int home = 0, away = 0;
            for (int p = 0; p < report.Players.Count; p++)
            {
                if (!report.Players[p].Injured)
                {
                    continue;
                }

                if (report.Players[p].Team == 0)
                {
                    home++;
                }
                else
                {
                    away++;
                }
            }

            injuries[i] = (home, away);
        });

        int totalHome = 0, totalAway = 0;
        for (int i = 0; i < injuries.Length; i++)
        {
            totalHome += injuries[i].Home;
            totalAway += injuries[i].Away;
        }

        return (totalHome, totalAway);
    }
}
