using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Items;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// <b>Cuánto aporta equipar</b> (ADR 0033). El escalón "muy buena" de la escala de calidad de build se
/// define como "buena, además equipada", así que ese escalón solo existe si el equipamiento mueve la
/// aguja de verdad. Aquí se mide, con la misma receta que un lote de <c>/Balance</c>
/// (<c>--rosters</c> y <c>--home-away</c>): varias plantillas generadas por arm, cada emparejamiento
/// jugado en las dos direcciones para que la ventaja de campo no contamine, y el <b>mismo</b> par de
/// equipos con y sin objetos.
///
/// <para>El punto de comparación es un espejo: los dos equipos son plantillas de la misma raza y calidad
/// con sus perks iniciales repartidos igual que al empezar una run
/// (<see cref="PerkAssignment.AssignInitial"/>). Sobre ese 50% de partida, lo que suba el equipo
/// equipado <b>es</b> lo que aporta equipar, sin nada más de por medio.</para>
/// </summary>
[Trait("Category", "Gate")]
[Collection("Gate")]
public sealed class EquipmentImpactTests
{
    /// <summary>
    /// Plantillas distintas sobre las que se promedia (equivalente a <c>--rosters</c>). Subido de 8 a 24
    /// en <c>b241611</c> (paquete Z), sin tocar el umbral: con 8 el brazo de cada arm eran 512 partidos.
    /// Remedido en worktree (sexta ronda, independent-reviewer, BA-N): con 8 plantillas la puerta daba
    /// **8,2** en los tres commits anteriores a `b241611` y **7,4** en el último de ellos (`044eabb`); tras
    /// `b241611` (que en el mismo commit sube `Rosters` a 24, reescribe la economía y añade ocho perks al
    /// catálogo entre otros 40 ficheros — no un cambio aislado de catálogo) el mismo instrumento de 8
    /// plantillas mide **4,7**. El movimiento real es de **2,7**, no de una décima suelta, y sigue sin
    /// aislar qué parte de `b241611` lo causó — no se atribuye al catálogo de perks en concreto, es la
    /// misma clase de correlación temporal sin aislar que ADR 0116 (BA-N) marcó REJECTED para el paso de
    /// 61 a 94 perks. Con 24 la desviación entre arms baja a ~1,8 y el test avisa de una regresión de
    /// verdad (cifra de dispersión sin remedir todavía; extrapolada de la de 96 plantillas por 1/√n).
    /// </summary>
    // Paquete AZ (ADR 0090): de 24 a 96 plantillas. Con 24 (1.536 partidos por brazo, 24×32×2 direcciones)
    // la diferencia de dos tasas tenía un error típico de ~1,8 puntos y el umbral de 3,0 quedaba dentro
    // del ruido (medido 3,0 justo tras la tanda 2). Con 96 plantillas (6.144 partidos por brazo) el error
    // baja a ~0,9; en Release son segundos. Corregido aquí (independent-reviewer, BA-N, tercera ronda):
    // esta nota decía "768" y luego "3.072" -las dos, la mitad de las cifras reales por no contar la
    // vuelta (×2 direcciones)-, el mismo error de aritmética cometido dos veces en la misma frase.
    private const int Rosters = 96;

    /// <summary>Partidos por plantilla y dirección; con ida y vuelta salen 2x (equivalente a <c>--home-away</c>).</summary>
    private const int MatchesPerRoster = 32;

    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly ItemCatalog Items = ItemLoader.FromJson(TestData.LoadAllFiles());

    /// <summary>
    /// Equipamiento de una build "muy buena": un objeto por titular (RF-076), elegido con criterio para
    /// su puesto y con la mezcla de rarezas que una run llega a reunir en el acto 3 (ADR 0040: dos raros,
    /// tres poco comunes y dos comunes), no siete comunes. El maldito va donde su contrapartida no duele:
    /// <c>berserker_totem</c> baja técnica y se le da a un central, que es de lo que trata la ADR 0036.
    /// </summary>
    private static readonly string[] Loadout =
    {
        "veteran_armband",      // 0 GK  (poco común: técnica y resistencia)
        "berserker_totem",      // 1 DEF (raro maldito: bestia física que no sabe jugar)
        "weighted_wraps",       // 2 DEF (poco común: fuerza y resistencia)
        "champions_sash",       // 3 MID (raro)
        "duelists_gloves",      // 4 MID (poco común: velocidad y técnica)
        "iron_gauntlets",       // 5 MID (común: fuerza)
        "worn_boots",           // 6 FWD (común: velocidad)
    };

    private readonly ITestOutputHelper _output;

    public EquipmentImpactTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Bases de semilla sobre las que se promedia (BB-T, 19 sep 2026). Antes había UNA sola, la 1000, y
    /// eso era el problema: con un error típico de ~0,9 por base contra un umbral de 1,0, esta puerta
    /// cruzaba el listón por puro muestreo cada ~6 % de commits —el falso positivo que la ADR 0116
    /// presupuestó y aceptó—, y ya había costado tres investigaciones (BA-M, BA-N, BB-T).
    ///
    /// <para>Medido en BB-T con el motor congelado byte a byte: la 1000 sale la MÁS BAJA de las ocho en
    /// dos catálogos distintos (1,40 con 94 perks, 0,46 con 102), mientras las otras siete van de 2,0 a
    /// 4,05. Promediando las ocho, el error típico de la media baja de ~0,9 a ~0,3.</para>
    ///
    /// <para><b>El umbral NO se toca aquí</b>: sigue en 1,0. Esto es solo instrumentación — primero se
    /// mide con la muestra buena y se guarda el resultado; decidir si 1,0 se queda corto es un cambio de
    /// rango de balance y pide su ADR (RT-057).</para>
    /// </summary>
    private static readonly ulong[] SeedBases = { 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000 };

    [Fact]
    public void EquippingAGoodBuildIsWorthSeveralPointsOfWinRate()
    {
        int matchesPerArm = Rosters * MatchesPerRoster * 2;

        // El paralelismo vive en el arnés, nunca en /Sim: Parallel.For por índice, cada semilla función
        // pura del índice, un Catalog por hilo (las condiciones compiladas no son reentrantes) y las
        // reducciones después, en orden. Salida idéntica a la del bucle secuencial.
        var bare = new int[SeedBases.Length * Rosters];
        var equipped = new int[SeedBases.Length * Rosters];
        Parallel.For(0, SeedBases.Length * Rosters, i =>
        {
            var catalog = ThreadCatalogs.Current;
            ulong rosterSeed = SeedBases[i / Rosters] + (ulong)(i % Rosters);
            bare[i] = WinsOfRoster(catalog, rosterSeed, equipped: false);
            equipped[i] = WinsOfRoster(catalog, rosterSeed, equipped: true);
        });

        var deltas = new List<double>(SeedBases.Length);
        var bareRates = new List<double>(SeedBases.Length);
        _output.WriteLine($"partidos por brazo y base: {matchesPerArm} · bases: {SeedBases.Length} · total {2 * matchesPerArm * SeedBases.Length}");
        _output.WriteLine("base | sin equipar | equipada | aporta");
        for (int b = 0; b < SeedBases.Length; b++)
        {
            int from = b * Rosters, to = from + Rosters;
            int bareWins = 0, equippedWins = 0;
            for (int i = from; i < to; i++)
            {
                bareWins += bare[i];
                equippedWins += equipped[i];
            }

            double bareRate = 100.0 * bareWins / matchesPerArm;
            double equippedRate = 100.0 * equippedWins / matchesPerArm;
            bareRates.Add(bareRate);
            deltas.Add(equippedRate - bareRate);
            _output.WriteLine($"{SeedBases[b],4} | {bareRate,10:F2}% | {equippedRate,7:F2}% | {equippedRate - bareRate,+6:F2}");
        }

        double mean = deltas.Average();
        double sd = Math.Sqrt(deltas.Sum(d => (d - mean) * (d - mean)) / (deltas.Count - 1));
        double standardError = sd / Math.Sqrt(deltas.Count);
        _output.WriteLine("");
        _output.WriteLine($"media {mean:F2} · sd entre bases {sd:F2} · error típico de la media {standardError:F2}");
        _output.WriteLine($"IC95 % aproximado: [{mean - (2 * standardError):F2}, {mean + (2 * standardError):F2}]");
        _output.WriteLine($"mínimo {deltas.Min():F2} (base {SeedBases[deltas.IndexOf(deltas.Min())]}) · máximo {deltas.Max():F2}");

        // El espejo tiene que estar donde debe: si el brazo sin equipar no ronda el 50%, el punto de
        // comparación está sesgado y la diferencia no significaría nada.
        Assert.InRange(bareRates.Average(), 42.0, 58.0);

        // ADR 0033: "muy buena" = "buena, además equipada". Si equipar no da un escalón claro, ese nivel
        // de la escala no existe y la curva de puertas no se puede cumplir.
        //
        // UMBRAL 1,5 (ADR 0118), aplicado a la MEDIA de ocho bases. Sustituye al 1,0 de la ADR 0116, que
        // se fijó cuando la puerta medía con UNA sola semilla y su error típico era ~0,9.
        //
        // Con la muestra nueva el error típico de la media es 0,40 y el valor medido 2,54. Con eso, 1,0
        // ya no daba falsos positivos (0,01 %) pero solo avisaba el 25 % de las veces si el efecto se
        // halvara; 1,5 sube esa detección al 72 % manteniendo el falso positivo en 0,47 %. No se eligió
        // 2,0 —que detectaría el 97 %— porque su falso positivo es 8,85 %, del mismo orden que el ~6 %
        // que ya costó tres investigaciones (BA-M, BA-N, BB-T).
        //
        // Derivación completa y tabla en docs/decisiones/0118-*.md; historial en BA-N.md y BB-T.md.
        Assert.True(
            mean >= 1.5,
            $"equipar a los siete titulares solo aporta {mean:F1} puntos de tasa de victoria de media sobre "
                + $"{SeedBases.Length} bases de semilla: con eso el escalón 'muy buena' de la ADR 0033 no tiene "
                + "contenido y los objetos están mal calibrados");
    }

    private static int WinsOfRoster(Catalog catalog, ulong rosterSeed, bool equipped)
    {
        int wins = 0;
        var challenger = Build(catalog, rosterSeed, "challenger", firstId: 0, equipped);
        var reference = Build(catalog, rosterSeed + 500UL, "reference", firstId: 100, equipped: false);

        for (int m = 0; m < MatchesPerRoster; m++)
        {
            ulong seed = (rosterSeed * 1000UL) + (ulong)m;
            var referee = new RefereeSetup("Neutral", RefereeTrait.Neutral, 0);
            var config = new SimConfig(CollectLog: false);

            // Ida: el retador juega en casa. Vuelta: el mismo emparejamiento con los campos cambiados,
            // que es lo que hace --home-away.
            if (Underleague.Sim.Engine.Simulator.Run(new MatchSetup(challenger, reference, referee), seed, catalog, config).Report.Winner == 0)
            {
                wins++;
            }

            if (Underleague.Sim.Engine.Simulator.Run(new MatchSetup(reference, challenger, referee), seed, catalog, config).Report.Winner == 1)
            {
                wins++;
            }
        }

        return wins;
    }

    /// <summary>Plantilla humana de calidad 50 con sus perks iniciales y, si toca, un objeto por titular.</summary>
    private static TeamSetup Build(Catalog catalog, ulong seed, string id, int firstId, bool equipped)
    {
        var rng = RngStreams.Generation(seed, firstId);
        var team = TeamGenerator.Generate(ref rng, catalog, id, Race.Human, 50, firstId);
        var players = new List<PlayerDefinition>(PerkAssignment.AssignInitial(ref rng, team.Players, catalog));

        if (equipped)
        {
            for (int slot = 0; slot < Loadout.Length && slot < players.Count; slot++)
            {
                players[slot] = players[slot] with { Item = RunEquipment.ToMatchItem(Items.Get(Loadout[slot])) };
            }
        }

        return team with { Players = players, Lineup = Lineup.Default(players.Take(7).ToList()) };
    }
}
