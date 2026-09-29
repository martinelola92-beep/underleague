using System;
using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Knavall;

/// <summary>
/// Control dibujado a mano con <b>zonas</b>: rectángulos que llevan su cartel de ayuda (<see cref="Tip"/>)
/// y, si se pueden pulsar, su acción. Es la base de los paneles de Knavall (ADR 0162): el componente pinta
/// en <see cref="_Draw"/> y, mientras pinta, declara sus zonas con <see cref="Zone"/>; el ratón, los clics y
/// los carteles salen solos de ahí, así que dibujo y zona nunca se desalinean.
/// <para>
/// El cartel se elige por zona con <c>_GetTooltip</c>: cada zona devuelve un texto distinto, así que Godot
/// cambia de cartel al pasar de un icono al de al lado sin que el componente tenga que saberlo.
/// </para>
/// </summary>
public partial class InkCanvas : Control
{
    private readonly List<(Rect2 Rect, Tip? Tip, Action? Click, string Key)> _zones = new();
    private Tip? _pending;

    /// <summary>Clave de la zona pulsable bajo el ratón, o vacía. Para pintar el estado «encima».</summary>
    protected string HoverKey { get; private set; } = string.Empty;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>Se llama al principio de cada <c>_Draw</c>: las zonas se declaran de nuevo al pintar.</summary>
    protected void ClearZones() => _zones.Clear();

    /// <summary>
    /// Declara una zona. La última declarada gana si se solapan (lo pintado encima es lo que se señala).
    /// <paramref name="key"/> identifica la zona pulsable para <see cref="HoverKey"/>.
    /// </summary>
    protected void Zone(Rect2 rect, Tip? tip, Action? click = null, string key = "")
    {
        _zones.Add((rect, tip, click, key.Length > 0 ? key : (click is not null ? rect.ToString() : string.Empty)));
    }

    private int Find(Vector2 at, bool clickable)
    {
        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            var zone = _zones[i];
            if (zone.Rect.HasPoint(at) && (!clickable || zone.Click is not null) && (clickable || zone.Tip is not null))
            {
                return i;
            }
        }

        return -1;
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        int index = Find(atPosition, clickable: false);
        if (index < 0)
        {
            _pending = null;
            return string.Empty;
        }

        _pending = _zones[index].Tip;
        return _pending!.Title + "#" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public override GodotObject? _MakeCustomTooltip(string forText) =>
        _pending is { } tip ? InkTooltip.Build(tip) : null;

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } button:
                {
                    int index = Find(button.Position, clickable: true);
                    if (index >= 0)
                    {
                        _zones[index].Click!.Invoke();
                        AcceptEvent();
                    }

                    break;
                }

            case InputEventMouseMotion motion:
                {
                    int index = Find(motion.Position, clickable: true);
                    string key = index >= 0 ? _zones[index].Key : string.Empty;
                    MouseDefaultCursorShape = index >= 0 ? CursorShape.PointingHand : CursorShape.Arrow;
                    if (key != HoverKey)
                    {
                        HoverKey = key;
                        QueueRedraw();
                    }

                    break;
                }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && HoverKey.Length > 0)
        {
            HoverKey = string.Empty;
            QueueRedraw();
        }
    }
}
