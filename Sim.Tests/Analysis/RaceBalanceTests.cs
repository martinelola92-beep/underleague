using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// D-29 (<c>docs/pendientes.md</c>): el equilibrio entre las cinco razas de lanzamiento, <b>sin ningún
/// perk asignado</b>, no lo vigilaba ninguna puerta. El reajuste de fase 1 (paquete U) midió 20 puntos de
/// diferencia entre elfos (68,5%) y no-muertos (48,5%) contra <c>human_none</c> en solitario, dentro de la
/// banda 30-70% de RT-055 pero incumpliendo su espíritu (RF-032: cada raza sostiene tres builds viables).
/// Esta puerta mide cada raza contra las OTRAS CUATRO en todas-contra-todos, que es el punto de
/// comparación correcto para el diseño de una raza (comparar solo contra <c>human_none</c>, como hacía
/// D-29, mide "raza contra humanos", no "raza contra el resto").
///
/// <para>
/// <b>Lo que resultó NO ser la causa.</b> Ni <c>discipline</c> (doblarlo de 35 a 70 no movió la tasa ni
/// una décima), ni <c>bodyRadius</c> (subirlo rompe
/// <c>Sim.Tests.Engine.BodiesTests.TheLighterBodyTakesTheLargerShareOfThePush</c>, que fija al elfo como
/// el cuerpo ligero del catálogo), ni el sesgo de atributos. Este último es la trampa que costó un
/// intento entero: <c>attributeBias</c> se aplica <b>antes</b> de renormalizar al presupuesto de
/// generación (<c>PlayerGenerator.GenerateAttributes</c>, pasos 3 a 5), así que no añade puntos, los
/// <b>reparte</b>, y como palanca de balance casi no mueve nada. Medido: con las habilidades apagadas,
/// las cinco razas caben en 3,5 puntos (enanos 49,5 · elfos 51,0 · humanos 49,3 · orcos 51,8 ·
/// no-muertos 48,3). Los sesgos de <c>data/races/*.json</c> ya estaban bien; describen a la raza
/// (RF-024b, tabla §3.4) y no se tocan para balancear.
/// </para>
///
/// <para>
/// <b>La causa era el presupuesto de las habilidades raciales</b> (RF-031b, ADR 0026), que la ADR no
/// había fijado: repartía un canal distinto a cada raza pero no cuánto podía valer. Apagando cada
/// habilidad y volviendo a medir (1.000 plantillas por pareja, 40.000 partidos, semilla 1): Toque valía
/// <b>+10,4</b> puntos y las otras cuatro entre 0 y +0,9. Y no era una cifra mal calibrada sino un canal
/// ilegal: la mitad de <c>interceptEvasion</c> valía ella sola +6,6 porque <c>intercept</c> tiene base
/// 250 y el escalón mínimo de la escala de puntos porcentuales son 500, así que el valor legal más
/// pequeño que se puede escribir no es "esquivar mejor" sino "ser inmune" (D-30). La ADR 0026 recoge
/// ahora el criterio de presupuesto (§"Presupuesto de impacto"): canal legal antes que valor, techo de un
/// escalón de la escala (+2,5 puntos de tasa agrupada), y presupuesto aparte para las dos habilidades que
/// actúan fuera del partido. Con Toque en <c>tackleEvasion</c> +5 y sin la mitad de intercepción, y
/// Sangre caliente calibrada de 5 a 15 ticks para subir al mismo techo, las cinco quedan entre +0 y +1,7.
/// </para>
///
/// <para>
/// <b>Metodología</b>: todas-contra-todas entre las cinco referencias de raza sin ningún perk
/// (<c>data/balance/builds/*_none.json</c>: <see cref="Races"/>), plantillas independientes por raza
/// (aquí no hay build gemela que comparar, a diferencia de <see cref="BuildGateTests"/>), local/visitante
/// y reparto de ids alternados en las cuatro combinaciones (metodología del paquete I). Cada raza juega
/// las otras cuatro y la métrica es la tasa de victoria <b>pooled</b> sobre esas cuatro celdas (matches
/// iguales por celda, así que pooled = media aritmética de las cuatro). El criterio (D-29, más estricto
/// que RT-055 porque compara contra la media de las demás razas en vez de contra una única referencia) es
/// que ninguna raza se salga de <see cref="MinPooledWinRate"/>-<see cref="MaxPooledWinRate"/>%.
/// </para>
///
/// <para>
/// Muestra: <see cref="Rosters"/> plantillas × <see cref="MatchesPerRoster"/> partidos por pareja de
/// razas, diez parejas (todas-contra-todos de cinco razas) = <see cref="Rosters"/>×<see
/// cref="MatchesPerRoster"/>×10 = 10.000 partidos, semilla <see cref="Seed"/>, unos 45 s. La varianza
/// dominante en esta medida es de plantilla a plantilla, no de partido a partido dentro de la misma
/// plantilla (a diferencia de <see cref="BuildGateTests"/>, aquí no hay una única plantilla emparejada por
/// raza que promediar contra sí misma): las cuatro repeticiones de una plantilla son casi la misma
/// observación, así que la unidad efectiva es la plantilla y el error típico por raza ronda 1,6 puntos a
/// 250 plantillas. Por eso <see cref="Rosters"/> se fija muy alto (250, frente a los 40 de
/// <see cref="BuildGateTests"/>) y <see cref="MatchesPerRoster"/> al mínimo (4, un solo ciclo
/// local/visitante × reparto de ids): más plantillas compran más estabilidad por segundo de puerta que
/// más partidos por plantilla.
/// </para>
///
/// <para>
/// Medición de cierre del reequilibrio, con esta misma muestra y semilla: enanos 47,55 · elfos 54,12 ·
/// humanos 48,95 · orcos 51,80 · no-muertos 47,58. El margen más ajustado es el de los elfos contra el
/// techo, <b>5,9 puntos</b> (antes del reequilibrio estaba por debajo de 1). Con 1.000 plantillas, que es
/// el valor poblacional, se aprietan más todavía: 49,07 · 52,89 · 48,23 · 52,48 · 47,34. Categoría
/// <c>Gate</c> como el resto de puertas de fase 1.
/// </para>
/// </summary>
[Trait("Category", "Gate")]
public sealed class RaceBalanceTests
{
    /// <summary>Referencias de raza sin ningún perk, orden alfabético (docs/balance.md).</summary>
    private static readonly string[] Races =
    {
        "dwarf_none", "elf_none", "human_none", "orc_none", "undead_none",
    };

    /// <summary>Plantillas distintas por pareja de razas sobre las que se promedia cada celda.</summary>
    private const int Rosters = 250;

    /// <summary>Partidos por plantilla y pareja (múltiplo de 4: local/visitante × reparto de ids).</summary>
    private const int MatchesPerRoster = 4;

    /// <summary>Semilla base del lote de la puerta.</summary>
    private const ulong Seed = 1;

    /// <summary>Primer id de jugador del equipo que lleva los ids bajos.</summary>
    private const int PrimaryIdBase = 1;

    /// <summary>Primer id de jugador del equipo que lleva los ids altos.</summary>
    private const int SecondaryIdBase = 100001;

    /// <summary>D-29: ninguna raza gana menos del 40% contra la media de las otras cuatro.</summary>
    private const double MinPooledWinRate = 40.0;

    /// <summary>D-29: ninguna raza gana más del 60% contra la media de las otras cuatro.</summary>
    private const double MaxPooledWinRate = 60.0;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private static readonly Lazy<IReadOnlyDictionary<string, double>> PooledWinRates = new(ComputePooledWinRates);

    /// <summary>
    /// D-29: sin ningún perk, ninguna raza de lanzamiento se sale de 40%-60% contra la media de las otras
    /// cuatro. Un solo <c>[Fact]</c> con las cinco razas nombradas en el mensaje de fallo: si una raza se
    /// sale de la banda, el mensaje dice cuál y con qué margen, sin tener que cruzar con el log de la
    /// puerta.
    /// </summary>
    [Fact]
    public void NoLaunchRaceDominatesOrUnderperformsWithoutPerks()
    {
        var rates = PooledWinRates.Value;
        var offenders = rates
            .Where(kv => kv.Value < MinPooledWinRate || kv.Value > MaxPooledWinRate)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value:F2}%")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "razas fuera de 40%-60% contra la media de las otras cuatro (D-29): " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Simula las diez parejas de la matriz todas-contra-todos y devuelve, por raza, la tasa de victoria
    /// pooled sobre sus cuatro emparejamientos. Cada pareja se juega en las cuatro combinaciones de
    /// (local, visitante) × (ids bajos, ids altos) para que ni la ventaja de jugar en casa ni el
    /// desempate por id favorezcan sistemáticamente a una de las dos razas (metodología del paquete I,
    /// igual que <see cref="BuildGateTests"/> y <see cref="RarityAndBossTests"/>).
    /// </summary>
    private static IReadOnlyDictionary<string, double> ComputePooledWinRates()
    {
        var catalog = TestData.LoadCatalog();
        var builds = BuildFile.LoadAll(TestData.DataDirectory);
        var config = new SimConfig(CollectLog: false);

        var wins = Races.ToDictionary(r => r, _ => 0, StringComparer.Ordinal);
        var matches = Races.ToDictionary(r => r, _ => 0, StringComparer.Ordinal);
        int matchIndex = 0;

        for (int i = 0; i < Races.Length; i++)
        {
            for (int j = i + 1; j < Races.Length; j++)
            {
                string raceA = Races[i];
                string raceB = Races[j];
                var buildA = builds[raceA];
                var buildB = builds[raceB];

                for (int roster = 0; roster < Rosters; roster++)
                {
                    for (int k = 0; k < MatchesPerRoster; k++)
                    {
                        bool aAway = (k % 2) == 1;
                        bool aHasHighIds = ((k / 2) % 2) == 1;
                        int aIdBase = aHasHighIds ? SecondaryIdBase : PrimaryIdBase;
                        int bIdBase = aHasHighIds ? PrimaryIdBase : SecondaryIdBase;

                        var teamA = buildA.ToTeamSetup(catalog, Seed, roster, aIdBase);
                        var teamB = buildB.ToTeamSetup(catalog, Seed, roster, bIdBase);

                        var setup = aAway
                            ? new MatchSetup(teamB, teamA, Referee)
                            : new MatchSetup(teamA, teamB, Referee);

                        var report = Simulator.Run(setup, RngStreams.MatchSeed(Seed, matchIndex++), catalog, config).Report;

                        int aSide = aAway ? 1 : 0;
                        matches[raceA]++;
                        matches[raceB]++;
                        if (report.Winner == aSide)
                        {
                            wins[raceA]++;
                        }
                        else if (report.Winner == 1 - aSide)
                        {
                            wins[raceB]++;
                        }
                    }
                }
            }
        }

        return Races.ToDictionary(
            r => r,
            r => 100.0 * wins[r] / matches[r],
            StringComparer.Ordinal);
    }
}
