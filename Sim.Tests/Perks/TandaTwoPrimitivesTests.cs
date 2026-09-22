using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Las cinco primitivas de la tanda 2 del catálogo unificado (docs/analisis/perks-catalogo-unificado.md
/// §3.2): C4 (<c>modifyTraitScalar</c>), C5 (<c>modifyMarkBias</c>), C8 (<c>shiftHome</c> y
/// <c>modifyZoneShape</c>) y la acción extra tras un evento (<c>extraAction</c>). C7
/// (<c>modifyTackleBias</c>) tiene su propio fichero, <see cref="Underleague.Sim.Tests.Engine.
/// TackleBiasTests"/>, porque se prueba con el arnés ligero de <c>Utility.Choose</c> y no con un motor
/// completo. Ningún perk real se escribe todavía en <c>data/perks/</c> (paquete siguiente).
/// </summary>
public sealed class TandaTwoPrimitivesTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    // ================================================================== C4: modifyTraitScalar

    /// <summary>
    /// El escalar no es un dato inerte: es EXACTAMENTE el que consulta la fórmula de tiro
    /// (<c>Utility.EvaluateShoot</c>, <c>rangeCenti = (ShootBaseRangeCells + p.ShootRangeBonusCells) *
    /// 100</c>). Un tirador a un metro más allá del alcance base paga la penalización por estar fuera de
    /// rango; con el escalar escrito por el perk (+3 casillas), la MISMA distancia queda dentro de rango y
    /// la penalización desaparece del todo: no es un matiz, es la diferencia entre "descontado" y "cero".
    /// </summary>
    [Fact]
    public void ModifyTraitScalarOnShootRangeRemovesTheBeyondRangePenalty()
    {
        const string Cannon = """[{ "type": "modifyTraitScalar", "target": "owner", "scalar": "shootRangeBonusCells", "value": 3, "duration": "match" }]""";
        var catalog = TestPerks.CatalogWith(("cannon", TestPerks.Json("cannon", "MATCH_START", Cannon)));
        var setup = TestPerks.Match(catalog, 1, (6, new[] { "cannon" })); // home forward
        var engine = TestPerks.Engine(catalog, setup);
        var shooter = engine.PlayerById(6)!;

        Assert.Equal(0, shooter.ShootRangeBonusCells);

        // Un metro más allá de un alcance base de 6 casillas: fuera de rango sin el escalar.
        const int BaseRangeCells = 6;
        const int BeyondRangePenaltyPerCell = 100;
        shooter.State = PlayerState.Dribbling;
        shooter.Position = new Vec2(16f - (BaseRangeCells + 1), PitchConstants.CenterRow);

        var weights = ShootRangeWeights(BaseRangeCells, BeyondRangePenaltyPerCell);
        var context = ShootContext(weights, shooter);
        context.Ball.Owner = shooter;

        var rows = new List<UtilityRow>();
        Utility.Choose(context, shooter, rows);
        Assert.Equal(-BeyondRangePenaltyPerCell, Row(rows, PlayerAction.Shoot).Context);

        // El mismo jugador, en el mismo sitio: el perk escribe el escalar y la MISMA fórmula (sin tocarla)
        // ya no penaliza nada, porque el alcance efectivo (6+3=9) ahora cubre la distancia (7).
        engine.Effects!.Publish(MatchStart(engine));
        Assert.Equal(3, shooter.ShootRangeBonusCells);

        rows.Clear();
        Utility.Choose(context, shooter, rows);
        Assert.Equal(0, Row(rows, PlayerAction.Shoot).Context);
    }

    // ================================================================== C5: modifyMarkBias

    /// <summary>"Perro de presa": el marcador prefiere una etiqueta de rival, aunque no sea el más cercano.</summary>
    [Fact]
    public void PreferTagMarkBiasPicksTheTaggedRivalInsteadOfTheCloserOne()
    {
        const string PreferTag =
            """[{ "type": "modifyMarkBias", "markBias": "preferTag", "markTag": "Defender", "target": "owner", "value": 6, "duration": "match" }]""";

        var plainCatalog = TestPerks.CatalogWith();
        var baseline = TestPerks.Engine(plainCatalog, TestPerks.Match(plainCatalog, 9));
        PositionPreferTagScenario(baseline);
        baseline.ForceReassignMarking();
        var markerBaseline = baseline.PlayerById(3)!; // home midfielder
        Assert.Equal(106, markerBaseline.MarkTarget!.Id); // el forward cercano, sin sesgo.

        var catalogWithPerk = TestPerks.CatalogWith(("bloodhound", TestPerks.Json("bloodhound", "MATCH_START", PreferTag)));
        var setupWithPerk = TestPerks.Match(catalogWithPerk, 9, (3, new[] { "bloodhound" }));
        var biased = TestPerks.Engine(catalogWithPerk, setupWithPerk);
        PositionPreferTagScenario(biased);
        biased.Effects!.Publish(MatchStart(biased));
        biased.ForceReassignMarking();
        var markerBiased = biased.PlayerById(3)!;
        Assert.Equal(101, markerBiased.MarkTarget!.Id); // el defensor lejano, CON sesgo por etiqueta.
    }

    /// <summary>"Guardaespaldas": marca al rival que amenaza a su vinculado, no al más cercano a sí mismo.</summary>
    [Fact]
    public void ProtectLinkedMarkBiasPicksTheRivalNearTheProtectedAllyInsteadOfTheOneNearTheMarker()
    {
        const string ProtectLinked =
            """[{ "type": "modifyMarkBias", "markBias": "protectLinked", "target": "linked", "value": 6, "duration": "match" }]""";

        var (baseline, baselineIds) = LinkedMatch(null);
        PositionProtectLinkedScenario(baseline, baselineIds);
        baseline.ForceReassignMarking();
        var hubBaseline = baseline.PlayerById(baselineIds[4])!;
        Assert.Equal(101, hubBaseline.MarkTarget!.Id); // sin sesgo: el rival más cercano AL HUB, no al vinculado.

        var (biased, biasedIds) = LinkedMatch(("bodyguard", TestPerks.Json(
            "bodyguard", "MATCH_START", ProtectLinked, axis: "alignment", links: """["beside"]""")));
        PositionProtectLinkedScenario(biased, biasedIds);
        biased.Effects!.Publish(MatchStart(biased));

        var hub = biased.PlayerById(biasedIds[4])!;
        var ally = biased.PlayerById(biasedIds[5])!;
        Assert.Same(ally, hub.MarkProtect);
        Assert.Equal(6, hub.MarkProtectBonusCells);

        biased.ForceReassignMarking();
        Assert.Equal(102, hub.MarkTarget!.Id); // el rival cerca del vinculado, no el cerca del propio hub.
    }

    /// <summary>"Hombre libre": ningún marcador rival lo prefiere, así que el emparejamiento CONTRARIO cambia.</summary>
    [Fact]
    public void AvoidedMarkBiasMakesRivalMarkersPreferAnotherTarget()
    {
        const string Avoided = """[{ "type": "modifyMarkBias", "markBias": "avoided", "target": "owner", "value": 6, "duration": "match" }]""";

        var plainCatalog = TestPerks.CatalogWith();
        var baseline = TestPerks.Engine(plainCatalog, TestPerks.Match(plainCatalog, 9));
        PositionAvoidedScenario(baseline);
        baseline.ForceReassignMarking();
        var homeMarkerBaseline = baseline.PlayerById(1)!;
        Assert.Equal(101, homeMarkerBaseline.MarkTarget!.Id); // el más cercano, sin sesgo.

        var catalogWithPerk = TestPerks.CatalogWith(("free_man", TestPerks.Json("free_man", "MATCH_START", Avoided)));
        // El perk lo lleva el RIVAL (101), no el marcador: es el único de los tres que toca la IA
        // del equipo CONTRARIO (encargo, C5).
        var setupWithPerk = TestPerks.Match(catalogWithPerk, 9, (101, new[] { "free_man" }));
        var biased = TestPerks.Engine(catalogWithPerk, setupWithPerk);
        PositionAvoidedScenario(biased);
        biased.Effects!.Publish(MatchStart(biased));
        biased.ForceReassignMarking();
        var homeMarkerBiased = biased.PlayerById(1)!;
        Assert.Equal(102, homeMarkerBiased.MarkTarget!.Id); // el equipo contrario ya no lo prefiere.
    }

    // ================================================================== C8: shiftHome / modifyZoneShape

    /// <summary>
    /// "Línea adelantada"/"Pivote hondo"/"Desmarque profundo": el hogar efectivo se desplaza POR JUGADOR,
    /// sumado al desplazamiento de bloque táctico que ya existía. Se ejercita
    /// <c>MatchEngine.UpdateBlockShift</c> directamente (internal solo para esto, ver su comentario) para
    /// no depender de cuántos ticks tarda el bloque en moverse: aísla la parte nueva.
    /// </summary>
    [Fact]
    public void ShiftHomeMovesTheEffectiveHomeForwardAndThePlayerLivesWhereItSays()
    {
        const string ShiftForward = """[{ "type": "shiftHome", "target": "owner", "value": 3, "duration": "match" }]""";
        var catalog = TestPerks.CatalogWith(("deep_pivot", TestPerks.Json("deep_pivot", "MATCH_START", ShiftForward)));
        var setup = TestPerks.Match(catalog, 1, (3, new[] { "deep_pivot" })); // home midfielder: sin techo de línea defensiva.
        var engine = TestPerks.Engine(catalog, setup);
        var player = engine.PlayerById(3)!;
        var teammate = engine.PlayerById(4)!; // sin el perk: sirve de control.

        Assert.Equal(0, player.HomeShiftCells);
        engine.Effects!.Publish(MatchStart(engine));
        Assert.Equal(3, player.HomeShiftCells);

        engine.UpdateBlockShift();

        // Equipo 0 ataca hacia +X (Pitch.AttackDirection(0) == 1): el hogar efectivo de CADA jugador es
        // su hogar fijo más el desplazamiento de bloque táctico del tick (igual para los dos, mismo
        // equipo) más su propio shiftHome. Restando el desplazamiento de cada uno respecto a su propio
        // hogar fijo, el término de bloque se cancela y solo queda la parte del efecto: exactamente 3.
        float playerDelta = player.EffectiveHome.X - player.HomeCenter.X;
        float teammateDelta = teammate.EffectiveHome.X - teammate.HomeCenter.X;
        Assert.Equal(3f, playerDelta - teammateDelta, 3);

        // Y el jugador VIVE ahí: TargetPoint/Position convergen hacia EffectiveHome cuando no hay nada más
        // urgente (Utility.Choose ya lo comprueba en otros tests); aquí basta con que el punto de destino
        // que la utilidad usaría de verdad -su hogar efectivo- se haya movido, no solo un campo interno.
        Assert.True(player.EffectiveHome.X > player.HomeCenter.X, "el hogar efectivo debería quedar por delante del hogar fijo");
    }

    /// <summary>
    /// "Sombra": la zona de acción cambia de forma en UNA sola dirección, sin tocar las otras dos (a
    /// diferencia de <c>modifyLeash</c>, que las ensancha por igual). Se demuestra con
    /// <c>ActionZone.Clamp</c>: un punto que antes quedaba recortado a los lados de la zona, con la zona
    /// ampliada hacia los lados ya no lo está.
    /// </summary>
    [Fact]
    public void ModifyZoneShapeWidensOnlyTheSidesAndChangesWhatGetsClamped()
    {
        const string WidenSides = """[{ "type": "modifyZoneShape", "target": "owner", "dimension": "sides", "value": 2, "duration": "match" }]""";
        var catalog = TestPerks.CatalogWith(("shadow", TestPerks.Json("shadow", "MATCH_START", WidenSides)));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "shadow" })); // home defender: sides finito (2 casillas).
        var engine = TestPerks.Engine(catalog, setup);
        var player = engine.PlayerById(1)!;

        int forwardBefore = player.Zone.ForwardMilli;
        int backBefore = player.Zone.BackMilli;
        int sidesBefore = player.Zone.SidesMilli;

        // Medio casillón más allá del borde lateral actual: fuera de la zona blanda original por
        // construcción, y sobradamente dentro de la ampliada (+2 casillas más adelante).
        int direction = Pitch.AttackDirection(player.Team);
        float sidesBeforeCells = ActionZone.Cells(sidesBefore);
        var farToTheSide = new Vec2(player.EffectiveHome.X, player.EffectiveHome.Y + sidesBeforeCells + 0.5f);
        var beforeClamp = player.Zone.Clamp(farToTheSide, player.EffectiveHome, direction);
        Assert.NotEqual(farToTheSide, beforeClamp); // recortado: 1,9 está fuera de la zona blanda original.

        engine.Effects!.Publish(MatchStart(engine));

        // Solo 'sides' cambia; forward y back quedan EXACTAMENTE igual (la primitiva toca una dimensión).
        Assert.Equal(forwardBefore, player.Zone.ForwardMilli);
        Assert.Equal(backBefore, player.Zone.BackMilli);
        Assert.Equal(sidesBefore + 2000, player.Zone.SidesMilli);

        var afterClamp = player.Zone.Clamp(farToTheSide, player.EffectiveHome, direction);
        Assert.Equal(farToTheSide, afterClamp); // ya no se recorta: la zona ampliada lo admite tal cual.
    }

    // ================================================================== acción extra tras un evento

    /// <summary>
    /// "Doble disparo": el MISMO tirador repite el disparo DENTRO DEL MISMO tick -sin publicar nada en un
    /// tick futuro (RT-020)-, con el orden y el corte de RT-041/RT-042: la cadena se resuelve con el
    /// _maxDepth/RecursionCuts que ya existía para cualquier evento anidado, y el corte es observable en
    /// el informe. Con maxDepth=1 la cadena resuelve el disparo REAL dos veces (dos LaunchShot de verdad,
    /// con el balón recuperado y vuelto a lanzar) antes de que la tercera se corte.
    /// </summary>
    [Fact]
    public void ExtraActionOnShotRepeatsTheRealShotWithinTheSameTickAndCutsAtMaxDepth()
    {
        const string Extra = """[{ "type": "extraAction" }]""";
        var catalog = TestPerks.CatalogWith(("double_shot", TestPerks.Json("double_shot", "SHOT", Extra, scope: "actor")));
        var setup = TestPerks.Match(catalog, 1, (6, new[] { "double_shot" })); // home forward
        var engine = TestPerks.Engine(catalog, setup, maxDepth: 1);
        var shooter = engine.PlayerById(6)!;

        Assert.Equal(0, shooter.Shots);
        Assert.Equal(0, engine.Report.RecursionCuts);

        engine.Effects!.Publish(Shot(engine, shooter));

        Assert.Equal(2, shooter.Shots);
        Assert.Equal(1, engine.Report.RecursionCuts);
    }

    /// <summary>
    /// "Embestida"/"Arrollador": la MISMA idea sobre una entrada -repite dentro del mismo tick, corte por
    /// profundidad-, pero con <c>MatchEngine.ResolveTackle</c> en vez de <c>LaunchShot</c>: cada nivel de
    /// la cadena busca el rival alcanzable más cercano y entra de verdad (cuenta como entrada sin balón,
    /// ADR 0105). Desde la ADR 0125 D1 eso se lee en <c>OffBallTackles</c>, no en <c>Tackles</c>: aquí no
    /// hay portador (<c>Ball.Owner = null</c>), así que ninguna de las dos repeticiones disputa el balón.
    /// </summary>
    [Fact]
    public void ExtraActionOnTackleRepeatsARealOffBallTackleWithinTheSameTickAndCutsAtMaxDepth()
    {
        const string Extra = """[{ "type": "extraAction" }]""";
        var catalog = TestPerks.CatalogWith(("bulldozer", TestPerks.Json("bulldozer", "TACKLE", Extra, scope: "actor")));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "bulldozer" })); // home defender
        var engine = TestPerks.Engine(catalog, setup, maxDepth: 1);
        var tackler = engine.PlayerById(1)!;
        var rival = engine.PlayerById(101)!; // away defender, al alcance.

        tackler.Position = new Vec2(5f, 3f);
        rival.Position = new Vec2(5.5f, 3f);
        engine.Ball.Owner = null;

        Assert.Equal(0, tackler.OffBallTackles);
        Assert.Equal(0, engine.Report.RecursionCuts);

        engine.Effects!.Publish(Tackle(engine, tackler));

        Assert.Equal(2, tackler.OffBallTackles);
        Assert.Equal(0, tackler.Tackles);
        Assert.Equal(1, engine.Report.RecursionCuts);
    }

    /// <summary>
    /// <b>El agujero conocido de la regla «0 = ese puesto no entra nunca» (ADR 0125 D3), fijado aquí para
    /// que nadie vuelva a escribirla como cerrada.</b> Esa regla la cumple <c>Utility.EvaluateTackle</c>,
    /// que es quien DECIDE; <c>MatchEngine.RepeatTackle</c> no decide, ejecuta un efecto de perk
    /// (<c>extraAction</c>) y marca la entrada como sin balón si el objetivo no lleva el balón, sin
    /// consultar el mapa por puesto. Un delantero con ajuste 0 y "Embestida" produce, por tanto, entradas
    /// sin balón.
    ///
    /// <para>Estado: <b>mecanismo real, sin evidencia de activación</b> (Regla F). Los dos perks que lo
    /// disparan —<c>charge</c> y <c>steamroller</c>— no están asignados a ningún no-defensa en
    /// <c>/data</c>, así que hoy no ocurre en ningún lote; el conjunto de referencia no lleva perks. Va con
    /// la decisión abierta de <c>extraAction</c> en <c>docs/pendientes/BE-A.md</c>. Este test no aprueba el
    /// comportamiento: lo deja escrito para que cambiarlo sea deliberado.</para>
    /// </summary>
    [Fact]
    public void RepeatTackleIgnoresThePerPositionMapAndIsTheKnownGapOfTheZeroRule()
    {
        const string Extra = """[{ "type": "extraAction" }]""";
        var catalog = TestPerks.CatalogWith(("bulldozer", TestPerks.Json("bulldozer", "TACKLE", Extra, scope: "actor")));
        Assert.Equal(0, catalog.Ai.OffBallTackleAdjust(Position.Forward));

        var setup = TestPerks.Match(catalog, 1, (6, new[] { "bulldozer" })); // home forward, ajuste 0
        var engine = TestPerks.Engine(catalog, setup, maxDepth: 1);
        var tackler = engine.PlayerById(6)!;
        var rival = engine.PlayerById(106)!;

        tackler.Position = new Vec2(5f, 3f);
        rival.Position = new Vec2(5.5f, 3f);
        engine.Ball.Owner = null;

        engine.Effects!.Publish(Tackle(engine, tackler));

        Assert.True(
            tackler.OffBallTackles > 0,
            "RepeatTackle no mira el mapa por puesto: si esto deja de cumplirse, la regla «0 = nunca» pasó "
                + "a ser cierta de verdad y hay que actualizar la ADR 0125 y docs/pendientes/BE-A.md");
        Assert.Equal(0, tackler.Tackles);
    }

    // ---------------------------------------------------------------- ayudantes: C5

    /// <summary>
    /// Escenario de "Perro de presa": marcador MID (id3, home), con un delantero away (106) cerca y un
    /// defensa away (101) lejos que lleva la etiqueta preferida ("Defender"). Un centrocampista no tiene
    /// preferencia de rol por ninguno de los dos (<c>Marking.Prefers</c> solo empareja MID-MID), así que
    /// sin sesgo gana el más cercano por distancia pura. El resto de jugadores se aparta a un rincón para
    /// que ningún otro marcador les quite el candidato antes de que le toque el turno a id3.
    /// </summary>
    private static void PositionPreferTagScenario(MatchEngine engine)
    {
        DumpEveryoneExcept(engine, new[] { 3, 101, 106 });
        engine.PlayerById(3)!.Position = new Vec2(0f, 0f);
        engine.PlayerById(106)!.Position = new Vec2(1f, 0f); // cerca, sin la etiqueta preferida.
        engine.PlayerById(101)!.Position = new Vec2(5f, 0f); // lejos, CON la etiqueta preferida.
    }

    /// <summary>
    /// Escenario de "Hombre libre": dos defensas away (101, 102) frente a un defensa home (1); 101 está
    /// más cerca (lo preferiría por defecto) y lleva el perk que lo hace evitado.
    /// </summary>
    private static void PositionAvoidedScenario(MatchEngine engine)
    {
        DumpEveryoneExcept(engine, new[] { 1, 101, 102 });
        engine.PlayerById(1)!.Position = new Vec2(0f, 0f);
        engine.PlayerById(101)!.Position = new Vec2(1f, 0f); // más cerca, evitado con el perk.
        engine.PlayerById(102)!.Position = new Vec2(3f, 0f);
    }

    /// <summary>
    /// Escenario de "Guardaespaldas": el hub (id4) y su vinculado "beside" (id5) quedan lejos entre sí a
    /// propósito (10 casillas) para que "cerca del hub" y "cerca del vinculado" sean regiones disjuntas;
    /// un defensa away (101) cerca del HUB y otro (102) cerca del VINCULADO, con una diferencia de coste
    /// (7 contra 2) demasiado grande para el rol pero justo del tamaño del tope de C5 (6 casillas).
    /// </summary>
    private static void PositionProtectLinkedScenario(MatchEngine engine, int[] ids)
    {
        DumpEveryoneExcept(engine, new[] { ids[4], ids[5], 101, 102 });
        engine.PlayerById(ids[4])!.Position = new Vec2(0f, 0f);
        engine.PlayerById(ids[5])!.Position = new Vec2(5f, 0f);
        engine.PlayerById(101)!.Position = new Vec2(1f, 0f); // cerca del hub, lejos del vinculado (dist. 4).
        engine.PlayerById(102)!.Position = new Vec2(6f, 0f); // lejos del hub (6), cerca del vinculado (dist. 1).
    }

    /// <summary>Aparta a todos los jugadores salvo los indicados a un rincón lejano, para que no compitan por los candidatos que el test sí controla.</summary>
    private static void DumpEveryoneExcept(MatchEngine engine, IReadOnlyCollection<int> keep)
    {
        foreach (int id in HomeAndAwayIds())
        {
            if (keep.Contains(id))
            {
                continue;
            }

            var player = engine.PlayerById(id);
            if (player is not null)
            {
                player.Position = new Vec2(15f, 6f);
            }
        }
    }

    private static IEnumerable<int> HomeAndAwayIds()
    {
        for (int i = 0; i <= 9; i++)
        {
            yield return i;
            yield return i + 100;
        }
    }

    private static (MatchEngine Engine, int[] Ids) LinkedMatch((string Id, string Json)? perk)
    {
        var catalog = perk is null ? TestPerks.CatalogWith() : TestPerks.CatalogWith((perk.Value.Id, perk.Value.Json));
        var baseSetup = TestPerks.Match(catalog, 9);
        var ids = baseSetup.Home.Lineup.Slots.Select(s => s.PlayerId).ToArray();
        var setup = WithCells(baseSetup);
        if (perk is { } p)
        {
            setup = setup with { Home = WithPerk(setup.Home, ids[4], p.Id) };
        }

        return (TestPerks.Engine(catalog, setup), ids);
    }

    /// <summary>Misma cuadrícula que Sim.Tests.Perks.LinkTests: hogar del índice 4 con vecino "beside" en el 5.</summary>
    private static readonly Cell[] LinkCells =
    {
        new(0, 2), new(2, 1), new(2, 2), new(3, 2), new(4, 2), new(4, 3), new(5, 2),
    };

    private static MatchSetup WithCells(MatchSetup setup) => setup with
    {
        Home = setup.Home with { Lineup = Relocate(setup.Home.Lineup) },
        Away = setup.Away with { Lineup = Relocate(setup.Away.Lineup) },
    };

    private static Lineup Relocate(Lineup lineup) => new(
        lineup.Slots.Select((slot, i) => slot with { HomeCell = LinkCells[i] }).ToList());

    private static TeamSetup WithPerk(TeamSetup team, int playerId, string perkId) => team with
    {
        Players = team.Players.Select(p => p.Id == playerId ? p with { Perks = new[] { perkId } } : p).ToList(),
    };

    // ---------------------------------------------------------------- ayudantes: C4 (arnés ligero de Utility)

    private static AiWeights ShootRangeWeights(int baseRangeCells, int beyondRangePenaltyPerCell)
    {
        int positions = Enum.GetValues<Position>().Length;
        int actions = Enum.GetValues<PlayerAction>().Length;
        var baseTable = new int[positions, actions];
        var tacticalTable = new int[Enum.GetValues<TacticalState>().Length, actions];
        for (int s = 0; s < tacticalTable.GetLength(0); s++)
        {
            for (int a = 0; a < actions; a++)
            {
                tacticalTable[s, a] = 100;
            }
        }

        var context = new AiContext(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            ShootBaseRangeCells: baseRangeCells,
            ShootInRangeBonus: 0,
            ShootBeyondRangePenaltyPerCell: beyondRangePenaltyPerCell,
            ShootDistancePenaltyPerCell: 0,
            ShootAnglePenaltyPerRow: 0,
            TackleDistanceMaxCells: 0f,
            TackleOutOfReachPenalty: 0,
            TackleBallCarrierBonus: 0,
            RetreatDistanceBonusPerCell: 0,
            RetreatAtHomePenalty: 0);

        var shifts = new BlockShift[Enum.GetValues<TacticalState>().Length];
        return new AiWeights(baseTable, tacticalTable, TestData.OffBallTackleAdjust(), context, shifts);
    }

    private static UtilityContext ShootContext(AiWeights weights, MatchPlayer shooter)
    {
        var ball = new Ball { InterceptAttempted = new bool[1], Position = shooter.Position };
        shooter.Index = 0;
        var context = new UtilityContext(new[] { shooter }, ball, weights, Catalog.Tuning.ActionZone, Catalog.Tuning.Pass.InterceptRadiusCells);
        context.TacticalStates[0] = TacticalState.InPossession;
        context.TacticalStates[1] = TacticalState.InPossession;
        context.NearestToBall[0] = shooter;
        return context;
    }

    private static UtilityRow Row(IReadOnlyList<UtilityRow> rows, PlayerAction action)
    {
        foreach (var row in rows)
        {
            if (row.Action == action)
            {
                return row;
            }
        }

        throw new InvalidOperationException($"la tabla de utilidad no contiene la acción {action}");
    }

    // ---------------------------------------------------------------- ayudantes comunes

    private static MatchEvent MatchStart(MatchEngine engine) => new(
        EventType.MatchStart, engine.Tick, -1, -1, -1, -1,
        new Cell(0, 0), Zone.Middle, MatchPhase.Kickoff, 0, 0, "kickoff");

    private static MatchEvent Tackle(MatchEngine engine, MatchPlayer owner) => new(
        EventType.Tackle, engine.Tick, owner.Team, owner.Id, -1, -1,
        owner.HomeCell, Zone.Own, MatchPhase.OpenPlay, engine.BiasFor(0), 0, "attempted");

    private static MatchEvent Shot(MatchEngine engine, MatchPlayer owner) => new(
        EventType.Shot, engine.Tick, owner.Team, owner.Id, -1, -1,
        owner.HomeCell, Zone.Own, MatchPhase.OpenPlay, engine.BiasFor(0), 0, "attempted");
}
