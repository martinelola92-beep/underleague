using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Clubs;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Equipment;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Map;
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
    private readonly MapConfig _map;
    private readonly ClubCatalog _clubs;

    public StandardRunSystems(EconomyConfig economy, ItemCatalog items, ConsumableCatalog consumables, RivalCatalog rivals, MapConfig map, ClubCatalog clubs)
    {
        _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _consumables = consumables ?? throw new ArgumentNullException(nameof(consumables));
        _rivals = rivals ?? throw new ArgumentNullException(nameof(rivals));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _clubs = clubs ?? throw new ArgumentNullException(nameof(clubs));
    }

    /// <summary>Configuración de economía de esta instancia (para tests y <c>/Balance</c>).</summary>
    public EconomyConfig Economy => _economy;

    /// <summary>Catálogo de objetos de esta instancia.</summary>
    public ItemCatalog Items => _items;

    /// <summary>Catálogo de consumibles de esta instancia.</summary>
    public ConsumableCatalog Consumables => _consumables;

    /// <summary>Catálogo de rivales de esta instancia.</summary>
    public RivalCatalog Rivals => _rivals;

    /// <summary>Estructura del mapa de esta instancia (D-2/D-10, <c>data/map/map.json</c>).</summary>
    public MapConfig Map => _map;

    /// <summary>Catálogo de clubes iniciales de esta instancia (RF-004, <c>data/clubs/</c>).</summary>
    public ClubCatalog Clubs => _clubs;

    /// <summary>
    /// Construye los cinco catálogos del paquete X de una instantánea de <c>/data</c> (el mismo
    /// diccionario que consume <c>DataLoader.FromJson</c>). Ayudante de conveniencia para tests y
    /// <c>/Balance</c>: evita llamar a los cinco cargadores por separado.
    /// </summary>
    public static StandardRunSystems FromJson(IReadOnlyDictionary<string, string> files) => new(
        EconomyLoader.FromJson(files),
        ItemLoader.FromJson(files),
        ConsumableLoader.FromJson(files),
        RivalLoader.FromJson(files),
        MapLoader.FromJson(files),
        ClubLoader.FromJson(files));

    /// <summary>
    /// <see cref="RunSetup"/> completo para empezar una run con <b>estos</b> datos: oro de partida
    /// (<c>economy.startingGold</c>), nodos por acto (<c>map.nodesPerAct</c>) y rivales estáticos por acto
    /// (<c>data/rivals/</c>).
    ///
    /// <para>Existe porque <c>RunSetup.StartingGold</c> es un <c>init</c> que vale <b>0</b> si nadie lo
    /// rellena: quien empezaba una run sin acordarse llegaba al primer mercado sin un solo oro y no podía
    /// comprar nada. El valor sigue saliendo de <c>economy.startingGoldByDivision</c>, no de
    /// <c>data/clubs/</c> (que solo lo enseña, informativo, en la pantalla de selección de club: ver
    /// <see cref="Clubs.ClubDefinition"/>), para no acoplar el arranque de la run a un dato que hoy no
    /// varía entre clubes.</para>
    /// </summary>
    /// <param name="clubId">Id del club (RF-004).</param>
    /// <param name="race">Raza del club (RF-004).</param>
    /// <param name="files">Instantánea de <c>/data</c> con la que se juega la run (RT-061b).</param>
    /// <param name="division">División en la que se juega (RF-128): de ella sale el oro de partida (ADR 0044).</param>
    public RunSetup NewRunSetup(
        string clubId,
        Model.Race race,
        IReadOnlyDictionary<string, string> files,
        Division division = Division.Third) =>
        new(clubId, race, files)
        {
            Division = division,
            StartingGold = _economy.StartingGoldFor(division),
            NodesPerActByAct = _map.NodesPerAct,
            OpponentIdsByAct = OpponentIdsByAct(),
        };

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
                var built = RivalTeamBuilder.Build(team, catalog);
                return node.Kind == NodeKind.EliteMatch
                    ? LevelUp(built, _map.EliteRivalLevelBonus, catalog)
                    : built;
            }
        }

        return DefaultRunSystems.Instance.OpponentFor(state, node, catalog);
    }

    /// <inheritdoc />
    public RefereeSetup RefereeFor(RunState state, MapNode node, Catalog catalog) =>
        DefaultRunSystems.Instance.RefereeFor(state, node, catalog);

    /// <inheritdoc />
    /// <remarks>
    /// Aquí entra el <b>desgaste creciente por acto</b> de la ADR 0043: la probabilidad de lesión se
    /// multiplica por <c>tuning.injury.actScalePercent</c> del acto y, en un nodo de élite, además por
    /// <c>eliteScalePercent</c> (más riesgo, que es la mitad de lo que le da su función al nodo). Es un
    /// dato, no una fórmula: el motor recibe el multiplicador ya calculado y su fórmula no cambia.
    /// </remarks>
    public SimConfig MatchConfig(RunState state, MapNode node, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(catalog);

        var injury = catalog.Tuning.Injury;
        int scale = injury.ScaleForAct(node.Act);
        if (node.Kind == NodeKind.EliteMatch)
        {
            scale = scale * injury.EliteScalePercent / 100;
        }

        return DefaultRunSystems.Instance.MatchConfig(state, node, catalog) with { InjuryScalePercent = scale };
    }

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
            NodeKind.Enrollment => state.WithPendingNode(node.Id),
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
            // Perder no paga (RF-114g) y no hay recompensa que elegir. Aquí solo llegan las derrotas
            // ORDINARIAS: la derrota contra el jefe termina la run antes de AfterMatch (RunEngine).
            int penalty = _economy.DefeatGoldPenalty
                + (state.Gold * _economy.DefeatGoldPenaltyPercent / 100);
            return penalty > 0 ? state.AddGold(-penalty) : state;
        }

        int gold = GoldCalculator.GoldForWin(state, node, summary, _economy);
        state = state.AddGold(gold);

        // ADR 0043: superar el jefe cura la plantilla. Es lo que cierra el ciclo de desgaste del acto —se
        // puede exprimir la plantilla sabiendo que habrá alivio, en vez de administrar una ruina uniforme
        // durante toda la run— y la otra mitad del trampolín, junto a los dos perks. El muerto no vuelve
        // (RF-093).
        var reward = _economy.RewardFor(node.Kind);
        if (reward.HealsRoster)
        {
            state = HealRoster(state);
        }

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
            ExpandRoster => EnrollmentSystem.Expand(state, _economy),
            ReleasePlayer release => EnrollmentSystem.Release(state, release),
            ChooseReward choose => RewardSystem.Choose(state, choose, catalog, _economy, _items),
            DeclineReward => RewardSystem.Decline(state, _economy),
            RerollRewards => RewardSystem.Reroll(state, _economy),
            TransferItem transfer => EquipmentSystem.Apply(state, transfer, _economy, _items),
            EquipStoredItem stored => EquipmentSystem.Apply(state, stored, _economy, _items),
            _ => throw new NotSupportedException(
                $"la decisión {decision.GetType().Name} no la resuelve el paquete X: la resuelve el paquete Y (jefe) "
                    + "sustituyendo StandardRunSystems por su propia implementación de IRunSystems, o componiendo las dos."),
        };
    }

    /// <summary>
    /// El rival de un nodo de élite es el del acto <b>subido de nivel</b> (ADR 0043): más riesgo, que es
    /// la mitad de lo que le da su función al nodo. Se sube con <c>Progression.LevelUp</c>, la misma
    /// escalera de RF-027 por la que sube la plantilla del jugador, en vez de inventar una dificultad
    /// aparte.
    /// </summary>
    private static TeamSetup LevelUp(TeamSetup team, int levels, Catalog catalog)
    {
        if (levels <= 0)
        {
            return team;
        }

        var players = new List<PlayerDefinition>(team.Players.Count);
        for (int i = 0; i < team.Players.Count; i++)
        {
            players.Add(Progression.Progression.LevelUp(
                team.Players[i], team.Players[i].Level + levels, catalog.Tuning.Progression));
        }

        return team with { Players = players };
    }

    /// <summary>
    /// Cura la plantilla entera (ADR 0043): la lesión grave y las leves acumuladas desaparecen, el muerto
    /// no vuelve (RF-093). Recorre el roster por id ascendente, que es como <c>RunState.WithPlayer</c> lo
    /// mantiene ordenado (RT-041).
    /// </summary>
    private static RunState HealRoster(RunState state)
    {
        var next = state;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            if (player.PhysicalState != PhysicalState.Healthy || player.MinorInjuries > 0)
            {
                next = next.WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
            }
        }

        return next;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> BossRuleModifiers(RunState state, MapNode node, Catalog catalog) =>
        Array.Empty<string>();

    /// <inheritdoc />
    /// <remarks>
    /// Lo único que transforma el paquete X es el <b>nombre</b> del equipo del jugador (RF-004): el resto
    /// del partido queda igual, los objetos equipados ya viajan dentro del <c>PlayerDefinition</c> que
    /// arma <c>RunLineup</c>, y los modificadores de regla son del jefe (paquete Y), que envuelve a esta
    /// clase con <c>BossRunSystems</c>.
    ///
    /// <para><c>RunEngine.BuildMatch</c> etiqueta el equipo propio con <c>state.ClubId</c>, que es un id
    /// de datos (<c>"underleague_fc"</c>, no un nombre): sin este paso, ese id llegaba tal cual al
    /// marcador del partido, al log de eventos (RF-121) y al informe post-partido (RF-119). Si el id
    /// resuelve a un club conocido, se sustituye por su nombre; si no (equipos de prueba, <c>/Balance</c>,
    /// que no usan <c>data/clubs/</c>), se deja como estaba, sin lanzar.</para>
    /// </remarks>
    public MatchSetup TransformMatch(RunState state, MapNode node, MatchSetup setup, int playerTeamIndex, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(setup);

        var club = _clubs.Find(state.ClubId);
        if (club is null)
        {
            return setup;
        }

        var own = playerTeamIndex == 0 ? setup.Home : setup.Away;
        if (own is null || string.Equals(own.Name, club.Name.Es, StringComparison.Ordinal))
        {
            return setup;
        }

        var renamed = own with { Name = club.Name.Es };
        return playerTeamIndex == 0 ? setup with { Home = renamed } : setup with { Away = renamed };
    }
}
