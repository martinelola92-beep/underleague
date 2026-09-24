using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// ADR 0146 — <b><c>modifyUtility</c> deja de ser inalcanzable</b>.
///
/// <para>Era el hallazgo más incómodo de la auditoría de identidad: el <b>único canal del motor que cambia
/// lo que un jugador QUIERE hacer</b> —en vez de si le sale— estaba implementado, pilotado y medido con
/// RT-056 en verde… y <b>no estaba en el enum del esquema</b>, así que ningún dato de <c>/data</c> podía
/// usarlo. Un sistema construido y desconectado.</para>
///
/// <para>Estos tests demuestran las dos mitades: que un perk escrito en JSON <b>carga</b> con ese efecto, y
/// que el cargador <b>rechaza</b> el caso que el plan nombraba como error de datos.</para>
/// </summary>
public sealed class ModifyUtilityReachableTests
{
    /// <summary>Un perk que empuja a tirar, escrito como lo escribiría un diseñador.</summary>
    private static readonly string ShootMore = TestPerks.Json(
        "test_shoot_more",
        "RECOVERY",
        """[ { "type": "modifyUtility", "target": "actor", "utilityAction": "Shoot", "value": 40, "duration": "match" } ]""");

    /// <summary>El mismo, acotado al último tercio: la cláusula de zona de C2.</summary>
    private static readonly string ShootMoreUpFront = TestPerks.Json(
        "test_shoot_more_up_front",
        "RECOVERY",
        """[ { "type": "modifyUtility", "target": "actor", "utilityAction": "Shoot", "utilityZone": "Opposing", "value": 40, "duration": "match" } ]""");

    /// <summary>
    /// <b>Un perk con <c>modifyUtility</c> carga.</b> Es la afirmación entera del paquete: el canal existía
    /// y el dato no podía nombrarlo.
    /// </summary>
    [Fact]
    public void UnPerkConModifyUtilityCarga()
    {
        var catalog = TestPerks.CatalogWith(("test_shoot_more", ShootMore));
        var perk = catalog.Perks.All.Single(p => p.Id == "test_shoot_more");

        var effect = Assert.Single(perk.Effects);
        Assert.Equal(Underleague.Sim.Perks.EffectType.ModifyUtility, effect.Type);
        Assert.Equal(PlayerAction.Shoot, effect.UtilityAction);
        Assert.Null(effect.UtilityZone);
    }

    /// <summary>
    /// <b>Y con la cláusula de tercio también</b>, que es la variante con la que se pilotó la primitiva:
    /// «quiere tirar, pero sólo cuando está arriba».
    /// </summary>
    [Fact]
    public void LaClausulaDeTercioSeCarga()
    {
        var catalog = TestPerks.CatalogWith(("test_shoot_more_up_front", ShootMoreUpFront));
        var perk = catalog.Perks.All.Single(p => p.Id == "test_shoot_more_up_front");

        Assert.Equal(Zone.Opposing, Assert.Single(perk.Effects).UtilityZone);
    }

    /// <summary>
    /// <b>Un bono de intención sin decir a qué intención es un error de datos</b>, no un perk que no hace
    /// nada. Es RT-032 aplicado al canal nuevo: en <c>/data</c>, lo que no puede funcionar tiene que
    /// fallar al cargar.
    /// </summary>
    [Fact]
    public void SinAccionEsErrorDeDatos()
    {
        string incomplete = TestPerks.Json(
            "test_no_action",
            "RECOVERY",
            """[ { "type": "modifyUtility", "target": "actor", "value": 40, "duration": "match" } ]""");

        var error = Assert.Throws<DataException>(
            () => TestPerks.CatalogWith(("test_no_action", incomplete)));

        Assert.Contains("utilityAction", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Y el techo C10: un bono a una acción que ese puesto no puede elegir nunca.</b> El plan lo
    /// nombraba como «un ×3 a <c>Shoot</c> en un portero tiene que ser un error de datos», y sale sin
    /// ninguna regla especial: el peso base de <c>Shoot</c> para el portero es cero, y un porcentaje de
    /// cero es cero.
    ///
    /// <para>Se comprueba contra la tabla real de pesos y no contra una lista escrita a mano, que es lo
    /// que hace que la regla siga valiendo cuando la tabla cambie.</para>
    /// </summary>
    [Fact]
    public void UnBonoAUnaAccionImposibleParaEsePuestoEsErrorDeDatos()
    {
        var catalog = TestData.LoadCatalog();
        Assert.Equal(0, catalog.Ai.Base(Position.Goalkeeper, PlayerAction.Shoot));

        string keeperShooter = TestPerks.Json(
            "test_keeper_shooter",
            "RECOVERY",
            """[ { "type": "modifyUtility", "target": "actor", "utilityAction": "Shoot", "value": 200, "duration": "match" } ]""",
            positionOnly: "Goalkeeper");

        var error = Assert.Throws<DataException>(
            () => TestPerks.CatalogWith(("test_keeper_shooter", keeperShooter)));

        Assert.Contains("peso base", error.Message, StringComparison.Ordinal);
    }
}
