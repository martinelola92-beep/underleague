using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Condiciones NCalc (RT-034): se compilan una vez al cargar, la gramática es cerrada y cualquier
/// identificador, función o tipo incorrecto es error de carga, nunca de partido.
/// </summary>
public sealed class ConditionTests
{
    private const string Effect = """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 5, "duration": "match" }]""";

    [Fact]
    public void EmptyConditionIsAlwaysTrue()
    {
        var perk = TestPerks.Load("always", TestPerks.Json("always", "MATCH_START", Effect));
        Assert.True(perk.CompiledCondition.IsAlwaysTrue);
        Assert.Equal(string.Empty, perk.Condition);
    }

    [Theory]
    [InlineData("hasTag(actor, 'Brute') && bias() < 0")]
    [InlineData("!isMob()")]
    [InlineData("adjacentCount(owner, 'Defender') >= 1")]
    [InlineData("teammatesWithTag(owner, 'Fine') > 2 || level(owner) == 3")]
    [InlineData("zone(actor) == 'Own'")]
    [InlineData("position(owner) != 'Goalkeeper'")]
    [InlineData("detail() == 'severe'")]
    [InlineData("attr(actor, 'strength') > 40 && counter('matches') <= 8")]
    [InlineData("distanceToGoal(actor) < 3 && scoreDiff() < 0 && tick() > 600")]
    [InlineData("adjacent(owner, 'Fine')")]
    [InlineData("startsIn(owner, 'OwnThird')")]
    [InlineData("startsOn(owner, 'RightFlank') && !startsIn(owner, 'Middle')")]
    [InlineData("linked(owner, 'diagonalBehind')")]
    [InlineData("nearAlly(owner, 'Defender', 2) || nearOpponent(actor, 'Forward', 3)")]
    [InlineData("stat(owner, 'goals') >= 1 && stat(actor, 'saves') < 3")]
    public void GrammarAcceptsEveryDocumentedForm(string condition)
    {
        var perk = TestPerks.Load("ok", TestPerks.Json("ok", "TACKLE", Effect, condition: condition));
        Assert.Equal(condition, perk.Condition);
        Assert.False(perk.CompiledCondition.IsAlwaysTrue);
    }

    [Theory]
    [InlineData("hasTag(shooter, 'Brute')", "identificador desconocido")]
    [InlineData("isLucky()", "función desconocida")]
    [InlineData("bias()", "devuelve")]
    [InlineData("attr(actor, 'charisma') > 3", "atributo desconocido")]
    [InlineData("bias() < 'zero'", "entero literal")]
    [InlineData("0 < bias()", "a la izquierda")]
    [InlineData("bias() + 1 > 0", "a la izquierda")]
    [InlineData("tick()", "devuelve")]
    [InlineData("zone(actor) > 'Own'", "'==' y '!='")]
    [InlineData("hasTag(actor, 'Brute') == 1", "no se compara")]
    [InlineData("hasTag(actor)", "argumento")]
    [InlineData("hasTag('actor', 'Brute')", "identificador")]
    [InlineData("startsIn(owner, 'Somewhere')", "tercio de inicio desconocida")]
    [InlineData("startsOn(owner, 'Wing')", "banda de inicio desconocida")]
    [InlineData("linked(owner, 'diagonal')", "relación de vínculo desconocida")]
    [InlineData("stat(owner, 'assists') > 0", "estadística desconocida")]
    [InlineData("nearAlly(owner, 'Fine', 0)", "radio de 1 a 8")]
    [InlineData("nearAlly(owner, 'Fine', 99)", "radio de 1 a 8")]
    [InlineData("nearOpponent(owner, 'Fine', 'two')", "entero literal")]
    [InlineData("nearAlly(owner, 'Fine')", "argumento")]
    public void InvalidConditionIsALoadError(string condition, string expected)
    {
        var ex = Assert.Throws<DataException>(
            () => TestPerks.Load("bad", TestPerks.Json("bad", "TACKLE", Effect, condition: condition)));
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.Equal("perks/bad.json", ex.File);
    }

    [Fact]
    public void HasTagOnTheActorDecidesWhetherThePerkFires()
    {
        // El titular 1 es defensa humano: tiene la etiqueta Neutral, no Brute.
        var withNeutral = Fires("hasTag(actor, 'Neutral')");
        var withBrute = Fires("hasTag(actor, 'Brute')");
        Assert.True(withNeutral);
        Assert.False(withBrute);
    }

    [Fact]
    public void AbsentTargetMakesHasTagFalse()
    {
        // El evento de prueba no trae Target: hasTag(target, ...) es falso, no una excepción (§2).
        Assert.False(Fires("hasTag(target, 'Neutral')"));
    }

    [Fact]
    public void BiasIsSeenFromTheOwnerTeam()
    {
        // Criterio +20 favorece al local (RF-060): para un perk de un jugador local bias() es +20.
        Assert.True(Fires("bias() > 10", bias: 20));
        Assert.False(Fires("bias() > 10", bias: -20));
    }

    [Fact]
    public void CounterStartsAtZeroForNonAccumulatingPerks()
    {
        Assert.True(Fires("counter('matches') == 0"));
    }

    [Fact]
    public void TickAndScoreDiffComeFromTheMatch()
    {
        Assert.True(Fires("tick() == 0 && scoreDiff() == 0"));
    }

    /// <summary>
    /// Zona de inicio (§1.5): se lee de la casilla-hogar, no de dónde está el jugador. El titular 1 es un
    /// defensa local, así que empieza en su propio tercio y en una banda, nunca en el carril central.
    /// </summary>
    [Fact]
    public void StartZoneAndFlankComeFromTheHomeCell()
    {
        Assert.True(Fires("startsIn(owner, 'OwnThird')"));
        Assert.False(Fires("startsIn(owner, 'AttackingThird')"));
        Assert.False(Fires("startsIn(owner, 'Middle')"));
        Assert.False(Fires("startsOn(owner, 'Center')"));
    }

    /// <summary>
    /// Proximidad dinámica (ADR 0021): se mide sobre la posición **real** en el momento del evento. En el
    /// saque inicial cada uno está en su casilla-hogar, así que el portero propio está cerca y el rival no.
    /// </summary>
    [Fact]
    public void ProximityIsMeasuredOnTheRealPositions()
    {
        Assert.True(Fires("nearAlly(owner, 'Goalkeeper', 8)"));
        Assert.False(Fires("nearAlly(owner, 'Goalkeeper', 1)"));
        Assert.False(Fires("nearOpponent(owner, 'Goalkeeper', 8)"));
        Assert.True(Fires("nearOpponent(owner, 'Forward', 8)"));
    }

    /// <summary>
    /// <c>stat</c> expone lo que el motor ya lleva para el informe post-partido (RF-119): un perk de
    /// acumulación ya no tiene que declarar su propio contador para leer sus goles o sus entradas.
    /// </summary>
    [Fact]
    public void StatReadsTheRunningMatchStatistics()
    {
        Assert.True(Fires("stat(owner, 'goals') == 0"));
        Assert.True(Fires("stat(owner, 'passesCompleted') < 1"));
        Assert.True(Fires("stat(owner, 'saves') == 0"));
        Assert.False(Fires("stat(owner, 'tacklesWon') > 0"));
    }

    /// <summary>
    /// Publica un TACKLE del jugador 1 (que lleva el perk) y dice si el perk se activó. Es la vía de
    /// evaluación real: la condición se ejerce a través del motor de efectos, no de un contexto simulado.
    /// </summary>
    private static bool Fires(string condition, int bias = 0)
    {
        var catalog = TestPerks.CatalogWith(("probe", TestPerks.Json("probe", "TACKLE", Effect, condition: condition)));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "probe" }));
        setup = setup with { Referee = setup.Referee with { InitialBias = bias } };

        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;
        engine.Effects!.Publish(new MatchEvent(
            EventType.Tackle, 0, owner.Team, owner.Id, -1, -1,
            owner.HomeCell, Zone.Own, MatchPhase.OpenPlay, bias, 0, "attempted"));

        return engine.Report.PerkActivations.Any(a => a.PerkId == "probe");
    }
}
