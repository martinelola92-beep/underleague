using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Run.Systems.Rewards;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// BA-G (<c>docs/pendientes/BA-G.md</c>, ADR 0169): un nombre completo no se repite dentro de una run. El sorteo de
/// <see cref="NameGenerator"/> no tiene memoria y una raza admite entre 300 y 920 nombres, así que en un centenar de
/// jugadores por run repetirse era lo normal. Lo que se prueba es la regla en cada sitio que genera un jugador, y que
/// cumplirla <b>no toca los dados de nadie</b>: sólo cambia el nombre de quien repetía.
/// </summary>
public sealed class RunNamesTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    private static MarketOffers Offers(RunState state, MapNode node) =>
        MarketOfferGenerator.Generate(state, node, Catalog, Systems.Economy, Systems.Items, Systems.Consumables);

    private static IReadOnlyList<RunPlayer> PlayersOf(MarketOffers offers) =>
        offers.Recruits.Select(o => o.Player)
            .Concat(offers.Youths.Select(o => o.Player))
            .Concat(offers.Mercenaries.Select(o => o.Player))
            .ToList();

    // ------------------------------------------------------------------ la causa (instrumento de control, Regla J)

    /// <summary>
    /// <b>CONFIRMED</b>: el sorteo sin memoria se repite con la escala de una run. Cien nombres de una raza de 300
    /// posibles: la probabilidad de que no haya ninguna pareja es e^-16, así que en todas las semillas hay
    /// repetidos. Es el control del resto de la clase: sin la regla, los tests de abajo no podrían pasar.
    /// </summary>
    [Fact]
    public void TheRawDrawRepeatsAtTheScaleOfARun()
    {
        var generator = new NameGenerator(Catalog.Race(Race.Dwarf));
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var rng = RngStreams.Generation(seed, 0);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int repeats = 0;
            for (int i = 0; i < 100; i++)
            {
                repeats += seen.Add(generator.Next(ref rng).Es) ? 0 : 1;
            }

            Assert.True(repeats > 0, $"semilla {seed}: 100 nombres de 300 posibles sin ninguna pareja repetida");
        }
    }

    // ------------------------------------------------------------------ el retículo y los billetes

    [Fact]
    public void EveryTicketBelowTheLatticeSizeIsADifferentNameForEveryRaceAndSeed()
    {
        foreach (var race in Catalog.Races)
        {
            var lattice = new NameLattice(race);
            foreach (ulong seed in new[] { 1UL, 7UL, 123456UL })
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                for (int ticket = 0; ticket < lattice.Count; ticket++)
                {
                    names.Add(lattice.NameOfTicket(seed, ticket));
                }

                Assert.Equal(lattice.Count, names.Count);
            }
        }
    }

    [Fact]
    public void TheReservedSetHasOneNamePerTicketAndIsTheSameAsTheTicketsThemselves()
    {
        var lattice = new NameLattice(Catalog.Race(Race.Orc));
        var reserved = lattice.NamesOfTickets(99UL, RivalRoster.SigningPoolTickets);

        Assert.Equal(RivalRoster.SigningPoolTickets, reserved.Count);
        for (int ticket = 0; ticket < RivalRoster.SigningPoolTickets; ticket++)
        {
            Assert.Contains(lattice.NameOfTicket(99UL, ticket), reserved);
        }
    }

    // ------------------------------------------------------------------ el surtido del mercado

    /// <summary>
    /// Cien mercados de cinco semillas y las cinco razas: ningún jugador del surtido se llama como otro del mismo
    /// surtido ni como alguien de la plantilla. Con el sorteo sin memoria, la probabilidad de que ocurra en cada uno
    /// pasa del 3 % por oferta (9 nombres de la plantilla entre 300).
    /// </summary>
    [Fact]
    public void NoPlayerOfAMarketRepeatsAnotherOneOfItOrTheRoster()
    {
        foreach (var race in new[] { Race.Human, Race.Elf, Race.Dwarf, Race.Orc, Race.Undead })
        {
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var state = RunEngine.Start(SystemsTestSupport.Setup(clubRace: race), seed, Catalog, Systems);
                var rosterNames = state.Roster.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                for (int id = 1000; id < 1100; id++)
                {
                    var node = new MapNode(id, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);
                    var names = PlayersOf(Offers(state, node)).Select(p => p.Name).ToList();

                    Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
                    Assert.DoesNotContain(names, rosterNames.Contains);
                }
            }
        }
    }

    /// <summary>
    /// Se fuerza el choque: alguien de la plantilla pasa a llamarse como un fichaje del surtido. La oferta cambia de
    /// nombre y de <b>nada más</b>: atributos, rareza, perks y precio salen idénticos, porque el nombre nuevo sale de
    /// un flujo propio y no gasta ni un dado del mercado (RT-022). Es lo que evita mover el balance.
    /// </summary>
    [Fact]
    public void AForcedCollisionChangesTheNameOfTheOfferAndNothingElse()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 424242UL, Catalog, Systems);
        var node = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);
        var free = Offers(state, node);
        string taken = free.Recruits[0].Player.Name;

        var withCollision = state.WithPlayer(state.Roster[3] with { Name = taken });
        var moved = Offers(withCollision, node);

        Assert.NotEqual(taken, moved.Recruits[0].Player.Name);
        Assert.Equal(free.Recruits[0].Price, moved.Recruits[0].Price);
        TestRuns.AssertSamePlayer(free.Recruits[0].Player, moved.Recruits[0].Player with { Name = taken });

        // Y el resto del surtido, en su orden, no se entera.
        Assert.Equal(free.Recruits.Skip(1).Select(o => o.Player.Name), moved.Recruits.Skip(1).Select(o => o.Player.Name));
        Assert.Equal(free.Youths.Select(o => o.Player.Name), moved.Youths.Select(o => o.Player.Name));
        Assert.Equal(free.Perks.Select(o => o.PerkId), moved.Perks.Select(o => o.PerkId));
        Assert.Equal(free.Items.Select(o => o.ItemId), moved.Items.Select(o => o.ItemId));
    }

    /// <summary>
    /// El surtido se deriva otra vez en cada consulta, también después de comprar. Comprar un canterano no puede
    /// cambiar el nombre de ninguna oferta —ni el de la comprada—: el registro de la elección lo evita.
    /// </summary>
    [Fact]
    public void TheMarketIsTheSameAfterBuyingFromIt()
    {
        var state = SystemsTestSupport.WithFakePendingNode(
            RunEngine.Start(SystemsTestSupport.Setup(startingGold: 600), 31337UL, Catalog, Systems), NodeKind.Market);
        var node = state.GetNode(state.PendingNodeId);
        var before = Offers(state, node);
        var youth = before.Youths[0].Player;

        var bought = RunEngine.Apply(state, new BuyOffer(MarketCategories.Youth, 0), Catalog, Systems);
        var after = Offers(bought, node);

        Assert.Contains(bought.Roster, p => p.Name == youth.Name && p.IsYouth);
        Assert.Equal(PlayersOf(before).Select(p => p.Name), PlayersOf(after).Select(p => p.Name));
    }

    /// <summary>
    /// Comprar dos veces la misma oferta (el mercado no la retira) no mete a dos jugadores con el mismo nombre: el
    /// sumidero estricto renombra al segundo.
    /// </summary>
    [Fact]
    public void BuyingTheSameOfferTwiceNeverDuplicatesAName()
    {
        var state = SystemsTestSupport.WithFakePendingNode(
            RunEngine.Start(SystemsTestSupport.Setup(startingGold: 600), 31337UL, Catalog, Systems), NodeKind.Market);
        var first = RunEngine.Apply(state, new BuyOffer(MarketCategories.Youth, 0), Catalog, Systems);
        var room = first.WithoutPlayer(first.Roster.First(p => !p.IsYouth && p.Position == Position.Defender).Id);

        var second = RunEngine.Apply(room, new BuyOffer(MarketCategories.Youth, 0), Catalog, Systems);

        var names = second.Roster.Select(p => p.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, second.Roster.Count(p => p.IsYouth));
    }

    /// <summary>
    /// El registro vive en <c>Counters</c> (W-11) para no subir el esquema del guardado: sobrevive a guardar y cargar, y
    /// el surtido que se deriva del estado cargado es el mismo, con la compra hecha o no.
    /// </summary>
    [Fact]
    public void TheRegistrySurvivesASaveAndLoad()
    {
        var state = SystemsTestSupport.WithFakePendingNode(
            RunEngine.Start(SystemsTestSupport.Setup(startingGold: 600), 6060UL, Catalog, Systems), NodeKind.Market);
        var node = state.GetNode(state.PendingNodeId);
        var bought = RunEngine.Apply(state, new BuyOffer(MarketCategories.Youth, 0), Catalog, Systems);

        var reloaded = Underleague.Sim.Run.Save.RunSave.Load(Underleague.Sim.Run.Save.RunSave.Save(bought));

        Assert.Equal(
            bought.Counters.Where(c => c.Key.StartsWith(RunNames.OwnerPrefix, StringComparison.Ordinal)).OrderBy(c => c.Key, StringComparer.Ordinal),
            reloaded.Counters.Where(c => c.Key.StartsWith(RunNames.OwnerPrefix, StringComparison.Ordinal)).OrderBy(c => c.Key, StringComparer.Ordinal));
        Assert.Equal(PlayersOf(Offers(bought, node)).Select(p => p.Name), PlayersOf(Offers(reloaded, node)).Select(p => p.Name));
    }

    /// <summary>Un mercenario contratado tampoco repite a nadie: su raza es otra, su registro es el mismo.</summary>
    [Fact]
    public void AHiredMercenaryKeepsTheNameOfTheOfferAndIsRegistered()
    {
        var state = SystemsTestSupport.WithFakePendingNode(
            RunEngine.Start(SystemsTestSupport.Setup(startingGold: 600), 8080UL, Catalog, Systems), NodeKind.Market);
        var node = state.GetNode(state.PendingNodeId);
        var offer = Offers(state, node).Mercenaries[0].Player;

        var hired = RunEngine.Apply(state, new HireMercenary(0), Catalog, Systems);

        Assert.Contains(hired.Roster, p => p.IsMercenary && p.Name == offer.Name);
        Assert.True(hired.Counters.ContainsKey(RunNames.OwnerPrefix + offer.Name));
    }

    // ------------------------------------------------------------------ recompensas y eventos

    [Fact]
    public void ARewardPlayerNeverRepeatsTheRosterNorAnotherOptionAcrossManyNodes()
    {
        int players = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var state = SystemsTestSupport.WithFakePendingNode(
                RunEngine.Start(SystemsTestSupport.Setup(clubRace: Race.Dwarf), seed, Catalog, Systems), NodeKind.EliteMatch);
            var node = state.GetNode(state.PendingNodeId);
            var rosterNames = state.Roster.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

            var names = RewardSystem.Options(state, node, Catalog, Systems.Economy, Systems.Items)
                .OfType<PlayerRewardOption>().Select(o => o.Player.Name).ToList();

            players += names.Count;
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
            Assert.DoesNotContain(names, rosterNames.Contains);
        }

        Assert.True(players > 0, "instrumento: en 40 semillas tiene que salir algún jugador de recompensa");
    }

    /// <summary>
    /// El jefe da dos elecciones: el jugador que se cobra en la primera no puede reaparecer en la segunda con el mismo
    /// nombre. Por eso la elección forma parte de a quién se le anota el nombre.
    /// </summary>
    [Fact]
    public void ThePlayerTakenInTheFirstPickOfABossCannotComeBackInTheSecondOne()
    {
        int compared = 0;
        for (ulong seed = 1; seed <= 60 && compared == 0; seed++)
        {
            var state = SystemsTestSupport.WithFakePendingNode(
                RunEngine.Start(SystemsTestSupport.Setup(clubRace: Race.Orc), seed, Catalog, Systems), NodeKind.Boss);
            var node = state.GetNode(state.PendingNodeId);
            var options = RewardSystem.Options(state, node, Catalog, Systems.Economy, Systems.Items);
            int index = options.ToList().FindIndex(o => o is PlayerRewardOption);
            if (index < 0)
            {
                continue;
            }

            string taken = ((PlayerRewardOption)options[index]).Player.Name;
            var afterFirst = RunEngine.Apply(state, new ChooseReward(index), Catalog, Systems);
            var second = RewardSystem.Options(afterFirst, node, Catalog, Systems.Economy, Systems.Items)
                .OfType<PlayerRewardOption>().Select(o => o.Player.Name);

            Assert.DoesNotContain(taken, second);
            compared++;
        }

        Assert.True(compared > 0, "instrumento: en 60 semillas el primer reparto de un jefe tiene que ofrecer un jugador");
    }

    // ------------------------------------------------------------------ plantilla inicial y fichajes rivales

    [Fact]
    public void TheInitialRosterHasNoRepeatsAndNoNameReservedToTheRivalSignings()
    {
        foreach (var race in new[] { Race.Human, Race.Elf, Race.Dwarf, Race.Orc, Race.Undead })
        {
            var lattice = new NameLattice(Catalog.Race(race));
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var state = RunEngine.Start(SystemsTestSupport.Setup(clubRace: race), seed, Catalog, Systems);
                var reserved = lattice.NamesOfTickets(seed, RivalRoster.SigningPoolTickets);
                var names = state.Roster.Select(p => p.Name).ToList();

                Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
                Assert.DoesNotContain(names, reserved.Contains);
                Assert.All(names, n => Assert.True(state.Counters.ContainsKey(RunNames.OwnerPrefix + n), $"{n} sin registrar"));
            }
        }
    }

    /// <summary>
    /// Un fichaje rival no repite a nadie de los datos de su clan ni a otro fichaje del mismo clan, ni con seis
    /// generaciones por puesto: los billetes son una biyección y el nombre de un jugador de datos se salta.
    /// </summary>
    [Fact]
    public void RivalSigningsNeverRepeatEachOtherNorTheirClanData()
    {
        foreach (var team in Systems.Rivals.All)
        {
            var dataNames = team.Players.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            foreach (ulong seed in new[] { 1UL, 2UL, 20260929UL })
            {
                var names = new List<string>();
                for (int generation = 0; generation < 6; generation++)
                {
                    for (int slot = 0; slot < team.Players.Count; slot++)
                    {
                        names.Add(RivalRoster.SigningName(team, new RivalVacancy(team.ClanId, slot, generation), seed, Catalog));
                    }
                }

                Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
                Assert.DoesNotContain(names, dataNames.Contains);
            }
        }
    }

    /// <summary>
    /// El fichaje se sigue derivando sin mirar la plantilla ni el tiempo (ADR 0165): el mismo (semilla, clan, puesto,
    /// generación) es el mismo nombre en cada partido, se haya comprado lo que se haya comprado entre medias.
    /// </summary>
    [Fact]
    public void ASigningNameDoesNotDependOnAnythingButItsCoordinates()
    {
        var team = Systems.Rivals.All[0];
        var vacancy = new RivalVacancy(team.ClanId, 4, 1);

        string first = RivalRoster.SigningName(team, vacancy, 555UL, Catalog);
        _ = RunEngine.Start(SystemsTestSupport.Setup(), 555UL, Catalog, Systems);
        string second = RivalRoster.SigningName(team, vacancy, 555UL, Catalog);

        Assert.Equal(first, second);
        Assert.NotEqual(first, RivalRoster.SigningName(team, vacancy with { Slot = 5 }, 555UL, Catalog));
    }

    /// <summary>
    /// Ni el club ni un canterano se llaman como un fichaje que un clan rival pueda tener: los nombres reservados de
    /// cada raza no salen nunca en un mercado. Se comprueba con los de las cinco razas, no sólo con la del club: un
    /// mercenario es de otra.
    /// </summary>
    [Fact]
    public void NobodyOnTheClubSideCarriesANameReservedToARivalSigning()
    {
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, Systems);
            for (int id = 2000; id < 2060; id++)
            {
                var node = new MapNode(id, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);
                foreach (var player in PlayersOf(Offers(state, node)))
                {
                    var reserved = new NameLattice(Catalog.Race(player.Race)).NamesOfTickets(seed, RivalRoster.SigningPoolTickets);
                    Assert.DoesNotContain(player.Name, reserved);
                }
            }
        }
    }

    // ------------------------------------------------------------------ el mundo: clanes y jefes

    private static HashSet<string> ClanDataNames() =>
        Systems.Rivals.All.SelectMany(t => t.Players.Select(p => p.Name)).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Los diez jugadores de datos de cada clan son identidad (el némesis se reconoce por su nombre): quien cede es el club.
    /// Ni la plantilla inicial ni nada de lo que se le ofrece se llama como uno de ellos. Sin la reserva, medido con el mismo
    /// arnés en 80 plantillas finales, 22 llevaban un jugador con el nombre de uno de datos (revisión independiente).
    /// </summary>
    [Fact]
    public void NobodyOnTheClubSideCarriesTheNameOfAClanDataPlayer()
    {
        var clan = ClanDataNames();
        Assert.Equal(50, clan.Count);
        foreach (var race in new[] { Race.Human, Race.Elf, Race.Dwarf, Race.Orc, Race.Undead })
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var state = RunEngine.Start(SystemsTestSupport.Setup(clubRace: race), seed, Catalog, Systems);
                Assert.DoesNotContain(state.Roster.Select(p => p.Name), clan.Contains);
                for (int id = 3000; id < 3030; id++)
                {
                    var node = new MapNode(id, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);
                    Assert.DoesNotContain(PlayersOf(Offers(state, node)).Select(p => p.Name), clan.Contains);
                }
            }
        }
    }

    /// <summary>
    /// El equipo de un jefe se genera sin saber quién hay en el club y cede: ningún jugador del jefe se llama como uno de la
    /// plantilla. Con el sorteo sin memoria, un enano contra el jefe enano coincidía en 56 de 200 plantillas iniciales
    /// (revisión independiente). Y cede <b>sólo el nombre</b>: id, atributos, perks y alineación salen como los del jefe.
    /// </summary>
    [Fact]
    public void TheBossTeamNeverRepeatsANameOfTheClubAndOnlyItsNamesChange()
    {
        var bosses = BossCatalog.FromJson(TestData.LoadAllFiles());
        var systems = new BossRunSystems(bosses, Systems);
        int renamed = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var state = systems.AssignBosses(RunEngine.Start(SystemsTestSupport.Setup(clubRace: Race.Dwarf), seed, Catalog, systems));
            var map = state.MapOf(1);
            var node = map.Get(map.BossNodeId);
            var clubNames = state.Roster.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

            var team = systems.OpponentFor(state, node, Catalog);

            var names = team.Players.Select(p => p.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
            Assert.DoesNotContain(names, clubNames.Contains);

            // Sin cambiar nada de la plantilla del club, el equipo del jefe es el que sale de sus datos, salvo el nombre.
            var boss = bosses.ForAct(1);
            var rng = RngStreams.Generation(state.Seed, node.Id);
            var raw = boss.Template.ToTeamSetup(ref rng, Catalog, boss.Id, DefaultRunSystems.OpponentFirstPlayerId, boss.NameIn("es"));
            Assert.Equal(raw.Players.Select(p => p.Id), team.Players.Select(p => p.Id));
            Assert.Equal(raw.Players.Select(p => p.Attributes), team.Players.Select(p => p.Attributes));
            Assert.Equal(raw.Players.Select(p => string.Join('+', p.Perks)), team.Players.Select(p => string.Join('+', p.Perks)));
            Assert.Equal(raw.Lineup.Slots.Select(s => (s.PlayerId, s.HomeCell)), team.Lineup.Slots.Select(s => (s.PlayerId, s.HomeCell)));
            renamed += raw.Players.Where((p, i) => p.Name != team.Players[i].Name).Count();
        }

        Assert.True(renamed > 0, "instrumento: en 60 semillas alguien del jefe enano tiene que coincidir con un enano del club");
    }

    /// <summary>
    /// Un nombre que ya se usó en la run no vuelve a ofrecerse aunque su jugador ya no esté (se vendió, se fue): el registro lo
    /// recuerda. Se fuerza con una entrada de otra elección que lleva el nombre de una oferta.
    /// </summary>
    [Fact]
    public void ANameThatWasUsedByAnotherChoiceIsNotOfferedAgainEvenWithoutItsPlayer()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 424242UL, Catalog, Systems);
        var node = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);
        string wanted = Offers(state, node).Recruits[0].Player.Name;

        var remembered = state.WithCounter(RunNames.OwnerPrefix + wanted, RunNames.OwnerToken(state, 77) + 1);
        var offers = Offers(remembered, node);

        Assert.DoesNotContain(wanted, PlayersOf(offers).Select(p => p.Name));
    }

    // ------------------------------------------------------------------ una run entera

    /// <summary>
    /// Doce runs por raza jugadas de principio a fin por la política de <c>/Balance</c>, que compra en el mercado,
    /// ficha mercenarios y cobra recompensas: la plantilla con la que acaban —vivos y muertos— no tiene un nombre
    /// repetido, y ninguno con el nombre de un jugador de datos de un clan. Sin la regla, en las mismas semillas el 14 % de
    /// las runs acababa con un repetido (censo de BA-G: 5 de 36). La política no vende (<c>SellKeepingAvailable</c> alto): con la de fábrica, una de cada seis runs intenta
    /// vender a un fichaje sin experiencia y lanza, con o sin esta regla (BA-G, «hermano»).
    /// </summary>
    [Fact]
    public void AWholeRunEndsWithoutARepeatedName()
    {
        var files = TestData.LoadAllFiles();
        var bosses = BossCatalog.FromJson(files);
        var options = RunPolicyOptions.Default with { SellKeepingAvailable = 99 };
        int played = 0;
        foreach (var race in new[] { Race.Human, Race.Dwarf, Race.Orc })
        {
            var setup = SystemsTestSupport.Setup(clubRace: race);
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var result = RunPolicy.Play(setup, seed, Catalog, Systems, bosses, options);
                var names = result.FinalState!.Roster.Select(p => p.Name).ToList();

                Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
                Assert.DoesNotContain(names, ClanDataNames().Contains);
                played++;
            }
        }

        Assert.Equal(36, played);
    }
}
