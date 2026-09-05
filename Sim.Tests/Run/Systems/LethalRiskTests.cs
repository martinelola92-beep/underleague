using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// Las <b>cinco condiciones</b> que la ADR 0048 declara obligatorias desde que un jugador sano puede
/// morir. Son lo único que separa el azar duro del azar injusto, así que se comprueban aquí como
/// propiedades del sistema y no como buenas intenciones:
/// <list type="number">
/// <item><b>Anticipación</b>: el perk letal sale en el informe de ojeo (RF-013) — lo cubre
/// <c>LethalPerkTests</c>— y en la descripción generada (RT-035).</item>
/// <item><b>Evitación</b>: ese rival vive en un nodo concreto y el mapa ofrece otra ruta.</item>
/// <item><b>Reducción</b>: el indicador de riesgo por jugador existe, es numérico y <b>cambia al cambiar
/// la alineación</b> (RF-012c). Es la condición más importante de las cinco.</item>
/// <item><b>Recuperación</b>: el objeto del muerto vuelve al inventario (RF-075..078).</item>
/// <item><b>Rareza</b>: se mide en la puerta de run completa, no aquí.</item>
/// </list>
/// </summary>
public sealed class LethalRiskTests
{
    /// <summary>
    /// Condición 3, primera mitad: contra un rival con perk letal, cada titular tiene un <b>número</b>, y
    /// no todos el mismo. Sin número no hay nada que reducir.
    /// </summary>
    [Fact]
    public void AgainstALethalOpponentEveryStarterGetsARiskNumber()
    {
        var (state, node) = StateAtLethalMatch();
        var risks = RunEngine.LethalRisks(state, node.Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

        Assert.NotEmpty(risks);
        Assert.All(risks, r => Assert.Equal(LineupWarningKind.LethalOpponentRisk, r.Kind));
        Assert.Contains(risks, r => r.Risk > 0);
        Assert.All(risks, r => Assert.InRange(r.Risk, 0, 10000));

        // Y aparece en las advertencias previas a confirmar, que es donde la interfaz lo lee (RF-012d).
        var warnings = RunEngine.LineupWarnings(state, node.Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        Assert.Contains(warnings, w => w.Kind == LineupWarningKind.LethalOpponentRisk && w.Risk > 0);
    }

    /// <summary>
    /// Condición 3, la que de verdad la sostiene: <b>cambiar la alineación cambia el número</b>. Se
    /// comprueban las dos palancas por separado, porque son dos decisiones distintas del jugador:
    /// mover a alguien de casilla y cambiar quién juega.
    /// </summary>
    [Fact]
    public void MovingThePlayersChangesTheNumber()
    {
        var (state, node) = StateAtLethalMatch();
        var baseline = Total(RunEngine.LethalRisks(state, node.Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems));
        Assert.True(baseline > 0, "el rival de este nodo no amenaza a nadie: el escenario no prueba nada");

        // (a) Colocación: llevar al marcado a la casilla más lejos del carnicero le baja el número. Es
        // literalmente "alejarlo de la banda peligrosa" de la ADR 0048.
        var slots = new List<LineupSlot>(state.Lineup.Slots);
        var original = RunEngine.LethalRisks(state, node.Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        int marked = original.OrderByDescending(r => r.Risk).ThenBy(r => r.PlayerId).First().PlayerId;

        var carriers = Lethality.CarriersOf(
            SystemsTestSupport.Systems.OpponentFor(state, node, SystemsTestSupport.Catalog), SystemsTestSupport.Catalog);
        int a = slots.FindIndex(s => s.PlayerId == marked);
        int b = FarthestSlot(slots, carriers);
        Assert.NotEqual(a, b);

        var swapped = new List<LineupSlot>(slots);
        swapped[a] = swapped[a] with { HomeCell = slots[b].HomeCell };
        swapped[b] = swapped[b] with { HomeCell = slots[a].HomeCell };
        var moved = RunEngine.LethalRisks(
            state, node.Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems, new Lineup(swapped));

        Assert.Equal(original.Count, moved.Count);
        Assert.True(
            moved.Single(r => r.PlayerId == marked).Risk < original.Single(r => r.PlayerId == marked).Risk,
            "mover al marcado lejos del portador no le ha bajado el riesgo: la colocación no es una palanca");

        // (b) Quién juega: sentar al que más riesgo tiene y sacar a un suplente cambia el total.
        int worst = original.OrderByDescending(r => r.Risk).ThenBy(r => r.PlayerId).First().PlayerId;
        var bench = state.AvailablePlayers.FirstOrDefault(p => original.All(r => r.PlayerId != p.Id));
        Assert.NotNull(bench);

        var replaced = new List<LineupSlot>(slots);
        int index = replaced.FindIndex(s => s.PlayerId == worst);
        replaced[index] = replaced[index] with { PlayerId = bench!.Id };
        var afterSwap = RunEngine.LethalRisks(
            state, node.Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems, new Lineup(replaced));

        Assert.NotEqual(baseline, Total(afterSwap));
    }

    /// <summary>
    /// Condición 3, tercera palanca y la que el jugador siente antes que ninguna: <b>estar tocado
    /// multiplica el número</b>. La regla vieja de RF-093 —un sano no muere— no desaparece del todo:
    /// deja de ser una puerta y se convierte en un multiplicador (ADR 0048).
    /// </summary>
    [Fact]
    public void BeingHurtMultipliesTheNumberOfTheSamePlayerInTheSameCell()
    {
        var (state, node) = StateAtLethalMatch();
        var risks = RunEngine.LethalRisks(state, node.Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        int target = risks.OrderByDescending(r => r.Risk).ThenBy(r => r.PlayerId).First().PlayerId;

        var hurt = state.WithPlayer(state.GetPlayer(target).WithPhysicalState(PhysicalState.MinorInjury));
        var after = RunEngine.LethalRisks(hurt, node.Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

        int before = risks.Single(r => r.PlayerId == target).Risk;
        int now = after.Single(r => r.PlayerId == target).Risk;
        Assert.True(now > before, $"tocado {now} y sano {before}: el estado tiene que pesar en el indicador");
    }

    /// <summary>
    /// Condición 2: el rival letal vive en un <b>nodo concreto</b> y el mapa ofrece otra ruta. Si el
    /// jugador no pudiera esquivar el partido, la información previa no serviría de nada.
    /// </summary>
    [Fact]
    public void TheLethalOpponentLivesInOneNodeAndThereIsAnotherRoute()
    {
        int lethalNodes = 0;
        int withSiblingRoute = 0;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var state = RunEngine.Start(SystemsTestSupport.Setup(Race.Orc), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
            for (int act = 1; act <= RunRules.Acts; act++)
            {
                var map = state.MapOf(act);
                foreach (var candidate in map.Nodes)
                {
                    // El jefe queda fuera: es la puerta del acto, no un desvío, y esquivarlo no sería una
                    // ruta sino no jugar.
                    if (!candidate.IsMatch || candidate.Kind == NodeKind.Boss || !HasLethalCarrier(state, candidate))
                    {
                        continue;
                    }

                    lethalNodes++;
                    if (map.Nodes.Any(other =>
                        other.Layer == candidate.Layer && other.Id != candidate.Id && !HasLethalCarrier(state, other)))
                    {
                        withSiblingRoute++;
                    }
                }
            }
        }

        Assert.True(lethalNodes > 0, "ninguna de las veinte runs tenía un rival letal en el mapa: el escenario no prueba nada");

        // Medido, y es un compromiso con número: con CUATRO rivales letales de cinco por acto solo el
        // 30% de los nodos letales tenía alternativa limpia en su capa; con TRES (lo que hoy hay) sube al
        // 50%; con DOS llega al 77%, pero entonces las muertes por run se quedan en 0,8 y no alcanzan la
        // banda 1,5-3 de la ADR 0048. El 100% no es alcanzable con tres de cinco ni con el mejor reparto
        // posible: MapGenerator reparte los rivales con un cursor consecutivo sobre la baraja del acto y
        // tres marcados en un ciclo de cinco tienen por fuerza dos adyacentes. Cerrarlo del todo exige
        // bajar a dos letales por acto y añadir la restricción "dos letales nunca en la misma capa" al
        // generador; queda anotado en pendientes.md con su coste. La cota es de no regresión.
        double share = 100.0 * withSiblingRoute / lethalNodes;
        Assert.True(
            share >= 45.0,
            $"solo el {share:F0}% de los nodos letales tiene alternativa no letal en su capa (ADR 0048, condición 2)");
    }

    /// <summary>
    /// Condición 4: <b>el objeto del muerto vuelve al inventario</b>. Morir cuesta el jugador, no el
    /// jugador y su equipo; es la mitad "se puede rehacer" de lo que hace sostenible que un sano muera.
    /// </summary>
    [Fact]
    public void TheItemOfADeadPlayerComesBackToTheStore()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 909, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var victim = state.Roster[0];
        string itemId = SystemsTestSupport.Systems.Items.All[0].Id;

        state = state.WithPlayer(victim with { Item = itemId });
        Assert.Equal(0, state.StockOf(itemId));

        // La muerte la aplica el bucle al leer el evento DEATH; aquí se ejercita el efecto de estado que
        // esa lectura produce, que es lo que la interfaz y la política ven.
        var dead = state.GetPlayer(victim.Id);
        state = state
            .WithPlayer(dead with { Item = null, PhysicalState = PhysicalState.Dead })
            .WithStockedItem(itemId, 1);

        Assert.Equal(1, state.StockOf(itemId));
        Assert.Contains(itemId, state.StoredItems);

        // Y se le puede dar a un vivo sin pagar nada.
        var heir = state.Roster.First(p => p.PhysicalState != PhysicalState.Dead && p.Item is null);
        int goldBefore = state.Gold;
        state = RunEngine.Apply(
            state, new EquipStoredItem(heir.Id, itemId), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

        Assert.Equal(itemId, state.GetPlayer(heir.Id).Item);
        Assert.Equal(goldBefore, state.Gold);
        Assert.Equal(0, state.StockOf(itemId));
    }

    /// <summary>El almacén no es un grifo: solo sale de él lo que un muerto dejó.</summary>
    [Fact]
    public void AnItemThatNobodyDiedWithCannotBeClaimed()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 910, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        string itemId = SystemsTestSupport.Systems.Items.All[0].Id;

        Assert.Throws<ArgumentException>(() => RunEngine.Apply(
            state, new EquipStoredItem(state.Roster[0].Id, itemId), SystemsTestSupport.Catalog, SystemsTestSupport.Systems));
    }

    // ------------------------------------------------------------------ interno

    private static int Total(IReadOnlyList<LineupWarning> risks)
    {
        int total = 0;
        for (int i = 0; i < risks.Count; i++)
        {
            total += risks[i].Risk;
        }

        return total;
    }

    /// <summary>Índice del titular más lejos de todos los portadores, por emparejamiento (ADR 0048).</summary>
    private static int FarthestSlot(
        IReadOnlyList<LineupSlot> slots, IReadOnlyList<Lethality.LethalCarrier> carriers)
    {
        int best = 0;
        int bestDistance = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            int distance = int.MaxValue;
            for (int c = 0; c < carriers.Count; c++)
            {
                distance = Math.Min(distance, Lethality.Matchup(carriers[c].Home, slots[i].HomeCell));
            }

            if (distance > bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static bool HasLethalCarrier(RunState state, MapNode node) =>
        Scouting.LethalPerks(
            SystemsTestSupport.Systems.OpponentFor(state, node, SystemsTestSupport.Catalog),
            SystemsTestSupport.Catalog).Count > 0;

    /// <summary>
    /// Semillas con las que se busca una run que llegue a un partido de rival letal. Los rivales letales
    /// viven en los actos 2 y 3 (<c>data/rivals/</c>), así que llegar a uno exige <b>ganar</b> el camino
    /// hasta allí: con una sola semilla, cualquier cambio de catálogo o de resolución que mueva un
    /// resultado deja el test sin caso y lo hace fallar por algo que no está probando. Se prueban varias
    /// y se usa la primera que sirve, que es determinista igual.
    /// </summary>
    private static readonly ulong[] LethalSearchSeeds =
        { 3, 4, 5, 7, 9, 10, 11, 13, 17, 21, 24, 25, 27, 28, 30, 33, 35, 36, 39, 41, 45, 46, 48, 50,
          31337, 4242, 90210, 1234, 777, 20250905, 5150, 8675309, 112358, 606, 2718281, 31415, 99991,
          424242, 13, 271828, 55555, 1618033, 101, 202, 303, 404, 505, 606060, 7007, 80808, 909090 };

    /// <summary>Una run parada delante de un partido cuyo rival lleva algún perk letal.</summary>
    private static (RunState State, MapNode Node) StateAtLethalMatch()
    {
        foreach (ulong seed in LethalSearchSeeds)
        {
            if (SearchLethalMatch(seed) is { } found)
            {
                return found;
            }
        }

        throw new InvalidOperationException(
            "no se ha encontrado ningún partido con rival letal en ninguna de las semillas de búsqueda");
    }

    private static (RunState State, MapNode Node)? SearchLethalMatch(ulong seed)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(Race.Orc), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        for (int i = 0; i < 60 && !RunEngine.Outcome(state).IsOver; i++)
        {
            if (state.Phase == RunPhase.NodeOpen)
            {
                state = RunEngine.Apply(state, new LeaveNode(), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
                continue;
            }

            var available = RunEngine.AvailableNodes(state);
            if (available.Count == 0)
            {
                break;
            }

            var lethal = available.FirstOrDefault(n => n.IsMatch && HasLethalCarrier(state, n));
            if (lethal is not null)
            {
                return (state, lethal);
            }

            state = RunEngine.Enter(state, available[0].Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        }

        return null;
    }
}
