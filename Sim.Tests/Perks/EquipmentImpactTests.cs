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
public sealed class EquipmentImpactTests
{
    /// <summary>
    /// Plantillas distintas sobre las que se promedia (equivalente a <c>--rosters</c>). Subido de 8 a 24
    /// en el paquete Z: con 8 el brazo de cada arm eran 512 partidos, la diferencia entre arms tenía una
    /// desviación de ~3 puntos y el umbral de 5 quedaba dentro del ruido —añadir ocho perks al catálogo
    /// (RF-070) movió la medida de 5,4 a 4,7 sin tocar un solo objeto—. Con 24 la desviación baja a
    /// ~1,8 y el test avisa de una regresión de verdad.
    /// </summary>
    private const int Rosters = 24;

    /// <summary>Partidos por plantilla y dirección; con ida y vuelta salen 2x (equivalente a <c>--home-away</c>).</summary>
    private const int MatchesPerRoster = 32;

    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly ItemCatalog Items = ItemLoader.FromJson(TestData.LoadAllFiles());

    /// <summary>
    /// Equipamiento de una build "muy buena": un objeto por titular (RF-076), elegido con criterio para
    /// su puesto —fuerza atrás, técnica y velocidad en el medio, un maldito potente arriba— y con los
    /// tres arquetipos representados sin repetir objeto.
    /// </summary>
    private static readonly string[] Loadout =
    {
        "focus_lens",        // 0 GK
        "iron_gauntlets",    // 1 DEF
        "veteran_armband",   // 2 DEF
        "worn_boots",        // 3 MID
        "endurance_belt",    // 4 MID
        "loose_leash_charm", // 5 MID
        "berserker_totem",   // 6 FWD (maldito)
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
        // de la escala no existe y la curva de puertas no se puede cumplir. El umbral es deliberadamente
        // bajo (la mitad de lo que se mide hoy) para que el test avise de una regresión de verdad y no
        // del ruido de un reajuste.
        Assert.True(
            equippedRate - bareRate >= 5.0,
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
