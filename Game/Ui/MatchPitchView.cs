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
///
/// <para>
/// Encima de las fichas van las dos capas que explican <b>por qué</b> cada uno está donde está: el
/// <b>marcaje</b> —una línea de marcador a marcado, punteada cuando solo es la asignación de la posesión
/// y continua cuando el jugador está de verdad yendo a por su par (ADR 0022)— y la <b>intención</b>, el
/// punto al que la acción elegida le manda ir. Las dos salen de la traza; aquí no se deduce ninguna.
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

    /// <summary>
    /// Interruptor de las líneas de marcaje de <b>todo</b> el campo. Apagado siguen viéndose las del
    /// jugador seguido y las de quien está marcando de verdad en ese tick: eso nunca estorba porque casi
    /// nunca hay más de uno.
    /// </summary>
    public bool ShowMarking { get; set; } = true;

    /// <summary>Lado de una casilla en píxeles; el campo se pinta cuadrado, como en <see cref="PitchView"/>.</summary>
    /// <summary>
    /// Lado de la casilla en píxeles. El campo se dibuja con un margen de un radio de ficha por lado: un
    /// jugador pegado a la banda está centrado en la línea, así que sin ese margen media ficha se sale del
    /// control y pisa la leyenda de arriba.
    /// </summary>
    public float CellSize => Mathf.Min(
        Size.X / (Pitch.Columns + (2f * TokenRadius)),
        Size.Y / (Pitch.Rows + (2f * TokenRadius)));

    /// <summary>
    /// Esquina del campo dentro del control. Centrar reparte el margen por igual a los cuatro lados y, con
    /// <see cref="CellSize"/> ya descontando los dos radios, deja exactamente el radio de una ficha por
    /// lado en el eje que manda.
    /// </summary>
    private Vector2 Origin
    {
        get
        {
            float cell = CellSize;
            return new Vector2(
                (Size.X - (cell * Pitch.Columns)) / 2f,
                (Size.Y - (cell * Pitch.Rows)) / 2f);
        }
    }

    /// <summary>
    /// Píxel del control donde cae un punto del campo. Lo usa quien tenga que apuntar a una ficha desde
    /// fuera —la secuencia de capturas, sin ir más lejos— para que nadie más rehaga esta cuenta y se
    /// desincronice del margen.
    /// </summary>
    public Vector2 PixelOf(Vec2 point) => Origin + ToPixels(point, CellSize);

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

        // Todo el dibujo va desplazado el margen: así las coordenadas de casilla siguen siendo las del
        // motor y ninguna función de pintado tiene que saber del margen.
        DrawSetTransform(Origin);
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

        DrawMarking(trace, frame, cell);
        DrawIntent(trace, frame, cell);

        int carrier = trace.BallOwnerAt(frame);
        for (int i = 0; i < trace.Players.Count; i++)
        {
            DrawToken(trace, frame, i, i == carrier, cell);
        }

        DrawMarkGrips(trace, frame, cell);
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

            float distance = PixelOf(trace.PositionAt(frame, i)).DistanceTo(point);
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

    /// <summary>
    /// El marcaje (ADR 0022): una línea del marcador al marcado, en el color del marcador y con un punto
    /// en el extremo marcado, que es lo que dice en qué sentido se lee. La asignación va <b>punteada</b>
    /// y el marcaje en curso <b>continuo</b>: la diferencia entre "este es su par de esta posesión" y
    /// "ahora mismo está yendo a por él" no puede depender solo del grosor (UI-002).
    /// <para>
    /// La distinción importa más de lo que parece: la asignación la tienen los doce jugadores de campo
    /// todo el partido, pero <c>MarkOpponent</c> apenas gana la tabla de utilidad, así que la línea
    /// continua es rara y la punteada es el emparejamiento latente.
    /// </para>
    /// </summary>
    private void DrawMarking(MatchTrace trace, int frame, float cell)
    {
        float radius = cell * TokenRadius;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            int target = trace.MarkTargetAt(frame, i);
            if (target < 0 || !trace.OnPitchAt(frame, i) || !trace.OnPitchAt(frame, target))
            {
                continue;
            }

            bool active = trace.ActionAt(frame, i) == PlayerAction.MarkOpponent;
            bool involved = SelectedId >= 0
                && (trace.Players[i].Id == SelectedId || trace.Players[target].Id == SelectedId);
            if (!ShowMarking && !active && !involved)
            {
                continue;
            }

            var from = PositionOf(trace, frame, i, cell);
            var to = PositionOf(trace, frame, target, cell);
            var away = to - from;
            float length = away.Length();
            if (length <= radius + 6f)
            {
                // Encima el uno del otro: la línea sería un punto y el par ya se ve en la propia imagen.
                continue;
            }

            away /= length;

            // El recorte por el radio de las dos fichas se reparte la línea cuando no cabe entero: marcar
            // de cerca es justo lo que hace un buen marcador, y ese es el caso en el que el trazo no puede
            // desaparecer.
            float trim = Mathf.Min(radius + 3f, length * 0.35f);
            var start = from + (away * trim);
            var end = to - (away * trim);
            var color = trace.Players[i].Team == 0 ? Style.TeamOwn : Style.TeamRival;
            color = new Color(color, active ? 0.9f : (involved ? 0.6f : 0.3f));

            if (active)
            {
                DrawLine(start, end, color, 2.5f);
            }
            else
            {
                // La asignación empareja a los doce jugadores de campo, y un delantero puede tener por par
                // a un defensa que está en la otra punta. Esas líneas larguísimas se desvanecen por el
                // centro: siguen diciendo quién va con quién por los dos extremos, pero no cortan el campo
                // en diagonal.
                DrawFadingDashes(start, end, color, involved ? 1.6f : 1.2f, cell);
            }

            DrawCircle(end, active ? 3.5f : 2.5f, color);
        }
    }

    /// <summary>
    /// El <b>corchete</b> del marcaje en curso: un arco en el color del marcador pegado a la ficha del
    /// marcado, por el lado desde el que le llega. Va encima de las fichas y existe porque la línea sola
    /// no vale: cuando alguien marca bien está encima de su par, y entonces entre las dos fichas no cabe
    /// ni un píxel de línea. El arco se ve igual a un palmo que a media cancha.
    /// </summary>
    private void DrawMarkGrips(MatchTrace trace, int frame, float cell)
    {
        float radius = cell * TokenRadius;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            int target = trace.MarkTargetAt(frame, i);
            if (target < 0
                || !trace.OnPitchAt(frame, i)
                || !trace.OnPitchAt(frame, target)
                || trace.ActionAt(frame, i) != PlayerAction.MarkOpponent)
            {
                continue;
            }

            var from = PositionOf(trace, frame, i, cell);
            var to = PositionOf(trace, frame, target, cell);
            var away = from - to;
            if (away.LengthSquared() < 1f)
            {
                continue;
            }

            float angle = away.Angle();
            var color = (trace.Players[i].Team == 0 ? Style.TeamOwn : Style.TeamRival).Lightened(0.3f);
            DrawArc(to, radius + 7f, angle - 0.75f, angle + 0.75f, 16, color, 3.5f);
        }
    }

    /// <summary>
    /// Trazo punteado que se apaga por el centro cuanto más largo es. Es lo que permite enseñar los doce
    /// emparejamientos a la vez sin convertir el campo en una tela de araña: los tramos pegados a las dos
    /// fichas se ven, el vuelo entre ellas casi no.
    /// </summary>
    private void DrawFadingDashes(Vector2 from, Vector2 to, Color color, float width, float cell)
    {
        float length = from.DistanceTo(to);
        if (length <= 0.01f)
        {
            return;
        }

        const float Dash = 4f;
        var step = (to - from) / length;
        float dip = Mathf.Clamp((length - (cell * 2.5f)) / (cell * 4f), 0f, 1f);

        for (float d = 0f; d < length; d += Dash * 2f)
        {
            float along = (d + (Dash * 0.5f)) / length;
            float middle = Mathf.Min(along, 1f - along) * 2f;
            var faded = new Color(color, color.A * Mathf.Lerp(1f, Mathf.Lerp(1f, 0.10f, middle), dip));
            DrawLine(from + (step * d), from + (step * Mathf.Min(d + Dash, length)), faded, width);
        }
    }

    /// <summary>
    /// A dónde intenta ir cada uno: el punto que le puso la acción elegida (<c>TargetPoint</c>). Para el
    /// jugador seguido se pinta la línea entera hasta el destino, con un aro donde acaba; para los demás solo
    /// un <b>bigote</b> corto en esa dirección, que es lo que hace que el campo en reposo —doce jugadores
    /// colocándose, todos con el mismo anillo tenue— diga hacia dónde va cada uno sin llenarlo de líneas.
    /// </summary>
    private void DrawIntent(MatchTrace trace, int frame, float cell)
    {
        float radius = cell * TokenRadius;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (!trace.OnPitchAt(frame, i) || Style.IsDown(trace.StateAt(frame, i)))
            {
                continue;
            }

            var from = PositionOf(trace, frame, i, cell);
            var to = ToPixels(trace.TargetAt(frame, i), cell);
            var away = to - from;
            float length = away.Length();
            if (length <= radius + 5f)
            {
                continue;
            }

            away /= length;
            var start = from + (away * (radius + 2f));
            bool selected = trace.Players[i].Id == SelectedId;
            if (!selected)
            {
                DrawLine(start, from + (away * Mathf.Min(length, radius + (cell * 0.30f))), Style.Intent, 2f);
                continue;
            }

            Style.DrawDashed(this, start, to, new Color(Style.Accent, 0.85f), 1.5f, 4f);
            DrawArc(to, 4.5f, 0f, Mathf.Tau, 16, Style.Accent, 1.5f);
        }
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
