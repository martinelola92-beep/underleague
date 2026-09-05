using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Rewards;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// Los arcos de build vistos desde el <b>pool</b> (ADR 0051): qué entra en la recompensa y en el mercado
/// según el acto, qué exige un maestro para poder cobrarse y qué desaparece del catálogo en cuanto se
/// acepta. Las reglas viven en <c>/Sim</c>, no en la pantalla: aquí se comprueba que el motor las hace
/// cumplir aunque nadie mire.
/// </summary>
public sealed class PerkArcTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    private static RunState Start(Race race = Race.Human) =>
        RunEngine.Start(SystemsTestSupport.Setup(race), 4242UL, Catalog, SystemsTestSupport.Systems);

    /// <summary>Da a los titulares los perks indicados, respetando los slots por rareza (RF-023).</summary>
    private static RunState With(RunState state, params string[] perkIds)
    {
        int index = 0;
        foreach (string perkId in perkIds)
        {
            var perk = Catalog.Perks.Get(perkId);
            var carriers = PerkPool.EligibleCarriers(state, perk, Catalog);
            Assert.True(carriers.Count > 0, $"nadie puede llevar '{perkId}' en esta plantilla");
            state = state.WithPlayer(PerkPool.WithPerk(state.GetPlayer(carriers[index % carriers.Count]), perkId));
            index++;
        }

        return state;
    }

    /// <summary>Miembros de una línea que no son maestros, por id ascendente.</summary>
    private static IReadOnlyList<PerkDefinition> Pieces(string family) =>
        Catalog.Perks.MembersOf(family).Where(p => !p.IsMaster && p.Race is null).ToList();

    // ------------------------------------------------------------------ profundidad nativa

    /// <summary>ADR 0051: un maestro no sale en el acto 1. Ni siquiera fuera de profundidad.</summary>
    [Fact]
    public void NoMasterIsOfferedInActOne()
    {
        var state = Start();
        var master = Catalog.Perks.Masters[0];
        var line = Pieces(master.Requires!.Family);
        state = With(state, line.Take(master.Requires.Count).Select(p => p.Id).ToArray());

        Assert.DoesNotContain(PerkPool.Offerable(state, Catalog, act: 1), p => p.IsMaster);
        Assert.Contains(PerkPool.Offerable(state, Catalog, act: 2), p => p.Id == master.Id);
    }

    /// <summary>
    /// El pool <b>mejora con la run</b>: lo que nace en el acto 2 no está en el 1 salvo como sorpresa
    /// fuera de profundidad, y su peso relativo sube cuando llega su acto.
    /// </summary>
    [Fact]
    public void DepthMovesWeightFromFillerToDeepPerks()
    {
        var deep = Catalog.Perks.All.First(p => p.MinAct == 3 && !p.IsMaster);
        var filler = Catalog.Perks.All.First(p => p.MinAct == 1);

        int deepAtOne = PerkPool.DepthWeightPercent(deep, Catalog, act: 1);
        int deepAtThree = PerkPool.DepthWeightPercent(deep, Catalog, act: 3);
        int fillerAtOne = PerkPool.DepthWeightPercent(filler, Catalog, act: 1);
        int fillerAtThree = PerkPool.DepthWeightPercent(filler, Catalog, act: 3);

        Assert.True(deepAtOne > 0, "fuera de profundidad tiene que ser posible, no imposible (ADR 0051)");
        Assert.True(deepAtOne < deepAtThree / 4, "fuera de profundidad tiene que ser raro");
        Assert.Equal(Catalog.Perks.Arcs.Depth.FullPercent, fillerAtOne);
        Assert.True(fillerAtThree < fillerAtOne, "el relleno tiene que salir sobre todo pronto");
    }

    // ------------------------------------------------------------------ lo que un maestro exige

    [Fact]
    public void AMasterCannotBeTakenBeforeItsLineIsBuilt()
    {
        var state = Start();
        var master = Catalog.Perks.Masters[0];

        Assert.Equal(PerkAvailability.Unmet, PerkPool.Availability(state, master, Catalog));

        var line = Pieces(master.Requires!.Family).Take(master.Requires.Count).Select(p => p.Id).ToArray();
        state = With(state, line);
        Assert.Equal(PerkAvailability.Available, PerkPool.Availability(state, master, Catalog));
    }

    /// <summary>
    /// Un maestro entra en el pool cuando le falta <b>como mucho una pieza</b>: se ve venir, y esa es la
    /// pieza que el mercado vende. Con dos o más piezas de menos, no aparece.
    /// </summary>
    [Fact]
    public void AMasterAppearsWhenItIsOnePieceAway()
    {
        var master = Catalog.Perks.Masters.First(m => m.Requires!.Count >= 2);
        var line = Pieces(master.Requires!.Family).Select(p => p.Id).ToList();

        var far = Start();
        var near = With(Start(), line.Take(master.Requires.Count - 1).ToArray());

        Assert.DoesNotContain(PerkPool.Offerable(far, Catalog, 2), p => p.Id == master.Id);
        Assert.Contains(PerkPool.Offerable(near, Catalog, 2), p => p.Id == master.Id);
    }

    // ------------------------------------------------------------------ lo que un maestro cierra

    /// <summary>
    /// El bloqueo es permanente y mira hacia adelante (ADR 0051): la línea cerrada desaparece del pool y
    /// lo que ya se llevaba de ella <b>sigue puesto</b>, porque un perk no se puede retirar (RF-072).
    /// </summary>
    [Fact]
    public void TakingAMasterClosesItsLineForTheRestOfTheRun()
    {
        var master = Catalog.Perks.Masters.First(m => m.Blocks.Families.Count > 0);
        string closedFamily = master.Blocks.Families[0];
        string keepsWorking = Pieces(closedFamily)[0].Id;

        var state = With(Start(), keepsWorking);
        state = With(state, Pieces(master.Requires!.Family).Take(master.Requires.Count).Select(p => p.Id).ToArray());
        Assert.Contains(PerkPool.Offerable(state, Catalog, 2), p => p.Family == closedFamily);

        state = With(state, master.Id);

        var closed = PerkPool.ClosedBy(state, Catalog);
        Assert.Contains(closedFamily, closed.Families);
        Assert.DoesNotContain(PerkPool.Offerable(state, Catalog, 3), p => p.Family == closedFamily);
        Assert.Contains(PerkPool.HeldPerkIds(state), id => id == keepsWorking);
        Assert.Equal(
            PerkAvailability.Closed,
            PerkPool.Availability(state, Catalog.Perks.MembersOf(closedFamily).First(p => p.Id != keepsWorking), Catalog));
    }

    /// <summary>
    /// Los dos maestros de un par se excluyen: quien cierra la línea del otro se cierra también su
    /// maestro, que es lo que hace que dos builds de la misma raza no puedan converger.
    /// </summary>
    [Fact]
    public void TwoOpposedMastersCannotCoexist()
    {
        var first = Catalog.Perks.Masters.First(m => m.Blocks.Families.Count > 0);
        var rival = Catalog.Perks.Masters.FirstOrDefault(m => first.Blocks.Families.Contains(m.Family));
        Assert.NotNull(rival);

        var state = With(Start(), Pieces(first.Requires!.Family).Take(first.Requires.Count).Select(p => p.Id).ToArray());
        state = With(state, first.Id);

        Assert.Equal(PerkAvailability.Closed, PerkPool.Availability(state, rival!, Catalog));
    }

    /// <summary>
    /// La regla la hace cumplir <c>/Sim</c>, no la pantalla: cobrar un maestro sin su línea es un error
    /// explícito, tanto en la recompensa (RF-071) como en el mercado (RF-114e).
    /// </summary>
    [Fact]
    public void TakingAnUnmetMasterIsRejectedByTheEngine()
    {
        var state = Start();
        var master = Catalog.Perks.Masters[0];
        var carriers = PerkPool.EligibleCarriers(state, master, Catalog);
        Assert.NotEmpty(carriers);

        // PerkPool.Require es el guardián que llaman RewardSystem.ApplyPerk y MarketSystem.BuyPerk: las
        // dos vías de conseguir un perk pasan por aquí, así que probarlo una vez las prueba las dos.
        var error = Assert.Throws<InvalidOperationException>(() => PerkPool.Require(state, master, Catalog));
        Assert.Contains("maestro", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// ADR 0055: un maestro <b>solo se compra</b>. No sale nunca como recompensa por ganar, ni siquiera
    /// con la línea entera construida, y cobrarlo por esa vía es un error explícito. Es la palanca que
    /// hace del mercado parte del núcleo de la build: sin pasar por uno, el objetivo de la línea no
    /// existe.
    /// </summary>
    [Fact]
    public void AMasterIsSoldInTheMarketAndNeverGivenAsAReward()
    {
        var master = Catalog.Perks.Masters[0];
        var state = With(Start(), Pieces(master.Requires!.Family).Take(master.Requires.Count).Select(p => p.Id).ToArray());

        Assert.Contains(PerkPool.Offerable(state, Catalog, 3, PerkSource.Market), p => p.Id == master.Id);
        Assert.DoesNotContain(PerkPool.Offerable(state, Catalog, 3, PerkSource.Reward), p => p.IsMaster);

        Assert.Equal(PerkAvailability.Available, PerkPool.Availability(state, master, Catalog, PerkSource.Market));
        Assert.Equal(PerkAvailability.MarketOnly, PerkPool.Availability(state, master, Catalog, PerkSource.Reward));

        var error = Assert.Throws<InvalidOperationException>(
            () => PerkPool.Require(state, master, Catalog, PerkSource.Reward));
        Assert.Contains("mercado", error.Message, StringComparison.Ordinal);

        // Y por la vía del mercado, con la línea construida, se puede.
        PerkPool.Require(state, master, Catalog, PerkSource.Market);
    }

    /// <summary>Los perks iniciales de una plantilla nunca incluyen un maestro (ADR 0051).</summary>
    [Fact]
    public void NoRosterStartsWithAMaster()
    {
        foreach (var race in new[] { Race.Human, Race.Orc, Race.Elf, Race.Dwarf, Race.Undead })
        {
            var state = Start(race);
            foreach (string id in PerkPool.HeldPerkIds(state))
            {
                Assert.False(Catalog.Perks.Get(id).IsMaster, $"la plantilla inicial de {race} entra con '{id}'");
            }
        }
    }
}
