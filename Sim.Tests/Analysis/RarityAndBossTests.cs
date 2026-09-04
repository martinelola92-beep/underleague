using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Métricas obligatorias de rareza y jefe final (ADR 0027, RF-023b, RF-024; tabla de métricas de diseño
/// de docs/balance.md). Son las dos que la ADR 0027 dejó como condición para no tener que revisarse:
/// un común de nivel máximo equivale a un legendario de nivel 2, pierde con claridad contra uno de nivel
/// alto, y un equipo <b>sin ningún legendario</b> puede ganarle al jefe final si el jugador ha jugado bien.
///
/// <para><b>Cómo está construido el jefe final</b> (RF-001c; no existe todavía como sistema de campaña):
/// once... siete titulares y tres suplentes generados con <c>rarity: legendary</c> y
/// <c>level: <see cref="BossLevel"/></c> sobre la misma calidad 50 que el resto del conjunto de
/// referencia, sin perks. Es la lectura literal de "plantilla íntegramente legendaria": todo lo que
/// cambia respecto de un rival normal es el presupuesto de atributos que da la rareza (300 frente a 250)
/// y el nivel. Cuando la fase 2 defina el jefe de verdad (perks, objetos, condición de derrota propia),
/// esta construcción hay que sustituirla por la real y volver a medir.</para>
///
/// <para><b>Qué es "haber jugado bien"</b>: la plantilla del jugador es una build coherente
/// (<see cref="PlayerBuild"/>) con todos sus jugadores <b>comunes</b> y a nivel máximo, que es el techo
/// de una run en la que no ha tocado ni un legendario. La tasa mínima aceptable se fija en
/// <see cref="MinBossWinRate"/>%: por debajo de eso la run es invencible sin legendarios y la decisión de
/// la ADR 0027 hay que revisarla.</para>
///
/// <para>Muestra: <see cref="Rosters"/> plantillas × <see cref="MatchesPerRoster"/> partidos, semilla
/// <see cref="Seed"/>, local y visitante alternados. Categoría <c>Gate</c> como el resto de puertas.</para>
/// </summary>
[Trait("Category", "Gate")]
public sealed class RarityAndBossTests
{
    private const int Rosters = 24;
    private const int MatchesPerRoster = 20;
    private const ulong Seed = 1;
    private const int PrimaryIdBase = 1;
    private const int SecondaryIdBase = 100001;

    /// <summary>Nivel máximo de un jugador (RF-023, Progression.MaxLevel).</summary>
    private const int MaxLevel = 8;

    /// <summary>Nivel de la plantilla del jefe final: máximo, como el de un jugador que ha llegado al final.</summary>
    private const int BossLevel = 8;

    /// <summary>Tasa mínima con la que un equipo sin legendarios tiene que poder ganarle al jefe (ADR 0027).</summary>
    private const double MinBossWinRate = 25.0;

    /// <summary>Build coherente que representa "el jugador ha jugado bien".</summary>
    private const string PlayerBuild = "human_wall";

    /// <summary>Build sin perks de la misma raza, para las comparaciones "en igualdad de perks" (RF-024).</summary>
    private const string NeutralBuild = "human_none";

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    /// <summary>
    /// RF-024 (v0.9.1, ADR 0027): un común de nivel 8 queda entre el 45% y el 55% frente a un legendario
    /// de nivel 2 en igualdad de perks. "Igualdad de perks" se lee como ninguno de los dos lados: lo que
    /// se compara es el presupuesto de atributos que dan rareza y nivel (306 frente a 308).
    /// </summary>
    [Fact]
    public void CommonAtMaxLevelMatchesALegendaryAtLevelTwo()
    {
        double rate = WinRate(NeutralBuild, MaxLevel, Rarity.Common, NeutralBuild, 2, Rarity.Legendary);
        Assert.InRange(rate, 45.0, 55.0);
    }

    /// <summary>
    /// RF-024, segunda mitad: contra un legendario de nivel alto el común pierde con claridad. "Con
    /// claridad" se fija en menos del 40%, que es el umbral por debajo del cual RT-055 ya considera que
    /// una build no es competitiva.
    /// </summary>
    [Fact]
    public void CommonAtMaxLevelLosesClearlyToALegendaryAtMaxLevel()
    {
        double rate = WinRate(NeutralBuild, MaxLevel, Rarity.Common, NeutralBuild, MaxLevel, Rarity.Legendary);
        Assert.InRange(rate, 0.0, 40.0);
    }

    /// <summary>
    /// Salvaguarda de la ADR 0027: un equipo sin ningún legendario tiene que poder ganarle al jefe final.
    /// Si esta métrica falla, la decisión de que el legendario sea netamente superior hay que revisarla.
    /// </summary>
    [Fact]
    public void ATeamWithoutLegendariesCanBeatTheFinalBoss()
    {
        double rate = WinRate(PlayerBuild, MaxLevel, Rarity.Common, NeutralBuild, BossLevel, Rarity.Legendary);
        Assert.True(
            rate >= MinBossWinRate,
            $"un equipo sin legendarios gana al jefe final el {rate:F2}% de las veces, por debajo del {MinBossWinRate}% "
                + "que la ADR 0027 fija como condición para no revisarla");
    }

    /// <summary>
    /// Tasa de victoria de la build <paramref name="build"/> (con el nivel y la rareza indicados) contra
    /// <paramref name="opponent"/>, con plantillas emparejadas: los dos equipos salen del mismo índice de
    /// generación, así que solo cambian rareza, nivel y perks. Cada plantilla juega los mismos partidos de
    /// local y de visitante y con los ids bajos y altos alternados (metodología del paquete I).
    /// </summary>
    private static double WinRate(string build, int level, Rarity rarity, string opponent, int opponentLevel, Rarity opponentRarity)
    {
        var catalog = TestData.LoadCatalog();
        var builds = BuildFile.LoadAll(TestData.DataDirectory);
        var config = new SimConfig(CollectLog: false);

        int matches = 0;
        int wins = 0;
        int matchIndex = 0;

        for (int roster = 0; roster < Rosters; roster++)
        {
            for (int k = 0; k < MatchesPerRoster; k++)
            {
                bool subjectAway = (k % 2) == 1;
                bool subjectHasHighIds = ((k / 2) % 2) == 1;
                int subjectIdBase = subjectHasHighIds ? SecondaryIdBase : PrimaryIdBase;
                int opponentIdBase = subjectHasHighIds ? PrimaryIdBase : SecondaryIdBase;

                var subjectTeam = builds[build].ToTeamSetup(catalog, Seed, roster, subjectIdBase, level, rarity);
                var opponentTeam = builds[opponent].ToTeamSetup(catalog, Seed, roster, opponentIdBase, opponentLevel, opponentRarity);

                var setup = subjectAway
                    ? new MatchSetup(opponentTeam, subjectTeam, Referee)
                    : new MatchSetup(subjectTeam, opponentTeam, Referee);

                var report = Simulator.Run(setup, RngStreams.MatchSeed(Seed, matchIndex++), catalog, config).Report;
                matches++;
                if (report.Winner == (subjectAway ? 1 : 0))
                {
                    wins++;
                }
            }
        }

        return 100.0 * wins / matches;
    }
}
