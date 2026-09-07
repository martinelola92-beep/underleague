using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Placement;

namespace Underleague.Game.Ui;

/// <summary>
/// Cuadrícula de colocación de 16x5 (RF-040) con la mitad propia utilizable (RF-041), la zona de acción
/// del jugador manipulado (RF-045, ADR 0029), los vínculos direccionales (RF-044, RF-106) y el modo de
/// cobertura del equipo (ADR 0029 §4).
/// <para>
/// <b>No calcula nada</b> (RT-014): recibe de la pantalla las casillas ya resueltas por
/// <c>Sim.Placement.PlacementView</c> y las pinta. Su única aritmética es pasar de casilla a píxel.
/// </para>
/// </summary>
public partial class PitchView : Control
{
    /// <summary>Botón principal pulsado sobre una casilla (ratón). La pantalla decide qué significa.</summary>
    [Signal]
    public delegate void CellPressedEventHandler(int column, int row);

    /// <summary>Botón principal soltado sobre una casilla (ratón): fin del arrastre.</summary>
    [Signal]
    public delegate void CellReleasedEventHandler(int column, int row);

    /// <summary>El ratón ha entrado en otra casilla: mueve el cursor, igual que la cruceta del mando.</summary>
    [Signal]
    public delegate void CellHoveredEventHandler(int column, int row);

    /// <summary>Datos de la plantilla y la alineación que se está pintando.</summary>
    public TeamState? State { get; set; }

    /// <summary>Casilla bajo el cursor. Es la misma para ratón y mando (UI-006): un único cursor.</summary>
    public Cell Cursor { get; set; } = new(0, 2);

    /// <summary>Jugador seleccionado (-1: ninguno). Es de quien se pinta la zona de acción.</summary>
    public int SelectedId { get; set; } = -1;

    /// <summary>Jugador cogido y pendiente de soltar (-1: ninguno).</summary>
    public int HeldId { get; set; } = -1;

    /// <summary>Modo de cobertura del equipo a una pulsación (ADR 0029 §4).</summary>
    public bool CoverageMode { get; set; }

    /// <summary>
    /// Modo de zonas: dibuja sobre la mitad propia los tres tercios de inicio (<c>startsIn</c>) y las
    /// tres bandas (<c>startsOn</c>) con los mismos nombres con los que los perks hablan de ellos. Es
    /// excluyente con el de cobertura: son dos lecturas del mismo espacio.
    /// </summary>
    public bool ZonesMode { get; set; }

    /// <summary>
    /// Tercio a resaltar <b>él solo</b> (una clave de <see cref="ZoneHintText.ZoneKeys"/>), o null. Es lo
    /// que se pinta mientras el ratón descansa sobre el nombre de ese tercio dentro de la descripción de
    /// un perk (AW-F): la pregunta ahí es "¿dónde está <i>ese</i> tercio?", así que tintar los tres
    /// respondería a otra. Es independiente de <see cref="ZonesMode"/>: funciona con el modo apagado, y
    /// con el modo encendido se superpone como un subrayado del que se está mirando.
    /// </summary>
    public string? HighlightedZoneKey { get; set; }

    /// <summary>Banda a resaltar ella sola (una clave de <see cref="ZoneHintText.FlankKeys"/>), o null.</summary>
    public string? HighlightedFlankKey { get; set; }

    /// <summary>False si soltar en la casilla del cursor no sería una colocación válida (RF-041).</summary>
    public bool CursorValid { get; set; } = true;

    /// <summary>Zona del jugador manipulado, ya resuelta por <c>/Sim</c>; null si no hay ninguno.</summary>
    public PlacementZone? Zone { get; set; }

    /// <summary>Mapa de cobertura, ya resuelto por <c>/Sim</c>.</summary>
    public CoverageMap? Coverage { get; set; }

    /// <summary>Alineación que se pinta: la actual, o la previsualizada mientras se mueve a alguien.</summary>
    public Lineup? Preview { get; set; }

    /// <summary>Vínculos de la alineación pintada.</summary>
    public IReadOnlyList<PlacementLink> Links { get; set; } = System.Array.Empty<PlacementLink>();

    /// <summary>Vínculos que aparecerían al soltar (ADR 0029 §5).</summary>
    public IReadOnlyList<PlacementLink> Created { get; set; } = System.Array.Empty<PlacementLink>();

    /// <summary>Vínculos que desaparecerían al soltar.</summary>
    public IReadOnlyList<PlacementLink> Broken { get; set; } = System.Array.Empty<PlacementLink>();

    /// <summary>Lado de una casilla en píxeles.</summary>
    public float CellSize => Mathf.Min(Size.X / Pitch.Columns, Size.Y / Pitch.Rows);

    /// <summary>Centro en píxeles de una casilla.</summary>
    public Vector2 CenterOf(Cell cell) => new((cell.Column + 0.5f) * CellSize, (cell.Row + 0.5f) * CellSize);

    /// <summary>Casilla que contiene un punto local; puede caer fuera de la cuadrícula.</summary>
    public Cell CellAt(Vector2 point) => new(Mathf.FloorToInt(point.X / CellSize), Mathf.FloorToInt(point.Y / CellSize));

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                {
                    var cell = CellAt(button.Position);
                    EmitSignal(button.Pressed ? SignalName.CellPressed : SignalName.CellReleased, cell.Column, cell.Row);
                    AcceptEvent();
                    break;
                }

            case InputEventMouseMotion motion:
                {
                    var cell = CellAt(motion.Position);
                    if (cell != Cursor)
                    {
                        EmitSignal(SignalName.CellHovered, cell.Column, cell.Row);
                    }

                    break;
                }
        }
    }

    public override void _Draw()
    {
        if (State is null)
        {
            return;
        }

        float cell = CellSize;
        DrawField(cell);

        if (CoverageMode)
        {
            DrawCoverage(cell);
        }
        else if (ZonesMode)
        {
            DrawStartZones(cell);
        }
        else if (Zone is { } zone)
        {
            DrawZone(zone, cell);
        }

        // El resaltado de una zona suelta va después del modo que toque y antes de la retícula: es una
        // capa sobre el césped, como los tintes de tercio, no una marca encima de las fichas.
        DrawHighlight(cell);

        DrawGrid(cell);
        if (!CoverageMode)
        {
            DrawCoordinates(cell);
        }

        DrawLinks(cell);
        DrawTokens(cell);
        DrawCursor(cell);

        // Los rótulos van los últimos, sobre las fichas: en este modo el sujeto es la cuadrícula, y un
        // nombre tapado por una ficha no explica nada.
        if (ZonesMode)
        {
            DrawStartZoneNames(cell);
        }
        else
        {
            // Con el modo de zonas encendido cada franja ya lleva su nombre escrito: repetirlo para la
            // resaltada sería el mismo rótulo dos veces en el mismo sitio.
            DrawHighlightNames(cell);
        }
    }

    private void DrawField(float cell)
    {
        DrawRect(new Rect2(0f, 0f, cell * Pitch.Columns, cell * Pitch.Rows), Style.Grass);
        DrawRect(new Rect2(0f, 0f, cell * Pitch.PlacementColumns, cell * Pitch.Rows), Style.GrassOwn);
        DrawLine(new Vector2(cell * Pitch.Columns / 2f, 0f), new Vector2(cell * Pitch.Columns / 2f, cell * Pitch.Rows), Style.GrassLine, 2f);
        DrawRect(new Rect2(0f, cell, cell * Pitch.AreaColumns, cell * Pitch.AreaRows), Style.GrassLine, false, 2f);
        DrawRect(new Rect2(cell * (Pitch.Columns - Pitch.AreaColumns), cell, cell * Pitch.AreaColumns, cell * Pitch.AreaRows), Style.GrassLine, false, 2f);
    }

    /// <summary>
    /// Numeración de columnas y filas. La pantalla cita casillas por número (la lista de alineación, la
    /// descripción de un perk de colocación), así que la cuadrícula tiene que decir cuáles son.
    /// </summary>
    private void DrawCoordinates(float cell)
    {
        var font = GetThemeDefaultFont();
        var color = new Color(Style.TextDim, 0.55f);
        for (int column = 0; column < Pitch.Columns; column++)
        {
            Style.DrawText(this, font, new Vector2((column * cell) + 3f, 2f), column.ToString(System.Globalization.CultureInfo.InvariantCulture), Style.TextSmall, color);
        }

        for (int row = 1; row < Pitch.Rows; row++)
        {
            Style.DrawText(this, font, new Vector2(3f, (row * cell) + 2f), row.ToString(System.Globalization.CultureInfo.InvariantCulture), Style.TextSmall, color);
        }
    }

    private void DrawGrid(float cell)
    {
        for (int column = 0; column <= Pitch.Columns; column++)
        {
            DrawLine(new Vector2(column * cell, 0f), new Vector2(column * cell, cell * Pitch.Rows), Style.GrassLine, 1f);
        }

        for (int row = 0; row <= Pitch.Rows; row++)
        {
            DrawLine(new Vector2(0f, row * cell), new Vector2(cell * Pitch.Columns, row * cell), Style.GrassLine, 1f);
        }
    }

    /// <summary>
    /// Las dos capas de RF-045. La zona lleva relleno sólido y borde continuo; el margen, relleno claro,
    /// trama diagonal y borde punteado: se distinguen por forma además de por color (UI-002).
    /// </summary>
    private void DrawZone(PlacementZone zone, float cell)
    {
        var margin = new HashSet<Cell>(zone.Margin);
        var inner = new HashSet<Cell>(zone.Zone);

        foreach (var c in zone.Margin)
        {
            var rect = RectOf(c, cell);
            DrawRect(rect, Style.MarginFill);
            Style.DrawHatch(this, rect, new Color(Style.MarginEdge, 0.22f), 9f);
        }

        foreach (var c in zone.Zone)
        {
            DrawRect(RectOf(c, cell), Style.ZoneFill);
        }

        DrawBorder(margin, cell, Style.MarginEdge, dashed: true);
        DrawBorder(inner, cell, Style.ZoneEdge, dashed: false);
    }

    /// <summary>Contorno del conjunto de casillas: solo las aristas que dan a fuera del conjunto.</summary>
    private void DrawBorder(HashSet<Cell> cells, float cell, Color color, bool dashed)
    {
        foreach (var c in cells)
        {
            var rect = RectOf(c, cell);
            Edge(cells, c, new Cell(c.Column, c.Row - 1), rect.Position, rect.Position + new Vector2(rect.Size.X, 0f), color, dashed);
            Edge(cells, c, new Cell(c.Column, c.Row + 1), rect.Position + new Vector2(0f, rect.Size.Y), rect.Position + rect.Size, color, dashed);
            Edge(cells, c, new Cell(c.Column - 1, c.Row), rect.Position, rect.Position + new Vector2(0f, rect.Size.Y), color, dashed);
            Edge(cells, c, new Cell(c.Column + 1, c.Row), rect.Position + new Vector2(rect.Size.X, 0f), rect.Position + rect.Size, color, dashed);
        }
    }

    private void Edge(HashSet<Cell> cells, Cell from, Cell neighbour, Vector2 a, Vector2 b, Color color, bool dashed)
    {
        if (cells.Contains(neighbour))
        {
            return;
        }

        _ = from;
        if (dashed)
        {
            Style.DrawDashed(this, a, b, color, 2f);
        }
        else
        {
            DrawLine(a, b, color, 2f);
        }
    }

    /// <summary>
    /// Primera columna de cada tercio de inicio sobre las 8 columnas de colocación, y la primera columna
    /// que ya no es de ninguno. Es la misma partición entera que hace <c>LinkGeometry.ZoneOfHome</c>
    /// (columna * 3 / 8): 0-2 el tercio propio, 3-5 el centro del campo, 6-7 el tercio rival.
    /// </summary>
    private static readonly int[] ThirdBounds = { 0, 3, 6, Pitch.PlacementColumns };

    /// <summary>
    /// Claves de los tres tercios y de las tres bandas, en el orden en el que se pintan. Salen de
    /// <see cref="ZoneHintText"/> y no de una copia local para que la cuadrícula y el marcado de las
    /// descripciones no puedan hablar de zonas distintas.
    /// </summary>
    private static readonly string[] ThirdKeys = ZoneHintText.ZoneKeys;

    private static readonly string[] FlankKeys = ZoneHintText.FlankKeys;

    /// <summary>Rectángulo de un tercio de inicio: todas sus columnas de colocación, de banda a banda.</summary>
    private static Rect2 ThirdRect(int third, float cell) => new(
        ThirdBounds[third] * cell,
        0f,
        (ThirdBounds[third + 1] - ThirdBounds[third]) * cell,
        cell * Pitch.Rows);

    /// <summary>
    /// Rectángulo de una banda de inicio sobre la mitad propia. Las bandas parten filas, no columnas:
    /// la central es una sola fila (<c>Pitch.Rows / 2</c>) y las otras dos son <b>todas</b> las que
    /// quedan a cada lado, que es justo lo que el texto del perk no dejaba claro (AW-F).
    /// </summary>
    private static Rect2 FlankRect(int flank, float cell)
    {
        int center = Pitch.Rows / 2;
        int first = flank switch { 0 => 0, 1 => center, _ => center + 1 };
        int last = flank switch { 0 => center - 1, 1 => center, _ => Pitch.Rows - 1 };
        return new Rect2(0f, first * cell, cell * Pitch.PlacementColumns, (last - first + 1) * cell);
    }

    private static int IndexOf(string[] keys, string? key)
    {
        if (key is null)
        {
            return -1;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            if (string.Equals(keys[i], key, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Una sola franja resaltada, con el mismo tono de acento con el que la ficha marca la frase que la
    /// nombra: lo que se lee y lo que se pinta son la misma cosa. Relleno <b>y</b> borde continuo, no
    /// solo color (UI-002), y el nombre lo pone <see cref="DrawHighlightNames"/> al final.
    /// </summary>
    private void DrawHighlight(float cell)
    {
        int third = IndexOf(ThirdKeys, HighlightedZoneKey);
        if (third >= 0)
        {
            Highlight(ThirdRect(third, cell));
        }

        int flank = IndexOf(FlankKeys, HighlightedFlankKey);
        if (flank >= 0)
        {
            Highlight(FlankRect(flank, cell));
        }
    }

    private void Highlight(Rect2 rect)
    {
        DrawRect(rect, new Color(Style.Accent, 0.15f));
        DrawRect(rect, Style.Accent, false, 2f);
    }

    private void DrawHighlightNames(float cell)
    {
        if (State is null)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        int third = IndexOf(ThirdKeys, HighlightedZoneKey);
        if (third >= 0)
        {
            // Arriba, bajo la numeración de columnas, y no abajo como el modo de zonas completo: así el
            // sitio del rótulo dice por sí solo cuál de las dos lecturas del campo se está viendo.
            var rect = ThirdRect(third, cell);
            string text = State.Templates.Get("startZones", ThirdKeys[third]);
            var size = font.GetStringSize(text, HorizontalAlignment.Left, -1f, Style.TextSmall);
            Tag(font, new Vector2(rect.Position.X + ((rect.Size.X - size.X) / 2f), 20f), text, size);
        }

        int flank = IndexOf(FlankKeys, HighlightedFlankKey);
        if (flank >= 0)
        {
            var rect = FlankRect(flank, cell);
            string text = State.Templates.Get("startFlanks", FlankKeys[flank]);
            var size = font.GetStringSize(text, HorizontalAlignment.Left, -1f, Style.TextSmall);
            Tag(font, new Vector2(rect.Position.X + rect.Size.X + 10f, rect.Position.Y + ((rect.Size.Y - size.Y) / 2f)), text, size);
        }
    }

    /// <summary>
    /// Tercios de inicio y bandas sobre la mitad propia (RF-040..045). Los perks describen la cuadrícula
    /// de alineación —"empieza en su tercio adelantado", "en cualquier fila de su izquierda"— y no había
    /// forma de ver dónde están: este modo las dibuja con los mismos nombres con los que los perks hablan.
    /// <para>
    /// Los tres tercios se distinguen por color <b>y</b> por su nombre escrito; las tres bandas, por la
    /// línea punteada que separa la fila central y por sus rótulos (UI-002: nunca solo el color).
    /// </para>
    /// </summary>
    private void DrawStartZones(float cell)
    {
        float height = cell * Pitch.Rows;
        for (int third = 0; third < ThirdKeys.Length; third++)
        {
            var rect = ThirdRect(third, cell);
            DrawRect(rect, Style.StartZoneFills[third]);
            if (third > 0)
            {
                DrawLine(new Vector2(rect.Position.X, 0f), new Vector2(rect.Position.X, height), Style.ZoneDivider, 2f);
            }
        }

        // Las bandas parten las filas, no las columnas: la central (fila 2) es el carril y las otras dos
        // son las bandas. Se marcan con línea punteada para no confundirlas con los cortes de tercio.
        int center = Pitch.Rows / 2;
        float own = cell * Pitch.PlacementColumns;
        Style.DrawDashed(this, new Vector2(0f, center * cell), new Vector2(own, center * cell), Style.ZoneDivider, 2f);
        Style.DrawDashed(this, new Vector2(0f, (center + 1) * cell), new Vector2(own, (center + 1) * cell), Style.ZoneDivider, 2f);
    }

    /// <summary>
    /// Rótulos del modo de zonas. Los nombres no se escriben aquí: salen de <c>startZones</c> y
    /// <c>startFlanks</c> de <c>data/l10n</c> (RT-073), que es de donde salen también las descripciones
    /// de los perks, así que el campo y el texto del perk no pueden llamar a lo mismo de dos maneras.
    /// </summary>
    private void DrawStartZoneNames(float cell)
    {
        if (State is null)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        for (int third = 0; third < ThirdKeys.Length; third++)
        {
            string text = State.Templates.Get("startZones", ThirdKeys[third]);
            var rect = ThirdRect(third, cell);
            var size = font.GetStringSize(text, HorizontalAlignment.Left, -1f, Style.TextSmall);
            Tag(font, new Vector2(rect.Position.X + ((rect.Size.X - size.X) / 2f), (cell * Pitch.Rows) - size.Y - 8f), text, size);
        }

        // Las bandas se rotulan en la mitad rival, que está vacía: sobre la propia taparían las fichas.
        for (int flank = 0; flank < FlankKeys.Length; flank++)
        {
            string text = State.Templates.Get("startFlanks", FlankKeys[flank]);
            var rect = FlankRect(flank, cell);
            var size = font.GetStringSize(text, HorizontalAlignment.Left, -1f, Style.TextSmall);
            Tag(font, new Vector2(rect.Position.X + rect.Size.X + 10f, rect.Position.Y + ((rect.Size.Y - size.Y) / 2f)), text, size);
        }
    }

    /// <summary>Rótulo con fondo: sobre el césped y sobre las fichas, el texto solo se lee con respaldo.</summary>
    private void Tag(Font font, Vector2 topLeft, string text, Vector2 size)
    {
        DrawRect(new Rect2(topLeft - new Vector2(4f, 2f), size + new Vector2(8f, 4f)), new Color(Style.Background, 0.82f));
        Style.DrawText(this, font, topLeft, text, Style.TextSmall, Style.Text);
    }

    /// <summary>Mapa de calor de cuántos jugadores cubren cada casilla, con los huecos destacados (ADR 0029 §4).</summary>
    private void DrawCoverage(float cell)
    {
        if (Coverage is not { } coverage)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        for (int row = 0; row < Pitch.Rows; row++)
        {
            for (int column = 0; column < Pitch.Columns; column++)
            {
                var c = new Cell(column, row);
                var rect = RectOf(c, cell);
                int count = coverage.Count(c);
                DrawRect(rect, Style.CoverageColor(count, coverage.Max));

                if (count == 0)
                {
                    Style.DrawHatch(this, rect, new Color(Style.Hole, 0.75f), 6f);
                    DrawRect(rect, Style.Hole, false, 2f);
                }

                string label = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var size = font.GetStringSize(label, HorizontalAlignment.Left, -1f, Style.TextLarge);
                Style.DrawText(this, font, rect.Position + ((rect.Size - size) / 2f) - new Vector2(0f, 2f), label, Style.TextLarge, count == 0 ? Style.Hole : Style.Text);
            }
        }
    }

    /// <summary>Vínculos direccionales: los actuales en gris y, al mover, los que se crean y se rompen.</summary>
    private void DrawLinks(float cell)
    {
        var drawn = new HashSet<(int, int)>();
        foreach (var link in Links)
        {
            var key = link.FromPlayerId < link.ToPlayerId ? (link.FromPlayerId, link.ToPlayerId) : (link.ToPlayerId, link.FromPlayerId);
            if (!drawn.Add(key))
            {
                continue;
            }

            DrawLink(link, Style.LinkLine, 1.5f, cell, dashed: false, arrow: false);
        }

        foreach (var link in Broken)
        {
            DrawLink(link, Style.LinkBroken, 2.5f, cell, dashed: true, arrow: true);
        }

        foreach (var link in Created)
        {
            DrawLink(link, Style.LinkCreated, 2.5f, cell, dashed: false, arrow: true);
        }
    }

    private void DrawLink(PlacementLink link, Color color, float width, float cell, bool dashed, bool arrow)
    {
        if (Preview is not { } lineup)
        {
            return;
        }

        Cell? from = null;
        Cell? to = null;
        foreach (var slot in lineup.Slots)
        {
            if (slot.PlayerId == link.FromPlayerId)
            {
                from = slot.HomeCell;
            }

            if (slot.PlayerId == link.ToPlayerId)
            {
                to = slot.HomeCell;
            }
        }

        if (from is null || to is null)
        {
            return;
        }

        var a = CenterOf(from.Value);
        var b = CenterOf(to.Value);
        var direction = (b - a).Normalized();
        a += direction * cell * 0.24f;
        b -= direction * cell * 0.24f;

        if (dashed)
        {
            Style.DrawDashed(this, a, b, color, width);
        }
        else
        {
            DrawLine(a, b, color, width);
        }

        if (arrow)
        {
            var side = new Vector2(-direction.Y, direction.X) * 5f;
            DrawColoredPolygon(new[] { b, b - (direction * 10f) + side, b - (direction * 10f) - side }, color);
        }
    }

    /// <summary>
    /// Fichas del campo: círculos de colores (fase 1, sin arte). Color por posición, silueta por posición
    /// (UI-002) y grosor de contorno para el jugador seleccionado. El estado físico y las mejoras viven
    /// en la ficha, nunca sobre el sprite (UI-005, UI-014).
    /// </summary>
    private void DrawTokens(float cell)
    {
        if (State is null || Preview is not { } lineup)
        {
            return;
        }

        float radius = cell * 0.33f;
        foreach (var slot in lineup.Slots)
        {
            var player = State.Find(slot.PlayerId);
            if (player is null)
            {
                continue;
            }

            bool held = slot.PlayerId == HeldId;
            var center = held ? CenterOf(Cursor) : CenterOf(slot.HomeCell);
            var color = Style.Of(player.Position);

            if (held)
            {
                DrawArc(CenterOf(slot.HomeCell), radius, 0f, Mathf.Tau, 24, new Color(color, 0.35f), 2f);
            }

            DrawCircle(center, radius, color);
            Style.DrawPositionIcon(this, center, radius * 0.45f, player.Position, Style.Background);

            if (slot.PlayerId == SelectedId)
            {
                DrawArc(center, radius + 3f, 0f, Mathf.Tau, 32, Style.Accent, 3f);
            }
        }
    }

    /// <summary>Cursor único de colocación: lo mueve la cruceta y lo mueve el ratón (UI-006, RT-071).</summary>
    private void DrawCursor(float cell)
    {
        var rect = RectOf(Cursor, cell).Grow(-2f);
        var color = CursorValid ? Style.Cursor : Style.Hole;
        DrawRect(rect, color, false, HeldId >= 0 ? 3f : 2f);
        if (HeldId >= 0)
        {
            DrawRect(rect.Grow(-3f), color, false, 1f);
        }
    }

    private static Rect2 RectOf(Cell cell, float size) => new(cell.Column * size, cell.Row * size, size, size);
}
