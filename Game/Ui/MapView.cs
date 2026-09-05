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
/// <para>
/// Con los cuatro carriles de la ADR 0053 el acto pasa de ~20 nodos dibujados a ~36, así que el dibujo
/// tiene tres cosas que el de ancho dos no necesitaba:
/// <list type="number">
/// <item>el <b>carril es una altura fija</b> —<see cref="MapNode.IndexInLayer"/> se dibuja siempre a la
/// misma <c>y</c>, en todas las capas—, que es lo que hace visible la regla de movimiento: una arista
/// sube, baja o sigue recta, nunca salta dos filas. Las capas de un solo nodo (entrada y jefe) se
/// centran;</item>
/// <item>las aristas <b>arrancan y terminan en el borde</b> del glifo, no en el centro, para que los
/// cruces entre carriles vecinos se lean como cruces y no como manchas;</item>
/// <item>lo que ya <b>no se puede alcanzar</b> desde donde está el jugador se apaga del todo y pierde la
/// etiqueta. Es la información nueva del mapa de cuatro carriles: subir de carril cierra la parte baja
/// del acto, y eso hay que verlo antes de elegir, no después.</item>
/// </list>
/// </para>
/// </summary>
public partial class MapView : Control
{
    private readonly Dictionary<int, Vector2> _positions = new();
    private readonly HashSet<int> _reachable = new();

    /// <summary>Radio del glifo de nodo. El jefe se dibuja algo mayor.</summary>
    private const float NodeRadius = 12f;

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
            if (position.DistanceTo(click.Position) <= NodeRadius + 4f && Contains(AvailableIds, id))
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
        MarkReachable();
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
                if (!_positions.TryGetValue(node.Next[i], out var to))
                {
                    continue;
                }

                bool live = node.Id == CurrentNodeId && Contains(AvailableIds, node.Next[i]);
                bool open = _reachable.Contains(node.Id) && _reachable.Contains(node.Next[i]);
                var color = live ? Style.Accent : new Color(Style.Line, open ? 1f : 0.35f);
                float width = live ? 2.5f : 1f;

                // Del borde al borde: con cuatro carriles hay cruces, y una línea que muere en el centro
                // del glifo los vuelve ilegibles.
                var step = (to - from).Normalized() * (NodeRadius + 2f);
                DrawLine(from + step, to - step, color, width);
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
        bool open = _reachable.Contains(node.Id);
        var color = Style.Of(node.Kind);
        float radius = node.Kind == NodeKind.Boss ? NodeRadius + 3f : NodeRadius;

        if (visited || current)
        {
            DrawCircle(center, radius, new Color(color, 0.85f));
        }
        else
        {
            DrawCircle(center, radius, new Color(color, available ? 0.35f : open ? 0.16f : 0.07f));
            DrawArc(center, radius, 0f, Mathf.Tau, 24, new Color(color, available ? 0.95f : open ? 0.45f : 0.18f), 1.5f);
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
            // El distintivo de dificultad (RF-012) se apaga con el nodo: si no, la parte del acto que ya
            // no se puede alcanzar es lo que más brilla del mapa.
            float ink = available || visited || current ? 1f : open ? 0.55f : 0.20f;
            Style.DrawDifficultyIcon(this, center, 5f, node.Difficulty, new Color(Style.DifficultyColor(node.Difficulty), ink));
        }

        // La etiqueta solo donde importa: con 36 nodos por acto, poner el texto también bajo lo que ya
        // no se puede alcanzar convierte el grafo en una mancha.
        if (!open && !visited && !current)
        {
            return;
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

    /// <summary>
    /// Coloca los nodos: una columna por capa y una fila por <b>carril</b>, la misma altura en todo el
    /// acto. Las capas de un solo nodo —la entrada y el jefe, los dos extremos cerrados del acto— van
    /// centradas, que es donde el jugador espera encontrarlas.
    /// </summary>
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
        float spacing = Mathf.Min(68f, (Size.Y - 34f) / MapGenerator.Lanes);
        float middle = (MapGenerator.Lanes - 1) / 2f;
        foreach (var node in Map.Nodes)
        {
            float lane = perLayer[node.Layer] == 1 ? middle : node.IndexInLayer;
            float y = (Size.Y / 2f) + ((lane - middle) * spacing);
            _positions[node.Id] = new Vector2(24f + (node.Layer * stepX), y);
        }
    }

    /// <summary>
    /// Lo que todavía se puede alcanzar desde donde está el jugador, siguiendo aristas hacia delante.
    /// Antes de entrar en el primer nodo es el acto entero. No decide nada: es la misma cuenta que hace
    /// el jugador con el dedo, dibujada.
    /// </summary>
    private void MarkReachable()
    {
        _reachable.Clear();
        if (Map is null)
        {
            return;
        }

        var frontier = new List<int>();
        if (CurrentNodeId < 0)
        {
            for (int i = 0; i < Map.EntryNodeIds.Count; i++)
            {
                frontier.Add(Map.EntryNodeIds[i]);
            }
        }
        else
        {
            frontier.Add(CurrentNodeId);
        }

        for (int i = 0; i < frontier.Count; i++)
        {
            _reachable.Add(frontier[i]);
        }

        while (frontier.Count > 0)
        {
            var next = new List<int>();
            for (int i = 0; i < frontier.Count; i++)
            {
                var node = Map.Find(frontier[i]);
                if (node is null)
                {
                    continue;
                }

                for (int e = 0; e < node.Next.Count; e++)
                {
                    if (_reachable.Add(node.Next[e]))
                    {
                        next.Add(node.Next[e]);
                    }
                }
            }

            frontier = next;
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
