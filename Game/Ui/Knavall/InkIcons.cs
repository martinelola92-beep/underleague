using System;
using Godot;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Consumables;

namespace Underleague.Game.Ui.Knavall;

/// <summary>Los iconos del lenguaje de Knavall. Uno por concepto que el jugador tiene que reconocer sin leer.</summary>
public enum Glyph
{
    None,

    // atributos
    Strength,
    Speed,
    Technique,
    Stamina,
    Leash,

    // posiciones y papel
    Goalkeeper,
    Defender,
    Midfielder,
    Forward,
    Crown,
    Bench,

    // pestañas y acciones
    Shirt,
    Pitch,
    Potion,
    Chest,
    Swap,
    Back,
    Zones,
    Coverage,

    // objetos
    Boot,
    Glove,
    Pauldron,
    Helmet,
    Amulet,
    Banner,
    Blade,
    Ring,
    Whistle,
    Lantern,
    Totem,

    // consumibles
    Medical,
    Tactical,
    Dirty,
    Supernatural,
    Manual,
    Conditional,

    // rasgos
    Fist,
    Runner,
    Ball,
    Target,
    Drop,
    ShieldHeart,
    WhiteFlag,
    Horn,
    Sleep,
    Paw,
    Wall,

    // perks y estado
    Perk,
    Racial,
    EmptySlot,
    Healthy,
    MinorInjury,
    SevereInjury,
    Dead,
    Info,
}

/// <summary>
/// Iconos dibujados por código con contorno grueso (ADR 0162). Se piden por concepto (<see cref="Glyph"/>),
/// centrados en un punto y con un lado en píxeles; el contorno escala con el tamaño para que un icono de 20
/// px y uno de 80 px sean el mismo dibujo. Ninguno es una calavera ni un hueso (RA-026): la muerte es una
/// lápida, la lesión un vendaje o una muleta.
/// </summary>
public static class InkIcons
{
    private static readonly Color Skin = new("e9c49a");
    private static readonly Color Steel = new("aab0b4");
    private static readonly Color SteelDark = new("6c7378");
    private static readonly Color Leather = new("8a4b2a");
    private static readonly Color LeatherLight = new("b8733f");
    private static readonly Color Glass = new("cfe3e0");
    private static readonly Color Blue = new("3f6fb5");
    private static readonly Color Purple = new("6d4a9a");
    private static readonly Color Gold = new("e8ad2c");

    /// <summary>Icono de un atributo del jugador.</summary>
    public static Glyph Of(AttributeKind kind) => kind switch
    {
        AttributeKind.Strength => Glyph.Strength,
        AttributeKind.Speed => Glyph.Speed,
        AttributeKind.Technique => Glyph.Technique,
        AttributeKind.Stamina => Glyph.Stamina,
        _ => Glyph.Leash,
    };

    /// <summary>Icono de la posición.</summary>
    public static Glyph Of(Position position) => position switch
    {
        Position.Goalkeeper => Glyph.Goalkeeper,
        Position.Defender => Glyph.Defender,
        Position.Midfielder => Glyph.Midfielder,
        _ => Glyph.Forward,
    };

    /// <summary>Icono del estado físico (color y forma, UI-002).</summary>
    public static Glyph Of(PhysicalState state) => state switch
    {
        PhysicalState.Healthy => Glyph.Healthy,
        PhysicalState.MinorInjury => Glyph.MinorInjury,
        PhysicalState.SevereInjury => Glyph.SevereInjury,
        _ => Glyph.Dead,
    };

    /// <summary>Icono de la familia de un consumible.</summary>
    public static Glyph Of(ConsumableFamily family) => family switch
    {
        ConsumableFamily.Medical => Glyph.Medical,
        ConsumableFamily.Tactical => Glyph.Tactical,
        ConsumableFamily.Dirty => Glyph.Dirty,
        _ => Glyph.Supernatural,
    };

    /// <summary>
    /// Icono de un rasgo por su id de catálogo. Presentación pura: el dato no cambia, solo se elige dibujo.
    /// </summary>
    public static Glyph OfTrait(string traitId) => traitId switch
    {
        "Aggressive" => Glyph.Fist,
        "Fast" => Glyph.Runner,
        "Scorer" => Glyph.Ball,
        "LongShot" => Glyph.Target,
        "Cerebral" => Glyph.Technique,
        "Dirty" => Glyph.Drop,
        "Resilient" => Glyph.ShieldHeart,
        "Coward" => Glyph.WhiteFlag,
        "Leader" => Glyph.Horn,
        "Lazy" => Glyph.Sleep,
        "Cat" => Glyph.Paw,
        "Wall" => Glyph.Wall,
        "Rusher" => Glyph.Runner,
        _ => Glyph.Perk,
    };

    /// <summary>
    /// Icono de un objeto por su id. Los objetos de <c>data/items/</c> no declaran qué son físicamente
    /// (ADR 0036: un objeto es un paquete de atributos), así que la pantalla lo deduce del nombre interno.
    /// Si no reconoce ninguna palabra, cae a un amuleto: nunca a un hueco vacío.
    /// </summary>
    public static Glyph OfItem(string itemId)
    {
        string id = itemId.ToLowerInvariant();
        if (Has(id, "totem", "anvil"))
        {
            return Glyph.Totem;
        }

        if (Has(id, "boot", "sandal", "greave", "tendon", "spike"))
        {
            return Glyph.Boot;
        }

        if (Has(id, "gauntlet", "glove", "wrap", "bracer"))
        {
            return Glyph.Glove;
        }

        if (Has(id, "pauldron", "harness", "belt", "sash", "armband"))
        {
            return Glyph.Pauldron;
        }

        if (Has(id, "visor", "circlet", "lens", "helm"))
        {
            return Glyph.Helmet;
        }

        if (Has(id, "banner", "pennant"))
        {
            return Glyph.Banner;
        }

        if (Has(id, "blade"))
        {
            return Glyph.Blade;
        }

        if (Has(id, "ring", "medal"))
        {
            return Glyph.Ring;
        }

        if (Has(id, "whistle"))
        {
            return Glyph.Whistle;
        }

        if (Has(id, "lantern"))
        {
            return Glyph.Lantern;
        }

        if (Has(id, "relic_scorer"))
        {
            return Glyph.Boot;
        }

        if (Has(id, "relic_butcher"))
        {
            return Glyph.Blade;
        }

        if (Has(id, "relic_wall"))
        {
            return Glyph.Glove;
        }

        return Glyph.Amulet;
    }

    private static Color SilhouetteTint(Glyph glyph) => glyph switch
    {
        Glyph.Healthy => Ink.Green,
        Glyph.MinorInjury => Ink.Ochre,
        Glyph.SevereInjury => Ink.Red,
        Glyph.Crown => Ink.Ochre,
        Glyph.Stamina => Ink.Red,
        _ => Ink.Black,
    };

    private static void DrawSilhouette(CanvasItem t, Texture2D icon, Vector2 center, float size, Color tint)
    {
        var rect = new Rect2(center - new Vector2(size / 2f, size / 2f), new Vector2(size, size));
        bool dark = tint.Luminance < 0.2f;
        if (!dark)
        {
            float o = Mathf.Max(1.2f, size * 0.045f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.Tau / 8f;
                t.DrawTextureRect(icon, new Rect2(rect.Position + new Vector2(Mathf.Cos(a) * o, Mathf.Sin(a) * o), rect.Size), false, Ink.Black);
            }
        }

        t.DrawTextureRect(icon, rect, false, tint);
    }

    private static bool Has(string id, params string[] words)
    {
        foreach (string word in words)
        {
            if (id.Contains(word, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Dibuja el icono centrado en <paramref name="center"/> con lado <paramref name="size"/>.
    /// <paramref name="tint"/> sustituye el color principal cuando el icono tiene uno (p. ej. la posición
    /// sobre una placa de su color); los contornos son siempre de tinta.
    /// </summary>
    public static void Draw(CanvasItem t, Glyph glyph, Vector2 center, float size, Color? tint = null)
    {
        if (glyph == Glyph.None)
        {
            return;
        }

        // Pase de materiales: los glifos que el arte pinta como silueta de tinta salen de game-icons.net. Los de
        // color (estado, corona) llevan contorno negro para leerse sobre papel y sobre madera (color y forma, UI-002).
        if (Art.Icon(glyph.ToString()) is { } icon)
        {
            DrawSilhouette(t, icon, center, size, tint ?? SilhouetteTint(glyph));
            return;
        }

        var g = new Pen(t, center, size);
        switch (glyph)
        {
            case Glyph.Strength:
            case Glyph.Fist:
                Fist(g, tint ?? Skin, glyph == Glyph.Fist);
                break;
            case Glyph.Speed:
                WingedBoot(g, tint ?? Leather);
                break;
            case Glyph.Technique:
                Brain(g, tint ?? new Color("e59aa0"));
                break;
            case Glyph.Stamina:
                Heart(g, tint ?? Ink.Red, 0f, 0f, 1f);
                break;
            case Glyph.Leash:
                Chain(g, tint ?? Steel);
                break;
            case Glyph.Goalkeeper:
                GloveShape(g, tint ?? Gold);
                break;
            case Glyph.Defender:
                ShieldShape(g, tint ?? Blue);
                break;
            case Glyph.Midfielder:
            case Glyph.Runner:
                RunnerShape(g, tint ?? Ink.Black);
                break;
            case Glyph.Forward:
            case Glyph.Ball:
                BallShape(g, glyph == Glyph.Forward);
                break;
            case Glyph.Crown:
                CrownShape(g, tint ?? Gold);
                break;
            case Glyph.Bench:
                BenchShape(g, tint ?? Ink.WoodLight);
                break;
            case Glyph.Shirt:
                ShirtShape(g, tint ?? Ink.Paper);
                break;
            case Glyph.Pitch:
                PitchShape(g);
                break;
            case Glyph.Potion:
            case Glyph.Medical:
                Flask(g, tint ?? Ink.Red, glyph == Glyph.Medical);
                break;
            case Glyph.Supernatural:
                Flask(g, tint ?? Blue, false, stars: true);
                break;
            case Glyph.Chest:
                ChestShape(g);
                break;
            case Glyph.Swap:
                SwapShape(g, tint ?? Ink.Black);
                break;
            case Glyph.Back:
                ArrowShape(g, tint ?? Ink.Paper);
                break;
            case Glyph.Zones:
                ZonesShape(g);
                break;
            case Glyph.Coverage:
                EyeShape(g);
                break;
            case Glyph.Boot:
                BootShape(g, tint ?? Ink.Red);
                break;
            case Glyph.Glove:
                GloveShape(g, tint ?? LeatherLight);
                break;
            case Glyph.Pauldron:
                PauldronShape(g, tint ?? Steel);
                break;
            case Glyph.Helmet:
                HelmetShape(g, tint ?? Steel);
                break;
            case Glyph.Amulet:
                AmuletShape(g, tint ?? Ink.GreenLight);
                break;
            case Glyph.Banner:
                BannerShape(g, tint ?? Ink.Red);
                break;
            case Glyph.Blade:
                BladeShape(g);
                break;
            case Glyph.Ring:
                RingShape(g, tint ?? Gold);
                break;
            case Glyph.Whistle:
                WhistleShape(g, tint ?? Steel);
                break;
            case Glyph.Lantern:
                LanternShape(g);
                break;
            case Glyph.Totem:
                TotemShape(g);
                break;
            case Glyph.Tactical:
                ScrollShape(g);
                break;
            case Glyph.Dirty:
                KnuckleShape(g, tint ?? SteelDark);
                break;
            case Glyph.Manual:
                HandShape(g, tint ?? Skin);
                break;
            case Glyph.Conditional:
                HourglassShape(g);
                break;
            case Glyph.Target:
                TargetShape(g);
                break;
            case Glyph.Drop:
                DropShape(g, tint ?? new Color("7a5a2e"));
                break;
            case Glyph.ShieldHeart:
                ShieldShape(g, tint ?? Ink.GreenLight);
                Heart(g, Ink.Red, 0f, -0.05f, 0.45f);
                break;
            case Glyph.WhiteFlag:
                FlagShape(g, tint ?? Colors.White);
                break;
            case Glyph.Horn:
                HornShape(g, tint ?? Gold);
                break;
            case Glyph.Sleep:
                SleepShape(g);
                break;
            case Glyph.Paw:
                PawShape(g, tint ?? Ink.Black);
                break;
            case Glyph.Wall:
                WallShape(g, tint ?? new Color("b5654a"));
                break;
            case Glyph.Perk:
                StarShape(g, tint ?? Gold);
                break;
            case Glyph.Racial:
                FlameShape(g, tint ?? Ink.Ochre);
                break;
            case Glyph.EmptySlot:
                EmptyShape(g);
                break;
            case Glyph.Healthy:
                Heart(g, tint ?? Ink.GreenLight, 0f, 0f, 1f);
                break;
            case Glyph.MinorInjury:
                BandageShape(g);
                break;
            case Glyph.SevereInjury:
                CrutchShape(g);
                break;
            case Glyph.Dead:
                TombShape(g);
                break;
            case Glyph.Info:
                InfoShape(g);
                break;
        }
    }

    // ------------------------------------------------------------------ pluma

    /// <summary>Coordenadas unitarias (-1..1 en los dos ejes) a píxeles, con el grosor de trazo del tamaño.</summary>
    private readonly struct Pen
    {
        public readonly CanvasItem T;
        public readonly Vector2 C;
        public readonly float H;
        public readonly float W;

        public Pen(CanvasItem target, Vector2 center, float size)
        {
            T = target;
            C = center;
            H = size / 2f;
            W = Mathf.Max(1.4f, size * 0.055f);
        }

        public Vector2 P(float x, float y) => C + new Vector2(x * H, y * H);

        public Vector2[] Ps(params float[] xy)
        {
            var points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = P(xy[i * 2], xy[(i * 2) + 1]);
            }

            return points;
        }

        public void Fill(Color color, params float[] xy) => Ink.Poly(T, Ps(xy), color, W);

        public void Flat(Color color, params float[] xy) => T.DrawColoredPolygon(Ps(xy), color);

        public void Line(float x0, float y0, float x1, float y1, Color? color = null, float scale = 1f) =>
            T.DrawLine(P(x0, y0), P(x1, y1), color ?? Ink.Black, W * scale, true);

        public void Circle(float x, float y, float r, Color color, bool outline = true)
        {
            if (outline)
            {
                Ink.Disc(T, P(x, y), r * H, color, W);
            }
            else
            {
                T.DrawCircle(P(x, y), r * H, color);
            }
        }

        public void Arc(float x, float y, float r, float from, float to, Color? color = null, float scale = 1f) =>
            T.DrawArc(P(x, y), r * H, from, to, 20, color ?? Ink.Black, W * scale, true);
    }

    // ------------------------------------------------------------------ dibujos

    private static void Fist(Pen g, Color skin, bool burst)
    {
        if (burst)
        {
            var star = Broadcast.Pregon.Burst(g.H * 0.98f, g.H * 0.62f, 9, g.C);
            Ink.Poly(g.T, star, Ink.Ochre, g.W * 0.8f);
        }

        float s = burst ? 0.78f : 1f;
        // Puño cerrado de frente: cuatro nudillos arriba, palma, pulgar cruzado y muñeca.
        g.Fill(skin, -0.62f * s, -0.28f * s, 0.62f * s, -0.28f * s, 0.7f * s, 0.35f * s, 0.35f * s, 0.62f * s, -0.4f * s, 0.62f * s, -0.72f * s, 0.3f * s);
        for (int i = 0; i < 4; i++)
        {
            float x = (-0.47f + (i * 0.31f)) * s;
            g.Circle(x, -0.36f * s, 0.19f * s, skin);
        }

        g.Fill(skin, -0.5f * s, 0.08f * s, 0.15f * s, 0.02f * s, 0.2f * s, 0.2f * s, -0.45f * s, 0.26f * s);
        g.Fill(Ink.Red.Darkened(0.2f), -0.35f * s, 0.62f * s, 0.32f * s, 0.62f * s, 0.3f * s, 0.95f * s, -0.33f * s, 0.95f * s);
    }

    private static void WingedBoot(Pen g, Color leather)
    {
        // Ala detrás del talón, tres plumas.
        g.Fill(Ink.Paper, -0.15f, -0.15f, -0.95f, -0.75f, -0.72f, -0.35f, -0.98f, -0.3f, -0.68f, 0.02f, -0.9f, 0.08f, -0.2f, 0.25f);
        g.Line(-0.72f, -0.35f, -0.25f, -0.05f, null, 0.6f);
        g.Line(-0.68f, 0.02f, -0.25f, 0.1f, null, 0.6f);
        // Bota: caña y pie hacia la derecha.
        g.Fill(leather, -0.35f, -0.85f, 0.15f, -0.85f, 0.2f, 0.25f, 0.9f, 0.4f, 0.92f, 0.8f, -0.4f, 0.8f);
        g.Fill(Ink.Black, -0.42f, 0.66f, 0.94f, 0.66f, 0.92f, 0.86f, -0.42f, 0.86f);
        g.Line(-0.3f, -0.55f, 0.12f, -0.55f, Ink.Paper, 0.7f);
    }

    private static void Brain(Pen g, Color pink)
    {
        g.Circle(-0.35f, -0.2f, 0.45f, pink);
        g.Circle(0.3f, -0.25f, 0.48f, pink);
        g.Circle(-0.3f, 0.3f, 0.42f, pink);
        g.Circle(0.35f, 0.28f, 0.42f, pink);
        g.Flat(pink, -0.5f, -0.3f, 0.5f, -0.3f, 0.5f, 0.35f, -0.5f, 0.35f);
        g.Line(0f, -0.6f, 0.02f, 0.65f, null, 0.8f);
        g.Arc(-0.3f, -0.05f, 0.18f, 3.6f, 6.0f, null, 0.7f);
        g.Arc(0.32f, 0.05f, 0.2f, 0.4f, 3.0f, null, 0.7f);
        g.Arc(-0.28f, 0.42f, 0.16f, 4.2f, 6.2f, null, 0.7f);
    }

    private static void Heart(Pen g, Color color, float ox, float oy, float s)
    {
        g.Fill(
            color,
            ox + (0f * s), oy + (-0.45f * s),
            ox + (0.25f * s), oy + (-0.8f * s),
            ox + (0.65f * s), oy + (-0.8f * s),
            ox + (0.9f * s), oy + (-0.45f * s),
            ox + (0.85f * s), oy + (0.0f * s),
            ox + (0f * s), oy + (0.85f * s),
            ox + (-0.85f * s), oy + (0.0f * s),
            ox + (-0.9f * s), oy + (-0.45f * s),
            ox + (-0.65f * s), oy + (-0.8f * s),
            ox + (-0.25f * s), oy + (-0.8f * s));
        if (s > 0.6f)
        {
            g.Line(ox + (-0.55f * s), oy + (-0.55f * s), ox + (-0.35f * s), oy + (-0.62f * s), new Color(1f, 1f, 1f, 0.7f), 0.9f);
        }
    }

    private static void Chain(Pen g, Color steel)
    {
        // Dos eslabones enganchados en diagonal: la correa.
        g.T.DrawSetTransform(g.C, -0.7f, Vector2.One);
        foreach (float x in new[] { -0.38f, 0.38f })
        {
            var center = new Vector2(x * g.H, 0f);
            g.T.DrawArc(center, g.H * 0.36f, 0f, Mathf.Tau, 20, Ink.Black, g.W * 3.2f, true);
            g.T.DrawArc(center, g.H * 0.36f, 0f, Mathf.Tau, 20, steel, g.W * 1.4f, true);
        }

        g.T.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private static void GloveShape(Pen g, Color color)
    {
        // Guante abierto de portero: palma, cuatro dedos y pulgar.
        g.Fill(color, -0.55f, -0.1f, 0.5f, -0.1f, 0.55f, 0.55f, 0.3f, 0.9f, -0.35f, 0.9f, -0.6f, 0.55f);
        for (int i = 0; i < 4; i++)
        {
            float x = -0.42f + (i * 0.28f);
            g.Fill(color, x - 0.12f, -0.12f, x - 0.12f, -0.72f + (Math.Abs(i - 1.5f) * 0.1f), x + 0.12f, -0.72f + (Math.Abs(i - 1.5f) * 0.1f), x + 0.12f, -0.12f);
        }

        g.Fill(color, -0.55f, 0.2f, -0.95f, -0.2f, -0.8f, -0.35f, -0.5f, -0.05f);
        g.Fill(Ink.Paper, -0.45f, 0.62f, 0.45f, 0.62f, 0.4f, 0.8f, -0.4f, 0.8f);
    }

    private static void ShieldShape(Pen g, Color color)
    {
        g.Fill(color, -0.72f, -0.8f, 0.72f, -0.8f, 0.72f, 0.1f, 0f, 0.92f, -0.72f, 0.1f);
        g.Flat(new Color(1f, 1f, 1f, 0.25f), -0.52f, -0.62f, 0f, -0.62f, 0f, 0.62f, -0.52f, 0.05f);
        g.Line(0f, -0.8f, 0f, 0.9f, null, 0.7f);
    }

    private static void RunnerShape(Pen g, Color color)
    {
        // Monigote a la carrera, de trazo gordo.
        g.Circle(0.25f, -0.72f, 0.2f, color, false);
        g.Line(0.15f, -0.45f, -0.1f, 0.15f, color, 3.2f);
        g.Line(0.08f, -0.3f, 0.55f, -0.05f, color, 2.6f);
        g.Line(0.08f, -0.3f, -0.45f, -0.4f, color, 2.6f);
        g.Line(-0.1f, 0.15f, 0.35f, 0.45f, color, 2.8f);
        g.Line(0.35f, 0.45f, 0.25f, 0.9f, color, 2.8f);
        g.Line(-0.1f, 0.15f, -0.5f, 0.45f, color, 2.8f);
        g.Line(-0.5f, 0.45f, -0.85f, 0.35f, color, 2.8f);
    }

    private static void BallShape(Pen g, bool withSpeed)
    {
        float x = withSpeed ? 0.22f : 0f;
        if (withSpeed)
        {
            g.Line(-0.95f, -0.3f, -0.45f, -0.3f, null, 1.2f);
            g.Line(-0.85f, 0.05f, -0.4f, 0.05f, null, 1.2f);
            g.Line(-0.95f, 0.4f, -0.45f, 0.4f, null, 1.2f);
        }

        float r = withSpeed ? 0.66f : 0.85f;
        g.Circle(x, 0f, r, Colors.White);
        var pent = new float[10];
        for (int i = 0; i < 5; i++)
        {
            float a = (-Mathf.Pi / 2f) + (i * Mathf.Tau / 5f);
            pent[i * 2] = x + (Mathf.Cos(a) * r * 0.36f);
            pent[(i * 2) + 1] = Mathf.Sin(a) * r * 0.36f;
            g.Line(x + (Mathf.Cos(a) * r * 0.36f), Mathf.Sin(a) * r * 0.36f, x + (Mathf.Cos(a) * r * 0.9f), Mathf.Sin(a) * r * 0.9f, null, 0.7f);
        }

        g.Fill(Ink.Black, pent);
    }

    private static void CrownShape(Pen g, Color color)
    {
        g.Fill(color, -0.85f, 0.55f, -0.9f, -0.5f, -0.45f, -0.05f, 0f, -0.75f, 0.45f, -0.05f, 0.9f, -0.5f, 0.85f, 0.55f);
        g.Fill(color.Darkened(0.2f), -0.85f, 0.4f, 0.85f, 0.4f, 0.85f, 0.7f, -0.85f, 0.7f);
        g.Circle(0f, 0.15f, 0.12f, Ink.Red);
    }

    private static void BenchShape(Pen g, Color color)
    {
        g.Fill(color, -0.95f, -0.25f, 0.95f, -0.25f, 0.95f, 0.05f, -0.95f, 0.05f);
        g.Fill(color.Darkened(0.2f), -0.75f, 0.05f, -0.55f, 0.05f, -0.55f, 0.75f, -0.75f, 0.75f);
        g.Fill(color.Darkened(0.2f), 0.55f, 0.05f, 0.75f, 0.05f, 0.75f, 0.75f, 0.55f, 0.75f);
    }

    private static void ShirtShape(Pen g, Color color)
    {
        g.Fill(color, -0.3f, -0.8f, -0.9f, -0.5f, -0.7f, -0.05f, -0.45f, -0.18f, -0.45f, 0.85f, 0.45f, 0.85f, 0.45f, -0.18f, 0.7f, -0.05f, 0.9f, -0.5f, 0.3f, -0.8f, 0f, -0.55f);
        g.Line(-0.3f, -0.8f, 0f, -0.55f, Ink.Red, 1.2f);
        g.Line(0.3f, -0.8f, 0f, -0.55f, Ink.Red, 1.2f);
    }

    private static void PitchShape(Pen g)
    {
        g.Fill(new Color("4f8a3a"), -0.95f, -0.65f, 0.95f, -0.65f, 0.95f, 0.65f, -0.95f, 0.65f);
        g.Line(0f, -0.65f, 0f, 0.65f, Ink.Paper, 0.7f);
        g.Arc(0f, 0f, 0.25f, 0f, Mathf.Tau, Ink.Paper, 0.7f);
        g.Line(-0.95f, -0.3f, -0.72f, -0.3f, Ink.Paper, 0.7f);
        g.Line(-0.72f, -0.3f, -0.72f, 0.3f, Ink.Paper, 0.7f);
        g.Line(-0.95f, 0.3f, -0.72f, 0.3f, Ink.Paper, 0.7f);
        g.Line(0.95f, -0.3f, 0.72f, -0.3f, Ink.Paper, 0.7f);
        g.Line(0.72f, -0.3f, 0.72f, 0.3f, Ink.Paper, 0.7f);
        g.Line(0.95f, 0.3f, 0.72f, 0.3f, Ink.Paper, 0.7f);
    }

    private static void Flask(Pen g, Color liquid, bool cross, bool stars = false)
    {
        // Frasco redondo con cuello y corcho.
        g.Fill(Glass, -0.22f, -0.55f, 0.22f, -0.55f, 0.22f, -0.2f, 0.62f, 0.12f, 0.62f, 0.62f, 0.4f, 0.9f, -0.4f, 0.9f, -0.62f, 0.62f, -0.62f, 0.12f, -0.22f, -0.2f);
        g.Flat(liquid, -0.55f, 0.15f, 0.55f, 0.15f, 0.55f, 0.6f, 0.36f, 0.83f, -0.36f, 0.83f, -0.55f, 0.6f);
        g.Line(-0.55f, 0.15f, 0.55f, 0.15f, null, 0.7f);
        g.Fill(new Color("b8864e"), -0.26f, -0.9f, 0.26f, -0.9f, 0.24f, -0.55f, -0.24f, -0.55f);
        g.Line(-0.4f, -0.05f, -0.25f, -0.15f, new Color(1f, 1f, 1f, 0.8f), 0.9f);
        if (cross)
        {
            g.Flat(Colors.White, -0.08f, 0.25f, 0.08f, 0.25f, 0.08f, 0.38f, 0.22f, 0.38f, 0.22f, 0.52f, 0.08f, 0.52f, 0.08f, 0.66f, -0.08f, 0.66f, -0.08f, 0.52f, -0.22f, 0.52f, -0.22f, 0.38f, -0.08f, 0.38f);
        }

        if (stars)
        {
            var s1 = Broadcast.Pregon.Burst(g.H * 0.16f, g.H * 0.06f, 4, g.P(-0.15f, 0.45f));
            var s2 = Broadcast.Pregon.Burst(g.H * 0.11f, g.H * 0.04f, 4, g.P(0.22f, 0.3f));
            g.T.DrawColoredPolygon(s1, new Color("fff3b0"));
            g.T.DrawColoredPolygon(s2, new Color("fff3b0"));
        }
    }

    private static void ChestShape(Pen g)
    {
        // Cofre de madera con tapa curva, herrajes y cerradura.
        g.Fill(Ink.WoodLight, -0.9f, -0.1f, 0.9f, -0.1f, 0.9f, 0.8f, -0.9f, 0.8f);
        var lid = new float[] { -0.9f, -0.1f, -0.9f, -0.35f, -0.7f, -0.7f, -0.3f, -0.85f, 0.3f, -0.85f, 0.7f, -0.7f, 0.9f, -0.35f, 0.9f, -0.1f };
        g.Fill(Ink.Wood, lid);
        g.Line(-0.9f, 0.35f, 0.9f, 0.35f, Ink.WoodDark, 0.7f);
        g.Fill(SteelDark, -0.62f, -0.82f, -0.46f, -0.86f, -0.46f, 0.8f, -0.62f, 0.8f);
        g.Fill(SteelDark, 0.46f, -0.86f, 0.62f, -0.82f, 0.62f, 0.8f, 0.46f, 0.8f);
        g.Fill(Gold, -0.16f, -0.25f, 0.16f, -0.25f, 0.16f, 0.15f, -0.16f, 0.15f);
        g.Circle(0f, -0.08f, 0.05f, Ink.Black, false);
    }

    private static void SwapShape(Pen g, Color color)
    {
        g.Line(-0.75f, -0.3f, 0.6f, -0.3f, color, 2f);
        g.Fill(color, 0.45f, -0.6f, 0.9f, -0.3f, 0.45f, 0f);
        g.Line(0.75f, 0.3f, -0.6f, 0.3f, color, 2f);
        g.Fill(color, -0.45f, 0f, -0.9f, 0.3f, -0.45f, 0.6f);
    }

    private static void ArrowShape(Pen g, Color color)
    {
        g.Fill(color, -0.9f, 0f, -0.2f, -0.65f, -0.2f, -0.28f, 0.85f, -0.28f, 0.85f, 0.28f, -0.2f, 0.28f, -0.2f, 0.65f);
    }

    private static void ZonesShape(Pen g)
    {
        g.Fill(new Color("5b8fc9"), -0.95f, -0.7f, -0.33f, -0.7f, -0.33f, 0.7f, -0.95f, 0.7f);
        g.Fill(Gold, -0.33f, -0.7f, 0.33f, -0.7f, 0.33f, 0.7f, -0.33f, 0.7f);
        g.Fill(Ink.Red, 0.33f, -0.7f, 0.95f, -0.7f, 0.95f, 0.7f, 0.33f, 0.7f);
    }

    private static void EyeShape(Pen g)
    {
        g.Fill(Colors.White, -0.95f, 0f, -0.5f, -0.5f, 0f, -0.62f, 0.5f, -0.5f, 0.95f, 0f, 0.5f, 0.5f, 0f, 0.62f, -0.5f, 0.5f);
        g.Circle(0f, 0f, 0.35f, new Color("5fad56"));
        g.Circle(0f, 0f, 0.15f, Ink.Black, false);
    }

    private static void BootShape(Pen g, Color color)
    {
        g.Fill(color, -0.45f, -0.9f, 0.2f, -0.9f, 0.22f, 0.15f, 0.85f, 0.35f, 0.95f, 0.72f, -0.5f, 0.72f);
        g.Fill(Ink.Black, -0.52f, 0.6f, 0.97f, 0.6f, 0.95f, 0.88f, -0.52f, 0.88f);
        g.Fill(color.Darkened(0.3f), -0.5f, -0.9f, 0.25f, -0.9f, 0.25f, -0.62f, -0.5f, -0.62f);
        for (int i = 0; i < 3; i++)
        {
            float y = -0.4f + (i * 0.2f);
            g.Line(-0.1f, y, 0.2f, y + 0.08f, Ink.Paper, 0.8f);
        }

        // Tacos de metal: la bota es para hacer daño.
        g.Fill(Steel, -0.35f, 0.88f, -0.25f, 1f, -0.15f, 0.88f);
        g.Fill(Steel, 0.2f, 0.88f, 0.3f, 1f, 0.4f, 0.88f);
        g.Fill(Steel, 0.62f, 0.88f, 0.72f, 1f, 0.82f, 0.88f);
    }

    private static void PauldronShape(Pen g, Color color)
    {
        g.Fill(color, -0.9f, 0.5f, -0.8f, -0.2f, -0.35f, -0.7f, 0.35f, -0.7f, 0.8f, -0.2f, 0.9f, 0.5f, 0.5f, 0.3f, 0f, 0.25f, -0.5f, 0.3f);
        g.Fill(color.Darkened(0.25f), -0.7f, 0.45f, -0.6f, 0.1f, 0f, -0.05f, 0.6f, 0.1f, 0.7f, 0.45f, 0f, 0.35f);
        g.Fill(Steel, -0.1f, -0.95f, 0.1f, -0.95f, 0.05f, -0.68f, -0.05f, -0.68f);
        g.Circle(-0.45f, -0.2f, 0.07f, Ink.Black, false);
        g.Circle(0.45f, -0.2f, 0.07f, Ink.Black, false);
    }

    private static void HelmetShape(Pen g, Color color)
    {
        g.Fill(color, -0.75f, 0.65f, -0.8f, -0.1f, -0.5f, -0.7f, 0f, -0.85f, 0.5f, -0.7f, 0.8f, -0.1f, 0.75f, 0.65f, 0.25f, 0.65f, 0.2f, 0.05f, -0.2f, 0.05f, -0.25f, 0.65f);
        g.Fill(Ink.Black, -0.55f, -0.1f, -0.2f, -0.1f, -0.2f, 0.05f, -0.55f, 0.05f);
        g.Fill(Ink.Black, 0.2f, -0.1f, 0.55f, -0.1f, 0.55f, 0.05f, 0.2f, 0.05f);
        g.Fill(color.Darkened(0.2f), -0.08f, -0.2f, 0.08f, -0.2f, 0.08f, 0.62f, -0.08f, 0.62f);
    }

    private static void AmuletShape(Pen g, Color gem)
    {
        g.Arc(0f, -0.35f, 0.55f, Mathf.Pi, Mathf.Tau, null, 1.6f);
        g.Arc(0f, -0.35f, 0.55f, Mathf.Pi, Mathf.Tau, Gold, 0.7f);
        g.Fill(Gold, 0f, -0.2f, 0.52f, 0.25f, 0f, 0.92f, -0.52f, 0.25f);
        g.Fill(gem, 0f, 0.05f, 0.28f, 0.3f, 0f, 0.68f, -0.28f, 0.3f);
    }

    private static void BannerShape(Pen g, Color color)
    {
        g.Fill(Ink.WoodLight, -0.7f, -0.95f, -0.58f, -0.95f, -0.58f, 0.95f, -0.7f, 0.95f);
        g.Fill(color, -0.58f, -0.85f, 0.8f, -0.85f, 0.8f, 0.4f, 0.1f, 0.15f, -0.58f, 0.4f);
        g.Circle(0.1f, -0.35f, 0.18f, Gold);
    }

    private static void BladeShape(Pen g)
    {
        g.Fill(Steel, -0.08f, -0.95f, 0.12f, -0.75f, 0.12f, 0.35f, -0.08f, 0.35f);
        g.Flat(new Color(1f, 1f, 1f, 0.5f), -0.04f, -0.8f, 0.02f, -0.75f, 0.02f, 0.3f, -0.04f, 0.3f);
        // Mella: el filo está roto a mitad.
        g.Flat(Ink.Paper, 0.12f, -0.3f, 0.02f, -0.22f, 0.12f, -0.12f);
        g.Fill(Gold, -0.45f, 0.35f, 0.5f, 0.35f, 0.5f, 0.5f, -0.45f, 0.5f);
        g.Fill(Leather, -0.08f, 0.5f, 0.12f, 0.5f, 0.12f, 0.88f, -0.08f, 0.88f);
    }

    private static void RingShape(Pen g, Color color)
    {
        g.Arc(0f, 0.15f, 0.55f, 0f, Mathf.Tau, null, 3.4f);
        g.Arc(0f, 0.15f, 0.55f, 0f, Mathf.Tau, color, 1.6f);
        g.Fill(Ink.Red, 0f, -0.9f, 0.3f, -0.55f, 0f, -0.25f, -0.3f, -0.55f);
    }

    private static void WhistleShape(Pen g, Color color)
    {
        g.Fill(color, -0.8f, -0.25f, 0.2f, -0.25f, 0.2f, 0.05f, -0.8f, 0.05f);
        g.Circle(0.35f, 0.2f, 0.45f, color);
        g.Circle(0.35f, 0.2f, 0.14f, Ink.Black, false);
        g.Arc(-0.6f, -0.6f, 0.35f, 0.5f, 2.2f, null, 0.8f);
    }

    private static void LanternShape(Pen g)
    {
        g.Fill(SteelDark, -0.35f, -0.85f, 0.35f, -0.85f, 0.45f, -0.6f, -0.45f, -0.6f);
        g.Fill(new Color("f6d36b"), -0.45f, -0.6f, 0.45f, -0.6f, 0.4f, 0.65f, -0.4f, 0.65f);
        g.Fill(new Color("e0632f"), 0f, -0.35f, 0.18f, 0.2f, 0f, 0.4f, -0.18f, 0.2f);
        g.Fill(SteelDark, -0.5f, 0.65f, 0.5f, 0.65f, 0.5f, 0.85f, -0.5f, 0.85f);
        g.Line(-0.15f, -0.6f, -0.15f, 0.65f, null, 0.6f);
        g.Line(0.15f, -0.6f, 0.15f, 0.65f, null, 0.6f);
    }

    private static void TotemShape(Pen g)
    {
        // Poste tallado con cara de pocos amigos y dos plumas rojas: el tótem del que salen los malditos.
        g.Fill(Ink.Red, -0.5f, -0.55f, -0.95f, -0.95f, -0.62f, -0.4f);
        g.Fill(Ink.Red, 0.5f, -0.55f, 0.95f, -0.95f, 0.62f, -0.4f);
        g.Fill(Ink.WoodLight, -0.5f, -0.8f, 0.5f, -0.8f, 0.55f, 0.95f, -0.55f, 0.95f);
        g.Line(-0.5f, 0.1f, 0.52f, 0.1f, Ink.WoodDark, 0.8f);
        g.Fill(Ink.Black, -0.35f, -0.45f, -0.08f, -0.3f, -0.35f, -0.25f);
        g.Fill(Ink.Black, 0.35f, -0.45f, 0.08f, -0.3f, 0.35f, -0.25f);
        g.Fill(Ink.Paper, -0.3f, -0.15f, 0.3f, -0.15f, 0.25f, 0.02f, -0.25f, 0.02f);
        g.Line(-0.1f, -0.15f, -0.1f, 0.02f, null, 0.6f);
        g.Line(0.1f, -0.15f, 0.1f, 0.02f, null, 0.6f);
        g.Fill(Ink.Ochre, -0.3f, 0.3f, 0.3f, 0.3f, 0f, 0.75f);
    }

    private static void ScrollShape(Pen g)
    {
        g.Fill(Ink.Paper, -0.6f, -0.65f, 0.6f, -0.65f, 0.6f, 0.65f, -0.6f, 0.65f);
        g.Fill(Ink.PaperWarm, -0.8f, -0.8f, 0.8f, -0.8f, 0.8f, -0.55f, -0.8f, -0.55f);
        g.Fill(Ink.PaperWarm, -0.8f, 0.55f, 0.8f, 0.55f, 0.8f, 0.8f, -0.8f, 0.8f);
        // Pizarra táctica: dos cruces y una flecha.
        g.Line(-0.4f, -0.3f, -0.2f, -0.1f, Ink.Red, 0.8f);
        g.Line(-0.2f, -0.3f, -0.4f, -0.1f, Ink.Red, 0.8f);
        g.Line(-0.25f, 0.3f, 0.35f, -0.2f, null, 0.8f);
        g.Fill(Ink.Black, 0.2f, -0.3f, 0.45f, -0.3f, 0.4f, -0.05f);
    }

    private static void KnuckleShape(Pen g, Color color)
    {
        g.Fill(color, -0.85f, -0.4f, 0.85f, -0.4f, 0.85f, 0.05f, 0.4f, 0.75f, -0.4f, 0.75f, -0.85f, 0.05f);
        for (int i = 0; i < 4; i++)
        {
            g.Circle(-0.6f + (i * 0.4f), -0.2f, 0.14f, Ink.Paper);
        }

        g.Flat(new Color("7a5a2e", 0.8f), 0.2f, 0.2f, 0.7f, 0.1f, 0.55f, 0.45f, 0.1f, 0.5f);
    }

    private static void HandShape(Pen g, Color skin)
    {
        // Mano que pulsa: índice extendido hacia arriba.
        g.Fill(skin, -0.1f, -0.95f, 0.18f, -0.95f, 0.18f, -0.1f, 0.55f, -0.1f, 0.6f, 0.5f, 0.3f, 0.9f, -0.45f, 0.9f, -0.6f, 0.35f, -0.62f, -0.15f, -0.38f, -0.15f, -0.35f, 0.1f, -0.1f, 0.05f);
        g.Line(0.18f, 0.12f, 0.55f, 0.12f, null, 0.6f);
        g.Line(0.18f, 0.3f, 0.58f, 0.3f, null, 0.6f);
    }

    private static void HourglassShape(Pen g)
    {
        g.Fill(Ink.WoodLight, -0.7f, -0.95f, 0.7f, -0.95f, 0.7f, -0.78f, -0.7f, -0.78f);
        g.Fill(Ink.WoodLight, -0.7f, 0.78f, 0.7f, 0.78f, 0.7f, 0.95f, -0.7f, 0.95f);
        g.Fill(Glass, -0.5f, -0.78f, 0.5f, -0.78f, 0.08f, 0f, 0.5f, 0.78f, -0.5f, 0.78f, -0.08f, 0f);
        g.Flat(Gold, -0.28f, -0.4f, 0.28f, -0.4f, 0.04f, -0.05f, -0.04f, -0.05f);
        g.Flat(Gold, -0.42f, 0.74f, 0.42f, 0.74f, 0f, 0.35f);
    }

    private static void TargetShape(Pen g)
    {
        g.Circle(0f, 0f, 0.88f, Colors.White);
        g.Circle(0f, 0f, 0.6f, Ink.Red);
        g.Circle(0f, 0f, 0.32f, Colors.White);
        g.Circle(0f, 0f, 0.12f, Ink.Red);
        g.Line(0.95f, -0.95f, 0.05f, -0.05f, null, 1.3f);
        g.Fill(Ink.Paper, 0.95f, -0.95f, 0.6f, -0.95f, 0.95f, -0.6f);
    }

    private static void DropShape(Pen g, Color color)
    {
        g.Fill(color, 0f, -0.95f, 0.45f, -0.2f, 0.62f, 0.25f, 0.45f, 0.7f, 0f, 0.9f, -0.45f, 0.7f, -0.62f, 0.25f, -0.45f, -0.2f);
        g.Line(-0.25f, 0.15f, -0.2f, 0.45f, new Color(1f, 1f, 1f, 0.7f), 1.1f);
    }

    private static void FlagShape(Pen g, Color color)
    {
        g.Fill(Ink.WoodLight, -0.75f, -0.95f, -0.6f, -0.95f, -0.6f, 0.95f, -0.75f, 0.95f);
        g.Fill(color, -0.6f, -0.85f, 0.2f, -0.72f, 0.85f, -0.85f, 0.8f, -0.05f, 0.15f, 0.08f, -0.6f, -0.05f);
    }

    private static void HornShape(Pen g, Color color)
    {
        g.Fill(color, -0.9f, -0.12f, 0.2f, -0.35f, 0.9f, -0.85f, 0.9f, 0.85f, 0.2f, 0.35f, -0.9f, 0.12f);
        g.Line(0.9f, -0.85f, 0.9f, 0.85f, null, 1.6f);
        g.Arc(0.95f, 0f, 0.3f, -0.8f, 0.8f, null, 0.8f);
    }

    private static void SleepShape(Pen g)
    {
        var font = Ink.Heavy;
        int big = Mathf.Max(8, Mathf.RoundToInt(g.H * 1.3f));
        int small = Mathf.Max(7, Mathf.RoundToInt(g.H * 0.9f));
        Ink.Outlined(g.T, font, g.P(-0.9f, -0.2f), "Z", big, Ink.Paper, Ink.Black, Mathf.Max(3, (int)g.W * 2));
        Ink.Outlined(g.T, font, g.P(0.15f, -0.9f), "z", small, Ink.Paper, Ink.Black, Mathf.Max(3, (int)g.W * 2));
    }

    private static void PawShape(Pen g, Color color)
    {
        g.Circle(0f, 0.3f, 0.45f, color, false);
        g.Circle(-0.6f, -0.2f, 0.2f, color, false);
        g.Circle(-0.25f, -0.62f, 0.2f, color, false);
        g.Circle(0.25f, -0.62f, 0.2f, color, false);
        g.Circle(0.6f, -0.2f, 0.2f, color, false);
    }

    private static void WallShape(Pen g, Color color)
    {
        g.Fill(color, -0.9f, -0.75f, 0.9f, -0.75f, 0.9f, 0.8f, -0.9f, 0.8f);
        for (int row = 0; row < 3; row++)
        {
            float y = -0.75f + (row * 0.52f);
            g.Line(-0.9f, y + 0.52f, 0.9f, y + 0.52f, null, 0.6f);
            float offset = row % 2 == 0 ? 0f : 0.3f;
            for (float x = -0.6f + offset; x < 0.9f; x += 0.6f)
            {
                g.Line(x, y, x, y + 0.52f, null, 0.6f);
            }
        }
    }

    private static void StarShape(Pen g, Color color)
    {
        var star = Broadcast.Pregon.Burst(g.H * 0.95f, g.H * 0.42f, 5, g.C);
        // Rota para que la punta mire arriba.
        for (int i = 0; i < star.Length; i++)
        {
            star[i] = g.C + (star[i] - g.C).Rotated(-Mathf.Pi / 2f);
        }

        Ink.Poly(g.T, star, color, g.W);
    }

    private static void FlameShape(Pen g, Color color)
    {
        g.Fill(color, 0f, -0.95f, 0.35f, -0.45f, 0.3f, -0.7f, 0.7f, -0.1f, 0.6f, 0.55f, 0f, 0.92f, -0.6f, 0.55f, -0.7f, -0.05f, -0.35f, -0.5f, -0.25f, -0.2f);
        g.Fill(Ink.Red, 0f, -0.2f, 0.3f, 0.3f, 0f, 0.72f, -0.3f, 0.3f);
    }

    private static void EmptyShape(Pen g)
    {
        const int Dashes = 12;
        for (int i = 0; i < Dashes; i++)
        {
            float a0 = i * Mathf.Tau / Dashes;
            g.Arc(0f, 0f, 0.8f, a0, a0 + (Mathf.Tau / Dashes * 0.55f), Ink.Muted, 0.9f);
        }

        g.Line(-0.35f, 0f, 0.35f, 0f, Ink.Muted, 1.1f);
        g.Line(0f, -0.35f, 0f, 0.35f, Ink.Muted, 1.1f);
    }

    private static void BandageShape(Pen g)
    {
        g.T.DrawSetTransform(g.C, -0.75f, Vector2.One);
        var rect = new Rect2(-g.H * 0.95f, -g.H * 0.36f, g.H * 1.9f, g.H * 0.72f);
        Ink.Poly(g.T, Ink.Rough(rect, 0.5f, 3, 6), Ink.Paper, g.W);
        g.T.DrawRect(new Rect2(-g.H * 0.3f, -g.H * 0.36f, g.H * 0.6f, g.H * 0.72f), Ink.PaperDark);
        g.T.DrawCircle(new Vector2(-g.H * 0.1f, 0f), g.H * 0.06f, Ink.Muted);
        g.T.DrawCircle(new Vector2(g.H * 0.12f, -g.H * 0.1f), g.H * 0.06f, Ink.Muted);
        g.T.DrawCircle(new Vector2(g.H * 0.1f, g.H * 0.14f), g.H * 0.06f, Ink.Muted);
        g.T.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private static void CrutchShape(Pen g)
    {
        g.Line(-0.15f, -0.55f, 0.1f, 0.95f, null, 3f);
        g.Line(-0.15f, -0.55f, 0.1f, 0.95f, Ink.WoodLight, 1.4f);
        g.Line(0.45f, -0.6f, 0.1f, 0.2f, null, 3f);
        g.Line(0.45f, -0.6f, 0.1f, 0.2f, Ink.WoodLight, 1.4f);
        g.Fill(Ink.Paper, -0.5f, -0.9f, 0.75f, -0.72f, 0.7f, -0.5f, -0.55f, -0.68f);
        g.Fill(Ink.Red, -0.12f, 0.1f, 0.3f, 0.05f, 0.33f, 0.2f, -0.1f, 0.26f);
    }

    private static void TombShape(Pen g)
    {
        g.Fill(new Color("9a9d97"), -0.6f, 0.85f, -0.6f, -0.35f, -0.4f, -0.78f, 0f, -0.92f, 0.4f, -0.78f, 0.6f, -0.35f, 0.6f, 0.85f);
        g.Fill(new Color("5c8a35"), -0.95f, 0.72f, 0.95f, 0.72f, 0.95f, 0.95f, -0.95f, 0.95f);
        var font = Ink.Heavy;
        int size = Mathf.Max(7, Mathf.RoundToInt(g.H * 0.55f));
        float width = Ink.Width(font, "RIP", size);
        Ink.Text(g.T, font, g.P(0f, -0.35f) - new Vector2(width / 2f, 0f), "RIP", size, Ink.Black);
    }

    private static void InfoShape(Pen g)
    {
        g.Circle(0f, 0f, 0.9f, Ink.Paper);
        g.Circle(0f, -0.45f, 0.12f, Ink.Black, false);
        g.Line(0f, -0.15f, 0f, 0.55f, null, 1.8f);
    }
}
