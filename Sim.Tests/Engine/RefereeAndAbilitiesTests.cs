using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Los tres enchufes de habilidad racial que el paquete S dejó abiertos y probados en el motor de perks
/// pero sin conectar al motor de partido (§5.13, §5.10), y el criterio del árbitro adelantado de la
/// fase 3 (RF-062..RF-064, ADR 0030 §3).
/// </summary>
public sealed class RefereeAndAbilitiesTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Sangre caliente (orcos, ADR 0026): el derribo que provoca un orco dura más. Se mide en el punto
    /// exacto en el que el motor lo usa —la duración del estado <c>KnockedDown</c> que aplica al rival—,
    /// antes y después de que la habilidad se active, para que lo único que cambie entre las dos lecturas
    /// sea la habilidad.
    /// </summary>
    [Fact]
    public void AnOrcLeavesTheRivalDownForLonger()
    {
        var engine = Engine(Race.Orc);
        var orc = engine.PlayerById(1)!;
        int baseTicks = Catalog.Tuning.States.KnockedDownTicks;

        Assert.Equal(baseTicks, engine.KnockdownTicksCausedBy(orc, baseTicks));

        engine.Effects!.Publish(MatchStart(engine));

        Assert.Equal(baseTicks + 5, engine.KnockdownTicksCausedBy(orc, baseTicks));
    }

    /// <summary>
    /// Un humano no tiene Sangre caliente: su derribo dura lo que dice <c>tuning.states</c>. Es la mitad
    /// que faltaba de la comprobación anterior —que el canal esté conectado, no que sume siempre—.
    /// </summary>
    [Fact]
    public void AHumanKnocksDownForTheStandardTime()
    {
        var engine = Engine(Race.Human);
        var human = engine.PlayerById(1)!;
        int baseTicks = Catalog.Tuning.States.KnockedDownTicks;

        engine.Effects!.Publish(MatchStart(engine));

        Assert.Equal(baseTicks, engine.KnockdownTicksCausedBy(human, baseTicks));
    }

    /// <summary>
    /// Toque (elfos, ADR 0026): el elfo esquiva mejor en los dos canales que abrió el paquete S. Se mide
    /// sobre las dos probabilidades reales del motor —la de que le roben el balón en una entrada y la de
    /// que le corten el pase—, antes y después de activar la habilidad.
    /// </summary>
    [Fact]
    public void AnElfEvadesTacklesAndInterceptionsBetter()
    {
        var engine = Engine(Race.Elf);
        var elf = engine.PlayerById(1)!;
        var opponent = engine.PlayerById(101)!;

        int tackleBefore = engine.TackleWinChance(opponent, elf);
        int interceptBefore = engine.InterceptChance(opponent, elf);

        engine.Effects!.Publish(MatchStart(engine));

        // 10 puntos porcentuales del perk, en la base 10.000 del motor (§5.2).
        Assert.Equal(tackleBefore - 1000, engine.TackleWinChance(opponent, elf));
        Assert.Equal(interceptBefore - 1000, engine.InterceptChance(opponent, elf));
    }

    /// <summary>
    /// El hueco de §5.10: un equipo <b>sin ningún perk asignado</b> tiene que recibir igualmente su
    /// habilidad racial, porque no ocupa slot y no está en <c>Definition.Perks</c>. Se comprueba de punta
    /// a punta, sobre el informe post-partido: catorce titulares orcos, cero perks, catorce activaciones
    /// de Sangre caliente.
    /// </summary>
    [Fact]
    public void ATeamWithNoPerksStillGetsItsRacialAbility()
    {
        var setup = RaceMatch(Race.Orc, seed: 11);
        Assert.All(setup.Home.Players, p => Assert.Empty(p.Perks));
        Assert.All(setup.Away.Players, p => Assert.Empty(p.Perks));

        var result = Simulator.Run(setup, 11, Catalog, new SimConfig(CollectLog: false));

        var summaries = result.Report.PerksSummary.Where(p => p.PerkId == "hot_blooded").ToList();
        Assert.Equal(14, summaries.Sum(p => p.Activations));
    }

    /// <summary>
    /// RF-063: una acción sucia desplaza el criterio en contra del que la comete <b>aunque no se
    /// señale</b>. Se aísla apagando en los datos <b>todo</b> lo que el árbitro sí castiga —las dos
    /// tiradas de falta, sus dos bonos de rasgo y el efecto del propio criterio sobre ellas— y toda la
    /// cadena de lesión, y dejando correr el emparejamiento más violento que hay. El resultado es un
    /// partido con bloqueos y sin una sola falta ni lesión señalada: si el criterio se ha movido, solo
    /// pueden haberlo movido las acciones sucias que el árbitro no pitó.
    /// </summary>
    [Fact]
    public void AnUnwhistledDirtyActionStillMovesTheReferee()
    {
        var tuning = Catalog.Tuning;
        var silentReferee = CatalogWith(
            ("sim/tuning.json", $"\"biasFoulShiftPer10\": {tuning.Referee.BiasFoulShiftPer10}", "\"biasFoulShiftPer10\": 0"),
            ("sim/tuning.json", $"\"foulBase\": {tuning.Block.FoulBase}", "\"foulBase\": 0"),
            ("sim/tuning.json", $"\"foulBase\": {tuning.Tackle.FoulBase}", "\"foulBase\": 0"),
            ("sim/tuning.json", $"\"foulStrengthFactor\": {tuning.Tackle.FoulStrengthFactor}", "\"foulStrengthFactor\": 0"),
            ("sim/tuning.json", $"\"onTackleBase\": {tuning.Injury.OnTackleBase}", "\"onTackleBase\": 0"),
            ("sim/tuning.json", $"\"onFoulBase\": {tuning.Injury.OnFoulBase}", "\"onFoulBase\": 0"),
            ("sim/tuning.json", $"\"attackerStrengthFactor\": {tuning.Injury.AttackerStrengthFactor}", "\"attackerStrengthFactor\": 0"),
            ("sim/tuning.json", $"\"victimStaminaResistFactor\": {tuning.Injury.VictimStaminaResistFactor}", "\"victimStaminaResistFactor\": 0"),
            ("traits/traits.json", "\"hardTackleBonus\": 15", "\"hardTackleBonus\": 0"),
            ("traits/traits.json", "\"foulChanceBonus\": 15", "\"foulChanceBonus\": 0"),
            ("traits/traits.json", "\"injuryChanceBonus\": 10", "\"injuryChanceBonus\": 0"));

        var result = Simulator.Run(TestMatches.Brutal(silentReferee), 4, silentReferee, new SimConfig(CollectLog: false));

        Assert.Equal(0, result.Report.Fouls);
        Assert.Equal(0, result.Report.Injuries);
        Assert.True(result.Report.Blocks > 0, "el escenario tenía que producir bloqueos sin balón");
        Assert.True(
            result.Report.FinalBias != 0,
            "el criterio debía haberse movido con las acciones sucias no señaladas (RF-063)");
    }

    /// <summary>
    /// El criterio evoluciona <b>durante</b> el partido, no solo al final (RF-062): cada evento de la
    /// secuencia lleva el criterio vigente en su tick, así que el recorrido es visible y el informe
    /// termina en el último valor de ese recorrido.
    /// </summary>
    [Fact]
    public void TheRefereeCriterionMovesDuringTheMatchAndIsVisibleInTheReport()
    {
        var result = Simulator.Run(TestMatches.Brutal(Catalog), 4, Catalog, new SimConfig(CollectLog: false));

        var distinct = result.Events.Select(e => e.Bias).Distinct().ToList();
        Assert.True(distinct.Count > 1, "el criterio tenía que tomar más de un valor a lo largo del partido");
        Assert.Equal(result.Events[^1].Bias, result.Report.FinalBias);
    }

    /// <summary>
    /// RF-062: el criterio no se sale nunca de -100..+100, por muchas acciones sucias que se acumulen. El
    /// emparejamiento brutal es el peor caso posible y el tope tiene que aguantarlo.
    /// </summary>
    [Fact]
    public void TheCriterionStaysWithinItsRange()
    {
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var result = Simulator.Run(TestMatches.Brutal(Catalog), seed, Catalog, new SimConfig(CollectLog: false));
            Assert.InRange(result.Report.FinalBias, -100, 100);
            Assert.All(result.Events, e => Assert.InRange(e.Bias, -100, 100));
        }
    }

    /// <summary>
    /// RF-064: el criterio es un efecto real sobre la simulación, no un adorno. Con el mismo partido y la
    /// misma semilla, un árbitro que arranca con criterio muy favorable al local y otro muy desfavorable
    /// producen partidos distintos.
    /// </summary>
    [Fact]
    public void TheInitialCriterionChangesTheMatch()
    {
        // Se mide sobre treinta semillas y no sobre una (paquete U): el criterio desplaza el umbral de
        // las tiradas, así que en un partido concreto puede no llegar a voltear ninguna y las dos
        // secuencias de eventos salir idénticas. Lo que RF-064 exige es el efecto, no que cada partido
        // suelto cambie: con el árbitro a favor, el local comete MENOS faltas señaladas que con el
        // árbitro en contra, y alguna de las treinta secuencias tiene que ser distinta.
        const int Seeds = 30;
        var setup = TestMatches.Brutal(Catalog);
        var friendly = setup with { Referee = setup.Referee with { InitialBias = 80 } };
        var hostile = setup with { Referee = setup.Referee with { InitialBias = -80 } };

        int friendlyHomeFouls = 0;
        int hostileHomeFouls = 0;
        bool anySequenceDiffers = false;

        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var withFriendly = Simulator.Run(friendly, seed, Catalog, new SimConfig(CollectLog: false));
            var withHostile = Simulator.Run(hostile, seed, Catalog, new SimConfig(CollectLog: false));

            Assert.NotEqual(withFriendly.Report.FinalBias, withHostile.Report.FinalBias);

            friendlyHomeFouls += withFriendly.Report.Players.Where(p => p.Team == 0).Sum(p => p.Fouls);
            hostileHomeFouls += withHostile.Report.Players.Where(p => p.Team == 0).Sum(p => p.Fouls);

            anySequenceDiffers = anySequenceDiffers
                || !withFriendly.Events.Select(e => (e.Type, e.Tick, e.Detail))
                    .SequenceEqual(withHostile.Events.Select(e => (e.Type, e.Tick, e.Detail)));
        }

        Assert.True(anySequenceDiffers, "el criterio inicial no cambió ningún partido de los treinta");
        Assert.True(
            friendlyHomeFouls < hostileHomeFouls,
            $"faltas del local con árbitro a favor {friendlyHomeFouls} frente a en contra {hostileHomeFouls}");
    }

    // ------------------------------------------------------------------ ayudantes

    /// <summary>Motor de partido con los dos equipos de la misma raza y sin ningún perk asignado.</summary>
    private static MatchEngine Engine(Race race) =>
        new(RaceMatch(race, seed: 3), 3, Catalog, SimConfig.Default);

    private static MatchSetup RaceMatch(Race race, ulong seed)
    {
        var homeRng = RngStreams.Generation(seed, 0);
        var awayRng = RngStreams.Generation(seed, 1);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", race, 50, 0);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", race, 50, 100);
        return new MatchSetup(home, away, new RefereeSetup("Neutral", RefereeTrait.Neutral, 0));
    }

    /// <summary>El catálogo real con unas sustituciones de texto sobre los ficheros de <c>/data</c>.</summary>
    private static Catalog CatalogWith(params (string File, string From, string To)[] replacements)
    {
        var files = TestData.LoadAllFiles();
        foreach (var (file, from, to) in replacements)
        {
            // "o ya está puesto": dos secciones distintas pueden compartir el mismo valor de partida y
            // la primera sustitución deja a la segunda sin nada que hacer.
            Assert.True(
                files[file].Contains(from, StringComparison.Ordinal) || files[file].Contains(to, StringComparison.Ordinal),
                $"{file} no contiene '{from}'");
            files[file] = files[file].Replace(from, to, StringComparison.Ordinal);
        }

        return DataLoader.FromJson(files);
    }

    private static MatchEvent MatchStart(MatchEngine engine) => new(
        EventType.MatchStart, engine.Tick, -1, -1, -1, -1,
        new Cell(0, 0), Zone.Middle, MatchPhase.Kickoff, 0, 0, "kickoff");
}
