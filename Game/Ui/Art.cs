using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// El arte en fichero de la interfaz (pase de materiales, 9 oct 2026; decisión del revisor: Claude produce o
/// descarga el arte). Un único sitio que sabe dónde están las texturas, para que las primitivas de dibujo
/// (<see cref="Knavall.Ink"/>, <see cref="Broadcast.Pregon"/>, <see cref="WoodTable"/>) pinten con material
/// y, si falta el fichero, sigan pintando como antes: nada depende de que el arte exista.
/// <para>
/// Las texturas de material son <b>mapas de detalle en gris</b> (media ~0,93): se multiplican por el color de la
/// paleta, así que el tono lo sigue decidiendo el código. Generadas por <c>tools/arte/materiales.py</c>; los
/// iconos, por <c>tools/arte/iconos.py</c> (game-icons.net, CC BY 3.0, <c>Game/Art/Icons/CREDITS.md</c>).
/// </para>
/// </summary>
public static class Art
{
    /// <summary>Lado de las texturas de material: un panel de hasta este tamaño no repite el dibujo.</summary>
    public const float MaterialSize = 2048f;

    /// <summary>Lo que oscurece de media un mapa de detalle; el relleno se aclara en esta proporción para conservar el tono.</summary>
    public const float DetailCompensation = 1.07f;

    private static readonly Dictionary<string, Texture2D?> Cache = new();

    public static Texture2D? Parchment => Load("res://Art/Textures/parchment_detail.png");

    public static Texture2D? Wood => Load("res://Art/Textures/wood_detail.png");

    /// <summary>Uno de los cuatro trazos de brocha seca (blanco con alfa), elegido por la semilla.</summary>
    public static Texture2D? Brush(int seed) => Load($"res://Art/Textures/brush_{((seed % 4) + 4) % 4:00}.png");

    /// <summary>El icono de silueta de un glifo (blanco con alfa), o null si ese glifo sigue dibujándose por código.</summary>
    public static Texture2D? Icon(string glyph) => Load($"res://Art/Icons/{glyph}.png");

    private static Texture2D? Load(string path)
    {
        if (!Cache.TryGetValue(path, out var texture))
        {
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Cache[path] = texture;
        }

        return texture;
    }

    /// <summary>
    /// Rellena <paramref name="polygon"/> con <paramref name="material"/> teñido de <paramref name="fill"/>. Las
    /// coordenadas de textura salen de la posición en pantalla más un desplazamiento por semilla, así que dos
    /// paneles vecinos no muestran la misma mancha. Sin textura, relleno plano: el dibujo de siempre.
    /// </summary>
    public static void FillPolygon(CanvasItem target, Vector2[] polygon, Color fill, Texture2D? material, int seed)
    {
        if (material is null || polygon.Length < 3)
        {
            target.DrawColoredPolygon(polygon, fill);
            return;
        }

        var offset = new Vector2(Hash(seed) % 900, Hash(seed * 7 + 3) % 900);
        var uvs = new Vector2[polygon.Length];
        for (int i = 0; i < polygon.Length; i++)
        {
            uvs[i] = (polygon[i] + offset) / MaterialSize;
        }

        var tint = new Color(
            Mathf.Min(fill.R * DetailCompensation, 1f),
            Mathf.Min(fill.G * DetailCompensation, 1f),
            Mathf.Min(fill.B * DetailCompensation, 1f),
            fill.A);
        target.DrawPolygon(polygon, new[] { tint }, uvs, material);
    }

    /// <summary>
    /// Borde quemado: dos o tres filetes oscuros y translúcidos hacia dentro del contorno, el envejecido del borde
    /// de un papel o de una tabla. <paramref name="inward"/> es el polígono ya encogido en pasos sucesivos.
    /// </summary>
    public static void Burn(CanvasItem target, Vector2[] outline, Color tone, float width, float alpha)
    {
        if (outline.Length < 3)
        {
            return;
        }

        var closed = new Vector2[outline.Length + 1];
        System.Array.Copy(outline, closed, outline.Length);
        closed[outline.Length] = outline[0];
        target.DrawPolyline(closed, new Color(tone, alpha), width, true);
    }

    private static int Hash(int seed)
    {
        unchecked
        {
            uint x = (uint)seed * 2654435761u;
            x ^= x >> 15;
            return (int)(x & 0x7fffffff);
        }
    }
}
