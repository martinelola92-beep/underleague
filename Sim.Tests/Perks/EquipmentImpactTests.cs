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
    /// en el paquete Z: con 8 el brazo de cada arm eran 512 partidos, la diferencia entre arms tenía una
    /// desviación de ~3 puntos y el umbral de 5 quedaba dentro del ruido —añadir ocho perks al catálogo
    /// (RF-070) movió la medida de 5,4 a 4,7 sin tocar un solo objeto—. Con 24 la desviación baja a
    /// ~1,8 y el test avisa de una regresión de verdad.
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

    [Fact]
    public void EquippingAGoodBuildIsWorthSeveralPointsOfWinRate()
    {
        int bare = WinsOf(equipped: false);
        int equipped = WinsOf(equipped: true);
        int matches = Rosters * MatchesPerRoster * 2;

        double bareRate = 100.0 * bare / matches;
        double equippedRate = 100.0 * equipped / matches;
        _output.WriteLine($"partidos por brazo: {matches}");
        _output.WriteLine($"sin equipar:  {bare}/{matches} = {bareRate:F1}%");
        _output.WriteLine($"equipada:     {equipped}/{matches} = {equippedRate:F1}%");
        _output.WriteLine($"lo que aporta equipar: {equippedRate - bareRate:+0.0;-0.0} puntos");

        // El espejo tiene que estar donde debe: si el brazo sin equipar no ronda el 50%, el punto de
        // comparación está sesgado y la diferencia no significaría nada.
        Assert.InRange(bareRate, 42.0, 58.0);

        // ADR 0033: "muy buena" = "buena, además equipada". Si equipar no da un escalón claro, ese nivel
        // de la escala no existe y la curva de puertas no se puede cumplir.
        //
        // El umbral sale de la medida, no de la aritmética: la tabla de valor marginal de la ADR 0038
        // predice 5,8 puntos para este juego de siete objetos, y medido dan 3,3. La tabla se midió con
        // +20 repartidos entre los DIEZ jugadores y aquí el bono va entero a UNO, así que sobrestima por
        // un factor de 1,6; queda anotado, porque el precio de los objetos se calcula con ella.
        //
        // Umbral 1,0 desde la ADR 0116 (BA-N), CORREGIDA por el independent-reviewer: el motivo original
        // ("con 94 perks lo medido cayó a 1,7, el catálogo diluye la aportación marginal") quedó REJECTED
        // -no solo sin aislar-. Congelando el catálogo de perks (mismo /data en cinco commits) esta misma
        // puerta dio 1,7 / 2,1 / 1,7 / 3,0 / 3,4: el número se mueve solo porque cada cambio en /Sim
        // (ninguno tocaba objetos ni perks) resortea los 6.144 partidos del brazo. Es el mismo mecanismo
        // que ya documenta la ADR 0115 ("desplaza el consumo de RNG lo suficiente para mover números de
        // builds concretos"), no un efecto del tamaño del catálogo.
        //
        // El motivo real de 1,0, y el que sí sostiene el umbral: el error típico de esta medición es
        // ~0,9 puntos (arriba, 6.144 partidos/brazo) y el valor verdadero, estimado por esos cinco
        // puntos, ronda 2,4 -esa media es de solo cinco medidas, con su propio error típico de ~0,35, así
        // que los porcentajes de abajo son órdenes de magnitud, no una calibración a la décima-. Con esa
        // varianza, un umbral de 2,0 tenía del orden de 34 % de probabilidad de salir rojo por puro
        // muestreo en cualquier commit que no tocara ni objetos ni perks -exactamente lo que produjo BA-M
        // y BA-N-; 1,0 baja ese falso positivo a del orden de 6 %. Detección real si el efecto de
        // verdad se degradara: del orden de 87 % de aviso si equipar dejara de aportar nada, del orden de
        // 41 % si aportara la mitad de lo normal -esta puerta protege "equipar hace algo", no el escalón
        // fino de la ADR 0033-.
        // Ver ADR 0116 para la derivación completa y `docs/pendientes/BA-N.md` para el historial.
        Assert.True(
            equippedRate - bareRate >= 1.0,
            $"equipar a los siete titulares solo aporta {equippedRate - bareRate:F1} puntos de tasa de victoria: "
                + "con eso el escalón 'muy buena' de la ADR 0033 no tiene contenido y los objetos están mal calibrados");
    }

    private static int WinsOf(bool equipped)
    {
        int wins = 0;
        for (int roster = 0; roster < Rosters; roster++)
        {
            ulong rosterSeed = 1000UL + (ulong)roster;
            var challenger = Build(rosterSeed, "challenger", firstId: 0, equipped);
            var reference = Build(rosterSeed + 500UL, "reference", firstId: 100, equipped: false);

            for (int m = 0; m < MatchesPerRoster; m++)
            {
                ulong seed = (rosterSeed * 1000UL) + (ulong)m;
                var referee = new RefereeSetup("Neutral", RefereeTrait.Neutral, 0);
                var config = new SimConfig(CollectLog: false);

                // Ida: el retador juega en casa. Vuelta: el mismo emparejamiento con los campos
                // cambiados, que es lo que hace --home-away.
                if (Underleague.Sim.Engine.Simulator.Run(new MatchSetup(challenger, reference, referee), seed, Catalog, config).Report.Winner == 0)
                {
                    wins++;
                }

                if (Underleague.Sim.Engine.Simulator.Run(new MatchSetup(reference, challenger, referee), seed, Catalog, config).Report.Winner == 1)
                {
                    wins++;
                }
            }
        }

        return wins;
    }

    /// <summary>Plantilla humana de calidad 50 con sus perks iniciales y, si toca, un objeto por titular.</summary>
    private static TeamSetup Build(ulong seed, string id, int firstId, bool equipped)
    {
        var rng = RngStreams.Generation(seed, firstId);
        var team = TeamGenerator.Generate(ref rng, Catalog, id, Race.Human, 50, firstId);
        var players = new List<PlayerDefinition>(PerkAssignment.AssignInitial(ref rng, team.Players, Catalog));

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
