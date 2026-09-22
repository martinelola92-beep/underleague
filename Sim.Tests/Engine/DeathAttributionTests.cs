using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Atribución de lesiones y muertes causadas (RF-122, ADR 0124): el motor lleva la cuenta de quién ha
/// hecho qué, no solo cuánto ha pasado. Las dos vías de muerte (RF-093) son las dos que la ADR nombra:
/// la entrada sobre un titular que ya arrastraba una lesión grave (<c>MatchEngine.ResolveInjury</c>) y el
/// perk rival marcado como letal (<c>EffectEngine</c>).
/// </summary>
public sealed class DeathAttributionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Cada lesión resuelta por el camino normal del motor (§2, "Juego sucio") suma
    /// <see cref="MatchPlayer.InjuriesCaused"/> a quien entra, nunca a quien la recibe. Un test con un
    /// único tackler fijo demostraría esto solo por construcción: la víctima nunca entra a nadie, así que
    /// <c>Assert.Equal(0, victim.InjuriesCaused)</c> sería cierto aunque el <c>++</c> estuviera en la línea
    /// equivocada (enmienda de ADR 0124, corrección 5). Aquí los dos jugadores llevan los MISMOS atributos
    /// extremos (fuerza 99, aguante 1) y cada uno entra al otro por turnos: si el contador se sumara al
    /// que recibe en vez de al que entra, la segunda mitad del test lo delataría.
    /// </summary>
    [Fact]
    public void InjuryIsAttributedToWhoeverEntersNeverToWhoeverReceives()
    {
        var extreme = new Attributes(Strength: 99, Speed: 50, Technique: 50, Stamina: 1, Leash: 50);
        var setup = TestMatches.Reference(Catalog, 2);
        var withExtremes = setup with
        {
            Home = setup.Home with
            {
                Players = setup.Home.Players
                    .Select(p => p.Id == setup.Home.Players[0].Id ? p with { Attributes = extreme } : p)
                    .ToList(),
            },
            Away = setup.Away with
            {
                Players = setup.Away.Players
                    .Select(p => p.Id == setup.Away.Players[0].Id ? p with { Attributes = extreme } : p)
                    .ToList(),
            },
        };

        var engine = TestPerks.Engine(Catalog, withExtremes);
        var a = engine.PlayerById(withExtremes.Home.Players[0].Id)!;
        var b = engine.PlayerById(withExtremes.Away.Players[0].Id)!;

        bool aInjuredB = false;
        for (int i = 0; i < 500 && !aInjuredB; i++)
        {
            engine.ProvokeInjury(a, b);
            aInjuredB = b.Injured;
        }

        Assert.True(aInjuredB, "quinientos intentos con fuerza 99 contra aguante 1 y sin lograr ninguna lesión");
        Assert.Equal(1, a.InjuriesCaused);
        Assert.Equal(0, b.InjuriesCaused);

        bool bInjuredA = false;
        for (int i = 0; i < 500 && !bInjuredA; i++)
        {
            engine.ProvokeInjury(b, a);
            bInjuredA = a.Injured;
        }

        Assert.True(bInjuredA, "quinientos intentos con fuerza 99 contra aguante 1 y sin lograr ninguna lesión (sentido inverso)");
        Assert.Equal(1, b.InjuriesCaused);

        // El contador de A no se ha movido por HABER SIDO lesionado en el segundo bloque: sigue en 1.
        Assert.Equal(1, a.InjuriesCaused);
    }

    /// <summary>
    /// RF-093 vía 1: quien saltó al campo con una lesión grave sin tratar y vuelve a lesionarse, muere.
    /// <see cref="MatchEngine.Kill"/> recibe a quien le hizo la segunda lesión (ADR 0124, decisión 2,
    /// primer llamante) y le suma <see cref="MatchPlayer.DeathsCaused"/>.
    /// </summary>
    [Fact]
    public void DeathViaRelapseOnASevereInjuryIsAttributedToTheTackler()
    {
        var setup = TestMatches.Reference(Catalog, 1);
        var withSevereVictim = setup with
        {
            Away = setup.Away with
            {
                Players = setup.Away.Players
                    .Select(p => p.Id == setup.Away.Players[0].Id
                        ? p with { PhysicalState = PhysicalState.SevereInjury, Attributes = p.Attributes with { Stamina = 1 } }
                        : p)
                    .ToList(),
            },
            Home = setup.Home with
            {
                Players = setup.Home.Players
                    .Select(p => p.Id == setup.Home.Players[0].Id
                        ? p with { Attributes = p.Attributes with { Strength = 99 } }
                        : p)
                    .ToList(),
            },
        };

        var engine = TestPerks.Engine(Catalog, withSevereVictim);
        var tackler = engine.PlayerById(withSevereVictim.Home.Players[0].Id)!;
        var victim = engine.PlayerById(withSevereVictim.Away.Players[0].Id)!;

        bool died = false;
        for (int i = 0; i < 500 && !died; i++)
        {
            engine.ProvokeInjury(tackler, victim);
            died = victim.Dead;
        }

        Assert.True(died, "quinientos intentos sobre un titular con lesión grave sin lograr ninguna muerte");
        Assert.Equal(1, tackler.DeathsCaused);
        Assert.Equal(1, engine.Report.Deaths);
    }

    /// <summary>
    /// RF-093 vía 2: un perk rival marcado como letal. <c>EffectEngine</c> llama a <c>Kill</c> con
    /// <c>subscription.Owner</c> (ADR 0124, decisión 2, segundo llamante), así que el portador del perk
    /// suma <see cref="MatchPlayer.DeathsCaused"/> cuando la tirada letal acierta.
    /// </summary>
    [Fact]
    public void DeathViaALethalPerkIsAttributedToItsOwner()
    {
        const string SweepInjury =
            """[{ "type": "modifyProbability", "target": "opposingTeam", "probability": "injury", "value": 30, "duration": "match" }]""";
        var catalog = TestPerks.CatalogWith((
            "butcher",
            TestPerks.Json("butcher", "TACKLE", SweepInjury, rarity: "legendary", kind: "ruleBreaker")
                .Replace("\"lethal\": false", "\"lethal\": true, \"lethalChance\": 10000", StringComparison.Ordinal)));

        var setup = TestPerks.Match(catalog, 1, (1, new[] { "butcher" }));
        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;
        var perk = catalog.Perks.Get("butcher");
        var lethality = catalog.Tuning.Injury.Lethality;

        // La tirada final (Lethality.Chance) compone lethalChance con la proximidad del emparejamiento
        // (ADR 0048): un rival lejano puede quedarse a 0 aunque lethalChance sea el tope. Se elige, como en
        // LethalPerkTests, al rival con más probabilidad, y aun así se repite: no es una certeza.
        var opponentTeam = owner.Team == 0 ? setup.Away : setup.Home;
        var target = opponentTeam.Lineup.Slots
            .Select(slot => engine.PlayerById(slot.PlayerId)!)
            .OrderByDescending(p => Lethality.Chance(
                lethality, perk.LethalChance, owner.BaseAttribute(AttributeKind.Strength),
                p.BaseAttribute(AttributeKind.Stamina), 100,
                Lethality.MatchupAbsolute(p.HomeCell, p.Team, owner.HomeCell, owner.Team)))
            .ThenBy(p => p.Id)
            .First();

        for (int i = 0; i < 200 && !target.Dead; i++)
        {
            engine.Effects!.Publish(new MatchEvent(
                EventType.Tackle, engine.Tick, owner.Team, owner.Id, -1, target.Id,
                owner.HomeCell, Zone.Own, MatchPhase.OpenPlay, engine.BiasFor(0), 0, "attempted"));
        }

        Assert.True(target.Dead, "el rival marcado por el letal (lethalChance 10000) no murió en doscientas entradas");
        Assert.Equal(1, owner.DeathsCaused);
    }
}
