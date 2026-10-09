using System.Collections.Generic;
using Godot;
using Underleague.Game.Ui.Knavall;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Quién es quién en la pausa de un suceso (BX-15/BX-17): un cartel sobre la cabeza de cada implicado —el que hace la
/// falta, el que cae, el amonestado, el lesionado— con su papel y su nombre. «No queda claro quién ha caído ni quién
/// ha hecho la falta… escucho un grito, no sé quién es»: el grito y el sello llegan ahora con nombre encima.
/// <para>
/// No decide nada (RT-014): la pantalla le pasa, cada fotograma, dónde está la cabeza de cada implicado en pantalla
/// y qué papel tiene según los eventos del momento. Dibujado por código: relleno sustituible.
/// </para>
/// </summary>
[Tool]
[GlobalClass]
public partial class MomentTags : Control
{
    /// <summary>Un cartel: dónde (coordenadas de este control), el papel y el nombre, y el color del papel.</summary>
    public readonly record struct Tag(Vector2 Anchor, string Role, string Name, Color Tone);

    private readonly List<Tag> _tags = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (Engine.IsEditorHint())
        {
            _tags.Add(new Tag(new Vector2(300f, 300f), "FALTA", "Grok Zampalobos", Ink.Red));
            _tags.Add(new Tag(new Vector2(520f, 330f), "AL SUELO", "Juno Duque", Ink.Black));
        }
    }

    /// <summary>Los carteles de este fotograma; lista vacía para ocultarlos.</summary>
    public void SetTags(IReadOnlyList<Tag> tags)
    {
        _tags.Clear();
        _tags.AddRange(tags);
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (int i = 0; i < _tags.Count; i++)
        {
            DrawTag(_tags[i], i);
        }
    }

    private void DrawTag(Tag tag, int index)
    {
        const int RoleSize = 18;
        const int NameSize = 20;
        float width = Mathf.Max(Ink.Width(Ink.Heavy, tag.Role, RoleSize), Ink.Width(Ink.Data, tag.Name, NameSize)) + 30f;
        width = Mathf.Clamp(width, 120f, 300f);
        const float Height = 58f;

        // Encima de la cabeza, con un pico que la señala; dos carteles vecinos se separan en altura.
        var topLeft = tag.Anchor - new Vector2(width / 2f, Height + 22f + ((index % 2) * 14f));
        var rect = new Rect2(topLeft, new Vector2(width, Height));
        var beak = new[] { tag.Anchor + new Vector2(-10f, -22f - ((index % 2) * 14f)), tag.Anchor + new Vector2(10f, -22f - ((index % 2) * 14f)), tag.Anchor - new Vector2(0f, 6f) };
        Ink.Slab(this, rect, Ink.Paper, 40 + index, 1.4f, 3f, new Vector2(4f, 5f));
        Ink.Poly(this, beak, Ink.Paper, 3f);
        DrawColoredPolygon(new[] { beak[0] + new Vector2(2f, 2f), beak[1] + new Vector2(-2f, 2f), beak[2] + new Vector2(0f, -4f) }, Ink.Paper);

        var roleRect = new Rect2(rect.Position + new Vector2(8f, 6f), new Vector2(width - 16f, 22f));
        Ink.Brush(this, roleRect, tag.Tone, 60 + index);
        Ink.Outlined(this, Ink.Heavy, roleRect.Position + new Vector2(0f, -1f), tag.Role, RoleSize, Ink.Paper, Ink.Black, 4, roleRect.Size.X, HorizontalAlignment.Center);
        Ink.Text(this, Ink.Data, rect.Position + new Vector2(6f, 30f), Ink.Fit(Ink.Data, tag.Name, NameSize, width - 12f), NameSize, Ink.Black, width - 12f, HorizontalAlignment.Center);
    }
}
