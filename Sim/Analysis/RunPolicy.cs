using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Run.Systems.Rewards;

namespace Underleague.Sim.Analysis;

/// <summary>
/// Las tres doctrinas de compra que la ADR 0037 enfrenta. Es lo <b>único</b> que cambia entre las tres
/// políticas automáticas: nodo, alineación, clínica y elección de recompensa son idénticos, para que la
/// diferencia de tasa de victoria sea atribuible a la decisión de comprar y a nada más.
/// </summary>
public enum PurchaseDoctrine
{
    /// <summary>Compra lo primero que mejora la plantilla en cuanto puede pagarlo. Sin reserva y sin listón.</summary>
    Spender,

    /// <summary>No compra salvo que el artículo sea raro o legendario. Acumula.</summary>
    Saver,

    /// <summary>
    /// Compra según lo que falta para la puerta siguiente: reparte el oro entre los mercados que quedan
    /// antes del jefe del acto y lo gasta entero en el último.
    /// </summary>
    Contextual,
}

/// <summary>
/// Umbrales de la política automática de <see cref="RunPolicy"/>. Todos son enteros con nombre: la
/// política tiene que poder leerse entera desde aquí, porque su valor está en ser <b>explicable</b>, no
/// en jugar bien (fase2-diseno.md §10, ADR 0037).
/// </summary>
public sealed record RunPolicyOptions
{
    /// <summary>Doctrina de compra (ADR 0037). Lo demás es igual en las tres políticas.</summary>
    public PurchaseDoctrine Doctrine { get; init; } = PurchaseDoctrine.Contextual;

    /// <summary>Trata en la clínica mientras los disponibles estén por debajo de este número.</summary>
    public int TreatWhileAvailableBelow { get; init; } = 8;

    /// <summary>Acepta un partido de élite solo con al menos estos disponibles.</summary>
    public int EliteFromAvailable { get; init; } = 8;

    /// <summary>Tope de plantilla: por encima no se fichan canteranos ni se aceptan jugadores de recompensa.</summary>
    public int RosterCap { get; init; } = 13;

    /// <summary>Ficha de pago aunque no mejore al once si los disponibles bajan de este número.</summary>
    public int SignWhileAvailableBelow { get; init; } = 8;

    /// <summary>Ficha un mercenario solo si los disponibles están por debajo de este número (D-3).</summary>
    public int HireMercenaryWhileAvailableBelow { get; init; } = 6;

    /// <summary>Nunca vende si con ello los disponibles bajan de este número.</summary>
    public int SellKeepingAvailable { get; init; } = 8;

    /// <summary>Cuánto vale un perk en puntos de atributo al valorar a un jugador.</summary>
    public int PerkWorthInAttributePoints { get; init; } = 10;

    /// <summary>Cuánto vale un objeto equipado en puntos de atributo al valorar a un jugador.</summary>
    public int ItemWorthInAttributePoints { get; init; } = 8;

    /// <summary>Compras máximas en un mismo nodo de mercado; corta el bucle, no la política.</summary>
    public int MaxMarketActions { get; init; } = 16;

    /// <summary>Pasos máximos del bucle de run; corta el bucle, no la política.</summary>
    public int MaxSteps { get; init; } = 200;

    /// <summary>Umbrales por defecto, con la doctrina contextual.</summary>
    public static RunPolicyOptions Default { get; } = new();

    /// <summary>Los mismos umbrales con la doctrina indicada.</summary>
    public static RunPolicyOptions For(PurchaseDoctrine doctrine) => Default with { Doctrine = doctrine };

    /// <summary>
    /// Múltiplo del coste del reroll que hay que tener para gastarlo (RF-071b). La gastadora repite en
    /// cuanto puede pagarlo; la ahorradora no repite nunca; la contextual solo con holgura.
    /// </summary>
    public int RerollGoldFactor => Doctrine switch
    {
        PurchaseDoctrine.Spender => 1,
        PurchaseDoctrine.Saver => int.MaxValue,
        _ => 3,
    };
}

/// <summary>
/// Lo que una run jugada con una política automática deja para <c>runs.csv</c> (fase2-diseno.md §10,
/// ADR 0037). Enteros: los promedios los calcula <see cref="FullRunMetrics"/>.
/// </summary>
public sealed record RunPlayResult(
    ulong Seed,
    PurchaseDoctrine Doctrine,
    Race ClubRace,
    RunOutcomeKind Outcome,
    DefeatCause Cause,
    int ActReached,
    int Matches,
    int MatchesWon,
    int BossesBeaten,
    int GoldEarned,
    int GoldFromSales,
    int GoldSpentMarket,
    int GoldSpentClinic,
    int GoldSpentReroll,
    int GoldSpentWages,
    int GoldLeft,
    int Deaths,
    int Injuries,
    int OwnInjuries,
    int SevereInjuriesSuffered,
    int FinalRosterSize,
    int FinalAvailable,
    int AverageLevelTimes100,
    int PerksOnRoster,
    int PerksOnStarters,
    int ItemsOnRoster,
    int AccumulatedCounters,
    int MarketsVisited,
    int OffersSeen,
    int OffersAffordable,
    int GoldAtMarketArrival,
    int BrokeMarketVisits,
    int Purchases,
    int PerksBought,
    int ItemsBought,
    int PlayersSigned,
    int YouthsSigned,
    int MercenariesHired,
    int PlayersSold,
    int Treatments,
    int Rerolls,
    IReadOnlyList<int> MatchesByAct,
    IReadOnlyList<int> WinsByAct,
    IReadOnlyList<int> MarketsByAct,
    IReadOnlyList<int> GoldEarnedByAct)
{
    /// <summary>True si la run terminó ganando al jefe final (RF-002).</summary>
    public bool Won => Outcome == RunOutcomeKind.Victory;

    /// <summary>Oro gastado en los cuatro sumideros vivos en fase 2 (RF-114k).</summary>
    public int GoldSpent => GoldSpentMarket + GoldSpentClinic + GoldSpentReroll + GoldSpentWages;

    /// <summary>True si la run llegó a pasar por al menos un nodo de mercado (RF-114b: quien no pasa, no se lleva nada).</summary>
    public bool VisitedMarket => MarketsVisited > 0;
}

/// <summary>
/// <b>Las políticas automáticas</b> con las que <c>/Balance --full-runs</c> juega runs completas
/// (fase2-diseno.md §10, ADR 0037). Ninguna pretende jugar bien: pretenden ser <b>legibles y
/// reproducibles</b>, para que un cambio en la economía se lea en la métrica y no en el criterio de
/// quien mide. Son puras y deterministas, como todo lo demás de <c>/Sim</c>: mismo (setup, semilla,
/// catálogo, doctrina) =&gt; misma run.
///
/// <para><b>Tres políticas, una sola diferencia.</b> La ADR 0037 mide la escasez enfrentando una
/// doctrina <i>gastadora</i>, una <i>ahorradora</i> y una <i>contextual</i>; para que la comparación
/// signifique algo, todo lo demás es idéntico entre las tres. Lo único que cambia es
/// <see cref="RunPolicyOptions.Doctrine"/>: cuánto se permite gastar en un mercado, qué listón exige a
/// un artículo y cuándo repite la tirada de recompensa.</para>
///
/// <para><b>Las reglas comunes</b>, en el orden en que se aplican:</para>
/// <list type="number">
/// <item><b>Qué nodo.</b> Si hay un lesionado grave sin tratar y el oro cubre la clínica, la clínica. Si
/// no, el mercado. Entre partidos: el de élite solo con <see cref="RunPolicyOptions.EliteFromAvailable"/>
/// disponibles o más, y si no, el de menor dificultad. Entre servicios: el evento si el oro no llega a
/// pagar una clínica, y si no, el entrenamiento. A igualdad, el id más bajo (RT-041).</item>
/// <item><b>Quién juega.</b> Los siete de más <i>valor</i> por rol (1 portero, 2 defensas, 3
/// centrocampistas, 1 delantero, y el resto por valor). Valor = suma de los cinco atributos +
/// <see cref="RunPolicyOptions.PerkWorthInAttributePoints"/> por perk +
/// <see cref="RunPolicyOptions.ItemWorthInAttributePoints"/> si lleva objeto.</item>
/// <item><b>Cuándo se arriesga a un lesionado grave</b> (RF-093 vía 1). Cuando no hay siete
/// disponibles, o cuando el oro no cubre su tratamiento y aun así es mejor que el suplente al que
/// sustituiría. Quien está con lesión grave ya <b>no cuenta</b> para el mínimo de RF-002b, así que
/// alinearlo no acerca la derrota por plantilla: lo que arriesga es perderlo para siempre.</item>
/// <item><b>Clínica.</b> Trata al lesionado grave de más valor mientras los disponibles estén por
/// debajo de <see cref="RunPolicyOptions.TreatWhileAvailableBelow"/> y el oro alcance.</item>
/// <item><b>Mercado</b>, regenerando el surtido tras cada compra (el surtido depende de la plantilla):
/// canteranos gratis mientras la plantilla no llegue al tope; luego un perk para un titular, luego un
/// objeto para un titular sin objeto, luego un fichaje que mejore al titular más flojo, y un mercenario
/// solo si faltan cuerpos. Nunca compra consumibles, y hay que decir por qué: el estado no lleva
/// inventario de consumibles (X-9), así que equiparlos no exige haberlos comprado y pagar por ellos es
/// tirar oro. Mientras haya un lesionado grave sin tratar se <b>reserva</b> el precio de la clínica.</item>
/// <item><b>Recompensa</b> (RF-071). Prefiere el perk para un titular; luego el objeto para un titular
/// sin objeto; luego el jugador.</item>
/// <item><b>Reroll</b> (RF-071b). Lo gasta cuando ninguna de las tres opciones es un perk para un
/// titular ni un objeto para un titular sin objeto, y el oro reservable cubre
/// <see cref="RunPolicyOptions.RerollGoldFactor"/> veces su coste.</item>
/// </list>
///
/// <para><b>Y la única diferencia</b>, en la regla 5 y en la 7:</para>
/// <list type="bullet">
/// <item><b>Gastadora</b>: presupuesto = todo el oro, sin reserva de clínica, y compra el artículo más
/// barato que mejore algo. Repite la tirada en cuanto puede pagarla.</item>
/// <item><b>Ahorradora</b>: solo compra artículos <b>raros o legendarios</b> —perk, objeto o fichaje—;
/// los comunes no pasan su listón. Nunca repite la tirada.</item>
/// <item><b>Contextual</b>: presupuesto = oro repartido entre los mercados que le quedan <b>antes del
/// jefe de este acto</b>, y el oro entero en el último; dentro de ese presupuesto prefiere el raro o
/// legendario y, si no le llega, compra el común. Guardar oro para después del examen no vale
/// nada.</item>
/// </list>
/// </summary>
public static class RunPolicy
{
    /// <summary>Juega una run entera con la política indicada y devuelve su fila de <c>runs.csv</c>.</summary>
    public static RunPlayResult Play(
        RunSetup setup,
        ulong seed,
        Catalog catalog,
        StandardRunSystems standard,
        BossCatalog bosses,
        RunPolicyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(bosses);
        options ??= RunPolicyOptions.Default;

        var bossSystems = new BossRunSystems(bosses, standard);
        var ledger = new Ledger();
        var systems = new RecordingSystems(bossSystems, ledger);
        var state = bossSystems.AssignBosses(RunEngine.Start(setup, seed, catalog, systems));

        for (int step = 0; step < options.MaxSteps && !RunEngine.Outcome(state).IsOver; step++)
        {
            if (state.Phase == RunPhase.NodeOpen)
            {
                state = ResolveOpenNode(state, catalog, standard, systems, options, ledger);
                continue;
            }

            var nodes = RunEngine.AvailableNodes(state);
            if (nodes.Count == 0)
            {
                break;
            }

            var node = ChooseNode(state, nodes, standard.Economy, options);
            state = node.IsMatch
                ? PlayMatch(state, node, catalog, systems, options, standard.Economy.ClinicCost, ledger)
                : EnterService(state, node, catalog, systems, ledger);
        }

        return Summarize(state, setup, seed, options, ledger);
    }

    // ------------------------------------------------------------------ 1. qué nodo

    /// <summary>Regla 1. Devuelve el nodo elegido entre los accesibles; a igualdad, el de id menor (RT-041).</summary>
    public static MapNode ChooseNode(
        RunState state,
        IReadOnlyList<MapNode> nodes,
        EconomyConfig economy,
        RunPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(options);

        bool needsClinic = HasUntreatedSevereInjury(state) && state.Gold >= economy.ClinicCost;
        bool poor = state.Gold < economy.ClinicCost;
        bool strong = state.AvailablePlayerCount >= options.EliteFromAvailable;

        MapNode? best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            int score = node.Kind switch
            {
                NodeKind.Clinic => needsClinic ? 100 : 20,
                NodeKind.Market => 90,
                NodeKind.Event => poor ? 40 : 25,
                NodeKind.Training => 30,
                NodeKind.EliteMatch => strong ? 60 : 40 - node.Difficulty,
                NodeKind.LeagueMatch => 50 - node.Difficulty,
                NodeKind.Boss => 10,
                _ => 0,
            };

            if (score > bestScore)
            {
                bestScore = score;
                best = node;
            }
        }

        return best ?? nodes[0];
    }

    // ------------------------------------------------------------------ 2 y 3. quién juega

    /// <summary>Reglas 2 y 3 sin la clínica a la vista: nunca arriesga a un lesionado grave si hay siete sanos.</summary>
    public static IReadOnlyList<RunPlayer> ChooseStarters(RunState state, RunPolicyOptions options) =>
        ChooseStarters(state, options, clinicCost: int.MaxValue);

    /// <summary>
    /// Reglas 2 y 3 con el coste de la clínica a la vista: se arriesga a un lesionado grave cuando no hay
    /// siete disponibles, o cuando el oro <b>no</b> cubre su tratamiento y aun así es mejor que el
    /// suplente al que sustituiría. Con la clínica pagada al alcance, la política no arriesga a nadie.
    /// </summary>
    public static IReadOnlyList<RunPlayer> ChooseStarters(RunState state, RunPolicyOptions options, int clinicCost)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);

        var pool = new List<RunPlayer>(state.AvailablePlayers);
        var risky = new List<RunPlayer>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState == PhysicalState.SevereInjury)
            {
                risky.Add(state.Roster[i]);
            }
        }

        if (risky.Count > 0)
        {
            SortByValue(risky, options);
            var healthy = new List<RunPlayer>(pool);
            SortByValue(healthy, options);
            int seventhValue = healthy.Count >= RunRules.MaxStarters
                ? Value(healthy[RunRules.MaxStarters - 1], options)
                : int.MinValue;
            bool cannotAffordClinic = state.Gold < clinicCost;

            for (int i = 0; i < risky.Count; i++)
            {
                bool shortHanded = pool.Count < RunRules.MaxStarters;
                bool betterThanBench = cannotAffordClinic && Value(risky[i], options) > seventhValue;
                if (shortHanded || betterThanBench)
                {
                    pool.Add(risky[i]);
                }
            }
        }

        var starters = new List<RunPlayer>(RunRules.MaxStarters);
        TakeBest(starters, pool, Position.Goalkeeper, 1, options);
        TakeBest(starters, pool, Position.Defender, 2, options);
        TakeBest(starters, pool, Position.Midfielder, 3, options);
        TakeBest(starters, pool, Position.Forward, 1, options);

        var rest = new List<RunPlayer>();
        for (int i = 0; i < pool.Count; i++)
        {
            if (!Contains(starters, pool[i].Id))
            {
                rest.Add(pool[i]);
            }
        }

        SortByValue(rest, options);
        for (int i = 0; i < rest.Count && starters.Count < RunRules.MaxStarters; i++)
        {
            starters.Add(rest[i]);
        }

        starters.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return starters;
    }

    /// <summary>Valor de un jugador para la política: atributos, perks y objeto, en puntos de atributo.</summary>
    public static int Value(RunPlayer player, RunPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(options);
        var a = player.Attributes;
        return a.Strength + a.Speed + a.Technique + a.Stamina + a.Leash
            + (options.PerkWorthInAttributePoints * player.Perks.Count)
            + (player.Item is null ? 0 : options.ItemWorthInAttributePoints);
    }

    /// <summary>
    /// Mercados que quedan por delante en este acto, contando el nodo actual si es de mercado. Es el
    /// dato con el que la doctrina contextual reparte el oro: RF-011b garantiza que habrá otro y el mapa
    /// dice cuándo, que es la condición 1 de la ADR 0037 (el dilema es informado, no ciego).
    /// </summary>
    public static int MarketsLeftInAct(RunState state, MapNode from)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(from);
        var map = state.MapOf(from.Act);
        int count = 0;
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            if (map.Nodes[i].Kind == NodeKind.Market && map.Nodes[i].Layer >= from.Layer)
            {
                count++;
            }
        }

        return count < 1 ? 1 : count;
    }

    // ------------------------------------------------------------------ interno

    private static RunState PlayMatch(
        RunState state,
        MapNode node,
        Catalog catalog,
        IRunSystems systems,
        RunPolicyOptions options,
        int clinicCost,
        Ledger ledger)
    {
        var starters = ChooseStarters(state, options, clinicCost);
        if (starters.Count >= RunRules.MinimumAvailablePlayers)
        {
            state = RunEngine.Apply(state, new SetLineup(RunLineup.Compose(starters)), catalog, systems);
        }

        int wagesDue = WagesDue(state);
        int goldBefore = state.Gold;
        int deadBefore = CountState(state, PhysicalState.Dead);
        int severeBefore = CountState(state, PhysicalState.SevereInjury);

        state = RunEngine.Enter(state, node.Id, catalog, systems);

        var outcome = RunEngine.Outcome(state);
        bool won = LastResultAt(state, node.Id) == NodeResult.Won;
        bool ranAfterMatch = outcome.Cause != DefeatCause.NotEnoughPlayers
            && !(node.Kind == NodeKind.Boss && !won);

        int wagesPaid = ranAfterMatch ? Math.Min(wagesDue, goldBefore) : 0;
        int earned = (state.Gold - goldBefore) + wagesPaid;

        ledger.Matches++;
        ledger.MatchesByAct[node.Act - 1]++;
        if (won)
        {
            ledger.MatchesWon++;
            ledger.WinsByAct[node.Act - 1]++;
            if (node.Kind == NodeKind.Boss && outcome.Kind != RunOutcomeKind.Defeat)
            {
                ledger.BossesBeaten++;
            }
        }

        ledger.GoldSpentWages += wagesPaid;
        if (earned > 0)
        {
            ledger.GoldEarned += earned;
            ledger.GoldEarnedByAct[node.Act - 1] += earned;
        }

        ledger.Deaths += CountState(state, PhysicalState.Dead) - deadBefore;
        int severeNow = CountState(state, PhysicalState.SevereInjury);
        if (severeNow > severeBefore)
        {
            ledger.SevereInjuries += severeNow - severeBefore;
        }

        return state;
    }

    private static RunState EnterService(RunState state, MapNode node, Catalog catalog, IRunSystems systems, Ledger ledger)
    {
        int goldBefore = state.Gold;
        var next = RunEngine.Enter(state, node.Id, catalog, systems);
        int delta = next.Gold - goldBefore;
        if (delta > 0)
        {
            ledger.GoldEarned += delta;
            ledger.GoldEarnedByAct[node.Act - 1] += delta;
        }

        if (node.Kind == NodeKind.Market)
        {
            ledger.MarketsVisited++;
            ledger.MarketsByAct[node.Act - 1]++;
        }

        return next;
    }

    private static RunState ResolveOpenNode(
        RunState state,
        Catalog catalog,
        StandardRunSystems standard,
        IRunSystems systems,
        RunPolicyOptions options,
        Ledger ledger)
    {
        var node = state.GetNode(state.PendingNodeId);
        state = node.Kind switch
        {
            NodeKind.Market => VisitMarket(state, node, catalog, standard, systems, options, ledger),
            NodeKind.Clinic => VisitClinic(state, catalog, standard.Economy, systems, options, ledger),
            _ => node.IsMatch && !RewardSystem.AlreadyClaimed(state, node.Id)
                ? TakeReward(state, node, catalog, standard, systems, options, ledger)
                : state,
        };

        return RunEngine.Outcome(state).IsOver
            ? state
            : RunEngine.Apply(state, new LeaveNode(), catalog, systems);
    }

    // ------------------------------------------------------------------ 4. clínica

    private static RunState VisitClinic(
        RunState state,
        Catalog catalog,
        EconomyConfig economy,
        IRunSystems systems,
        RunPolicyOptions options,
        Ledger ledger)
    {
        for (int i = 0; i < RunRules.MaxStarters; i++)
        {
            if (state.AvailablePlayerCount >= options.TreatWhileAvailableBelow || state.Gold < economy.ClinicCost)
            {
                break;
            }

            var patient = BestSevereInjured(state, options);
            if (patient is null)
            {
                break;
            }

            state = RunEngine.Apply(state, new TreatPlayer(patient.Id), catalog, systems);
            ledger.GoldSpentClinic += economy.ClinicCost;
            ledger.Treatments++;
        }

        return state;
    }

    // ------------------------------------------------------------------ 5. mercado

    private static RunState VisitMarket(
        RunState state,
        MapNode node,
        Catalog catalog,
        StandardRunSystems standard,
        IRunSystems systems,
        RunPolicyOptions options,
        Ledger ledger)
    {
        // Escasez (ADR 0037): lo primero que se anota es lo que el jugador ve al llegar y cuánto de eso
        // podría pagar. Se mide **antes** de comprar, que es cuando el dilema existe.
        var arrival = MarketOfferGenerator.Generate(
            state, node, catalog, standard.Economy, standard.Items, standard.Consumables);
        var counted = CountOffers(arrival, state.Gold);
        ledger.OffersSeen += counted.Offers;
        ledger.GoldAtMarketArrival += state.Gold;
        ledger.OffersAffordable += counted.Affordable;
        if (counted.PricedAffordable == 0)
        {
            ledger.BrokeMarketVisits++;
        }

        var used = new HashSet<(string Category, int Index)>();
        int spentHere = 0;
        for (int action = 0; action < options.MaxMarketActions; action++)
        {
            if (RunEngine.Outcome(state).IsOver)
            {
                break;
            }

            var current = action == 0
                ? arrival
                : MarketOfferGenerator.Generate(state, node, catalog, standard.Economy, standard.Items, standard.Consumables);
            var decision = NextMarketAction(state, node, current, catalog, standard.Economy, options, used, spentHere);
            if (decision is null)
            {
                break;
            }

            int goldBefore = state.Gold;
            state = RunEngine.Apply(state, decision, catalog, systems);
            int delta = state.Gold - goldBefore;
            if (delta < 0)
            {
                ledger.GoldSpentMarket += -delta;
                spentHere += -delta;
            }
            else
            {
                ledger.GoldFromSales += delta;
                ledger.GoldEarned += delta;
                ledger.GoldEarnedByAct[node.Act - 1] += delta;
            }

            switch (decision)
            {
                case BuyOffer { Category: MarketCategories.Youth } youth:
                    used.Add((MarketCategories.Youth, youth.OfferIndex));
                    ledger.YouthsSigned++;
                    ledger.Purchases++;
                    break;
                case BuyOffer { Category: MarketCategories.Perk } perk:
                    used.Add((MarketCategories.Perk, perk.OfferIndex));
                    ledger.PerksBought++;
                    ledger.Purchases++;
                    break;
                case BuyOffer { Category: MarketCategories.Item } item:
                    used.Add((MarketCategories.Item, item.OfferIndex));
                    ledger.ItemsBought++;
                    ledger.Purchases++;
                    break;
                case BuyOffer { Category: MarketCategories.Player } player:
                    used.Add((MarketCategories.Player, player.OfferIndex));
                    ledger.PlayersSigned++;
                    ledger.Purchases++;
                    break;
                case HireMercenary mercenary:
                    used.Add(("mercenary", mercenary.OfferIndex));
                    ledger.MercenariesHired++;
                    ledger.Purchases++;
                    break;
                case SellPlayer:
                    ledger.PlayersSold++;
                    break;
                default:
                    break;
            }
        }

        return state;
    }

    /// <summary>Cuántos artículos ofrece el mercado y cuántos puede pagar el jugador (ADR 0037).</summary>
    private readonly record struct OfferCount(int Offers, int Affordable, int PricedAffordable);

    private static OfferCount CountOffers(MarketOffers offers, int gold)
    {
        // Los gratuitos (canteranos y mercenarios) cuentan como surtido y como asequibles: son parte de
        // lo que el jugador ve y puede llevarse, y son dos de las vías de recuperación de la ADR 0037.
        int total = offers.Youths.Count + offers.Mercenaries.Count;
        int affordable = total;
        int priced = 0;

        Count(offers.Recruits.Select(o => o.Price));
        Count(offers.Perks.Select(o => o.Price));
        Count(offers.Items.Select(o => o.Price));
        Count(offers.Consumables.Select(o => o.Price));

        return new OfferCount(total, affordable, priced);

        void Count(IEnumerable<int> prices)
        {
            foreach (int price in prices)
            {
                total++;
                if (price <= gold)
                {
                    affordable++;
                    priced++;
                }
            }
        }
    }

    /// <summary>
    /// Presupuesto que la doctrina se permite gastar en <b>este</b> mercado. Es la única diferencia
    /// numérica entre las tres políticas (ADR 0037).
    /// </summary>
    private static int Budget(
        RunState state,
        MapNode node,
        EconomyConfig economy,
        RunPolicyOptions options,
        int alreadySpentHere)
    {
        int reserved = Spendable(state, economy);
        switch (options.Doctrine)
        {
            case PurchaseDoctrine.Spender:
                return state.Gold;

            case PurchaseDoctrine.Saver:
                return reserved;

            default:
                int markets = MarketsLeftInAct(state, node);
                if (markets <= 1)
                {
                    // Último mercado antes del jefe: guardar oro para después del examen no vale nada.
                    return reserved;
                }

                // No guarda oro por guardarlo: lo que la hace contextual es **qué** compra —solo lo que
                // le falta al once, y repartido— no cuánto se deja para después. El oro que le sobra le
                // sobra porque el surtido no tenía nada que le sirviera, que es la forma honesta de
                // llegar al mercado siguiente con dinero (ADR 0037).
                return reserved;
        }
    }

    private static bool ClearsTheBar(Rarity rarity, RunPolicyOptions options) =>
        options.Doctrine != PurchaseDoctrine.Saver || rarity != Rarity.Common;

    private static RunDecision? NextMarketAction(
        RunState state,
        MapNode node,
        MarketOffers offers,
        Catalog catalog,
        EconomyConfig economy,
        RunPolicyOptions options,
        HashSet<(string Category, int Index)> used,
        int alreadySpentHere)
    {
        // (a) Canteranos: gratis, así que primero y sin mirar el oro. Son además una de las tres vías de
        // recuperación que la ADR 0037 declara obligatorias para que arruinarse no sea irreversible.
        if (state.Roster.Count < options.RosterCap)
        {
            for (int i = 0; i < offers.Youths.Count; i++)
            {
                if (!used.Contains((MarketCategories.Youth, i)))
                {
                    return new BuyOffer(MarketCategories.Youth, i);
                }
            }
        }

        int budget = Budget(state, node, economy, options, alreadySpentHere);
        var lineup = ChooseStarters(state, options);
        var placement = PlacementOf(lineup);

        // (b) Un perk para un titular (RF-114e). Dentro del presupuesto, primero el que pasa el listón de
        // la doctrina y, a igual rareza, el más barato: la escalera de la ADR 0033 la marca la
        // **densidad** de perks en el once (14 en "correcta", 17 en "muy buena").
        int bestPerk = -1, bestPerkCarrier = -1, bestPerkRank = int.MinValue;
        for (int i = 0; i < offers.Perks.Count; i++)
        {
            if (used.Contains((MarketCategories.Perk, i)) || offers.Perks[i].Price > budget)
            {
                continue;
            }

            var perk = catalog.Perks.Find(offers.Perks[i].PerkId);
            if (perk is null || !ClearsTheBar(perk.Rarity, options))
            {
                continue;
            }

            int carrier = BestCarrier(state, perk, PerkPool.EligibleCarriers(state, perk, catalog), lineup, placement, options);
            if (carrier < 0)
            {
                continue;
            }

            int rank = Rank(perk.Rarity, offers.Perks[i].Price, options, perk.ElseEffects.Count == 0);
            if (rank > bestPerkRank)
            {
                bestPerk = i;
                bestPerkRank = rank;
                bestPerkCarrier = carrier;
            }
        }

        if (bestPerk >= 0)
        {
            return new BuyOffer(MarketCategories.Perk, bestPerk, bestPerkCarrier);
        }

        // (c) Un objeto para un titular sin objeto (RF-076), con el mismo criterio.
        int naked = BestStarterWithoutItem(state, lineup, options);
        if (naked >= 0)
        {
            int bestItem = -1, bestItemRank = int.MinValue;
            for (int i = 0; i < offers.Items.Count; i++)
            {
                if (used.Contains((MarketCategories.Item, i)) || offers.Items[i].Price > budget)
                {
                    continue;
                }

                var rarity = offers.Items[i].Rarity;
                if (!ClearsTheBar(rarity, options))
                {
                    continue;
                }

                int rank = Rank(rarity, offers.Items[i].Price, options, safe: true);
                if (rank > bestItemRank)
                {
                    bestItem = i;
                    bestItemRank = rank;
                }
            }

            if (bestItem >= 0)
            {
                return new BuyOffer(MarketCategories.Item, bestItem, naked);
            }
        }

        // (d) Fichaje de pago: si faltan cuerpos, o si mejora en atributos al titular más flojo. Se
        // compara por atributos y no por valor porque el fichaje entra sin perks: lo que se compra es el
        // jugador, y los perks se le ponen después. Es además la única forma de meter en el once a un
        // jugador **raro**, y con él el tercer slot de perk (RF-023) que la fila "muy buena" de la ADR
        // 0033 necesita: sin eso el once se satura en catorce perks y el oro del acto 3 no compra nada.
        int weakestStarter = WeakestStarterAttributes(lineup);
        bool needsBodies = state.AvailablePlayerCount < options.SignWhileAvailableBelow;
        int bestRecruit = -1;
        int bestRecruitAttributes = needsBodies ? int.MinValue : weakestStarter;
        for (int i = 0; i < offers.Recruits.Count; i++)
        {
            if (used.Contains((MarketCategories.Player, i)) || offers.Recruits[i].Price > budget)
            {
                continue;
            }

            var recruit = offers.Recruits[i].Player;
            if (!needsBodies && !ClearsTheBar(recruit.Rarity, options))
            {
                continue;
            }

            int attributes = AttributeSum(recruit);
            if (attributes > bestRecruitAttributes)
            {
                bestRecruit = i;
                bestRecruitAttributes = attributes;
            }
        }

        if (bestRecruit >= 0)
        {
            // (e) Venta para hacer sitio (RF-114f): solo cuando hay a quién fichar y la plantilla está
            // llena, y nunca un canterano —es la inversión de RF-114c, no mercancía—, ni un titular, ni
            // un mercenario, que se marcha solo (RF-111).
            if (state.Roster.Count >= options.RosterCap)
            {
                var surplus = WorstSellable(state, lineup, options);
                return surplus is null || state.AvailablePlayerCount <= options.SellKeepingAvailable
                    ? null
                    : new SellPlayer(surplus.Id);
            }

            return new BuyOffer(MarketCategories.Player, bestRecruit);
        }

        // (f) Mercenario solo si faltan cuerpos: no cuesta fichaje, cuesta salario (RF-110..113, D-3).
        if (state.AvailablePlayerCount < options.HireMercenaryWhileAvailableBelow)
        {
            for (int i = 0; i < offers.Mercenaries.Count; i++)
            {
                if (!used.Contains(("mercenary", i)))
                {
                    return new HireMercenary(i);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Orden de preferencia dentro del presupuesto. Primero <b>que no castigue</b>: un perk con
    /// <c>elseEffects</c> negativos en un portador que no cumple su condición es un malus estático, y la
    /// política no puede evaluar la condición fuera del partido, así que aplica la regla que sí puede
    /// leer —"si no sé si se va a cumplir, prefiero el que no me castiga si no se cumple"—. Luego la
    /// rareza y, a igualdad, el más barato, para que quepan más artículos. La gastadora invierte el
    /// criterio: compra lo primero que puede pagar, que es lo más barato.
    /// </summary>
    private static int Rank(Rarity rarity, int price, RunPolicyOptions options, bool safe) =>
        options.Doctrine == PurchaseDoctrine.Spender
            ? -price
            : ((safe ? 1_000_000 : 0) + ((int)rarity * 10_000)) - price;

    // ------------------------------------------------------------------ 6 y 7. recompensa y reroll

    private static RunState TakeReward(
        RunState state,
        MapNode node,
        Catalog catalog,
        StandardRunSystems standard,
        IRunSystems systems,
        RunPolicyOptions options,
        Ledger ledger)
    {
        var rewards = RewardSystem.Options(state, node, catalog, standard.Economy, standard.Items);
        var choice = PickReward(state, rewards, catalog, options);

        if (choice.Score < BestRewardScore && state.NodeRerolls == 0 && options.RerollGoldFactor != int.MaxValue)
        {
            int cost = standard.Economy.RerollCost(state.RerollsUsed);
            if (Spendable(state, standard.Economy) >= cost * options.RerollGoldFactor)
            {
                state = RunEngine.Apply(state, new RerollRewards(), catalog, systems);
                ledger.GoldSpentReroll += cost;
                ledger.Rerolls++;
                var rerolled = RewardSystem.Options(state, node, catalog, standard.Economy, standard.Items);
                choice = PickReward(state, rerolled, catalog, options);
            }
        }

        return choice.Index < 0
            ? state
            : RunEngine.Apply(state, new ChooseReward(choice.Index, choice.Carrier), catalog, systems);
    }

    /// <summary>Puntuación de la mejor recompensa posible: un perk o un objeto para un titular.</summary>
    private const int BestRewardScore = 3;

    private readonly record struct RewardChoice(int Index, int Carrier, int Score);

    private static RewardChoice PickReward(
        RunState state,
        IReadOnlyList<RewardOption> rewards,
        Catalog catalog,
        RunPolicyOptions options)
    {
        var lineup = ChooseStarters(state, options);
        var placement = PlacementOf(lineup);
        int naked = BestStarterWithoutItem(state, lineup, options);
        var best = new RewardChoice(-1, -1, 0);

        for (int i = 0; i < rewards.Count; i++)
        {
            int score, carrier = -1;
            switch (rewards[i])
            {
                case PerkRewardOption perk:
                    var definition = catalog.Perks.Find(perk.PerkId);
                    var carriers = definition is null
                        ? Array.Empty<int>()
                        : PerkPool.EligibleCarriers(state, definition, catalog);
                    carrier = definition is null
                        ? -1
                        : BestCarrier(state, definition, carriers, lineup, placement, options);
                    score = carrier >= 0 ? 3 : (carriers.Count > 0 ? 2 : 0);
                    if (score == 2)
                    {
                        carrier = carriers[0];
                    }

                    break;

                case ItemRewardOption:
                    carrier = naked;
                    score = naked >= 0 ? 3 : 1;
                    if (score == 1 && lineup.Count > 0)
                    {
                        carrier = lineup[0].Id;
                    }

                    break;

                case PlayerRewardOption:
                    score = state.Roster.Count < options.RosterCap ? 2 : 1;
                    break;

                default:
                    score = 0;
                    break;
            }

            if (score > best.Score)
            {
                best = new RewardChoice(i, carrier, score);
            }
        }

        return best;
    }

    // ------------------------------------------------------------------ ayudantes

    /// <summary>Oro que la política se permite gastar: reserva la clínica mientras haya un lesionado grave.</summary>
    private static int Spendable(RunState state, EconomyConfig economy) =>
        HasUntreatedSevereInjury(state) ? Math.Max(0, state.Gold - economy.ClinicCost) : state.Gold;

    private static bool HasUntreatedSevereInjury(RunState state)
    {
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState == PhysicalState.SevereInjury)
            {
                return true;
            }
        }

        return false;
    }

    private static RunPlayer? BestSevereInjured(RunState state, RunPolicyOptions options)
    {
        RunPlayer? best = null;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.PhysicalState != PhysicalState.SevereInjury)
            {
                continue;
            }

            if (best is null || Value(player, options) > Value(best, options))
            {
                best = player;
            }
        }

        return best;
    }

    /// <summary>
    /// A quién se le da el perk. Las dos doctrinas que piensan se lo dan al titular elegible con
    /// <b>menos perks</b> (y, a igualdad, al de más valor): repartir es lo que hace que
    /// <c>death_mark</c>, el modificador del jefe del acto 2, no se lleve media build por delante, y es
    /// además cómo están construidos los cuatro escalones de <c>data/balance/builds/</c>. La gastadora
    /// no distingue titular de suplente: se lo da al elegible de menor id, que es "el primero que
    /// mejora a alguien".
    /// </summary>
    private static int BestCarrier(
        RunState state,
        Perks.PerkDefinition perk,
        IReadOnlyList<int> carriers,
        IReadOnlyList<RunPlayer> lineup,
        Lineup? placement,
        RunPolicyOptions options)
    {
        if (options.Doctrine == PurchaseDoctrine.Spender)
        {
            return carriers.Count > 0 ? carriers[0] : -1;
        }

        int best = -1, bestPerks = int.MaxValue, bestValue = int.MinValue;
        for (int i = 0; i < carriers.Count; i++)
        {
            if (!Contains(lineup, carriers[i]))
            {
                continue;
            }

            // Regla de lectura (PerkPlacement): un perk cuya condición de colocación no se cumple en ese
            // portador ocupa un slot y, si castiga, resta. Es la diferencia entre el escalón "correcta"
            // y el "incoherente" de la ADR 0033, y es lo que un jugador ve en la descripción.
            if (placement is not null && !PerkPlacement.Fits(perk, carriers[i], placement, state))
            {
                continue;
            }

            var player = state.GetPlayer(carriers[i]);
            int perks = player.Perks.Count;
            int value = Value(player, options);
            if (perks < bestPerks || (perks == bestPerks && value > bestValue))
            {
                best = carriers[i];
                bestPerks = perks;
                bestValue = value;
            }
        }

        return best;
    }

    /// <summary>Colocación del once elegido, o null si no hay once que colocar (menos de cinco disponibles).</summary>
    private static Lineup? PlacementOf(IReadOnlyList<RunPlayer> lineup) =>
        lineup.Count is >= RunRules.MinimumAvailablePlayers and <= RunRules.MaxStarters
            ? RunLineup.Compose(lineup)
            : null;

    /// <summary>Suplente disponible de menos valor que la política se permite vender (RF-114f).</summary>
    private static RunPlayer? WorstSellable(RunState state, IReadOnlyList<RunPlayer> lineup, RunPolicyOptions options)
    {
        RunPlayer? worst = null;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (!player.IsAvailable || player.IsMercenary || player.IsYouth || Contains(lineup, player.Id))
            {
                continue;
            }

            if (worst is null || Value(player, options) < Value(worst, options))
            {
                worst = player;
            }
        }

        return worst;
    }

    private static int AttributeSum(RunPlayer player)
    {
        var a = player.Attributes;
        return a.Strength + a.Speed + a.Technique + a.Stamina + a.Leash;
    }

    private static int WeakestStarterAttributes(IReadOnlyList<RunPlayer> lineup)
    {
        int worst = int.MaxValue;
        for (int i = 0; i < lineup.Count; i++)
        {
            int attributes = AttributeSum(lineup[i]);
            if (attributes < worst)
            {
                worst = attributes;
            }
        }

        return worst == int.MaxValue ? 0 : worst;
    }

    /// <summary>
    /// Quién recibe el objeto: el titular sin objeto de más valor. La gastadora vuelve a no distinguir y
    /// equipa a cualquiera de la plantilla que no lleve nada, titular o no (RF-076: un objeto por
    /// jugador).
    /// </summary>
    private static int BestStarterWithoutItem(
        RunState state,
        IReadOnlyList<RunPlayer> lineup,
        RunPolicyOptions options)
    {
        var pool = options.Doctrine == PurchaseDoctrine.Spender ? state.Roster : lineup;
        int best = -1, bestValue = int.MinValue;
        for (int i = 0; i < pool.Count; i++)
        {
            var player = pool[i];
            if (player.Item is not null || player.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            int value = options.Doctrine == PurchaseDoctrine.Spender ? -player.Id : Value(player, options);
            if (value > bestValue)
            {
                best = player.Id;
                bestValue = value;
            }
        }

        return best;
    }

    private static void TakeBest(
        List<RunPlayer> starters,
        IReadOnlyList<RunPlayer> pool,
        Position position,
        int count,
        RunPolicyOptions options)
    {
        var candidates = new List<RunPlayer>();
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Position == position && !Contains(starters, pool[i].Id))
            {
                candidates.Add(pool[i]);
            }
        }

        SortByValue(candidates, options);
        for (int i = 0; i < candidates.Count && i < count && starters.Count < RunRules.MaxStarters; i++)
        {
            starters.Add(candidates[i]);
        }
    }

    /// <summary>Ordena por valor descendente y, a igualdad, por id ascendente (RT-041: nada de empates al azar).</summary>
    private static void SortByValue(List<RunPlayer> players, RunPolicyOptions options) =>
        players.Sort((a, b) =>
        {
            int byValue = Value(b, options).CompareTo(Value(a, options));
            return byValue != 0 ? byValue : a.Id.CompareTo(b.Id);
        });

    private static bool Contains(IReadOnlyList<RunPlayer> players, int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private static int WagesDue(RunState state)
    {
        int total = 0;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.IsMercenary && player.PhysicalState != PhysicalState.Dead)
            {
                total += player.Wage;
            }
        }

        return total;
    }

    private static int CountState(RunState state, PhysicalState physical)
    {
        int count = 0;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState == physical)
            {
                count++;
            }
        }

        return count;
    }

    private static NodeResult LastResultAt(RunState state, int nodeId)
    {
        for (int i = state.NodeHistory.Count - 1; i >= 0; i--)
        {
            if (state.NodeHistory[i].NodeId == nodeId)
            {
                return state.NodeHistory[i].Result;
            }
        }

        return NodeResult.Completed;
    }

    private static RunPlayResult Summarize(
        RunState state,
        RunSetup setup,
        ulong seed,
        RunPolicyOptions options,
        Ledger ledger)
    {
        var outcome = RunEngine.Outcome(state);
        var lineup = state.AvailablePlayerCount >= RunRules.MinimumAvailablePlayers
            ? ChooseStarters(state, options)
            : Array.Empty<RunPlayer>();

        int levels = 0, perks = 0, starterPerks = 0, items = 0, injuries = 0, counters = 0;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            levels += player.Level;
            perks += player.Perks.Count;
            if (Contains(lineup, player.Id))
            {
                starterPerks += player.Perks.Count;
            }

            if (player.Item is not null)
            {
                items++;
            }

            if (player.PhysicalState == PhysicalState.MinorInjury)
            {
                injuries++;
            }

            foreach (var (_, value) in player.Counters)
            {
                counters += value;
            }
        }

        return new RunPlayResult(
            seed,
            options.Doctrine,
            setup.ClubRace,
            outcome.Kind,
            outcome.Cause,
            state.Act,
            ledger.Matches,
            ledger.MatchesWon,
            ledger.BossesBeaten,
            ledger.GoldEarned,
            ledger.GoldFromSales,
            ledger.GoldSpentMarket,
            ledger.GoldSpentClinic,
            ledger.GoldSpentReroll,
            ledger.GoldSpentWages,
            state.Gold,
            ledger.Deaths,
            injuries,
            ledger.OwnInjuries,
            ledger.SevereInjuries,
            state.Roster.Count,
            state.AvailablePlayerCount,
            state.Roster.Count > 0 ? levels * 100 / state.Roster.Count : 0,
            perks,
            starterPerks,
            items,
            counters,
            ledger.MarketsVisited,
            ledger.OffersSeen,
            ledger.OffersAffordable,
            ledger.GoldAtMarketArrival,
            ledger.BrokeMarketVisits,
            ledger.Purchases,
            ledger.PerksBought,
            ledger.ItemsBought,
            ledger.PlayersSigned,
            ledger.YouthsSigned,
            ledger.MercenariesHired,
            ledger.PlayersSold,
            ledger.Treatments,
            ledger.Rerolls,
            ledger.MatchesByAct,
            ledger.WinsByAct,
            ledger.MarketsByAct,
            ledger.GoldEarnedByAct);
    }

    /// <summary>
    /// Envoltorio de <see cref="IRunSystems"/> que solo <b>mira</b>: apunta en el libro mayor las
    /// lesiones y muertes propias que el resumen del partido ya trae (<see cref="RunMatchSummary"/>) y
    /// reenvía todo lo demás. No cambia ninguna decisión: sin él habría que deducir las lesiones del
    /// estado, y las leves se borran al jugar (W-10). Su límite: <c>AfterMatch</c> no se llama en el
    /// partido que termina la run, así que ese último partido no cuenta sus lesiones.
    /// </summary>
    private sealed class RecordingSystems : IRunSystems
    {
        private readonly IRunSystems _inner;
        private readonly Ledger _ledger;

        public RecordingSystems(IRunSystems inner, Ledger ledger)
        {
            _inner = inner;
            _ledger = ledger;
        }

        public RunState AfterMatch(RunState state, MapNode node, RunMatchSummary summary, Catalog catalog)
        {
            _ledger.OwnInjuries += summary.OwnInjuries;
            return _inner.AfterMatch(state, node, summary, catalog);
        }

        public IReadOnlyList<RunReferee> CreateReferees(ulong seed, int count, Catalog catalog) =>
            _inner.CreateReferees(seed, count, catalog);

        public TeamSetup OpponentFor(RunState state, MapNode node, Catalog catalog) =>
            _inner.OpponentFor(state, node, catalog);

        public RefereeSetup RefereeFor(RunState state, MapNode node, Catalog catalog) =>
            _inner.RefereeFor(state, node, catalog);

        public Underleague.Sim.Engine.SimConfig MatchConfig(RunState state, MapNode node) =>
            _inner.MatchConfig(state, node);

        public RunState OpenNode(RunState state, MapNode node, Catalog catalog) =>
            _inner.OpenNode(state, node, catalog);

        public RunState ApplyDecision(RunState state, RunDecision decision, Catalog catalog) =>
            _inner.ApplyDecision(state, decision, catalog);

        public IReadOnlyList<string> BossRuleModifiers(RunState state, MapNode node, Catalog catalog) =>
            _inner.BossRuleModifiers(state, node, catalog);

        public MatchSetup TransformMatch(
            RunState state, MapNode node, MatchSetup setup, int playerTeamIndex, Catalog catalog) =>
            _inner.TransformMatch(state, node, setup, playerTeamIndex, catalog);
    }

    /// <summary>Contabilidad de una run mientras se juega. Mutable a propósito y estrictamente local.</summary>
    private sealed class Ledger
    {
        public int OwnInjuries;

        public int Matches;
        public int MatchesWon;
        public int BossesBeaten;
        public int GoldEarned;
        public int GoldFromSales;
        public int GoldSpentMarket;
        public int GoldSpentClinic;
        public int GoldSpentReroll;
        public int GoldSpentWages;
        public int Deaths;
        public int SevereInjuries;
        public int MarketsVisited;
        public int OffersSeen;
        public int GoldAtMarketArrival;
        public int OffersAffordable;
        public int BrokeMarketVisits;
        public int Purchases;
        public int PerksBought;
        public int ItemsBought;
        public int PlayersSigned;
        public int YouthsSigned;
        public int MercenariesHired;
        public int PlayersSold;
        public int Treatments;
        public int Rerolls;

        public int[] MatchesByAct { get; } = new int[RunRules.Acts];

        public int[] WinsByAct { get; } = new int[RunRules.Acts];

        public int[] MarketsByAct { get; } = new int[RunRules.Acts];

        public int[] GoldEarnedByAct { get; } = new int[RunRules.Acts];
    }
}
