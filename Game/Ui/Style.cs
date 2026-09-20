using System.Collections.Generic;
using Godot;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// Paleta, medidas y primitivas de dibujo de la interfaz. Un solo sitio para las decisiones visuales que
/// las demás pantallas heredan (UI-021).
/// <para>
/// <b>Dos tamaños de texto y ninguno menor de 11 px a 1280x800</b> (UI-004): <see cref="TextSmall"/> para
/// el cuerpo y <see cref="TextLarge"/> para los títulos y el nombre del jugador expandido. No hay un
/// tercero. <b>Color y forma siempre juntos</b> (UI-002): cada posición tiene color y silueta, cada
/// estado físico color e icono, y las dos capas de la zona de acción se distinguen por tono <b>y</b> por
/// tipo de borde (sólido frente a punteado con trama).
/// </para>
/// <para>
/// <b>Voz de pregón (encargo pregon-resto, ADR 0120):</b> desde el 20 sep 2026 la paleta deja el tema
/// oscuro de cristal y pasa a la de <c>docs/ui/README.md</c> §1 — madera oscura de fondo, pergamino para
/// los paneles, tinta parda para el texto, dorado para lo destacado — con los MISMOS nombres de token, así
/// que ninguna pantalla vieja tiene que tocarse para heredarla. Dos tokens quedan fuera a propósito:
/// <see cref="Hole"/> (sigue siendo un rojo claro: lo usa <c>PitchView</c> sobre el césped oscuro, que no
/// cambia, y un rojo tan oscuro como la sangre heráldica se volvería invisible ahí) y los colores de
/// equipo (siguen siendo azur/gules, ya lo eran). <see cref="OnWood"/>, <see cref="GrassLabel"/> y
/// <see cref="NeutralBadge"/> son nuevos: hacen falta porque algunos textos viven directamente sobre la
/// madera del fondo o sobre el césped, sin un panel de pergamino debajo, y el mismo tono que ahora lee
/// bien sobre pergamino (tinta oscura) sería invisible ahí.
/// </para>
/// </summary>
public static class Style
{
    /// <summary>Cuerpo de texto. 12 px a 1280x800, por encima del mínimo de UI-004.</summary>
    public const int TextSmall = 12;

    /// <summary>Titulares y nombre del jugador. El segundo y último tamaño (UI-004).</summary>
    public const int TextLarge = 17;

    /// <summary>Alto de la ficha colapsada (UI-011): una tira de 24 px, ni uno más.</summary>
    public const int CollapsedHeight = 24;

    /// <summary>Fondo de pantalla: madera oscura, la estructura persistente (docs/ui/README.md §1).</summary>
    public static readonly Color Background = new("241a10");

    /// <summary>Panel de contenido: pergamino. <see cref="Widgets.Panel"/> le añade el borde rasgado.</summary>
    public static readonly Color Panel = new("efe2c0");

    /// <summary>Segundo tono de pergamino, algo más cálido: paneles destacados (ficha expandida, marcador).</summary>
    public static readonly Color PanelSoft = new("e6d2a0");

    /// <summary>Cuerpo de texto sobre pergamino: tinta parda (nunca sobre madera ni césped sin más).</summary>
    public static readonly Color Text = new("3a2a1a");

    /// <summary>Texto secundario sobre pergamino: la misma tinta, algo más clara.</summary>
    public static readonly Color TextDim = new("6b5335");

    /// <summary>Dorado: lo destacado y la economía.</summary>
    public static readonly Color Accent = new("c9982f");

    /// <summary>Borde de pergamino y líneas finas sobre pergamino.</summary>
    public static readonly Color Line = new("b89c68");

    /// <summary>
    /// Texto claro para lo que vive directamente sobre <see cref="Background"/> (madera), sin un panel de
    /// pergamino debajo: la ayuda de entrada al pie de casi toda pantalla, el subtítulo de la cabecera.
    /// <see cref="Text"/>/<see cref="TextDim"/> son tinta oscura a propósito para leerse sobre pergamino —
    /// sobre madera oscura serían invisibles.
    /// </summary>
    public static readonly Color OnWood = new("cdb98c");

    /// <summary>
    /// Texto claro sin backdrop de papel, para lo que se escribe directamente sobre un fondo oscuro que
    /// no es pergamino. Ya no la usa <c>PitchView</c> (encargo pizarra, 20 sep 2026: su campo de
    /// colocación pasó a pizarrón y usa <see cref="Chalkboard.Chalk"/>, su propia tiza) ni
    /// <c>MatchPitchView</c>/<c>MatchPitchView3D</c> (siguen en césped, <see cref="Grass"/>, y no rotulan
    /// coordenadas sobre él). Se conserva por si otra pantalla la necesita.
    /// </summary>
    public static readonly Color GrassLabel = new("e8ddc0");

    /// <summary>
    /// Distintivo neutro para lo que antes tomaba prestado <see cref="LinkLine"/> (un gris translúcido
    /// pensado para dibujarse sobre césped, no como una placa opaca sobre pergamino): el "CONS" del
    /// Mercado y el "ITEM" del Informe.
    /// </summary>
    public static readonly Color NeutralBadge = new("5c6f8f");

    public static readonly Color Grass = new("223028");
    public static readonly Color GrassOwn = new("2a3b31");
    public static readonly Color GrassLine = new("3d5346");

    /// <summary>Capa 1 de RF-045: la zona propia, tono sólido y borde continuo.</summary>
    public static readonly Color ZoneFill = new(0.31f, 0.68f, 0.92f, 0.30f);
    public static readonly Color ZoneEdge = new(0.47f, 0.80f, 1.00f, 0.95f);

    /// <summary>Capa 2 de RF-045: el margen exterior, tono claro, borde punteado y trama diagonal.</summary>
    public static readonly Color MarginFill = new(0.47f, 0.80f, 1.00f, 0.07f);
    public static readonly Color MarginEdge = new(0.62f, 0.86f, 1.00f, 0.60f);

    /// <summary>
    /// Modo de zonas: los tres tercios de inicio de <c>startsIn</c> sobre la mitad propia. Van de frío a
    /// cálido según se acerca la portería rival, y además llevan su nombre escrito: el color ordena, el
    /// texto identifica (UI-002).
    /// </summary>
    public static readonly Color[] StartZoneFills =
    {
        new(0.31f, 0.68f, 0.92f, 0.16f),
        new(0.94f, 0.71f, 0.16f, 0.14f),
        new(0.89f, 0.35f, 0.30f, 0.18f),
    };

    /// <summary>Separadores entre tercios y entre bandas del modo de zonas, y color de sus rótulos.</summary>
    public static readonly Color ZoneDivider = new(0.90f, 0.93f, 0.97f, 0.45f);

    /// <summary>
    /// Colores de equipo del campo del partido. Son los dos tonos más separados de la paleta de
    /// posiciones, para que a 20 fichas de distancia se sepa de un vistazo de quién es cada una. El
    /// equipo del jugador es siempre el local (W-15).
    /// </summary>
    public static readonly Color TeamOwn = new("4f86c6");

    public static readonly Color TeamRival = new("d9544d");

    /// <summary>Balón: el único elemento blanco del campo, para que no se confunda con ninguna ficha.</summary>
    public static readonly Color Ball = new("f2f4f8");

    /// <summary>Anillo del poseedor del balón (RF-121: quién lo lleva, de un vistazo).</summary>
    public static readonly Color Carrier = new("ffffff");

    /// <summary>
    /// Bigote de intención del campo del partido: hacia dónde le manda ir su acción. Va en un gris claro
    /// muy transparente a propósito —no es un color de equipo ni de estado— porque son doce a la vez y
    /// tienen que leerse como una textura de movimiento, no como doce líneas que compiten con las fichas.
    /// </summary>
    public static readonly Color Intent = new(0.85f, 0.89f, 0.95f, 0.30f);

    public static readonly Color LinkLine = new(0.72f, 0.76f, 0.83f, 0.45f);
    public static readonly Color LinkCreated = new("5fd07a");
    public static readonly Color LinkBroken = new("e2585a");

    /// <summary>
    /// Rojo claro deliberadamente <b>fuera</b> del retinte de pregón: <c>PitchView</c> lo pinta sobre el
    /// césped oscuro (que no cambia) para el hueco de cobertura y el cursor inválido, y necesita ser
    /// claro ahí. Donde este mismo token se usaba como texto de aviso sobre un panel de pergamino
    /// (Mercado, Recompensa...) sigue leyéndose — no es tinta oscura, pero tampoco tan claro como para
    /// perderse en el pergamino — y no hacía falta partirlo en dos tokens para eso.
    /// </summary>
    public static readonly Color Hole = new("e2585a");

    public static readonly Color Cursor = new("f0b429");

    private static readonly Color[] PositionColors =
    {
        new("e8c547"), // portero
        new("4f86c6"), // defensa
        new("5fad56"), // centrocampista
        new("d9544d"), // delantero
    };

    private static readonly Color[] StateColors =
    {
        new("5fad56"), // sano
        new("e8c547"), // lesión leve
        new("d9544d"), // lesión grave
        new("6b7280"), // muerto
    };

    /// <summary>Color de la posición (RF fase 1: círculos de colores, sin arte).</summary>
    public static Color Of(Position position) => PositionColors[(int)position];

    /// <summary>Color del estado físico (UI-002: cuatro colores, y cuatro iconos en <see cref="StateIcon"/>).</summary>
    public static Color Of(PhysicalState state) => StateColors[(int)state];

    /// <summary>
    /// Color del estado de la máquina de estados del jugador (RT-089c), el anillo de cada ficha del
    /// campo del partido. Los cuatro estados en los que el jugador <b>decide</b> —colocarse, perseguir,
    /// regatear— y los de acción en curso llevan tonos vivos; los de baja (derribado, lesionado,
    /// expulsado) van en gris y además pierden el anillo por una cruz, para que no dependa del color
    /// (UI-002, <see cref="DrawDownMark"/>).
    /// </summary>
    public static Color Of(Underleague.Sim.Engine.PlayerState state) => state switch
    {
        Underleague.Sim.Engine.PlayerState.Positioning => new Color(0.42f, 0.50f, 0.59f, 0.85f),
        Underleague.Sim.Engine.PlayerState.Chasing => new("57c2b5"),
        Underleague.Sim.Engine.PlayerState.Dribbling => new("f0b429"),
        Underleague.Sim.Engine.PlayerState.Passing => new("5fd07a"),
        Underleague.Sim.Engine.PlayerState.Shooting => new("ff7a45"),
        Underleague.Sim.Engine.PlayerState.Tackling => new("e2585a"),
        Underleague.Sim.Engine.PlayerState.Blocking => new("c58fd0"),
        Underleague.Sim.Engine.PlayerState.KnockedDown => new("9aa0aa"),
        Underleague.Sim.Engine.PlayerState.Injured => new("8b2e2e"),
        Underleague.Sim.Engine.PlayerState.Celebrating => new("ffe08a"),
        _ => new("4a4f59"),
    };

    /// <summary>True si el estado saca al jugador de la jugada: la ficha se marca con una cruz (UI-002).</summary>
    public static bool IsDown(Underleague.Sim.Engine.PlayerState state) =>
        state is Underleague.Sim.Engine.PlayerState.KnockedDown
            or Underleague.Sim.Engine.PlayerState.Injured
            or Underleague.Sim.Engine.PlayerState.SentOff;

    /// <summary>Cruz sobre una ficha fuera de la jugada; la marca de forma que acompaña al gris.</summary>
    public static void DrawDownMark(CanvasItem target, Vector2 center, float radius, Color color)
    {
        target.DrawLine(center - new Vector2(radius, radius), center + new Vector2(radius, radius), color, 2f);
        target.DrawLine(center + new Vector2(-radius, radius), center + new Vector2(radius, -radius), color, 2f);
    }

    /// <summary>Rampa del mapa de calor de cobertura (ADR 0029 §4); 0 jugadores se pinta aparte, como hueco.</summary>
    public static Color CoverageColor(int count, int max)
    {
        if (count <= 0)
        {
            return new Color(Hole, 0.32f);
        }

        float t = max <= 1 ? 1f : (count - 1) / (float)(max - 1);
        return new Color(0.20f + (0.55f * t), 0.55f + (0.30f * t), 0.35f + (0.10f * t), 0.20f + (0.45f * t));
    }

    /// <summary>Dibuja el texto tomando <paramref name="topLeft"/> como esquina, no como línea base.</summary>
    public static void DrawText(CanvasItem target, Font font, Vector2 topLeft, string text, int size, Color color, float maxWidth = -1f)
    {
        target.DrawString(font, new Vector2(topLeft.X, topLeft.Y + font.GetAscent(size)), text, HorizontalAlignment.Left, maxWidth, size, color);
    }

    /// <summary>Parte el texto en líneas que caben en <paramref name="maxWidth"/> (DrawString recorta, no parte).</summary>
    public static List<string> Wrap(Font font, string text, int size, float maxWidth)
    {
        // Un salto de línea del propio texto es un corte pedido a mano: se respeta antes de medir nada.
        // Lo usan las descripciones de perk, que separan "qué hace" de "Condición:" (RT-035).
        if (text.Contains('\n'))
        {
            var pieces = new List<string>();
            foreach (string segment in text.Split('\n'))
            {
                pieces.AddRange(Wrap(font, segment, size, maxWidth));
            }

            return pieces;
        }

        var lines = new List<string>();
        string current = string.Empty;
        foreach (string word in text.Split(' '))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (font.GetStringSize(candidate, HorizontalAlignment.Left, -1f, size).X <= maxWidth || current.Length == 0)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    /// <summary>Línea punteada: la forma con la que el margen exterior se distingue de la zona sin depender del color (UI-002).</summary>
    public static void DrawDashed(CanvasItem target, Vector2 from, Vector2 to, Color color, float width, float dash = 5f)
    {
        float length = from.DistanceTo(to);
        if (length <= 0.01f)
        {
            return;
        }

        Vector2 step = (to - from) / length;
        for (float d = 0f; d < length; d += dash * 2f)
        {
            target.DrawLine(from + (step * d), from + (step * Mathf.Min(d + dash, length)), color, width);
        }
    }

    /// <summary>Trama diagonal dentro de una casilla: la segunda marca de forma del margen exterior.</summary>
    public static void DrawHatch(CanvasItem target, Rect2 rect, Color color, float spacing = 7f)
    {
        for (float x = rect.Position.X - rect.Size.Y; x < rect.Position.X + rect.Size.X; x += spacing)
        {
            float x0 = Mathf.Max(x, rect.Position.X);
            float y0 = rect.Position.Y + (x0 - x);
            float x1 = Mathf.Min(x + rect.Size.Y, rect.Position.X + rect.Size.X);
            float y1 = rect.Position.Y + (x1 - x);
            if (x1 > x0 && y0 < rect.Position.Y + rect.Size.Y)
            {
                target.DrawLine(new Vector2(x0, y0), new Vector2(x1, y1), color, 1f);
            }
        }
    }

    /// <summary>
    /// Silueta de la posición (UI-002: la posición no se distingue solo por color). Portero cuadrado,
    /// defensa triángulo hacia su portería, centrocampista rombo, delantero triángulo hacia la rival.
    /// </summary>
    public static void DrawPositionIcon(CanvasItem target, Vector2 center, float radius, Position position, Color color)
    {
        switch (position)
        {
            case Position.Goalkeeper:
                target.DrawRect(new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2f, radius * 2f)), color);
                break;
            case Position.Defender:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(-radius, 0f),
                    center + new Vector2(radius, -radius),
                    center + new Vector2(radius, radius),
                }, color);
                break;
            case Position.Midfielder:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(0f, -radius),
                    center + new Vector2(radius, 0f),
                    center + new Vector2(0f, radius),
                    center + new Vector2(-radius, 0f),
                }, color);
                break;
            default:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(radius, 0f),
                    center + new Vector2(-radius, -radius),
                    center + new Vector2(-radius, radius),
                }, color);
                break;
        }
    }

    /// <summary>
    /// Icono del estado físico (UI-002: cuatro colores <b>y</b> cuatro iconos). Sano círculo lleno,
    /// lesión leve triángulo, lesión grave cuadrado partido, muerto cruz.
    /// </summary>
    public static void DrawStateIcon(CanvasItem target, Vector2 center, float radius, PhysicalState state, Color color)
    {
        switch (state)
        {
            case PhysicalState.Healthy:
                target.DrawCircle(center, radius, color);
                break;
            case PhysicalState.MinorInjury:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(0f, -radius),
                    center + new Vector2(radius, radius),
                    center + new Vector2(-radius, radius),
                }, color);
                break;
            case PhysicalState.SevereInjury:
                target.DrawRect(new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2f, radius)), color);
                target.DrawRect(new Rect2(center + new Vector2(-radius, radius * 0.35f), new Vector2(radius * 2f, radius * 0.65f)), color);
                break;
            default:
                target.DrawLine(center - new Vector2(radius, radius), center + new Vector2(radius, radius), color, 2f);
                target.DrawLine(center + new Vector2(-radius, radius), center + new Vector2(radius, -radius), color, 2f);
                break;
        }
    }

    /// <summary>
    /// Colores del <b>distintivo de dificultad</b> de un nodo de partido (RF-012): cinco niveles, de
    /// verde a rojo. El color nunca va solo: <see cref="DrawDifficultyIcon"/> le da además una silueta
    /// propia a cada nivel (UI-002).
    /// </summary>
    private static readonly Color[] DifficultyColors =
    {
        new("5fad56"), // 1
        new("9dc44d"), // 2
        new("e8c547"), // 3
        new("e08b3c"), // 4
        new("d9544d"), // 5
    };

    // Oscurecidos respecto al tema oscuro original (retoque del encargo pregon-resto, punto 4: "contraste
    // de los iconos de nodo del Mapa sobre pergamino"): los tonos claros pensados para un fondo casi
    // negro se lavaban sobre el pergamino claro del panel del grafo.
    private static readonly Color[] NodeColors =
    {
        new("3f5a80"), // partido de liga
        new("7d4a91"), // partido de élite
        new("1f7d70"), // mercado
        new("2d6ea3"), // clínica
        new("5f6570"), // taller (fase 3)
        new("6f8a34"), // entrenamiento
        new("8a6a2a"), // evento
        new("a33530"), // jefe
        new("925c85"), // inscripción
    };

    /// <summary>Color del nivel de dificultad, 1..5 (RF-012).</summary>
    public static Color DifficultyColor(int level) =>
        DifficultyColors[Mathf.Clamp(level - 1, 0, DifficultyColors.Length - 1)];

    /// <summary>Color del tipo de nodo (RF-011). El mercado se destaca aparte, en el mapa (RF-011b).</summary>
    public static Color Of(NodeKind kind) => NodeColors[Mathf.Clamp((int)kind, 0, NodeColors.Length - 1)];

    /// <summary>
    /// Silueta del nivel de dificultad (RF-012, UI-002: color <b>e</b> icono). Cinco formas distintas,
    /// crecientes en número de puntas: círculo, triángulo, rombo, cuadrado y estrella de cuatro puntas.
    /// </summary>
    public static void DrawDifficultyIcon(CanvasItem target, Vector2 center, float radius, int level, Color color)
    {
        switch (Mathf.Clamp(level, 1, 5))
        {
            case 1:
                target.DrawCircle(center, radius, color);
                break;
            case 2:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(0f, -radius),
                    center + new Vector2(radius, radius * 0.8f),
                    center + new Vector2(-radius, radius * 0.8f),
                }, color);
                break;
            case 3:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(0f, -radius),
                    center + new Vector2(radius, 0f),
                    center + new Vector2(0f, radius),
                    center + new Vector2(-radius, 0f),
                }, color);
                break;
            case 4:
                target.DrawRect(new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2f, radius * 2f)), color);
                break;
            default:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(0f, -radius),
                    center + new Vector2(radius * 0.34f, -radius * 0.34f),
                    center + new Vector2(radius, 0f),
                    center + new Vector2(radius * 0.34f, radius * 0.34f),
                    center + new Vector2(0f, radius),
                    center + new Vector2(-radius * 0.34f, radius * 0.34f),
                    center + new Vector2(-radius, 0f),
                    center + new Vector2(-radius * 0.34f, -radius * 0.34f),
                }, color);
                break;
        }
    }
}
