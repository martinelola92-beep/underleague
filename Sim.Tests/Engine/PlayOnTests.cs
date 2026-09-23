using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0134 (E): «seguir jugando» es la tercera respuesta al punto de decisión de la ADR 0094, y como las
/// otras dos viaja en el estado inicial. Lo que se comprueba aquí es lo que el <b>motor</b> promete: el
/// lesionado leve que se queda no deja el campo y su punto deja de pender sin registro aparte; la lesión se
/// cuenta igual; sus atributos efectivos pasan a ser <b>exactamente</b> los que trae la decisión, en ese
/// tick y no antes; pedirlo sobre una lesión grave o sobre quien nunca estuvo en el campo es un error
/// explícito (RT-032); y sin ninguna decisión que responder el partido es el de siempre, evento a evento.
///
/// <para>El <b>precio</b> —cuánto baja cada atributo, la inmunidad de RF-035, el redondeo, que la correa no
/// se toque— no se prueba aquí porque no se decide aquí: lo calcula <c>RunPlayer.ToDefinition</c> y viaja
/// hecho en <see cref="PlayOn.After"/>. Ver la documentación de <see cref="PlayOn"/>.</para>
/// </summary>
public sealed class PlayOnTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly SimConfig Config = new(CollectLog: false);

    /// <summary>
    /// Atributos base del equipo que recibe las lesiones: aguante 7 contra la fuerza 99 de los brutales
    /// para que las entradas lesionen, y correa 99 como en <see cref="TestMatches.Brutal"/>.
    /// </summary>
    private static readonly Attributes Fragile = new(20, 50, 33, 7, 99);

    /// <summary>
    /// Los atributos que trae la decisión en estos tests. <b>No</b> son el 85 % de <see cref="Fragile"/>
    /// —que sería (17, 42, 28, 5, 84)— y bajan también la correa: así, si el motor volviera a calcular el
    /// precio en vez de ejecutar el que recibe, ningún atributo coincidiría.
    /// </summary>
    private static readonly Attributes After = new(13, 41, 27, 6, 77);

    /// <summary>
    /// Una segunda decisión, distinta de <see cref="After"/> en los cinco atributos, para fechar el cambio
    /// comparando dos partidos en los que <b>solo</b> difieren los atributos: los dos se quedan en el campo.
    /// </summary>
    private static readonly Attributes AfterAlt = new(19, 49, 32, 7, 98);

    private static readonly Position[] Positions =
    {
        Position.Goalkeeper, Position.Defender, Position.Defender,
        Position.Midfielder, Position.Midfielder, Position.Midfielder, Position.Forward,
    };

    /// <summary>
    /// Siete frágiles (con siete, y no con cinco como <see cref="TestMatches.Brutal"/>, para que el equipo
    /// no se quede por debajo del mínimo en cuanto cae el primero) contra los siete brutales, más los
    /// suplentes pedidos (ids 50 en adelante), que no están en la alineación. Un suplente no entra en el
    /// partido si ninguna sustitución lo nombra, así que ampliar el banquillo no cambia el partido base.
    /// </summary>
    private static MatchSetup Scenario(int bench = 1)
    {
        PlayerDefinition Make(int id, Position position) =>
            new(id, "fragile" + id, Race.Human, position, Rarity.Common, 1, Fragile, Array.Empty<Trait>(),
                new[] { "Neutral", position.ToString() }, PhysicalState.Healthy);

        var starters = new List<PlayerDefinition>();
        for (int i = 0; i < Positions.Length; i++)
        {
            starters.Add(Make(i, Positions[i]));
        }

        var players = new List<PlayerDefinition>(starters);
        for (int i = 0; i < bench; i++)
        {
            players.Add(Make(50 + i, i % 2 == 0 ? Position.Defender : Position.Forward));
        }

        var fragile = new TeamSetup("fragile", "fragile", Race.Human, players, Lineup.Default(starters));
        return TestMatches.Brutal(Catalog) with { Home = fragile };
    }

    /// <summary>
    /// Primera semilla cuya <b>primera</b> lesión del equipo 0 es del tipo pedido y cae con margen de
    /// sobra antes del final. Exigir que sea la primera mantiene el escenario legible: nadie ha salido del
    /// campo todavía cuando llega la decisión.
    /// </summary>
    private static (ulong Seed, MatchResult Baseline, MatchEvent Injury) FirstInjury(MatchSetup setup, string detail)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var result = Simulator.Run(setup, seed, Catalog, Config);
            for (int i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (e.Type != EventType.Injury || e.Team != 0)
                {
                    continue;
                }

                if (e.Detail == detail && e.Tick > 1 && e.Tick < Catalog.Tuning.RegulationTicks - 60)
                {
                    return (seed, result, e);
                }

                break;
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"ninguna semilla abre con una lesión '{detail}' del equipo 0 con margen: el escenario Brutal ya no es brutal");
    }

    private static MatchSetup WithPlayOn(MatchSetup setup, MatchEvent injury) =>
        setup with { Home = setup.Home with { PlayOns = new[] { new PlayOn(injury.Tick, injury.Actor, After) } } };

    /// <summary>Corre el partido por el motor directamente, que es la única forma de leer los atributos efectivos al acabar.</summary>
    private static MatchPlayer PlayerAfter(MatchSetup setup, ulong seed, int playerId)
    {
        var engine = new MatchEngine(setup, seed, Catalog, Config);
        engine.Run();
        return engine.PlayerById(playerId)!;
    }

    /// <summary>
    /// Traza del partido (<c>SimConfig.Trace</c>): posición y presencia en el campo de los 20 jugadores en
    /// <b>todos</b> los ticks. Es el instrumento que hace falta para fechar un cambio, porque un atributo
    /// distinto mueve al jugador en el tick siguiente aunque tarde en producir un evento.
    ///
    /// <para><b>No</b> se usa <c>RegulationTicksOverride</c> para «cortar el partido antes»: la duración
    /// reglamentaria entra en la curva de fatiga (<c>MatchEngine</c>, <c>span = RegulationTicks −
    /// fatigueStartTick</c>), así que acortarla cambia el partido desde el tick 1 y lo que se mide ya no es
    /// el mismo partido. Costó un test verde que no probaba nada.</para>
    /// </summary>
    private static MatchTrace TraceOf(MatchSetup setup, ulong seed) =>
        Simulator.Run(setup, seed, Catalog, Config with { Trace = true }).Trace!;

    private static int IndexIn(MatchTrace trace, int playerId)
    {
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Id == playerId)
            {
                return i;
            }
        }

        throw new Xunit.Sdk.XunitException($"el jugador {playerId} no está en la traza");
    }

    /// <summary>Las posiciones de los 20 son idénticas en todos los fotogramas anteriores a ese tick.</summary>
    private static void AssertSamePositionsBefore(MatchTrace a, MatchTrace b, int tick)
    {
        for (int frame = 0; frame < a.FrameCount && a.TickAt(frame) < tick; frame++)
        {
            Assert.True(frame < b.FrameCount, $"la segunda traza se queda corta en el fotograma {frame}");
            for (int player = 0; player < a.Players.Count; player++)
            {
                Assert.Equal(a.PositionAt(frame, player), b.PositionAt(frame, player));
                Assert.Equal(a.OnPitchAt(frame, player), b.OnPitchAt(frame, player));
            }
        }
    }

    /// <summary>True si en algún fotograma posterior a ese tick alguien está en otro sitio.</summary>
    private static bool DivergesAfter(MatchTrace a, MatchTrace b, int tick)
    {
        for (int frame = 0; frame < a.FrameCount && frame < b.FrameCount; frame++)
        {
            if (a.TickAt(frame) <= tick)
            {
                continue;
            }

            for (int player = 0; player < a.Players.Count; player++)
            {
                if (a.PositionAt(frame, player) != b.PositionAt(frame, player))
                {
                    return true;
                }
            }
        }

        return a.FrameCount != b.FrameCount;
    }

    private static void AssertAttributes(Attributes expected, MatchPlayer player)
    {
        Assert.Equal(expected.Strength, player.Effective(AttributeKind.Strength));
        Assert.Equal(expected.Speed, player.Effective(AttributeKind.Speed));
        Assert.Equal(expected.Technique, player.Effective(AttributeKind.Technique));
        Assert.Equal(expected.Stamina, player.Effective(AttributeKind.Stamina));
        Assert.Equal(expected.Leash, player.Effective(AttributeKind.Leash));
    }

    /// <summary>
    /// Lo que da nombre al apartado: esa lesión no lo aparta. En el mismo tick en el que el partido sin la
    /// decisión ya lo ha sacado del campo, el partido con ella lo tiene dentro; y sigue dentro después. Si
    /// más adelante acaba saliendo es por algo posterior —quedarse deja al jugador expuesto a la siguiente
    /// entrada, que es parte del canje—, nunca por esta.
    /// </summary>
    [Fact]
    public void TheMinorInjuryDoesNotRemoveHimWhenHePlaysOn()
    {
        var setup = Scenario();
        var (seed, baseline, injury) = FirstInjury(setup, "minor");
        var playOn = WithPlayOn(setup, injury);

        var left = Assert.Single(baseline.Report.Players, p => p.PlayerId == injury.Actor);
        Assert.Equal(injury.Tick, left.LeftPitchTick);

        var withoutDecision = TraceOf(setup, seed);
        var withDecision = TraceOf(playOn, seed);
        int player = IndexIn(withDecision, injury.Actor);
        int frame = withDecision.FrameOfTick(injury.Tick);
        Assert.False(withoutDecision.OnPitchAt(withoutDecision.FrameOfTick(injury.Tick), player),
            "sin la decisión, el lesionado debería haber dejado el campo en ese mismo tick");
        Assert.True(withDecision.OnPitchAt(frame, player), "la lesión leve lo sacó del campo pese al PlayOn");
        Assert.True(withDecision.OnPitchAt(frame + 1, player), "salió del campo al tick siguiente");

        var result = Simulator.Run(playOn, seed, Catalog, Config);
        var stayed = Assert.Single(result.Report.Players, p => p.PlayerId == injury.Actor);
        Assert.True(stayed.LeftPitchTick < 0 || stayed.LeftPitchTick > injury.Tick,
            $"se lesionó en el tick {injury.Tick} y aun así dejó el campo en el {stayed.LeftPitchTick}");
        Assert.True(stayed.TicksOnPitch > left.TicksOnPitch,
            $"el que sigue jugando estuvo {stayed.TicksOnPitch} ticks en el campo y el que salió {left.TicksOnPitch}");
        Assert.Contains(result.Events, e => e.Tick > injury.Tick
            && (e.Actor == injury.Actor || e.Target == injury.Actor || e.Opponent == injury.Actor));

        // Y el punto de decisión deja de pender sin bookkeeping: SubstitutionPoints mira LeftPitchTick. Si
        // más tarde le pasa otra cosa, eso es una ventana nueva, no la que ya se respondió.
        var pending = SubstitutionPoints.Pending(playOn, result, 0, Catalog);
        Assert.True(pending is null || pending.Tick > injury.Tick, "la decisión ya respondida sigue pendiente");
    }

    /// <summary>
    /// La lesión ocurrió: el informe la cuenta y el jugador queda marcado, que es lo que la run apunta al
    /// acabar el partido. No se comparan los totales con los del partido sin <c>PlayOn</c> porque después
    /// de T los dos partidos son distintos —ese es justo el sentido de la decisión—, así que lo que se fija
    /// es la lesión concreta de T y el prefijo idéntico que la precede.
    /// </summary>
    [Fact]
    public void TheInjuryIsStillCountedAlthoughNobodyLeavesThePitch()
    {
        var setup = Scenario();
        var (seed, baseline, injury) = FirstInjury(setup, "minor");
        var result = Simulator.Run(WithPlayOn(setup, injury), seed, Catalog, Config);

        var stats = Assert.Single(result.Report.Players, p => p.PlayerId == injury.Actor);
        Assert.True(stats.Injured, "el que se queda en el campo no consta como lesionado");
        Assert.True(result.Report.Injuries >= 1);
        Assert.Contains(result.Events, e => e.Type == EventType.Injury && e.Tick == injury.Tick
            && e.Actor == injury.Actor && e.Detail == "minor");

        var before = baseline.Events.Where(e => e.Tick <= injury.Tick).ToList();
        var after = result.Events.Where(e => e.Tick <= injury.Tick).ToList();
        Assert.Equal(before.Count(e => e.Type == EventType.Injury), after.Count(e => e.Type == EventType.Injury));
    }

    /// <summary>
    /// Lo que el motor sí decide: los cinco atributos efectivos pasan a ser <b>exactamente</b> los que trae
    /// la decisión, ni uno recalculado. <see cref="After"/> no es el 85 % del base y baja también la correa,
    /// así que si el motor volviera a aplicar la fórmula de RF-091 —el error que la ADR 0134 arregló
    /// llevándose el cálculo a <c>RunPlayer.ToDefinition</c>— no coincidiría ningún atributo.
    /// </summary>
    [Fact]
    public void TheEffectiveAttributesBecomeExactlyTheOnesTheDecisionCarries()
    {
        var setup = Scenario();
        var (seed, _, injury) = FirstInjury(setup, "minor");
        AssertAttributes(After, PlayerAfter(WithPlayOn(setup, injury), seed, injury.Actor));
    }

    /// <summary>
    /// «Desde ese tick y no antes.» La prueba se hace con dos decisiones distintas sobre la misma lesión
    /// —<see cref="After"/> y <see cref="AfterAlt"/>, que difieren en los cinco atributos—, y no contra el
    /// partido sin decisión: así lo único que cambia entre las dos ejecuciones son los atributos, no también
    /// el hecho de quedarse en el campo. Hasta T las <b>posiciones de los veinte</b> coinciden tick a tick
    /// (si los atributos se hubieran aplicado antes, el jugador estaría en otro sitio al tick siguiente) y
    /// después del tick divergen. Y «ni después»: al acabar el partido los efectivos siguen siendo los de la
    /// decisión, nadie los vuelve a tocar.
    /// </summary>
    [Fact]
    public void TheNewAttributesStartAtTheInjuryTickAndNotBefore()
    {
        var setup = Scenario();
        var (seed, _, injury) = FirstInjury(setup, "minor");
        var playOn = WithPlayOn(setup, injury);
        var alternative = setup with
        {
            Home = setup.Home with { PlayOns = new[] { new PlayOn(injury.Tick, injury.Actor, AfterAlt) } },
        };

        var one = TraceOf(playOn, seed);
        var other = TraceOf(alternative, seed);
        AssertSamePositionsBefore(one, other, injury.Tick);
        Assert.True(DivergesAfter(one, other, injury.Tick),
            "dos decisiones con atributos distintos dieron el mismo partido: los atributos no se están aplicando");

        // Y tampoco pasa nada antes respecto al partido sin decisión ninguna, que hasta T es el mismo.
        AssertSamePositionsBefore(TraceOf(setup, seed), one, injury.Tick);

        AssertAttributes(After, PlayerAfter(playOn, seed, injury.Actor));
        AssertAttributes(AfterAlt, PlayerAfter(alternative, seed, injury.Actor));
    }

    /// <summary>
    /// RF-092: la lesión grave sigue apartando. Pedir que se quede es ilegal y se dice en voz alta, no se
    /// ignora en silencio (RT-032, como la sustitución ilegal de la ADR 0094).
    /// </summary>
    [Fact]
    public void PlayingOnThroughASevereInjuryIsAnExplicitError()
    {
        var setup = Scenario();
        var (seed, _, injury) = FirstInjury(setup, "severe");
        var error = Assert.Throws<ArgumentException>(
            () => Simulator.Run(WithPlayOn(setup, injury), seed, Catalog, Config));
        Assert.Contains("ADR 0134", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// BA-B otra vez, ahora sobre la tercera respuesta: el suplente que entró por el primer lesionado está
    /// en el campo tanto como un titular, así que puede lesionarse levemente y quedarse. Con la validación
    /// mirando solo la alineación inicial, responder esa ventana reventaba el partido —y con guardado
    /// ironman (RT-061) eso es la run entera—. Es el segundo lesionado de cualquier partido violento, no un
    /// caso de borde.
    /// </summary>
    [Fact]
    public void ASubstituteWhoCameOnCanAlsoPlayOn()
    {
        var setup = Scenario(bench: 3);
        for (ulong seed = 1; seed < 400; seed++)
        {
            var point = SubstitutionPoints.Pending(setup, Simulator.Run(setup, seed, Catalog, Config), 0, Catalog);
            if (point is null)
            {
                continue;
            }

            int substitute = point.Candidates[0].Id;
            var withSubstitution = setup with
            {
                Home = setup.Home with { Substitutions = new[] { new Substitution(point.Tick, point.OutPlayerId, substitute) } },
            };

            var injury = Simulator.Run(withSubstitution, seed, Catalog, Config).Events
                .FirstOrDefault(e => e.Type == EventType.Injury && e.Actor == substitute && e.Detail == "minor");
            if (injury is null)
            {
                continue;
            }

            var playOn = withSubstitution with
            {
                Home = withSubstitution.Home with { PlayOns = new[] { new PlayOn(injury.Tick, substitute, After) } },
            };

            var result = Simulator.Run(playOn, seed, Catalog, Config);
            var stats = Assert.Single(result.Report.Players, p => p.PlayerId == substitute);
            Assert.Equal(-1, stats.LeftPitchTick);
            Assert.True(stats.Injured);
            return;
        }

        throw new Xunit.Sdk.XunitException(
            "ninguna semilla lesiona levemente a un suplente que ya había entrado: el escenario ya no encadena dos lesiones");
    }

    /// <summary>Quien nunca llegó a estar en el campo no puede seguir jugando: no estaba jugando (ADR 0134 E).</summary>
    [Fact]
    public void PlayingOnWithSomebodyWhoWasNeverOnThePitchIsAnExplicitError()
    {
        var setup = Scenario();
        var bench = setup with { Home = setup.Home with { PlayOns = new[] { new PlayOn(10, 50, After) } } };
        Assert.Throws<ArgumentException>(() => Simulator.Run(bench, 1, Catalog, Config));

        var twice = setup with
        {
            Home = setup.Home with { PlayOns = new[] { new PlayOn(10, 1, After), new PlayOn(10, 1, After) } },
        };
        Assert.Throws<ArgumentException>(() => Simulator.Run(twice, 1, Catalog, Config));

        var empty = setup with { Home = setup.Home with { PlayOns = new PlayOn[] { null! } } };
        Assert.Throws<ArgumentException>(() => Simulator.Run(empty, 1, Catalog, Config));
    }

    /// <summary>
    /// Un <c>PlayOn</c> construido a medias, con <c>After</c> en <c>default</c>, dejaría al jugador en el
    /// campo sin pagar nada: la opción gratis que el apartado E existe para que no exista. Se rechaza por el
    /// rango (1..99, <c>Attributes.Clamp</c>), no por un null, porque <c>Attributes</c> es un struct.
    /// </summary>
    [Fact]
    public void PlayingOnWithoutTheAttributesTheDecisionCostsIsAnExplicitError()
    {
        var setup = Scenario();
        var unpaid = setup with { Home = setup.Home with { PlayOns = new[] { new PlayOn(10, 1, default) } } };
        var error = Assert.Throws<ArgumentException>(() => Simulator.Run(unpaid, 1, Catalog, Config));
        Assert.Contains("ADR 0134", error.Message, StringComparison.Ordinal);

        var partial = setup with
        {
            Home = setup.Home with { PlayOns = new[] { new PlayOn(10, 1, After with { Leash = 0 }) } },
        };
        Assert.Throws<ArgumentException>(() => Simulator.Run(partial, 1, Catalog, Config));
    }

    /// <summary>RT-024: la decisión es parte del estado inicial, así que el partido se reproduce igual.</summary>
    [Fact]
    public void TheSameDecisionReproducesTheSameMatch()
    {
        var setup = Scenario();
        var (seed, _, injury) = FirstInjury(setup, "minor");
        var playOn = WithPlayOn(setup, injury);

        Assert.Equal(
            Simulator.Run(playOn, seed, Catalog, Config).Events,
            Simulator.Run(playOn, seed, Catalog, Config).Events);
    }

    /// <summary>
    /// No regresión: sin ninguna decisión que responder, el motor hace lo de siempre. Una lista de
    /// <c>PlayOn</c> que no coincide con ninguna lesión no es «casi» inerte: da el mismo partido evento a
    /// evento que no traer lista, que es lo que garantiza que las tandas de <c>/Balance</c> —donde la
    /// política siempre sustituye y nadie ejerce esta decisión— no se muevan ni una tirada.
    /// </summary>
    [Fact]
    public void WithoutAnyDecisionTheMatchIsTheSameEventForEvent()
    {
        var setup = Scenario();
        var (seed, baseline, injury) = FirstInjury(setup, "minor");

        var neverFires = setup with
        {
            Home = setup.Home with { PlayOns = new[] { new PlayOn(injury.Tick + 1, injury.Actor, After) } },
        };
        Assert.Equal(baseline.Events, Simulator.Run(neverFires, seed, Catalog, Config).Events);

        for (ulong other = 1; other <= 20; other++)
        {
            var reference = TestMatches.Reference(Catalog, other);
            Assert.Equal(
                Simulator.Run(reference, other, Catalog, Config).Events,
                Simulator.Run(reference with
                {
                    Home = reference.Home with
                    {
                        PlayOns = new[] { new PlayOn(7, reference.Home.Lineup.Slots[0].PlayerId, After) },
                    },
                }, other, Catalog, Config).Events);
        }
    }
}
