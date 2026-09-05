using Underleague.Sim.Model;

namespace Underleague.Sim.Run;

/// <summary>
/// Entrada de <see cref="RunEngine.Start"/>: con qué club y con qué copia de <c>/data</c> empieza la run.
/// </summary>
/// <param name="ClubId">Id del club inicial (RF-004, <c>data/clubs/</c>).</param>
/// <param name="ClubRace">Raza del club: todos los jugadores iniciales son de ella (RF-004).</param>
/// <param name="DataSnapshot">
/// Contenido de <c>/data</c> tal y como estaba al empezar la run (RT-061b): ruta relativa -&gt; texto del
/// fichero, el mismo formato que consume <c>DataLoader.FromJson</c>. Se congela en el estado y el
/// guardado lo lleva consigo, así que una actualización del juego no altera una run en curso.
/// </param>
public sealed record RunSetup(
    string ClubId,
    Race ClubRace,
    IReadOnlyDictionary<string, string> DataSnapshot)
{
    /// <summary>División en la que se juega (RF-128). La fase 2 juega en tercera.</summary>
    public Division Division { get; init; } = Division.Third;

    /// <summary>Oro de partida del club (RF-004). Lo fijará <c>data/clubs/</c> en el paquete X.</summary>
    public int StartingGold { get; init; }

    /// <summary>
    /// Plantilla inicial explícita (RF-005: 7 titulares y 3 suplentes, uno de rareza superior). Si es
    /// null, <see cref="RunEngine.Start"/> la genera con <c>TeamGenerator</c> y
    /// <see cref="GeneratedQuality"/>, que es lo que hacen los tests y <c>/Balance</c> mientras
    /// <c>data/clubs/</c> no exista.
    /// </summary>
    public IReadOnlyList<RunPlayer>? Roster { get; init; }

    /// <summary>Calidad media de la plantilla generada cuando <see cref="Roster"/> es null.</summary>
    public int GeneratedQuality { get; init; } = 50;

    /// <summary>
    /// Nodos que el jugador <b>recorre</b> en cada acto, 10..12 (RF-001, D-2/D-10). Es la longitud del
    /// camino, no el número de nodos dibujados: ver la nota sobre RF-001 en <see cref="MapGenerator"/>.
    /// </summary>
    public int NodesPerAct { get; init; } = MapGenerator.DefaultPathLength;

    /// <summary>
    /// Nodos recorridos <b>por acto</b> (índice 0 = acto 1), cuando el acto crece con la run. Es el valor
    /// que cierra D-2/D-10 y vive en <c>data/map/map.json</c>. Null = los tres actos usan
    /// <see cref="NodesPerAct"/>.
    /// </summary>
    public IReadOnlyList<int>? NodesPerActByAct { get; init; }

    /// <summary>Nodos que el jugador recorre en el acto indicado (1..3).</summary>
    public int NodesOfAct(int act) =>
        NodesPerActByAct is { Count: > 0 } byAct && act >= 1 && act - 1 < byAct.Count
            ? byAct[act - 1]
            : NodesPerAct;

    /// <summary>
    /// Rivales estáticos por acto (RF-015): índice 0 = acto 1. Cada lista se reparte entre los nodos de
    /// partido de su acto. Null mientras el paquete X no traiga <c>data/rivals/</c>.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>>? OpponentIdsByAct { get; init; }

    /// <summary>Árbitros de la run, 6-8 (RF-061b).</summary>
    public int RefereeCount { get; init; } = 6;
}

/// <summary>
/// Una decisión del jugador dentro de un nodo abierto o en el mapa, para
/// <see cref="RunEngine.Apply(RunState, RunDecision, Underleague.Sim.Data.Catalog, IRunSystems?)"/>.
///
/// <para>La jerarquía es cerrada a propósito: cada tipo de decisión es un registro con sus datos y el
/// motor la despacha por tipo. Los paquetes X e Y añaden aquí los suyos y los resuelven en su
/// implementación de <see cref="IRunSystems.ApplyDecision"/>; el paquete W solo resuelve las dos que
/// son suyas, <see cref="SetLineup"/> y <see cref="SetConsumables"/>, y las de cierre de nodo.</para>
/// </summary>
public abstract record RunDecision;

/// <summary>Fija la alineación titular (RF-041). Se valida al entrar en el nodo de partido.</summary>
public sealed record SetLineup(Lineup Lineup) : RunDecision;

/// <summary>Fija los consumibles equipados de la run (RF-080..082).</summary>
public sealed record SetConsumables(IReadOnlyList<EquippedConsumable> Consumables) : RunDecision;

/// <summary>Cierra el nodo interactivo abierto y vuelve al mapa.</summary>
public sealed record LeaveNode : RunDecision;

/// <summary>Compra el artículo indicado del surtido del mercado abierto (RF-114). Paquete X.</summary>
/// <param name="Category">Categoría del surtido: jugadores, perks, equipamiento o consumibles (RF-114).</param>
/// <param name="OfferIndex">Índice dentro de la categoría, 0..3.</param>
/// <param name="TargetPlayerId">Jugador que recibe el perk o el objeto comprado (RF-114e); -1 si no aplica.</param>
public sealed record BuyOffer(string Category, int OfferIndex, int TargetPlayerId = -1) : RunDecision;

/// <summary>Vende un jugador de la plantilla (RF-114f). Paquete X.</summary>
public sealed record SellPlayer(int PlayerId) : RunDecision;

/// <summary>Ficha al mercenario indicado del surtido (RF-110..113). Paquete X.</summary>
public sealed record HireMercenary(int OfferIndex) : RunDecision;

/// <summary>Trata a un jugador en la clínica (RF-094). Paquete X.</summary>
public sealed record TreatPlayer(int PlayerId) : RunDecision;

/// <summary>Elige una de las tres recompensas tras ganar un partido (RF-071). Paquete X.</summary>
/// <param name="OptionIndex">Opción elegida, 0..2.</param>
/// <param name="CarrierPlayerId">Si la recompensa es un perk, jugador que lo porta; -1 si no aplica.</param>
public sealed record ChooseReward(int OptionIndex, int CarrierPlayerId = -1) : RunDecision;

/// <summary>
/// <b>Rechaza</b> la recompensa ofrecida y se va con las manos vacías (ADR 0043). RF-071 obligaba a
/// elegir una de las tres; con perks irreversibles (RF-072) y slots limitados, quedarse con la menos mala
/// puede ser peor que no quedarse con nada. Consume la elección, no la aplaza.
/// </summary>
public sealed record DeclineReward : RunDecision;

/// <summary>Vuelve a tirar las recompensas del nodo. Uno por nodo, coste creciente (RF-071b). Paquete X.</summary>
public sealed record RerollRewards : RunDecision;

/// <summary>Mueve el objeto equipado de un jugador a otro fuera de partido (RF-076b). Paquete X.</summary>
public sealed record TransferItem(int FromPlayerId, int ToPlayerId) : RunDecision;

/// <summary>
/// Equipa a un jugador un objeto que está en el <b>almacén</b> del club (ADR 0048, condición 4). Al
/// almacén solo se llega de una forma: heredando el equipamiento de un jugador muerto. Es "se puede
/// rehacer" escrito como decisión —la muerte cuesta el jugador, no el jugador y su equipo— y no cuesta
/// oro, porque el objeto ya estaba pagado.
/// </summary>
public sealed record EquipStoredItem(int PlayerId, string ItemId) : RunDecision;

/// <summary>
/// Compra un hueco de plantilla en un nodo de inscripción (ADR 0046, amplía RF-011). Paga el coste
/// creciente de <c>economy.enrollmentCosts</c> y sube el techo de plantilla en uno, hasta el máximo de
/// <see cref="RunRules.MaxRosterSize"/>. Paquete X.
/// </summary>
public sealed record ExpandRoster : RunDecision;

/// <summary>
/// <b>Descarta</b> a un jugador de la plantilla sin cobrar nada por él (RF-020, ADR 0046). Es la otra
/// mitad de "con la plantilla llena, fichar exige vender o descartar": vender solo se puede en el
/// mercado (RF-114f), y hacer sitio no puede depender de estar en un mercado. Nunca puede dejar los
/// disponibles por debajo del mínimo de RF-002b.
/// </summary>
public sealed record ReleasePlayer(int PlayerId) : RunDecision;
