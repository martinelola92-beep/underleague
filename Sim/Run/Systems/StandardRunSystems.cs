using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Equipment;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Run.Systems.Medical;
using Underleague.Sim.Run.Systems.Mercenaries;
using Underleague.Sim.Run.Systems.Nodes;
using Underleague.Sim.Run.Systems.Rewards;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Implementación de <see cref="IRunSystems"/> del paquete X: economía, mercado, recompensas, lesiones y
/// equipamiento (fase2-diseno.md §12). Sustituye a <see cref="DefaultRunSystems"/> sin tocar
/// <see cref="RunEngine"/> ni ningún fichero de <c>Sim/Run</c> fuera de <c>Sim/Run/Systems</c>.
///
/// <para>Lo que sigue siendo del paquete W o queda para el Y se delega en
/// <see cref="DefaultRunSystems.Instance"/>: árbitros (neutros hasta el paquete Y, W-16), configuración
/// del simulador, y el rival de un nodo de jefe (calibrado contra la ADR 0033 por el paquete Y; ver
/// <see cref="OpponentFor"/>). <see cref="BossRuleModifiers"/> devuelve la lista vacía por la misma
/// razón.</para>
/// </summary>
public sealed class StandardRunSystems : IRunSystems
{
    private readonly EconomyConfig _economy;
    private readonly ItemCatalog _items;
    private readonly ConsumableCatalog _consumables;
    private readonly RivalCatalog _rivals;

    public StandardRunSystems(EconomyConfig economy, ItemCatalog items, ConsumableCatalog consumables, RivalCatalog rivals)
    {
        _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _consumables = consumables ?? throw new ArgumentNullException(nameof(consumables));
        _rivals = rivals ?? throw new ArgumentNullException(nameof(rivals));
    }

    /// <summary>Configuración de economía de esta instancia (para tests y <c>/Balance</c>).</summary>
    public EconomyConfig Economy => _economy;

    /// <summary>Catálogo de objetos de esta instancia.</summary>
    public ItemCatalog Items => _items;

    /// <summary>Catálogo de consumibles de esta instancia.</summary>
    public ConsumableCatalog Consumables => _consumables;

    /// <summary>Catálogo de rivales de esta instancia.</summary>
    public RivalCatalog Rivals => _rivals;

    /// <summary>
    /// Construye los cuatro catálogos del paquete X de una instantánea de <c>/data</c> (el mismo
    /// diccionario que consume <c>DataLoader.FromJson</c>). Ayudante de conveniencia para tests y
    /// <c>/Balance</c>: evita llamar a los cuatro cargadores por separado.
    /// </summary>
    public static StandardRunSystems FromJson(IReadOnlyDictionary<string, string> files) => new(
        EconomyLoader.FromJson(files),
        ItemLoader.FromJson(files),
        ConsumableLoader.FromJson(files),
        RivalLoader.FromJson(files));

    /// <summary>Rivales estáticos de esta instancia, por acto (1..3), para <c>RunSetup.OpponentIdsByAct</c> (RF-015).</summary>
    public IReadOnlyList<IReadOnlyList<string>> OpponentIdsByAct() =>
        new[] { _rivals.OfAct(1), _rivals.OfAct(2), _rivals.OfAct(3) };

    /// <inheritdoc />
    public IReadOnlyList<RunReferee> CreateReferees(ulong seed, int count, Catalog catalog) =>
        DefaultRunSystems.Instance.CreateReferees(seed, count, catalog);

    /// <inheritdoc />
    public TeamSetup OpponentFor(RunState state, MapNode node, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(node);

        // El jefe lo calibra el paquete Y contra la tabla de la ADR 0033 (data/bosses/): mientras no
        // exista, el mismo procedural de DefaultRunSystems, que ya sube la calidad para un jefe
        // (BossQualityBonus). Es un rival estático más (data/rivals/) para liga y élite.
        if (node.Kind != NodeKind.Boss && node.OpponentId.Length > 0)
        {
            var team = _rivals.Find(node.OpponentId);
            if (team is not null)
            {
                return RivalTeamBuilder.Build(team, catalog);
            }
        }

        return DefaultRunSystems.Instance.OpponentFor(state, node, catalog);
    }

    /// <inheritdoc />
    public RefereeSetup RefereeFor(RunState state, MapNode node, Catalog catalog) =>
        DefaultRunSystems.Instance.RefereeFor(state, node, catalog);

    /// <inheritdoc />
    public SimConfig MatchConfig(RunState state, MapNode node) => DefaultRunSystems.Instance.MatchConfig(state, node);

    /// <inheritdoc />
    public RunState OpenNode(RunState state, MapNode node, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(catalog);

        return node.Kind switch
        {
            NodeKind.Market => state.WithPendingNode(node.Id),
            NodeKind.Clinic => state.WithPendingNode(node.Id),
            NodeKind.Training => ServiceNodeSystem.Training(state, _economy, catalog),
            NodeKind.Event => ServiceNodeSystem.Event(state, node, _economy),
            _ => state,
        };
    }

    /// <inheritdoc />
    public RunState AfterMatch(RunState state, MapNode node, RunMatchSummary summary, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(summary);

        // Salario, abandono de mercenarios y desgaste de objetos frágiles: sea cual sea el resultado
        // (RF-111 y RF-077 no distinguen victoria de derrota).
        state = EquipmentSystem.ProcessFragileItems(state, summary, _items);
        state = MercenarySystem.Process(state, summary, _economy);

        if (!summary.Won)
        {
            // Perder no paga (RF-114g) y no hay recompensa que elegir.
            return state;
        }

        int gold = GoldCalculator.GoldForWin(state, node, summary, _economy);
        state = state.AddGold(gold);

        // Deja el nodo abierto para RF-071: el jugador elige recompensa (y puede repetir tirada una vez,
        // RF-071b) antes de volver al mapa con LeaveNode.
        return state.WithPendingNode(node.Id);
    }

    /// <inheritdoc />
    public RunState ApplyDecision(RunState state, RunDecision decision, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(catalog);

        return decision switch
        {
            BuyOffer buy => MarketSystem.Buy(state, buy, catalog, _economy, _items, _consumables),
            SellPlayer sell => MarketSystem.Sell(state, sell, _economy),
            HireMercenary hire => MarketSystem.Hire(state, hire, catalog, _economy, _items, _consumables),
            TreatPlayer treat => MedicalSystem.Treat(state, treat, _economy),
            ChooseReward choose => RewardSystem.Choose(state, choose, catalog, _economy, _items),
            RerollRewards => RewardSystem.Reroll(state, _economy),
            TransferItem transfer => EquipmentSystem.Apply(state, transfer, _economy, _items),
            _ => throw new NotSupportedException(
                $"la decisión {decision.GetType().Name} no la resuelve el paquete X: la resuelve el paquete Y (jefe) "
                    + "sustituyendo StandardRunSystems por su propia implementación de IRunSystems, o componiendo las dos."),
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<string> BossRuleModifiers(RunState state, MapNode node, Catalog catalog) =>
        Array.Empty<string>();
}
