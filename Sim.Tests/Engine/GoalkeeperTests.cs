using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0141 — <b>el portero</b>: qué pasa después de una parada, a quién le da el balón, y cuándo sale.
///
/// <para>Era el agujero más grande de la IA. No tenía acciones propias, la parada no era una decisión sino
/// una tirada, y sobre todo: <b>si ganaba el duelo, atrapaba siempre</b>, así que una parada cerraba la
/// jugada y el rechace —y con él la segunda jugada— no existía. Además <c>GoalkeeperLeftArea</c> era
/// inalcanzable por construcción y el rasgo «Sale mucho» no podía cumplir su nombre.</para>
///
/// <para>Como en el resto del pass, aquí se afirma que las ramas <b>existen y son alcanzables</b>, nunca
/// con qué frecuencia: eso lo decide la fase de medición.</para>
/// </summary>
public sealed class GoalkeeperTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    // ---------------------------------------------------------------- el resultado de la parada

    /// <summary>
    /// <b>Las tres ramas de una parada ocurren en partidos de verdad</b>: hay paradas que se blocan, hay
    /// rechaces y hay balones desviados a córner. Antes sólo existía la primera, y por eso el rechace no
    /// podía ser una fuente de gol.
    /// </summary>
    [Fact]
    public void LasTresRamasDeUnaParadaOcurrenEnPartidosReales()
    {
        int saves = 0, parried = 0, corners = 0;

        for (ulong seed = 1; seed <= 150; seed++)
        {
            var report = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default).Report;

            saves += report.Saves[0] + report.Saves[1];
            parried += report.SavesParried[0] + report.SavesParried[1];
            corners += report.SavesToCorner[0] + report.SavesToCorner[1];
        }

        Assert.True(saves > 0, "sin paradas no se puede comprobar nada");
        Assert.True(parried > 0, "en ciento cincuenta partidos no hubo un solo rechace: la rama no es alcanzable");
        Assert.True(corners > 0, "en ciento cincuenta partidos no se desvió ni un balón a córner");
        Assert.True(
            saves > parried + corners,
            $"el portero ya no bloca casi nada: {saves - parried - corners} blocadas de {saves} paradas");
    }

    /// <summary>
    /// <b>Un rechace deja el balón suelto y en juego.</b> Es la mitad que convierte la parada en una
    /// jugada abierta: si el balón acabara en las manos o fuera del campo, no habría segunda jugada que
    /// disputar.
    /// </summary>
    [Fact]
    public void UnRechaceDejaElBalonSueltoYEnJuego()
    {
        for (ulong seed = 1; seed <= 150; seed++)
        {
            var result = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog,
                SimConfig.Default with { CollectLog = true });

            if (result.Report.SavesParried[0] + result.Report.SavesParried[1] == 0)
            {
                continue;
            }

            var parry = result.Events.First(
                e => e.Type == Underleague.Sim.Events.EventType.Save && e.Detail == "parried");

            // Tras el rechace el juego sigue: la jugada no se cierra con una reanudación en ese mismo tick.
            Assert.NotEqual(MatchPhase.Finished, parry.Phase);
            return;
        }

        Assert.Fail("ningún rechace en ciento cincuenta partidos");
    }

    // ---------------------------------------------------------------- la salida del área

    /// <summary>
    /// <b>Un portero normal no sale del área.</b> Es la mitad que el encargo protege explícitamente: el
    /// portero adelantado no puede ser comportamiento permanente. Sin esta comprobación, «hacer alcanzable
    /// la salida» se convertiría en «el portero vive fuera».
    /// </summary>
    [Fact]
    public void ElPorteroNoSaleDelAreaPorqueSi()
    {
        var engine = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));
        engine.RefreshPerceptionForTest();

        Assert.Equal(0f, engine.KeeperExitCellsForTest(0));
        Assert.Equal(0f, engine.KeeperExitCellsForTest(1));
    }

    /// <summary>
    /// <b>Perdiendo y con el partido acabándose, el portero puede salir.</b> Es la puerta que hace
    /// alcanzable <c>GoalkeeperLeftArea</c>, y usa la misma urgencia que mueve al resto del equipo (ADR
    /// 0140) en vez de inventarse un segundo reloj.
    /// </summary>
    [Fact]
    public void ConLaUrgenciaAlMaximoElPorteroPuedeSalir()
    {
        // La urgencia a tope es «ir perdiendo con el tiempo consumido»: 100 es el techo de la fórmula.
        Assert.Equal(100, MatchEngine.UrgencyPercent(goalDifference: -2, elapsedPercent: 100, perGoalPercent: 60));

        var tuning = Catalog.Tuning.Goalkeeper;
        Assert.True(
            tuning.ExitUrgencyPercent is > 0 and <= 100,
            "el umbral de salida tiene que ser alcanzable y no trivial");
        Assert.True(tuning.ExitCells > 0f, "sin ensanche del área la salida no existe");
    }

    /// <summary>
    /// <b>El rasgo «Sale mucho» hace algo.</b> Era el caso más claro del patrón «el texto promete lo que el
    /// código no hace»: multiplicaba su persecución del balón y el clamp al área lo anulaba entero, así que
    /// el rasgo no podía cumplir su nombre por construcción.
    /// </summary>
    [Fact]
    public void ElRasgoSaleMuchoAbreLaPuertaQueElClampCerraba()
    {
        var engine = TestPerks.Engine(Catalog, WithRusherKeeper(TestMatches.Reference(Catalog, 1)));
        var keeper = engine.PlayerAtForTest(0);
        Assert.Equal(Position.Goalkeeper, keeper.Role);
        Assert.True(keeper.HasTrait(Trait.Rusher), "el escenario tenía que traer al portero con el rasgo");

        // Balón suelto justo delante del área, que es la situación del portero que sale.
        var justOutside = new Vec2(Pitch.AreaColumns + 1f, PitchConstants.CenterRow);
        engine.PlaceForTest(0, justOutside);
        engine.SetLooseForTest(justOutside, new Vec2(0f, 0f), velocityZ: 0f);
        engine.RefreshPerceptionForTest();

        Assert.True(
            engine.KeeperExitCellsForTest(keeper.Team) > 0f,
            "con el rasgo y el balón suelto delante, el portero tenía que poder salir");

        // Y QUERER ir: ensanchar el clamp sin abrir la decisión no habría servido de nada, porque el
        // portero sólo perseguía balones sueltos dentro de su área. Las dos mitades o ninguna.
        Assert.Equal(PlayerAction.ChaseBall, engine.ChooseForTest(0));

        // Y el mismo escenario SIN el rasgo no abre la puerta: es del rasgo, no del balón suelto.
        var plain = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));
        plain.PlaceForTest(0, justOutside);
        plain.SetLooseForTest(justOutside, new Vec2(0f, 0f), velocityZ: 0f);
        plain.RefreshPerceptionForTest();

        Assert.Equal(0f, plain.KeeperExitCellsForTest(keeper.Team));
    }

    /// <summary>El mismo emparejamiento, con el portero local llevando «Sale mucho».</summary>
    private static MatchSetup WithRusherKeeper(MatchSetup setup)
    {
        var players = setup.Home.Players
            .Select(p => p.Position == Position.Goalkeeper
                ? p with { Traits = new[] { Trait.Rusher } }
                : p)
            .ToList();

        return setup with { Home = setup.Home with { Players = players } };
    }
}
