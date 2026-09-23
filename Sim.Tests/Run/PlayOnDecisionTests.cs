using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// La capa que decide el precio de «que siga jugando» (ADR 0134 E), que es distinta de la que lo ejecuta.
/// Los tests del motor (<c>PlayOnTests</c>) prueban que, dado un <see cref="PlayOn"/>, el partido hace lo
/// que dice; estos prueban que <c>/Sim/Run</c> <b>sabe fabricar ese <c>PlayOn</c></b> en todos los casos
/// que la bandeja ofrece. La revisión independiente del paquete encontró que no: la bandeja ofrecía la
/// opción sobre un suplente que ya había entrado y el cálculo del precio lanzaba, porque solo miraba a los
/// titulares. Un test por cada uno de los tres fallos que salieron de ahí.
/// </summary>
public sealed class PlayOnDecisionTests
{
    /// <summary>
    /// El caso que reventaba: un jugador del <b>banquillo</b> —el que entró por una sustitución y después
    /// se lesiona— también tiene precio. <c>SubstitutionPoints.WasOnPitch</c> lo cuenta como que estuvo en
    /// el campo, el motor lo acepta y la bandeja le ofrece quedarse, así que negarle el precio aquí
    /// convertía una opción ofrecida en una excepción a mitad de partido.
    /// </summary>
    [Fact]
    public void ASubstituteOnTheBenchAlsoHasAPrice()
    {
        var (state, _) = TestRuns.WalkToMatch(
            RunEngine.Start(SystemsTestSupport.Setup(Race.Orc), 1, SystemsTestSupport.Catalog, SystemsTestSupport.Systems),
            SystemsTestSupport.Catalog,
            SystemsTestSupport.Systems);

        var built = RunLineup.Build(state, SystemsTestSupport.Catalog);
        Assert.NotEmpty(built.Bench);

        int benchId = built.Bench[0].Id;
        var after = RunLineup.AttributesWithExtraMinorInjuries(state, SystemsTestSupport.Catalog, benchId, 1);

        // Y el precio es el de siempre: lo que daría la campaña con una lesión leve más.
        var expected = (state.GetPlayer(benchId) with { MinorInjuries = state.GetPlayer(benchId).MinorInjuries + 1 })
            .ToDefinition(SystemsTestSupport.Catalog).Attributes;
        Assert.Equal(expected.Strength, after.Strength);
        Assert.Equal(expected.Speed, after.Speed);
        Assert.Equal(expected.Technique, after.Technique);
        Assert.Equal(expected.Stamina, after.Stamina);
        Assert.Equal(expected.Leash, after.Leash);
    }

    /// <summary>
    /// Dos veces en el mismo partido: el precio acumulado es <b>lineal</b> como el de la campaña
    /// (<c>100 − 15·n</c>), no la suma de dos descuentos. Antes el motor sumaba <c>after − base</c> dos
    /// veces y dejaba al jugador en <c>After1 + After2 − base</c>, bastante por debajo de lo prometido, con
    /// la resistencia por los suelos —que es justo el atributo del que depende su siguiente lesión— y con
    /// un tercero se habría salido del rango legal.
    /// </summary>
    [Fact]
    public void TwoDecisionsForTheSamePlayerComposeLikeTwoInjuriesInTheCampaign()
    {
        var (state, _) = TestRuns.WalkToMatch(
            RunEngine.Start(SystemsTestSupport.Setup(Race.Orc), 1, SystemsTestSupport.Catalog, SystemsTestSupport.Systems),
            SystemsTestSupport.Catalog,
            SystemsTestSupport.Systems);

        int id = RunLineup.Effective(state).Lineup.Slots[1].PlayerId;
        var once = RunLineup.AttributesWithExtraMinorInjuries(state, SystemsTestSupport.Catalog, id, 1);
        var twice = RunLineup.AttributesWithExtraMinorInjuries(state, SystemsTestSupport.Catalog, id, 2);

        var player = state.GetPlayer(id);
        var expected = (player with { MinorInjuries = player.MinorInjuries + 2 })
            .ToDefinition(SystemsTestSupport.Catalog).Attributes;

        Assert.Equal(expected.Strength, twice.Strength);
        Assert.Equal(expected.Stamina, twice.Stamina);

        // Y no es «dos veces lo primero»: la composición lineal es más suave que la multiplicativa, así que
        // el segundo escalón nunca baja tanto como el primero salvo empate por truncamiento.
        var baseAttributes = player.ToDefinition(SystemsTestSupport.Catalog).Attributes;
        Assert.True(
            twice.Strength >= (2 * once.Strength) - baseAttributes.Strength,
            $"la segunda lesión compone multiplicativamente: {twice.Strength} contra el mínimo lineal esperado");
    }

    /// <summary>
    /// Pedir el precio de alguien que no juega este partido sigue siendo un error explícito (RT-032), no un
    /// cero silencioso: si un día la bandeja ofrece quedarse a quien no está, hay que enterarse.
    /// </summary>
    [Fact]
    public void SomebodyWhoIsNotPlayingHasNoPrice()
    {
        var (state, _) = TestRuns.WalkToMatch(
            RunEngine.Start(SystemsTestSupport.Setup(Race.Orc), 1, SystemsTestSupport.Catalog, SystemsTestSupport.Systems),
            SystemsTestSupport.Catalog,
            SystemsTestSupport.Systems);

        Assert.Throws<ArgumentException>(
            () => RunLineup.AttributesWithExtraMinorInjuries(state, SystemsTestSupport.Catalog, 9999, 1));
    }
}
