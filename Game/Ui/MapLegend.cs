using Godot;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// Leyenda del mapa (encargo mapa-pregon, revisión de reparto del 20 sep 2026): un <see cref="KindGlyph"/>
/// —el mismo glifo que dibuja el nodo en el grafo y en la lista de destinos— con el nombre y una línea de
/// qué es, para cada uno de los ocho <see cref="NodeKind"/>. Vive debajo de la lista de destinos, en la
/// misma columna izquierda y con su mismo ancho completo (376 px): eso deja sitio de sobra para que cada
/// fila sea <b>una sola línea</b> ("nombre — qué es"), en vez de la columna estrecha de ~140 px de la
/// primera versión, que partía la descripción en tres líneas y se desbordaba sobre la madera.
/// <para>
/// Filas de alto dinámico (mismo patrón que <c>MapScreen.BuildChoices</c>): el texto decide su propio
/// alto envolviendo, y la fila se ajusta a él, así que un nombre más largo no se monta sobre la fila
/// siguiente aunque en el ancho de esta columna no debería envolver nunca.
/// </para>
/// </summary>
public partial class MapLegend : Control
{
    private static readonly NodeKind[] Kinds =
    {
        NodeKind.LeagueMatch, NodeKind.EliteMatch, NodeKind.Market, NodeKind.Clinic,
        NodeKind.Workshop, NodeKind.Training, NodeKind.Event, NodeKind.Boss,
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        EssentialLabel.Title(this, UiText.Get("ui.map.legend.title"), new Vector2(0f, 0f), Size.X, Pregon.Wax);

        var rows = new VBoxContainer
        {
            Position = new Vector2(0f, 22f),
            Size = new Vector2(Size.X, Size.Y - 22f),
        };
        rows.AddThemeConstantOverride("separation", 4);
        AddChild(rows);

        const float GlyphSize = 24f;
        const float TextX = GlyphSize + 8f;
        foreach (var kind in Kinds)
        {
            var row = new Control();
            rows.AddChild(row);

            var glyph = new KindGlyph
            {
                Kind = kind,
                Position = new Vector2(0f, 0f),
                Size = new Vector2(GlyphSize, GlyphSize),
            };
            row.AddChild(glyph);

            // "nombre — qué es" en una sola línea: con el ancho completo de la columna de destinos
            // (376 px) hasta la descripción más larga cabe sin envolver.
            string line = UiText.Get("ui.kind." + kind) + " — " + UiText.Get("ui.map.legend." + kind);
            var text = EssentialLabel.Body(row, line, new Vector2(TextX, (GlyphSize - EssentialLabel.Size) / 2f), Size.X - TextX);
            row.CustomMinimumSize = new Vector2(Size.X, Mathf.Max(GlyphSize, text.Size.Y));
        }

        // El estado del nodo —accesible, visitado, fuera de alcance— no lo cubre el icono: sigue siendo
        // color de anillo y relleno (MapView.DrawNode). Va como fila más de la misma lista, para que
        // nunca se monte sobre la última ficha por muy larga que salga.
        EssentialLabel.Body(rows, UiText.Get("ui.map.legend"), Vector2.Zero, Size.X, Style.TextDim);
    }
}
