using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Cuerpos con volumen y separación blanda (ADR 0020, RT-016, fase1b-diseno.md §2.1): acumulación en
/// buffer (Jacobi), reparto inverso a la masa, tope por tick e inmunidad al empuje.
/// </summary>
public sealed class BodiesTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// El desplazamiento se acumula en un buffer y se aplica al final: el jugador del medio de tres
    /// cuerpos alineados y simétricos recibe dos empujes iguales y opuestos y <b>no se mueve</b>, y los
    /// de los extremos se separan exactamente lo mismo. Aplicando los empujes sobre la marcha, el del
    /// medio se movería con el primer par y el resultado dependería del orden del bucle: es el sesgo por
    /// id que hubo que corregir en la fase 0 y lo que este test vigila.
    /// </summary>
    [Fact]
    public void SeparationAccumulatesInABufferAndIsAppliedAtTheEnd()
    {
        var left = Player(0, Race.Human, Position.Midfielder, new Cell(5, 2));
        var middle = Player(1, Race.Human, Position.Midfielder, new Cell(6, 2));
        var right = Player(2, Race.Human, Position.Midfielder, new Cell(7, 2));
        var players = Order(left, middle, right);

        left.Position = new Vec2(5.0f, 2.5f);
        middle.Position = new Vec2(5.6f, 2.5f);
        right.Position = new Vec2(6.2f, 2.5f);

        Separate(players);

        Assert.Equal(2.5f, middle.Position.Y, 5);
        Assert.Equal(5.6f, middle.Position.X, 5);
        Assert.True(left.Position.X < 5.0f, $"el de la izquierda debía retroceder, quedó en {left.Position.X}");
        Assert.True(right.Position.X > 6.2f, $"el de la derecha debía avanzar, quedó en {right.Position.X}");
        Assert.Equal(5.0f - left.Position.X, right.Position.X - 6.2f, 5);
    }

    /// <summary>
    /// El desplazamiento de cada jugador es la suma de sus contactos por pares, no el resultado de una
    /// cadena de movimientos: se comprueba comparando el empuje de tres cuerpos a la vez con la suma de
    /// los empujes de cada par por separado. Es la propiedad que hace conmutativo el orden del bucle.
    /// </summary>
    [Fact]
    public void ThePushOfAPlayerIsTheSumOfItsPairwiseContacts()
    {
        float[] together = Displacements(5.0f, 5.55f, 6.1f);
        float[] leftPair = Displacements(5.0f, 5.55f, 99f);
        float[] rightPair = Displacements(99f, 5.55f, 6.1f);

        for (int i = 0; i < together.Length; i++)
        {
            Assert.Equal(leftPair[i] + rightPair[i], together[i], 5);
        }
    }

    /// <summary>
    /// Inmunidad al empuje (§2.1.5): la habilidad racial <c>roots</c> (Raíces, enanos) deja el
    /// desplazamiento en cero, y el que choca con él se lleva el solape entero. El motor no pregunta por
    /// la raza: lee <see cref="MatchPlayer.Immovable"/>, que un efecto de perk también puede encender.
    /// </summary>
    [Fact]
    public void APlayerWithRootsIsNeverPushed()
    {
        var dwarf = Player(0, Race.Dwarf, Position.Defender, new Cell(5, 2));
        var human = Player(1, Race.Human, Position.Midfielder, new Cell(6, 2));
        var players = Order(dwarf, human);

        Assert.True(dwarf.Immovable, "la raza con la habilidad 'roots' arranca con Immovable");
        Assert.False(human.Immovable);

        dwarf.Position = new Vec2(5.0f, 2.5f);
        human.Position = new Vec2(5.58f, 2.5f);
        Separate(players);

        Assert.Equal(5.0f, dwarf.Position.X, 5);
        Assert.Equal(2.5f, dwarf.Position.Y, 5);
        Assert.Equal(0.62f - 0.58f, human.Position.X - 5.58f, 4);
    }

    /// <summary>
    /// La inmunidad es una propiedad del jugador, no de la raza: apagarla en un enano lo vuelve
    /// empujable. Es el gancho por el que el paquete de perks activará el efecto sin tocar el motor.
    /// </summary>
    [Fact]
    public void ImmunityIsAPlayerPropertyThatAnEffectCanToggle()
    {
        var dwarf = Player(0, Race.Dwarf, Position.Defender, new Cell(5, 2));
        var human = Player(1, Race.Human, Position.Midfielder, new Cell(6, 2));
        var players = Order(dwarf, human);

        dwarf.Immovable = false;
        dwarf.Position = new Vec2(5.0f, 2.5f);
        human.Position = new Vec2(5.58f, 2.5f);
        Separate(players);

        Assert.True(dwarf.Position.X < 5.0f, "sin inmunidad el enano también se mueve");
    }

    /// <summary>
    /// El reparto es inverso a la masa (§2.1.2): el orco fuerte y grande abre hueco y el elfo flaco sale
    /// despedido. Las dos distancias son menores que el tope por tick, así que lo que se compara es el
    /// reparto y no el recorte.
    /// </summary>
    [Fact]
    public void TheLighterBodyTakesTheLargerShareOfThePush()
    {
        var orc = Player(0, Race.Orc, Position.Defender, new Cell(5, 2), strength: 80);
        var elf = Player(1, Race.Elf, Position.Forward, new Cell(6, 2), strength: 20);
        var players = Order(orc, elf);

        orc.Position = new Vec2(5.0f, 2.5f);
        elf.Position = new Vec2(5.6f, 2.5f);
        Separate(players);

        float orcPush = 5.0f - orc.Position.X;
        float elfPush = elf.Position.X - 5.6f;
        Assert.True(orcPush > 0f && elfPush > 0f);
        Assert.True(elfPush > orcPush * 2f, $"el elfo debía salir despedido: orco {orcPush}, elfo {elfPush}");
    }

    /// <summary>El tope por tick acota el empuje aunque el solape sea enorme (§2.1.3).</summary>
    [Fact]
    public void ThePushPerTickIsCapped()
    {
        var a = Player(0, Race.Human, Position.Midfielder, new Cell(5, 2));
        var b = Player(1, Race.Human, Position.Midfielder, new Cell(6, 2));
        var players = Order(a, b);

        a.Position = new Vec2(5.0f, 2.5f);
        b.Position = new Vec2(5.01f, 2.5f);
        Separate(players);

        float cap = Catalog.Tuning.Bodies.MaxPushPerTickMilli / 1000f;
        Assert.Equal(cap, 5.0f - a.Position.X, 5);
        Assert.Equal(cap, b.Position.X - 5.01f, 5);
    }

    /// <summary>
    /// El empuje de una entrada (§2.1.4) es mucho mayor que el de una separación normal: usa el contacto
    /// pleno y sube el tope del receptor en el mismo <c>tacklePushMultiplier</c>.
    /// </summary>
    [Fact]
    public void ATackleShovesTheCarrierBeyondTheOrdinaryCap()
    {
        var tackler = Player(0, Race.Human, Position.Defender, new Cell(5, 2));
        var carrier = Player(1, Race.Human, Position.Forward, new Cell(6, 2));
        var players = Order(tackler, carrier);

        tackler.Position = new Vec2(5.0f, 2.5f);
        carrier.Position = new Vec2(5.9f, 2.5f);

        var separation = new BodySeparation(Catalog.Tuning.Bodies, players.Length);
        separation.BeginTick();
        separation.AddTacklePush(tackler, carrier);
        separation.Resolve(players);

        float cap = Catalog.Tuning.Bodies.MaxPushPerTickMilli / 1000f;
        float pushed = carrier.Position.X - 5.9f;
        Assert.True(pushed > cap, $"la entrada debía empujar más que el tope normal ({cap}), empujó {pushed}");
        Assert.Equal(cap * Catalog.Tuning.Bodies.TacklePushMultiplier / 100f, pushed, 5);
    }

    /// <summary>Desplazamientos en X de tres cuerpos colocados en las coordenadas indicadas.</summary>
    private static float[] Displacements(float x0, float x1, float x2)
    {
        var a = Player(0, Race.Human, Position.Midfielder, new Cell(5, 2));
        var b = Player(1, Race.Human, Position.Midfielder, new Cell(6, 2));
        var c = Player(2, Race.Human, Position.Midfielder, new Cell(7, 2));
        var players = Order(a, b, c);

        a.Position = new Vec2(x0, 2.5f);
        b.Position = new Vec2(x1, 2.5f);
        c.Position = new Vec2(x2, 2.5f);
        Separate(players);

        return new[] { a.Position.X - x0, b.Position.X - x1, c.Position.X - x2 };
    }

    private static void Separate(MatchPlayer[] players)
    {
        var separation = new BodySeparation(Catalog.Tuning.Bodies, players.Length);
        separation.BeginTick();
        separation.Resolve(players);
    }

    private static MatchPlayer[] Order(params MatchPlayer[] players)
    {
        for (int i = 0; i < players.Length; i++)
        {
            players[i].Index = i;
        }

        return players;
    }

    private static MatchPlayer Player(int id, Race race, Position position, Cell home, int team = 0, int strength = 50)
    {
        var definition = new PlayerDefinition(
            id, "p" + id, race, position, Rarity.Common, 1,
            new Attributes(strength, 50, 50, 50, 50),
            Array.Empty<Trait>(),
            new[] { position.ToString() },
            PhysicalState.Healthy);
        return new MatchPlayer(definition, team, home, Catalog);
    }
}
