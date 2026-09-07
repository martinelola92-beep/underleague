using System;
using System.Collections.Generic;
using System.Text;
using Underleague.Sim.Data;

namespace Underleague.Game.Ui;

/// <summary>
/// Marca las frases de zona de inicio dentro de una descripción ya generada (AW-F).
/// <para>
/// <c>Sim.Perks.DescriptionGenerator</c> devuelve <b>texto plano</b> y así se queda: lo consumen también
/// el aviso de <c>TeamScreen</c>, la pantalla de Ojeo y los tests de descripción, y ninguno entiende de
/// marcado. Lo que hace esta clase es un paso posterior y sólo de interfaz: busca en ese texto las seis
/// frases con las que los perks nombran los tercios y las bandas —<c>startZones</c> y <c>startFlanks</c>
/// de <c>data/l10n</c>, las mismas que rotula la cuadrícula (RT-073)— y las envuelve en BBCode para que
/// <see cref="Godot.RichTextLabel"/> les ponga tooltip nativo (<c>[hint]</c>) y avise al pasar el ratón
/// por encima (<c>[url]</c> más <c>meta_hover_started</c>).
/// </para>
/// <para>
/// Las frases <b>no se escriben aquí</b>: se leen de las plantillas en cada llamada, así que cambiar el
/// texto de un tercio en <c>data/l10n</c> no deja el resaltado buscando una frase que ya no existe.
/// </para>
/// </summary>
public static class ZoneHintText
{
    /// <summary>Tipo de resaltado que pide un tercio de inicio (<c>startsIn</c>).</summary>
    public const string ZoneKind = "zone";

    /// <summary>Tipo de resaltado que pide una banda de inicio (<c>startsOn</c>).</summary>
    public const string FlankKind = "flank";

    /// <summary>Claves de los tres tercios, en orden de <c>StartZone</c>.</summary>
    public static readonly string[] ZoneKeys = { "OwnThird", "Middle", "AttackingThird" };

    /// <summary>Claves de las tres bandas, en orden de <c>StartFlank</c>.</summary>
    public static readonly string[] FlankKeys = { "LeftFlank", "Center", "RightFlank" };

    private const string ZoneSection = "startZones";
    private const string FlankSection = "startFlanks";

    /// <summary>Sufijo de la clave con la explicación larga; la corta no lleva sufijo.</summary>
    private const string HintSuffix = "Hint";

    /// <summary>Una frase de zona localizada dentro del texto, con lo que hay que decir de ella.</summary>
    public readonly record struct ZoneSpan(int Start, int Length, string Kind, string Key, string Hint)
    {
        /// <summary>Carga de <c>[url=...]</c>: lo que llega al manejador de <c>meta_hover_started</c>.</summary>
        public string Payload => Kind + ":" + Key;
    }

    /// <summary>True si el texto nombra alguna de las seis zonas.</summary>
    public static bool Mentions(string text, DescriptionTemplates templates) => Spans(text, templates).Count > 0;

    /// <summary>
    /// Frases de zona del texto, sin solaparse y en orden de aparición. De cada frase se marca sólo su
    /// <b>primera</b> aparición: un perk que nombra dos veces el mismo tercio no gana nada repitiendo el
    /// mismo tooltip, y así el marcado es el mismo lo largo que sea la descripción.
    /// </summary>
    public static IReadOnlyList<ZoneSpan> Spans(string text, DescriptionTemplates templates)
    {
        var found = new List<ZoneSpan>();
        Collect(found, text, templates, ZoneSection, ZoneKeys, ZoneKind);
        Collect(found, text, templates, FlankSection, FlankKeys, FlankKind);

        // Orden de aparición y, a igualdad, la frase más larga primero: "su tercio central" gana a
        // cualquier frase más corta que empezara en el mismo sitio.
        found.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));

        var result = new List<ZoneSpan>(found.Count);
        int end = 0;
        foreach (var span in found)
        {
            if (span.Start >= end)
            {
                result.Add(span);
                end = span.Start + span.Length;
            }
        }

        return result;
    }

    /// <summary>Versión BBCode del texto, o null si no nombra ninguna zona (entonces no hace falta marcado).</summary>
    public static string? Markup(string text, DescriptionTemplates templates) =>
        Markup(text, new[] { text }, templates);

    /// <summary>
    /// Versión BBCode de un texto <b>ya partido en líneas</b> por <c>Style.Wrap</c>. Se marca sobre las
    /// líneas ya envueltas, y no dejando que el <c>RichTextLabel</c> envuelva por su cuenta, porque el
    /// alto de la ficha se calcula contando líneas de <c>Style.Wrap</c>: si el label partiera el texto
    /// por su lado y le salieran otras tantas, la ficha dejaría un hueco o se comería la sección
    /// siguiente. Una frase partida por el salto se marca en los dos trozos, con el mismo tooltip.
    /// </summary>
    public static string? Markup(string text, IReadOnlyList<string> lines, DescriptionTemplates templates)
    {
        var spans = Spans(text, templates);
        if (spans.Count == 0)
        {
            return null;
        }

        var offsets = Offsets(text, lines);
        return offsets is null
            ? Build(text, new[] { text }, new[] { 0 }, spans)
            : Build(text, lines, offsets, spans);
    }

    /// <summary>
    /// Posición en el texto original de cada línea envuelta. <c>Style.Wrap</c> sólo se come el espacio
    /// del corte, así que cada línea sigue siendo un trozo literal del original; si alguna no lo fuera
    /// se devuelve null y el marcado se hace sin envolver, que se ve peor pero nunca marca de menos.
    /// </summary>
    private static int[]? Offsets(string text, IReadOnlyList<string> lines)
    {
        var offsets = new int[lines.Count];
        int cursor = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            int at = text.IndexOf(lines[i], cursor, StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }

            offsets[i] = at;
            cursor = at + lines[i].Length;
        }

        return offsets;
    }

    /// <summary>
    /// En qué línea envuelta cae una frase y qué texto la precede dentro de esa línea. Lo usa la
    /// secuencia de capturas para llevar el ratón encima de la frase de verdad; medir el ancho de
    /// <paramref name="prefix"/> con la fuente da la x, y <paramref name="line"/> la fila.
    /// </summary>
    public static bool TryLocate(
        string text,
        IReadOnlyList<string> lines,
        ZoneSpan span,
        out int line,
        out string prefix,
        out string phrase)
    {
        line = 0;
        prefix = string.Empty;
        phrase = string.Empty;

        var offsets = Offsets(text, lines);
        if (offsets is null)
        {
            return false;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            int start = offsets[i];
            int end = start + lines[i].Length;
            if (span.Start < start || span.Start >= end)
            {
                continue;
            }

            line = i;
            prefix = text[start..span.Start];
            phrase = text[span.Start..Math.Min(span.Start + span.Length, end)];
            return true;
        }

        return false;
    }

    private static string Build(string text, IReadOnlyList<string> lines, int[] offsets, IReadOnlyList<ZoneSpan> spans)
    {
        var builder = new StringBuilder(text.Length + (spans.Count * 96));
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            int lineStart = offsets[i];
            int lineEnd = lineStart + lines[i].Length;
            int cursor = lineStart;

            foreach (var span in spans)
            {
                int from = Math.Max(span.Start, lineStart);
                int to = Math.Min(span.Start + span.Length, lineEnd);
                if (to <= from)
                {
                    continue;
                }

                Escape(builder, text.AsSpan(cursor, from - cursor));
                builder.Append("[url=").Append(span.Payload)
                    .Append("][hint=\"").Append(Attribute(span.Hint))
                    .Append("\"][color=#").Append(Style.Accent.ToHtml(false)).Append("][u]");
                Escape(builder, text.AsSpan(from, to - from));
                builder.Append("[/u][/color][/hint][/url]");
                cursor = to;
            }

            Escape(builder, text.AsSpan(cursor, lineEnd - cursor));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Tipo y clave de la carga de <c>[url=...]</c>. El manejador recibe lo que el propio marcado puso,
    /// pero se valida igual contra las seis claves conocidas: un resaltado se traduce en pintar una
    /// franja del campo, y ahí no entra una clave que no sea de la lista.
    /// </summary>
    public static bool TryParse(string meta, out string kind, out string key)
    {
        kind = string.Empty;
        key = string.Empty;
        int separator = meta.IndexOf(':');
        if (separator <= 0 || separator == meta.Length - 1)
        {
            return false;
        }

        string candidateKind = meta[..separator];
        string candidateKey = meta[(separator + 1)..];
        string[] keys = candidateKind switch
        {
            ZoneKind => ZoneKeys,
            FlankKind => FlankKeys,
            _ => Array.Empty<string>(),
        };

        foreach (string known in keys)
        {
            if (string.Equals(known, candidateKey, StringComparison.Ordinal))
            {
                kind = candidateKind;
                key = candidateKey;
                return true;
            }
        }

        return false;
    }

    private static void Collect(
        List<ZoneSpan> into,
        string text,
        DescriptionTemplates templates,
        string section,
        string[] keys,
        string kind)
    {
        foreach (string key in keys)
        {
            string? phrase = templates.Find(section, key);
            if (string.IsNullOrEmpty(phrase))
            {
                continue;
            }

            int at = text.IndexOf(phrase, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            // Sin explicación larga se marca igual y el tooltip repite la frase corta: es un texto que
            // falta en /data, y un texto que falta debe verse, no dejar la frase muda.
            into.Add(new ZoneSpan(at, phrase.Length, kind, key, templates.Find(section, key + HintSuffix) ?? phrase));
        }
    }

    /// <summary>El único carácter que abre etiqueta en BBCode. Ningún nombre debería traerlo, pero puede.</summary>
    private static void Escape(StringBuilder builder, ReadOnlySpan<char> text)
    {
        foreach (char c in text)
        {
            if (c == '[')
            {
                builder.Append("[lb]");
            }
            else
            {
                builder.Append(c);
            }
        }
    }

    /// <summary>Valor de un atributo entrecomillado: ni comillas que lo cierren antes de tiempo ni corchetes.</summary>
    private static string Attribute(string text) =>
        text.Replace('"', '\'').Replace('[', '(').Replace(']', ')');
}
