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
/// <b>Lo que resultó NO ser la causa</b> (corrige la hipótesis de D-29). Con el sesgo de atributos
/// original de los elfos intacto, doblar <c>discipline</c> de 35 a 70 no movió la tasa de victoria ni una
/// décima (61,23% en los dos casos, misma semilla y mismas plantillas). Igualando <c>attributeBias</c>,
/// <c>bodyRadius</c> (30) y <c>discipline</c> (55) de los elfos exactamente a los de los humanos —perfil
/// físico y de atributos idéntico— los elfos seguían ganando el 59,85% contra <c>human_none</c> en
/// solitario. La causa dominante es <c>elf_touch</c> (ADR 0026, RF-031b): +10 pp de resistencia a las
/// entradas y +10 pp de resistencia a las intercepciones para <b>toda</b> la plantilla, siempre activa
/// (la concede la raza, no ocupa slot y no se puede quitar de una build "sin perks"). No es una palanca
/// que este encargo pueda tocar (no está en la lista de campos autorizados de D-29: <c>attributeBias</c>,
/// <c>bodyRadius</c>, <c>discipline</c>, <c>styleTagWeights</c>).
/// </para>
///
/// <para>
/// <b>Por qué la calibración final solo toca fuerza, y con un margen ajustado.</b> <c>bodyRadius</c>
/// parecía la palanca más prometedora (subirlo diluye la ventaja de <c>elf_touch</c> sin tocarlo), pero
/// <c>Sim.Tests.Engine.BodiesTests.TheLighterBodyTakesTheLargerShareOfThePush</c> fija que el elfo es el
/// cuerpo <b>ligero</b> del catálogo frente al orco (§2.1.2: "el ligero se lleva la parte mayor del
/// empuje"): con <c>bodyRadius</c> por encima de ~32 el solape con un orco a distancia de contacto supera
/// el tope de empuje por tick de los dos cuerpos a la vez, la puerta de física se rompe (empuje 50/50 en
/// vez de a favor del elfo) y, aunque numéricamente la tasa de victoria mejoraba con radios de hasta 60,
/// era una mejora **de un modelo de físicas roto**, no del diseño. Se descarta por completo (queda en 30,
/// el valor original) y la puerta de <c>BodiesTests</c> no se toca (regla del encargo: "si tu ajuste
/// racial mueve alguna puerta, reajusta tu propio cambio, no la puerta"). Con <c>bodyRadius</c> fuera de
/// juego, <c>discipline</c> demostrado inerte (arriba) y cualquier sesgo positivo de velocidad o técnica
/// subiendo la tasa por encima del 60% por pequeño que fuera (el sesgo original ya lo hacía), el único
/// margen que queda es <c>attributeBias.strength</c> en negativo. También tiene techo: los atributos se
/// recortan a [1, 99] (RT-023), así que a partir de unos -35 casi toda la plantilla ya está en el suelo y
/// bajar más (probado hasta -70) no cambia nada medible. El resultado no es un margen amplio: la tasa
/// pooled de los elfos medida en el cierre está entre el 58,8% y el 60,3% según la muestra (D-29 en
/// <c>docs/pendientes.md</c> documenta el rango completo), así que esta puerta usa <see cref="Rosters"/>
/// alto para acercarse al valor poblacional. <c>styleTagWeights</c> no se toca: ningún perk de una build
/// <c>*_none</c> consulta la etiqueta de estilo, así que no tiene ningún efecto mecánico que medir aquí.
/// </para>
///
/// <para>
/// <b>Metodología</b>: todas-contra-todos entre las cinco referencias de raza sin ningún perk
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
/// raza que promediar contra sí misma), así que <see cref="Rosters"/> se fija muy alto (250, frente a los
/// 40 de <see cref="BuildGateTests"/>) y <see cref="MatchesPerRoster"/> al mínimo (4, un solo ciclo
/// local/visitante × reparto de ids): más plantillas compran más estabilidad por segundo de puerta que más
/// partidos por plantilla. Aun así, el margen de los elfos contra el techo del 60% es el más ajustado de
/// todas las puertas de fase 1 (por debajo de 1 punto en la medición de cierre): es el límite real de lo
/// que <c>attributeBias</c>/<c>bodyRadius</c>/<c>discipline</c> pueden corregir mientras <c>elf_touch</c>
/// no se toque, y queda anotado como D-29 sin cerrar del todo en <c>docs/pendientes.md</c>: si un cambio
/// futuro en <c>/Sim</c> o en <c>data/perks/elf_touch.json</c> desplaza esta tasa, esta puerta lo dirá, y
/// puede que la solución completa exija por fin revisar <c>elf_touch</c>, fuera del alcance de este
/// encargo. Categoría <c>Gate</c> como el resto de puertas de fase 1.
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
