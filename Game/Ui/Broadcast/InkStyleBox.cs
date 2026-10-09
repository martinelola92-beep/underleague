using Godot;
using Underleague.Game.Ui;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// El dibujo de tinta del pregón (pergamino con borde irregular, sombra dura y contorno; o pendón con cola
/// de golondrina y orla) empaquetado como un <see cref="StyleBox"/> para que viva en una escena y no en un
/// <c>_Draw</c>.
/// <para>
/// Es el <b>relleno provisional</b> de las piezas de la interfaz que el revisor sustituye por sprites
/// propios (CLAUDE.md, regla 10): en el inspector, cada «Styles» de un <see cref="Panel"/>, un
/// <see cref="Button"/> o una <see cref="ProgressBar"/> se cambia de <c>InkStyleBox</c> a un
/// <c>StyleBoxTexture</c> con el sprite (admite nueve partes), y el código no se entera: sólo rellena
/// textos y estados. Guía en <c>docs/ui/editar-en-godot.md</c>.
/// </para>
/// <para>
/// <c>[Tool]</c> para que el editor lo pinte igual que el juego. Cada propiedad avisa con
/// <see cref="Resource.EmitChanged"/> para que el editor redibuje al tocarla.
/// </para>
/// </summary>
[Tool]
[GlobalClass]
public partial class InkStyleBox : StyleBox
{
    public enum InkShape
    {
        /// <summary>Rectángulo de borde irregular: papel, pergamino, tabla.</summary>
        Parchment,

        /// <summary>Pendón con cola de golondrina abajo (paños de equipo).</summary>
        Swallowtail,

        /// <summary>Píldora (rectángulo de extremos semicirculares): varales de madera. Con ancho = alto, un círculo.</summary>
        Pill,

        /// <summary>Banda con muesca en V a cada lado (banda de pregón); la profundidad de la muesca es <see cref="Notch"/>.</summary>
        Pennant,
    }

    private InkShape _shape = InkShape.Parchment;
    private Color _fill = new("4a3321");
    private Color _edge = Pregon.Sable;
    private int _seed = 1;
    private float _amplitude = 2f;
    private float _edgeWidth = 2f;
    private Vector2 _shadowOffset = new(4f, 5f);
    private Color _shadowColor = new(0f, 0f, 0f, 0.35f);
    private float _notch = 12f;
    private bool _orla;
    private Color _orlaColor = Pregon.Or;
    private float _orlaInset = 6f;
    private int _grainLines;
    private Color _grainColor = new("3a2718");

    [Export]
    public InkShape Shape { get => _shape; set { _shape = value; EmitChanged(); } }

    [Export]
    public Color Fill { get => _fill; set { _fill = value; EmitChanged(); } }

    [Export]
    public Color Edge { get => _edge; set { _edge = value; EmitChanged(); } }

    /// <summary>Semilla del trazo irregular: dos piezas con la misma semilla y el mismo tamaño tienen el mismo borde.</summary>
    [Export]
    public int Seed { get => _seed; set { _seed = value; EmitChanged(); } }

    /// <summary>Cuánto se desvía el borde de la recta, en píxeles. 0 = rectángulo limpio.</summary>
    [Export]
    public float Amplitude { get => _amplitude; set { _amplitude = value; EmitChanged(); } }

    /// <summary>Grosor del contorno; 0 = sin contorno.</summary>
    [Export]
    public float EdgeWidth { get => _edgeWidth; set { _edgeWidth = value; EmitChanged(); } }

    /// <summary>Desplazamiento de la sombra dura; alfa 0 en <see cref="ShadowColor"/> = sin sombra.</summary>
    [Export]
    public Vector2 ShadowOffset { get => _shadowOffset; set { _shadowOffset = value; EmitChanged(); } }

    [Export]
    public Color ShadowColor { get => _shadowColor; set { _shadowColor = value; EmitChanged(); } }

    /// <summary>Profundidad de la cola de golondrina (<see cref="InkShape.Swallowtail"/>) o de la muesca (<see cref="InkShape.Pennant"/>).</summary>
    [Export]
    public float Notch { get => _notch; set { _notch = value; EmitChanged(); } }

    /// <summary>Filete bordado hacia dentro del contorno.</summary>
    [Export]
    public bool Orla { get => _orla; set { _orla = value; EmitChanged(); } }

    [Export]
    public Color OrlaColor { get => _orlaColor; set { _orlaColor = value; EmitChanged(); } }

    [Export]
    public float OrlaInset { get => _orlaInset; set { _orlaInset = value; EmitChanged(); } }

    /// <summary>Vetas horizontales de tabla (el fondo del tablero lleva tres).</summary>
    [Export]
    public int GrainLines { get => _grainLines; set { _grainLines = value; EmitChanged(); } }

    [Export]
    public Color GrainColor { get => _grainColor; set { _grainColor = value; EmitChanged(); } }

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        float w = rect.Size.X;
        float h = rect.Size.Y;
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var local = _shape switch
        {
            InkShape.Swallowtail => Pregon.Swallowtail(w, h, _notch),
            InkShape.Pill => Pregon.StadiumPoly(w, h),
            InkShape.Pennant => new[]
            {
                new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w - _notch, h / 2f),
                new Vector2(w, h), new Vector2(0f, h), new Vector2(_notch, h / 2f),
            },
            _ => Pregon.RoughRect(w, h, _amplitude, _seed),
        };

        if (_shadowColor.A > 0f)
        {
            RenderingServer.CanvasItemAddPolygon(toCanvasItem, Shift(local, rect.Position + _shadowOffset), new[] { _shadowColor });
        }

        var outline = Shift(local, rect.Position);

        // Pase de materiales: la tabla lleva veta de madera y el resto grano de papel o de paño, multiplicados por
        // el color de la caja. Sin la textura, relleno plano como siempre.
        var material = _grainLines > 0 ? Art.Wood : Art.Parchment;
        if (material is null)
        {
            RenderingServer.CanvasItemAddPolygon(toCanvasItem, outline, new[] { _fill });
        }
        else
        {
            var uvs = new Vector2[outline.Length];
            var offset = new Vector2(_seed * 131 % 900, _seed * 59 % 900);
            for (int i = 0; i < outline.Length; i++)
            {
                uvs[i] = (outline[i] + offset) / Art.MaterialSize;
            }

            var tint = new Color(
                Mathf.Min(_fill.R * Art.DetailCompensation, 1f),
                Mathf.Min(_fill.G * Art.DetailCompensation, 1f),
                Mathf.Min(_fill.B * Art.DetailCompensation, 1f),
                _fill.A);
            RenderingServer.CanvasItemAddPolygon(toCanvasItem, outline, new[] { tint }, uvs, material.GetRid());
        }

        for (int i = 1; i <= (material is null ? _grainLines : 0); i++)
        {
            float y = rect.Position.Y + (i * h / (_grainLines + 1));
            var line = new[] { new Vector2(rect.Position.X + 8f, y), new Vector2(rect.End.X - 8f, y + Pregon.Jitter(400 + i, 2f)) };
            RenderingServer.CanvasItemAddPolyline(toCanvasItem, line, new[] { _grainColor }, 1.5f, true);
        }

        if (_edgeWidth > 0f)
        {
            RenderingServer.CanvasItemAddPolyline(toCanvasItem, Close(outline), new[] { _edge }, _edgeWidth, true);
        }

        if (_orla)
        {
            RenderingServer.CanvasItemAddPolyline(toCanvasItem, Close(Inset(local, _orlaInset, rect.Position)), new[] { _orlaColor }, 1.6f, true);
        }
    }

    private static Vector2[] Shift(Vector2[] poly, Vector2 offset)
    {
        var r = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            r[i] = poly[i] + offset;
        }

        return r;
    }

    private static Vector2[] Close(Vector2[] poly)
    {
        var r = new Vector2[poly.Length + 1];
        System.Array.Copy(poly, r, poly.Length);
        r[poly.Length] = poly[0];
        return r;
    }

    /// <summary>La misma orla que <see cref="Pregon.DrawOrla"/>: cada vértice hacia el centroide.</summary>
    private static Vector2[] Inset(Vector2[] poly, float inset, Vector2 offset)
    {
        var center = Vector2.Zero;
        foreach (var p in poly)
        {
            center += p;
        }

        center /= poly.Length;
        var inner = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            var dir = center - poly[i];
            inner[i] = poly[i] + (dir.LengthSquared() > 0.0001f ? dir.Normalized() * inset : Vector2.Zero) + offset;
        }

        return inner;
    }
}
