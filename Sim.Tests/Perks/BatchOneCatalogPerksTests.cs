using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Los trece perks de la tanda 1 del catálogo unificado (docs/analisis/perks-catalogo-unificado.md §8.1):
/// existen en el catálogo <b>real</b> de <c>data/perks/</c> —a diferencia del resto de
/// <c>Sim.Tests/Perks</c>, que declara perks de usar y tirar sobre <see cref="TestPerks.CatalogWith"/>—,
/// cargan sin error y generan descripción no vacía en español e inglés (RT-035). No repite lo que ya
/// prueban <see cref="FourPrimitivesTests"/> (que las primitivas HACEN lo que dicen) ni
/// <see cref="PerkLoaderTests"/> (que el cargador valida el formato de cualquier perk): esto comprueba que
/// el catálogo de datos, y no un ejemplo de test, usa esas primitivas correctamente.
/// </summary>
public sealed class BatchOneCatalogPerksTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    public static IEnumerable<object[]> Ids => new[]
    {
        "low_block", "line_keeper", "diver", "half_leg", "earthquake", "dirty_play",
        "last_man", "hand_of_god", "no_dying", "tough_hide", "local_idol", "box_office", "ad_machine",
    }.Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(Ids))]
    public void ExistsInTheCatalogAndDescribesInBothLanguages(string id)
    {
        var perk = Catalog.Perks.Find(id);
        Assert.NotNull(perk);

        string es = DescriptionGenerator.Describe(perk!, "es", Catalog);
        string en = DescriptionGenerator.Describe(perk!, "en", Catalog);

        Assert.False(string.IsNullOrWhiteSpace(es));
        Assert.False(string.IsNullOrWhiteSpace(en));
        Assert.NotEqual(es, en);
    }

    [Fact]
    public void OpponentScopeReadsTheOtherPlayerInvolvedInTheEvent()
    {
        // ad_machine (INJURY) y diver (FOUL) son los dos perks de esta tanda que usan el scope 'opponent'
        // nuevo (PerkScope.Opponent): el dueño es el rival IMPLICADO del evento, no su actor. En INJURY el
        // Actor es la víctima y el causante viaja en Opponent (MatchEngine.ResolveInjury); en FOUL el Actor
        // es quien entra y el fouleado viaja en Opponent (MatchEngine.ResolveFoul/WhistleOrLetPlay), así
        // que 'opponent' -y no 'target', que FOUL nunca rellena- es lo que hace que diver se dispare para
        // el jugador fouleado, no para el que comete la falta.
        Assert.Equal(PerkScope.Opponent, Catalog.Perks.Get("ad_machine").Scope);
        Assert.Equal(PerkScope.Opponent, Catalog.Perks.Get("diver").Scope);
    }

    [Fact]
    public void PositionRestrictedPerksMatchTheDesignTable()
    {
        Assert.Equal(Position.Goalkeeper, Catalog.Perks.Get("line_keeper").PositionOnly);
        Assert.Equal(Position.Goalkeeper, Catalog.Perks.Get("hand_of_god").PositionOnly);
        Assert.Equal(Position.Defender, Catalog.Perks.Get("last_man").PositionOnly);
        Assert.Equal(Position.Forward, Catalog.Perks.Get("local_idol").PositionOnly);
    }

    [Fact]
    public void NegativeLeashPerksExpressIdentityNotADrawback()
    {
        // Decision PD-1: low_block y line_keeper son los dos primeros perks con modifyLeash NEGATIVO en su
        // efecto PRINCIPAL (no en elseEffects): un perk puede penalizar si expresa una identidad táctica
        // real. low_block penaliza a todo el EQUIPO (bloque bajo); line_keeper solo al PORTADOR.
        var lowBlock = Catalog.Perks.Get("low_block");
        var lineKeeper = Catalog.Perks.Get("line_keeper");

        Assert.Equal(EffectType.ModifyLeash, lowBlock.Effects[0].Type);
        Assert.Equal(-2, lowBlock.Effects[0].Value);
        Assert.Equal(EffectTarget.Team, lowBlock.Effects[0].Target);

        Assert.Equal(EffectType.ModifyLeash, lineKeeper.Effects[0].Type);
        Assert.Equal(-2, lineKeeper.Effects[0].Value);
        Assert.Equal(EffectTarget.Owner, lineKeeper.Effects[0].Target);
    }

    [Fact]
    public void NoDyingLimitIsPerMatchBecauseTheEngineDoesNotPersistUsesAcrossMatches()
    {
        // El diseño pedía "1 por run si el cargador lo admite". El esquema acepta LimitScope.Run (existe
        // como valor), pero EffectEngine reinicia PerkSubscription.Uses en cada partido nuevo: no hay, para
        // el contador de USOS de un límite, un equivalente a EffectEngine.SeedCounters (que sí persiste los
        // CONTADORES declarados con accumulatesAcrossMatches). Declarar 'run' aquí prometería una garantía
        // -una muerte evitada por RUN completa- que el motor de hoy no cumple: se comportaría exactamente
        // igual que 'match', pero la descripción generada diría "una vez en la run" mintiendo sobre lo que
        // pasa en el segundo partido. Se deja en 'match', que es lo que el cargador admite de verdad hoy
        // (ver _doc de no_dying.json e informe al revisor).
        var limit = Catalog.Perks.Get("no_dying").Limit;

        Assert.NotNull(limit);
        Assert.Equal(LimitScope.Match, limit!.Per);
        Assert.Equal(1, limit.Times);
    }

    [Fact]
    public void AccumulationCountersHaveAGoldRateDeclared()
    {
        // ADR 0113: el oro de un perk es una inversión, no un premio. Los tres perks de eje de acumulación
        // de esta tanda declaran accumulatesAcrossMatches, así que sus contadores llegan a
        // data/economy/counter-gold.json (Sim.Run.Systems.Economy.CounterGoldTable).
        Assert.True(Catalog.Perks.Get("local_idol").AccumulatesAcrossMatches);
        Assert.True(Catalog.Perks.Get("box_office").AccumulatesAcrossMatches);
        Assert.True(Catalog.Perks.Get("ad_machine").AccumulatesAcrossMatches);

        var files = TestData.LoadAllFiles();
        var table = Underleague.Sim.Run.Systems.Economy.CounterGoldTable.FromJson(files);
        Assert.True(table.RateFor("localIdolGoals") > 0);
        Assert.True(table.RateFor("boxOfficeMatches") > 0);
        Assert.True(table.RateFor("adMachineInjuries") > 0);
    }
}
