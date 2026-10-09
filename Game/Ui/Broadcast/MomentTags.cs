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

    private const int RoleSize = 18;
    private const int NameSize = 20;
    private const float TagHeight = 58f;

    public override void _Draw()
    {
        // Dos implicados juntos (el que entra y el que cae suelen estar a una casilla) darían carteles montados: cada
        // cartel que pisaría a uno ya puesto sube encima de él, con su pico estirado hasta la cabeza.
        var placed = new List<Rect2>();
        for (int i = 0; i < _tags.Count; i++)
        {
            var tag = _tags[i];
            float width = Mathf.Clamp(Mathf.Max(Ink.Width(Ink.Heavy, tag.Role, RoleSize), Ink.Width(Ink.Data, tag.Name, NameSize)) + 30f, 120f, 300f);
            var rect = new Rect2(tag.Anchor - new Vector2(width / 2f, TagHeight + 22f), new Vector2(width, TagHeight));
            for (int guard = 0; guard < 6 && placed.Exists(r => r.Grow(4f).Intersects(rect)); guard++)
            {
                rect.Position -= new Vector2(0f, TagHeight + 10f);
            }

            placed.Add(rect);
            DrawTag(tag, rect, i);
        }
    }

    private void DrawTag(Tag tag, Rect2 rect, int index)
    {
        float width = rect.Size.X;
        float lift = tag.Anchor.Y - rect.End.Y;
        var beak = new[] { new Vector2(tag.Anchor.X - 10f, rect.End.Y - 2f), new Vector2(tag.Anchor.X + 10f, rect.End.Y - 2f), tag.Anchor - new Vector2(0f, 6f) };
        if (lift > 30f)
        {
            // Subido sobre otro: el pico es un hilo de tinta hasta la cabeza.
            DrawLine(new Vector2(tag.Anchor.X, rect.End.Y), tag.Anchor - new Vector2(0f, 6f), Ink.Black, 2.5f, true);
            beak = new[] { new Vector2(tag.Anchor.X - 7f, rect.End.Y - 2f), new Vector2(tag.Anchor.X + 7f, rect.End.Y - 2f), new Vector2(tag.Anchor.X, rect.End.Y + 10f) };
        }

        Ink.Slab(this, rect, Ink.Paper, 40 + index, 1.4f, 3f, new Vector2(4f, 5f));
        Ink.Poly(this, beak, Ink.Paper, 3f);
        DrawColoredPolygon(new[] { beak[0] + new Vector2(2f, 2f), beak[1] + new Vector2(-2f, 2f), beak[2] + new Vector2(0f, -4f) }, Ink.Paper);

        var roleRect = new Rect2(rect.Position + new Vector2(8f, 6f), new Vector2(width - 16f, 22f));
        Ink.Brush(this, roleRect, tag.Tone, 60 + index);
        Ink.Outlined(this, Ink.Heavy, roleRect.Position + new Vector2(0f, -1f), tag.Role, RoleSize, Ink.Paper, Ink.Black, 4, roleRect.Size.X, HorizontalAlignment.Center);
        Ink.Text(this, Ink.Data, rect.Position + new Vector2(6f, 30f), Ink.Fit(Ink.Data, tag.Name, NameSize, width - 12f), NameSize, Ink.Black, width - 12f, HorizontalAlignment.Center);
    }
}
