using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;
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

    /// <summary>
    /// Valor mínimo de un lesionado grave para que compense tratarlo aunque sobren cuerpos (ADR 0041).
    /// El valor de un jugador es la suma de sus cinco atributos más lo que aportan perks y objeto, así
    /// que 250 es un titular corriente de nivel 1 y cualquier titular de la mitad de la run lo supera.
    /// </summary>
    public int TreatFromValue { get; init; } = 250;

    /// <summary>Acepta un partido de élite solo con al menos estos disponibles.</summary>
    public int EliteFromAvailable { get; init; } = 8;

    /// <summary>
    /// El tope de plantilla ya no es un umbral de la política: lo fija el estado (RF-020, ADR 0046),
    /// <c>RunState.RosterCapacity</c>, base 10 y hasta 12 con huecos de inscripción comprados. La opción
    /// desapareció a propósito: cuando el tope era de la política, subirlo era gratis y el desgaste no
    /// mordía (ADR 0045).
    /// </summary>
    public int EnrollFromAct { get; init; } = 1;

    /// <summary>Ficha de pago aunque no mejore al once si los disponibles bajan de este número.</summary>
    public int SignWhileAvailableBelow { get; init; } = 8;

    /// <summary>Ficha un mercenario solo si los disponibles están por debajo de este número (D-3).</summary>
    public int HireMercenaryWhileAvailableBelow { get; init; } = 6;

    /// <summary>
    /// Nunca vende si con ello los disponibles bajan de este número. Baja de 8 a 7 con la plantilla
    /// corta (RF-020, ADR 0046): con una base de diez, exigir nueve disponibles para vender dejaba a la
    /// política sin poder hacer sitio nunca, y "vender o descartar para fichar" es justo la decisión que
    /// la plantilla corta pone sobre la mesa.
    /// </summary>
    public int SellKeepingAvailable { get; init; } = 7;

    /// <summary>Cuánto vale un perk en puntos de atributo al valorar a un jugador.</summary>
    public int PerkWorthInAttributePoints { get; init; } = 10;

    /// <summary>Cuánto vale un objeto equipado en puntos de atributo al valorar a un jugador.</summary>
    public int ItemWorthInAttributePoints { get; init; } = 8;

    /// <summary>
    /// Valor medido mínimo (ADR 0038, <c>data/economy/perk-values.json</c>, en milésimas de punto de tasa
    /// de victoria) que la doctrina <b>contextual</b> le exige a un perk para gastarse un slot en él, sea
    /// comprándolo o cogiéndolo de recompensa. Cero: un perk cuyo valor medido es negativo empeora al
    /// equipo que lo lleva, y el slot es irreversible (RF-072, RF-023). Es la diferencia entre "comprar lo
    /// que me falta" y "comprar lo que hay", y no la aplican las dos doctrinas puras: la gastadora coge lo
    /// primero que puede pagar y la ahorradora mira la rareza, que no es lo mismo que el valor.
    /// </summary>
    public int MinPerkValue { get; init; }

    /// <summary>
    /// Si la política <b>lee el informe de ojeo</b> (RF-013) antes de alinear: con un rival que lleva
    /// perks letales, deja en el banquillo a los tocados mientras le queden siete sanos (ADR 0046).
    /// Existe como interruptor para poder medir <b>las dos</b> cifras —lo que muere quien lee el informe
    /// y lo que muere quien no— porque es la diferencia entre una muerte injusta y una decisión.
    /// </summary>
    public bool HeedsLethalScouting { get; init; } = true;

    /// <summary>
    /// Cuánto vale un jugador muerto, en porcentaje de su valor de partido (ADR 0048). Es lo que
    /// convierte el indicador de riesgo (RF-012c) en una decisión de alineación: la política puntúa cada
    /// candidato en cada casilla con <c>valor × (1 − coste × riesgo)</c>, así que un titular mejor pero
    /// más expuesto puede perder el sitio frente a uno peor y más duro. 400 = perderlo cuesta cuatro
    /// partidos suyos, que es el orden de magnitud de una baja definitiva a mitad de run.
    ///
    /// <para>Es un dial de la <b>política</b>, no del juego: no vive en <c>/data</c> porque no describe
    /// ninguna regla, describe cómo de prudente es el jugador simulado. Vale 0 cuando
    /// <see cref="HeedsLethalScouting"/> es false, que es la política de control con la que se mide si
    /// atender al indicador sirve de algo.</para>
    /// </summary>
    public int DeathCostPercent { get; init; } = 150;

    /// <summary>
    /// Si la política <b>persigue un maestro</b> (ADR 0051): elige la línea que más lejos tiene construida
    /// y, a igualdad de todo lo demás, prefiere sus perks y el maestro que la corona. Es un dial de la
    /// política, no del juego: sin él, la política automática es exactamente el jugador que la ADR
    /// describe como el problema —el que acumula piezas sueltas— y la medición de "¿los arcos existen?"
    /// no diría nada, porque nadie estaría intentando cerrarlos.
    /// </summary>
    public bool PursuesMasters { get; init; } = true;

    /// <summary>
    /// Si la política <b>esquiva los mercados</b> (ADR 0055). Es la medida de control de la métrica que
    /// esa ADR pide: ganar la run sin entrar en ningún mercado tiene que ser prácticamente imposible
    /// (&lt; 5%). No es una doctrina de compra —es la misma contextual jugando igual de bien todo lo
    /// demás— y solo cambia la <b>regla 1</b>, la de elegir nodo: con el mapa de cuatro carriles (ADR
    /// 0053) el mercado se puede esquivar en el 98,9% de los actos, así que la política lo consigue casi
    /// siempre y entra solo cuando no hay otra ruta.
    /// </summary>
    public bool AvoidsMarkets { get; init; }

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
    int GoldSpentEnrollment,
    int GoldSpentReroll,
    int GoldSpentWages,
    int GoldLeft,
    int Deaths,
    int Injuries,
    int OwnInjuries,
    int MatchInjuries,
    int SevereInjuriesSuffered,

    /// <summary>Jugadores que ocupan plantilla al terminar (RF-020): los muertos ya no cuentan (ADR 0046).</summary>
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
    int SlotsBought,
    int Rerolls,
    int RewardsTaken,
    int RewardsDeclined,
    int NodesVisited,
    IReadOnlyList<int> MatchesByAct,
    IReadOnlyList<int> WinsByAct,
    IReadOnlyList<int> MarketsByAct,
    IReadOnlyList<int> GoldEarnedByAct,

    /// <summary>Muertes por acto (ADR 0048).</summary>
    IReadOnlyList<int> DeathsByAct,

    /// <summary>Perks del once al entrar en el jefe de cada acto (ADR 0049).</summary>
    IReadOnlyList<int> PerksAtBossByAct,

    /// <summary>Objetos del once al entrar en el jefe de cada acto (ADR 0049).</summary>
    IReadOnlyList<int> ItemsAtBossByAct,

    /// <summary>Jefes jugados por acto, denominador de los dos de arriba.</summary>
    IReadOnlyList<int> BossSamplesByAct,

    /// <summary>Objetos recuperados del inventario tras una muerte (ADR 0048, condición 4).</summary>
    int ItemsRecovered,

    /// <summary>
    /// Perks <b>maestros</b> que la plantilla lleva al terminar (ADR 0051), por id ordinal. Es la
    /// respuesta a "¿los arcos existen?": si nunca se cierra ninguno, están mal calibrados o piden
    /// demasiado.
    /// </summary>
    IReadOnlyList<string> Masters,

    /// <summary>
    /// Ids <b>distintos</b> de perk que la plantilla lleva al terminar (ADR 0051). Es con lo que se mide
    /// si dos runs que tomaron maestros distintos construyeron builds distintas de verdad (RF-032).
    /// </summary>
    IReadOnlyList<string> FinalPerks,

    /// <summary>Veces que un maestro estuvo en el mostrador de un mercado de esta run (ADR 0055).</summary>
    int MastersOffered,

    /// <summary>De esas, cuántas se podían cobrar y pagar (ADR 0055).</summary>
    int MastersAffordable)
{
    /// <summary>True si la run terminó ganando al jefe final (RF-002).</summary>
    public bool Won => Outcome == RunOutcomeKind.Victory;

    /// <summary>Oro gastado en los cinco sumideros vivos en fase 2 (RF-114k, ADR 0046).</summary>
    public int GoldSpent =>
        GoldSpentMarket + GoldSpentClinic + GoldSpentEnrollment + GoldSpentReroll + GoldSpentWages;

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
/// centrocampistas, 1 delantero, y el resto por valor), <b>leyendo antes el informe de ojeo</b>
/// (RF-013): si el rival lleva perks letales, los tocados se quedan fuera mientras queden siete sanos
/// (<see cref="RunPolicyOptions.HeedsLethalScouting"/>). Valor = suma de los cinco atributos +
/// <see cref="RunPolicyOptions.PerkWorthInAttributePoints"/> por perk +
/// <see cref="RunPolicyOptions.ItemWorthInAttributePoints"/> si lleva objeto.</item>
/// <item><b>Cuándo se arriesga a un lesionado grave</b> (RF-093 vía 1). Cuando no hay siete
/// disponibles, o cuando el oro no cubre su tratamiento y aun así es mejor que el suplente al que
/// sustituiría. Quien está con lesión grave ya <b>no cuenta</b> para el mínimo de RF-002b, así que
/// alinearlo no acerca la derrota por plantilla: lo que arriesga es perderlo para siempre.</item>
/// <item><b>Clínica.</b> Trata al lesionado grave de más valor mientras los disponibles estén por
/// debajo de <see cref="RunPolicyOptions.TreatWhileAvailableBelow"/> y el oro alcance.</item>
/// <item><b>Inscripción</b> (ADR 0046). Compra un hueco de plantilla mientras la plantilla esté llena
/// (RF-020) y queden huecos que vender, y <b>reserva el precio del primero</b> en el mercado igual que
/// reserva la clínica; para el segundo no ahorra. Sin la reserva el nodo es decorado —el mercado va antes
/// en el acto y se lleva el oro— y reservando para los dos la plantilla vuelve a ser ancha y el desgaste
/// deja de morder (medido en fase2-diseno.md §20.2).</item>
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

        return Summarize(state, setup, seed, catalog, options, ledger);
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
        bool wantsSlot = WantsRosterSlot(state, economy, options);

        MapNode? best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            int score = node.Kind switch
            {
                NodeKind.Clinic => needsClinic ? 100 : 20,

                // ADR 0055: el mercado es la parada más valiosa del mapa... salvo para la política de
                // control, que lo esquiva siempre (int.MinValue, no un peso bajo: con cuatro carriles hay
                // actos en los que la única ruta pasa por uno, y en ese caso entra igual porque no hay
                // alternativa, que es exactamente lo que el 98,9% de la ADR 0053 deja fuera).
                NodeKind.Market => options.AvoidsMarkets ? int.MinValue + 1 : 90,
                NodeKind.Event => poor ? 40 : 25,
                NodeKind.Training => 30,
                NodeKind.EliteMatch => strong ? 60 : 40 - node.Difficulty,
                NodeKind.LeagueMatch => 50 - node.Difficulty,
                NodeKind.Boss => 10,
                NodeKind.Enrollment => wantsSlot ? 80 : 15,
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
    public static IReadOnlyList<RunPlayer> ChooseStarters(RunState state, RunPolicyOptions options, int clinicCost) =>
        ChooseStarters(state, options, clinicCost, lethalOpponent: false);

    /// <summary>
    /// Reglas 2 y 3 <b>con el indicador de riesgo delante</b> (RF-012c, ADR 0048 condición 3). Es la
    /// versión que sabe que un jugador sano también puede morir: además de dejar fuera a los tocados,
    /// puntúa a cada candidato <b>en cada casilla</b> con <c>valor × (1 − coste × riesgo)</c> y coloca en
    /// consecuencia. El once que devuelve viene <b>en orden de colocación</b>, no por id, porque desde
    /// esta ADR la casilla es parte de la decisión: <c>RunLineup.Compose</c> reparte las casillas en el
    /// orden de la lista.
    ///
    /// <para>Con <paramref name="carriers"/> vacío se comporta exactamente como la versión sin riesgo:
    /// no hay a quién temer y colocar deja de importar.</para>
    /// </summary>
    public static IReadOnlyList<RunPlayer> ChooseStarters(
        RunState state,
        RunPolicyOptions options,
        int clinicCost,
        Catalog catalog,
        IReadOnlyList<Underleague.Sim.Perks.Lethality.LethalCarrier> carriers)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(carriers);

        var pool = LineupPool(state, options, clinicCost, carriers.Count > 0);
        // Con coste 0 no hay nada que descontar; con coste NEGATIVO el descuento se invierte y la
        // política busca el riesgo a propósito, que es como se mide el techo de la agencia (ADR 0048).
        if (carriers.Count == 0 || options.DeathCostPercent == 0)
        {
            return ByValue(pool, options);
        }

        var lethality = catalog.Tuning.Injury.Lethality;
        var starters = new List<RunPlayer>(RunRules.MaxStarters);
        var taken = new List<Cell>(RunRules.MaxStarters);

        // Mismo reparto de casillas que RunLineup.Compose, en el mismo orden, y en cada una el candidato
        // con mejor valor descontado por su riesgo EN ESA CASILLA. Aquí es donde "alejar a tu mejor
        // jugador de la banda peligrosa" deja de ser una frase y se convierte en una decisión.
        TakeSafest(starters, taken, pool, Position.Goalkeeper, 1, options, catalog, lethality, carriers);
        TakeSafest(starters, taken, pool, Position.Defender, 2, options, catalog, lethality, carriers);
        TakeSafest(starters, taken, pool, Position.Midfielder, 3, options, catalog, lethality, carriers);
        TakeSafest(starters, taken, pool, Position.Forward, 1, options, catalog, lethality, carriers);

        while (starters.Count < RunRules.MaxStarters && taken.Count < RunRules.MaxStarters)
        {
            // Sin portero de verdad en la plantilla, Compose recoloca al de menor id en la portería y las
            // casillas se corren: puede no quedar ninguna libre con el once aún incompleto. Se corta y se
            // deja el resto a Compose, que es quien manda sobre la colocación final.
            if (!RunLineup.TryCellFor(Position.Midfielder, taken, out var cell))
            {
                break;
            }

            var best = Best(pool, starters, position: null, cell, options, catalog, lethality, carriers);
            if (best is null)
            {
                break;
            }

            starters.Add(best);
            taken.Add(cell);
        }

        return starters;
    }

    /// <summary>Mejor candidato para esa casilla, o null si no queda ninguno elegible.</summary>
    private static RunPlayer? Best(
        IReadOnlyList<RunPlayer> pool,
        List<RunPlayer> starters,
        Position? position,
        Cell cell,
        RunPolicyOptions options,
        Catalog catalog,
        Underleague.Sim.Perks.LethalityTuning lethality,
        IReadOnlyList<Underleague.Sim.Perks.Lethality.LethalCarrier> carriers)
    {
        RunPlayer? best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < pool.Count; i++)
        {
            var candidate = pool[i];
            if ((position is { } required && candidate.Position != required) || Contains(starters, candidate.Id))
            {
                continue;
            }

            int score = RiskAdjustedValue(candidate, cell, options, catalog, lethality, carriers);

            // Empates por id ascendente (RT-041): nunca al azar.
            if (best is null || score > bestScore || (score == bestScore && candidate.Id < best.Id))
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static void TakeSafest(
        List<RunPlayer> starters,
        List<Cell> taken,
        IReadOnlyList<RunPlayer> pool,
        Position position,
        int count,
        RunPolicyOptions options,
        Catalog catalog,
        Underleague.Sim.Perks.LethalityTuning lethality,
        IReadOnlyList<Underleague.Sim.Perks.Lethality.LethalCarrier> carriers)
    {
        for (int c = 0; c < count && starters.Count < RunRules.MaxStarters; c++)
        {
            Cell cell;
            if (position == Position.Goalkeeper)
            {
                cell = RunLineup.GoalkeeperCell;
            }
            else if (!RunLineup.TryCellFor(position, taken, out cell))
            {
                return;
            }

            var best = Best(pool, starters, position, cell, options, catalog, lethality, carriers);
            if (best is null)
            {
                return;
            }

            starters.Add(best);
            taken.Add(cell);
        }
    }

    /// <summary>
    /// Valor del jugador descontado por la probabilidad de perderlo en esa casilla (ADR 0048): el
    /// indicador de RF-012c convertido en un número con el que se puede comparar a dos candidatos.
    /// </summary>
    public static int RiskAdjustedValue(
        RunPlayer player,
        Cell cell,
        RunPolicyOptions options,
        Catalog catalog,
        Underleague.Sim.Perks.LethalityTuning lethality,
        IReadOnlyList<Underleague.Sim.Perks.Lethality.LethalCarrier> carriers)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);

        int value = Value(player, options);

        // Exposición, no probabilidad de morir: solo muere el marcado, pero comparar candidatos para una
        // casilla es comparar lo apetecibles que son como víctima. Bajar la exposición del once entero es
        // bajar la del que acabe marcado, que es el número que cuenta (ADR 0048).
        int risk = Underleague.Sim.Perks.Lethality.Exposure(
            lethality, carriers, player.PhysicalState, player.ToDefinition(catalog).Attributes.Stamina, cell);
        int penalty = Math.Min(10000, options.DeathCostPercent * risk / 100);
        return value * (10000 - penalty) / 10000;
    }

    /// <summary>
    /// Reglas 2 y 3 <b>leyendo el informe de ojeo</b> (RF-013), pero sin el indicador numérico. Con
    /// <paramref name="lethalOpponent"/> a true la política <b>saca del once a los tocados</b> mientras
    /// le queden siete sanos: contra un equipo que remata heridos, alinear a un herido multiplica por
    /// varias veces su probabilidad de morir (RF-093 vía 2), y está anunciado antes de confirmar la
    /// alineación (RF-012d). Es la versión de la ADR 0046; la que además <b>coloca</b> con criterio es la
    /// sobrecarga con <c>carriers</c> (ADR 0048).
    /// </summary>
    public static IReadOnlyList<RunPlayer> ChooseStarters(
        RunState state,
        RunPolicyOptions options,
        int clinicCost,
        bool lethalOpponent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);
        return ByValue(LineupPool(state, options, clinicCost, lethalOpponent), options);
    }

    /// <summary>
    /// Los candidatos a titular: los disponibles, sin los tocados si el rival mata y quedan siete sanos,
    /// más los lesionados graves que la política decide arriesgar (regla 3). Es la parte de la decisión
    /// que <b>no</b> depende de la colocación, y por eso la comparten las dos versiones de
    /// <c>ChooseStarters</c>.
    /// </summary>
    private static List<RunPlayer> LineupPool(
        RunState state, RunPolicyOptions options, int clinicCost, bool lethalOpponent)
    {
        var pool = new List<RunPlayer>(state.AvailablePlayers);
        if (lethalOpponent)
        {
            var healthyOnly = new List<RunPlayer>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].PhysicalState == PhysicalState.Healthy)
                {
                    healthyOnly.Add(pool[i]);
                }
            }

            // Solo si sale un once entero de sanos: quedarse en inferioridad para no arriesgar a un
            // tocado sería cambiar una muerte posible por una derrota segura (RF-002d).
            if (healthyOnly.Count >= RunRules.MaxStarters)
            {
                pool = healthyOnly;
            }
        }

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

        return pool;
    }

    /// <summary>El once por valor y por rol, sin mirar el riesgo: el criterio de siempre (regla 2).</summary>
    private static IReadOnlyList<RunPlayer> ByValue(List<RunPlayer> pool, RunPolicyOptions options)
    {
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
        // RF-013 y RF-012c: el informe de ojeo y el indicador de riesgo se leen ANTES de alinear. Si el
        // rival lleva un perk letal, los tocados se quedan en el banquillo mientras haya siete sanos (ADR
        // 0046) y, desde la ADR 0048 —en la que un sano también muere—, el once y la colocación se
        // deciden con el número de riesgo de cada casilla delante.
        var carriers = options.HeedsLethalScouting
            ? Underleague.Sim.Perks.Lethality.CarriersOf(systems.OpponentFor(state, node, catalog), catalog)
            : Array.Empty<Underleague.Sim.Perks.Lethality.LethalCarrier>();
        var starters = ChooseStarters(state, options, clinicCost, catalog, carriers);
        if (starters.Count >= RunRules.MinimumAvailablePlayers)
        {
            state = RunEngine.Apply(state, new SetLineup(RunLineup.Compose(starters)), catalog, systems);
        }

        int wagesDue = WagesDue(state);
        int goldBefore = state.Gold;
        int deadBefore = CountState(state, PhysicalState.Dead);
        int stockBefore = state.Counter(RunState.ItemsRecoveredCounter);
        int severeBefore = CountState(state, PhysicalState.SevereInjury);

        state = RunEngine.Enter(state, node.Id, catalog, systems);

        var outcome = RunEngine.Outcome(state);
        bool won = LastResultAt(state, node.Id) == NodeResult.Won;
        bool ranAfterMatch = outcome.Cause != DefeatCause.NotEnoughPlayers
            && !(node.Kind == NodeKind.Boss && !won);

        int wagesPaid = ranAfterMatch ? Math.Min(wagesDue, goldBefore) : 0;
        int earned = (state.Gold - goldBefore) + wagesPaid;

        ledger.Nodes++;
        ledger.Matches++;
        ledger.MatchesByAct[node.Act - 1]++;

        // ADR 0049: con qué build se llega a cada jefe. Es el dato con el que se calibra la densidad de
        // data/balance/groups.json, y bajar las opciones de recompensa lo mueve por definición.
        if (node.Kind == NodeKind.Boss)
        {
            ledger.BossSamplesByAct[node.Act - 1]++;
            for (int i = 0; i < starters.Count; i++)
            {
                ledger.PerksAtBossByAct[node.Act - 1] += starters[i].Perks.Count;
                if (starters[i].Item is not null)
                {
                    ledger.ItemsAtBossByAct[node.Act - 1]++;
                }
            }
        }
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

        int deaths = CountState(state, PhysicalState.Dead) - deadBefore;
        ledger.Deaths += deaths;
        ledger.DeathsByAct[node.Act - 1] += deaths;
        ledger.ItemsRecovered += state.Counter(RunState.ItemsRecoveredCounter) - stockBefore;
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

        ledger.Nodes++;
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
            NodeKind.Enrollment => VisitEnrollment(state, catalog, standard.Economy, systems, options, ledger),
            _ => node.IsMatch
                ? TakeRewards(state, node, catalog, standard, systems, options, ledger)
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
            if (state.Gold < economy.ClinicCost)
            {
                break;
            }

            var patient = BestSevereInjured(state, options);
            if (patient is null)
            {
                break;
            }

            // Dos razones para pagar la clínica, y la primera es la que la mantiene viva (ADR 0041): que
            // el lesionado sea una PIEZA, no que falten cuerpos. Con trece jugadores en plantilla los
            // disponibles nunca bajan de ocho, así que la regla de "solo si falta gente" convertía la
            // clínica en contenido muerto por mucho que hubiese lesiones. Un grave sin tratar es un
            // titular menos y, si se le alinea igualmente, un candidato a morir (RF-093 vía 1).
            bool worthTreating = state.AvailablePlayerCount < options.TreatWhileAvailableBelow
                || Value(patient, options) >= options.TreatFromValue;
            if (!worthTreating)
            {
                break;
            }

            state = RunEngine.Apply(state, new TreatPlayer(patient.Id), catalog, systems);
            ledger.GoldSpentClinic += economy.ClinicCost;
            ledger.Treatments++;
        }

        return state;
    }

    // ------------------------------------------------------------------ 4b. inscripción

    /// <summary>
    /// ¿Merece la pena comprar un hueco de plantilla (ADR 0046)? Solo cuando la plantilla está
    /// <b>llena</b> —si sobra sitio, el hueco no compra nada—, quedan huecos que vender, y el oro llega
    /// sin comerse el tratamiento pendiente. Es la misma forma que la regla de la clínica: una condición
    /// de necesidad y una de bolsillo, iguales en las tres doctrinas, para que la diferencia entre ellas
    /// siga siendo solo la de comprar (ADR 0037).
    /// </summary>
    private static bool WantsRosterSlot(RunState state, EconomyConfig economy, RunPolicyOptions options)
    {
        int cost = NeededEnrollmentCost(state, economy, options);
        return cost >= 0 && Spendable(state, economy) >= cost;
    }

    /// <summary>
    /// Coste del hueco que la política <b>necesita</b>, o -1 si no necesita ninguno. Necesita uno mientras
    /// la plantilla esté llena (RF-020) y queden huecos que vender: con sitio de sobra el hueco no compra
    /// nada. Es el gemelo de la reserva de la clínica —la política aparta su precio del presupuesto del
    /// mercado mientras lo necesita—, y hace falta porque el mercado va antes en el acto y si no se
    /// reserva llega al nodo de inscripción con el oro ya gastado: medido, sin reserva la política compra
    /// 0,26 huecos por run y el nodo es decorado.
    /// </summary>
    private static int NeededEnrollmentCost(RunState state, EconomyConfig economy, RunPolicyOptions options)
    {
        if (state.HasRosterSpace || state.EnrollmentSlotsLeft <= 0 || state.Act < options.EnrollFromAct)
        {
            return -1;
        }

        return economy.EnrollmentCost(state.Counter(RunState.EnrollmentSlotsCounter));
    }

    private static RunState VisitEnrollment(
        RunState state,
        Catalog catalog,
        EconomyConfig economy,
        IRunSystems systems,
        RunPolicyOptions options,
        Ledger ledger)
    {
        if (!WantsRosterSlot(state, economy, options))
        {
            return state;
        }

        int cost = economy.EnrollmentCost(state.Counter(RunState.EnrollmentSlotsCounter));
        state = RunEngine.Apply(state, new ExpandRoster(), catalog, systems);
        ledger.GoldSpentEnrollment += cost;
        ledger.SlotsBought++;
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

        // ADR 0055: el maestro solo se compra, así que cuántas veces llega a estar EN el mostrador —y
        // cuántas de esas se podía comprar de verdad— es la diferencia entre "el arco no se cierra porque
        // no aparece" y "no se cierra porque no se puede pagar". Sin las dos cifras, el 3% de arcos
        // cerrados no dice qué hay que mover.
        for (int i = 0; i < arrival.Perks.Count; i++)
        {
            if (catalog.Perks.Find(arrival.Perks[i].PerkId) is not { IsMaster: true } master)
            {
                continue;
            }

            ledger.MastersOffered++;
            if (PerkPool.Availability(state, master, catalog, PerkSource.Market) == PerkAvailability.Available
                && arrival.Perks[i].Price <= state.Gold)
            {
                ledger.MastersAffordable++;
            }
        }
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
            var decision = NextMarketAction(state, node, current, catalog, standard.Economy, standard.Items, options, used, spentHere);
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
        // ADR 0046: además de la clínica, la reserva del hueco de plantilla. La gastadora no reserva nada
        // —ni clínica ni hueco—, que es lo que la define y lo que hace que se quede sin recambios.
        int reserved = SpendableAtMarket(state, economy, options);
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

    /// <summary>
    /// ¿Merece ese perk uno de los pocos slots del once? Solo la doctrina contextual lo pregunta, y lo
    /// pregunta al <b>valor medido</b> de la ADR 0038, no a la rareza: la mitad del catálogo mide negativo
    /// —resta tasa de victoria a quien lo lleva— y el pool los ofrece <i>más</i> a menudo, porque su peso
    /// es inversamente proporcional al valor. Coger el menos malo llena un slot irreversible (RF-072) y
    /// deja fuera al perk que llegue después.
    /// </summary>
    private static bool WorthASlot(string perkId, EconomyConfig economy, RunPolicyOptions options) =>
        options.Doctrine != PurchaseDoctrine.Contextual
        || (economy.PerkValues.ValueOf(perkId) ?? 0) >= options.MinPerkValue;

    private static RunDecision? NextMarketAction(
        RunState state,
        MapNode node,
        MarketOffers offers,
        Catalog catalog,
        EconomyConfig economy,
        ItemCatalog? items,
        RunPolicyOptions options,
        HashSet<(string Category, int Index)> used,
        int alreadySpentHere)
    {
        // (a) Canteranos: gratis, así que primero y sin mirar el oro. Son además una de las tres vías de
        // recuperación que la ADR 0037 declara obligatorias para que arruinarse no sea irreversible.
        // Gratis en oro, no en plantilla: con la plantilla llena hay que hacer sitio antes (RF-020).
        if (state.HasRosterSpace)
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
        string pursuedFamily = PursuedFamily(state, catalog, options);
        int bestPerk = -1, bestPerkCarrier = -1, bestPerkRank = int.MinValue;
        for (int i = 0; i < offers.Perks.Count; i++)
        {
            if (used.Contains((MarketCategories.Perk, i)) || offers.Perks[i].Price > budget)
            {
                continue;
            }

            var perk = catalog.Perks.Find(offers.Perks[i].PerkId);
            if (perk is null || !ClearsTheBar(perk.Rarity, options) || !WorthASlot(perk.Id, economy, options))
            {
                continue;
            }

            // ADR 0051: comprar no salta el arco. Un maestro sin su línea construida no se puede cobrar,
            // así que tampoco se puja por él.
            if (PerkPool.Availability(state, perk, catalog, PerkSource.Market)
                is PerkAvailability.Unmet or PerkAvailability.Closed)
            {
                continue;
            }

            int carrier = BestCarrier(state, perk, PerkPool.EligibleCarriers(state, perk, catalog), lineup, placement, options);
            if (carrier < 0)
            {
                continue;
            }

            // La contextual ordena por VALOR MEDIDO (ADR 0038) y no por rareza: es la doctrina que sabe
            // qué le falta al once, y lo que le falta se mide en tasa de victoria, no en color del marco.
            // Las dos puras siguen con su criterio (la gastadora, lo más barato; la ahorradora, lo más
            // raro), que es justamente lo que las hace comparables.
            int rank = options.Doctrine == PurchaseDoctrine.Contextual
                ? (economy.PerkValues.ValueOf(perk.Id) ?? 0) * 10
                    + (perk.ElseEffects.Count == 0 ? 1_000_000 : 0)
                    - offers.Perks[i].Price
                : Rank(perk.Rarity, offers.Perks[i].Price, options, perk.ElseEffects.Count == 0);

            // ADR 0051: aquí es donde el mercado recupera el papel que el trampolín le quitó. Si a la run
            // le falta una pieza de su línea, o el maestro está a la venta, se paga antes que nada.
            rank += ArcPreference(perk, pursuedFamily, takeable: true) * ArcMarketWeight;
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

        // (c) Un objeto para un titular sin objeto (RF-076). Aquí es donde las tres doctrinas dejan de
        // parecerse: con la ADR 0036 un objeto es un paquete de atributos, así que la contextual compra
        // **el par (objeto, portador) que mejor encaja** y las dos puras siguen comprando por rareza y
        // precio y se lo dan a quien toque.
        int naked = BestStarterWithoutItem(state, lineup, options);
        if (naked >= 0)
        {
            int bestItem = -1, bestItemRank = int.MinValue, bestItemCarrier = naked;
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

                int rank;
                int carrier = naked;
                if (options.Doctrine == PurchaseDoctrine.Contextual && items is not null)
                {
                    var definition = items.Find(offers.Items[i].ItemId);
                    if (definition is null)
                    {
                        continue;
                    }

                    (carrier, rank) = BestFitFor(definition, lineup, catalog);
                    if (carrier < 0 || rank <= 0)
                    {
                        // Un objeto que no le sirve a nadie del once no es una compra: es tirar oro.
                        continue;
                    }
                }
                else
                {
                    rank = Rank(rarity, offers.Items[i].Price, options, safe: true);
                }

                if (rank > bestItemRank)
                {
                    bestItem = i;
                    bestItemRank = rank;
                    bestItemCarrier = carrier;
                }
            }

            if (bestItem >= 0)
            {
                return new BuyOffer(MarketCategories.Item, bestItem, bestItemCarrier);
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
            if (!state.HasRosterSpace)
            {
                var surplus = WorstSellable(state, lineup, options);
                return surplus is null || state.AvailablePlayerCount <= options.SellKeepingAvailable
                    ? null
                    : new SellPlayer(surplus.Id);
            }

            return new BuyOffer(MarketCategories.Player, bestRecruit);
        }

        // (f) Mercenario solo si faltan cuerpos Y cabe: no cuesta fichaje ni hueco de mercado, pero sí
        // ocupa plantilla (RF-020, ADR 0046). Con la plantilla llena y cuerpos faltando, la salida no es
        // el mercado: es el nodo de inscripción, y por eso la política lo prioriza justo en ese caso
        // (WantsRosterSlot). Ese es el bucle que la plantilla corta pone en marcha.
        if (state.HasRosterSpace && state.AvailablePlayerCount < options.HireMercenaryWhileAvailableBelow)
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

    /// <summary>
    /// Cobra <b>todas</b> las elecciones que da el nodo: una tras un partido de liga o de élite, dos tras
    /// un jefe (ADR 0043). Cada elección se resuelve por separado —surtido nuevo y decisión nueva—, que es
    /// lo que hace del jefe un trampolín y no una recompensa doble del mismo dado.
    /// </summary>
    private static RunState TakeRewards(
        RunState state,
        MapNode node,
        Catalog catalog,
        StandardRunSystems standard,
        IRunSystems systems,
        RunPolicyOptions options,
        Ledger ledger)
    {
        int picks = RewardSystem.PicksFor(node, standard.Economy);
        for (int pick = 0; pick < picks; pick++)
        {
            if (RewardSystem.AlreadyClaimed(state, node, standard.Economy) || RunEngine.Outcome(state).IsOver)
            {
                break;
            }

            state = TakeReward(state, node, catalog, standard, systems, options, ledger);
        }

        return ClaimStoredItems(state, catalog, systems);
    }

    /// <summary>
    /// Reparte el equipamiento heredado de los muertos (ADR 0048, condición 4). El objeto del caído está
    /// en el almacén y no cuesta oro: dárselo a un titular sin objeto es la parte de "se puede rehacer"
    /// que el jugador no tiene por qué pagar dos veces. Se hace después de cobrar la recompensa para que
    /// el objeto recién elegido cuente y no se duplique el hueco.
    /// </summary>
    private static RunState ClaimStoredItems(RunState state, Catalog catalog, IRunSystems systems)
    {
        var stored = state.StoredItems;
        for (int i = 0; i < stored.Count; i++)
        {
            int carrier = -1;
            var roster = state.Roster;
            for (int p = 0; p < roster.Count; p++)
            {
                if (roster[p].PhysicalState != PhysicalState.Dead && roster[p].Item is null)
                {
                    carrier = roster[p].Id;
                    break;
                }
            }

            if (carrier < 0)
            {
                break;
            }

            state = RunEngine.Apply(state, new EquipStoredItem(carrier, stored[i]), catalog, systems);
        }

        return state;
    }

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
        var choice = PickReward(state, rewards, catalog, standard.Economy, standard.Items, options);

        if (choice.Score < BestRewardScore && state.NodeRerolls == 0 && options.RerollGoldFactor != int.MaxValue)
        {
            int cost = standard.Economy.RerollCost(state.RerollsUsed);
            if (Spendable(state, standard.Economy) >= cost * options.RerollGoldFactor)
            {
                state = RunEngine.Apply(state, new RerollRewards(), catalog, systems);
                ledger.GoldSpentReroll += cost;
                ledger.Rerolls++;
                var rerolled = RewardSystem.Options(state, node, catalog, standard.Economy, standard.Items);
                choice = PickReward(state, rerolled, catalog, standard.Economy, standard.Items, options);
            }
        }

        // ADR 0043: rechazar. Un perk es irreversible (RF-072) y ocupa uno de los pocos slots que el once
        // tiene (RF-023), así que quedarse con el menos malo empeora la build: si ninguna opción encaja
        // -ningún perk que el filtro PerkPlacement acepte en un titular, ningún objeto para un titular sin
        // objeto, ningún cuerpo que haga falta-, se va con las manos vacías. La doctrina gastadora lo hace
        // mucho menos, y no por una regla aparte: no filtra por colocación, así que casi cualquier perk le
        // "encaja".
        if (choice.Index < 0 || choice.Score < BestRewardScore)
        {
            ledger.RewardsDeclined++;
            return RunEngine.Apply(state, new DeclineReward(), catalog, systems);
        }

        ledger.RewardsTaken++;
        return RunEngine.Apply(state, new ChooseReward(choice.Index, choice.Carrier), catalog, systems);
    }

    /// <summary>Puntuación de la mejor recompensa posible: un perk o un objeto para un titular.</summary>
    private const int BestRewardScore = 3;

    /// <summary>Preferencia de un maestro que ya se puede cobrar, dentro de su misma puntuación (ADR 0051).</summary>
    private const int MasterPreference = 2;

    /// <summary>Preferencia de una pieza de la línea que la run persigue (ADR 0051).</summary>
    private const int FamilyPreference = 1;

    /// <summary>
    /// Lo que pesa la preferencia de arco en el mercado (ADR 0051). Está por encima del término de
    /// antisinergia (un millón) para que el maestro de la línea se compre aunque haya otro perk más
    /// valioso a la venta: la línea completa vale más que la pieza suelta, y eso es lo que se mide.
    /// </summary>
    private const int ArcMarketWeight = 2_000_000;

    /// <summary>
    /// La línea que la run persigue (ADR 0051): aquella de la que ya lleva más piezas y cuyo maestro
    /// sigue siendo alcanzable. Empates por id de línea ordinal ascendente (RT-041): la política no tira
    /// un dado para decidir su build. Cadena vacía si no persigue ninguna.
    /// </summary>
    private static string PursuedFamily(RunState state, Catalog catalog, RunPolicyOptions options)
    {
        if (!options.PursuesMasters)
        {
            return string.Empty;
        }

        var closed = PerkPool.ClosedBy(state, catalog);
        string best = string.Empty;
        int bestHeld = -1;
        foreach (var master in catalog.Perks.Masters)
        {
            if (master.Requires is not { } requirement || closed.Blocks(master))
            {
                continue;
            }

            int held = PerkPool.FamilyHeld(state, catalog, requirement.Family);
            if (held > bestHeld
                || (held == bestHeld && string.CompareOrdinal(requirement.Family, best) < 0))
            {
                best = requirement.Family;
                bestHeld = held;
            }
        }

        return best;
    }

    /// <summary>
    /// Cuánto prefiere la política este perk dentro de su puntuación (ADR 0051): el maestro que ya puede
    /// cobrar por encima de todo, y por debajo las piezas de la línea que persigue.
    /// </summary>
    private static int ArcPreference(Perks.PerkDefinition perk, string pursued, bool takeable)
    {
        if (perk.IsMaster)
        {
            return takeable ? MasterPreference : 0;
        }

        return pursued.Length > 0 && string.Equals(perk.Family, pursued, StringComparison.Ordinal)
            ? FamilyPreference
            : 0;
    }

    private readonly record struct RewardChoice(int Index, int Carrier, int Score, int Preference = 0);

    private static RewardChoice PickReward(
        RunState state,
        IReadOnlyList<RewardOption> rewards,
        Catalog catalog,
        EconomyConfig economy,
        ItemCatalog? items,
        RunPolicyOptions options)
    {
        var lineup = ChooseStarters(state, options);
        var placement = PlacementOf(lineup);
        int naked = BestStarterWithoutItem(state, lineup, options);
        string pursued = PursuedFamily(state, catalog, options);
        var best = new RewardChoice(-1, -1, 0);

        for (int i = 0; i < rewards.Count; i++)
        {
            int score, carrier = -1, preference = 0;
            switch (rewards[i])
            {
                case PerkRewardOption perk:
                    var definition = catalog.Perks.Find(perk.PerkId);

                    // ADR 0051: un maestro que la run todavía no puede sostener, o una línea que otro
                    // maestro cerró, no son opciones: /Sim las rechazaría al cobrarlas.
                    // "Cobrable" aquí es lo que decide el ARCO (ADR 0051, ADR 0055), no si hay portador:
                    // eso lo resuelve la lista de portadores dos líneas más abajo y tiene su propia
                    // puntuación.
                    bool takeable = definition is not null
                        && PerkPool.Availability(state, definition, catalog, PerkSource.Reward)
                            is not (PerkAvailability.Unmet or PerkAvailability.Closed or PerkAvailability.MarketOnly);
                    var carriers = definition is null || !takeable || !WorthASlot(perk.PerkId, economy, options)
                        ? Array.Empty<int>()
                        : PerkPool.EligibleCarriers(state, definition, catalog);
                    carrier = definition is null || carriers.Count == 0
                        ? -1
                        : BestCarrier(state, definition, carriers, lineup, placement, options);
                    score = carrier >= 0 ? 3 : (carriers.Count > 0 ? 2 : 0);
                    if (score == 2)
                    {
                        carrier = carriers[0];
                    }

                    if (definition is not null)
                    {
                        preference = ArcPreference(definition, pursued, takeable);
                    }

                    break;

                case ItemRewardOption item:
                    // La contextual elige portador por encaje (ADR 0036); las puras, el titular sin
                    // objeto de más valor.
                    carrier = naked;
                    if (options.Doctrine == PurchaseDoctrine.Contextual && items?.Find(item.ItemId) is { } itemDefinition)
                    {
                        int fitted = BestFitFor(itemDefinition, lineup, catalog).Carrier;
                        if (fitted >= 0)
                        {
                            carrier = fitted;
                        }
                    }

                    score = carrier >= 0 ? 3 : 1;
                    if (score == 1 && lineup.Count > 0)
                    {
                        carrier = lineup[0].Id;
                    }

                    break;

                case PlayerRewardOption:
                    // Un jugador de recompensa vale como el mejor perk solo cuando faltan cuerpos. Con la
                    // plantilla llena (RF-020) la opción ni siquiera se puede cobrar —en un nodo de
                    // recompensa no hay mercado en el que vender—, así que vale 0 y el nodo se rechaza
                    // (ADR 0043) sin ocupar nada irreversible.
                    score = !state.HasRosterSpace
                        ? 0
                        : (state.AvailablePlayerCount < options.SignWhileAvailableBelow ? 3 : 2);
                    break;

                default:
                    score = 0;
                    break;
            }

            // A igual puntuación manda el arco (ADR 0051): entre dos opciones igual de buenas, la que
            // avanza la línea que la run persigue. Es lo que convierte "coger lo mejor de tres" en
            // "construir hacia algo", que es justo lo que el catálogo suelto no permitía.
            if (score > best.Score || (score == best.Score && score > 0 && preference > best.Preference))
            {
                best = new RewardChoice(i, carrier, score, preference);
            }
        }

        return best;
    }

    // ------------------------------------------------------------------ ayudantes

    /// <summary>Oro que la política se permite gastar: reserva la clínica mientras haya un lesionado grave.</summary>
    private static int Spendable(RunState state, EconomyConfig economy) =>
        HasUntreatedSevereInjury(state) ? Math.Max(0, state.Gold - economy.ClinicCost) : state.Gold;

    /// <summary>
    /// Oro que la política se permite gastar <b>en el mercado</b>: además de la clínica, aparta el precio
    /// del hueco de plantilla cuando lo necesita (ADR 0046). Sin esta reserva el nodo de inscripción es
    /// decorado: el mercado va antes en el acto y se lleva el oro.
    /// </summary>
    private static int SpendableAtMarket(RunState state, EconomyConfig economy, RunPolicyOptions options)
    {
        int gold = Spendable(state, economy);

        // Solo se ahorra para el PRIMER hueco. El segundo cuesta "bastante más" (ADR 0046) y la política
        // lo compra únicamente si le sobra el oro al llegar al nodo: ahorrar para él significaría no
        // comprar nada en el mercado durante medio acto, y a esas alturas un perk raro vale más que el
        // duodécimo cuerpo. Medido: reservando para los dos, la plantilla vuelve a 11,2, las muertes caen
        // a 0,22 y el gasto de mercado se hunde de 53 a 24 de oro por run.
        if (state.Counter(RunState.EnrollmentSlotsCounter) > 0)
        {
            return gold;
        }

        int slot = NeededEnrollmentCost(state, economy, options);
        return slot > 0 ? Math.Max(0, gold - slot) : gold;
    }

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

    /// <summary>
    /// <b>Cuánto le sirve ESE objeto a ESE jugador</b> (ADR 0036). Con el equipamiento convertido en
    /// atributos, "¿a quién le doy las botas?" se responde mirando la plantilla, y esa es la decisión que
    /// separa a la doctrina contextual de las dos puras: la gastadora y la ahorradora equipan a quien
    /// toque, la contextual mira el puesto.
    ///
    /// <para>El peso de cada atributo por posición no se inventa: es <c>tuning.generation.positionShare</c>,
    /// el mismo reparto con el que el generador decide en qué gasta su presupuesto un portero o un
    /// delantero. Así el maldito cae donde su contrapartida no duele —<c>berserker_totem</c> baja técnica
    /// y vale mucho en un central y muy poco en el organizador— sin una sola regla escrita a mano.</para>
    /// </summary>
    private static int ItemFit(ItemDefinition item, RunPlayer player, Catalog catalog)
    {
        var share = catalog.Tuning.Generation.PositionShare.Of(player.Position);
        int score = 0;
        foreach (var kind in ItemScale.AttributeOrder)
        {
            score += item.Modifier.Get(kind) * share.Get(kind);
        }

        return score;
    }

    /// <summary>Titular sin objeto al que mejor le viene ese objeto, con su ajuste; (-1, 0) si a ninguno.</summary>
    private static (int Carrier, int Fit) BestFitFor(
        ItemDefinition item, IReadOnlyList<RunPlayer> lineup, Catalog catalog)
    {
        int carrier = -1, best = int.MinValue;
        for (int i = 0; i < lineup.Count; i++)
        {
            var player = lineup[i];
            if (player.Item is not null || player.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            int fit = ItemFit(item, player, catalog);
            if (fit > best)
            {
                best = fit;
                carrier = player.Id;
            }
        }

        return carrier < 0 ? (-1, 0) : (carrier, best);
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
        Catalog catalog,
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

        var held = PerkPool.HeldPerkIds(state);
        var masters = new List<string>();
        for (int i = 0; i < held.Count; i++)
        {
            if (catalog.Perks.Find(held[i]) is { IsMaster: true })
            {
                masters.Add(held[i]);
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
            ledger.GoldSpentEnrollment,
            ledger.GoldSpentReroll,
            ledger.GoldSpentWages,
            state.Gold,
            ledger.Deaths,
            injuries,
            ledger.OwnInjuries,
            ledger.MatchInjuries,
            ledger.SevereInjuries,
            state.RosterSize,
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
            ledger.SlotsBought,
            ledger.Rerolls,
            ledger.RewardsTaken,
            ledger.RewardsDeclined,
            ledger.Nodes,
            ledger.MatchesByAct,
            ledger.WinsByAct,
            ledger.MarketsByAct,
            ledger.GoldEarnedByAct,
            ledger.DeathsByAct,
            ledger.PerksAtBossByAct,
            ledger.ItemsAtBossByAct,
            ledger.BossSamplesByAct,
            ledger.ItemsRecovered,
            masters,
            held,
            ledger.MastersOffered,
            ledger.MastersAffordable);
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
            _ledger.MatchInjuries += summary.Report.Injuries;
            return _inner.AfterMatch(state, node, summary, catalog);
        }

        public IReadOnlyList<RunReferee> CreateReferees(ulong seed, int count, Catalog catalog) =>
            _inner.CreateReferees(seed, count, catalog);

        public TeamSetup OpponentFor(RunState state, MapNode node, Catalog catalog) =>
            _inner.OpponentFor(state, node, catalog);

        public RefereeSetup RefereeFor(RunState state, MapNode node, Catalog catalog) =>
            _inner.RefereeFor(state, node, catalog);

        public Underleague.Sim.Engine.SimConfig MatchConfig(RunState state, MapNode node, Catalog catalog) =>
            _inner.MatchConfig(state, node, catalog);

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

        /// <summary>Lesiones de los DOS equipos en los partidos de la run: la misma cifra que mide RT-056.</summary>
        public int MatchInjuries;

        public int Matches;
        public int MatchesWon;
        public int BossesBeaten;
        public int GoldEarned;
        public int GoldFromSales;
        public int GoldSpentMarket;
        public int GoldSpentClinic;

        /// <summary>Oro gastado en huecos de plantilla (ADR 0046): el sumidero nuevo.</summary>
        public int GoldSpentEnrollment;

        /// <summary>Huecos de plantilla comprados en la run (0, 1 o 2).</summary>
        public int SlotsBought;
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

        /// <summary>Elecciones de recompensa cobradas (ADR 0043: el jefe da dos).</summary>
        public int RewardsTaken;

        /// <summary>Elecciones rechazadas porque ninguna opción encajaba (ADR 0043).</summary>
        public int RewardsDeclined;

        /// <summary>Veces que un maestro estuvo en el mostrador de un mercado (ADR 0055).</summary>
        public int MastersOffered;

        /// <summary>De esas, cuántas se podían cobrar y pagar de verdad (ADR 0055).</summary>
        public int MastersAffordable;

        /// <summary>Nodos recorridos, de partido y de servicio: la duración de la run en nodos (RF-003b).</summary>
        public int Nodes;

        public int[] MatchesByAct { get; } = new int[RunRules.Acts];

        public int[] WinsByAct { get; } = new int[RunRules.Acts];

        public int[] MarketsByAct { get; } = new int[RunRules.Acts];

        public int[] GoldEarnedByAct { get; } = new int[RunRules.Acts];

        /// <summary>Muertes por acto (ADR 0048): la banda 1,5-3 no dice nada sin saber dónde caen.</summary>
        public int[] DeathsByAct { get; } = new int[RunRules.Acts];

        /// <summary>Perks del once al entrar en el jefe de cada acto (ADR 0049: calibra groups.json).</summary>
        public int[] PerksAtBossByAct { get; } = new int[RunRules.Acts];

        /// <summary>Objetos del once al entrar en el jefe de cada acto.</summary>
        public int[] ItemsAtBossByAct { get; } = new int[RunRules.Acts];

        /// <summary>Jefes jugados por acto, para promediar los dos de arriba.</summary>
        public int[] BossSamplesByAct { get; } = new int[RunRules.Acts];

        /// <summary>
        /// Objetos que el inventario ha recuperado de un muerto (ADR 0048, condición 4): la
        /// "recuperación" que sostiene que la muerte sea rehacible y no solo una pérdida.
        /// </summary>
        public int ItemsRecovered;
    }
}
