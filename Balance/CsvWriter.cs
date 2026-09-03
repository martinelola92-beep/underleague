using System.Globalization;
using System.Text;

namespace Underleague.Balance;

/// <summary>Escritura de CSV mínima (sin comillas ni escapes: todos los campos son ids/números simples).</summary>
public static class CsvWriter
{
    public static void Write(string path, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(string.Join(',', header));
        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(',', row));
        }
    }

    /// <summary>Formatea un valor numérico con dos decimales, cultura invariante (§4: "todos los valores con dos decimales").</summary>
    public static string F2(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    public static string Bool(bool value) => value ? "true" : "false";
}
