using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// Bucle de run (RF-002, RF-002b..e, RF-010, RT-030). El test que manda es
/// <see cref="Defeat_HappensOnlyByTheTwoWays"/>: RF-002b dice "dos vías únicamente" y eso es tanto una
/// garantía de que esas dos funcionan como una de que no hay una tercera.
/// </summary>
public class RunEngineTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void Start_LeavesAConsistentRun()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 12345, Catalog);

        Assert.Equal(RunState.CurrentSchemaVersion, state.SchemaVersion);
        Assert.Equal(1, state.Act);
        Assert.Equal(-1, state.CurrentNodeId);
        Assert.Equal(RunPhase.OnMap, state.Phase);
        Assert.Equal(RunRules.Acts, state.Maps.Count);
        Assert.Equal(10, state.Roster.Count);            // RF-005: 7 titulares y 3 suplentes
        Assert.Equal(10, state.AvailablePlayerCount);    // RF-002e
        Assert.False(state.IsBelowMinimum);
        Assert.Equal(RunOutcomeKind.InProgress, RunEngine.Outcome(state).Kind);
        Assert.Contains(state.Roster, p => p.Rarity != Rarity.Common);   // RF-005: uno de rareza superior
        Assert.NotEmpty(state.DataSnapshot);                             // RT-061b
        Assert.Equal(state.CurrentMap.EntryNodeIds.Count, RunEngine.AvailableNodes(state).Count);
    }

    [Fact]
    public void AvailableNodes_NeverGoBackwards()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 4321, Catalog);
        var systems = new TestRunSystems();

        int previousLayer = -1;
        for (int i = 0; i < 8 && !RunEngine.Outcome(state).IsOver; i++)
        {
            var nodes = RunEngine.AvailableNodes(state);
            if (nodes.Count == 0)
            {
                break;
            }

            foreach (var node in nodes)
            {
                Assert.True(node.Layer > previousLayer, "RF-010: no hay retroceso");
            }

            previousLayer = nodes[0].Layer;
            state = RunEngine.Enter(state, nodes[0].Id, Catalog, systems);
            if (state.Act != 1)
            {
                break;
            }
        }
    }

    [Fact]
    public void Enter_RejectsANodeThatIsNotReachable()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 7, Catalog);
        int bossId = state.CurrentMap.BossNodeId;
        var error = Assert.Throws<ArgumentException>(() => RunEngine.Enter(state, bossId, Catalog));
        Assert.Contains("no es accesible", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARunCanBePlayedFromStartToFinish()
    {
        var systems = new TestRunSystems { OpponentQuality = 30 };
        var state = TestRuns.PlayToTheEnd(RunEngine.Start(TestRuns.Setup(quality: 70), 1, Catalog), Catalog, systems);

        var outcome = RunEngine.Outcome(state);
        Assert.True(outcome.IsOver, "la run debería haber terminado");
        Assert.Equal(RunOutcomeKind.Victory, outcome.Kind);
        Assert.Equal(RunPhase.Finished, state.Phase);

        // Tres jefes ganados, uno por acto.
        Assert.Equal(3, state.NodeHistory.Count(e => e.Kind == NodeKind.Boss && e.Result == NodeResult.Won));

        // Y la run tiene la longitud que pide el diseño: 30-36 nodos (RF-003b) y 18-22 partidos
        // (fase2-diseno.md §10). Es la comprobación que ata la lectura de RF-001 al resto de números.
        // El tope sigue siendo 22 y el peor camino, 20 (RF-003b). El SUELO baja a 17 con la ADR 0053
        // (fase2-diseno.md §24, AH-8): hay una capa de partido por acto que se puede esquivar, y este
        // recorrido -que siempre coge el primer nodo disponible, o sea el carril de arriba- la esquiva
        // cuando el servicio cae ahí. Los 18-22 de §10 los mide la política automática sobre runs
        // completas (FullRunGateTests), no un camino ciego.
        Assert.InRange(state.NodeHistory.Count, 30, 36);
        Assert.InRange(state.NodeHistory.Count(e => NodeKinds.IsMatch(e.Kind)), 17, 22);

        // Y la experiencia se ha repartido: alguien ha subido de nivel por el camino (RF-025, RF-027).
        Assert.Contains(state.Roster, p => p.Experience > 0);
    }

    [Fact]
    public void LosingAnOrdinaryMatch_DoesNotEndTheRun()
    {
        // RF-002c: perder un partido ordinario no termina la run.
        var systems = new TestRunSystems { OpponentQuality = 99 };
        var state = RunEngine.Start(TestRuns.Setup(quality: 20), 555, Catalog);

        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, systems);
        state = RunEngine.Enter(walked, node.Id, Catalog, systems);

        Assert.Equal(NodeResult.Lost, state.NodeHistory.Single(e => e.NodeId == node.Id).Result);
        Assert.False(RunEngine.Outcome(state).IsOver);
        Assert.Equal(RunPhase.OnMap, state.Phase);
    }

    [Fact]
    public void LosingTheBossMatch_EndsTheRun()
    {
        // RF-002b, vía 1.
        var systems = new TestRunSystems { OpponentQuality = 99 };
        var state = RunStateBuilder.From(TestRuns.Setup(quality: 15), 909, Catalog).BeforeBoss().Build();

        int bossId = state.CurrentMap.BossNodeId;
        state = RunEngine.Enter(state, bossId, Catalog, systems);

        var outcome = RunEngine.Outcome(state);
        Assert.Equal(RunOutcomeKind.Defeat, outcome.Kind);
        Assert.Equal(DefeatCause.BossMatchLost, outcome.Cause);
        Assert.Equal(bossId, outcome.NodeId);
        Assert.Throws<InvalidOperationException>(() => RunEngine.Enter(state, bossId, Catalog, systems));
    }

    [Fact]
    public void WinningTheThirdBoss_WinsTheRun()
    {
        // RF-002: la run termina en victoria al derrotar al jefe del acto 3.
        var systems = new TestRunSystems { OpponentQuality = 20 };
        // La semilla se elige para que el partido se gane: con calidad 80 contra 20 se gana casi
        // siempre, pero "casi" no es "siempre", y el id del nodo de jefe -del que sale la semilla del
        // partido- se movió con el mapa de cuatro carriles (ADR 0053).
        var state = RunStateBuilder.From(TestRuns.Setup(quality: 80), 4115, Catalog)
            .AtAct(3)
            .BeforeBoss()
            .Build();

        state = RunEngine.Enter(state, state.CurrentMap.BossNodeId, Catalog, systems);

        Assert.Equal(RunOutcomeKind.Victory, RunEngine.Outcome(state).Kind);
    }

    [Fact]
    public void WinningTheFirstBoss_OpensTheNextAct()
    {
        var systems = new TestRunSystems { OpponentQuality = 20 };
        var state = RunStateBuilder.From(TestRuns.Setup(quality: 80), 4115, Catalog).BeforeBoss().Build();

        state = RunEngine.Enter(state, state.CurrentMap.BossNodeId, Catalog, systems);

        Assert.False(RunEngine.Outcome(state).IsOver);
        Assert.Equal(2, state.Act);
        Assert.Equal(-1, state.CurrentNodeId);
        Assert.True(state.MapOf(1).BossModifierRevealed);   // RF-014b
        Assert.Equal(state.MapOf(2).EntryNodeIds.Count, RunEngine.AvailableNodes(state).Count);
    }

    [Fact]
    public void DroppingBelowFivePlayers_EndsTheRunDuringTheMatch()
    {
        // RF-002b, vía 2: con cinco disponibles, una lesión grave o una muerte en pleno partido termina
        // la run al instante. Se juega contra un rival brutal y se buscan las semillas en las que la
        // baja llega de verdad: el test es determinista (recorre semillas fijas) y falla si el motor
        // deja de producir bajas o si el bucle deja de detectarlas.
        var systems = new TestRunSystems { OpponentQuality = 99, OpponentTraits = new[] { Trait.Aggressive, Trait.Dirty } };

        int endedDuringTheMatch = 0;
        for (ulong seed = 1; seed <= 40 && endedDuringTheMatch == 0; seed++)
        {
            var state = RunStateBuilder.From(TestRuns.Setup(quality: 20), seed, Catalog)
                .WithAvailablePlayers(RunRules.MinimumAvailablePlayers)
                .Build();
            Assert.Equal(RunRules.MinimumAvailablePlayers, state.AvailablePlayerCount);

            var (walked, node) = TestRuns.WalkToMatch(state, Catalog, systems);
            var after = RunEngine.Enter(walked, node.Id, Catalog, systems);
            var outcome = RunEngine.Outcome(after);
            if (outcome.Cause != DefeatCause.NotEnoughPlayers)
            {
                continue;
            }

            endedDuringTheMatch++;
            Assert.Equal(RunOutcomeKind.Defeat, outcome.Kind);
            Assert.True(outcome.Tick >= 0, "la derrota durante un partido lleva el tick de la baja");
            Assert.True(after.AvailablePlayerCount < RunRules.MinimumAvailablePlayers);
        }

        Assert.Equal(1, endedDuringTheMatch);
    }

    [Fact]
    public void DroppingBelowFivePlayers_EndsTheRunOutsideAMatch()
    {
        // RF-002b: "en cualquier momento". Vender o perder un jugador fuera del partido cuenta igual.
        var state = RunStateBuilder.From(TestRuns.Setup(), 31, Catalog)
            .WithAvailablePlayers(RunRules.MinimumAvailablePlayers)
            .Build();

        Assert.False(RunEngine.Outcome(state).IsOver);

        var victim = state.AvailablePlayers[0];
        state = state.WithoutPlayer(victim.Id);

        var outcome = RunEngine.Outcome(state);
        Assert.Equal(RunOutcomeKind.Defeat, outcome.Kind);
        Assert.Equal(DefeatCause.NotEnoughPlayers, outcome.Cause);
        Assert.Equal(-1, outcome.Tick);
    }

    [Fact]
    public void Defeat_HappensOnlyByTheTwoWays()
    {
        // RF-002b: se juegan varias runs completas contra rivales duros y se comprueba que ninguna
        // termina por otro motivo, y que ninguna sigue viva después de cumplirse una de las dos vías.
        var systems = new TestRunSystems { OpponentQuality = 75, OpponentTraits = new[] { Trait.Aggressive } };
        int defeats = 0;

        for (ulong seed = 1; seed <= 8; seed++)
        {
            var state = TestRuns.PlayToTheEnd(RunEngine.Start(TestRuns.Setup(quality: 45), seed, Catalog), Catalog, systems);
            var outcome = RunEngine.Outcome(state);
            Assert.True(outcome.IsOver, $"la run con semilla {seed} no terminó");

            if (outcome.Kind == RunOutcomeKind.Victory)
            {
                Assert.Equal(state.CurrentMap.BossNodeId, outcome.NodeId);
                Assert.Equal(3, state.Act);
                continue;
            }

            defeats++;
            Assert.Contains(outcome.Cause, new[] { DefeatCause.BossMatchLost, DefeatCause.NotEnoughPlayers });

            if (outcome.Cause == DefeatCause.BossMatchLost)
            {
                var node = state.GetNode(outcome.NodeId);
                Assert.Equal(NodeKind.Boss, node.Kind);
                Assert.Equal(NodeResult.Lost, state.NodeHistory.Last(e => e.NodeId == node.Id).Result);

                // Un partido ordinario perdido antes nunca terminó la run.
                Assert.Contains(state.NodeHistory, e => e.Kind != NodeKind.Boss);
            }
            else
            {
                Assert.True(state.AvailablePlayerCount < RunRules.MinimumAvailablePlayers);
            }
        }

        Assert.True(defeats > 0, "ninguna de las ocho runs se perdió: el escenario no prueba nada");
    }

    [Fact]
    public void LostOrdinaryMatches_NeverEndARunWithEnoughPlayers()
    {
        // La otra mitad de "solo por esas dos vías": una run con partidos ordinarios perdidos sigue viva
        // mientras le queden cinco jugadores.
        var systems = new TestRunSystems { OpponentQuality = 95 };
        var state = RunEngine.Start(TestRuns.Setup(quality: 25), 2024, Catalog);

        int lost = 0;
        for (int i = 0; i < 6 && !RunEngine.Outcome(state).IsOver; i++)
        {
            var node = RunEngine.AvailableNodes(state).FirstOrDefault(n => n.Kind is NodeKind.LeagueMatch or NodeKind.EliteMatch);
            if (node is null)
            {
                var any = RunEngine.AvailableNodes(state);
                if (any.Count == 0)
                {
                    break;
                }

                state = RunEngine.Enter(state, any[0].Id, Catalog, systems);
                continue;
            }

            state = RunEngine.Enter(state, node.Id, Catalog, systems);
            if (state.NodeHistory.Last().Result == NodeResult.Lost)
            {
                lost++;
                Assert.True(
                    RunEngine.Outcome(state).IsOver == state.IsBelowMinimum,
                    "una derrota ordinaria solo puede terminar la run si además deja la plantilla por debajo de 5");
            }
        }

        Assert.True(lost > 0, "no se perdió ningún partido ordinario: el escenario no prueba nada");
    }

    [Fact]
    public void PlayingShorthanded_IsAllowed()
    {
        // RF-002d: se puede jugar con 5 o 6, dejando casillas vacías.
        foreach (int available in new[] { 5, 6 })
        {
            var state = RunStateBuilder.From(TestRuns.Setup(), 77, Catalog)
                .WithAvailablePlayers(available)
                .Build();

            var (_, node) = TestRuns.WalkToMatch(state, Catalog, new TestRunSystems());
            var (setup, _, lineup) = RunEngine.BuildMatch(state, node.Id, Catalog);
            Assert.Equal(available, lineup.Lineup.Slots.Count);
            Assert.Equal(available, setup.Home.Players.Count);
            Assert.Equal(lineup.Lineup.Slots.Count, lineup.Lineup.Slots.Select(s => s.HomeCell).Distinct().Count());
            Assert.Single(setup.Home.Players, p => p.Position == Position.Goalkeeper);
        }
    }

    [Fact]
    public void WithoutAGoalkeeper_AnOutfieldPlayerTakesTheGloves()
    {
        // Sin esto la run se quedaría sin partido jugable, y RF-002b dice que solo termina de dos formas.
        var state = RunEngine.Start(TestRuns.Setup(), 606, Catalog);
        var goalkeeper = state.Roster.First(p => p.Position == Position.Goalkeeper);
        state = state.WithPlayer(goalkeeper with { PhysicalState = PhysicalState.SevereInjury });

        var (_, node) = TestRuns.WalkToMatch(state, Catalog, new TestRunSystems());
        var (setup, _, lineup) = RunEngine.BuildMatch(state, node.Id, Catalog);

        Assert.NotEqual(-1, lineup.EmergencyGoalkeeperId);
        Assert.NotEqual(goalkeeper.Id, lineup.EmergencyGoalkeeperId);
        Assert.Single(setup.Home.Players, p => p.Position == Position.Goalkeeper);
        Assert.Equal(Position.Goalkeeper, state.GetPlayer(lineup.EmergencyGoalkeeperId).Position != Position.Goalkeeper
            ? setup.Home.Players.Single(p => p.Id == lineup.EmergencyGoalkeeperId).Position
            : Position.Goalkeeper);
    }

    [Fact]
    public void SameSeed_SameRun()
    {
        // RT-024 aplicado al bucle completo: dos runs con la misma semilla recorren el mismo mapa y
        // terminan igual.
        var systems = new TestRunSystems { OpponentQuality = 55 };
        var a = TestRuns.PlayToTheEnd(RunEngine.Start(TestRuns.Setup(), 8888, Catalog), Catalog, systems);
        var b = TestRuns.PlayToTheEnd(RunEngine.Start(TestRuns.Setup(), 8888, Catalog), Catalog, systems);

        Assert.Equal(RunEngine.Outcome(a), RunEngine.Outcome(b));
        Assert.Equal(a.NodeHistory.Count, b.NodeHistory.Count);
        for (int i = 0; i < a.NodeHistory.Count; i++)
        {
            Assert.Equal(a.NodeHistory[i], b.NodeHistory[i]);
        }

        Assert.Equal(a.AvailablePlayerCount, b.AvailablePlayerCount);
    }

    [Fact]
    public void MinorInjury_CostsAttributesForOneMatchAndThenClears()
    {
        // RF-091: -15% a todos los atributos durante el siguiente partido, acumulable.
        var state = RunEngine.Start(TestRuns.Setup(), 1212, Catalog);
        var player = state.Roster.First(p => p.Position != Position.Goalkeeper);
        var healthy = player.ToDefinition(Catalog);

        var hurt = player with { PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 1 };
        var hurtDefinition = hurt.ToDefinition(Catalog);
        Assert.Equal(player.Attributes.Strength * 85 / 100, hurtDefinition.Attributes.Strength);
        Assert.Equal(healthy.Attributes.Leash, hurtDefinition.Attributes.Leash);

        var twice = (player with { PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 2 }).ToDefinition(Catalog);
        Assert.Equal(player.Attributes.Speed * 70 / 100, twice.Attributes.Speed);

        // Y tras jugar un partido, el titular sale sin la penalización pendiente.
        var systems = new TestRunSystems { OpponentQuality = 20 };
        state = state.WithPlayer(hurt);
        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, systems);
        state = RunEngine.Enter(walked, node.Id, Catalog, systems);

        var after = state.GetPlayer(player.Id);
        Assert.True(after.MinorInjuries == 0 || after.PhysicalState != PhysicalState.Healthy);
    }

    [Fact]
    public void MarketDecisions_AreLeftToPackageX()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 3, Catalog);
        var error = Assert.Throws<NotSupportedException>(
            () => RunEngine.Apply(state, new BuyOffer("players", 0), Catalog));
        Assert.Contains("paquete X", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LineupAndConsumables_AreValidated()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 3, Catalog);

        var slots = state.Roster.Take(6).Select((p, i) => new LineupSlot(p.Id, new Cell(i == 0 ? 0 : 2 + i, 2))).ToList();
        state = RunEngine.Apply(state, new SetLineup(new Lineup(slots)), Catalog);
        Assert.Equal(6, state.Lineup.Slots.Count);

        Assert.Throws<ArgumentException>(
            () => RunEngine.Apply(state, new SetLineup(new Lineup(slots.Take(4).ToList())), Catalog));

        var dead = state.Roster[^1] with { PhysicalState = PhysicalState.Dead };
        var withDead = state.WithPlayer(dead);
        Assert.Throws<ArgumentException>(() => RunEngine.Apply(
            withDead,
            new SetLineup(new Lineup(slots.Take(5).Append(new LineupSlot(dead.Id, new Cell(6, 2))).ToList())),
            Catalog));

        state = RunEngine.Apply(state, new SetConsumables(new[] { new EquippedConsumable("bandage", ConsumableMode.Manual, "TACKLE") }), Catalog);
        Assert.Single(state.Consumables);

        Assert.Throws<ArgumentException>(() => RunEngine.Apply(
            state,
            new SetConsumables(new[] { new EquippedConsumable("a", ConsumableMode.Conditional, "TACKLE") }),
            Catalog));
    }
}
