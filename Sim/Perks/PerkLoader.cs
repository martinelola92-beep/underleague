using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// Cargador de <c>data/perks/&lt;id&gt;.json</c> en el formato revisado de <c>docs/fase1b-diseno.md</c>
/// §1.4 (RT-032, RT-033). Vive en <c>/Sim/Perks</c> y no en <c>/Sim/Data</c> porque el formato del perk
/// es parte del contrato del motor de perks: quien añade un tipo de efecto, un objetivo o una función de
/// condición cambia el cargador en el mismo sitio en el que cambia el motor, y no en otro paquete.
/// <para>
/// No hace E/S (RT-012): recibe el contenido del fichero ya leído, igual que <see cref="DataLoader"/>.
/// </para>
/// <para>Reglas del formato revisado que este cargador impone:</para>
/// <list type="bullet">
/// <item><c>value</c> de <c>modifyProbability</c> es un <b>multiplicador de cuota</b> escrito como
/// porcentaje con signo (ADR 0050 P1): <c>±15, ±30, ±50, ±100</c>, donde el negativo es el inverso
/// exacto del positivo de la misma magnitud. El cargador lo convierte al multiplicador interno en base
/// 10.000. Ya no hay escala por canal (la tabla de la ADR 0035 queda retirada): multiplicar cuotas hace
/// que un perk valga lo mismo en cualquier canal por construcción. El resto de efectos conserva su
/// propia unidad (puntos de atributo, casillas, ticks).</item>
/// <item><c>axis</c> obligatorio, uno de los ocho ejes de <c>docs/perks-ejes.md</c>.</item>
/// <item><c>race</c> null (universal) o id de raza (exclusivo, ADR 0023).</item>
/// <item>Un perk universal **no puede consultar la etiqueta de especie** (ADR 0023, RF-065b): en un club
/// monoraza esa condición se cumple siempre o nunca, así que no es una decisión.</item>
/// <item><c>links</c> declara las relaciones direccionales que el perk necesita (RF-044, ADR 0021) y es
/// requisito para usar los objetivos <c>linked</c> y <c>linkedWithTag:&lt;Tag&gt;</c>.</item>
/// </list>
/// </summary>
public static class PerkLoader
{
    private static readonly string[] KnownKeys =
    {
        "id", "name", "rarity", "kind", "axis", "race", "trigger", "scope", "links", "condition",
        "effects", "elseEffects", "limit", "accumulatesAcrossMatches", "lethal", "lethalChance", "positionOnly",
        "tagsRequired", "tagsForbidden",

        // Arcos de build y profundidad nativa (ADR 0051).
        "minAct", "frequency", "family", "requiresPerks", "blocksPerks",
    };

    /// <summary>Actos de una run (RF-001b). Un acto nativo fuera de este rango es error de carga.</summary>
    public const int Acts = 3;

    /// <summary>Frecuencia de un perk que no la declara (ADR 0051): la normal.</summary>
    public const int DefaultFrequency = 100;

    /// <summary>
    /// Copias máximas que un <c>modifyProbability</c> con contador puede acumular (ADR 0050 P1). Diez
    /// copias de un ×1,15 son ×4,05 y de un ×2 son ×1.024: por encima de eso el eje de acumulación deja
    /// de ser una progresión y pasa a ser un interruptor.
    /// </summary>
    public const int MaxCounterStacks = 10;

    private static readonly string[] EffectKnownKeys =
    {
        "type", "target", "attribute", "value", "valuePerCounter", "counter", "maxValue",
        "counterDivisor", "probability", "duration", "state", "ticks", "immunity",
    };

    private static readonly string[] AxisNames =
    {
        "identity", "accumulation", "alignment", "startZone", "geometry", "matchState", "composition", "proximity",
    };

    private static readonly string[] LinkNames =
    {
        "beside", "ahead", "behind", "left", "right", "diagonalAhead", "diagonalBehind",
    };

    private static readonly string[] ImmunityNames = { "push", "mourning", "minorInjuryPenalty" };

    /// <summary>
    /// Etiquetas de especie (ADR 0024): coinciden con los ids de <see cref="Race"/>, que es lo que
    /// <c>data/races/*.json</c> pone en <c>speciesTag</c>. Un perk universal no puede nombrarlas.
    /// </summary>
    public static bool IsSpeciesTag(string tag) => Enum.TryParse<Race>(tag, out var race) && Enum.IsDefined(race);

    /// <summary>
    /// Analiza el contenido de un fichero de perk. Lanza <see cref="DataException"/> si no cumple §1.4.
    /// La escala de <c>modifyProbability</c> ya no es un parámetro: desde la ADR 0050 P1 es única para
    /// todos los canales (<see cref="ProbabilityScale"/>), y por eso la tabla por canal de la ADR 0035
    /// desapareció de <c>tuning.json</c>.
    /// </summary>
    public static PerkDefinition Parse(string file, string content)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(file, "$", $"JSON inválido: {ex.Message}");
        }

        var root = new Node(doc.RootElement, file, "$");
        root.EnsureKnownKeys(KnownKeys);

        string id = root.Prop("id").AsString();
        var name = ParseLocalizedName(root.Prop("name"));
        var rarity = ParseEnum<Rarity>(root.Prop("rarity"), "rareza");
        var kind = ParseEnum<PerkKind>(root.Prop("kind"), "tipo de perk");
        var axis = (PerkAxis)Index(AxisNames, root.Prop("axis").AsString(), root.Prop("axis"), "eje");

        Race? race = null;
        if (root.TryProp("race") is { } raceNode)
        {
            race = ParseEnum<Race>(raceNode, "raza");
        }

        var trigger = ParseTrigger(root.Prop("trigger"));
        var scope = root.TryProp("scope") is { } scopeNode
            ? ParseEnum<PerkScope>(scopeNode, "alcance")
            : PerkScope.Actor;

        var links = ParseLinks(root.TryProp("links"));

        string conditionSource = root.TryProp("condition") is { } conditionNode ? conditionNode.AsString() : string.Empty;
        var condition = ConditionCompiler.Compile(conditionSource, file, "$.condition");

        var effects = ParseEffects(root.Prop("effects"), file, trigger, links);
        var elseEffects = root.TryProp("elseEffects") is { } elseNode
            ? ParseEffects(elseNode, file, trigger, links)
            : Array.Empty<EffectDefinition>();
        if (effects.Count == 0 && elseEffects.Count == 0)
        {
            throw new DataException(file, "$.effects", "un perk debe tener al menos un efecto");
        }

        LimitDefinition? limit = null;
        if (root.TryProp("limit") is { } limitNode)
        {
            limitNode.EnsureKnownKeys("per", "times");
            int times = limitNode.Prop("times").AsInt();
            if (times < 1)
            {
                throw new DataException(file, limitNode.Path + ".times", "el límite debe ser al menos 1");
            }

            limit = new LimitDefinition(ParseEnum<LimitScope>(limitNode.Prop("per"), "ámbito de límite"), times);
        }

        bool accumulates = root.TryProp("accumulatesAcrossMatches") is { } accNode && accNode.AsBool();
        bool lethal = root.TryProp("lethal") is { } lethalNode && lethalNode.AsBool();
        int lethalChance = root.TryProp("lethalChance") is { } chanceNode ? chanceNode.AsInt() : 0;
        if (lethal)
        {
            // ADR 0048: un jugador sano puede morir, así que alcanzar a una víctima ya no la mata: tira
            // por ella. La probabilidad base es del perk —es la palanca con la que se sube y se baja la
            // letalidad al medir— y sin ella el perk sería letal en el ojeo y inofensivo en el campo.
            if (lethalChance <= 0)
            {
                throw new DataException(
                    file,
                    "$.lethalChance",
                    "un perk letal necesita una probabilidad base de muerte mayor que cero (ADR 0048): "
                        + "sin ella se anuncia como letal en el informe de ojeo y no mata nunca (RF-012d)");
            }

            // RF-093 vía 2. Ya no se rechaza: en fase 1 no había muertes y un perk letal era una promesa
            // que el motor no cumplía; desde la fase 2 el motor la cumple (EffectEngine mata a los
            // rivales alcanzados por un perk letal que ya no estén sanos, y solo a ellos). Lo que sí se
            // exige es que el perk pueda alcanzar a un rival: un perk letal que solo se aplica a sí mismo
            // o a su equipo sería una etiqueta de peligro sin peligro, y RF-013 obliga a destacarlo en el
            // informe de ojeo, así que tiene que significar algo.
            if (!ReachesAnOpponent(effects) && !ReachesAnOpponent(elseEffects))
            {
                throw new DataException(
                    file,
                    "$.lethal",
                    "un perk letal debe tener algún efecto sobre el rival (target actor, target, opponent u "
                        + "opposingTeam): matar solo puede alcanzar a un rival (RF-093)");
            }
        }

        else if (lethalChance != 0)
        {
            throw new DataException(
                file, "$.lethalChance", "solo un perk con lethal:true puede declarar lethalChance (ADR 0048)");
        }

        Position? positionOnly = null;
        if (root.TryProp("positionOnly") is { } positionNode)
        {
            positionOnly = ParseEnum<Position>(positionNode, "posición");
        }

        var tagsRequired = ParseTags(root.TryProp("tagsRequired"));
        var tagsForbidden = ParseTags(root.TryProp("tagsForbidden"));

        if (race is null)
        {
            RejectSpeciesTags(file, condition, tagsRequired, tagsForbidden, effects, elseEffects);
        }

        var (minAct, frequency, family, requires, blocks) = ParseArc(root, file, id);

        return new PerkDefinition(
            id, name, rarity, kind, axis, race, links, trigger, scope, conditionSource, condition,
            effects, elseEffects, limit, accumulates, lethal, lethalChance, positionOnly, tagsRequired,
            tagsForbidden, minAct, frequency, family, requires, blocks);
    }

    // ---------------------------------------------------------------- arcos de build (ADR 0051)

    /// <summary>
    /// Lee los cuatro campos de la ADR 0051. Lo que se puede comprobar con el fichero delante se
    /// comprueba aquí; lo que exige ver el catálogo entero —que la línea exista, que sea alcanzable, que
    /// no haya ciclos— lo comprueba <see cref="ValidateArcs"/> una vez cargados todos los perks.
    /// </summary>
    private static (int MinAct, int Frequency, string Family, MasterRequirement? Requires, PerkBlock Blocks) ParseArc(
        Node root, string file, string id)
    {
        int minAct = root.TryProp("minAct") is { } actNode ? actNode.AsInt() : 1;
        if (minAct < 1 || minAct > Acts)
        {
            throw new DataException(file, "$.minAct", $"el acto nativo tiene que estar entre 1 y {Acts} (ADR 0051)");
        }

        int frequency = root.TryProp("frequency") is { } frequencyNode ? frequencyNode.AsInt() : DefaultFrequency;
        if (frequency < 10 || frequency > 500)
        {
            throw new DataException(
                file, "$.frequency", "la frecuencia va de 10 a 500, con 100 como lo normal (ADR 0051)");
        }

        string family = root.TryProp("family") is { } familyNode ? familyNode.AsString() : string.Empty;

        MasterRequirement? requires = null;
        if (root.TryProp("requiresPerks") is { } requiresNode)
        {
            requiresNode.EnsureKnownKeys("family", "count");
            int count = requiresNode.Prop("count").AsInt();
            if (count < 2)
            {
                throw new DataException(
                    file,
                    requiresNode.Path + ".count",
                    "un maestro exige llevar ya al menos dos perks de su línea (ADR 0051): con uno no hay arco");
            }

            requires = new MasterRequirement(requiresNode.Prop("family").AsString(), count);
        }

        var blocks = PerkBlock.None;
        if (root.TryProp("blocksPerks") is { } blocksNode)
        {
            blocksNode.EnsureKnownKeys("families", "perks");
            var families = ParseIdList(blocksNode.TryProp("families"));
            var perks = ParseIdList(blocksNode.TryProp("perks"));
            blocks = new PerkBlock(families, perks);
            if (perks.Contains(id, StringComparer.Ordinal))
            {
                throw new DataException(file, blocksNode.Path + ".perks", "un perk no puede cerrarse a sí mismo");
            }
        }

        if (requires is null && blocks.Any)
        {
            throw new DataException(
                file,
                "$.blocksPerks",
                "solo un maestro (con requiresPerks) puede cerrar líneas: un perk que cierra sin exigir "
                    + "nada es una trampa sin arco detrás (ADR 0051)");
        }

        if (requires is not null)
        {
            if (!blocks.Any)
            {
                throw new DataException(
                    file,
                    "$.blocksPerks",
                    "un maestro tiene que cerrar algo (ADR 0051): sin exclusión, comprometerse no cuesta nada");
            }

            if (family.Length == 0)
            {
                throw new DataException(
                    file, "$.family", "un maestro pertenece a la línea que corona (ADR 0051): declara 'family'");
            }

            if (minAct < 2)
            {
                throw new DataException(
                    file, "$.minAct", "un maestro no puede salir en el acto 1 (ADR 0051): su acto nativo es 2 o 3");
            }

            if (blocks.Families.Contains(requires.Family, StringComparer.Ordinal))
            {
                throw new DataException(
                    file,
                    "$.blocksPerks.families",
                    $"el maestro exige la línea '{requires.Family}' y la cierra a la vez: es inalcanzable");
            }
        }

        return (minAct, frequency, family, requires, blocks);
    }

    private static IReadOnlyList<string> ParseIdList(Node? node) =>
        node is { } list ? list.EnumerateArray().Select(j => j.AsString()).ToArray() : Array.Empty<string>();

    /// <summary>
    /// Validación de los arcos que solo se puede hacer con el catálogo entero delante (ADR 0051): que las
    /// líneas existan, que las dependencias existan, que ningún maestro sea <b>inalcanzable</b> —su línea
    /// tiene que tener suficientes miembros que no sean maestros, o llegar a él exigiría llegar antes a
    /// otro maestro— y que no haya ciclos entre maestros. La llama <c>DataLoader</c> tras cargar todos los
    /// perks; un dato inválido es un error explícito, nunca silencioso (RT-032).
    /// </summary>
    public static void ValidateArcs(IReadOnlyList<PerkDefinition> perks, BuildArcs arcs)
    {
        ArgumentNullException.ThrowIfNull(perks);
        ArgumentNullException.ThrowIfNull(arcs);

        var byId = new Dictionary<string, PerkDefinition>(StringComparer.Ordinal);
        foreach (var perk in perks)
        {
            byId[perk.Id] = perk;
        }

        foreach (var perk in perks)
        {
            string file = "perks/" + perk.Id + ".json";
            if (perk.HasFamily && !arcs.HasFamily(perk.Family))
            {
                throw new DataException(
                    file,
                    "$.family",
                    $"línea desconocida '{perk.Family}': declárala en data/build/arcs.json (hay {arcs.FamilyList()})");
            }

            foreach (var blocked in perk.Blocks.Families)
            {
                if (!arcs.HasFamily(blocked))
                {
                    throw new DataException(
                        file, "$.blocksPerks.families", $"línea desconocida '{blocked}' (hay {arcs.FamilyList()})");
                }
            }

            foreach (var blocked in perk.Blocks.Perks)
            {
                if (!byId.ContainsKey(blocked))
                {
                    throw new DataException(
                        file, "$.blocksPerks.perks", $"el perk bloqueado '{blocked}' no existe en el catálogo");
                }
            }

            if (perk.Requires is not { } requirement)
            {
                continue;
            }

            if (!arcs.HasFamily(requirement.Family))
            {
                throw new DataException(
                    file,
                    "$.requiresPerks.family",
                    $"línea desconocida '{requirement.Family}' (hay {arcs.FamilyList()})");
            }

            // Alcanzable: la línea tiene que poder aportar las piezas que el maestro pide SIN contar con
            // otro maestro. Es la comprobación que descarta a la vez el maestro inalcanzable (línea con
            // menos miembros de los que exige) y el ciclo (dos maestros que se exigen el uno al otro):
            // si solo se cuentan piezas normales, no hay forma de construir un ciclo.
            int reachable = 0;
            foreach (var member in perks)
            {
                if (!member.IsMaster
                    && string.Equals(member.Family, requirement.Family, StringComparison.Ordinal)
                    && (member.Race is null || member.Race == perk.Race))
                {
                    reachable++;
                }
            }

            if (reachable < requirement.Count)
            {
                throw new DataException(
                    file,
                    "$.requiresPerks",
                    $"maestro inalcanzable: exige {requirement.Count} perks de la línea '{requirement.Family}' "
                        + $"y solo hay {reachable} que no sean maestros y puedan aparecer en la misma run");
            }
        }
    }

    /// <summary>
    /// True si alguno de los efectos puede recaer sobre un jugador del equipo contrario. Es la condición
    /// que debe cumplir un perk marcado como letal (RF-093, RF-013): los objetivos colectivos propios
    /// (<c>team</c>, <c>adjacent</c>, <c>withTag</c>, <c>linked</c>) nunca alcanzan a un rival, y
    /// <c>owner</c> tampoco.
    /// </summary>
    private static bool ReachesAnOpponent(IReadOnlyList<EffectDefinition> effects)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].Target is EffectTarget.Actor
                or EffectTarget.Target
                or EffectTarget.Opponent
                or EffectTarget.OpposingTeam)
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- campos nuevos de §1.4

    private static IReadOnlyList<LinkRelation> ParseLinks(Node? node)
    {
        if (node is not { } array)
        {
            return Array.Empty<LinkRelation>();
        }

        var links = new List<LinkRelation>();
        foreach (var item in array.EnumerateArray())
        {
            var relation = (LinkRelation)Index(LinkNames, item.AsString(), item, "relación de vínculo");
            if (links.Contains(relation))
            {
                throw new DataException(item.File, item.Path, $"relación de vínculo repetida '{item.AsString()}'");
            }

            links.Add(relation);
        }

        return links;
    }

    /// <summary>
    /// ADR 0023 / RF-065b: un perk sin <c>race</c> no puede nombrar una etiqueta de especie, ni en la
    /// condición, ni en <c>tagsRequired</c>/<c>tagsForbidden</c>, ni en el objetivo de un efecto.
    /// </summary>
    private static void RejectSpeciesTags(
        string file,
        CompiledCondition condition,
        IReadOnlyList<string> tagsRequired,
        IReadOnlyList<string> tagsForbidden,
        IReadOnlyList<EffectDefinition> effects,
        IReadOnlyList<EffectDefinition> elseEffects)
    {
        const string Why = "un perk universal (race: null) no puede consultar la etiqueta de especie "
            + "(ADR 0023): en un club monoraza se cumple siempre o nunca. Usa estilo, rasgo o posición, "
            + "o declara el perk como exclusivo de raza.";

        foreach (var tag in ConditionCompiler.TagLiterals(condition.Ast))
        {
            if (IsSpeciesTag(tag))
            {
                throw new DataException(file, "$.condition", $"etiqueta de especie '{tag}': {Why}");
            }
        }

        Check(tagsRequired, "$.tagsRequired");
        Check(tagsForbidden, "$.tagsForbidden");
        CheckEffects(effects, "$.effects");
        CheckEffects(elseEffects, "$.elseEffects");

        void Check(IReadOnlyList<string> tags, string path)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (IsSpeciesTag(tags[i]))
                {
                    throw new DataException(file, path, $"etiqueta de especie '{tags[i]}': {Why}");
                }
            }
        }

        void CheckEffects(IReadOnlyList<EffectDefinition> list, string path)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].TargetTag.Length > 0 && IsSpeciesTag(list[i].TargetTag))
                {
                    throw new DataException(file, $"{path}[{i}].target", $"etiqueta de especie '{list[i].TargetTag}': {Why}");
                }
            }
        }
    }

    // ---------------------------------------------------------------- efectos

    private static IReadOnlyList<EffectDefinition> ParseEffects(
        Node node, string file, EventType trigger, IReadOnlyList<LinkRelation> links)
    {
        var effects = new List<EffectDefinition>();
        foreach (var item in node.EnumerateArray())
        {
            effects.Add(ParseEffect(item, file, trigger, links));
        }

        return effects;
    }

    private static EffectDefinition ParseEffect(
        Node node, string file, EventType trigger, IReadOnlyList<LinkRelation> links)
    {
        node.EnsureKnownKeys(EffectKnownKeys);
        var type = ParseEnum<EffectType>(node.Prop("type"), "tipo de efecto");

        var (target, targetTag) = node.TryProp("target") is { } targetNode
            ? ParseTarget(targetNode)
            : (EffectTarget.Owner, string.Empty);

        var duration = node.TryProp("duration") is { } durationNode
            ? ParseEnum<EffectDuration>(durationNode, "duración")
            : EffectDuration.Instant;

        int value = node.TryProp("value") is { } valueNode ? valueNode.AsInt() : 0;
        bool usesCounter = node.TryProp("valuePerCounter") is not null;
        int valuePerCounter = usesCounter ? node.Prop("valuePerCounter").AsInt() : 0;
        string counter = node.TryProp("counter") is { } counterNode ? counterNode.AsString() : string.Empty;
        int maxValue = node.TryProp("maxValue") is { } maxNode ? maxNode.AsInt() : 0;
        int counterDivisor = node.TryProp("counterDivisor") is { } divisorNode ? divisorNode.AsInt() : 1;
        int ticks = node.TryProp("ticks") is { } ticksNode ? ticksNode.AsInt() : 0;

        var attribute = AttributeKind.Strength;
        if (node.TryProp("attribute") is { } attributeNode)
        {
            attribute = ConditionCompiler.Attribute(attributeNode.AsString())
                ?? throw new DataException(file, attributeNode.Path, $"atributo desconocido '{attributeNode.AsString()}'");
        }

        var probability = ProbabilityKind.Foul;
        if (node.TryProp("probability") is { } probabilityNode)
        {
            probability = ParseEnum<ProbabilityKind>(probabilityNode, "probabilidad");
        }

        var state = PlayerState.KnockedDown;
        if (node.TryProp("state") is { } stateNode)
        {
            state = ParseEnum<PlayerState>(stateNode, "estado");
        }

        var immunity = ImmunityKind.Push;
        if (node.TryProp("immunity") is { } immunityNode)
        {
            immunity = (ImmunityKind)Index(ImmunityNames, immunityNode.AsString(), immunityNode, "inmunidad");
        }

        // ADR 0050 P1: el dato se escribe como porcentaje de CUOTA con signo y el cargador lo lleva al
        // multiplicador interno en base 10.000. Solo modifyProbability vive en esa base: los puntos de
        // atributo, las casillas de correa y los ticks de derribo son sus propias unidades y no se tocan.
        // Cuando el efecto escala con un contador, cada unidad vale una copia más del mismo multiplicador
        // y el tope es el multiplicador máximo que puede alcanzar.
        if (type == EffectType.ModifyProbability)
        {
            if (usesCounter)
            {
                valuePerCounter = ToMultiplier(node, valuePerCounter, "valuePerCounter");

                // Con cuotas, "por cada unidad del contador" es "una copia más del perk", así que el tope
                // natural es un NÚMERO DE COPIAS y no otro multiplicador: un perk de ×1,3 que acumula
                // hasta cinco veces llega a ×3,71 sin que ninguna de las cinco multiplicaciones se salga
                // de la escala legal. Expresarlo como multiplicador dejaría el eje de acumulación (RF-070)
                // encerrado en el ×2 de la escala, que es menos de lo que valía sumando puntos.
                if (maxValue is < 1 or > MaxCounterStacks)
                {
                    throw new DataException(
                        node.File,
                        node.Path + ".maxValue",
                        $"'{maxValue}' no es un tope válido: en modifyProbability con contador el tope es el "
                            + $"número de veces que el multiplicador se acumula, entre 1 y {MaxCounterStacks} "
                            + "(ADR 0050 P1)");
                }
            }
            else
            {
                value = ToMultiplier(node, value, "value");
            }
        }

        ValidateEffect(node, file, trigger, type, target, duration, usesCounter, counter, counterDivisor, state, links, value);

        return new EffectDefinition(
            type, target, targetTag, attribute, value, usesCounter, valuePerCounter, counter,
            maxValue, counterDivisor, probability, duration, state, ticks, immunity);
    }

    /// <summary>
    /// Convierte el porcentaje de cuota con signo de <c>/data</c> en el multiplicador interno
    /// (ADR 0050 P1). La escala es única para todos los canales, que es justo lo que multiplicar cuotas
    /// consigue y lo que la tabla por canal de la ADR 0035 intentaba parchear.
    /// </summary>
    private static int ToMultiplier(Node node, int percent, string field)
    {
        if (ProbabilityScale.IsLegal(percent))
        {
            return ProbabilityScale.ToMultiplier(percent);
        }

        throw new DataException(
            node.File,
            node.Path + "." + field,
            $"'{percent}' no es un valor legal de modifyProbability: un perk multiplica la CUOTA del canal "
                + $"y la escala es {ProbabilityScale.Allowed} (ADR 0050 P1). El negativo es el inverso "
                + "exacto del positivo de la misma magnitud: -30 es dividir por 1,3, no restar el 30%");
    }

    private static void ValidateEffect(
        Node node,
        string file,
        EventType trigger,
        EffectType type,
        EffectTarget target,
        EffectDuration duration,
        bool usesCounter,
        string counter,
        int counterDivisor,
        PlayerState state,
        IReadOnlyList<LinkRelation> links,
        int value)
    {
        bool instantOnly = type is EffectType.AddCounter or EffectType.ModifyBias or EffectType.SetState
            or EffectType.CancelEvent or EffectType.Immunity;
        if (instantOnly && duration != EffectDuration.Instant)
        {
            throw new DataException(file, node.Path, $"'{type}' solo admite duration 'instant'");
        }

        if (!instantOnly && duration == EffectDuration.Instant)
        {
            throw new DataException(file, node.Path, $"'{type}' necesita una duración ('play', 'match' o 'run')");
        }

        if (type == EffectType.CancelEvent && trigger is not (EventType.Card or EventType.Injury or EventType.Foul))
        {
            throw new DataException(
                file, node.Path, "cancelEvent solo es válido con trigger CARD, INJURY o FOUL");
        }

        if (type == EffectType.SetState)
        {
            if (state != PlayerState.KnockedDown)
            {
                throw new DataException(file, node.Path, "setState solo admite el estado 'KnockedDown'");
            }

            if (target is not (EffectTarget.Target or EffectTarget.Opponent or EffectTarget.OpposingTeam))
            {
                throw new DataException(
                    file, node.Path, "setState solo puede derribar a objetivos rivales (target, opponent, opposingTeam)");
            }
        }

        if (type == EffectType.AddCounter && counter.Length == 0)
        {
            throw new DataException(file, node.Path, "addCounter necesita el nombre del contador");
        }

        if (usesCounter)
        {
            // El valor por contador es la forma que tiene el eje de acumulación de crecer partido a
            // partido (RF-070): vale en los tres canales que suman un número al jugador, y no en los que
            // encienden un interruptor (cancelEvent, immunity), disparan un estado o mueven al árbitro.
            if (type is not (EffectType.ModifyAttribute or EffectType.ModifyLeash or EffectType.ModifyProbability))
            {
                throw new DataException(
                    file, node.Path, "valuePerCounter solo es válido en modifyAttribute, modifyLeash y modifyProbability");
            }

            if (target is EffectTarget.Linked or EffectTarget.LinkedWithTag)
            {
                throw new DataException(
                    file, node.Path, "un modificador por par no puede escalar con un contador: elige uno de los dos");
            }

            if (counter.Length == 0)
            {
                throw new DataException(file, node.Path, "valuePerCounter necesita el contador de referencia");
            }

            if (counterDivisor < 1)
            {
                throw new DataException(file, node.Path, "counterDivisor debe ser al menos 1");
            }
        }

        if (target is EffectTarget.Linked or EffectTarget.LinkedWithTag && links.Count == 0)
        {
            throw new DataException(
                file,
                node.Path,
                "un objetivo vinculado exige declarar 'links' con al menos una relación (ADR 0021)");
        }

        if (type == EffectType.ModifyProbability && !usesCounter && value == ProbabilityScale.Neutral)
        {
            throw new DataException(file, node.Path, "modifyProbability con value 0 no hace nada");
        }

        if (type == EffectType.ModifyKnockdownTicks)
        {
            if (value == 0)
            {
                throw new DataException(file, node.Path, "modifyKnockdownTicks con value 0 no hace nada");
            }

            if (target is not (EffectTarget.Owner or EffectTarget.Team or EffectTarget.Actor or EffectTarget.WithTag))
            {
                throw new DataException(
                    file, node.Path, "modifyKnockdownTicks actúa sobre quien entra (owner, actor, team o withTag)");
            }
        }

        if (type == EffectType.ModifyExperience)
        {
            if (duration != EffectDuration.Run)
            {
                throw new DataException(file, node.Path, "modifyExperience solo admite duration 'run': actúa fuera del partido");
            }

            if (target != EffectTarget.Owner)
            {
                throw new DataException(file, node.Path, "modifyExperience solo admite target 'owner'");
            }

            if (value == 0)
            {
                throw new DataException(file, node.Path, "modifyExperience con value 0 no hace nada");
            }
        }

        if (type == EffectType.Immunity
            && target is not (EffectTarget.Owner or EffectTarget.Team or EffectTarget.WithTag))
        {
            throw new DataException(file, node.Path, "immunity solo admite target 'owner', 'team' o 'withTag:<Tag>'");
        }
    }

    private static (EffectTarget Target, string Tag) ParseTarget(Node node)
    {
        string text = node.AsString();
        int separator = text.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0)
        {
            return (ParseEnum<EffectTarget>(node, "objetivo"), string.Empty);
        }

        string prefix = text[..separator];
        string tag = text[(separator + 1)..];
        if (tag.Length == 0)
        {
            throw new DataException(node.File, node.Path, $"objetivo '{text}' sin etiqueta");
        }

        var target = prefix switch
        {
            "withTag" => EffectTarget.WithTag,
            "adjacentWithTag" => EffectTarget.AdjacentWithTag,
            "linkedWithTag" => EffectTarget.LinkedWithTag,
            _ => throw new DataException(node.File, node.Path, $"objetivo desconocido '{text}'"),
        };

        return (target, tag);
    }

    // ---------------------------------------------------------------- utilidades comunes

    private static IReadOnlyList<string> ParseTags(Node? node) =>
        node is { } tags ? tags.EnumerateArray().Select(j => j.AsString()).ToArray() : Array.Empty<string>();

    private static LocalizedName ParseLocalizedName(Node node) =>
        new(node.Prop("es").AsString(), node.Prop("en").AsString());

    private static EventType ParseTrigger(Node node)
    {
        string text = node.AsString();
        foreach (var candidate in Enum.GetValues<EventType>())
        {
            if (string.Equals(EventTypeNames.ToUpperSnake(candidate), text, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        throw new DataException(node.File, node.Path, $"disparador desconocido '{text}'");
    }

    private static T ParseEnum<T>(Node node, string what)
        where T : struct, Enum
    {
        string text = node.AsString();
        string pascal = text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
        if (Enum.TryParse<T>(pascal, out var value) && Enum.IsDefined(value))
        {
            return value;
        }

        throw new DataException(node.File, node.Path, $"{what} desconocido '{text}'");
    }

    private static int Index(string[] names, string text, Node node, string what)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], text, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new DataException(node.File, node.Path, $"{what} desconocido '{text}'");
    }

    /// <summary>
    /// Lector mínimo de JSON con ruta ("$.effects[0].value") para los mensajes de error de RT-032. Es el
    /// mismo contrato que el ayudante equivalente de <see cref="DataLoader"/>, que es privado de esa
    /// clase; duplicarlo aquí es lo que permite que el formato de perk viva en su propio paquete.
    /// </summary>
    private readonly struct Node
    {
        private readonly JsonElement _element;

        public Node(JsonElement element, string file, string path)
        {
            _element = element;
            File = file;
            Path = path;
        }

        public string File { get; }

        public string Path { get; }

        public Node Prop(string name)
        {
            if (_element.ValueKind != JsonValueKind.Object)
            {
                throw new DataException(File, Path, $"se esperaba un objeto con la propiedad '{name}'");
            }

            if (!_element.TryGetProperty(name, out var value))
            {
                throw new DataException(File, Path, $"falta la propiedad requerida '{name}'");
            }

            return new Node(value, File, Path + "." + name);
        }

        /// <summary>
        /// Propiedad opcional. Un <c>null</c> de JSON cuenta como **ausente**: el formato de §1.4 escribe
        /// explícitamente <c>"limit": null</c>, <c>"race": null</c> y <c>"positionOnly": null</c> para que
        /// el fichero enseñe todos sus campos, y las tres cosas significan "no hay".
        /// </summary>
        public Node? TryProp(string name) =>
            _element.ValueKind == JsonValueKind.Object
                && _element.TryGetProperty(name, out var value)
                && value.ValueKind != JsonValueKind.Null
                    ? new Node(value, File, Path + "." + name)
                    : null;

        public string AsString() => _element.ValueKind == JsonValueKind.String
            ? _element.GetString()!
            : throw new DataException(File, Path, "se esperaba una cadena");

        public int AsInt() => _element.ValueKind == JsonValueKind.Number && _element.TryGetInt32(out int value)
            ? value
            : throw new DataException(File, Path, "se esperaba un entero");

        public bool AsBool() => _element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? _element.GetBoolean()
            : throw new DataException(File, Path, "se esperaba un booleano");

        public IEnumerable<Node> EnumerateArray()
        {
            if (_element.ValueKind != JsonValueKind.Array)
            {
                throw new DataException(File, Path, "se esperaba un array");
            }

            int i = 0;
            foreach (var item in _element.EnumerateArray())
            {
                yield return new Node(item, File, Path + $"[{i}]");
                i++;
            }
        }

        public void EnsureKnownKeys(params IReadOnlyList<string> known)
        {
            if (_element.ValueKind != JsonValueKind.Object)
            {
                throw new DataException(File, Path, "se esperaba un objeto");
            }

            foreach (var property in _element.EnumerateObject())
            {
                if (property.Name == "_doc" || known.Contains(property.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                throw new DataException(File, Path, $"clave desconocida '{property.Name}'");
            }
        }
    }
}
