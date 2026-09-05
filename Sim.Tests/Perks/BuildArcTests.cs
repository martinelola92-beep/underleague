using Underleague.Sim.Data;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Arcos de build y profundidad nativa (ADR 0051): perks <b>maestros</b> que exigen media línea y cierran
/// otra, y el acto nativo con el que el pool mejora a lo largo de la run.
///
/// <para>Lo que se comprueba aquí es la <b>validación de carga</b> y las <b>invariantes del catálogo</b>.
/// Que el pool las respete es de <c>PerkPoolArcTests</c>; que la pantalla las enseñe, de la vista.</para>
/// </summary>
public sealed class BuildArcTests
{
    private const string OneEffect =
        """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 3, "duration": "match" }]""";

    private static string Master(
        string id,
        string family = "wall",
        string requiresFamily = "wall",
        int count = 2,
        string blocks = """{ "families": ["aim"], "perks": [] }""",
        int minAct = 2) =>
        TestPerks.Json(
            id,
            "MATCH_START",
            OneEffect,
            rarity: "rare",
            kind: "conditional",
            minAct: minAct,
            family: family,
            requiresPerks: $$"""{ "family": "{{requiresFamily}}", "count": {{count}} }""",
            blocksPerks: blocks);

    /// <summary>Piezas sueltas de una línea, para que un maestro que la exige sea alcanzable.</summary>
    private static (string Id, string Json)[] Line(string family, int members)
    {
        var perks = new List<(string, string)>();
        for (int i = 0; i < members; i++)
        {
            string id = family + "_piece" + i;
            perks.Add((id, TestPerks.Json(id, "MATCH_START", OneEffect, family: family)));
        }

        return perks.ToArray();
    }

    // ------------------------------------------------------------------ formato

    [Fact]
    public void MasterDeclaresWhatItRequiresAndWhatItCloses()
    {
        var catalog = TestPerks.CatalogWith(
            Line("wall", 3).Append(("wall_master", Master("wall_master"))).ToArray());

        var master = catalog.Perks.Get("wall_master");
        Assert.True(master.IsMaster);
        Assert.Equal("wall", master.Requires!.Family);
        Assert.Equal(2, master.Requires.Count);
        Assert.Equal(new[] { "aim" }, master.Blocks.Families);
        Assert.Equal(2, master.MinAct);
    }

    [Fact]
    public void AnUnknownFamilyIsALoadError()
    {
        var error = Assert.Throws<DataException>(() => TestPerks.Load(
            "stray", TestPerks.Json("stray", "MATCH_START", OneEffect, family: "nonexistent")));
        Assert.Contains("línea desconocida", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMasterThatRequiresMorePiecesThanItsLineHasIsRejected()
    {
        // Alcanzabilidad (ADR 0051): la línea solo tiene DOS piezas que no son maestros y el maestro pide
        // tres, así que nadie podría cobrarlo nunca.
        var error = Assert.Throws<DataException>(() => TestPerks.CatalogWith(
            Line("wall", 2).Append(("wall_master", Master("wall_master", count: 3))).ToArray()));
        Assert.Contains("inalcanzable", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// El ciclo (dos maestros que se exigen el uno al otro) lo descarta la misma comprobación: al contar
    /// solo piezas que no son maestros, un maestro nunca puede ser el escalón de otro.
    /// </summary>
    [Fact]
    public void TwoMastersCannotBeEachOthersRequirement()
    {
        var error = Assert.Throws<DataException>(() => TestPerks.CatalogWith(
            ("wall_master", Master("wall_master", family: "wall", requiresFamily: "craft", count: 2)),
            ("craft_master", Master(
                "craft_master",
                family: "craft",
                requiresFamily: "wall",
                count: 2,
                blocks: """{ "families": ["butchery"], "perks": [] }"""))));
        Assert.Contains("inalcanzable", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMasterCannotRequireTheLineItCloses()
    {
        var error = Assert.Throws<DataException>(() => TestPerks.CatalogWith(
            Line("wall", 3)
                .Append(("wall_master", Master(
                    "wall_master", blocks: """{ "families": ["wall"], "perks": [] }""")))
                .ToArray()));
        Assert.Contains("inalcanzable", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMasterCannotAppearInActOne()
    {
        var error = Assert.Throws<DataException>(() => TestPerks.CatalogWith(
            Line("wall", 3).Append(("wall_master", Master("wall_master", minAct: 1))).ToArray()));
        Assert.Contains("acto 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAMasterCanCloseALine()
    {
        var error = Assert.Throws<DataException>(() => TestPerks.Load(
            "sneaky",
            TestPerks.Json(
                "sneaky", "MATCH_START", OneEffect, blocksPerks: """{ "families": ["aim"], "perks": [] }""")));
        Assert.Contains("solo un maestro", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMasterHasToCloseSomething()
    {
        var error = Assert.Throws<DataException>(() => TestPerks.CatalogWith(
            Line("wall", 3)
                .Append(("wall_master", Master("wall_master", blocks: """{ "families": [], "perks": [] }""")))
                .ToArray()));
        Assert.Contains("cerrar algo", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockingAPerkThatDoesNotExistIsALoadError()
    {
        var error = Assert.Throws<DataException>(() => TestPerks.CatalogWith(
            Line("wall", 3)
                .Append(("wall_master", Master(
                    "wall_master", blocks: """{ "families": [], "perks": ["ghost"] }""")))
                .ToArray()));
        Assert.Contains("no existe", error.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ el catálogo real

    /// <summary>
    /// ADR 0051: los maestros son entre el 5% y el 10% del catálogo. Si crecen más, el catálogo deja de
    /// ser un roguelite de piezas sueltas y se convierte en un árbol de talentos.
    /// </summary>
    [Fact]
    public void MastersAreASmallShareOfTheCatalog()
    {
        var catalog = TestData.LoadCatalog();
        int share = catalog.Perks.MasterSharePercent;
        Assert.InRange(share, 3, 10);
        Assert.NotEmpty(catalog.Perks.Masters);
    }

    /// <summary>
    /// Cada línea del catálogo tiene que tener miembros suficientes para que su maestro sea un objetivo y
    /// no un imposible, y ninguna puede ser tan grande que llevar media línea sea automático.
    /// </summary>
    [Fact]
    public void EveryLineHasEnoughMembersAndNotTooMany()
    {
        var catalog = TestData.LoadCatalog();
        foreach (string family in catalog.Perks.Arcs.Families)
        {
            var members = catalog.Perks.MembersOf(family);
            Assert.True(members.Count >= 4, $"la línea '{family}' solo tiene {members.Count} perks");
            Assert.True(members.Count <= 12, $"la línea '{family}' tiene {members.Count} perks: es media familia entera");
        }
    }

    /// <summary>Toda línea declarada tiene su maestro, y todo maestro corona la línea a la que pertenece.</summary>
    [Fact]
    public void EveryLineHasItsMasterAndEveryMasterItsLine()
    {
        var catalog = TestData.LoadCatalog();
        foreach (var master in catalog.Perks.Masters)
        {
            Assert.Equal(master.Family, master.Requires!.Family);
            Assert.True(catalog.Perks.Arcs.HasFamily(master.Family));
        }

        foreach (string family in catalog.Perks.Arcs.Families)
        {
            Assert.Contains(catalog.Perks.Masters, m => string.Equals(m.Family, family, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// La descripción generada de un maestro dice qué exige y qué cierra, en todos los idiomas y sin una
    /// sola frase escrita a mano (RT-035, RF-012d): es la única forma de que el bloqueo se lea antes de
    /// aceptar, y un perk no se puede retirar (RF-072).
    /// </summary>
    [Fact]
    public void GeneratedDescriptionSaysWhatItRequiresAndWhatItCloses()
    {
        var catalog = TestData.LoadCatalog();
        foreach (var master in catalog.Perks.Masters)
        {
            foreach (string language in catalog.Localization.Languages)
            {
                var templates = catalog.Localization.Get(language);
                string text = DescriptionGenerator.Describe(master, templates, catalog.Perks);
                string requiredFamily = templates.Get("families", master.Requires!.Family);
                Assert.Contains(requiredFamily, text, StringComparison.Ordinal);
                Assert.Contains(
                    master.Requires.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    text,
                    StringComparison.Ordinal);

                foreach (string blocked in master.Blocks.Families)
                {
                    Assert.Contains(templates.Get("families", blocked), text, StringComparison.Ordinal);
                }
            }
        }
    }

    // ------------------------------------------------------------------ profundidad nativa

    [Fact]
    public void TheDepthCurveDecaysAboveTheNativeActAndIsRareBelowIt()
    {
        var depth = TestData.LoadCatalog().Perks.Arcs.Depth;

        // En su acto, peso completo; por encima, decae.
        Assert.Equal(depth.FullPercent, depth.WeightPercent(minAct: 2, act: 2));
        Assert.True(depth.WeightPercent(2, 3) < depth.WeightPercent(2, 2));

        // Por debajo, fuera de profundidad: posible pero raro, que es lo que lo hace memorable.
        int outOfDepth = depth.WeightPercent(minAct: 3, act: 1);
        Assert.InRange(outOfDepth, 0, depth.FullPercent / 4);
    }

    /// <summary>
    /// Cada perk y cada objeto declaran su acto nativo (ADR 0051), y lo letal y lo raro no abren la run:
    /// el acto 1 es el taller (ADR 0043).
    /// </summary>
    [Fact]
    public void LethalPerksAreNotAnActOneReward()
    {
        var catalog = TestData.LoadCatalog();
        foreach (var perk in catalog.Perks.All)
        {
            Assert.InRange(perk.MinAct, 1, 3);
            Assert.InRange(perk.Frequency, 10, 500);
            if (perk.Lethal)
            {
                Assert.True(perk.MinAct >= 2, $"'{perk.Id}' mata y su acto nativo es el 1");
            }
        }
    }
}
