using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0140 — <b>marcador, minuto y orden táctica</b>.
///
/// <para>La IA ignoraba por completo el resultado: el estado táctico salía sólo de la posesión y no existía
/// «ir a por el empate» ni «defender la ventaja». Y la única decisión táctica del jugador era la
/// alineación. Estos tests fijan las dos reglas que lo cambian y, sobre todo, la que evita que se coman
/// el motor: <b>con orden neutra y empate, la mentalidad no hace absolutamente nada</b>.</para>
/// </summary>
public sealed class MentalityAndUrgencyTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    // ---------------------------------------------------------------- la capa es inerte en neutro

    /// <summary>
    /// <b>Neutral es 100 en todas las acciones</b>, y eso no es calibración: es la definición de la
    /// referencia. Es además lo que garantiza que la capa nueva sea <b>exactamente inerte</b> en un
    /// partido empatado con las dos órdenes neutras —multiplicar por 100 y dividir por 100 en aritmética
    /// entera devuelve el mismo número—, así que todo lo que se mueva en un lote es atribuible a la
    /// urgencia o a la orden, nunca a haber metido la capa.
    /// </summary>
    [Fact]
    public void NeutralEsCienEnTodasLasAcciones()
    {
        foreach (var action in Enum.GetValues<PlayerAction>())
        {
            Assert.Equal(100, Catalog.Ai.Mentality(Mentality.Neutral, action));
        }
    }

    // ---------------------------------------------------------------- la urgencia

    /// <summary>Empatado no hay urgencia ninguna, por mucho que se acabe el tiempo.</summary>
    [Fact]
    public void EmpatadoNoHayUrgencia()
    {
        Assert.Equal(0, MatchEngine.UrgencyPercent(goalDifference: 0, elapsedPercent: 100, perGoalPercent: 60));
    }

    /// <summary>
    /// <b>La urgencia crece con el minuto.</b> El mismo 0-1 no pide lo mismo en el minuto 10 que en el 89,
    /// y ésa es la mitad de la regla que el encargo pedía: «el efecto debe aumentar razonablemente con la
    /// cercanía del final».
    /// </summary>
    [Fact]
    public void LaUrgenciaCreceConElMinuto()
    {
        int pronto = MatchEngine.UrgencyPercent(-1, elapsedPercent: 10, perGoalPercent: 60);
        int tarde = MatchEngine.UrgencyPercent(-1, elapsedPercent: 90, perGoalPercent: 60);

        Assert.True(tarde > pronto, $"la urgencia no crece con el tiempo: {pronto} -> {tarde}");
    }

    /// <summary>
    /// <b>La urgencia crece con el marcador, pero satura.</b> Ir dos abajo aprieta más que ir uno; ir
    /// cuatro abajo no aprieta más que ir tres, porque un equipo no puede atacar «más que con todo».
    /// </summary>
    [Fact]
    public void LaUrgenciaCreceConElMarcadorPeroSatura()
    {
        int uno = MatchEngine.UrgencyPercent(-1, 100, 60);
        int dos = MatchEngine.UrgencyPercent(-2, 100, 60);
        int cuatro = MatchEngine.UrgencyPercent(-4, 100, 60);

        Assert.True(dos > uno, $"ir dos abajo tenía que apretar más que ir uno: {uno} -> {dos}");
        Assert.Equal(dos, cuatro);
    }

    /// <summary>
    /// <b>El signo decide hacia dónde empuja, no cuánto.</b> Ir ganando por uno y ir perdiendo por uno
    /// aprietan lo mismo: lo que cambia es hacia qué mentalidad.
    /// </summary>
    [Fact]
    public void GanarYPerderAprietanLoMismoEnDireccionesContrarias()
    {
        Assert.Equal(
            MatchEngine.UrgencyPercent(-1, 80, 60),
            MatchEngine.UrgencyPercent(1, 80, 60));
    }

    /// <summary>
    /// En un partido de verdad, el que va por detrás tiene la urgencia apuntando a <b>ofensivo</b> y el que
    /// va por delante a <b>defensivo</b>. Es la parte que no se puede afirmar con una función pura: que el
    /// motor lea el marcador que toca y se lo dé al equipo que toca.
    /// </summary>
    [Fact]
    public void ElQueVaPerdiendoApuntaAOfensivoYElQueVaGanandoADefensivo()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var engine = Perks.TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, seed));
            var result = engine.Run();
            if (result.Report.Goals[0] == result.Report.Goals[1])
            {
                continue;
            }

            int leader = result.Report.Goals[0] > result.Report.Goals[1] ? 0 : 1;
            engine.RefreshPerceptionForTest();

            Assert.Equal(Mentality.Defensive, engine.UrgencyTargetForTest(leader));
            Assert.Equal(Mentality.Offensive, engine.UrgencyTargetForTest(1 - leader));
            Assert.True(engine.UrgencyForTest(leader) > 0, "al final de un partido con marcador la urgencia no puede ser cero");
            return;
        }

        Assert.Fail("ninguna de las cuarenta semillas terminó con un ganador");
    }

    // ---------------------------------------------------------------- la orden del jugador

    /// <summary>
    /// <b>La orden del jugador cambia el partido.</b> Mismos equipos, misma semilla, misma todo: sólo
    /// cambia con qué mentalidad sale el equipo local, y el partido que sale es otro.
    ///
    /// <para>No se afirma <i>qué</i> cambia —eso sería calibrar, y este paquete lo tiene prohibido—, sólo
    /// que la orden llega hasta el campo. Si no llegara, sería una preferencia decorativa.</para>
    /// </summary>
    [Fact]
    public void LaOrdenDelJugadorLlegaHastaElCampo()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);

            var attacking = setup with { Home = setup.Home with { Order = Mentality.Offensive } };
            var defending = setup with { Home = setup.Home with { Order = Mentality.Defensive } };

            var a = Simulator.Run(attacking, seed, Catalog, SimConfig.Default).Report;
            var d = Simulator.Run(defending, seed, Catalog, SimConfig.Default).Report;

            bool different = a.Shots[0] != d.Shots[0]
                || a.Goals[0] != d.Goals[0]
                || a.Goals[1] != d.Goals[1]
                || a.PossessionChanges != d.PossessionChanges;

            if (different)
            {
                return;
            }
        }

        Assert.Fail("salir ofensivo o defensivo no cambió nada en veinte partidos: la orden no llega al campo");
    }
}
