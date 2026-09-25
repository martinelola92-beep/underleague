using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// El enfriamiento de perk (RF-069c, ADR 0149): <c>limit.cooldownSeconds</c>. Es la primitiva que separa
/// un <b>acto</b> —ocurre ahora y no vuelve a ocurrir en un rato— de un estado permanente, y la que
/// permite colgar un perk de un evento frecuente sin que deje de ser un suceso.
///
/// <para>Los tres primeros fijan la <b>semántica</b> —se activa, se salta dentro de la ventana, vuelve
/// justo al cumplirse— y el resto, lo que el cargador tiene que <b>rechazar</b> (RT-032): un dato que no
/// significa lo que parece es un error explícito, nunca una anécdota.</para>
/// </summary>
public sealed class PerkCooldownTests
{
    private const string Strength1 =
        """[ { "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 1, "duration": "play" } ]""";

    /// <summary>Ticks lógicos por segundo (RT-020): lo que el cargador usa para convertir el dato.</summary>
    private const int TicksPerSecond = 15;

    [Fact]
    public void TheFirstActivationAlwaysGoesThrough()
    {
        var engine = Armed(cooldownSeconds: 4);

        Publish(engine, tick: 0);

        Assert.Single(engine.Report.PerkActivations);
    }

    [Fact]
    public void AnActivationInsideTheWindowIsSkipped()
    {
        var engine = Armed(cooldownSeconds: 4);

        Publish(engine, tick: 0);
        Publish(engine, tick: (4 * TicksPerSecond) - 1);

        Assert.Single(engine.Report.PerkActivations);
    }

    /// <summary>
    /// El borde exacto: el enfriamiento se mide <b>desde</b> la última activación, así que en el tick que
    /// lo cumple ya está disponible. Sin este caso, un desliz de uno en la comparación pasaría inadvertido.
    /// </summary>
    [Fact]
    public void ItComesBackExactlyWhenTheCooldownIsMet()
    {
        var engine = Armed(cooldownSeconds: 4);

        Publish(engine, tick: 0);
        Publish(engine, tick: 4 * TicksPerSecond);

        Assert.Equal(2, engine.Report.PerkActivations.Count);
    }

    /// <summary>
    /// Un enfriamiento mide tiempo de partido, no jugadas: el final de una jugada reinicia los cupos
    /// <c>per: play</c> pero <b>no</b> el reloj del enfriamiento. Si lo reiniciara, un perk con
    /// enfriamiento largo se dispararía en cada jugada y la primitiva no serviría para nada.
    /// </summary>
    [Fact]
    public void TheEndOfAPlayDoesNotRefundTheCooldown()
    {
        var engine = Armed(cooldownSeconds: 4);

        Publish(engine, tick: 0);
        engine.Effects!.EndPlay();
        Publish(engine, tick: 10);

        Assert.Single(engine.Report.PerkActivations);
    }

    [Fact]
    public void ACooldownWithoutAQuotaIsLegalAndNeedsNoScope()
    {
        var perk = TestPerks.Load("cooler", TestPerks.Json(
            "cooler", "TACKLE", Strength1, limit: """{ "cooldownSeconds": 3 }"""));

        Assert.NotNull(perk.Limit);
        Assert.Equal(3 * TicksPerSecond, perk.Limit!.CooldownTicks);
        Assert.Equal(int.MaxValue, perk.Limit.Times);
    }

    [Fact]
    public void AQuotaAndACooldownCanLiveInTheSamePerk()
    {
        var perk = TestPerks.Load("both", TestPerks.Json(
            "both", "TACKLE", Strength1, limit: """{ "per": "match", "times": 2, "cooldownSeconds": 3 }"""));

        Assert.Equal(new LimitDefinition(LimitScope.Match, 2, 3 * TicksPerSecond), perk.Limit);
    }

    [Theory]
    // Un límite que no declara ni cupo ni enfriamiento no limita nada, y el fichero se lee como si sí.
    [InlineData("""{ }""")]
    // Un ámbito sin cupo es decorativo: 'per' solo reinicia el cupo, y sin cupo no hay nada que reiniciar.
    [InlineData("""{ "per": "play", "cooldownSeconds": 3 }""")]
    // Un cupo sin ámbito no dice por cuánto tiempo.
    [InlineData("""{ "times": 2 }""")]
    // Cero segundos no es un enfriamiento.
    [InlineData("""{ "cooldownSeconds": 0 }""")]
    // Y un enfriamiento negativo, menos.
    [InlineData("""{ "cooldownSeconds": -1 }""")]
    public void TheLoaderRefusesALimitThatDoesNotMeanWhatItLooksLike(string limit)
    {
        Assert.Throws<DataException>(() => TestPerks.Load(
            "bad", TestPerks.Json("bad", "TACKLE", Strength1, limit: limit)));
    }

    private static MatchEngine Armed(int cooldownSeconds)
    {
        var catalog = TestPerks.CatalogWith(("cooler", TestPerks.Json(
            "cooler", "TACKLE", Strength1, limit: $$"""{ "cooldownSeconds": {{cooldownSeconds}} }""")));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "cooler" }));
        return TestPerks.Engine(catalog, setup);
    }

    private static void Publish(MatchEngine engine, int tick)
    {
        var actor = engine.PlayerById(1)!;
        engine.Effects!.Publish(new MatchEvent(
            EventType.Tackle, tick, actor.Team, actor.Id, -1, -1,
            actor.HomeCell, Zone.Own, MatchPhase.OpenPlay, 0, 0, "attempted"));
    }
}
