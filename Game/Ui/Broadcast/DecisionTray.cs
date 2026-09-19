using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>Quién sale: su puesto y la casilla en la que estaba (docs/ui/README.md §7, BA-I/BB-F).</summary>
public sealed record OutgoingModel(int PlayerId, string Name, string Position, string Square, string State);

/// <summary>Un candidato a entrar: nombre, puesto y estado; <see cref="Recommended"/> marca al sugerido.</summary>
public sealed record CandidateModel(int PlayerId, string Name, string Position, string State, bool Recommended);

/// <summary>
/// Bandeja de decisión (docs/ui/README.md §7): sustituye a la fila de tiras cuando hay que decidir quién
/// entra — el campo queda visible al 100%, sin tapar nada más. Dice quién sale con su puesto y su casilla,
/// lista los candidatos marcando el recomendado, y se elige con ratón (clic en un candidato o en
/// «Confirmar») y con teclado (flechas para mover la selección, Intro para confirmar).
/// </summary>
public partial class DecisionTray : Control
{
    public const float DesignHeight = 180f;

    [Signal]
    public delegate void ChosenEventHandler(int playerId);

    private OutgoingModel? _outgoing;
    private IReadOnlyList<CandidateModel> _candidates = System.Array.Empty<CandidateModel>();
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

    public void SetCandidates(IReadOnlyList<CandidateModel> candidates)
    {
        _candidates = candidates;
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
            if ((key.Keycode == Key.Left || key.Keycode == Key.Right) && _candidates.Count > 0)
            {
                int delta = key.Keycode == Key.Left ? -1 : 1;
                _selected = ((_selected + delta) % _candidates.Count + _candidates.Count) % _candidates.Count;
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

    private void Confirm()
    {
        if (_selected >= 0 && _selected < _candidates.Count)
        {
            EmitSignal(SignalName.Chosen, _candidates[_selected].PlayerId);
        }
    }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        Pregon.DrawParchment(this, Vector2.Zero, w, h, new Color("e3d2a8"), Pregon.VellumEdge, seed: 700, amplitude: 2f, edgeWidth: 2f);
        Style.DrawText(this, Pregon.Fell, new Vector2(20f, 8f), UiText.Get("ui.pregon.tray.caption"), Pregon.SizeHeader, Pregon.Sable, maxWidth: 600f);

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
            DrawSlot(new Vector2(x, ry), OutgoingWidth, outName, outSub, new Color("e7c3b8"), selected: false, recommended: false);
        }

        x += OutgoingWidth + 20f;
        Style.DrawText(this, Pregon.FellBig, new Vector2(x, ry + 6f), UiText.Get("ui.pregon.tray.arrow"), Pregon.SizeTitleSmall / 2, Pregon.Sable, maxWidth: 50f);
        x += 60f;

        _candidateRects.Clear();
        for (int i = 0; i < _candidates.Count; i++)
        {
            var c = _candidates[i];
            var rect = new Rect2(x, ry, 260f, 76f);
            _candidateRects.Add(rect);
            string sub = c.Recommended
                ? UiText.Get("ui.pregon.tray.candidateSubRecommended", c.Position, c.State, UiText.Get("ui.pregon.tray.recommended"))
                : UiText.Get("ui.pregon.tray.candidateSub", c.Position, c.State);
            DrawSlot(new Vector2(x, ry), 260f, c.Name, sub, Pregon.Vellum, i == _selected, c.Recommended);
            x += 260f + 20f;
        }

        float confirmW = 220f;
        _confirmRect = new Rect2(w - 20f - confirmW, ry + 8f, confirmW, 60f);
        Pregon.DrawParchment(this, new Vector2(_confirmRect.Position.X, _confirmRect.Position.Y), confirmW, 60f, Pregon.Or, Pregon.Sable, seed: 701, amplitude: 1.5f, edgeWidth: 2f);
        Pregon.DrawTextCentered(this, Pregon.Fell, _confirmRect.Position + new Vector2(0f, 14f), UiText.Get("ui.pregon.tray.confirm"), Pregon.SizeHeader, Pregon.Sable, confirmW);

        Style.DrawText(this, Pregon.DataSemiBold, new Vector2(x, ry + 24f), UiText.Get("ui.pregon.tray.hint"), Pregon.SizeDataSmall, Pregon.InkBrown, maxWidth: _confirmRect.Position.X - x - 20f);
    }

    private void DrawSlot(Vector2 at, float w, string name, string sub, Color fill, bool selected, bool recommended)
    {
        Pregon.DrawParchment(this, at, w, 76f, fill, selected ? Pregon.Or : Pregon.VellumEdge, seed: (int)(at.X + at.Y), amplitude: 1.5f, edgeWidth: selected ? 3.5f : 1.5f);
        Pregon.DrawShield(this, at + new Vector2(10f, 10f), 40f, 50f, ours: true);
        Style.DrawText(this, Pregon.DataBold, at + new Vector2(58f, 6f), name, Pregon.SizeData, Pregon.Sable, maxWidth: w - 66f);
        Style.DrawText(this, Pregon.DataSemiBold, at + new Vector2(58f, 38f), sub, Pregon.SizeDataSmall, recommended ? Pregon.Azur : Pregon.InkBrown, maxWidth: w - 66f);
    }
}
