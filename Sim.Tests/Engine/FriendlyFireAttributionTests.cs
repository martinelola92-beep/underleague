using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BE-B (docs/pendientes/BE-B.md): <b>lesionar o matar a alguien de tu propio equipo no te lo acredita.</b>
///
/// <para><b>Por qué hace falta el test y no basta el <c>if</c>.</b> Ningún dato de <c>/data</c> usa hoy esa
/// combinación, así que los dos filtros nuevos son inertes y se podrían borrar sin que ninguno de los
/// 1.135 tests se enterara — que es exactamente lo que señaló la revisión independiente. Estos dos casos
/// son los únicos que los ejercitan.</para>
///
/// <para><b>Por qué importa aunque hoy no se dispare.</b> <c>PerkLoader</c> admite <c>injure</c> con
/// objetivos <c>actor</c> y <c>target</c>, y ninguno de los dos es necesariamente un rival: en un
/// disparador de pase, <c>target</c> resuelve al <b>compañero receptor</b>. Desde la ADR 0124 lo que se
/// acredita <b>se guarda</b>, y RF-125 pone un logro encima («provocar 30 lesiones en una sola run
/// desbloquea orcos»), así que una lesión mal acreditada dejaría de ser un detalle interno para
/// desbloquear contenido lesionando a los tuyos.</para>
///
/// <para>Lo que NO se comprueba aquí, porque es deliberado: la lesión <b>ocurre</b> igual. Se filtra al
/// acreditar y no al resolver, para que un perk de daño amigo siga siendo diseñable sin contaminar el
/// contador.</para>
/// </summary>
public sealed class FriendlyFireAttributionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void InjuringATeammateDoesNotCreditTheInstigator()
    {
        var engine = TestPerks.Engine(Catalog, Brutal());
        var attacker = engine.PlayerById(0)!;
        var teammate = engine.PlayerById(1)!;
        Assert.Equal(attacker.Team, teammate.Team);

        for (int i = 0; i < 500 && !teammate.Injured; i++)
        {
            engine.ProvokeInjury(attacker, teammate);
        }

        Assert.True(teammate.Injured, "el montaje no llegó a lesionar al compañero: el test no mide nada");
        Assert.Equal(0, attacker.InjuriesCaused);
    }

    [Fact]
    public void InjuringYourselfDoesNotCreditYou()
    {
        var engine = TestPerks.Engine(Catalog, Brutal());
        var victim = engine.PlayerById(1)!;

        for (int i = 0; i < 500 && !victim.Injured; i++)
        {
            engine.ProvokeInjury(victim, victim);
        }

        Assert.True(victim.Injured, "el montaje no llegó a lesionar a la víctima: el test no mide nada");
        Assert.Equal(0, victim.InjuriesCaused);
    }

    [Fact]
    public void KillingATeammateDoesNotCreditTheKiller()
    {
        var engine = TestPerks.Engine(Catalog, Brutal());
        var killer = engine.PlayerById(0)!;
        var teammate = engine.PlayerById(1)!;
        Assert.Equal(killer.Team, teammate.Team);

        engine.Kill(teammate, "test", killer);

        Assert.True(teammate.Dead, "el montaje no llegó a matar al compañero: el test no mide nada");
        Assert.Equal(0, killer.DeathsCaused);
    }

    /// <summary>Un rival sí se acredita: el filtro no puede haberse comido también el caso legítimo.</summary>
    [Fact]
    public void KillingAnOpponentStillCreditsTheKiller()
    {
        var setup = Brutal();
        var engine = TestPerks.Engine(Catalog, setup);
        var killer = engine.PlayerById(setup.Home.Players[0].Id)!;
        var opponent = engine.PlayerById(setup.Away.Players[0].Id)!;
        Assert.NotEqual(killer.Team, opponent.Team);

        engine.Kill(opponent, "test", killer);

        Assert.Equal(1, killer.DeathsCaused);
    }

    /// <summary>Frágiles contra brutales: la lesión sale rápido y el test no depende de la suerte.</summary>
    private static MatchSetup Brutal() => TestMatches.Brutal(Catalog);
}
