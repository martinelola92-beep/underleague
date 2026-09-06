using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Aviso efímero sobre el campo: unas pocas líneas en un panel oscuro que aparecen al soltar a un
/// jugador y se desvanecen solas. Existe porque el efecto de una colocación —que un perk se encienda o
/// se apague— ocurría <b>sin decirlo</b>, y el principio rector es que nada de lo que pasa en el partido
/// puede ser una sorpresa (RF-012d): si mover a alguien apaga su perk, se dice ahí mismo.
/// <para>
/// No decide nada (RT-014): recibe las líneas ya compuestas por la pantalla, que a su vez las saca de
/// <c>Sim.Perks.LineupPerkPreviewer</c>. Un aviso nuevo <b>sustituye</b> al anterior: nunca se apilan,
/// porque el jugador arrastra fichas seguidas y una pila de avisos taparía el campo.
/// </para>
/// </summary>
public partial class Toast : Control
{
    /// <summary>Segundos a plena opacidad antes de empezar a desvanecerse.</summary>
    private const float Hold = 2.1f;

    /// <summary>Segundos que dura el desvanecido. Hold + Fade = los ~3 s del aviso.</summary>
    private const float Fade = 0.9f;

    private const float Padding = 10f;
    private const float LineHeight = 17f;

    private readonly List<ToastLine> _lines = new();

    private float _age;

    /// <summary>Esquina inferior izquierda del panel, en coordenadas del padre: el aviso crece hacia arriba.</summary>
    public Vector2 BottomLeft { get; set; }

    /// <summary>Ancho máximo del panel; las líneas más largas se recortan al medir.</summary>
    public float MaximumWidth { get; set; } = 820f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    /// <summary>
    /// Enseña estas líneas y reinicia el reloj. Con la lista vacía el aviso se apaga: "no hay nada que
    /// decir" es un resultado legítimo y no se anuncia con un panel vacío.
    /// </summary>
    public void Post(IReadOnlyList<ToastLine> lines)
    {
        _lines.Clear();
        if (lines is not null)
        {
            _lines.AddRange(lines);
        }

        _age = 0f;
        Modulate = Colors.White;
        Visible = _lines.Count > 0;
        if (!Visible)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        float width = 0f;
        foreach (var line in _lines)
        {
            width = Mathf.Max(width, font.GetStringSize(line.Text, HorizontalAlignment.Left, -1f, Style.TextSmall).X);
        }

        Size = new Vector2(
            Mathf.Min(width + (2f * Padding), MaximumWidth),
            (_lines.Count * LineHeight) + (2f * Padding));
        Position = new Vector2(BottomLeft.X, BottomLeft.Y - Size.Y);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _age += (float)delta;
        if (_age >= Hold + Fade)
        {
            Visible = false;
            return;
        }

        float alpha = _age <= Hold ? 1f : 1f - ((_age - Hold) / Fade);
        Modulate = new Color(1f, 1f, 1f, alpha);
    }

    public override void _Draw()
    {
        if (_lines.Count == 0)
        {
            return;
        }

        var rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, new Color(Style.Background, 0.94f));
        DrawRect(rect, Style.Line, false, 1f);

        var font = GetThemeDefaultFont();
        for (int i = 0; i < _lines.Count; i++)
        {
            Style.DrawText(
                this,
                font,
                new Vector2(Padding, Padding + (i * LineHeight)),
                _lines[i].Text,
                Style.TextSmall,
                _lines[i].Color,
                Size.X - (2f * Padding));
        }
    }
}

/// <summary>Una línea del aviso: el texto ya localizado y el color con el que se lee.</summary>
public readonly record struct ToastLine(string Text, Color Color);
