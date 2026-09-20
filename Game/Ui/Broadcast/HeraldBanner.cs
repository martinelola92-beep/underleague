using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Estandarte de pregón (N3 de <c>docs/ui/README.md</c> §4): un <b>pergamino colgado</b> —boceto del
/// revisor, 20 sep 2026—, no un gonfalón de tela: dos varales de madera arriba y abajo (rollos con los
/// cabos sobresaliendo), cuerpo de pergamino claro con cintas y filete en el color del equipo protagonista
/// (azur y oro el propio, gules y sable el rival), escudo con trompetas cruzadas en la cabecera y una
/// corona pequeña al pie. «Se hace saber», título («GOL», dominando la composición), cuerpo en cursiva y
/// pie. Cubre gol, roja y lesión grave — los tres comparten formato, solo cambian el color de las cintas y
/// los cuatro textos, que llegan ya resueltos por quien lo muestra (RT-035: nada de texto de efecto
/// escrito a mano aquí). Todo dibujado por código (regla 10 de <c>CLAUDE.md</c>): nada de arte importado.
/// La posición (lado contrario al suceso) la decide <c>BroadcastScreen.PositionBanner</c>, sin cambios.
/// </summary>
public partial class HeraldBanner : Control
{
    public const float DesignWidth = 500f;
    public const float DesignHeight = 640f;

    private bool _ours;
    private string _header = string.Empty;
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _footer = string.Empty;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(DesignWidth, DesignHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    public void Show(bool ours, string header, string title, string body, string footer)
    {
        _ours = ours;
        _header = header;
        _title = title;
        _body = body;
        _footer = footer;
        Visible = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var primary = _ours ? Pregon.Azur : Pregon.Gules;
        var secondary = _ours ? Pregon.Or : Pregon.Sable;
        var wood = new Color("6b4a2a");
        var woodDark = new Color("46301a");

        const float RollHeight = 32f;
        const float BodyMarginX = 44f;
        float bodyW = DesignWidth - (2f * BodyMarginX);
        float bodyY = RollHeight - 6f;
        float bodyH = DesignHeight - (2f * RollHeight) + 12f;
        var bodyTopLeft = new Vector2(BodyMarginX, bodyY);

        // Sombra del conjunto, antes que nada (debajo de todo lo demás).
        DrawRect(new Rect2(bodyTopLeft + new Vector2(9f, 10f), new Vector2(bodyW, bodyH)), new Color(0f, 0f, 0f, 0.28f));
        DrawScrollRoll(new Vector2((DesignWidth / 2f) + 6f, RollHeight / 2f + 7f), new Color(0f, 0f, 0f, 0.28f), new Color(0f, 0f, 0f, 0.28f));
        DrawScrollRoll(new Vector2((DesignWidth / 2f) + 6f, DesignHeight - (RollHeight / 2f) + 7f), new Color(0f, 0f, 0f, 0.28f), new Color(0f, 0f, 0f, 0.28f));

        // Cuerpo de pergamino: borde irregular determinista (papel, no tela) — el filete del contorno ya
        // lleva el color secundario del equipo (Or/Sable), la identidad de verdad va en las cintas.
        Pregon.DrawParchment(this, bodyTopLeft, bodyW, bodyH, Pregon.Vellum, secondary, seed: 30, amplitude: 1.4f, edgeWidth: 2f, shadowOffset: Vector2.Zero);

        // Cintas del equipo protagonista, a los dos lados del pergamino, con un filete más fino por dentro.
        const float RibbonWidth = 15f;
        DrawRect(new Rect2(bodyTopLeft, new Vector2(RibbonWidth, bodyH)), primary);
        DrawRect(new Rect2(bodyTopLeft + new Vector2(bodyW - RibbonWidth, 0f), new Vector2(RibbonWidth, bodyH)), primary);
        DrawLine(bodyTopLeft + new Vector2(RibbonWidth, 0f), bodyTopLeft + new Vector2(RibbonWidth, bodyH), secondary, 1.5f);
        DrawLine(bodyTopLeft + new Vector2(bodyW - RibbonWidth, 0f), bodyTopLeft + new Vector2(bodyW - RibbonWidth, bodyH), secondary, 1.5f);

        // Rollos de verdad, encima de la sombra y del cuerpo: los dos varales de los que cuelga el pergamino.
        DrawScrollRoll(new Vector2(DesignWidth / 2f, RollHeight / 2f), wood, woodDark);
        DrawScrollRoll(new Vector2(DesignWidth / 2f, DesignHeight - (RollHeight / 2f)), wood, woodDark);

        // Escudo en la cabecera, solo el escudo (revisión del revisor, 20 sep 2026: las trompetas cruzadas
        // no salían legibles a este tamaño — dos trazos finos superpuestos leían como un zigzag roto — así
        // que se deja solo el escudo, más grande, que sí se lee).
        var shieldCenter = new Vector2(DesignWidth / 2f, bodyY + 46f);
        Pregon.DrawShield(this, shieldCenter - new Vector2(32f, 36f), 64f, 80f, _ours);

        // Margen interior: el texto no se pega a las cintas.
        float tx = bodyTopLeft.X + RibbonWidth + 14f;
        float tw = bodyW - (2f * (RibbonWidth + 14f));

        // El bloque de texto ocupa la mayor parte del pergamino (revisión del revisor: «el texto nada en
        // el centro», hoy menos margen y letra más grande): de bodyY+100 a bodyY+bodyH-70, ~76% del alto
        // del cuerpo.
        Pregon.DrawFittedTitle(this, Pregon.Fell, new Vector2(tx, bodyY + 102f), _header, Pregon.SizeHeader, Pregon.InkBrown, tw);

        // «GOL» con el mismo peso visual que el boceto del revisor: ~55% del ancho del pergamino, no del
        // ancho de la columna de texto — se centra dentro de tw con su propio ancho más estrecho.
        int titlePreferred = _title.Length > 4 ? Pregon.SizeTitleSmall : 340;
        float titleMaxWidth = _title.Length > 4 ? tw : bodyW * 0.55f;
        float titleX = tx + ((tw - titleMaxWidth) / 2f);
        Pregon.DrawFittedTitle(this, Pregon.FellBig, new Vector2(titleX, bodyY + 156f), _title, titlePreferred, primary, titleMaxWidth);

        Pregon.DrawWrappedText(this, Pregon.FellItalic, new Vector2(tx, bodyY + 372f), _body, Pregon.SizeBody, Pregon.InkBrown, tw, centered: true);
        Pregon.DrawFittedTitle(this, Pregon.Fell, new Vector2(tx, bodyY + 452f), _footer, Pregon.SizeHeader, Pregon.InkBrown, tw);

        // Corona al pie, cerrando la proclama: puntas separadas, no una mancha (revisión del revisor).
        DrawCrown(new Vector2(DesignWidth / 2f, bodyY + bodyH - 44f), secondary);
    }

    /// <summary>Un varal de madera (píldora) con los dos cabos redondeados sobresaliendo — nunca una tela plana.</summary>
    private void DrawScrollRoll(Vector2 center, Color fill, Color edge)
    {
        const float RollWidth = DesignWidth - 6f;
        const float RollThickness = 24f;
        var poly = Pregon.StadiumPoly(RollWidth, RollThickness);
        var topLeft = center - new Vector2(RollWidth / 2f, RollThickness / 2f);
        var shifted = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            shifted[i] = poly[i] + topLeft;
        }

        DrawColoredPolygon(shifted, fill);
        var closed = new Vector2[poly.Length + 1];
        System.Array.Copy(shifted, closed, poly.Length);
        closed[poly.Length] = shifted[0];
        DrawPolyline(closed, edge, 2f, true);

        // Los cabos del varal, sobresaliendo un poco a cada lado del pergamino.
        DrawCircle(topLeft + new Vector2(2f, RollThickness / 2f), RollThickness * 0.42f, edge);
        DrawCircle(topLeft + new Vector2(RollWidth - 2f, RollThickness / 2f), RollThickness * 0.42f, edge);
    }

    /// <summary>Corona de tres puntas separadas, el pie de la proclama — nunca una mancha.</summary>
    private void DrawCrown(Vector2 center, Color color)
    {
        const float W = 56f, H = 34f;
        var poly = Pregon.CrownPoly(W, H);
        var topLeft = center - new Vector2(W / 2f, H * 0.82f);
        var shifted = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            shifted[i] = poly[i] + topLeft;
        }

        DrawColoredPolygon(shifted, color);
        var closed = new Vector2[poly.Length + 1];
        System.Array.Copy(shifted, closed, poly.Length);
        closed[poly.Length] = shifted[0];
        DrawPolyline(closed, Pregon.InkBrown, 1.6f, true);
    }
}
