using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Analysis.Detectors;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BH-A (barrido de detectores del 3 oct, `run:130@1025`, 280 ticks congelado): la entrada que repite
/// <c>extraAction</c> (Arrollador tras ganar el balón, Embestida/Toro tras fallar) elegía su blanco con el alcance
/// de antes de la ADR 0186 (decisión + 0,3) mientras la resolución, con <c>escapeBeyondDecisionReach</c>, exige el
/// de la decisión (1,0). Un rival entre 1,0 y 1,3 era «alcanzable» para elegirlo y «escapado» al resolver, y esa
/// rama devolvía a quien entra a <c>Positioning</c>… con el balón en los pies: un dueño del balón que no decide como
/// portador, quieto en su casilla, con los rivales fuera de su zona sin ir a por él.
/// </summary>
public sealed class RepeatTackleReachTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static Catalog WithEscape(bool on) =>
        Catalog with { Tuning = Catalog.Tuning with { Tackle = Catalog.Tuning.Tackle with { EscapeBeyondDecisionReach = on } } };

    /// <summary>
    /// Valor conocido: quien acaba de ganar el balón repite la entrada con un rival a 1,15 (dentro del margen viejo,
    /// fuera del alcance de la decisión). Con la regla encendida no hay blanco —el mismo alcance que la resolución—, no
    /// se tira nada y sigue siendo portador. Control con la regla apagada: el alcance es 1,3 en las dos, así que la
    /// repetición sí se tira.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheRepeatUsesTheSameReachAsTheResolution(bool escape)
    {
        var catalog = WithEscape(escape);
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        int tackler = engine.OutfieldIndexForTest(0, 0);
        int rival = engine.OutfieldIndexForTest(1, 5);
        var at = new Vec2(7f, 3.5f);
        engine.GiveBallForTest(tackler, at);
        float reach = catalog.Ai.Context.TackleDistanceMaxCells;
        engine.PlaceForTest(rival, new Vec2(at.X + reach + 0.15f, at.Y));
        // Nadie más a su alcance: el resto, lejos.
        for (int team = 0; team < 2; team++)
        {
            for (int k = 0; k < 6; k++)
            {
                int p = engine.OutfieldIndexForTest(team, k);
                if (p != tackler && p != rival)
                {
                    engine.PlaceForTest(p, new Vec2(team == 0 ? 1.5f : 14.5f, 0.5f + k));
                }
            }
        }

        var player = engine.PlayerAtForTest(tackler);
        bool tried = engine.RepeatTackle(player);

        Assert.Equal(!escape, tried);
        if (escape)
        {
            Assert.Equal(PlayerState.Dribbling, engine.StateForTest(tackler));
            Assert.Equal(player.Id, engine.BallOwnerIdForTest);
        }
    }

    /// <summary>
    /// El caso real, fijado por semilla: primer partido de la run de `human_abattoir` con semilla 130. En el tick 1001 el
    /// centrocampista 4 gana una entrada, Arrollador la repite contra 2000003 (a 1,19) y se quedaba en
    /// <c>Positioning</c> con el balón 304 fotogramas hasta el final (detector BH-A: 280 ticks sin evento). Ahora: ni un
    /// fotograma de juego abierto con el dueño del balón fuera de un estado de portador, y ninguna congelación.
    /// </summary>
    [Fact]
    public void TheRealCaseNoLongerFreezes()
    {
        var (setup, seed, config) = WorstCaseProbeTests.Build("run", 130, Catalog);
        var result = Simulator.Run(setup, seed, Catalog, config);
        Assert.Contains(result.Events, e => e.Type == EventType.PerkTriggered && e.Detail == "steamroller");
        Assert.Equal(0, OwnerOutOfCarrierFrames(result));
        Assert.Empty(SymptomDetectors.Freeze(DetectorTrace.From(result), out _));
    }

    /// <summary>
    /// BH-A (revisión independiente): la rama «el rival se escapó» no deja a un dueño del balón fuera de un estado de
    /// portador, venga de donde venga. Valor conocido: con el balón y el blanco a 2 casillas vuelve como portador. Control:
    /// sin el balón vuelve a <c>Positioning</c>, como siempre.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEscapedTackleNeverLeavesTheOwnerOutOfACarrierState(bool withBall)
    {
        var engine = new MatchEngine(TestMatches.Reference(Catalog, 5), 5, Catalog, SimConfig.Default);
        int tackler = engine.OutfieldIndexForTest(0, 0);
        int rival = engine.OutfieldIndexForTest(1, 5);
        var at = new Vec2(7f, 3.5f);
        if (withBall)
        {
            engine.GiveBallForTest(tackler, at);
        }
        else
        {
            engine.PlaceForTest(tackler, at);
        }

        engine.PlaceForTest(rival, new Vec2(at.X + 2f, at.Y));
        engine.ResolveTackleOnEscapedForTest(tackler, rival);

        var state = engine.StateForTest(tackler);
        if (withBall)
        {
            Assert.Equal(engine.PlayerAtForTest(tackler).Id, engine.BallOwnerIdForTest);
            Assert.False(state is PlayerState.Positioning or PlayerState.Chasing or PlayerState.Tackling or PlayerState.Blocking, $"dueño del balón en {state}");
        }
        else
        {
            Assert.Equal(PlayerState.Positioning, state);
        }
    }

    /// <summary>
    /// Censo permanente de la clase (BH-A): 60 partidos de run (con perks, donde vive Arrollador) y 60 de referencia sin
    /// un solo fotograma de juego abierto con el dueño del balón en un estado que no es de portador. Antes del arreglo, en
    /// 1.000 + 1.000 había uno (`run:130`); 60 no lo garantizan: es la red barata, el barrido es la cara.
    /// </summary>
    [Fact]
    public void NoOwnerIsLeftOutOfACarrierState()
    {
        int frames = 0;
        var where = new List<string>();
        foreach (string kind in new[] { "run", "ref" })
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var (setup, s, config) = WorstCaseProbeTests.Build(kind, seed, Catalog);
                int n = OwnerOutOfCarrierFrames(Simulator.Run(setup, s, Catalog, config));
                if (n > 0)
                {
                    frames += n;
                    where.Add($"{kind}:{seed} ({n})");
                }
            }
        }

        Assert.True(frames == 0, "dueño del balón fuera de estado de portador: " + string.Join(", ", where));
    }

    internal static int OwnerOutOfCarrierFrames(MatchResult result)
    {
        var tr = result.Trace!;
        int frames = 0;
        for (int f = 0; f < tr.FrameCount; f++)
        {
            int o = tr.BallOwnerAt(f);
            if (tr.PhaseAt(f) == MatchPhase.OpenPlay && o >= 0
                && tr.StateAt(f, o) is PlayerState.Positioning or PlayerState.Chasing or PlayerState.Tackling or PlayerState.Blocking)
            {
                frames++;
            }
        }

        return frames;
    }
}
