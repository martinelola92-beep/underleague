using System.Collections.Generic;
using Godot;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// El grafo del acto dibujado (RF-010): capas de izquierda a derecha, aristas hacia delante y un glifo
/// por nodo con <b>color y forma</b> propios (UI-002). No decide nada: recibe el mapa, dónde está el
/// jugador y a qué nodos puede ir, y avisa con <see cref="NodePressedEventHandler"/> cuando se pulsa uno
/// accesible.
/// <para>
/// Los nodos de partido llevan dentro el <b>distintivo de dificultad</b> de RF-012 y los de mercado un
/// anillo doble, porque son los nodos que salvan runs (RF-002e, RF-011b). Lo accesible lleva borde
/// dorado; lo ya recorrido, relleno; el resto del acto se ve apagado, que es información: enseña el
/// camino que se está dejando atrás.
/// </para>
/// </summary>
public partial class MapView : Control
{
    private readonly Dictionary<int, Vector2> _positions = new();

    /// <summary>Se pulsó un nodo accesible.</summary>
    [Signal]
    public delegate void NodePressedEventHandler(int nodeId);

    /// <summary>Mapa del acto que se dibuja.</summary>
    public ActMap? Map { get; set; }

    /// <summary>Nodo en el que está el jugador; -1 antes de entrar en el primero.</summary>
    public int CurrentNodeId { get; set; } = -1;

    /// <summary>Ids de los nodos accesibles ahora (RF-010: los sucesores del actual).</summary>
    public IReadOnlyList<int> AvailableIds { get; set; } = System.Array.Empty<int>();

    /// <summary>Ids de los nodos ya recorridos, para pintar el camino hecho.</summary>
    public IReadOnlyList<int> VisitedIds { get; set; } = System.Array.Empty<int>();

    /// <summary>Nodo señalado por el jugador (el que tiene el ratón encima o el botón enfocado); -1 si ninguno.</summary>
    public int HighlightedId { get; set; } = -1;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        foreach (var (id, position) in _positions)
        {
            if (position.DistanceTo(click.Position) <= 16f && Contains(AvailableIds, id))
            {
                EmitSignal(SignalName.NodePressed, id);
                return;
            }
        }
    }

    public override void _Draw()
    {
        if (Map is null)
        {
            return;
        }

        Layout();
        var font = ThemeDB.FallbackFont;

        // Primero las aristas, para que ningún glifo quede partido por una línea.
        foreach (var node in Map.Nodes)
        {
            if (!_positions.TryGetValue(node.Id, out var from))
            {
                continue;
            }

            for (int i = 0; i < node.Next.Count; i++)
            {
                if (_positions.TryGetValue(node.Next[i], out var to))
                {
                    bool live = node.Id == CurrentNodeId && Contains(AvailableIds, node.Next[i]);
                    DrawLine(from, to, live ? Style.Accent : Style.Line, live ? 2f : 1f);
                }
            }
        }

        foreach (var node in Map.Nodes)
        {
            if (_positions.TryGetValue(node.Id, out var center))
            {
                DrawNode(node, center, font);
            }
        }
    }

    private void DrawNode(MapNode node, Vector2 center, Font font)
    {
        bool available = Contains(AvailableIds, node.Id);
        bool visited = Contains(VisitedIds, node.Id);
        bool current = node.Id == CurrentNodeId;
        var color = Style.Of(node.Kind);
        float radius = node.Kind == NodeKind.Boss ? 15f : 12f;

        if (visited || current)
        {
            DrawCircle(center, radius, new Color(color, 0.85f));
        }
        else
        {
            DrawCircle(center, radius, new Color(color, available ? 0.35f : 0.12f));
            DrawArc(center, radius, 0f, Mathf.Tau, 24, new Color(color, available ? 0.95f : 0.35f), 1.5f);
        }

        // El mercado, con anillo doble: es el nodo que salva runs y tiene que verse a golpe de vista
        // (RF-002e, RF-011b).
        if (node.Kind == NodeKind.Market)
        {
            DrawArc(center, radius + 4f, 0f, Mathf.Tau, 28, new Color(color, available ? 1f : 0.5f), 1.5f);
        }

        if (available)
        {
            DrawArc(center, radius + 7f, 0f, Mathf.Tau, 28, Style.Accent, node.Id == HighlightedId ? 3f : 1.5f);
        }

        if (node.IsMatch && node.Difficulty > 0)
        {
            Style.DrawDifficultyIcon(this, center, 5f, node.Difficulty, Style.DifficultyColor(node.Difficulty));
        }

        string label = UiText.Get("ui.kind.short." + node.Kind);
        var size = font.GetStringSize(label, HorizontalAlignment.Left, -1f, Style.TextSmall);
        Style.DrawText(
            this,
            font,
            new Vector2(center.X - (size.X / 2f), center.Y + radius + 3f),
            label,
            Style.TextSmall,
            available ? Style.Text : Style.TextDim);
    }

    /// <summary>Coloca los nodos: una columna por capa, centradas verticalmente dentro de la capa.</summary>
    private void Layout()
    {
        _positions.Clear();
        if (Map is null || Map.Nodes.Count == 0)
        {
            return;
        }

        int layers = 0;
        var perLayer = new Dictionary<int, int>();
        foreach (var node in Map.Nodes)
        {
            layers = Mathf.Max(layers, node.Layer + 1);
            perLayer[node.Layer] = perLayer.TryGetValue(node.Layer, out int count) ? count + 1 : 1;
        }

        float stepX = layers > 1 ? (Size.X - 48f) / (layers - 1) : 0f;
        foreach (var node in Map.Nodes)
        {
            int inLayer = perLayer[node.Layer];
            float spacing = Mathf.Min(58f, (Size.Y - 40f) / Mathf.Max(1, inLayer));
            float y = (Size.Y / 2f) + ((node.IndexInLayer - ((inLayer - 1) / 2f)) * spacing);
            _positions[node.Id] = new Vector2(24f + (node.Layer * stepX), y);
        }
    }

    private static bool Contains(IReadOnlyList<int> ids, int id)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return true;
            }
        }

        return false;
    }
}
