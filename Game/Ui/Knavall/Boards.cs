using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Knavall;

/// <summary>Un cartel de papel sin más: el fondo de un panel cuyo contenido son otros nodos.</summary>
public partial class SheetCanvas : Control
{
    /// <summary>Semilla del borde rasgado y de las manchas.</summary>
    public int Seed { get; set; } = 1;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw() => Ink.Sheet(this, new Rect2(Vector2.Zero, Size - new Vector2(8f, 8f)), Seed);
}

/// <summary>
/// Tablón de la plantilla (ADR 0162): el cartel de papel de la columna izquierda con un brochazo rojo por
/// grupo (TITULARES, SUPLENTES). Las filas son hijas suyas, colocadas por la pantalla.
/// </summary>
public partial class RosterBoard : Control
{
    /// <summary>Altura y rótulo de cada brochazo de cabecera.</summary>
    public List<(float Y, string Text)> Banners { get; } = new();

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        Ink.Sheet(this, new Rect2(Vector2.Zero, Size - new Vector2(8f, 8f)), 3030);
        int index = 0;
        foreach (var (y, text) in Banners)
        {
            string caption = text.ToUpperInvariant();
            float width = Ink.Width(Ink.Display, caption, 22) + 40f;
            Ink.Brush(this, new Rect2(12f, y, width, 32f), Ink.Red, 700 + (index * 13));
            Ink.Outlined(this, Ink.Display, new Vector2(30f, y - 1f), caption, 22, Ink.Paper, Ink.Black, 5);
            index++;
        }
    }
}
