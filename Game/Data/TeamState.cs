using System;
using System.Collections.Generic;
using Underleague.Game.Autoload;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Placement;
using Underleague.Sim.Random;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Game.Data;

/// <summary>
/// Lo que la pantalla de Equipo necesita saber de un equipo: el catálogo con el que se lee y la plantilla
/// con su alineación. No hay datos falsos incrustados en ninguna parte de la pantalla.
/// <para>
/// Tiene tres orígenes y ninguno de ellos es la escena: la <b>run en curso</b>
/// (<see cref="FromRun"/>), un equipo ya construido como el rival del ojeo (<see cref="Of"/>) y la
/// plantilla de pruebas con la que la pantalla se diseñó (<see cref="Load"/>).
/// </para>
/// <para>
/// Ninguna regla de juego vive aquí (RT-014): mover a un jugador lo resuelve
/// <see cref="PlacementView.WithPlayerAt"/> y guardarlo, <c>RunEngine.Apply(SetLineup)</c>.
/// </para>
/// </summary>
public sealed class TeamState
{
    /// <summary>Idioma de la interfaz. En fase 4 lo elige el jugador (RT-073); hasta entonces, español.</summary>
    public const string Language = GameData.Language;

    private TeamState(Catalog catalog, TeamSetup team, RunController? run = null)
    {
        Catalog = catalog;
        Team = team;
        Templates = catalog.Localization.Get(Language);
        _run = run;
    }

    private readonly RunController? _run;

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b> (AW-K): sin una run detrás (equipo de pruebas, rival de
    /// ojeo) no hay ningún objeto equipado que enseñar, y la ficha del objeto es justo lo que hay que
    /// fotografiar. <see cref="ForceTestItem"/> es el mismo apaño que <c>TeamScreen.EnsurePlacementPerks</c>
    /// hace con perks, pero para objetos; nunca se usa con una run real detrás.
    /// </summary>
    private Dictionary<int, ItemDefinition>? _testItems;

    public Catalog Catalog { get; }

    public TeamSetup Team { get; private set; }

    public DescriptionTemplates Templates { get; }

    public IReadOnlyList<PlayerDefinition> Players => Team.Players;

    public Lineup Lineup => Team.Lineup;

    /// <summary>
    /// La plantilla de la <b>run en curso</b> (RT-030): la que se alinea de verdad. Los jugadores se
    /// convierten con <c>RunPlayer.ToDefinition</c>, que es la misma conversión con la que el motor los
    /// manda al campo —penalización de lesión leve incluida (RF-091)—, de modo que lo que la ficha
    /// enseña es lo que va a jugar.
    /// </summary>
    public static TeamState FromRun(RunController run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var catalog = run.Catalog ?? throw new InvalidOperationException("la run no tiene catálogo cargado");
        return new TeamState(catalog, TeamOf(run.State!, catalog, run.Systems), run);
    }

    /// <summary>
    /// Un equipo cualquiera ya construido: lo que necesita el informe de ojeo para enseñar la plantilla
    /// rival con las mismas fichas que la propia (UI-010). No hay run detrás, así que no se puede mover a
    /// nadie.
    /// </summary>
    public static TeamState Of(Catalog catalog, TeamSetup team) => new(catalog, team);

    /// <summary>
    /// El nombre visible del club, no su id de datos (RF-004): antes de este arreglo, la pantalla de
    /// Equipo mostraba literalmente <c>state.ClubId</c> ("underleague_fc") en el subtítulo. Si el
    /// catálogo de clubes de la run conoce el id, se usa su nombre; si no (equipo de pruebas cargado sin
    /// run, <see cref="Load"/>), se deja el id, que ese caso ya sustituye por el texto de marcador de
    /// posición antes de mostrarlo (<c>ui.team.placeholderClub</c>).
    /// </summary>
    private static TeamSetup TeamOf(RunState state, Catalog catalog, Sim.Run.Systems.StandardRunSystems? systems)
    {
        var players = new List<PlayerDefinition>(state.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            players.Add(state.Roster[i].ToDefinition(catalog));
        }

        string clubName = systems?.Clubs.Find(state.ClubId)?.Name.Es ?? state.ClubId;
        return new TeamSetup(state.ClubId, clubName, state.ClubRace, players, state.Lineup);
    }

    /// <summary>
    /// Carga <c>/data</c> y genera la plantilla con la semilla dada. Es el equipo de pruebas con el que
    /// la pantalla de Equipo se diseñó y con el que se regeneran sus capturas; una run de verdad entra
    /// por <see cref="FromRun"/>.
    /// </summary>
    public static TeamState Load(ulong seed)
    {
        var catalog = DataLoader.FromJson(GameData.Snapshot);

        var generation = RngStreams.Generation(seed, 0);
        var team = TeamGenerator.Generate(ref generation, catalog, "underleague_fc", Race.Orc, quality: 55, firstPlayerId: 1, level: 3);

        var rewards = RngStreams.Rewards(seed, 0);
        var withPerks = PerkAssignment.AssignInitial(ref rewards, team.Players, catalog);
        return new TeamState(catalog, team with { Players = withPerks });
    }

    /// <summary>
    /// Las prótesis que lleva el jugador en la run (ADR 0164), en el orden en que se instalaron; vacío sin run
    /// detrás (equipo de pruebas, rival de ojeo) o si su definición ya no está en el catálogo.
    /// </summary>
    public IReadOnlyList<Sim.Run.Systems.Medical.ProsthesisDefinition> ProsthesesOf(int playerId)
    {
        var result = new List<Sim.Run.Systems.Medical.ProsthesisDefinition>();
        if (_run?.State is not { } state || _run.Systems is null)
        {
            return result;
        }

        foreach (var slot in state.Roster)
        {
            if (slot.Id != playerId)
            {
                continue;
            }

            foreach (var installed in slot.Prostheses)
            {
                if (_run.Systems.Prostheses.Find(installed.Effect) is { } definition)
                {
                    result.Add(definition);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Estado frente al tope de prótesis (ADR 0187) del jugador de la run: cuántas lleva y si ya está lisiado.
    /// (0, false) sin run detrás.
    /// </summary>
    public (int Count, bool Crippled) ProsthesisCapOf(int playerId)
    {
        if (_run?.State is { } state)
        {
            foreach (var slot in state.Roster)
            {
                if (slot.Id == playerId)
                {
                    return (slot.Prostheses.Count, slot.IsCrippled);
                }
            }
        }

        return (0, false);
    }

    /// <summary>Jugador por id, o null si no está en la plantilla.</summary>
    public PlayerDefinition? Find(int id)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].Id == id)
            {
                return Players[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Objeto equipado por el jugador (AW-K), o null si no lleva ninguno. El dato vive en la capa de run
    /// (<c>RunPlayer.Item</c>), no en <see cref="PlayerDefinition"/>: sin run detrás (equipo de pruebas,
    /// rival de ojeo) siempre es null, salvo que las capturas lo hayan forzado con
    /// <see cref="ForceTestItem"/>.
    /// </summary>
    public ItemDefinition? EquippedItemOf(int playerId)
    {
        if (_testItems is not null && _testItems.TryGetValue(playerId, out var forced))
        {
            return forced;
        }

        if (_run is null)
        {
            return null;
        }

        foreach (var slot in _run.State!.Roster)
        {
            if (slot.Id == playerId)
            {
                return slot.Item is { } itemId ? _run.Systems!.Items.Find(itemId) : null;
            }
        }

        return null;
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b>: fuerza el objeto equipado de un jugador sin tocar la
    /// run ni el catálogo, igual que <c>TeamScreen.EnsurePlacementPerks</c> fuerza perks. No hace nada
    /// si ya hay una run real detrás: ahí el objeto lo decide el bucle de run, no una captura.
    /// </summary>
    public void ForceTestItem(int playerId, ItemDefinition item)
    {
        if (_run is { HasRun: true })
        {
            return;
        }

        _testItems ??= new Dictionary<int, ItemDefinition>();
        _testItems[playerId] = item;
    }

    /// <summary>Ver <see cref="ForceTestCareer"/>: solo para la secuencia de capturas.</summary>
    private Dictionary<int, RunCareer>? _testCareers;

    /// <summary>
    /// Carrera del jugador (RF-122, ADR 0124, F1 §6): el registro tipado que solo vive en
    /// <see cref="RunPlayer.Career"/>, no en <see cref="PlayerDefinition"/>. Null sin run detrás (equipo
    /// de pruebas, rival de ojeo) -mismo patrón que <see cref="EquippedItemOf"/>-, salvo que la secuencia
    /// de capturas la haya forzado con <see cref="ForceTestCareer"/>.
    /// </summary>
    public RunCareer? CareerOf(int playerId)
    {
        if (_testCareers is not null && _testCareers.TryGetValue(playerId, out var forced))
        {
            return forced;
        }

        if (_run is null)
        {
            return null;
        }

        foreach (var slot in _run.State!.Roster)
        {
            if (slot.Id == playerId)
            {
                return slot.Career;
            }
        }

        return null;
    }

    private Sim.Run.Systems.Nicknames.NicknameCatalog? _nicknames;

    /// <summary>
    /// Apodo del jugador (ADR 0163) en el idioma de la interfaz, derivado de su carrera; vacío si no ha
    /// ganado ninguno. Con una run detrás usa el catálogo de la run; sin ella (equipo de pruebas) lo lee de
    /// <c>/data</c>, para que la ficha de capturas enseñe lo mismo que enseñaría en una partida.
    /// </summary>
    public string NicknameOf(int playerId) => NicknameDefinitionOf(playerId)?.NameIn(Language) ?? string.Empty;

    /// <summary>El apodo (definición) que tiene ahora el jugador, o null. Ver <see cref="NicknameOf"/>.</summary>
    public Sim.Run.Systems.Nicknames.NicknameDefinition? NicknameDefinitionOf(int playerId)
    {
        var career = CareerOf(playerId);
        if (career is null)
        {
            return null;
        }

        _nicknames ??= _run?.Systems?.Nicknames ?? Sim.Run.Systems.Nicknames.NicknameLoader.FromJson(GameData.Snapshot);
        return Sim.Run.Systems.Nicknames.NicknameSystem.For(career, _nicknames);
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b>: fuerza la carrera de un jugador sin tocar la run ni el
    /// catálogo, igual que <see cref="ForceTestItem"/>. No hace nada si ya hay una run real detrás: ahí la
    /// carrera la acumula <c>MatchResolution</c>, no una captura.
    /// </summary>
    public void ForceTestCareer(int playerId, RunCareer career)
    {
        if (_run is { HasRun: true })
        {
            return;
        }

        _testCareers ??= new Dictionary<int, RunCareer>();
        _testCareers[playerId] = career;
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b> (CAT-B, mismo apaño que <see cref="ForceTestItem"/> para
    /// objetos): sin run detrás no hay consumibles que llevar, y la sección de Equipo necesita enseñar
    /// algo más que los huecos vacíos. Nunca se usa con una run real detrás.
    /// </summary>
    private ConsumableCatalog? _testConsumables;

    private List<EquippedConsumable>? _testEquippedConsumables;

    /// <summary>
    /// Lo que la run lleva en sus dos huecos de consumible (RF-080..082, ADR 0172), o vacío sin run detrás.
    /// El hueco es la posesión: no hay inventario suelto.
    /// </summary>
    public IReadOnlyList<EquippedConsumable> EquippedConsumables =>
        _testEquippedConsumables ?? _run?.State?.Consumables ?? Array.Empty<EquippedConsumable>();

    /// <summary>True si queda un hueco de consumible libre.</summary>
    public bool HasFreeConsumableSlot => EquippedConsumables.Count < RunRules.ConsumableSlots;

    /// <summary>
    /// Definición de un consumible, del catálogo de <b>esta</b> run; null sin run detrás (salvo que la
    /// secuencia de capturas haya forzado uno con <see cref="ForceTestConsumables"/>) o si el id no existe.
    /// </summary>
    public ConsumableDefinition? Consumable(string consumableId) =>
        _run?.Systems?.Consumables.Find(consumableId) ?? _testConsumables?.Find(consumableId);

    /// <summary>
    /// Aplica la lista completa de lo que se lleva en los huecos (CAT-B, ADR 0172): reconfigurar un modo o un
    /// disparador, o descartar uno (una lista sin él). Igual que <see cref="Move"/> con <c>SetLineup</c>: la
    /// regla la valida <c>/Sim</c> (<c>RunEngine.Apply(SetConsumables)</c>, RF-080..082); aquí solo se pide y
    /// se refresca la copia local. Sin run detrás no hay nada que reconfigurar, así que no hace nada.
    /// </summary>
    public void ApplyConsumables(IReadOnlyList<EquippedConsumable> consumables)
    {
        if (_run is not { HasRun: true })
        {
            return;
        }

        _run.Apply(new SetConsumables(consumables));
        Team = TeamOf(_run.State!, Catalog, _run.Systems);
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b> (ADR 0161, mismo apaño que <see cref="ForceTestItem"/> y
    /// <see cref="ForceTestConsumables"/>): sin run detrás no hay almacén del que sacar nada, y el cofre
    /// necesita enseñar algo más que el mensaje de vacío. Nunca se usa con una run real detrás.
    /// </summary>
    private Dictionary<string, ItemDefinition>? _testStoredItemDefs;

    private List<string>? _testStoredItems;

    /// <summary>
    /// Objetos del almacén, uno por copia y ordenados por id (RT-041, ADR 0161): botín de liga (§1),
    /// reliquia de un muerto (§2) o equipo heredado (ADR 0048). Vacío sin run detrás, salvo que la
    /// secuencia de capturas lo haya forzado con <see cref="ForceTestStoredItems"/>.
    /// </summary>
    public IReadOnlyList<string> StoredItems => _testStoredItems ?? _run?.State?.StoredItems ?? Array.Empty<string>();

    /// <summary>
    /// Definición de un objeto del almacén, del catálogo de <b>esta</b> run; null sin run detrás salvo que
    /// la secuencia de capturas haya forzado uno con <see cref="ForceTestStoredItems"/>.
    /// </summary>
    public ItemDefinition? Item(string itemId)
    {
        if (_testStoredItemDefs is not null && _testStoredItemDefs.TryGetValue(itemId, out var forced))
        {
            return forced;
        }

        return _run?.Systems?.Items.Find(itemId);
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b>: fuerza el contenido del almacén sin tocar la run ni el
    /// catálogo, igual que <see cref="ForceTestItem"/>. No hace nada si ya hay una run real detrás: ahí el
    /// almacén lo llena el bucle de run (botín, reliquia, herencia), no una captura.
    /// </summary>
    public void ForceTestStoredItems(IReadOnlyList<ItemDefinition> items)
    {
        if (_run is { HasRun: true })
        {
            return;
        }

        _testStoredItemDefs = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        var ids = new List<string>(items.Count);
        foreach (var item in items)
        {
            _testStoredItemDefs[item.Id] = item;
            ids.Add(item.Id);
        }

        ids.Sort(StringComparer.Ordinal);
        _testStoredItems = ids;
    }

    /// <summary>
    /// Equipa desde el almacén (ADR 0161 §3, cofre de Equipo, decisión <c>EquipStoredItem</c>). La regla
    /// —un objeto por jugador, el desplazado vuelve al cofre— la valida <c>/Sim</c>; aquí solo se pide y se
    /// refresca la copia local. Sin run detrás no hay almacén del que equipar nada, así que no hace nada.
    /// </summary>
    public void EquipStored(int playerId, string itemId)
    {
        if (_run is not { HasRun: true })
        {
            return;
        }

        _run.Apply(new EquipStoredItem(playerId, itemId));
        Team = TeamOf(_run.State!, Catalog, _run.Systems);
    }

    /// <summary>
    /// Guarda en el almacén el objeto que lleva puesto el jugador (ADR 0161 §3, decisión <c>StoreItem</c>).
    /// Sin run detrás no hace nada. Nombrado distinto de la decisión (como <see cref="EquipStored"/>) para
    /// no chocar el nombre del método con el del tipo <c>Underleague.Sim.Run.StoreItem</c> en su propio
    /// cuerpo.
    /// </summary>
    public void StoreEquipped(int playerId)
    {
        if (_run is not { HasRun: true })
        {
            return;
        }

        _run.Apply(new StoreItem(playerId));
        Team = TeamOf(_run.State!, Catalog, _run.Systems);
    }

    /// <summary>
    /// Pasa el objeto equipado de un jugador a otro (RF-076b, decisión <c>TransferItem</c> ya existente).
    /// Sin run detrás no hace nada. Mismo motivo de nombre que <see cref="StoreEquipped"/>.
    /// </summary>
    public void TransferEquipped(int fromPlayerId, int toPlayerId)
    {
        if (_run is not { HasRun: true })
        {
            return;
        }

        _run.Apply(new TransferItem(fromPlayerId, toPlayerId));
        Team = TeamOf(_run.State!, Catalog, _run.Systems);
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b>: fuerza un catálogo y lo que se lleva en los huecos sin tocar
    /// la run ni escribir nada de verdad. No hace nada si ya hay una run real detrás.
    /// </summary>
    public void ForceTestConsumables(ConsumableCatalog catalog, IReadOnlyList<EquippedConsumable> equipped)
    {
        if (_run is { HasRun: true })
        {
            return;
        }

        _testConsumables = catalog;
        _testEquippedConsumables = new List<EquippedConsumable>(equipped);
    }

    /// <summary>Jugador alineado en esa casilla, o null.</summary>
    public PlayerDefinition? At(Cell cell)
    {
        foreach (var slot in Lineup.Slots)
        {
            if (slot.HomeCell == cell)
            {
                return Find(slot.PlayerId);
            }
        }

        return null;
    }

    /// <summary>Casilla-hogar del jugador, o null si está en el banquillo.</summary>
    public Cell? CellOf(int playerId)
    {
        foreach (var slot in Lineup.Slots)
        {
            if (slot.PlayerId == playerId)
            {
                return slot.HomeCell;
            }
        }

        return null;
    }

    /// <summary>True si el jugador está en la alineación.</summary>
    public bool IsStarter(int playerId) => CellOf(playerId) is not null;

    /// <summary>Alineación resultante de dejar al jugador en esa casilla, <b>sin</b> aplicarla (RF-045: previsualización).</summary>
    public Lineup Preview(int playerId, Cell target) => PlacementView.WithPlayerAt(Lineup, Players, playerId, target);

    /// <summary>
    /// Por qué un movimiento no se va a poder hacer (BX-1), o null si se puede: la clave de texto y su argumento. La
    /// regla es la de <c>/Sim</c> —<c>RunLineup.CanStart</c> y <see cref="PlacementView.WithPlayerAt"/>—; aquí solo se
    /// pregunta cuál de ellas es la que dice que no, para contárselo al jugador en vez de dejarlo en silencio.
    /// </summary>
    public (string Key, string Name)? RefusalOf(int playerId, Cell target)
    {
        var player = Find(playerId);
        if (player is null)
        {
            return null;
        }

        if (_run?.State?.FindPlayer(playerId) is { } member && (member.IsCrippled || member.PhysicalState == PhysicalState.Dead))
        {
            return ("ui.team.refuse.out", player.Name);
        }

        if (!ReferenceEquals(Preview(playerId, target), Lineup))
        {
            return null;
        }

        if (!PlacementView.CanPlace(player.Position, target))
        {
            return ("ui.team.refuse.cell", player.Name);
        }

        return !IsStarter(playerId) && At(target) is null ? ("ui.team.refuse.full", player.Name) : null;
    }

    /// <summary>
    /// Aplica el movimiento. La regla es de <c>/Sim</c>; aquí solo se pide y se guarda el resultado.
    /// <para>
    /// Con una run detrás, la alineación no se guarda en esta clase: se le manda al motor como
    /// <c>SetLineup</c>, que es la única puerta por la que una decisión del jugador entra en el estado
    /// —y la que deja anotado quién sale al campo arrastrando una lesión grave (RF-093 vía 1)—. La
    /// pantalla no guarda una copia paralela de nada.
    /// </para>
    /// </summary>
    public bool Move(int playerId, Cell target)
    {
        if (RefusalOf(playerId, target) is { Key: "ui.team.refuse.out" })
        {
            return false;
        }

        var next = Preview(playerId, target);
        if (ReferenceEquals(next, Lineup))
        {
            return false;
        }

        if (_run is { HasRun: true })
        {
            // Mover una ficha no quita la decisión de jugar sin relleno (RF-002d, BC-H): se conserva tal cual está.
            _run.Apply(new SetLineup(next, RunLineup.PlaysShort(_run.State!)));
            Team = TeamOf(_run.State!, Catalog, _run.Systems);
            return true;
        }

        Team = Team with { Lineup = next };
        return true;
    }
}
