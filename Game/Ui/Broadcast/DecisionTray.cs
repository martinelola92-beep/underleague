using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>Quién sale: su puesto y la casilla en la que estaba (docs/ui/README.md §7, BA-I/BB-F).</summary>
public sealed record OutgoingModel(int PlayerId, string Name, string Position, string Square, string State);

/// <summary>
/// Un candidato a entrar: nombre, puesto y estado; <see cref="Recommended"/> marca al sugerido.
/// </summary>
/// <param name="Risk">
/// Qué le pasa a ESTE si recibe la entrada de un perk letal rival en la casilla que queda libre, ya
/// formateado (ADR 0134 C), o vacío si no hay riesgo. Es lo que convierte la elección en una decisión en
/// vez de en un sorteo: el jugador ya veía puesto y estado, pero no lo que arriesgaba al meterlo.
/// </param>
public sealed record CandidateModel(int PlayerId, string Name, string Position, string State, bool Recommended, string Risk);

/// <summary>
/// Una respuesta que no es «entra este» (ADR 0134): dejar el hueco o que el lesionado siga jugando. Va en
/// la misma fila que los candidatos y se navega igual, porque es la misma decisión —a quién expongo— y no
/// un menú aparte.
/// </summary>
/// <param name="Kind"><c>decline</c> o <c>playOn</c>; lo usa la pantalla para saber qué se eligió.</param>
/// <param name="Risk">
/// Lo que arriesga esta respuesta, ya formateado, o vacío. Existe porque «que siga jugando» cuesta mucho
/// más que su subtítulo: el que se queda tocado multiplica su probabilidad de morir (ADR 0134 E). Sin este
/// número la bandeja enseñaría el riesgo de los suplentes y callaría el de la opción que más arriesga,
/// que es justo al revés de lo que hace falta para decidir.
/// </param>
public sealed record TrayOption(string Kind, string Label, string Sub, string Risk = "");

/// <summary>
/// Bandeja de decisión (docs/ui/README.md §7): sustituye a la fila de tiras cuando hay que decidir quién
/// entra — el campo queda visible al 100%, sin tapar nada más. Dice quién sale con su puesto y su casilla,
/// lista los candidatos marcando el recomendado, y se elige con ratón (clic en un candidato o en
/// «Confirmar») y con teclado (flechas para mover la selección, Intro para confirmar).
/// </summary>
public partial class DecisionTray : Control
{
    // 124, no 180 (campo en perspectiva, 19 sep 2026): con el borde cercano del césped anclado a y≈690 de
    // 1280×800 (1035 lógicos), una bandeja de 180 tapaba la línea de banda, un jugador y el balón. La bandeja
    // sustituye a las tiras y el campo tiene que verse al 100 % (docs/ui/README §7): título y fila de
    // casillas se juntan, sin cambiar el contenido.
    public const float DesignHeight = 124f;

    [Signal]
    public delegate void ChosenEventHandler(int playerId);

    /// <summary>El jugador eligió una respuesta que no es un candidato: <c>decline</c> o <c>playOn</c>.</summary>
    [Signal]
    public delegate void OptionChosenEventHandler(string kind);

    private OutgoingModel? _outgoing;
    private IReadOnlyList<CandidateModel> _candidates = System.Array.Empty<CandidateModel>();
    private IReadOnlyList<TrayOption> _options = System.Array.Empty<TrayOption>();
    private int _selected;

    private readonly List<Rect2> _candidateRects = new();
    private Rect2 _confirmRect;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0f, DesignHeight);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        Visible = false;
    }

    public void SetOutgoing(OutgoingModel outgoing)
    {
        _outgoing = outgoing;
        Visible = true;
        QueueRedraw();
    }

    public void SetCandidates(IReadOnlyList<CandidateModel> candidates, IReadOnlyList<TrayOption>? options = null)
    {
        _candidates = candidates;
        _options = options ?? System.Array.Empty<TrayOption>();
        _selected = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Recommended)
            {
                _selected = i;
                break;
            }
        }

        Visible = true;
        CallDeferred(Control.MethodName.GrabFocus);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button)
        {
            for (int i = 0; i < _candidateRects.Count; i++)
            {
                if (_candidateRects[i].HasPoint(button.Position))
                {
                    _selected = i;
                    QueueRedraw();
                    AcceptEvent();
                    return;
                }
            }

            if (_confirmRect.HasPoint(button.Position))
            {
                Confirm();
                AcceptEvent();
            }

            return;
        }

        if (@event is InputEventKey { Pressed: true } key)
        {
            if ((key.Keycode == Key.Left || key.Keycode == Key.Right) && Choices > 0)
            {
                int delta = key.Keycode == Key.Left ? -1 : 1;
                _selected = ((_selected + delta) % Choices + Choices) % Choices;
                QueueRedraw();
                AcceptEvent();
            }
            else if (key.Keycode is Key.Enter or Key.KpEnter)
            {
                Confirm();
                AcceptEvent();
            }
        }
    }

    /// <summary>Candidatos más respuestas que no son candidatos: todo se navega en la misma fila.</summary>
    private int Choices => _candidates.Count + _options.Count;

    private void Confirm()
    {
        if (_selected < 0 || _selected >= Choices)
        {
            return;
        }

        if (_selected < _candidates.Count)
        {
            EmitSignal(SignalName.Chosen, _candidates[_selected].PlayerId);
            return;
        }

        EmitSignal(SignalName.OptionChosen, _options[_selected - _candidates.Count].Kind);
    }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        Pregon.DrawParchment(this, Vector2.Zero, w, h, new Color("e3d2a8"), Pregon.VellumEdge, seed: 700, amplitude: 2f, edgeWidth: 2f);
        Style.DrawText(this, Pregon.Titular, new Vector2(20f, 4f), UiText.Get("ui.pregon.tray.caption"), Pregon.SizeHeader, Pregon.Sable, maxWidth: 600f);

        float ry = h - 84f;
        float x = 20f;

        // 320, no 260 (revisión visual de la galería, 19 sep 2026): «{puesto} · {casilla} · lesión leve»
        // cortaba en «lesión lev» a 260 — es la casilla más cargada de texto de la bandeja, con puesto,
        // casilla y estado a la vez, y el resto del paño tiene sitio de sobra para dárselo.
        const float OutgoingWidth = 320f;
        if (_outgoing is { } outgoing)
        {
            string outName = UiText.Get("ui.pregon.tray.outLabel", outgoing.Name);
            string outSub = UiText.Get("ui.pregon.tray.outSub", outgoing.Position, outgoing.Square, outgoing.State);
            DrawSlot(
                new Vector2(x, ry), OutgoingWidth, outName, outSub, string.Empty,
                new Color("e7c3b8"), selected: false, recommended: false);
        }

        x += OutgoingWidth + 20f;
        Style.DrawText(this, Pregon.Titular, new Vector2(x, ry + 6f), UiText.Get("ui.pregon.tray.arrow"), Pregon.SizeTitleSmall / 2, Pregon.Sable, maxWidth: 50f);
        x += 60f;

        float confirmW = 220f;
        _confirmRect = new Rect2(w - 20f - confirmW, ry + 8f, confirmW, 60f);

        // ADR 0134: la fila dejó de ser «los candidatos» para ser «las respuestas», y pueden llegar a cinco
        // (tres suplentes, dejar el hueco y seguir jugando). Con el ancho fijo de 260 de antes ya se comía
        // el botón de Confirmar con tres candidatos, así que se reparte el hueco disponible en vez de
        // suponerlo: la bandeja no puede crecer hacia abajo (DesignHeight tapa el campo).
        int choices = Choices;
        float available = _confirmRect.Position.X - 20f - x;
        float slotW = choices > 0
            ? Mathf.Clamp((available - (20f * (choices - 1))) / choices, 120f, 260f)
            : 0f;

        _candidateRects.Clear();
        for (int i = 0; i < _candidates.Count; i++)
        {
            var c = _candidates[i];
            _candidateRects.Add(new Rect2(x, ry, slotW, 76f));
            string sub = c.Recommended
                ? UiText.Get("ui.pregon.tray.candidateSubRecommended", c.Position, c.State, UiText.Get("ui.pregon.tray.recommended"))
                : UiText.Get("ui.pregon.tray.candidateSub", c.Position, c.State);
            DrawSlot(new Vector2(x, ry), slotW, c.Name, sub, c.Risk, Pregon.Vellum, i == _selected, c.Recommended, shield: true);
            x += slotW + 20f;
        }

        for (int i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            _candidateRects.Add(new Rect2(x, ry, slotW, 76f));
            DrawSlot(
                new Vector2(x, ry), slotW, option.Label, option.Sub, option.Risk,
                new Color("ded2c0"), _candidates.Count + i == _selected, recommended: false, shield: false);
            x += slotW + 20f;
        }

        Pregon.DrawParchment(this, new Vector2(_confirmRect.Position.X, _confirmRect.Position.Y), confirmW, 60f, Pregon.Or, Pregon.Sable, seed: 701, amplitude: 1.5f, edgeWidth: 2f);
        Pregon.DrawTextCentered(this, Pregon.Titular, _confirmRect.Position + new Vector2(0f, 14f), UiText.Get("ui.pregon.tray.confirm"), Pregon.SizeHeader, Pregon.Sable, confirmW);

        // La ayuda de control sube junto al título: abajo ya no sobra sitio entre la última respuesta y
        // Confirmar, y arriba estaba vacío.
        Style.DrawText(
            this, Pregon.DataSemiBold, new Vector2(w - 320f, 10f), UiText.Get("ui.pregon.tray.hint"),
            Pregon.SizeDataSmall, Pregon.InkBrown, maxWidth: 300f);
    }

    private void DrawSlot(Vector2 at, float w, string name, string sub, string third, Color fill, bool selected, bool recommended, bool shield = true)
    {
        Pregon.DrawParchment(this, at, w, 76f, fill, selected ? Pregon.Or : Pregon.VellumEdge, seed: (int)(at.X + at.Y), amplitude: 1.5f, edgeWidth: selected ? 3.5f : 1.5f);
        float textX = 10f;
        if (shield)
        {
            Pregon.DrawShield(this, at + new Vector2(10f, 10f), 40f, 50f, ours: true);
            textX = 58f;
        }

        Style.DrawText(this, Pregon.DataBold, at + new Vector2(textX, 4f), name, Pregon.SizeData, Pregon.Sable, maxWidth: w - textX - 8f);
        Style.DrawText(this, Pregon.DataSemiBold, at + new Vector2(textX, 32f), sub, Pregon.SizeDataSmall, recommended ? Pregon.Azur : Pregon.InkBrown, maxWidth: w - textX - 8f);
        if (third.Length > 0)
        {
            // El riesgo va en su propia línea y en el rojo del daño: es el dato por el que se cambia de
            // opinión, y perdido dentro del subtítulo no se lee (RF-012c, RF-012d).
            Style.DrawText(this, Pregon.DataSemiBold, at + new Vector2(textX, 54f), third, Pregon.SizeDataSmall, Pregon.Gules, maxWidth: w - textX - 8f);
        }
    }
}
