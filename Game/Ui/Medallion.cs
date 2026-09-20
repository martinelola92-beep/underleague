using Godot;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui;

/// <summary>
/// Retrato de marcador de posición de un jugador: un medallón redondo —marco fino, fondo por raza claro,
/// un único rasgo dominante en tinta oscura por raza y un distintivo de posición en la esquina— dibujado
/// por código (decisión del revisor, 20 sep 2026: "cada vez que usemos ficha de jugador, el diseño debe
/// ser el mismo o muy similar"). Es <b>el único sitio</b> donde se decide cómo se ve un jugador:
/// <see cref="PlayerCard"/> lo usa en sus tres estados y <c>PitchView</c> lo usa para las fichas del
/// campo, así que la lista de plantilla y la cuadrícula de colocación enseñan al mismo jugador con el
/// mismo dibujo (UI-010).
/// <para>
/// Fase 1, sin arte (regla de fase, §7): es procedural y <b>determinista</b> —nunca <c>System.Random</c>,
/// misma disciplina que <see cref="Pregon.Jitter"/>— y la única variación por individuo es una función
/// del id de jugador, no del azar. Legible a 20, 34 y 44 px (arreglo del revisor, 20 sep 2026: la primera
/// versión leía como una mancha oscura a esos tamaños porque la cabeza rellenaba de tinta la mayor parte
/// del disco) porque el fondo de raza es lo que domina el disco y el rasgo de raza es un único trazo
/// grueso, no un relleno; y <b>distinguible en gris</b> (UI-002): el fondo lleva el color de la raza pero
/// el rasgo y el distintivo de posición son forma, no solo color.
/// </para>
/// </summary>
public static class Medallion
{
    private static readonly Color FrameDefault = new("8a6a3a");

    /// <summary>Tinta del rasgo de raza y del contorno: siempre la misma sobre cualquier fondo, para que la forma se lea igual.</summary>
    private static readonly Color Ink = new("241a10");

    private static readonly Color[] RaceBackgrounds =
    {
        new("e0c49a"), // Human
        new("a8cf74"), // Orc
        new("cbe8d4"), // Elf
        new("d9b479"), // Dwarf
        new("c7d2c0"), // Undead
        new("bcaee0"), // DarkElf
        new("e79a7a"), // Demon
        new("d194a8"), // Vampire
        new("9fdb8e"), // Lizard
    };

    /// <summary>Fondo de raza del medallón, por si una pantalla necesita el mismo color en otro sitio (p.ej. una placa).</summary>
    public static Color BackgroundOf(Race race) => RaceBackgrounds[(int)race];

    /// <summary>
    /// Dibuja el medallón completo: marco, fondo de raza, rasgo dominante y distintivo de posición.
    /// <paramref name="center"/> y <paramref name="radius"/> son locales a <paramref name="target"/>.
    /// <paramref name="ringColor"/> sustituye el bronce del marco —lo usa <c>PitchView</c> con el color de
    /// posición (arreglo del revisor, 20 sep 2026: en las fichas del campo el medallón añade identidad,
    /// no sustituye el código de color de posición que había antes de este componente).
    /// </summary>
    public static void Draw(CanvasItem target, Vector2 center, float radius, Race race, Position position, int playerId, Color? ringColor = null)
    {
        // Marco fino (arreglo del revisor): el anillo visible es solo el 10% exterior del radio, para que
        // el fondo de raza —lo que de verdad distingue al medallón en gris a tamaño pequeño— domine el
        // disco en vez de quedar aplastado entre un marco grueso y una cabeza oscura.
        target.DrawCircle(center, radius, ringColor ?? FrameDefault);
        target.DrawCircle(center, radius * 0.90f, RaceBackgrounds[(int)race]);
        target.DrawArc(center, radius * 0.995f, 0f, Mathf.Tau, 28, Ink, Mathf.Max(1f, radius * 0.05f));

        DrawMark(target, center, radius * 0.8f, race, playerId);
        DrawPositionBadge(target, center, radius, position);
    }

    /// <summary>
    /// El único rasgo dominante por raza (arreglo del revisor: "un solo rasgo dominante por raza", no un
    /// conjunto de detalles pequeños que a 20 px se emborronan en una mancha). Cada trazo es grueso y
    /// ocupa una fracción notable del disco, para que se lea sin ampliar la imagen.
    /// </summary>
    private static void DrawMark(CanvasItem target, Vector2 center, float r, Race race, int playerId)
    {
        // Variación por individuo, determinista (nunca System.Random): una pequeña rotación del rasgo.
        float spin = Pregon.Jitter((playerId * 613) + 11, 0.12f) * Mathf.Pi;

        switch (race)
        {
            case Race.Human:
                // Rasgo neutro: un aro grueso concéntrico, sin más detalle (la raza base).
                target.DrawArc(center, r * 0.72f, 0f, Mathf.Tau, 24, Ink, r * 0.24f);
                break;

            case Race.Orc:
                DrawWedge(target, center + Rotate(new Vector2(-r * 0.30f, r * 0.58f), spin), r * 0.66f, Mathf.Pi / 2f + spin, Ink);
                DrawWedge(target, center + Rotate(new Vector2(r * 0.30f, r * 0.58f), spin), r * 0.66f, Mathf.Pi / 2f + spin, Ink);
                break;

            case Race.Elf:
                DrawWedge(target, center + Rotate(new Vector2(-r * 0.78f, -r * 0.02f), spin), r * 0.62f, Mathf.Pi + spin, Ink);
                DrawWedge(target, center + Rotate(new Vector2(r * 0.78f, -r * 0.02f), spin), r * 0.62f, spin, Ink);
                break;

            case Race.Dwarf:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(-r * 0.62f, r * 0.05f),
                    center + new Vector2(r * 0.62f, r * 0.05f),
                    center + new Vector2(r * 0.46f, r * 0.92f),
                    center + new Vector2(-r * 0.46f, r * 0.92f),
                }, Ink);
                break;

            case Race.Undead:
                target.DrawLine(center + Rotate(new Vector2(-r * 0.58f, -r * 0.58f), spin), center + Rotate(new Vector2(r * 0.58f, r * 0.58f), spin), Ink, r * 0.30f);
                target.DrawLine(center + Rotate(new Vector2(-r * 0.58f, r * 0.58f), spin), center + Rotate(new Vector2(r * 0.58f, -r * 0.58f), spin), Ink, r * 0.30f);
                break;

            case Race.DarkElf:
                target.DrawColoredPolygon(new[]
                {
                    center + Rotate(new Vector2(0f, -r * 0.82f), spin),
                    center + Rotate(new Vector2(r * 0.62f, 0f), spin),
                    center + Rotate(new Vector2(0f, r * 0.82f), spin),
                    center + Rotate(new Vector2(-r * 0.62f, 0f), spin),
                }, Ink);
                break;

            case Race.Demon:
                DrawWedge(target, center + Rotate(new Vector2(-r * 0.42f, -r * 0.40f), spin), r * 0.62f, -Mathf.Pi / 2f + spin, Ink);
                DrawWedge(target, center + Rotate(new Vector2(r * 0.42f, -r * 0.40f), spin), r * 0.62f, -Mathf.Pi / 2f + spin, Ink);
                break;

            case Race.Vampire:
                // Un solo colmillo apuntando hacia abajo: el único rasgo que no comparte ni posición ni
                // orientación con ningún otro (arreglo del revisor: un rasgo dominante, no un conjunto).
                DrawWedge(target, center + Rotate(new Vector2(0f, r * 0.45f), spin), r * 0.82f, (Mathf.Pi / 2f) + spin, Ink);
                break;

            default: // Lizard
                // Hocico apuntando al costado, no hacia abajo: para no leerse igual que el colmillo del
                // vampiro (arreglo del revisor, 20 sep 2026).
                DrawWedge(target, center + Rotate(new Vector2(r * 0.45f, r * 0.05f), spin), r * 0.85f, spin, Ink);
                break;
        }
    }

    private static Vector2 Rotate(Vector2 v, float angle) => v.Rotated(angle);

    /// <summary>Cuña triangular ancha y corta: el bloque de construcción de casi todos los rasgos de raza.</summary>
    private static void DrawWedge(CanvasItem target, Vector2 tip, float length, float angle, Color color)
    {
        var forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var side = new Vector2(-forward.Y, forward.X) * (length * 0.5f);
        var basePoint = tip - (forward * length);
        target.DrawColoredPolygon(new[] { tip, basePoint + side, basePoint - side }, color);
    }

    /// <summary>Distintivo de posición en la esquina inferior derecha: color y forma (UI-002), como en <see cref="Style"/>.</summary>
    private static void DrawPositionBadge(CanvasItem target, Vector2 center, float radius, Position position)
    {
        var badgeCenter = center + new Vector2(radius * 0.64f, radius * 0.64f);
        float badgeR = radius * 0.36f;
        target.DrawCircle(badgeCenter, badgeR, Ink);
        target.DrawCircle(badgeCenter, badgeR * 0.82f, Style.Of(position));
        Style.DrawPositionIcon(target, badgeCenter, badgeR * 0.5f, position, Ink);
    }
}
