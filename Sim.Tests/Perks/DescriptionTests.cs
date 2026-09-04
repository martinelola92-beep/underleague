using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Descripciones generadas desde el efecto (RT-035). El texto exacto es parte del contrato: si alguien
/// cambia un valor del perk o una plantilla, el test cae y la descripción no puede quedarse obsoleta,
/// que es justo lo que RT-035 quiere hacer imposible.
/// <para>
/// Los perks de ejemplo se declaran aquí y no se leen de <c>data/perks/</c>: el catálogo de lanzamiento
/// lo escribe el paquete T y estos tests comprueban el **generador**, no el catálogo.
/// </para>
/// </summary>
public sealed class DescriptionTests
{
    private static readonly Catalog Catalog = TestPerks.CatalogWith();

    [Fact]
    public void ConditionAndLimitInSpanish() => Assert.Equal(
        "Al entrar, si el jugador es Bruto y si el criterio es menor que 0, "
            + "el jugador +3 de fuerza durante la jugada (máximo 2 por partido).",
        Describe("es", Bloodlust()));

    [Fact]
    public void ConditionAndLimitInEnglish() => Assert.Equal(
        "On a tackle, if the player is Brute and if the referee bias is less than 0, "
            + "the player +3 strength for the play (max 2 per match).",
        Describe("en", Bloodlust()));

    [Fact]
    public void ElseEffectsAndProbabilitiesAreDescribed()
    {
        var perk = TestPerks.Load("showboat", TestPerks.Json(
            "showboat",
            "DRIBBLE_ATTEMPTED",
            """[{ "type": "modifyProbability", "target": "actor", "probability": "dribble", "value": 15, "duration": "play" }]""",
            condition: "hasTag(owner, 'Fine')",
            elseEffects: """[{ "type": "modifyProbability", "target": "actor", "probability": "dribble", "value": -15, "duration": "play" }]"""));

        Assert.Equal(
            "Al encarar, si el portador es Fino, el jugador suma +15% a su probabilidad de regate; "
                + "si no, el jugador suma -15% a su probabilidad de regate.",
            Describe("es", perk));
    }

    [Fact]
    public void CancelEventNamesTheTriggeringEvent()
    {
        var perk = TestPerks.Load("lucky_charm", TestPerks.Json(
            "lucky_charm",
            "INJURY",
            """[{ "type": "cancelEvent" }]""",
            scope: "target",
            rarity: "legendary",
            kind: "ruleBreaker",
            limit: """{ "per": "match", "times": 1 }"""));

        Assert.Equal("En una lesión, anula la lesión (máximo 1 por partido).", Describe("es", perk));
        Assert.Equal("On an injury, cancels the injury (max 1 per match).", Describe("en", perk));
    }

    [Fact]
    public void CounterDivisorUsesItsOwnTemplate()
    {
        var perk = TestPerks.Load("bookworm", TestPerks.Json(
            "bookworm",
            "MATCH_START",
            """
            [{ "type": "modifyAttribute", "target": "owner", "attribute": "technique",
               "valuePerCounter": 1, "counter": "passes", "counterDivisor": 25, "maxValue": 6, "duration": "match" }]
            """));

        Assert.Equal(
            "Al empezar el partido, el portador +1 de técnica por cada 25 de pase (máximo 6) durante el partido.",
            Describe("es", perk));
    }

    /// <summary>
    /// RT-035 sobre el modificador por par (ADR 0021): la descripción tiene que decir **hacia quién**
    /// mejora el pase. "Mejora el pase" a secas sería una descripción que miente, porque el bono no vale
    /// para los demás pases del portador.
    /// </summary>
    [Fact]
    public void PairedProbabilityNamesTheLinkedPartner()
    {
        var perk = LinkedPasser();
        Assert.Equal(
            "Al empezar el partido, probabilidad de pase +10% hacia el compañero de su columna.",
            Describe("es", perk));
        Assert.Equal(
            "When the match starts, pass chance +10% towards the partner in their column.",
            Describe("en", perk));
    }

    [Fact]
    public void SeveralLinksAreJoined()
    {
        var perk = TestPerks.Load("hub", TestPerks.Json(
            "hub",
            "MATCH_START",
            """[{ "type": "modifyProbability", "target": "linked", "probability": "pass", "value": 5, "duration": "match" }]""",
            axis: "alignment",
            links: """["ahead", "behind"]"""));

        Assert.Equal(
            "Al empezar el partido, probabilidad de pase +5% hacia el compañero de delante y compañero de detrás.",
            Describe("es", perk));
    }

    /// <summary>
    /// Las funciones nuevas (§1.5) son describibles, y las unidades internas no aparecen: los ticks del
    /// derribo se dicen como "más tiempo" (docs/estilo-descripciones.md).
    /// </summary>
    [Theory]
    [InlineData("startsIn(owner, 'AttackingThird')", "si el portador empieza en el tercio rival")]
    [InlineData("startsOn(owner, 'LeftFlank')", "si el portador empieza en la banda izquierda")]
    [InlineData("linked(owner, 'behind')", "si el portador tiene compañero de detrás")]
    [InlineData("nearAlly(owner, 'Brute', 2)", "si el portador tiene cerca un Bruto a 2 casillas")]
    [InlineData("nearOpponent(actor, 'Fine', 3)", "si el jugador tiene cerca un Fino rival a 3 casillas")]
    [InlineData("stat(owner, 'goals') >= 2", "si el portador lleva al menos 2 goles")]
    [InlineData("stat(actor, 'tacklesWon') < 1", "si el jugador lleva menos de 1 entradas ganadas")]
    [InlineData("distanceToGoal(owner) < 3", "si el portador está a menos de 3 casillas de portería")]
    public void NewConditionFunctionsAreDescribed(string condition, string expected)
    {
        var perk = TestPerks.Load("cond", TestPerks.Json(
            "cond",
            "MATCH_START",
            """[{ "type": "modifyAttribute", "target": "owner", "attribute": "speed", "value": 1, "duration": "match" }]""",
            axis: "proximity",
            links: """["behind"]""",
            condition: condition));

        Assert.Equal($"Al empezar el partido, {expected}, el portador +1 de velocidad durante el partido.", Describe("es", perk));
    }

    [Fact]
    public void CounterScaledProbabilityIsDescribedInPercentagePoints()
    {
        var perk = TestPerks.Load("grower", TestPerks.Json(
            "grower",
            "MATCH_START",
            """
            [{ "type": "modifyProbability", "target": "owner", "probability": "intercept",
               "valuePerCounter": 5, "counter": "matches", "maxValue": 25, "duration": "match" }]
            """,
            axis: "accumulation",
            accumulates: true));

        Assert.Equal(
            "Al empezar el partido, el portador suma +5% a su probabilidad de interceptar por cada partido (máximo 25%).",
            Describe("es", perk));
    }

    /// <summary>
    /// <c>addCounter</c> es contabilidad interna pura (V-1, `docs/pendientes.md`): el contador que
    /// incrementa ya lo narra el efecto emparejado con "por cada partido", así que el generador no lo
    /// describe como una frase aparte y no debe aparecer en el texto final.
    /// </summary>
    [Fact]
    public void AddCounterEffectsAreNotDescribed()
    {
        var perk = TestPerks.Load("grinder", TestPerks.Json(
            "grinder",
            "MATCH_START",
            """
            [{ "type": "modifyProbability", "target": "owner", "probability": "intercept",
               "valuePerCounter": 5, "counter": "matches", "maxValue": 25, "duration": "match" },
             { "type": "addCounter", "counter": "matches", "value": 1 }]
            """,
            axis: "accumulation",
            accumulates: true));

        Assert.Equal(
            "Al empezar el partido, el portador suma +5% a su probabilidad de interceptar por cada partido (máximo 25%).",
            Describe("es", perk));
    }

    /// <summary>
    /// RT-035: la descripción es una sola frase (`docs/estilo-descripciones.md`, V-1). Ni el disparador ni
    /// el objetivo se anteponen con dos puntos: se integran en la frase con comas, y el resultado empieza
    /// en mayúscula y termina en punto.
    /// </summary>
    [Fact]
    public void RacialAbilitiesReadAsTheAdrDescribesThem()
    {
        Assert.Equal(
            "Al terminar el partido, el portador gana un 25% más de experiencia.",
            Describe("es", Catalog.Perks.Get("quick_learner")));
        Assert.Equal(
            "Al empezar el partido, el portador deja al rival derribado más tiempo con sus entradas.",
            Describe("es", Catalog.Perks.Get("hot_blooded")));
        Assert.Equal(
            "Al empezar el partido, el portador suma +5% a su resistencia a las entradas.",
            Describe("es", Catalog.Perks.Get("elf_touch")));
        Assert.Equal(
            "Al empezar el partido, el portador no puede ser desplazado por empujones.",
            Describe("es", Catalog.Perks.Get("roots")));
        Assert.Equal(
            "When the match starts, the holder cannot be shoved out of position.",
            Describe("en", Catalog.Perks.Get("roots")));
        Assert.Equal(
            "Al empezar el partido, el portador no entra en duelo cuando pierde a un vinculado "
                + "y el portador no sufre penalización por lesiones leves.",
            Describe("es", Catalog.Perks.Get("numb")));
    }

    /// <summary>
    /// RT-035: todo perk de <c>data/perks/</c> es describible en los dos idiomas. Se recorren los ficheros
    /// uno a uno en vez de cargar el catálogo entero porque el paquete T está reescribiéndolo al formato
    /// de §1.4; los que todavía no lo cumplen los rechaza el cargador y este test lo hace explícito en el
    /// mensaje, en lugar de caer con un error de carga sin contexto.
    /// </summary>
    [Fact]
    public void EveryPerkInDataIsDescribableInEveryLanguage()
    {
        var localization = Catalog.Localization;
        Assert.Contains("es", localization.Languages);
        Assert.Contains("en", localization.Languages);

        var pending = new List<string>();
        var described = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(TestData.DataDirectory, "perks"), "*.json")
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            string name = "perks/" + Path.GetFileName(path);
            PerkDefinition perk;
            try
            {
                perk = PerkLoader.Parse(name, File.ReadAllText(path));
            }
            catch (DataException)
            {
                // Formato viejo: lo migra el paquete T. Cuando esa lista quede vacía, este catch sobra.
                pending.Add(name);
                continue;
            }

            foreach (var language in localization.Languages)
            {
                string text = DescriptionGenerator.Describe(perk, language, Catalog);
                Assert.False(string.IsNullOrWhiteSpace(text), $"{perk.Id} no tiene descripción en {language}");
                Assert.DoesNotContain('{', text);
            }

            described.Add(perk.Id);
        }

        foreach (var ability in new[] { "elf_touch", "hot_blooded", "numb", "quick_learner", "roots" })
        {
            Assert.Contains(ability, described);
        }

        Assert.DoesNotContain("perks/quick_learner.json", pending);
    }

    /// <summary>
    /// La garantía fuerte de RT-035: **toda** clave que el generador puede llegar a pedir existe en los
    /// dos idiomas. Un tipo de efecto, un objetivo, una probabilidad, una inmunidad, una relación de
    /// vínculo o una función de condición nuevos sin plantilla caen aquí, aunque todavía no exista ningún
    /// perk del catálogo que los use.
    /// </summary>
    [Fact]
    public void EveryTemplateKeyTheGeneratorCanAskForExistsInEveryLanguage()
    {
        string[] comparisons = { "Lt", "Le", "Gt", "Ge", "Eq", "Ne" };
        string[] boolFunctions = { "hasTag", "isMob", "adjacent", "startsIn", "startsOn", "linked", "nearAlly", "nearOpponent" };
        string[] comparedFunctions =
        {
            "bias", "scoreDiff", "tick", "distanceToGoal", "level", "attr", "counter",
            "adjacentCount", "teammatesWithTag", "position", "zone", "detail", "stat",
        };

        foreach (var language in Catalog.Localization.Languages)
        {
            var templates = Catalog.Localization.Get(language);

            foreach (var trigger in Enum.GetValues<EventType>())
            {
                templates.Get("triggers", EventTypeNames.ToUpperSnake(trigger));
                templates.Get("events", EventTypeNames.ToUpperSnake(trigger));
            }

            foreach (var key in new[]
            {
                "modifyAttribute", "modifyAttributePerCounter", "modifyAttributePerCounterDivided",
                "modifyLeash", "modifyBias", "modifyProbability", "modifyProbabilityPaired", "cancelEvent",
                "addCounter", "setState", "modifyKnockdownTicks", "modifyKnockdownTicksDown", "immunity",
                "modifyExperience", "modifyExperienceDown",
            })
            {
                templates.Get("effects", key);
            }

            foreach (var key in new[]
            {
                "actor", "target", "opponent", "owner", "adjacent", "team", "opposingTeam",
                "withTag", "adjacentWithTag", "linked", "linkedWithTag",
            })
            {
                templates.Get("targets", key);
            }

            foreach (var key in new[]
            {
                "foul", "card", "injury", "injure", "severeInjury", "pass", "intercept", "dribble",
                "tackle", "shotOnTarget", "save", "tackleEvasion", "interceptEvasion",
            })
            {
                templates.Get("probabilities", key);
            }

            foreach (var key in new[] { "push", "mourning", "minorInjuryPenalty" })
            {
                templates.Get("immunities", key);
            }

            foreach (var key in new[] { "beside", "ahead", "behind", "left", "right", "diagonalAhead", "diagonalBehind" })
            {
                templates.Get("links", key);
            }

            foreach (var key in new[] { "OwnThird", "Middle", "AttackingThird" })
            {
                templates.Get("startZones", key);
            }

            foreach (var key in new[] { "LeftFlank", "Center", "RightFlank" })
            {
                templates.Get("startFlanks", key);
            }

            foreach (var key in new[] { "goals", "passesCompleted", "tacklesWon", "shots", "saves" })
            {
                templates.Get("stats", key);
            }

            foreach (var name in boolFunctions)
            {
                templates.Get("conditions", name);
            }

            foreach (var name in comparedFunctions)
            {
                foreach (var comparison in comparisons)
                {
                    if (name is "position" or "zone" or "detail" && comparison is not ("Eq" or "Ne"))
                    {
                        continue;
                    }

                    templates.Get("conditions", name + comparison);
                }
            }

            foreach (var key in new[] { "and", "or", "not" })
            {
                templates.Get("conditions", key);
            }

            foreach (var key in new[]
            {
                "plain", "withCondition", "withLimit", "withConditionAndLimit",
                "effectSeparator", "effectFinalSeparator", "elsePrefix", "linkSeparator",
            })
            {
                templates.Get("layout", key);
            }

            foreach (var duration in new[] { "instant", "play", "match", "run" })
            {
                templates.Get("durations", duration);
            }

            foreach (var scope in new[] { "play", "match", "mob", "run" })
            {
                templates.Get("limits", scope);
            }

            foreach (var attribute in Enum.GetValues<AttributeKind>())
            {
                templates.Get("attributes", attribute.ToString().ToLowerInvariant());
            }

            foreach (var position in Enum.GetValues<Position>())
            {
                templates.Get("positions", position.ToString());
            }

            foreach (var zone in Enum.GetValues<Zone>())
            {
                templates.Get("zones", zone.ToString());
            }
        }
    }

    private static PerkDefinition Bloodlust() => TestPerks.Load("bloodlust", TestPerks.Json(
        "bloodlust",
        "TACKLE",
        """[{ "type": "modifyAttribute", "target": "actor", "attribute": "strength", "value": 3, "duration": "play" }]""",
        kind: "conditional",
        condition: "hasTag(actor, 'Brute') && bias() < 0",
        limit: """{ "per": "match", "times": 2 }"""));

    private static PerkDefinition LinkedPasser() => TestPerks.Load("column_pass", TestPerks.Json(
        "column_pass",
        "MATCH_START",
        """[{ "type": "modifyProbability", "target": "linked", "probability": "pass", "value": 10, "duration": "match" }]""",
        axis: "alignment",
        links: """["beside"]"""));

    private static string Describe(string language, PerkDefinition perk) =>
        DescriptionGenerator.Describe(perk, language, Catalog);
}
