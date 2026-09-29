using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui.Knavall;

/// <summary>Lo que el tablero de inicio necesita saber de un club: nada de reglas, solo qué enseñar.</summary>
public sealed record ClubCard(string Id, Race Race, string Name, string RaceName, string Description, int StartingGold, Tip? Ability);

/// <summary>
/// Tablero del menú inicial en el lenguaje de Knavall (ADR 0162): travesaño con banderines, el nombre del
/// juego en un cartel de papel, los clanes como carteles grandes con el retrato de su raza y la ficha del
/// elegido con su habilidad racial. Solo pinta y avisa del clan pulsado; empezar, la semilla y continuar son
/// placas que monta la pantalla encima.
/// </summary>
public partial class ClubBoard : InkCanvas
{
    /// <summary>Franja de las tarjetas de clan.</summary>
    public const float CardsTop = 212f;
    public const float CardHeight = 244f;

    /// <summary>Paneles de abajo: ficha del clan a la izquierda, acciones a la derecha.</summary>
    public static readonly Rect2 DetailArea = new(20f, 478f, 744f, 262f);
    public static readonly Rect2 ActionArea = new(780f, 478f, 484f, 262f);

    private readonly List<ClubCard> _clubs = new();
    private string _chosen = string.Empty;
    private string _title = string.Empty;
    private string _tagline = string.Empty;

    /// <summary>Se ha pulsado un clan (su id).</summary>
    public event Action<string>? Picked;

    public void Bind(IReadOnlyList<ClubCard> clubs, string chosen, string title, string tagline)
    {
        _clubs.Clear();
        _clubs.AddRange(clubs);
        _chosen = chosen;
        _title = title.ToUpperInvariant();
        _tagline = tagline.ToUpperInvariant();
        QueueRedraw();
    }

    public override void _Draw()
    {
        ClearZones();
        DrawHeader();

        string choose = UiText.Get("ui.start.kn.choose").ToUpperInvariant();
        Ink.Brush(this, new Rect2(24f, 166f, Ink.Width(Ink.Display, choose, 26) + 44f, 36f), Ink.Red, 91);
        Ink.Outlined(this, Ink.Display, new Vector2(44f, 164f), choose, 26, Ink.Paper, Ink.Black, 5);

        DrawCards();

        Ink.Sheet(this, DetailArea, 4711);
        Ink.Sheet(this, ActionArea, 4712, Ink.PaperWarm);
        foreach (var club in _clubs)
        {
            if (club.Id == _chosen)
            {
                DrawDetail(club);
            }
        }
    }

    private void DrawHeader()
    {
        Ink.Plank(this, new Rect2(-8f, 4f, Size.X + 16f, 24f), 17, Ink.Wood);

        // Banderines de feria a lo largo del travesaño, a la derecha del cartel.
        var colors = new[] { Ink.Red, Ink.Ochre, Ink.Green, Ink.Black, Ink.Paper };
        for (int i = 0; i < 20; i++)
        {
            float x = 640f + (i * 31f);
            if (x > Size.X - 20f)
            {
                break;
            }

            float drop = 42f + Pregon.Jitter(i + 11, 8f);
            Ink.Poly(this, new[] { new Vector2(x, 24f), new Vector2(x + 27f, 24f), new Vector2(x + 13.5f, 24f + drop) }, colors[i % colors.Length], 2.4f);
        }

        // El nombre del juego en un cartel de papel clavado y torcido, con el escudo encima.
        Pregon.Tilted(this, new Vector2(360f, 86f), Mathf.DegToRad(-2f), () =>
        {
            var sign = new Rect2(-250f, -62f, 500f, 124f);
            Ink.Slab(this, sign, Ink.Paper, 2626, 3f, 4f, new Vector2(7f, 8f));
            int size = Ink.FitSize(Ink.Display, _title, 78, 380f, 40);
            float width = Ink.Width(Ink.Display, _title, size);
            Ink.Text(this, Ink.Display, new Vector2(-width / 2f + 40f, -72f), _title, size, Ink.Black);
            float tagWidth = Ink.Width(Ink.Heavy, _tagline, 24) + 36f;
            var tag = new Rect2((-tagWidth / 2f) + 40f, 20f, tagWidth, 34f);
            Ink.Brush(this, tag, Ink.Red, 27);
            Ink.Outlined(this, Ink.Heavy, tag.Position + new Vector2(18f, 3f), _tagline, 24, Ink.Paper, Ink.Black, 5);
            Ink.Nail(this, new Vector2(-236f, -48f));
            Ink.Nail(this, new Vector2(236f, -48f));
        });

        var shieldAt = new Vector2(76f, 34f);
        var poly = Ink.Shift(Pregon.ShieldPoly(92f, 108f), shieldAt);
        DrawColoredPolygon(Ink.Shift(poly, new Vector2(5f, 6f)), Ink.Shadow);
        Pregon.DrawShield(this, shieldAt, 92f, 108f, ours: true);
        DrawPolyline(Ink.Closed(poly), Ink.Black, 4.5f, true);
        Pregon.DrawOrla(this, Pregon.ShieldPoly(92f, 108f), shieldAt, 7f, Ink.Paper, 1.8f);
        InkIcons.Draw(this, Glyph.Ball, shieldAt + new Vector2(30f, 44f), 36f);
    }

    private void DrawCards()
    {
        if (_clubs.Count == 0)
        {
            return;
        }

        const float Gap = 16f;
        float width = (Size.X - 48f - (Gap * (_clubs.Count - 1))) / _clubs.Count;
        for (int i = 0; i < _clubs.Count; i++)
        {
            var club = _clubs[i];
            bool chosen = club.Id == _chosen;
            string key = "club:" + club.Id;
            bool hover = HoverKey == key;
            var lift = chosen ? new Vector2(0f, -8f) : hover ? new Vector2(0f, -3f) : Vector2.Zero;
            var card = new Rect2(new Vector2(24f + (i * (width + Gap)), CardsTop) + lift, new Vector2(width, CardHeight));

            Ink.Slab(this, card, chosen ? Ink.Ochre : hover ? Ink.Paper : Ink.PaperWarm, 300 + i, 1.8f, chosen ? 4f : 3f, chosen ? new Vector2(7f, 9f) : new Vector2(4f, 5f));

            float side = Mathf.Min(width - 36f, 150f);
            var photo = new Rect2(card.Position.X + ((width - side) / 2f), card.Position.Y + 14f, side, side);
            Portrait.Draw(this, photo, club.Race, Underleague.Sim.Model.Position.Forward, Face(club.Id));

            float y = photo.End.Y + 10f;
            string name = club.Name.ToUpperInvariant();
            int nameSize = Ink.FitSize(Ink.Display, name, 24, width - 20f, 15);
            float nameWidth = Ink.Width(Ink.Display, name, nameSize);
            Ink.Text(this, Ink.Display, new Vector2(card.Position.X + ((width - nameWidth) / 2f), y), name, nameSize, Ink.Black);
            y += Ink.Display.GetHeight(nameSize) - 6f;
            string race = club.RaceName.ToUpperInvariant();
            float raceWidth = Ink.Width(Ink.Heavy, race, 16);
            Ink.Text(this, Ink.Heavy, new Vector2(card.Position.X + ((width - raceWidth) / 2f), y), race, 16, chosen ? Ink.Brown : Ink.Muted);

            string captured = club.Id;
            Zone(card, club.Ability, () => Picked?.Invoke(captured), key);
        }
    }

    private void DrawDetail(ClubCard club)
    {
        var area = DetailArea;
        float left = area.Position.X + 24f;
        float right = area.End.X - 32f;
        float y = area.Position.Y + 18f;

        string name = club.Name.ToUpperInvariant();
        int size = Ink.FitSize(Ink.Display, name, 34, right - left - 150f, 22);
        Ink.Text(this, Ink.Display, new Vector2(left, y - 6f), name, size, Ink.Black);
        Ink.Stamp(this, new Vector2(right - 70f, y + 16f), club.RaceName.ToUpperInvariant(), Ink.Red, 6f, 18, new Color(Ink.Paper, 0.9f));
        y += Ink.Display.GetHeight(size) + 4f;

        foreach (string line in Style.Wrap(Ink.Plain, club.Description, 19, right - left))
        {
            Ink.Text(this, Ink.Plain, new Vector2(left, y), line, 19, Ink.Brown);
            y += 23f;
        }

        y += 12f;
        if (club.Ability is { } ability)
        {
            string label = UiText.Get("ui.start.ability").ToUpperInvariant();
            Ink.Text(this, Ink.Display, new Vector2(left, y + 6f), label, 18, Ink.RedDark);
            float x = left + Ink.Width(Ink.Display, label, 18) + 16f;
            string caption = ability.Title.ToUpperInvariant();
            var badge = new Rect2(x, y, Ink.Width(Ink.Heavy, caption, 18) + 54f, 38f);
            Ink.Brush(this, badge, Ink.Green, 77);
            InkIcons.Draw(this, Glyph.Racial, badge.Position + new Vector2(21f, 19f), 26f);
            Ink.Outlined(this, Ink.Heavy, badge.Position + new Vector2(40f, 7f), caption, 18, Ink.Paper, Ink.Black, 4);
            Zone(badge, ability);
            y += 50f;
        }

        string gold = UiText.Get("ui.start.kn.gold", club.StartingGold).ToUpperInvariant();
        Ink.Disc(this, new Vector2(left + 12f, y + 12f), 11f, Ink.Ochre, 2.4f);
        DrawArc(new Vector2(left + 12f, y + 12f), 6.5f, 0f, Mathf.Tau, 16, Ink.OchreDark, 1.6f, true);
        Ink.Text(this, Ink.Heavy, new Vector2(left + 32f, y + 1f), gold, 18, Ink.Brown);
    }

    /// <summary>Cara estable por club: la misma cara siempre, sin depender del hash de .NET (que cambia por proceso).</summary>
    private static int Face(string id)
    {
        int value = 17;
        foreach (char c in id)
        {
            value = (value * 31) + c;
        }

        return Math.Abs(value % 10_000);
    }
}
