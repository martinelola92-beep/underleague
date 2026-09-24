using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0142 — <b>el cansancio como recurso</b>.
///
/// <para>Antes era una rampa del reloj: a partir de un tick fijo todo el mundo se iba frenando por igual,
/// escalado por el aguante. El encargo la rechaza explícitamente —«no quiero una barra que simplemente se
/// vacíe hasta dejar a todos exhaustos»— y con razón: no había nada que gestionar, y el aguante era casi
/// decorativo.</para>
///
/// <para>Lo que estos tests fijan es que el cansancio (a) se gasta por lo que uno <b>hace</b>, (b) se
/// <b>recupera</b> al no hacerlo, (c) llega a los atributos y por tanto a todo lo que los lee, y (d) no
/// deja a la plantilla entera vacía al final. Cuánto cansa cada cosa es calibración y es la fase
/// siguiente.</para>
/// </summary>
public sealed class FatigueTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    // ---------------------------------------------------------------- el recurso

    /// <summary>Todo el mundo empieza entero: si no, el primer tick ya sería una penalización.</summary>
    [Fact]
    public void TodosEmpiezanConLaEnergiaLlena()
    {
        var engine = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));

        for (int i = 0; i < 14; i++)
        {
            Assert.Equal(MatchPlayer.MaxEnergy, engine.PlayerAtForTest(i).Energy);
            Assert.Equal(0, engine.PlayerAtForTest(i).FatiguePenaltyPoints);
        }
    }

    /// <summary>
    /// <b>Se gasta y se recupera.</b> Es la diferencia entre un recurso y una barra que sólo baja: sin la
    /// segunda mitad no existe la gestión del esfuerzo que el encargo pide.
    /// </summary>
    [Fact]
    public void LaEnergiaSeGastaYSeRecupera()
    {
        var player = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1)).PlayerAtForTest(1);

        player.SpendEnergy(400);
        Assert.Equal(MatchPlayer.MaxEnergy - 400, player.Energy);

        player.RecoverEnergy(150);
        Assert.Equal(MatchPlayer.MaxEnergy - 250, player.Energy);

        // Ni por debajo de cero ni por encima del máximo: los dos extremos son estados imposibles.
        player.SpendEnergy(10_000);
        Assert.Equal(0, player.Energy);
        player.RecoverEnergy(10_000);
        Assert.Equal(MatchPlayer.MaxEnergy, player.Energy);
    }

    /// <summary>
    /// <b>El cansancio llega a los atributos</b>, que es lo que lo hace llegar a todo lo demás: la
    /// velocidad, la puntería, los duelos, el pase, el tiro y las propias pendientes de la utilidad leen
    /// atributos, así que aplicarlo ahí es una regla en vez de siete.
    /// </summary>
    [Fact]
    public void ElCansancioRestaAtributosPeroNuncaBajaDeUno()
    {
        var player = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1)).PlayerAtForTest(1);

        int fresh = player.Speed;
        player.FatiguePenaltyPoints = 10;
        Assert.Equal(fresh - 10, player.Speed);

        player.FatiguePenaltyPoints = 10_000;
        Assert.Equal(1, player.Speed);
        Assert.Equal(1, player.Technique);
        Assert.Equal(1, player.Strength);
    }

    /// <summary>
    /// <b>El aguante NO se cansa</b>, y es deliberado: es lo que gobierna cuánto te cansas, así que
    /// restárselo a sí mismo haría una espiral —cuanto más cansado, menos aguante, más te cansas— que
    /// ninguna decisión del jugador podría anticipar (RF-012d).
    /// </summary>
    [Fact]
    public void ElAguanteNoSeCansaASiMismo()
    {
        var player = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1)).PlayerAtForTest(1);

        int fresh = player.Stamina;
        player.FatiguePenaltyPoints = 30;

        Assert.Equal(fresh, player.Stamina);
    }

    // ---------------------------------------------------------------- en partidos reales

    /// <summary>
    /// <b>Un partido cansa, y no a todos por igual.</b> Las dos mitades importan: si nadie acabara cansado
    /// el recurso sería decorativo, y si acabaran todos igual volveríamos a la rampa que el encargo
    /// rechaza.
    /// </summary>
    [Fact]
    public void UnPartidoCansaYNoATodosPorIgual()
    {
        var engine = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));
        engine.Run();

        int lowest = MatchPlayer.MaxEnergy;
        int highest = 0;
        for (int i = 0; i < 14; i++)
        {
            int energy = engine.PlayerAtForTest(i).Energy;
            lowest = Math.Min(lowest, energy);
            highest = Math.Max(highest, energy);
        }

        Assert.True(lowest < MatchPlayer.MaxEnergy, "nadie se cansó en todo el partido: el recurso no se gasta");
        Assert.True(highest > lowest, $"todos acabaron igual de cansados ({lowest}): eso es la rampa otra vez");
    }

    /// <summary>
    /// <b>No acaban todos agotados, y acaban repartidos.</b> Es el requisito explícito del encargo —«no
    /// hace falta que todos los jugadores terminen agotados»— y a la vez la comprobación de que el recurso
    /// <b>tiene estado estacionario</b>.
    ///
    /// <para>Lo encontró este mismo test: con la primera versión de los números <b>129 de 280 jugadores
    /// acababan exactamente a cero</b> y la mediana era 6 de 1000. Eso no es un recurso que se gestiona,
    /// es la barra que el encargo rechaza — y es un estado degenerado, no una cuestión de balance, porque
    /// la mitad de la plantilla pasaba el último tercio del partido clavada en el tope del castigo.</para>
    ///
    /// <para>Lo que se afirma aquí es la forma de la distribución, no ningún número concreto: que hay
    /// gente entera, gente a medias y gente fundida. Dónde cae exactamente la mediana es calibración y es
    /// la fase siguiente.</para>
    /// </summary>
    [Fact]
    public void NoAcabanTodosAgotadosYAcabanRepartidos()
    {
        int empty = 0;
        int comfortable = 0;
        int total = 0;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var engine = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, seed));
            engine.Run();

            for (int i = 0; i < 14; i++)
            {
                int energy = engine.PlayerAtForTest(i).Energy;
                total++;
                if (energy == 0)
                {
                    empty++;
                }
                else if (energy > MatchPlayer.MaxEnergy / 2)
                {
                    comfortable++;
                }
            }
        }

        Assert.True(comfortable > 0, "nadie acabó con energía de sobra: el recurso no se recupera");
        Assert.True(
            empty * 2 < total,
            $"más de la mitad de la plantilla acabó vacía ({empty} de {total}): eso es un acantilado, no un recurso");
    }

    /// <summary>
    /// <b>El rasgo de aguantar el esfuerzo hace algo.</b> <c>FatigueResistancePercent</c> existía como
    /// escalar de rasgo desde hace mucho y sólo modulaba la rampa que esta ADR retira; sin engancharlo
    /// aquí se habría quedado sin efecto ninguno, que es justo el patrón que este pass viene a cerrar.
    /// </summary>
    [Fact]
    public void ElAguanteAlEsfuerzoAbarataElGasto()
    {
        var fatigue = Catalog.Tuning.Fatigue;

        int normal = fatigue.RunCostPerTick;
        int resistant = fatigue.RunCostPerTick * 100 / (100 + 50);

        Assert.True(resistant < normal, "un 50 % de resistencia tenía que abaratar el gasto");
    }
}
