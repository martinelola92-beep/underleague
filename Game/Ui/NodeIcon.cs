using Godot;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// Icono de <see cref="NodeKind"/>, dibujado por código (regla de fase 10 de <c>CLAUDE.md</c>: nada de
/// imágenes importadas). Sustituye al círculo de color con tres letras que llevaba el mapa (encargo
/// mapa-pregon): cada tipo de nodo tiene una <b>silueta propia</b>, para que UI-002 (color <b>y</b> forma)
/// se cumpla incluso en gris, sin calaveras ni huesos (RA-026).
/// <para>
/// Comparte esta clase <see cref="MapView"/> (el grafo del mapa, iconos pequeños) y <see cref="NodeBadge"/>
/// (la lista de destinos) y <see cref="MapLegend"/> (la leyenda, a mayor tamaño): un solo sitio para no
/// tener tres dibujos distintos del mismo tipo de nodo.
/// </para>
/// <para>
/// Solo dibuja el <b>glifo</b> — nunca el relleno ni el anillo de estado (accesible/visitado/apagado),
/// que sigue siendo responsabilidad de quien llama, con el color que corresponda a ese estado (RT-014: la
/// pantalla no decide nada, solo pinta lo que ya sabe).
/// </para>
/// </summary>
public static class NodeIcon
{
    /// <summary>Dibuja el glifo de <paramref name="kind"/> centrado en <paramref name="center"/>, con radio <paramref name="radius"/> y trazo <paramref name="color"/>.</summary>
    public static void Draw(CanvasItem target, NodeKind kind, Vector2 center, float radius, Color color, float thickness = 1.4f)
    {
        switch (kind)
        {
            case NodeKind.LeagueMatch:
                DrawLeagueMatch(target, center, radius, color, thickness);
                break;
            case NodeKind.EliteMatch:
                DrawLeagueMatch(target, center, radius, color, thickness);
                DrawCrown(target, center, radius, color, thickness * 0.9f, band: -1.0f, tip: -1.34f);
                break;
            case NodeKind.Market:
                DrawMarket(target, center, radius, color, thickness);
                break;
            case NodeKind.Clinic:
                DrawClinic(target, center, radius, color, thickness);
                break;
            case NodeKind.Workshop:
                DrawWorkshop(target, center, radius, color, thickness);
                break;
            case NodeKind.Training:
                DrawTraining(target, center, radius, color, thickness);
                break;
            case NodeKind.Event:
                DrawEvent(target, center, radius, color, thickness);
                break;
            case NodeKind.Boss:
                DrawBoss(target, center, radius, color, thickness);
                break;
        }
    }

    private static Vector2 P(Vector2 center, float radius, float x, float y) => center + (new Vector2(x, y) * radius);

    private static Vector2[] Closed(Vector2[] pts)
    {
        var closed = new Vector2[pts.Length + 1];
        System.Array.Copy(pts, closed, pts.Length);
        closed[pts.Length] = pts[0];
        return closed;
    }

    /// <summary>Escudo partido con balón encima: el partido ordinario. La base que <see cref="NodeKind.EliteMatch"/> corona.</summary>
    private static void DrawLeagueMatch(CanvasItem target, Vector2 center, float radius, Color color, float thickness)
    {
        var shield = new[]
        {
            P(center, radius, -0.62f, -0.55f), P(center, radius, 0.62f, -0.55f),
            P(center, radius, 0.62f, 0.05f), P(center, radius, 0f, 0.68f), P(center, radius, -0.62f, 0.05f),
        };
        target.DrawPolyline(Closed(shield), color, thickness, true);
        target.DrawLine(P(center, radius, 0f, -0.55f), P(center, radius, 0f, 0.68f), color, thickness * 0.85f);

        var ballCenter = P(center, radius, 0f, -0.62f);
        float ballRadius = radius * 0.34f;
        target.DrawArc(ballCenter, ballRadius, 0f, Mathf.Tau, 16, color, thickness * 0.85f, true);
        target.DrawLine(ballCenter + new Vector2(-0.5f * ballRadius, 0f), ballCenter + new Vector2(0.5f * ballRadius, 0f), color, thickness * 0.7f);
    }

    /// <summary>Corona de tres puntas sobre el balón (partido de élite, RF-011). <paramref name="band"/>/<paramref name="tip"/> en unidades de radio.</summary>
    private static void DrawCrown(CanvasItem target, Vector2 center, float radius, Color color, float thickness, float band, float tip)
    {
        float valley = (band + tip) * 0.5f;
        var pts = new[]
        {
            P(center, radius, -0.22f, band), P(center, radius, -0.22f, valley),
            P(center, radius, -0.11f, (band + valley) * 0.5f), P(center, radius, 0f, tip),
            P(center, radius, 0.11f, (band + valley) * 0.5f), P(center, radius, 0.22f, valley),
            P(center, radius, 0.22f, band),
        };
        target.DrawPolyline(pts, color, thickness, true);
    }

    /// <summary>Bolsa de monedas con una moneda suelta al lado: el mercado (RF-011b), la única tienda del juego.</summary>
    private static void DrawMarket(CanvasItem target, Vector2 center, float radius, Color color, float thickness)
    {
        var sack = new[]
        {
            P(center, radius, -0.15f, -0.65f), P(center, radius, 0.15f, -0.65f),
            P(center, radius, 0.5f, 0f), P(center, radius, 0.38f, 0.65f),
            P(center, radius, -0.38f, 0.65f), P(center, radius, -0.5f, 0f),
        };
        target.DrawPolyline(Closed(sack), color, thickness, true);
        target.DrawArc(P(center, radius, 0f, -0.62f), radius * 0.18f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 8, color, thickness * 0.8f, true);

        var coin = P(center, radius, 0.58f, 0.55f);
        float coinRadius = radius * 0.22f;
        target.DrawArc(coin, coinRadius, 0f, Mathf.Tau, 14, color, thickness * 0.8f, true);
        target.DrawLine(coin + new Vector2(-0.5f * coinRadius, 0f), coin + new Vector2(0.5f * coinRadius, 0f), color, thickness * 0.6f);
    }

    /// <summary>
    /// Cruz de vendas: la clínica (RF-094), cura garantizada. Un mortero legible a tamaño de nodo pedía
    /// más trazos de los que un glifo de ~12 px aguanta sin volverse una mancha (revisión visual, 20 sep
    /// 2026: se leía como una carita sonriente) — la cruz gruesa con la venda cruzada en diagonal es el
    /// símbolo médico que se reconoce de un vistazo, con o sin color.
    /// </summary>
    private static void DrawClinic(CanvasItem target, Vector2 center, float radius, Color color, float thickness)
    {
        target.DrawLine(P(center, radius, -0.62f, 0f), P(center, radius, 0.62f, 0f), color, thickness * 1.8f);
        target.DrawLine(P(center, radius, 0f, -0.62f), P(center, radius, 0f, 0.62f), color, thickness * 1.8f);

        // La venda: una tira diagonal cruzando una de las puntas, más fina que los brazos de la cruz.
        target.DrawLine(P(center, radius, 0.15f, -0.75f), P(center, radius, 0.62f, -0.28f), color, thickness);
        target.DrawLine(P(center, radius, 0.3f, -0.78f), P(center, radius, 0.47f, -0.61f), color, thickness * 0.7f);
    }

    /// <summary>
    /// Yunque bloque con garfio: el taller de implantes (RF-095, fase 3). Silueta simplificada a dos
    /// bloques (revisión visual, 20 sep 2026: el perfil con cuerno se leía como un pájaro a tamaño de
    /// nodo) — superficie ancha arriba, base estrecha abajo, es la lectura inmediata de "yunque" sin
    /// detalle que se pierda por debajo del grosor de trazo.
    /// </summary>
    private static void DrawWorkshop(CanvasItem target, Vector2 center, float radius, Color color, float thickness)
    {
        var top = new[]
        {
            P(center, radius, -0.62f, -0.15f), P(center, radius, 0.62f, -0.15f),
            P(center, radius, 0.62f, 0.15f), P(center, radius, -0.62f, 0.15f),
        };
        target.DrawPolyline(Closed(top), color, thickness, true);

        var stem = new[]
        {
            P(center, radius, -0.22f, 0.15f), P(center, radius, 0.22f, 0.15f),
            P(center, radius, 0.34f, 0.6f), P(center, radius, -0.34f, 0.6f),
        };
        target.DrawPolyline(Closed(stem), color, thickness, true);

        target.DrawArc(P(center, radius, 0.35f, -0.62f), radius * 0.24f, Mathf.DegToRad(-20f), Mathf.DegToRad(210f), 12, color, thickness * 0.9f, true);
    }

    /// <summary>Diana con dardo: el entrenamiento.</summary>
    private static void DrawTraining(CanvasItem target, Vector2 center, float radius, Color color, float thickness)
    {
        target.DrawArc(center, radius * 0.75f, 0f, Mathf.Tau, 24, color, thickness * 0.8f, true);
        target.DrawArc(center, radius * 0.48f, 0f, Mathf.Tau, 20, color, thickness * 0.8f, true);
        target.DrawArc(center, radius * 0.22f, 0f, Mathf.Tau, 16, color, thickness * 0.8f, true);

        var tail = P(center, radius, 0.8f, -0.8f);
        target.DrawLine(tail, P(center, radius, 0.05f, 0.02f), color, thickness);
        target.DrawLine(tail, P(center, radius, 0.95f, -0.55f), color, thickness * 0.8f);
        target.DrawLine(tail, P(center, radius, 0.55f, -0.95f), color, thickness * 0.8f);
    }

    /// <summary>Dos dados, cada uno con su número de tantos: el evento aleatorio.</summary>
    private static void DrawEvent(CanvasItem target, Vector2 center, float radius, Color color, float thickness)
    {
        DrawDie(target, P(center, radius, -0.62f, -0.12f), radius * 0.52f, color, thickness, pipOne: true, pipTwo: true, pipThree: false);
        DrawDie(target, P(center, radius, 0.06f, 0.18f), radius * 0.58f, color, thickness, pipOne: true, pipTwo: true, pipThree: true);
    }

    private static void DrawDie(CanvasItem target, Vector2 topLeft, float side, Color color, float thickness, bool pipOne, bool pipTwo, bool pipThree)
    {
        target.DrawRect(new Rect2(topLeft, new Vector2(side, side)), color, false, thickness * 0.85f);
        float pip = side * 0.11f;
        if (pipOne)
        {
            target.DrawCircle(topLeft + new Vector2(side * 0.28f, side * 0.28f), pip, color);
        }

        if (pipTwo)
        {
            target.DrawCircle(topLeft + new Vector2(side * 0.72f, side * 0.72f), pip, color);
        }

        if (pipThree)
        {
            target.DrawCircle(topLeft + new Vector2(side * 0.5f, side * 0.5f), pip, color);
        }
    }

    /// <summary>Yelmo con visera y corona: el jefe del acto (RF-001).</summary>
    private static void DrawBoss(CanvasItem target, Vector2 center, float radius, Color color, float thickness)
    {
        target.DrawArc(P(center, radius, 0f, -0.05f), radius * 0.55f, Mathf.Pi, Mathf.Tau, 20, color, thickness, true);
        target.DrawLine(P(center, radius, -0.55f, -0.05f), P(center, radius, -0.4f, 0.55f), color, thickness);
        target.DrawLine(P(center, radius, 0.55f, -0.05f), P(center, radius, 0.4f, 0.55f), color, thickness);
        target.DrawLine(P(center, radius, -0.4f, 0.55f), P(center, radius, 0.4f, 0.55f), color, thickness);
        target.DrawLine(P(center, radius, -0.32f, 0.15f), P(center, radius, 0.32f, 0.15f), color, thickness * 0.85f);
        DrawCrown(target, center, radius, color, thickness * 0.9f, band: -0.58f, tip: -0.95f);
    }
}
