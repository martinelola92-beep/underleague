using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Las cuatro primitivas nuevas del catálogo unificado (docs/analisis/perks-catalogo-unificado.md §8.1):
/// <c>setState</c> sobre <c>adjacentOpponents</c> ("Terremoto"), el efecto <c>injure</c> ("Juego sucio"),
/// el efecto <c>relocate</c> ("Último hombre") y <c>cancelEvent</c> sobre <c>GOAL</c>/<c>DEATH</c> ("Mano
/// de dios", "Prohibido morir"). Cada test demuestra que la primitiva HACE lo que dice, no solo que carga:
/// ningún perk real se escribe todavía en <c>data/perks/</c> (paquete siguiente).
/// </summary>
public sealed class FourPrimitivesTests
{
    // ---------------------------------------------------------------- A. setState(adjacentOpponents)

    [Fact]
    public void SetStateOnAdjacentOpponentsKnocksDownOnlyRealNeighbours()
    {
        const string Earthquake = """[{ "type": "setState", "target": "adjacentOpponents", "state": "KnockedDown", "ticks": 10 }]""";
        var catalog = TestPerks.CatalogWith(("earthquake", TestPerks.Json("earthquake", "TACKLE", Earthquake)));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "earthquake" }));
        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;

        // Un compañero cerca (para comprobar que "solo a los rivales" es de verdad un filtro de equipo, no
        // solo de distancia), dos rivales dentro del radio real de una casilla (uno en línea recta, otro en
        // diagonal) y un rival claramente fuera.
        var teammateNear = engine.PlayerById(2)!;
        var rivalStraight = engine.PlayerById(100)!;
        var rivalDiagonal = engine.PlayerById(101)!;
        var rivalFar = engine.PlayerById(102)!;

        owner.Position = new Vec2(5f, 3f);
        teammateNear.Position = new Vec2(5.5f, 3f);
        rivalStraight.Position = new Vec2(5.9f, 3f);
        rivalDiagonal.Position = new Vec2(5.7f, 3.7f);
        rivalFar.Position = new Vec2(9f, 3f);

        engine.Effects!.Publish(Tackle(engine, owner));

        Assert.Equal(PlayerState.KnockedDown, rivalStraight.State);
        Assert.Equal(PlayerState.KnockedDown, rivalDiagonal.State);
        Assert.Equal(PlayerState.Positioning, rivalFar.State);
        Assert.Equal(PlayerState.Positioning, teammateNear.State);
        Assert.Equal(PlayerState.Positioning, owner.State);
    }

    // ---------------------------------------------------------------- B. injure

    [Fact]
    public void InjureEffectProvokesAnInjuryThroughTheNormalEngineFormula()
    {
        // El efecto no lo garantiza: pasa por la MISMA tirada que una entrada (MatchEngine.ResolveInjury),
        // así que se publica el mismo FOUL muchas veces sobre el mismo motor (mismo flujo de RNG, RT-021)
        // hasta que ocurra al menos una lesión. Con onTackleBase + onFoulBase de tuning.json (200 en base
        // 10.000, un 2%) la probabilidad de que 2.000 intentos no den ninguna es astronómicamente baja.
        const string Injure = """[{ "type": "injure", "target": "opponent" }]""";
        var catalog = TestPerks.CatalogWith(("dirty_play", TestPerks.Json("dirty_play", "FOUL", Injure, scope: "actor")));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "dirty_play" }));
        var engine = TestPerks.Engine(catalog, setup);
        var tackler = engine.PlayerById(1)!;
        var victim = engine.PlayerById(100)!;

        bool victimInjured = false;
        for (int tick = 0; tick < 2000 && !victimInjured; tick++)
        {
            engine.Effects!.Publish(new MatchEvent(
                EventType.Foul, tick, tackler.Team, tackler.Id, -1, victim.Id,
                tackler.HomeCell, Zone.Own, MatchPhase.OpenPlay, 0, 0, "foul"));
            victimInjured = victim.Injured;
        }

        Assert.True(victimInjured, "el efecto injure nunca provocó una lesión en 2000 intentos");
        Assert.False(victim.OnPitch);

        // Solo alcanza al rival resuelto (opponent = el fouled): el instigador no se lesiona a sí mismo.
        Assert.False(tackler.Injured);
        Assert.True(tackler.OnPitch);
    }

    // ---------------------------------------------------------------- C. relocate

    [Fact]
    public void RelocateOnBallCarrierMovesTheOwnerOntoWhoeverHasTheBall()
    {
        const string OnCarrier = """[{ "type": "relocate", "target": "owner", "point": "onBallCarrier" }]""";
        var catalog = TestPerks.CatalogWith(("last_man", TestPerks.Json("last_man", "TACKLE", OnCarrier)));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "last_man" }));
        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;
        var carrier = engine.PlayerById(100)!;
        carrier.Position = new Vec2(9f, 4f);
        engine.Ball.Owner = carrier;
        engine.Ball.Position = carrier.Position;

        engine.Effects!.Publish(Tackle(engine, owner));

        Assert.Equal(carrier.Position, owner.Position);
    }

    [Fact]
    public void RelocateOnBallCarrierDoesNothingWithALooseBall()
    {
        // Sin poseedor no hay a quién marcar: el efecto no mueve al portador (no es un error de datos, es
        // un estado legítimo del partido).
        const string OnCarrier = """[{ "type": "relocate", "target": "owner", "point": "onBallCarrier" }]""";
        var catalog = TestPerks.CatalogWith(("last_man", TestPerks.Json("last_man", "TACKLE", OnCarrier)));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "last_man" }));
        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;
        var before = owner.Position;
        engine.Ball.Owner = null;

        engine.Effects!.Publish(Tackle(engine, owner));

        Assert.Equal(before, owner.Position);
    }

    [Fact]
    public void RelocateBetweenBallAndOwnGoalCutsTheLaneToTheGoal()
    {
        const string Between = """[{ "type": "relocate", "target": "owner", "point": "betweenBallAndOwnGoal" }]""";
        var catalog = TestPerks.CatalogWith(("last_man", TestPerks.Json("last_man", "TACKLE", Between)));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "last_man" }));
        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!; // equipo 0: su portería propia es la que ataca el equipo 1.
        engine.Ball.Owner = null;
        engine.Ball.Position = new Vec2(10f, 5f);

        engine.Effects!.Publish(Tackle(engine, owner));

        var ownGoal = Pitch.GoalCenter(1);
        var expected = new Vec2((10f + ownGoal.X) / 2f, (5f + ownGoal.Y) / 2f);
        Assert.Equal(expected, owner.Position);
    }

    // ---------------------------------------------------------------- D. cancelEvent(GOAL / DEATH)

    [Fact]
    public void CancelEventOnGoalKeepsTheScoreboardCoherent()
    {
        const string Cancel = """[{ "type": "cancelEvent" }]""";
        var catalog = TestPerks.CatalogWith(
            ("gods_hand", TestPerks.Json("gods_hand", "GOAL", Cancel, scope: "opposingTeam", rarity: "legendary", kind: "ruleBreaker")));

        int cancelledAwayGoals = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var setup = TestPerks.Match(catalog, seed, (0, new[] { "gods_hand" }));
            var result = Simulator.Run(setup, seed, catalog, new SimConfig(CollectLog: false));

            // El portero local (dueño del perk) nunca encaja: TODOS los goles del visitante quedan
            // anulados, así que el marcador nunca sube para el equipo 1 en ninguna de las 40 semillas.
            Assert.Equal(0, result.Report.Goals[1]);

            cancelledAwayGoals += result.Events.Count(
                e => e.Type == EventType.Goal && e.Team == 1 && e.Detail.EndsWith(":cancelled", StringComparison.Ordinal));
        }

        Assert.True(cancelledAwayGoals > 0, "en 40 semillas ningún gol visitante llegó a intentarse: la puerta cancelable nunca se ejerció");
    }

    [Fact]
    public void CancelEventOnDeathLeavesThePlayerStandingAndTheReportConsistent()
    {
        // Directo sobre MatchEngine.Kill (RF-093 vía 2, la marca de un perk letal), y no a través de un
        // partido entero: la tirada letal es probabilística (docs/balance.md) y lo que hay que demostrar
        // es determinista -que Kill, cuando el evento se cancela, no toca nada-, no con qué frecuencia se
        // cancela una muerte real.
        const string NoDeath = """[{ "type": "cancelEvent" }]""";
        var catalog = TestPerks.CatalogWith(
            ("no_death", TestPerks.Json("no_death", "DEATH", NoDeath, scope: "actor", rarity: "legendary", kind: "ruleBreaker")));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "no_death" }));
        var engine = TestPerks.Engine(catalog, setup);
        var victim = engine.PlayerById(1)!;
        Vec2 before = victim.Position;

        engine.Kill(victim, "perk:test_lethal", killer: null);

        // El jugador queda exactamente como si Kill nunca se hubiera llamado (RF-093, ADR 0048): vivo, en
        // el campo, en su sitio, y el marcador de muertes del informe no sube.
        Assert.False(victim.Dead);
        Assert.True(victim.OnPitch);
        Assert.False(victim.Injured);
        Assert.Equal(before, victim.Position);
        Assert.Equal(0, engine.Report.Deaths);

        // El informe cuadra con Sim.Run.Substitutions (el mismo cruce que usa SubstitutionPoints.Pending,
        // docs/pendientes.md): LeftPitchTick nunca se tocó, así que ninguna muerte anulada abre una
        // ventana de sustitución fantasma para un jugador que nunca dejó el campo.
        Assert.Equal(-1, victim.LeftPitchTick);

        // Y el mecanismo genérico de cancelación (§2) se confirma publicando el mismo evento a mano.
        bool notCancelled = engine.Effects!.Publish(new MatchEvent(
            EventType.Death, engine.Tick, victim.Team, victim.Id, -1, -1,
            victim.HomeCell, Zone.Own, MatchPhase.OpenPlay, 0, 0, "again"));
        Assert.False(notCancelled);
    }

    // ---------------------------------------------------------------- ayudantes

    private static MatchEvent Tackle(MatchEngine engine, MatchPlayer owner) => new(
        EventType.Tackle, engine.Tick, owner.Team, owner.Id, -1, -1,
        owner.HomeCell, Zone.Own, MatchPhase.OpenPlay, engine.BiasFor(0), 0, "attempted");
}
