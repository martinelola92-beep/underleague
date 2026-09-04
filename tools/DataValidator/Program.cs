// Valida los ficheros JSON de /data contra sus esquemas (JSON Schema draft 2020-12, JsonSchema.Net)
// y, si están disponibles, los carga con Underleague.Sim.Data.DataLoader. RT-032, RT-083.
using System.Text.Json;
using Json.Schema;
using Underleague.Sim.Data;

var dataDir = ResolveDataDir(args);
if (dataDir is null)
{
    Console.WriteLine("ERROR: no se encontró el directorio 'data' subiendo desde el directorio actual.");
    return 1;
}

var schemasDir = Path.Combine(dataDir, "schemas");
var jsonFiles = Directory.EnumerateFiles(dataDir, "*.json", SearchOption.AllDirectories)
    .Where(f => !IsUnder(f, schemasDir))
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();

var errorCount = 0;
var fileContents = new Dictionary<string, string>();
var schemaCache = new Dictionary<string, JsonSchema>();

foreach (var file in jsonFiles)
{
    var rel = ToRelative(dataDir, file);
    string text;
    try
    {
        text = File.ReadAllText(file);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR {rel}: no se pudo leer el fichero ({ex.Message})");
        errorCount++;
        continue;
    }

    fileContents[rel] = text;

    var schemaFileName = MapSchemaFile(rel);
    if (schemaFileName is null)
    {
        Console.WriteLine($"ERROR {rel}: no hay esquema asignado para este fichero");
        errorCount++;
        continue;
    }

    var schemaPath = Path.Combine(schemasDir, schemaFileName);
    if (!File.Exists(schemaPath))
    {
        Console.WriteLine($"ERROR {rel}: falta el esquema '{schemaFileName}' en data/schemas/");
        errorCount++;
        continue;
    }

    JsonSchema schema;
    try
    {
        schema = GetSchema(schemaCache, schemaPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR {rel}: esquema '{schemaFileName}' inválido ({ex.Message})");
        errorCount++;
        continue;
    }

    JsonDocument doc;
    try
    {
        doc = JsonDocument.Parse(text);
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"ERROR {rel}: JSON inválido ({ex.Message})");
        errorCount++;
        continue;
    }

    using (doc)
    {
        var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        var results = schema.Evaluate(doc.RootElement, options);
        if (results.IsValid)
        {
            Console.WriteLine($"OK {rel}");
        }
        else
        {
            errorCount++;
            var messages = DescribeErrors(results);
            Console.WriteLine(messages.Count == 0
                ? $"ERROR {rel}: no cumple el esquema '{schemaFileName}'"
                : $"ERROR {rel}: {string.Join("; ", messages)}");
        }
    }
}

try
{
    DataLoader.FromJson(fileContents);
    Console.WriteLine("OK DataLoader.FromJson");
}
catch (DataException ex)
{
    errorCount++;
    Console.WriteLine($"ERROR {ex.File}: {ex.JsonPath} {ex.Message}");
}
catch (Exception ex)
{
    errorCount++;
    Console.WriteLine($"ERROR DataLoader.FromJson: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine($"{jsonFiles.Count} ficheros, {errorCount} errores");
return errorCount > 0 ? 1 : 0;

static string? ResolveDataDir(string[] args)
{
    if (args.Length > 0)
    {
        var provided = Path.GetFullPath(args[0]);
        return Directory.Exists(provided) ? provided : null;
    }

    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "data");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        dir = dir.Parent;
    }

    return null;
}

static bool IsUnder(string file, string dir)
{
    var fullFile = Path.GetFullPath(file);
    var fullDir = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
    return fullFile.StartsWith(fullDir, StringComparison.Ordinal);
}

static string ToRelative(string dataDir, string file)
{
    return Path.GetRelativePath(dataDir, file).Replace(Path.DirectorySeparatorChar, '/');
}

static string? MapSchemaFile(string relativePath)
{
    if (relativePath.StartsWith("races/", StringComparison.Ordinal) && relativePath.EndsWith(".json", StringComparison.Ordinal)
        && !relativePath[6..].Contains('/'))
    {
        return "races.schema.json";
    }

    if (relativePath.StartsWith("perks/", StringComparison.Ordinal) && relativePath.EndsWith(".json", StringComparison.Ordinal)
        && !relativePath[6..].Contains('/'))
    {
        return "perks.schema.json";
    }

    // l10n/<lang>/templates.json
    if (relativePath.StartsWith("l10n/", StringComparison.Ordinal) && relativePath.EndsWith("/templates.json", StringComparison.Ordinal)
        && relativePath.Count(c => c == '/') == 2)
    {
        return "l10n-templates.schema.json";
    }

    if (relativePath.StartsWith("balance/builds/", StringComparison.Ordinal) && relativePath.EndsWith(".json", StringComparison.Ordinal)
        && !relativePath[15..].Contains('/'))
    {
        return "balance-builds.schema.json";
    }

    if (relativePath.StartsWith("bosses/", StringComparison.Ordinal) && relativePath.EndsWith(".json", StringComparison.Ordinal)
        && !relativePath[7..].Contains('/'))
    {
        return "bosses.schema.json";
    }

    if (relativePath.StartsWith("items/", StringComparison.Ordinal) && relativePath.EndsWith(".json", StringComparison.Ordinal)
        && !relativePath[6..].Contains('/'))
    {
        return "items.schema.json";
    }

    if (relativePath.StartsWith("rivals/", StringComparison.Ordinal) && relativePath.EndsWith(".json", StringComparison.Ordinal)
        && !relativePath[7..].Contains('/'))
    {
        return "rivals.schema.json";
    }

    if (relativePath.StartsWith("consumables/", StringComparison.Ordinal) && relativePath.EndsWith(".json", StringComparison.Ordinal)
        && !relativePath[12..].Contains('/'))
    {
        return "consumables.schema.json";
    }

    return relativePath switch
    {
        "balance/groups.json" => "balance-groups.schema.json",
        "traits/traits.json" => "traits.schema.json",
        "ai/weights.json" => "ai-weights.schema.json",
        "sim/tuning.json" => "tuning.schema.json",
        "balance/reference.json" => "balance-reference.schema.json",
        "tags/styles.json" => "styles.schema.json",
        "economy/economy.json" => "economy.schema.json",
        "economy/perk-values.json" => "perk-values.schema.json",
        "equipment/equipment.json" => "equipment.schema.json",
        "map/map.json" => "map.schema.json",
        _ => null,
    };
}

static JsonSchema GetSchema(Dictionary<string, JsonSchema> cache, string schemaPath)
{
    if (cache.TryGetValue(schemaPath, out var cached))
    {
        return cached;
    }

    var schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
    cache[schemaPath] = schema;
    return schema;
}

static List<string> DescribeErrors(EvaluationResults results)
{
    var messages = new List<string>();
    Collect(results);
    return messages;

    void Collect(EvaluationResults node)
    {
        if (node.Errors is { Count: > 0 })
        {
            var pointer = node.InstanceLocation.ToString();
            foreach (var kv in node.Errors.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                messages.Add(string.IsNullOrEmpty(pointer)
                    ? $"(raíz) [{kv.Key}] {kv.Value}"
                    : $"{pointer} [{kv.Key}] {kv.Value}");
            }
        }

        if (node.Details is { Count: > 0 })
        {
            foreach (var child in node.Details)
            {
                Collect(child);
            }
        }
    }
}
