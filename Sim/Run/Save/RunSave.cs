using System.Buffers;
using System.Text;
using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Save;

/// <summary>Error de serialización o de carga de un guardado de run, con la ruta JSON donde ocurrió.</summary>
public sealed class RunSaveException : Exception
{
    /// <summary>Ruta JSON del problema, estilo <c>$.roster[2].level</c>.</summary>
    public string JsonPath { get; }

    /// <summary>Construye el error con su ruta JSON.</summary>
    public RunSaveException(string jsonPath, string message)
        : base($"{jsonPath}: {message}") => JsonPath = jsonPath;
}

/// <summary>
/// Serialización del estado de la run (RT-060, RT-061, RT-061b). <b>No toca el disco</b>: convierte a
/// texto y desde texto, y quien escribe el fichero es el llamador (<c>/Game</c>, <c>/Balance</c>),
/// igual que con <c>DataLoader.FromJson</c> (RT-012).
///
/// <para><b>Instantánea de <c>/data</c></b> (RT-061b): el guardado lleva dentro la copia de los
/// ficheros de datos con la que empezó la run. Al cargar, el catálogo sale de esa copia
/// (<see cref="CatalogFromSnapshot"/>), nunca del <c>/data</c> del disco, así que una actualización del
/// juego no altera una run en curso ni invalida sus repeticiones.</para>
///
/// <para><b>Ironman</b> (RT-061): la política -un slot, guardar al completar cada nodo, borrar al
/// cargar- la aplica el llamador. Lo que garantiza este paquete es que el estado guardado <b>basta</b>
/// para reproducirlo todo: como el partido de un nodo se deriva de <c>RngStreams.MatchSeed(seed,
/// nodeId)</c> y del estado de la plantilla, salir a mitad de un partido y volver reproduce
/// exactamente el mismo partido. No hace falta guardar nada del partido en curso.</para>
///
/// <para><b>Versionado</b>: el guardado lleva <c>schemaVersion</c> y cargar otra versión es un error
/// explícito. Nunca se migra en silencio (<c>modelo-datos.md</c>, "Versionado").</para>
/// </summary>
public static class RunSave
{
    /// <summary>Versión de esquema que escribe y acepta este código.</summary>
    public const int SchemaVersion = RunState.CurrentSchemaVersion;

    /// <summary>Serializa el estado a JSON. <paramref name="indented"/> solo afecta al formato.</summary>
    public static string Save(RunState state, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = indented }))
        {
            WriteState(writer, state);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Carga un estado desde el JSON de <see cref="Save"/>. Lanza <see cref="RunSaveException"/> con
    /// fichero y ruta si la versión no coincide o si falta algo obligatorio.
    /// </summary>
    public static RunState Load(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        using var document = Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new RunSaveException("$", "el guardado debe ser un objeto JSON");
        }

        int version = Int(root, "schemaVersion", "$");
        if (version != SchemaVersion)
        {
            throw new RunSaveException(
                "$.schemaVersion",
                $"el guardado es de la versión {version} y este código escribe la {SchemaVersion}. "
                    + "Una run guardada con otra versión se migra explícitamente o se rechaza; nunca se migra en silencio "
                    + "(modelo-datos.md, \"Versionado\").");
        }

        var state = new RunState
        {
            SchemaVersion = version,
            Seed = Seed(root, "$"),
            Division = Enum<Division>(root, "division", "$"),
            ClubId = Str(root, "club", "$"),
            ClubRace = Enum<Race>(root, "clubRace", "$"),
            Act = Int(root, "act", "$"),
            CurrentNodeId = Int(root, "currentNode", "$"),
            PendingNodeId = Int(root, "pendingNode", "$"),
            Phase = Enum<RunPhase>(root, "phase", "$"),
            Gold = Int(root, "gold", "$"),
            RerollsUsed = Int(root, "rerollsUsed", "$"),
            NodeRerolls = Int(root, "nodeRerolls", "$"),
            Result = ReadOutcome(Prop(root, "result", "$"), "$.result"),
            NodeHistory = ReadHistory(root),
            Maps = ReadMaps(root),
            Referees = ReadReferees(root),
            Lineup = ReadLineup(root),
            Consumables = ReadConsumables(root),
            Counters = ReadInts(root, "counters", "$"),
            Achievements = ReadInts(root, "achievements", "$"),
            DataSnapshot = ReadStrings(root, "dataSnapshot", "$"),
        };

        // Los catálogos de objetos y consumibles de la run son dato derivado de la instantánea y no se
        // guardan; se reconstruyen aquí, que es lo que hace que una run cargada siga jugando con el
        // equipamiento con el que empezó (RT-061b, RunEquipment).
        state = state.WithDataSnapshot(state.DataSnapshot).WithRoster(ReadRoster(root));

        // nextPlayerId manda sobre el que deduce WithRoster: una run que ha vendido a su último fichaje
        // no puede reutilizar su id (determinismo.md, "Orden").
        int nextPlayerId = Int(root, "nextPlayerId", "$");
        return nextPlayerId > state.NextPlayerId ? state with { NextPlayerId = nextPlayerId } : state;
    }

    /// <summary>
    /// Catálogo construido con la instantánea de <c>/data</c> de la run (RT-061b). Es el catálogo con el
    /// que hay que seguir jugándola, no el del <c>/data</c> actual.
    /// </summary>
    public static Catalog CatalogFromSnapshot(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.DataSnapshot.Count == 0)
        {
            throw new RunSaveException("$.dataSnapshot", "la run no lleva instantánea de /data (RT-061b)");
        }

        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, content) in state.DataSnapshot)
        {
            files[path] = content;
        }

        return DataLoader.FromJson(files);
    }

    // ------------------------------------------------------------------ escritura

    private static void WriteState(Utf8JsonWriter w, RunState state)
    {
        w.WriteStartObject();
        w.WriteNumber("schemaVersion", state.SchemaVersion);
        w.WriteString("seed", state.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteString("division", Camel(state.Division.ToString()));
        w.WriteString("club", state.ClubId);
        w.WriteString("clubRace", Camel(state.ClubRace.ToString()));
        w.WriteNumber("act", state.Act);
        w.WriteNumber("currentNode", state.CurrentNodeId);
        w.WriteNumber("pendingNode", state.PendingNodeId);
        w.WriteString("phase", Camel(state.Phase.ToString()));
        w.WriteNumber("gold", state.Gold);
        w.WriteNumber("rerollsUsed", state.RerollsUsed);
        w.WriteNumber("nodeRerolls", state.NodeRerolls);
        w.WriteNumber("nextPlayerId", state.NextPlayerId);

        w.WriteStartObject("result");
        w.WriteString("kind", Camel(state.Result.Kind.ToString()));
        w.WriteString("cause", Camel(state.Result.Cause.ToString()));
        w.WriteNumber("nodeId", state.Result.NodeId);
        w.WriteNumber("tick", state.Result.Tick);
        w.WriteEndObject();

        w.WriteStartArray("nodeHistory");
        for (int i = 0; i < state.NodeHistory.Count; i++)
        {
            var entry = state.NodeHistory[i];
            w.WriteStartObject();
            w.WriteNumber("nodeId", entry.NodeId);
            w.WriteString("kind", Camel(entry.Kind.ToString()));
            w.WriteString("result", Camel(entry.Result.ToString()));
            w.WriteEndObject();
        }

        w.WriteEndArray();

        w.WriteStartArray("maps");
        for (int i = 0; i < state.Maps.Count; i++)
        {
            WriteMap(w, state.Maps[i]);
        }

        w.WriteEndArray();

        w.WriteStartArray("referees");
        for (int i = 0; i < state.Referees.Count; i++)
        {
            var referee = state.Referees[i];
            w.WriteStartObject();
            w.WriteNumber("id", referee.Id);
            w.WriteString("name", referee.Name);
            w.WriteString("trait", Camel(referee.Trait.ToString()));
            w.WriteNumber("bribesReceived", referee.BribesReceived);
            w.WriteEndObject();
        }

        w.WriteEndArray();

        w.WriteStartArray("roster");
        for (int i = 0; i < state.Roster.Count; i++)
        {
            WritePlayer(w, state.Roster[i]);
        }

        w.WriteEndArray();

        w.WriteStartArray("lineup");
        for (int i = 0; i < state.Lineup.Slots.Count; i++)
        {
            var slot = state.Lineup.Slots[i];
            w.WriteStartObject();
            w.WriteNumber("playerId", slot.PlayerId);
            w.WriteNumber("column", slot.HomeCell.Column);
            w.WriteNumber("row", slot.HomeCell.Row);
            w.WriteEndObject();
        }

        w.WriteEndArray();

        w.WriteStartArray("consumables");
        for (int i = 0; i < state.Consumables.Count; i++)
        {
            var consumable = state.Consumables[i];
            w.WriteStartObject();
            w.WriteString("id", consumable.Id);
            w.WriteString("mode", Camel(consumable.Mode.ToString()));
            w.WriteString("trigger", consumable.Trigger);
            w.WriteEndObject();
        }

        w.WriteEndArray();

        WriteInts(w, "counters", state.Counters);
        WriteInts(w, "achievements", state.Achievements);

        w.WriteStartObject("dataSnapshot");
        foreach (var (path, content) in Sorted(state.DataSnapshot))
        {
            w.WriteString(path, content);
        }

        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteMap(Utf8JsonWriter w, ActMap map)
    {
        w.WriteStartObject();
        w.WriteNumber("act", map.Act);
        w.WriteNumber("bossNodeId", map.BossNodeId);
        w.WriteString("bossModifierId", map.BossModifierId);
        w.WriteBoolean("bossModifierRevealed", map.BossModifierRevealed);

        w.WriteStartArray("entryNodeIds");
        for (int i = 0; i < map.EntryNodeIds.Count; i++)
        {
            w.WriteNumberValue(map.EntryNodeIds[i]);
        }

        w.WriteEndArray();

        w.WriteStartArray("nodes");
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            w.WriteStartObject();
            w.WriteNumber("id", node.Id);
            w.WriteNumber("layer", node.Layer);
            w.WriteNumber("indexInLayer", node.IndexInLayer);
            w.WriteString("kind", Camel(node.Kind.ToString()));
            w.WriteStartArray("next");
            for (int e = 0; e < node.Next.Count; e++)
            {
                w.WriteNumberValue(node.Next[e]);
            }

            w.WriteEndArray();
            w.WriteString("opponentId", node.OpponentId);
            w.WriteNumber("difficulty", node.Difficulty);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WritePlayer(Utf8JsonWriter w, RunPlayer player)
    {
        w.WriteStartObject();
        w.WriteNumber("id", player.Id);
        w.WriteString("name", player.Name);
        w.WriteString("race", Camel(player.Race.ToString()));
        w.WriteString("position", Camel(player.Position.ToString()));
        w.WriteString("rarity", Camel(player.Rarity.ToString()));
        w.WriteNumber("level", player.Level);
        w.WriteNumber("experience", player.Experience);

        w.WriteStartObject("attributes");
        w.WriteNumber("strength", player.Attributes.Strength);
        w.WriteNumber("speed", player.Attributes.Speed);
        w.WriteNumber("technique", player.Attributes.Technique);
        w.WriteNumber("stamina", player.Attributes.Stamina);
        w.WriteNumber("leash", player.Attributes.Leash);
        w.WriteEndObject();

        w.WriteStartArray("traits");
        for (int i = 0; i < player.Traits.Count; i++)
        {
            w.WriteStringValue(Camel(player.Traits[i].ToString()));
        }

        w.WriteEndArray();

        w.WriteStartArray("tags");
        for (int i = 0; i < player.Tags.Count; i++)
        {
            w.WriteStringValue(player.Tags[i]);
        }

        w.WriteEndArray();

        w.WriteString("speciesTag", player.SpeciesTag);
        w.WriteString("styleTag", Camel(player.StyleTag.ToString()));

        w.WriteStartArray("perks");
        for (int i = 0; i < player.Perks.Count; i++)
        {
            w.WriteStringValue(player.Perks[i]);
        }

        w.WriteEndArray();

        if (player.Item is null)
        {
            w.WriteNull("item");
        }
        else
        {
            w.WriteString("item", player.Item);
        }

        w.WriteString("physicalState", Camel(player.PhysicalState.ToString()));
        w.WriteNumber("minorInjuries", player.MinorInjuries);

        w.WriteStartArray("prostheses");
        for (int i = 0; i < player.Prostheses.Count; i++)
        {
            w.WriteStartObject();
            w.WriteString("slot", player.Prostheses[i].Slot);
            w.WriteString("effect", player.Prostheses[i].Effect);
            w.WriteEndObject();
        }

        w.WriteEndArray();

        w.WriteNumber("wage", player.Wage);
        w.WriteBoolean("isMercenary", player.IsMercenary);
        w.WriteBoolean("isYouth", player.IsYouth);
        w.WriteNumber("matchesBenched", player.MatchesBenched);

        w.WriteStartArray("bonds");
        for (int i = 0; i < player.Bonds.Count; i++)
        {
            w.WriteStartObject();
            w.WriteNumber("otherPlayerId", player.Bonds[i].OtherPlayerId);
            w.WriteString("kind", Camel(player.Bonds[i].Kind.ToString()));
            w.WriteEndObject();
        }

        w.WriteEndArray();

        w.WriteNumber("mourning", player.Mourning);
        WriteInts(w, "counters", player.Counters);
        WriteInts(w, "bondProgress", player.BondProgress);
        w.WriteEndObject();
    }

    private static void WriteInts(Utf8JsonWriter w, string name, IReadOnlyDictionary<string, int> values)
    {
        w.WriteStartObject(name);
        foreach (var (key, value) in SortedInts(values))
        {
            w.WriteNumber(key, value);
        }

        w.WriteEndObject();
    }

    /// <summary>Recorrido ordenado por clave ordinal: un diccionario sin ordenar rompería el determinismo del texto.</summary>
    private static IEnumerable<KeyValuePair<string, string>> Sorted(IReadOnlyDictionary<string, string> values)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            sorted[key] = value;
        }

        return sorted;
    }

    private static IEnumerable<KeyValuePair<string, int>> SortedInts(IReadOnlyDictionary<string, int> values)
    {
        var sorted = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            sorted[key] = value;
        }

        return sorted;
    }

    // ------------------------------------------------------------------ lectura

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new RunSaveException("$", $"el guardado no es JSON válido: {e.Message}");
        }
    }

    private static List<NodeHistoryEntry> ReadHistory(JsonElement root)
    {
        var history = new List<NodeHistoryEntry>();
        var array = Prop(root, "nodeHistory", "$");
        int index = 0;
        foreach (var entry in array.EnumerateArray())
        {
            string path = $"$.nodeHistory[{index++}]";
            history.Add(new NodeHistoryEntry(
                Int(entry, "nodeId", path),
                Enum<NodeKind>(entry, "kind", path),
                Enum<NodeResult>(entry, "result", path)));
        }

        return history;
    }

    private static List<ActMap> ReadMaps(JsonElement root)
    {
        var maps = new List<ActMap>();
        var array = Prop(root, "maps", "$");
        int index = 0;
        foreach (var element in array.EnumerateArray())
        {
            string path = $"$.maps[{index++}]";
            var nodes = new List<MapNode>();
            int act = Int(element, "act", path);
            int nodeIndex = 0;
            foreach (var nodeElement in Prop(element, "nodes", path).EnumerateArray())
            {
                string nodePath = $"{path}.nodes[{nodeIndex++}]";
                var next = new List<int>();
                foreach (var target in Prop(nodeElement, "next", nodePath).EnumerateArray())
                {
                    next.Add(target.GetInt32());
                }

                nodes.Add(new MapNode(
                    Int(nodeElement, "id", nodePath),
                    act,
                    Int(nodeElement, "layer", nodePath),
                    Int(nodeElement, "indexInLayer", nodePath),
                    Enum<NodeKind>(nodeElement, "kind", nodePath),
                    next,
                    Str(nodeElement, "opponentId", nodePath),
                    Int(nodeElement, "difficulty", nodePath)));
            }

            var entries = new List<int>();
            foreach (var entry in Prop(element, "entryNodeIds", path).EnumerateArray())
            {
                entries.Add(entry.GetInt32());
            }

            maps.Add(new ActMap(
                act,
                nodes,
                entries,
                Int(element, "bossNodeId", path),
                Str(element, "bossModifierId", path),
                Bool(element, "bossModifierRevealed", path)));
        }

        return maps;
    }

    private static List<RunReferee> ReadReferees(JsonElement root)
    {
        var referees = new List<RunReferee>();
        int index = 0;
        foreach (var element in Prop(root, "referees", "$").EnumerateArray())
        {
            string path = $"$.referees[{index++}]";
            referees.Add(new RunReferee(
                Int(element, "id", path),
                Str(element, "name", path),
                Enum<RefereeTrait>(element, "trait", path),
                Int(element, "bribesReceived", path)));
        }

        return referees;
    }

    private static List<RunPlayer> ReadRoster(JsonElement root)
    {
        var roster = new List<RunPlayer>();
        int index = 0;
        foreach (var element in Prop(root, "roster", "$").EnumerateArray())
        {
            string path = $"$.roster[{index++}]";
            var attributes = Prop(element, "attributes", path);
            var traits = new List<Trait>();
            foreach (var trait in Prop(element, "traits", path).EnumerateArray())
            {
                traits.Add(ParseEnum<Trait>(trait.GetString(), $"{path}.traits"));
            }

            var tags = new List<string>();
            foreach (var tag in Prop(element, "tags", path).EnumerateArray())
            {
                tags.Add(tag.GetString() ?? string.Empty);
            }

            var perks = new List<string>();
            foreach (var perk in Prop(element, "perks", path).EnumerateArray())
            {
                perks.Add(perk.GetString() ?? string.Empty);
            }

            var prostheses = new List<RunProsthesis>();
            foreach (var prosthesis in Prop(element, "prostheses", path).EnumerateArray())
            {
                prostheses.Add(new RunProsthesis(
                    Str(prosthesis, "slot", path),
                    Str(prosthesis, "effect", path)));
            }

            var bonds = new List<RunBond>();
            foreach (var bond in Prop(element, "bonds", path).EnumerateArray())
            {
                bonds.Add(new RunBond(
                    Int(bond, "otherPlayerId", path),
                    Enum<BondKind>(bond, "kind", path)));
            }

            var itemElement = Prop(element, "item", path);
            roster.Add(new RunPlayer(
                Int(element, "id", path),
                Str(element, "name", path),
                Enum<Race>(element, "race", path),
                Enum<Position>(element, "position", path),
                Enum<Rarity>(element, "rarity", path),
                Int(element, "level", path),
                Int(element, "experience", path),
                new Attributes(
                    Int(attributes, "strength", path),
                    Int(attributes, "speed", path),
                    Int(attributes, "technique", path),
                    Int(attributes, "stamina", path),
                    Int(attributes, "leash", path)),
                traits,
                tags,
                Enum<PhysicalState>(element, "physicalState", path))
            {
                SpeciesTag = Str(element, "speciesTag", path),
                StyleTag = Enum<StyleTag>(element, "styleTag", path),
                Perks = perks,
                Item = itemElement.ValueKind == JsonValueKind.Null ? null : itemElement.GetString(),
                MinorInjuries = Int(element, "minorInjuries", path),
                Prostheses = prostheses,
                Wage = Int(element, "wage", path),
                IsMercenary = Bool(element, "isMercenary", path),
                IsYouth = Bool(element, "isYouth", path),
                MatchesBenched = Int(element, "matchesBenched", path),
                Bonds = bonds,
                Mourning = Int(element, "mourning", path),
                Counters = ReadInts(element, "counters", path),
                BondProgress = ReadInts(element, "bondProgress", path),
            });
        }

        return roster;
    }

    private static Lineup ReadLineup(JsonElement root)
    {
        var slots = new List<LineupSlot>();
        int index = 0;
        foreach (var element in Prop(root, "lineup", "$").EnumerateArray())
        {
            string path = $"$.lineup[{index++}]";
            slots.Add(new LineupSlot(
                Int(element, "playerId", path),
                new Cell(Int(element, "column", path), Int(element, "row", path))));
        }

        return new Lineup(slots);
    }

    private static List<EquippedConsumable> ReadConsumables(JsonElement root)
    {
        var consumables = new List<EquippedConsumable>();
        int index = 0;
        foreach (var element in Prop(root, "consumables", "$").EnumerateArray())
        {
            string path = $"$.consumables[{index++}]";
            consumables.Add(new EquippedConsumable(
                Str(element, "id", path),
                Enum<ConsumableMode>(element, "mode", path),
                Str(element, "trigger", path)));
        }

        return consumables;
    }

    private static RunOutcome ReadOutcome(JsonElement element, string path) => new(
        ParseEnum<RunOutcomeKind>(Str(element, "kind", path), path),
        ParseEnum<DefeatCause>(Str(element, "cause", path), path),
        Int(element, "nodeId", path),
        Int(element, "tick", path));

    private static SortedDictionary<string, int> ReadInts(JsonElement parent, string name, string path)
    {
        var values = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (!parent.TryGetProperty(name, out var element))
        {
            return values;
        }

        foreach (var property in element.EnumerateObject())
        {
            values[property.Name] = property.Value.GetInt32();
        }

        return values;
    }

    private static SortedDictionary<string, string> ReadStrings(JsonElement parent, string name, string path)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!parent.TryGetProperty(name, out var element))
        {
            return values;
        }

        foreach (var property in element.EnumerateObject())
        {
            values[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return values;
    }

    // ------------------------------------------------------------------ ayudantes

    private static JsonElement Prop(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var element)
            ? element
            : throw new RunSaveException($"{path}.{name}", "campo obligatorio ausente");

    private static int Int(JsonElement parent, string name, string path)
    {
        var element = Prop(parent, name, path);
        return element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : throw new RunSaveException($"{path}.{name}", $"se esperaba un entero y hay {element.ValueKind}");
    }

    private static bool Bool(JsonElement parent, string name, string path)
    {
        var element = Prop(parent, name, path);
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new RunSaveException($"{path}.{name}", $"se esperaba un booleano y hay {element.ValueKind}"),
        };
    }

    private static string Str(JsonElement parent, string name, string path)
    {
        var element = Prop(parent, name, path);
        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : throw new RunSaveException($"{path}.{name}", $"se esperaba una cadena y hay {element.ValueKind}");
    }

    private static ulong Seed(JsonElement parent, string path)
    {
        string text = Str(parent, "seed", path);
        return ulong.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong seed)
            ? seed
            : throw new RunSaveException($"{path}.seed", $"la semilla '{text}' no es un entero sin signo de 64 bits");
    }

    private static T Enum<T>(JsonElement parent, string name, string path)
        where T : struct, System.Enum =>
        ParseEnum<T>(Str(parent, name, path), $"{path}.{name}");

    /// <summary>
    /// Los valores de enum se escriben en <c>camelCase</c> (<c>modelo-datos.md</c>) y se leen sin
    /// distinguir mayúsculas, que es como los lee también el cargador de <c>/data</c>.
    /// </summary>
    private static T ParseEnum<T>(string? text, string path)
        where T : struct, System.Enum
    {
        if (text is not null && System.Enum.TryParse<T>(text, ignoreCase: true, out var value) && System.Enum.IsDefined(value))
        {
            return value;
        }

        throw new RunSaveException(path, $"'{text}' no es un valor válido de {typeof(T).Name}");
    }

    private static string Camel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
