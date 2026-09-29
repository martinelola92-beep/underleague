using Godot;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui.Knavall;

/// <summary>
/// Retrato de busto de un jugador, dibujado por código en tinta gruesa y color plano (ADR 0162): la cara
/// que el jugador reconoce de un vistazo en la plantilla y en grande en la ficha. Es el paso siguiente al
/// <see cref="Medallion"/> —conserva su fondo de color por raza para que la identidad no cambie de una
/// pantalla a otra— y, como él, es marcador de posición: procedural, determinista y sustituible por arte
/// cuando se cierre la fase 2.
/// <para>
/// Cada raza tiene su silueta firma (<c>docs/estilo-visual.md</c> §2bis): colmillos y mandíbula del orco,
/// barba y casco del enano, orejas largas y melena del elfo, la cuenca vacía del no-muerto, la nariz rota del
/// humano. Encima, variaciones por individuo —cejas, boca, cicatriz, color de pelo o de barba— que salen del
/// id del jugador, nunca del azar. El uniforme es el del equipo propio (azur y oro); el portero lleva el suyo.
/// </para>
/// <para>
/// Se dibuja en una caja de 100x100 unidades escalada al tamaño pedido, así que el mismo retrato vale a
/// 48 px (plantilla) y a 180 px (ficha). No se puede llamar dentro de otra transformación de dibujo
/// (<see cref="Pregon.Tilted"/>): usa la suya.
/// </para>
/// </summary>
public static class Portrait
{
    private const float Line = 3.2f;

    private static readonly Color Kit = Pregon.Azur;
    private static readonly Color KitTrim = Pregon.Or;
    private static readonly Color KeeperKit = new("e8c547");
    private static readonly Color KeeperTrim = new("1b1510");
    private static readonly Color Eye = new("fff6d8");
    private static readonly Color Mouth = new("5a1410");
    private static readonly Color Tooth = new("fbf3dc");
    private static readonly Color Scar = new("a8322a");

    private static readonly Color[] BeardColors = { new("c8622a"), new("7a4a25"), new("b8b2a6"), new("3a2a20") };
    private static readonly Color[] ElfHair = { new("f0d27a"), new("dfe3e6"), new("5a3a28"), new("c9a45a") };
    private static readonly Color[] HumanHair = { new("4a3020"), new("1f1814"), new("b5552a"), new("8a6a3c") };

    /// <summary>
    /// Dibuja el retrato en <paramref name="box"/> (se usa el lado menor, cuadrado, arriba a la izquierda).
    /// <paramref name="framed"/> pinta el marco de tinta y la sombra dura; sin marco queda solo el busto
    /// sobre su fondo, para quien lo enmarque a su manera.
    /// </summary>
    public static void Draw(CanvasItem t, Rect2 box, Race race, Position position, int playerId, bool framed = true, bool dimmed = false)
    {
        float side = Mathf.Min(box.Size.X, box.Size.Y);
        var square = new Rect2(box.Position, new Vector2(side, side));

        if (framed)
        {
            t.DrawRect(new Rect2(square.Position + new Vector2(3f, 4f), square.Size), Ink.Shadow);
        }

        float scale = side / 100f;
        t.DrawSetTransform(square.Position, 0f, new Vector2(scale, scale));
        Backdrop(t, race, playerId);
        Shoulders(t, position, race);
        Face(t, race, playerId);

        if (dimmed)
        {
            t.DrawRect(new Rect2(0f, 0f, 100f, 100f), new Color(0.12f, 0.1f, 0.08f, 0.55f));
        }

        if (framed)
        {
            t.DrawRect(new Rect2(0f, 0f, 100f, 100f), Ink.Black, false, 4.5f);
        }

        t.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    /// <summary>
    /// Ficha redonda del campo de colocación: la misma cara dentro de un disco del color de la raza, con el
    /// anillo del color del puesto y el distintivo del puesto en la esquina (color y forma, UI-002). Es lo
    /// que era el <see cref="Medallion"/> en el campo, ahora con la cara del jugador.
    /// </summary>
    public static void Token(CanvasItem t, Vector2 center, float radius, Race race, Position position, int playerId, Color ring)
    {
        t.DrawCircle(center + new Vector2(2f, 3f), radius, Ink.Shadow);
        t.DrawCircle(center, radius, ring);
        t.DrawCircle(center, radius * 0.84f, Medallion.BackgroundOf(race));
        t.DrawArc(center, radius, 0f, Mathf.Tau, 32, Ink.Black, 2f, true);

        float scale = radius * 2.05f / 100f;
        t.DrawSetTransform(center - (new Vector2(50f, 47f) * scale), 0f, new Vector2(scale, scale));
        Face(t, race, playerId);
        t.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        var badge = center + new Vector2(radius * 0.72f, radius * 0.72f);
        float badgeRadius = radius * 0.4f;
        Ink.Disc(t, badge, badgeRadius, ring, 1.6f);
        InkIcons.Draw(t, InkIcons.Of(position), badge, badgeRadius * 1.5f);
    }

    /// <summary>La cabeza de la raza con las variaciones del individuo, en la caja de 100x100.</summary>
    private static void Face(CanvasItem t, Race race, int playerId)
    {
        var v = new Variant(playerId);
        switch (race)
        {
            case Race.Orc:
                Orc(t, v, new Color("8fb052"), hair: true);
                break;
            case Race.Demon:
                Orc(t, v, new Color("c95a42"), hair: false, horns: true);
                break;
            case Race.Lizard:
                Orc(t, v, new Color("78b865"), hair: false);
                break;
            case Race.Dwarf:
                Dwarf(t, v);
                break;
            case Race.Elf:
                Elf(t, v, new Color("f1dcb8"), ElfHair[v.Pick(0, ElfHair.Length)]);
                break;
            case Race.DarkElf:
                Elf(t, v, new Color("9c86bf"), new Color("eef0f2"));
                break;
            case Race.Undead:
                Undead(t, v);
                break;
            case Race.Vampire:
                Human(t, v, new Color("ece6ee"), new Color("1f1814"), fangs: true);
                break;
            default:
                Human(t, v, new Color("e9b98f"), HumanHair[v.Pick(1, HumanHair.Length)], fangs: false);
                break;
        }
    }

    // ------------------------------------------------------------------ fondo y cuerpo

    private static void Backdrop(CanvasItem t, Race race, int playerId)
    {
        var tone = Medallion.BackgroundOf(race);
        t.DrawRect(new Rect2(0f, 0f, 100f, 100f), tone);

        // Rayos de viñeta desde detrás de la cabeza: energía de cómic sin textura. Cada rayo se recorta a la
        // caja, porque dibujar fuera de ella mancharía lo que haya alrededor del retrato.
        var center = new Vector2(50f, 44f);
        var ray = tone.Darkened(0.14f);
        var box = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(100f, 100f), new Vector2(0f, 100f) };
        for (int i = 0; i < 12; i += 2)
        {
            float a0 = (i * Mathf.Tau / 12f) + Pregon.Jitter(playerId + i, 0.05f);
            float a1 = a0 + (Mathf.Tau / 12f);
            var wedge = new[]
            {
                center,
                center + (new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 90f),
                center + (new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 90f),
            };
            foreach (var piece in Geometry2D.IntersectPolygons(wedge, box))
            {
                if (piece.Length >= 3)
                {
                    t.DrawColoredPolygon(piece, ray);
                }
            }
        }
    }

    private static void Shoulders(CanvasItem t, Position position, Race race)
    {
        bool keeper = position == Position.Goalkeeper;
        var kit = keeper ? KeeperKit : Kit;
        var trim = keeper ? KeeperTrim : KitTrim;
        float wide = race is Race.Orc or Race.Demon or Race.Lizard ? 4f : race is Race.Elf or Race.Undead or Race.DarkElf ? -4f : 0f;
        Ink.Poly(t, new[]
        {
            new Vector2(4f - wide, 100f), new Vector2(8f - wide, 86f), new Vector2(26f, 77f), new Vector2(40f, 74f),
            new Vector2(60f, 74f), new Vector2(74f, 77f), new Vector2(92f + wide, 86f), new Vector2(96f + wide, 100f),
        }, kit, Line);

        // Cuello de la camiseta en pico, con ribete.
        Ink.Poly(t, new[] { new Vector2(38f, 74f), new Vector2(62f, 74f), new Vector2(50f, 90f) }, trim, Line);
        Ink.Poly(t, new[] { new Vector2(42f, 74f), new Vector2(58f, 74f), new Vector2(50f, 85f) }, kit.Darkened(0.35f), 0f);

        // Una raya de hombro: es un futbolista antes que un monstruo.
        t.DrawLine(new Vector2(14f - wide, 88f), new Vector2(28f, 81f), trim, 3f, true);
        t.DrawLine(new Vector2(86f + wide, 88f), new Vector2(72f, 81f), trim, 3f, true);
    }

    private static void Neck(CanvasItem t, Color skin, float width)
    {
        Ink.Poly(t, new[]
        {
            new Vector2(50f - width, 64f), new Vector2(50f + width, 64f), new Vector2(50f + width + 2f, 78f), new Vector2(50f - width - 2f, 78f),
        }, skin.Darkened(0.12f), Line);
    }

    // ------------------------------------------------------------------ razas

    private static void Orc(CanvasItem t, Variant v, Color baseSkin, bool hair, bool horns = false)
    {
        var skin = v.Skin(baseSkin);
        Neck(t, skin, 15f);

        // Orejas en punta hacia fuera.
        Ink.Poly(t, new[] { new Vector2(24f, 38f), new Vector2(3f, 30f), new Vector2(22f, 54f) }, skin, Line);
        Ink.Poly(t, new[] { new Vector2(76f, 38f), new Vector2(97f, 30f), new Vector2(78f, 54f) }, skin, Line);

        if (horns)
        {
            Ink.Poly(t, new[] { new Vector2(30f, 22f), new Vector2(18f, 2f), new Vector2(38f, 18f) }, Ink.Paper, Line);
            Ink.Poly(t, new[] { new Vector2(70f, 22f), new Vector2(82f, 2f), new Vector2(62f, 18f) }, Ink.Paper, Line);
        }

        // Cabeza: frente estrecha, mandíbula de armario.
        var head = new[]
        {
            new Vector2(34f, 14f), new Vector2(66f, 14f), new Vector2(76f, 26f), new Vector2(80f, 48f), new Vector2(82f, 64f),
            new Vector2(72f, 76f), new Vector2(50f, 80f), new Vector2(28f, 76f), new Vector2(18f, 64f), new Vector2(20f, 48f), new Vector2(24f, 26f),
        };
        Ink.Poly(t, head, skin, Line);

        if (hair)
        {
            // Cresta de pelo negro.
            Ink.Poly(t, new[] { new Vector2(40f, 16f), new Vector2(44f, 2f), new Vector2(50f, 10f), new Vector2(56f, 1f), new Vector2(60f, 16f) }, Ink.Black, 0f);
        }

        // Sombra de mandíbula, plana.
        t.DrawColoredPolygon(new[] { new Vector2(24f, 64f), new Vector2(76f, 64f), new Vector2(70f, 74f), new Vector2(50f, 78f), new Vector2(30f, 74f) }, skin.Darkened(0.18f));

        Brows(t, v, 30f, 36f, 11f, 6.5f);
        Eyes(t, v, 39f, 61f, 44f, 4.2f, Eye, angry: true);

        // Nariz ancha y chata.
        Ink.Poly(t, new[] { new Vector2(45f, 46f), new Vector2(55f, 46f), new Vector2(60f, 56f), new Vector2(40f, 56f) }, skin.Darkened(0.2f), Line * 0.8f);
        t.DrawCircle(new Vector2(46f, 54f), 1.6f, Ink.Black);
        t.DrawCircle(new Vector2(54f, 54f), 1.6f, Ink.Black);

        // Boca y colmillos inferiores hacia arriba.
        MouthShape(t, v, 32f, 68f, 63f, 70f);
        Ink.Poly(t, new[] { new Vector2(33f, 67f), new Vector2(35f, 52f), new Vector2(41f, 66f) }, Tooth, Line * 0.8f);
        Ink.Poly(t, new[] { new Vector2(67f, 67f), new Vector2(65f, 52f), new Vector2(59f, 66f) }, Tooth, Line * 0.8f);

        ScarMark(t, v, 58f, 34f);

        if (v.Flag(11))
        {
            // Aro de oro en la oreja.
            t.DrawArc(new Vector2(10f, 40f), 4f, 0f, Mathf.Tau, 12, Ink.Black, 3.2f, true);
            t.DrawArc(new Vector2(10f, 40f), 4f, 0f, Mathf.Tau, 12, Ink.Ochre, 1.6f, true);
        }

        if (v.Flag(12))
        {
            // Parche en un ojo: lo perdió en una entrada, o eso cuenta.
            t.DrawLine(new Vector2(22f, 34f), new Vector2(78f, 30f), Ink.Black, 2.4f, true);
            Ink.Poly(t, new[] { new Vector2(33f, 39f), new Vector2(46f, 39f), new Vector2(45f, 50f), new Vector2(34f, 50f) }, Ink.Black, 0f);
        }
    }

    private static void Dwarf(CanvasItem t, Variant v)
    {
        var skin = v.Skin(new Color("e5b082"));
        var beard = BeardColors[v.Pick(2, BeardColors.Length)];
        Neck(t, skin, 13f);

        Ink.Poly(t, new[]
        {
            new Vector2(30f, 22f), new Vector2(70f, 22f), new Vector2(76f, 40f), new Vector2(74f, 58f), new Vector2(50f, 66f),
            new Vector2(26f, 58f), new Vector2(24f, 40f),
        }, skin, Line);

        // Orejas pequeñas.
        Ink.Disc(t, new Vector2(24f, 46f), 5f, skin, Line * 0.8f);
        Ink.Disc(t, new Vector2(76f, 46f), 5f, skin, Line * 0.8f);

        // Barba enorme que baja hasta tapar el pecho: la firma del enano.
        Ink.Poly(t, new[]
        {
            new Vector2(22f, 46f), new Vector2(34f, 56f), new Vector2(50f, 58f), new Vector2(66f, 56f), new Vector2(78f, 46f),
            new Vector2(82f, 70f), new Vector2(72f, 92f), new Vector2(58f, 100f), new Vector2(42f, 100f), new Vector2(28f, 92f), new Vector2(18f, 70f),
        }, beard, Line);
        for (int i = 0; i < 4; i++)
        {
            float x = 36f + (i * 9f);
            t.DrawLine(new Vector2(x, 64f), new Vector2(x + Pregon.Jitter(i + v.Seed, 3f), 92f), beard.Darkened(0.35f), 2f, true);
        }

        // Bigote.
        Ink.Poly(t, new[] { new Vector2(34f, 58f), new Vector2(50f, 53f), new Vector2(66f, 58f), new Vector2(58f, 62f), new Vector2(50f, 59f), new Vector2(42f, 62f) }, beard.Darkened(0.15f), Line * 0.8f);

        Eyes(t, v, 40f, 60f, 42f, 3.6f, Eye, angry: false);

        // Cejas pobladas del color de la barba.
        Ink.Poly(t, new[] { new Vector2(31f, 36f), new Vector2(47f, 35f), new Vector2(46f, 40f), new Vector2(32f, 40f) }, beard, Line * 0.7f);
        Ink.Poly(t, new[] { new Vector2(53f, 35f), new Vector2(69f, 36f), new Vector2(68f, 40f), new Vector2(54f, 40f) }, beard, Line * 0.7f);

        // Narizota roja.
        Ink.Disc(t, new Vector2(50f, 50f), 6.5f, new Color("d97a5c"), Line * 0.8f);

        // Casco de hierro con nasal.
        Ink.Poly(t, new[]
        {
            new Vector2(22f, 34f), new Vector2(26f, 16f), new Vector2(38f, 6f), new Vector2(62f, 6f), new Vector2(74f, 16f), new Vector2(78f, 34f),
        }, new Color("9aa2a8"), Line);
        Ink.Poly(t, new[] { new Vector2(20f, 30f), new Vector2(80f, 30f), new Vector2(80f, 36f), new Vector2(20f, 36f) }, new Color("6c7378"), Line * 0.8f);
        Ink.Poly(t, new[] { new Vector2(47f, 34f), new Vector2(53f, 34f), new Vector2(52f, 46f), new Vector2(48f, 46f) }, new Color("6c7378"), Line * 0.7f);
        t.DrawCircle(new Vector2(30f, 33f), 1.5f, Ink.Black);
        t.DrawCircle(new Vector2(70f, 33f), 1.5f, Ink.Black);
        if (v.Flag(4))
        {
            // Cuernecillos: la mitad de los enanos los lleva.
            Ink.Poly(t, new[] { new Vector2(24f, 20f), new Vector2(10f, 6f), new Vector2(16f, 24f) }, Ink.Paper, Line * 0.8f);
            Ink.Poly(t, new[] { new Vector2(76f, 20f), new Vector2(90f, 6f), new Vector2(84f, 24f) }, Ink.Paper, Line * 0.8f);
        }
    }

    private static void Elf(CanvasItem t, Variant v, Color baseSkin, Color hair)
    {
        var skin = v.Skin(baseSkin);

        // Melena por detrás, cae hasta los hombros.
        Ink.Poly(t, new[]
        {
            new Vector2(26f, 18f), new Vector2(74f, 18f), new Vector2(80f, 50f), new Vector2(82f, 84f), new Vector2(66f, 80f),
            new Vector2(34f, 80f), new Vector2(18f, 84f), new Vector2(20f, 50f),
        }, hair.Darkened(0.12f), Line);

        Neck(t, skin, 9f);

        // Orejas largas hacia arriba y afuera.
        Ink.Poly(t, new[] { new Vector2(34f, 44f), new Vector2(4f, 16f), new Vector2(32f, 56f) }, skin, Line);
        Ink.Poly(t, new[] { new Vector2(66f, 44f), new Vector2(96f, 16f), new Vector2(68f, 56f) }, skin, Line);

        // Cara larga y estrecha.
        Ink.Poly(t, new[]
        {
            new Vector2(38f, 16f), new Vector2(62f, 16f), new Vector2(68f, 36f), new Vector2(66f, 58f), new Vector2(56f, 74f),
            new Vector2(44f, 74f), new Vector2(34f, 58f), new Vector2(32f, 36f),
        }, skin, Line);

        // Flequillo recto.
        Ink.Poly(t, new[] { new Vector2(30f, 32f), new Vector2(34f, 14f), new Vector2(66f, 14f), new Vector2(70f, 32f), new Vector2(58f, 24f), new Vector2(46f, 28f) }, hair, Line);

        // Ojos rasgados, cejas altivas.
        t.DrawLine(new Vector2(37f, 38f), new Vector2(47f, 36f), Ink.Black, 2.4f, true);
        t.DrawLine(new Vector2(53f, 36f), new Vector2(63f, 38f), Ink.Black, 2.4f, true);
        Ink.Poly(t, new[] { new Vector2(37f, 44f), new Vector2(47f, 42f), new Vector2(46f, 46f), new Vector2(38f, 46f) }, Eye, Line * 0.6f);
        Ink.Poly(t, new[] { new Vector2(53f, 42f), new Vector2(63f, 44f), new Vector2(62f, 46f), new Vector2(54f, 46f) }, Eye, Line * 0.6f);
        t.DrawCircle(new Vector2(43f, 44f), 1.8f, new Color("2f7a4a"));
        t.DrawCircle(new Vector2(57f, 44f), 1.8f, new Color("2f7a4a"));

        // Nariz fina y sonrisa de superioridad.
        t.DrawLine(new Vector2(50f, 46f), new Vector2(52f, 56f), Ink.Black, 2f, true);
        t.DrawLine(new Vector2(52f, 56f), new Vector2(48f, 57f), Ink.Black, 2f, true);
        t.DrawArc(new Vector2(52f, 60f), 7f, 0.3f, 2.2f, 10, Ink.Black, 2.4f, true);
        ScarMark(t, v, 58f, 40f);
    }

    private static void Human(CanvasItem t, Variant v, Color baseSkin, Color hair, bool fangs)
    {
        var skin = v.Skin(baseSkin);
        Neck(t, skin, 12f);

        Ink.Disc(t, new Vector2(26f, 46f), 6f, skin, Line * 0.8f);
        Ink.Disc(t, new Vector2(74f, 46f), 6f, skin, Line * 0.8f);

        Ink.Poly(t, new[]
        {
            new Vector2(32f, 16f), new Vector2(68f, 16f), new Vector2(75f, 34f), new Vector2(74f, 58f), new Vector2(62f, 74f),
            new Vector2(38f, 74f), new Vector2(26f, 58f), new Vector2(25f, 34f),
        }, skin, Line);

        // Barba de tres días.
        for (int i = 0; i < 16; i++)
        {
            var at = new Vector2(34f + ((Pregon.Jitter((v.Seed * 3) + i, 0.5f) + 0.5f) * 32f), 60f + ((Pregon.Jitter((v.Seed * 5) + i, 0.5f) + 0.5f) * 12f));
            t.DrawCircle(at, 0.9f, skin.Darkened(0.45f));
        }

        // Pelo corto en casquete.
        Ink.Poly(t, new[]
        {
            new Vector2(24f, 36f), new Vector2(26f, 18f), new Vector2(38f, 8f), new Vector2(62f, 8f), new Vector2(74f, 18f), new Vector2(76f, 36f),
            new Vector2(68f, 24f), new Vector2(50f, 22f), new Vector2(32f, 24f),
        }, hair, Line);

        Brows(t, v, 33f, 34f, 12f, 4.5f);
        Eyes(t, v, 41f, 59f, 42f, 3.8f, Eye, angry: v.Flag(3));
        if (v.Flag(6))
        {
            // Ojo morado del último partido.
            t.DrawArc(new Vector2(41f, 42f), 6.5f, 0f, Mathf.Tau, 16, new Color("6d4a9a", 0.8f), 3f, true);
        }

        // Nariz rota, torcida a un lado.
        Ink.Poly(t, new[] { new Vector2(49f, 42f), new Vector2(55f, 50f), new Vector2(52f, 56f), new Vector2(45f, 55f) }, skin.Darkened(0.12f), Line * 0.7f);

        MouthShape(t, v, 38f, 64f, 62f, 66f);
        if (fangs)
        {
            Ink.Poly(t, new[] { new Vector2(42f, 64f), new Vector2(45f, 64f), new Vector2(43.5f, 71f) }, Tooth, Line * 0.5f);
            Ink.Poly(t, new[] { new Vector2(55f, 64f), new Vector2(58f, 64f), new Vector2(56.5f, 71f) }, Tooth, Line * 0.5f);
        }

        ScarMark(t, v, 62f, 30f);

        if (v.Flag(13))
        {
            // Venda en la cabeza, de la semana pasada.
            Ink.Poly(t, new[] { new Vector2(24f, 22f), new Vector2(76f, 22f), new Vector2(76f, 30f), new Vector2(24f, 30f) }, Ink.Paper, Line * 0.7f);
            t.DrawCircle(new Vector2(66f, 26f), 2.2f, Scar);
        }
    }

    private static void Undead(CanvasItem t, Variant v)
    {
        var skin = v.Skin(new Color("a3b59c"));
        Neck(t, skin, 8f);

        // Una oreja sí y otra no.
        Ink.Disc(t, new Vector2(72f, 46f), 5f, skin, Line * 0.8f);

        // Cabeza chupada: pómulos marcados y mandíbula estrecha.
        Ink.Poly(t, new[]
        {
            new Vector2(32f, 14f), new Vector2(68f, 14f), new Vector2(74f, 30f), new Vector2(72f, 50f), new Vector2(64f, 62f),
            new Vector2(60f, 76f), new Vector2(40f, 76f), new Vector2(36f, 62f), new Vector2(28f, 50f), new Vector2(26f, 30f),
        }, skin, Line);
        t.DrawLine(new Vector2(32f, 52f), new Vector2(38f, 62f), skin.Darkened(0.35f), 2.4f, true);
        t.DrawLine(new Vector2(68f, 52f), new Vector2(62f, 62f), skin.Darkened(0.35f), 2.4f, true);

        // Pelo ralo: cuatro mechones.
        for (int i = 0; i < 4; i++)
        {
            float x = 36f + (i * 9f);
            t.DrawLine(new Vector2(x, 16f), new Vector2(x - 4f + Pregon.Jitter(v.Seed + i, 3f), 4f), Ink.Black, 2.4f, true);
        }

        // La firma: una cuenca vacía y un ojo saltón.
        Ink.Disc(t, new Vector2(40f, 40f), 8f, new Color("241c18"), Line);
        Ink.Disc(t, new Vector2(60f, 40f), 7f, Eye, Line);
        t.DrawCircle(new Vector2(61f, 41f), 2f, Ink.Black);
        t.DrawLine(new Vector2(54f, 30f), new Vector2(68f, 31f), Ink.Black, 3f, true);

        // Costura en la frente.
        t.DrawLine(new Vector2(36f, 24f), new Vector2(62f, 22f), Scar, 2f, true);
        for (int i = 0; i < 5; i++)
        {
            float x = 39f + (i * 5.5f);
            t.DrawLine(new Vector2(x, 20f), new Vector2(x + 1f, 26f), Ink.Black, 1.4f, true);
        }

        // Agujero de nariz y dientes sin labios.
        Ink.Poly(t, new[] { new Vector2(47f, 48f), new Vector2(53f, 48f), new Vector2(50f, 55f) }, new Color("241c18"), Line * 0.6f);
        Ink.Poly(t, new[] { new Vector2(38f, 60f), new Vector2(62f, 60f), new Vector2(60f, 68f), new Vector2(40f, 68f) }, Tooth, Line * 0.8f);
        for (int i = 1; i < 6; i++)
        {
            float x = 38f + (i * 4f);
            t.DrawLine(new Vector2(x, 60f), new Vector2(x, 68f), Ink.Black, 1.4f, true);
        }

        if (v.Flag(5))
        {
            // Un diente de menos.
            t.DrawRect(new Rect2(46f, 60.5f, 4f, 7f), new Color("241c18"));
        }
    }

    // ------------------------------------------------------------------ rasgos de cara compartidos

    private static void Brows(CanvasItem t, Variant v, float left, float y, float length, float thickness)
    {
        float tilt = 3f + (v.Pick(7, 3) * 2f);
        t.DrawLine(new Vector2(left, y), new Vector2(left + length, y + tilt), Ink.Black, thickness, true);
        float right = 100f - left;
        t.DrawLine(new Vector2(right, y), new Vector2(right - length, y + tilt), Ink.Black, thickness, true);
    }

    private static void Eyes(CanvasItem t, Variant v, float leftX, float rightX, float y, float radius, Color white, bool angry)
    {
        Ink.Disc(t, new Vector2(leftX, y), radius, white, Line * 0.7f);
        Ink.Disc(t, new Vector2(rightX, y), radius, white, Line * 0.7f);
        float look = (v.Pick(8, 3) - 1) * 1.2f;
        float pupil = radius * (angry ? 0.42f : 0.5f);
        t.DrawCircle(new Vector2(leftX + look, y + 0.5f), pupil, Ink.Black);
        t.DrawCircle(new Vector2(rightX + look, y + 0.5f), pupil, Ink.Black);
        if (angry)
        {
            // Párpado caído en diagonal: cara de pocos amigos.
            t.DrawColoredPolygon(new[]
            {
                new Vector2(leftX - radius - 1f, y - radius - 1f), new Vector2(leftX + radius + 1f, y - radius - 1f), new Vector2(leftX + radius + 1f, y - 0.5f),
            }, Ink.Black);
            t.DrawColoredPolygon(new[]
            {
                new Vector2(rightX + radius + 1f, y - radius - 1f), new Vector2(rightX - radius - 1f, y - radius - 1f), new Vector2(rightX - radius - 1f, y - 0.5f),
            }, Ink.Black);
        }
    }

    private static void MouthShape(CanvasItem t, Variant v, float left, float top, float right, float bottom)
    {
        switch (v.Pick(9, 3))
        {
            case 0:
                // Mueca de dientes apretados.
                Ink.Poly(t, new[] { new Vector2(left, top), new Vector2(right, top - 1f), new Vector2(right - 2f, bottom + 4f), new Vector2(left + 2f, bottom + 4f) }, Tooth, Line * 0.8f);
                t.DrawLine(new Vector2(left + 1f, top + 3f), new Vector2(right - 1f, top + 2.5f), Ink.Black, 1.4f, true);
                for (int i = 1; i < 5; i++)
                {
                    float x = left + ((right - left) * i / 5f);
                    t.DrawLine(new Vector2(x, top), new Vector2(x, bottom + 4f), Ink.Black, 1.2f, true);
                }

                break;
            case 1:
                // Grito abierto.
                Ink.Poly(t, new[] { new Vector2(left + 2f, top), new Vector2(right - 2f, top), new Vector2(right - 6f, bottom + 8f), new Vector2(left + 6f, bottom + 8f) }, Mouth, Line * 0.8f);
                t.DrawColoredPolygon(new[] { new Vector2(left + 4f, top + 1f), new Vector2(right - 4f, top + 1f), new Vector2(right - 6f, top + 4f), new Vector2(left + 6f, top + 4f) }, Tooth);
                break;
            default:
                // Sonrisa torcida con un diente de menos.
                t.DrawArc(new Vector2((left + right) / 2f, top - 4f), (right - left) / 2f, 0.4f, 2.7f, 14, Ink.Black, 3f, true);
                t.DrawRect(new Rect2(((left + right) / 2f) - 3f, top + 1f, 6f, 4f), Tooth);
                t.DrawRect(new Rect2(((left + right) / 2f) - 0.5f, top + 1f, 2f, 4f), Ink.Black);
                break;
        }
    }

    private static void ScarMark(CanvasItem t, Variant v, float x, float y)
    {
        if (!v.Flag(10))
        {
            return;
        }

        t.DrawLine(new Vector2(x, y), new Vector2(x + 8f, y + 14f), Scar, 2.4f, true);
        for (int i = 0; i < 3; i++)
        {
            float k = 0.2f + (i * 0.3f);
            var at = new Vector2(x + (8f * k), y + (14f * k));
            t.DrawLine(at + new Vector2(-2.5f, 1.2f), at + new Vector2(2.5f, -1.2f), Scar, 1.4f, true);
        }
    }

    /// <summary>Variación por individuo: todo sale del id, nunca del azar.</summary>
    private readonly struct Variant
    {
        public readonly int Seed;

        public Variant(int playerId) => Seed = (playerId * 7919) + 101;

        public int Pick(int salt, int n) => Ink.Pick(Seed + (salt * 131), n);

        public bool Flag(int salt) => Pick(salt, 2) == 1;

        public Color Skin(Color baseSkin)
        {
            float shift = Pregon.Jitter(Seed + 17, 0.06f);
            return shift >= 0f ? baseSkin.Lightened(shift) : baseSkin.Darkened(-shift);
        }
    }
}
