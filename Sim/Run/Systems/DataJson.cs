using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Cursor de lectura sobre un <see cref="JsonElement"/> con fichero y ruta JSON arrastrados, para poder
/// lanzar <see cref="DataException"/> con contexto preciso (RT-032). Es el mismo patrón que usa
/// <c>Sim.Data.DataLoader</c> para <c>data/perks</c> y <c>data/races</c>, reescrito aquí porque ese tipo
/// es privado de su fichero: el paquete X no toca <c>Sim/Data</c> (fuera de sus fronteras, ADR de
/// paquetes de fase2-diseno.md §12) y carga <c>data/economy</c>, <c>data/items</c>, <c>data/rivals</c> y
/// <c>data/consumables</c> por su cuenta, sin pasar por <see cref="Underleague.Sim.Data.DataLoader"/> ni
/// por <see cref="Underleague.Sim.Data.Catalog"/>. Sin E/S: recibe el contenido ya leído (RT-012).
/// </summary>
internal readonly struct Json
{
    private readonly JsonElement _element;

    public Json(JsonElement element, string file, string path)
    {
        _element = element;
        File = file;
        Path = path;
    }

    public string File { get; }

    public string Path { get; }

    public bool IsNull => _element.ValueKind == JsonValueKind.Null;

    public static Json Root(string file, JsonDocument document) => new(document.RootElement, file, "$");

    public Json Prop(string name)
    {
        if (_element.ValueKind != JsonValueKind.Object)
        {
            throw new DataException(File, Path, $"se esperaba un objeto con la propiedad '{name}'");
        }

        if (!_element.TryGetProperty(name, out var value))
        {
            throw new DataException(File, Path, $"falta la propiedad requerida '{name}'");
        }

        return new Json(value, File, Path + "." + name);
    }

    public Json? TryProp(string name)
    {
        if (_element.ValueKind == JsonValueKind.Object && _element.TryGetProperty(name, out var value))
        {
            return new Json(value, File, Path + "." + name);
        }

        return null;
    }

    public string AsString()
    {
        if (_element.ValueKind != JsonValueKind.String)
        {
            throw new DataException(File, Path, "se esperaba una cadena");
        }

        return _element.GetString()!;
    }

    public int AsInt()
    {
        if (_element.ValueKind != JsonValueKind.Number || !_element.TryGetInt32(out int value))
        {
            throw new DataException(File, Path, "se esperaba un entero");
        }

        return value;
    }

    public bool AsBool()
    {
        if (_element.ValueKind != JsonValueKind.True && _element.ValueKind != JsonValueKind.False)
        {
            throw new DataException(File, Path, "se esperaba un booleano");
        }

        return _element.GetBoolean();
    }

    public IEnumerable<Json> EnumerateArray()
    {
        if (_element.ValueKind != JsonValueKind.Array)
        {
            throw new DataException(File, Path, "se esperaba un array");
        }

        int i = 0;
        foreach (var item in _element.EnumerateArray())
        {
            yield return new Json(item, File, Path + $"[{i}]");
            i++;
        }
    }

    public int Int(string property) => Prop(property).AsInt();

    public int OptionalInt(string property, int fallback) =>
        TryProp(property) is { } value ? value.AsInt() : fallback;

    public string Str(string property) => Prop(property).AsString();

    public string OptionalStr(string property, string fallback) =>
        TryProp(property) is { } value ? value.AsString() : fallback;

    public bool OptionalBool(string property, bool fallback) =>
        TryProp(property) is { } value ? value.AsBool() : fallback;
}

/// <summary>Nombre localizado es/en, con el mismo formato que <c>data/perks/*.json</c> y <c>data/races/*.json</c>.</summary>
internal static class LocalizedNameJson
{
    public static Underleague.Sim.Data.LocalizedName Read(Json node) =>
        new(node.Prop("es").AsString(), node.Prop("en").AsString());
}
