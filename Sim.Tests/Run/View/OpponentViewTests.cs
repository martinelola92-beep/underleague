using Underleague.Sim.Data;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.View;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run.View;

/// <summary>
/// BH-B (<c>docs/pendientes/BH-B.md</c>): el nodo de jefe guarda un <c>OpponentId</c> fantasma (BE-F) que resuelve
/// a un clan de la liga, y el mapa y el ojeo lo pintaban con el nombre y la descripción de ese clan. La ficha del
/// rival se decide por el tipo de nodo: el jefe se presenta como el jefe, con el nombre de <c>data/bosses/</c>.
/// </summary>
public sealed class OpponentViewTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    private static StandardRunSystems Standard => SystemsTestSupport.Systems;

    private static BossCatalog Bosses { get; } = BossCatalog.FromJson(TestData.LoadAllFiles());

    private static RunState NewRun(ulong seed)
    {
        var systems = new BossRunSystems(Bosses, Standard);
        return systems.AssignBosses(RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, systems));
    }

    /// <summary>
    /// Las tres semillas y los tres actos de la medición de la ficha: en todas, el nodo de jefe se presenta con el
    /// nombre del jefe de su acto, y ese nombre no es el de ningún clan de la liga.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(12345UL)]
    [InlineData(99991UL)]
    public void TheBossNodeIsPresentedAsTheBossOfItsActAndNeverAsALeagueClan(ulong seed)
    {
        var state = NewRun(seed);
        var clanNames = Standard.Rivals.All.Select(t => t.Name.Es).ToHashSet(StringComparer.Ordinal);

        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var map = state.MapOf(act);
            var node = map.Get(map.BossNodeId);
            var boss = Bosses.ForAct(act);

            var card = OpponentView.For(node, Bosses, Standard.Rivals, "es");

            Assert.NotNull(card);
            Assert.True(card!.IsBoss);
            Assert.Equal(boss.Id, card.Id);
            Assert.Equal(boss.Name["es"], card.Name);
            Assert.DoesNotContain(card.Name, clanNames);

            // Un jefe no tiene descripción de ojeo en los datos: la pantalla no puede heredar la de un clan.
            Assert.Equal(string.Empty, card.Description);
        }
    }

    [Fact]
    public void TheBossNameFollowsTheLanguage()
    {
        var state = NewRun(1);
        var map = state.MapOf(1);
        var node = map.Get(map.BossNodeId);

        var card = OpponentView.For(node, Bosses, Standard.Rivals, "en");

        Assert.Equal(Bosses.ForAct(1).Name["en"], card!.Name);
    }

    /// <summary>La cara contraria: un partido de liga o de élite sigue presentándose con su clan, nombre y descripción.</summary>
    [Fact]
    public void ALeagueNodeIsStillPresentedAsItsClan()
    {
        var state = NewRun(1);
        int checkedNodes = 0;
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            foreach (var node in state.MapOf(act).Nodes.Where(n => NodeKinds.IsCatalogRivalMatch(n.Kind) && n.OpponentId.Length > 0))
            {
                var team = Standard.Rivals.Find(node.OpponentId)!;

                var card = OpponentView.For(node, Bosses, Standard.Rivals, "es");

                Assert.NotNull(card);
                Assert.False(card!.IsBoss);
                Assert.Equal(team.Id, card.Id);
                Assert.Equal(team.Name.Es, card.Name);
                Assert.Equal(team.Description.Es, card.Description);
                checkedNodes++;
            }
        }

        Assert.True(checkedNodes > 0, "el mapa de la prueba no tiene ningún partido de catálogo que comprobar");
    }

    /// <summary>Lo que no se juega no tiene rival: mercado, clínica, evento y demás devuelven null, aunque guardaran un id.</summary>
    [Fact]
    public void ANodeThatIsNotPlayedHasNoOpponent()
    {
        var state = NewRun(1);
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            foreach (var node in state.MapOf(act).Nodes.Where(n => !NodeKinds.IsMatch(n.Kind)))
            {
                Assert.Null(OpponentView.For(node, Bosses, Standard.Rivals, "es"));
            }
        }
    }

    /// <summary>
    /// El mismo defecto un paso más allá del mapa y el ojeo: el equipo del jefe se llamaba como su <b>id de datos</b>
    /// («the_hunt»), y ese es el nombre que el marcador, el pregón y el informe enseñan (<c>MatchPlayback.RivalName</c>).
    /// El id del equipo no cambia; sí su nombre visible.
    /// </summary>
    [Fact]
    public void TheBossTeamCarriesTheBossNameNotItsDataId()
    {
        var systems = new BossRunSystems(Bosses, Standard);
        var state = NewRun(12345);
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var map = state.MapOf(act);
            var node = map.Get(map.BossNodeId);
            var boss = Bosses.ForAct(act);

            var team = systems.OpponentFor(state, node, Catalog);

            Assert.Equal(boss.Id, team.Id);
            Assert.Equal(boss.Name["es"], team.Name);
            Assert.NotEqual(boss.Id, team.Name);
        }
    }
}
