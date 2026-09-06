using Godot;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui;

/// <summary>
/// Campo del <b>partido en curso</b>: las 20 fichas y el balón en las posiciones continuas que grabó la
/// <see cref="MatchTrace"/> de <c>/Sim</c>, tick a tick. Es la hermana de <see cref="PitchView"/> —comparte
/// geometría (16x5, RF-056) y paleta (<see cref="Style"/>)— pero pinta cosas distintas: allí hay casillas
/// de colocación, aquí hay coordenadas continuas y una máquina de estados por jugador.
///
/// <para>
/// <b>No calcula nada</b> (RT-014): no simula, no decide y no interpola posiciones que no le hayan dado.
/// Lo único que hace con números es pasar de casilla a píxel y <b>suavizar entre dos ticks
/// consecutivos</b>, que es exactamente la interpolación que RT-020 permite en el render y prohíbe en la
/// lógica.
/// </para>
///
/// <para>
/// Lo que se lee de un vistazo, por capas: <b>color de relleno</b> = equipo, <b>dorsal</b> = quién es,
/// <b>anillo</b> = estado del jugador (RT-089c), <b>halo blanco</b> = lleva el balón. Los estados de baja
/// llevan además una cruz, para no depender del color (UI-002).
/// </para>
/// </summary>
public partial class MatchPitchView : Control
{
    /// <summary>Se ha pulsado sobre una ficha; -1 si el clic cayó en hierba (deselecciona).</summary>
    [Signal]
    public delegate void PlayerPickedEventHandler(int playerId);

    /// <summary>Radio de la ficha en fracción de casilla. Con casilla de 70 px son 20 px: caben dos cifras.</summary>
    private const float TokenRadius = 0.28f;

    /// <summary>Radio del balón: la mitad justa de la ficha, para que nunca se confunda con una.</summary>
    private const float BallRadius = 0.13f;

    /// <summary>Traza del partido; null mientras no haya partido reproducido.</summary>
    public MatchTrace? Trace { get; set; }

    /// <summary>Fotograma que se está pintando (índice, no tick).</summary>
    public int Frame { get; set; }

    /// <summary>Fracción 0..1 hacia el fotograma siguiente. Solo suaviza el dibujo (RT-020).</summary>
    public float Alpha { get; set; }

    /// <summary>Id del jugador seguido, o -1. Es de quien se pinta la correa.</summary>
    public int SelectedId { get; set; } = -1;

    /// <summary>Interruptor de la correa y la zona de acción del seguido (ADR 0028, ADR 0029).</summary>
    public bool ShowZone { get; set; }

    /// <summary>Lado de una casilla en píxeles; el campo se pinta cuadrado, como en <see cref="PitchView"/>.</summary>
    public float CellSize => Mathf.Min(Size.X / Pitch.Columns, Size.Y / Pitch.Rows);

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button || Trace is null)
        {
            return;
        }

        EmitSignal(SignalName.PlayerPicked, PlayerAt(button.Position));
        AcceptEvent();
    }

    public override void _Draw()
    {
        float cell = CellSize;
        DrawField(cell);

        if (Trace is not { FrameCount: > 0 } trace)
        {
            return;
        }

        int frame = Mathf.Clamp(Frame, 0, trace.FrameCount - 1);
        if (ShowZone && SelectedId >= 0)
        {
            DrawZone(trace, frame, cell);
        }

        int carrier = trace.BallOwnerAt(frame);
        for (int i = 0; i < trace.Players.Count; i++)
        {
            DrawToken(trace, frame, i, i == carrier, cell);
        }

        DrawBall(trace, frame, carrier, cell);
        DrawPhase(trace, frame, cell);
    }

    /// <summary>
    /// Posición del jugador en píxeles, suavizada hacia el tick siguiente. La interpolación es <b>solo</b>
    /// de dibujo: la traza no se toca y el tick lógico sigue siendo el entero (RT-020).
    /// </summary>
    private Vector2 PositionOf(MatchTrace trace, int frame, int player, float cell)
    {
        var here = trace.PositionAt(frame, player);
        if (Alpha <= 0f || frame + 1 >= trace.FrameCount)
        {
            return ToPixels(here, cell);
        }

        var next = trace.PositionAt(frame + 1, player);
        return ToPixels(new Vec2(Mathf.Lerp(here.X, next.X, Alpha), Mathf.Lerp(here.Y, next.Y, Alpha)), cell);
    }

    private Vector2 BallOf(MatchTrace trace, int frame, float cell)
    {
        var here = trace.BallAt(frame);
        if (Alpha <= 0f || frame + 1 >= trace.FrameCount)
        {
            return ToPixels(here, cell);
        }

        var next = trace.BallAt(frame + 1);
        return ToPixels(new Vec2(Mathf.Lerp(here.X, next.X, Alpha), Mathf.Lerp(here.Y, next.Y, Alpha)), cell);
    }

    private static Vector2 ToPixels(Vec2 point, float cell) => new(point.X * cell, point.Y * cell);

    /// <summary>Jugador cuya ficha contiene el punto, o -1. El más cercano gana si dos se solapan.</summary>
    private int PlayerAt(Vector2 point)
    {
        if (Trace is not { FrameCount: > 0 } trace)
        {
            return -1;
        }

        float cell = CellSize;
        int frame = Mathf.Clamp(Frame, 0, trace.FrameCount - 1);
        float reach = cell * TokenRadius * 1.4f;
        int best = -1;
        float bestDistance = reach;

        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (!trace.OnPitchAt(frame, i))
            {
                continue;
            }

            float distance = ToPixels(trace.PositionAt(frame, i), cell).DistanceTo(point);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = trace.Players[i].Id;
            }
        }

        return best;
    }

    /// <summary>Hierba, mitades, círculo central, áreas, porterías y una cuadrícula muy tenue.</summary>
    private void DrawField(float cell)
    {
        float width = cell * Pitch.Columns;
        float height = cell * Pitch.Rows;

        DrawRect(new Rect2(0f, 0f, width, height), Style.Grass);
        DrawRect(new Rect2(0f, 0f, width / 2f, height), Style.GrassOwn);

        // La cuadrícula de colocación sigue debajo, muy tenue: es la que el jugador manipuló en Equipo y
        // la referencia con la que lee "este defensa se ha ido tres casillas de su sitio".
        var faint = new Color(Style.GrassLine, 0.35f);
        for (int column = 1; column < Pitch.Columns; column++)
        {
            DrawLine(new Vector2(column * cell, 0f), new Vector2(column * cell, height), faint, 1f);
        }

        for (int row = 1; row < Pitch.Rows; row++)
        {
            DrawLine(new Vector2(0f, row * cell), new Vector2(width, row * cell), faint, 1f);
        }

        DrawLine(new Vector2(width / 2f, 0f), new Vector2(width / 2f, height), Style.GrassLine, 2f);
        DrawArc(new Vector2(width / 2f, height / 2f), cell * 0.9f, 0f, Mathf.Tau, 40, Style.GrassLine, 2f);
        DrawRect(new Rect2(0f, cell, cell * Pitch.AreaColumns, cell * Pitch.AreaRows), Style.GrassLine, false, 2f);
        DrawRect(
            new Rect2(cell * (Pitch.Columns - Pitch.AreaColumns), cell, cell * Pitch.AreaColumns, cell * Pitch.AreaRows),
            Style.GrassLine,
            false,
            2f);
        DrawRect(new Rect2(0f, 0f, width, height), Style.GrassLine, false, 2f);

        // Las dos porterías, en el centro exacto que usa Pitch.GoalCenter, cada una del color del equipo
        // que la defiende: es lo que dice hacia dónde ataca cada uno sin escribirlo en ninguna parte.
        float mouth = cell * 0.9f;
        DrawLine(new Vector2(2f, (height / 2f) - mouth), new Vector2(2f, (height / 2f) + mouth), Style.TeamOwn, 5f);
        DrawLine(
            new Vector2(width - 2f, (height / 2f) - mouth),
            new Vector2(width - 2f, (height / 2f) + mouth),
            Style.TeamRival,
            5f);
    }

    /// <summary>
    /// Zona de acción del jugador seguido (ADR 0028): el rectángulo blando alrededor de su casilla-hogar
    /// <b>efectiva</b> —la que el bloque táctico mueve cada tick— y la correa, la línea de esa casilla a
    /// donde está de verdad. Es lo que permite comprobar de un vistazo que la correa hace lo que dice la
    /// ADR 0029: cuánto se sale, hacia dónde y cuándo vuelve.
    /// </summary>
    private void DrawZone(MatchTrace trace, int frame, float cell)
    {
        int index = IndexOf(trace, SelectedId);
        if (index < 0)
        {
            return;
        }

        var zone = trace.ZoneAt(frame, index);
        float forward = zone.Forward < 0f ? Pitch.Columns : zone.Forward;
        float back = zone.Back < 0f ? Pitch.Columns : zone.Back;
        float sides = zone.Sides < 0f ? Pitch.Rows : zone.Sides;

        float ax = zone.Home.X + (forward * zone.Direction);
        float bx = zone.Home.X - (back * zone.Direction);
        var min = new Vector2(Mathf.Max(Mathf.Min(ax, bx), 0f), Mathf.Max(zone.Home.Y - sides, 0f));
        var max = new Vector2(
            Mathf.Min(Mathf.Max(ax, bx), Pitch.Columns),
            Mathf.Min(zone.Home.Y + sides, Pitch.Rows));

        var rect = new Rect2(min * cell, (max - min) * cell);
        DrawRect(rect, Style.ZoneFill);
        DrawRect(rect, Style.ZoneEdge, false, 2f);

        var home = ToPixels(zone.Home, cell);
        var player = PositionOf(trace, frame, index, cell);
        Style.DrawDashed(this, home, player, Style.ZoneEdge, 1.5f, 4f);
        DrawLine(home - new Vector2(5f, 0f), home + new Vector2(5f, 0f), Style.ZoneEdge, 2f);
        DrawLine(home - new Vector2(0f, 5f), home + new Vector2(0f, 5f), Style.ZoneEdge, 2f);
    }

    /// <summary>Una ficha: anillo de estado, disco del equipo y dorsal dentro.</summary>
    private void DrawToken(MatchTrace trace, int frame, int index, bool carrier, float cell)
    {
        if (!trace.OnPitchAt(frame, index))
        {
            return;
        }

        var player = trace.Players[index];
        var center = PositionOf(trace, frame, index, cell);
        var state = trace.StateAt(frame, index);
        float radius = cell * TokenRadius;

        if (carrier)
        {
            DrawArc(center, radius + 6f, 0f, Mathf.Tau, 32, new Color(Style.Carrier, 0.85f), 3f);
        }

        DrawArc(center, radius + 2.5f, 0f, Mathf.Tau, 28, Style.Of(state), state == PlayerState.Positioning ? 2f : 4f);
        DrawCircle(center, radius, player.Team == 0 ? Style.TeamOwn : Style.TeamRival);

        // El visitante lleva además un borde oscuro: los dos equipos no se distinguen solo por el tono
        // (UI-002), y a 20 fichas en movimiento eso importa más que en una lista.
        if (player.Team != 0)
        {
            DrawArc(center, radius - 1f, 0f, Mathf.Tau, 28, Style.Background, 2f);
        }

        var font = GetThemeDefaultFont();
        string label = player.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var size = font.GetStringSize(label, HorizontalAlignment.Left, -1f, Style.TextSmall);
        Style.DrawText(this, font, center - (size / 2f) - new Vector2(0f, 2f), label, Style.TextSmall, Style.Background);

        if (Style.IsDown(state))
        {
            Style.DrawDownMark(this, center, radius * 0.8f, Style.Background);
        }

        if (player.Id == SelectedId)
        {
            DrawArc(center, radius + 9f, 0f, Mathf.Tau, 36, Style.Accent, 2.5f);
        }
    }

    /// <summary>
    /// El balón. Con dueño está exactamente encima de él (así lo mantiene el motor), así que se desplaza
    /// un poco hacia la portería que ataca para que se vea a los pies y no debajo del dorsal.
    /// </summary>
    private void DrawBall(MatchTrace trace, int frame, int carrier, float cell)
    {
        var center = BallOf(trace, frame, cell);
        if (carrier >= 0)
        {
            float direction = trace.Players[carrier].Team == 0 ? 1f : -1f;
            center += new Vector2(direction * cell * (TokenRadius + BallRadius + 0.04f), 0f);
        }

        float radius = cell * BallRadius;
        DrawCircle(center, radius + 1.5f, Style.Background);
        DrawCircle(center, radius, Style.Ball);

        // En vuelo se marca con un anillo: un pase y un balón parado en el suelo se ven distintos.
        if (trace.BallInFlightAt(frame))
        {
            DrawArc(center, radius + 4f, 0f, Mathf.Tau, 20, new Color(Style.Ball, 0.55f), 1.5f);
        }
    }

    /// <summary>Fase del partido en una esquina; en juego abierto no se escribe nada, que es el 90% del tiempo.</summary>
    private void DrawPhase(MatchTrace trace, int frame, float cell)
    {
        var phase = trace.PhaseAt(frame);
        if (phase == MatchPhase.OpenPlay)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        string text = UiText.Get("ui.phase." + phase);
        var size = font.GetStringSize(text, HorizontalAlignment.Left, -1f, Style.TextSmall);
        var at = new Vector2((cell * Pitch.Columns) - size.X - 12f, 6f);
        DrawRect(new Rect2(at - new Vector2(6f, 2f), size + new Vector2(12f, 8f)), new Color(Style.Background, 0.75f));
        Style.DrawText(this, font, at, text, Style.TextSmall, Style.Accent);
    }

    private static int IndexOf(MatchTrace trace, int playerId)
    {
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Id == playerId)
            {
                return i;
            }
        }

        return -1;
    }
}
