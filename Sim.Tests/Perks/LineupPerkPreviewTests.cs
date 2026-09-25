using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Previsualización de perks desde la alineación (RF-012d, RF-040..045): la pantalla de Equipo tiene que
/// poder decir, antes del partido, qué perks enciende y apaga una colocación. Lo que se prueba aquí es
/// que <see cref="LineupPerkPreviewer"/> responde lo mismo que responderá el motor —de ahí la
/// comprobación cruzada del final— y que calla cuando la respuesta no depende solo de la alineación.
/// <para>
/// La alineación es la de por defecto (2-3-1, campo de siete filas, sucesora de la ADR 0103): GK (0,3);
/// DEF (2,2),(2,4); MID (3,3),(4,2),(4,4); FWD (6,3). Los jugadores se escriben a mano y con ids
/// consecutivos para que <see cref="Lineup.Default"/> reparta las casillas de forma predecible.
/// </para>
/// </summary>
public sealed class LineupPerkPreviewTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Casillas de la alineación por defecto, por id de jugador (1..7).</summary>
    private static readonly Cell[] DefaultCells =
    {
        new(0, 3), new(2, 2), new(2, 4), new(3, 3), new(4, 2), new(4, 4), new(6, 3),
    };

    /// <summary>
    /// <c>startsIn(owner,'Middle')</c> mira la columna de la casilla-hogar: 3-5 es el centro del campo y
    /// 6-7 el tercio rival, así que el mismo perk se enciende en el centrocampista y se apaga en el
    /// delantero sin que ninguno de los dos cambie de atributos.
    /// </summary>
    [Fact]
    public void CaptainsVoiceIsActiveInTheMiddleAndInactiveUpFront()
    {
        var preview = Preview((5, "captains_voice"), (7, "captains_voice"));

        Assert.Equal(LineupPerkStatus.Active, Status(preview, 5, "captains_voice"));
        Assert.Equal(LineupPerkStatus.Inactive, Status(preview, 7, "captains_voice"));
    }

    /// <summary>
    /// <c>startsOn</c> mira la fila: el centro es la fila 3 (única, con Rows impar), y cualquier otra es
    /// banda. Es el eje que el jugador no adivina mirando el campo, y por eso la pantalla lo tiene que
    /// decir.
    /// </summary>
    [Fact]
    public void FlankSpecialistReadsTheRowOfTheHomeCell()
    {
        var preview = Preview((5, "flank_specialist"), (4, "flank_specialist"));

        Assert.Equal(LineupPerkStatus.Active, Status(preview, 5, "flank_specialist"));
        Assert.Equal(LineupPerkStatus.Inactive, Status(preview, 4, "flank_specialist"));
    }

    /// <summary>El delantero de la alineación por defecto es el único que empieza en el tercio rival.</summary>
    [Fact]
    public void MarrowThirstIsActiveOnlyInTheAttackingThird()
    {
        var preview = Preview((7, "marrow_thirst"), (6, "marrow_thirst"));

        Assert.Equal(LineupPerkStatus.Active, Status(preview, 7, "marrow_thirst"));
        Assert.Equal(LineupPerkStatus.Inactive, Status(preview, 6, "marrow_thirst"));
    }

    /// <summary>
    /// <c>linked(owner,'ahead')</c> se resuelve entre casillas-hogar: el defensa de (2,2) tiene delante
    /// al centrocampista de (3,3), y el delantero de (6,3) no tiene a nadie por delante.
    /// </summary>
    [Fact]
    public void SpearpointNeedsATeammateAhead()
    {
        var preview = Preview((2, "spearpoint"), (7, "spearpoint"));

        Assert.Equal(LineupPerkStatus.Active, Status(preview, 2, "spearpoint"));
        Assert.Equal(LineupPerkStatus.Inactive, Status(preview, 7, "spearpoint"));
    }

    /// <summary>
    /// <c>teammatesWithTag</c> dentro de una comparación con literal también se decide con la alineación:
    /// cuenta titulares, que es lo que cuenta el motor al arrancar el partido.
    /// </summary>
    [Fact]
    public void TeammateCountsAreDecidedByWhoIsInTheLineup()
    {
        var withTwo = Preview(Team(brutes: 2), (4, "blood_tithe"));
        var withOne = Preview(Team(brutes: 1), (4, "blood_tithe"));

        Assert.Equal(LineupPerkStatus.Active, Status(withTwo, 4, "blood_tithe"));
        Assert.Equal(LineupPerkStatus.Inactive, Status(withOne, 4, "blood_tithe"));
    }

    /// <summary>
    /// Un perk cuya condición depende de cómo vaya el partido no se previsualiza: prometer una activación
    /// que el partido puede desmentir sería peor que no decir nada (RF-012d).
    /// </summary>
    [Fact]
    public void AConditionThatDependsOnTheMatchIsNotPreviewed()
    {
        // iron_studs: "zone(actor) == 'Opposing'" -- la zona viva, no la casilla de salida.
        var preview = Preview((5, "iron_studs"), (5, "captains_voice"));

        Assert.DoesNotContain(preview, p => p.PerkId == "iron_studs");
        Assert.Contains(preview, p => p.PerkId == "captains_voice");
    }

    /// <summary>Un perk sin condición no dice nada de la colocación, así que tampoco se previsualiza.</summary>
    [Fact]
    public void APerkWithoutConditionIsNotPreviewed()
    {
        // hot_blooded es la habilidad racial del orco: se dispara siempre, sin condición.
        Assert.Empty(Preview((5, "hot_blooded")));
    }

    /// <summary>Un suplente no está en ninguna casilla: no tiene colocación de la que hablar.</summary>
    [Fact]
    public void ASubstituteIsNotPreviewed()
    {
        var preview = Preview((8, "flank_specialist"), (5, "flank_specialist"));

        Assert.Equal(new[] { 5 }, preview.Select(p => p.PlayerId).ToArray());
    }

    /// <summary>Orden determinista de salida: id de jugador ascendente y, dentro, id de perk (RT-041).</summary>
    [Fact]
    public void TheOutputIsOrderedByPlayerThenPerk()
    {
        var players = Team();
        players[1] = players[1] with { Perks = new[] { "spearpoint", "captains_voice" } };
        players[6] = players[6] with { Perks = new[] { "marrow_thirst" } };

        var preview = LineupPerkPreviewer.Preview(LineupOf(players), players, Catalog);

        Assert.Equal(
            new[] { (2, "captains_voice"), (2, "spearpoint"), (7, "marrow_thirst") },
            preview.Select(p => (p.PlayerId, p.PerkId)).ToArray());
    }

    /// <summary>
    /// Comprobación cruzada: lo que la previsualización promete es lo que el motor hace. Un perk activo
    /// deja una activación normal en el informe; uno inactivo, o una con sufijo <c>":else"</c> —si el
    /// perk declara <c>elseEffects</c>— o ninguna. Sin este test la previsualización sería una segunda
    /// implementación de las condiciones, libre de divergir de la primera.
    /// </summary>
    [Fact]
    public void ThePreviewAgreesWithWhatTheEngineDoesAtKickoff()
    {
        var setup = TestMatches.Reference(Catalog, 11);
        var assignments = new (Cell Cell, string Perk)[]
        {
            (new Cell(4, 2), "flank_specialist"),   // banda: activo, con efecto normal
            (new Cell(3, 3), "flank_specialist"),   // carril central: inactivo, y tiene elseEffects
            (new Cell(6, 3), "captains_voice"),     // tercio rival: inactivo, y NO tiene elseEffects
            (new Cell(2, 2), "spearpoint"),         // tiene a alguien delante: activo
        };

        var home = WithPerksByCell(setup.Home, assignments);
        var result = Simulator.Run(setup with { Home = home }, 11, Catalog, new SimConfig(CollectLog: false));
        var preview = LineupPerkPreviewer.Preview(home.Lineup, home.Players, Catalog);

        Assert.Equal(assignments.Length, preview.Count);
        foreach (var entry in preview)
        {
            // El disparador se lee del perk y no se da por supuesto (ADR 0149 / RF-069c): desde que un
            // perk puede colgarse de un evento de fútbol en vez del pitido inicial, filtrar por
            // MATCH_START dejaba la lista vacía y el test pasaba a comprobar nada. Lo que se protege aquí
            // es que la PREVISUALIZACIÓN y el MOTOR coincidan sobre la misma condición, y eso no depende
            // de cuándo se dispare.
            var trigger = Catalog.Perks.Get(entry.PerkId).Trigger;
            var activations = result.Report.PerkActivations
                .Where(a => a.PerkId == entry.PerkId && a.OwnerId == entry.PlayerId && a.EventType == trigger)
                .ToList();

            if (entry.Status == LineupPerkStatus.Active)
            {
                Assert.All(activations, a => Assert.DoesNotContain(":else", a.Detail, StringComparison.Ordinal));

                // Sólo un perk de MATCH_START tiene garantizada al menos una activación en cualquier
                // partido; uno colgado de una entrada o de un regate depende de que la jugada ocurra, y
                // exigirle presencia convertiría este test en uno estadístico disfrazado.
                if (trigger == EventType.MatchStart)
                {
                    Assert.NotEmpty(activations);
                }
            }
            else
            {
                Assert.All(activations, a => Assert.EndsWith(":else", a.Detail, StringComparison.Ordinal));
            }
        }

        // Y los dos casos del contraste están de verdad representados, no ha salido todo activo.
        Assert.Contains(preview, p => p.Status == LineupPerkStatus.Active);
        Assert.Contains(preview, p => p.Status == LineupPerkStatus.Inactive);
    }

    // ------------------------------------------------------------------ ayudantes

    private static IReadOnlyList<LineupPerkPreview> Preview(params (int PlayerId, string PerkId)[] perks) =>
        Preview(Team(), perks);

    // ------------------------------------------------------------------ BB-J: el conteo del tooltip

    /// <summary>
    /// BB-J: no basta con decir "no se cumple". `pack_mentality` (borrado del catálogo, revisor) pedía
    /// <c>teammatesWithTag(owner,'Brute') &gt; 2</c>; su sustituto <c>blood_tithe</c> cuenta la misma
    /// etiqueta con <c>&gt; 1</c> (DOS brutos además del portador), y el jugador tiene que poder ver
    /// cuántos lleva antes del partido (RF-012d) tanto si el requisito ya se cumple como si le falta.
    /// </summary>
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(2, 2, true)]
    public void BloodTitheReportsHowManyBrutesYouHaveAndHowManyYouNeed(int brutes, int expectedCurrent, bool expectedMet)
    {
        var preview = Preview(Team(brutes), (4, "blood_tithe"));
        var requirement = Requirement(preview, 4, "blood_tithe");

        Assert.NotNull(requirement);
        Assert.Equal("teammatesWithTag", requirement!.Function);
        Assert.Equal("Brute", requirement.Tag);
        Assert.Equal(expectedCurrent, requirement.Current);
        Assert.Equal(2, requirement.Required); // "> 1" son dos
        Assert.Equal(expectedMet, requirement.Met);
        Assert.Equal(requirement.Met ? LineupPerkStatus.Active : LineupPerkStatus.Inactive, Status(preview, 4, "blood_tithe"));
    }

    /// <summary>
    /// `first_touch_school` pide <c>teammatesWithTag(owner,'Fine') &gt; 1</c>: el "1 de 2 Finos" del
    /// encargo. Se comprueba que el umbral se traduce a DOS, no a uno.
    /// </summary>
    [Fact]
    public void GreaterThanOneMeansYouNeedTwo()
    {
        var requirement = Requirement(Preview(Team(), (4, "first_touch_school")), 4, "first_touch_school");

        Assert.NotNull(requirement);
        Assert.Equal("Fine", requirement!.Tag);
        Assert.Equal(2, requirement.Required);
        Assert.Equal(0, requirement.Current);
        Assert.False(requirement.Met);
    }

    /// <summary>Un <c>hasTag</c> también es contable: se tiene o no se tiene, 0 de 1 o 1 de 1.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void HasTagIsReportedAsZeroOrOneOfOne(int brutes, int expectedCurrent)
    {
        // El id 5 (índice 4) es el primero que Team() marca como Brute.
        var requirement = Requirement(Preview(Team(brutes), (5, "bruised_knuckles")), 5, "bruised_knuckles");

        Assert.NotNull(requirement);
        Assert.Equal("hasTag", requirement!.Function);
        Assert.Equal("Brute", requirement.Tag);
        Assert.Equal(expectedCurrent, requirement.Current);
        Assert.Equal(1, requirement.Required);
    }

    /// <summary>
    /// Una condición sin nada contable —solo geometría de colocación— no inventa un requisito: la lista
    /// sale vacía y la pantalla no tiene nada que enseñar.
    /// </summary>
    [Fact]
    public void ConditionsWithNothingToCountReportNoRequirement()
    {
        var preview = Preview(Team(), (5, "captains_voice"), (7, "captains_voice"));
        Assert.Empty(preview.Single(p => p.PlayerId == 5 && p.PerkId == "captains_voice").Requirements);
    }

    private static LineupPerkRequirement? Requirement(
        IReadOnlyList<LineupPerkPreview> preview, int playerId, string perkId) =>
        preview.FirstOrDefault(p => p.PlayerId == playerId && p.PerkId == perkId)?.Requirements.FirstOrDefault();

    private static IReadOnlyList<LineupPerkPreview> Preview(
        List<PlayerDefinition> players, params (int PlayerId, string PerkId)[] perks)
    {
        for (int i = 0; i < players.Count; i++)
        {
            var assigned = perks.Where(p => p.PlayerId == players[i].Id).Select(p => p.PerkId).ToArray();
            if (assigned.Length > 0)
            {
                players[i] = players[i] with { Perks = assigned };
            }
        }

        return LineupPerkPreviewer.Preview(LineupOf(players), players, Catalog);
    }

    private static LineupPerkStatus? Status(IReadOnlyList<LineupPerkPreview> preview, int playerId, string perkId) =>
        preview.FirstOrDefault(p => p.PlayerId == playerId && p.PerkId == perkId)?.Status;

    /// <summary>Alineación por defecto de los siete primeros jugadores; el resto quedan en el banquillo.</summary>
    private static Lineup LineupOf(List<PlayerDefinition> players) => Lineup.Default(players.Take(7).ToList());

    /// <summary>
    /// Siete titulares (ids 1..7, uno por casilla de la alineación por defecto) y dos suplentes. Los
    /// <paramref name="brutes"/> primeros centrocampistas llevan la etiqueta Brute, para los perks que
    /// cuentan compañeros por etiqueta.
    /// </summary>
    private static List<PlayerDefinition> Team(int brutes = 0)
    {
        var positions = new[]
        {
            Position.Goalkeeper, Position.Defender, Position.Defender,
            Position.Midfielder, Position.Midfielder, Position.Midfielder,
            Position.Forward, Position.Midfielder, Position.Defender,
        };

        var attributes = new Attributes(50, 50, 50, 50, 50);
        var players = new List<PlayerDefinition>(positions.Length);
        int brutesLeft = brutes;
        for (int i = 0; i < positions.Length; i++)
        {
            var tags = new List<string> { "Neutral", positions[i].ToString() };

            // Los Brute se reparten desde el final de los titulares hacia atrás para que el portador del
            // perk que los cuenta (id 4) nunca sea uno de ellos: la función excluye al propio jugador.
            if (brutesLeft > 0 && i is 4 or 5)
            {
                tags.Add("Brute");
                brutesLeft--;
            }

            players.Add(new PlayerDefinition(
                i + 1, "p" + (i + 1), Race.Human, positions[i], Rarity.Common, 1,
                attributes, Array.Empty<Trait>(), tags, PhysicalState.Healthy));
        }

        return players;
    }

    /// <summary>Asigna cada perk al titular que ocupa esa casilla-hogar.</summary>
    private static TeamSetup WithPerksByCell(TeamSetup team, (Cell Cell, string Perk)[] assignments)
    {
        var players = new List<PlayerDefinition>(team.Players.Count);
        foreach (var player in team.Players)
        {
            var slot = team.Lineup.Slots.FirstOrDefault(s => s.PlayerId == player.Id);
            var perk = slot is null
                ? null
                : assignments.Where(a => a.Cell == slot.HomeCell).Select(a => a.Perk).FirstOrDefault();
            players.Add(perk is null ? player : player with { Perks = new[] { perk } });
        }

        return team with { Players = players };
    }

    /// <summary>Comprueba que la alineación por defecto sigue repartiendo las casillas que suponen estos tests.</summary>
    [Fact]
    public void TheDefaultLineupStillPlacesEveryoneWhereTheseTestsAssume()
    {
        var lineup = LineupOf(Team());
        foreach (var slot in lineup.Slots)
        {
            Assert.Equal(DefaultCells[slot.PlayerId - 1], slot.HomeCell);
        }
    }
}
