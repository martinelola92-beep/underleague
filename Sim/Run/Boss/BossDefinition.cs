using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Bosses;

/// <summary>
/// Qué hace un modificador de regla de jefe (RF-001b, RF-001c). Los cuatro son <b>reglas que cambian el
/// partido</b>, no bonos de atributos: cada uno invalida un eje distinto de construcción de build, de
/// modo que una build que lo apueste todo a ese eje no pasa la puerta (ADR 0033, espíritu de Balatro).
/// Todos se aplican como una transformación del <see cref="MatchSetup"/>, antes de simular, y son
/// deterministas: el orden de recorrido es siempre por id de jugador ascendente (RT-041).
/// </summary>
public enum BossModifierKind
{
    /// <summary>
    /// <b>Repetición</b>. Un perk repetido en el once solo surte efecto en su primer portador (id más
    /// bajo); en los demás se retira. Castiga la build de copiar y pegar el mismo perk en todos.
    /// </summary>
    SingleCopy,

    /// <summary>
    /// <b>Concentración</b>. El titular con más perks (desempate: mayor suma de atributos, luego menor
    /// id) juega marcado y pierde todos sus perks. Castiga la build que canaliza todo por un solo
    /// jugador.
    /// </summary>
    MarkStar,

    /// <summary>
    /// <b>Monocanal</b>. Los perks que tocan un canal de probabilidad concreto
    /// (<see cref="BossModifier.Probability"/>) no se aplican. Castiga la build que compra un único
    /// canal —el remate, la intercepción— y no tiene plan B.
    /// </summary>
    BanChannel,

    /// <summary>
    /// <b>Colocación adelantada</b>. Ningún titular puede empezar por delante de
    /// <see cref="BossModifier.Column"/>: el que esté más adelantado retrocede a la casilla libre más
    /// avanzada que quede. Castiga la build que depende de arrancar en el tercio atacante.
    /// </summary>
    PushBack,
}

/// <summary>Condición de derrota propia del jefe final (RF-001c, D-9).</summary>
public enum BossDefeatConditionKind
{
    /// <summary>El jefe no tiene condición propia: se pierde perdiendo el partido (RF-002b).</summary>
    None,

    /// <summary>
    /// <b>El campeón conserva el título</b>: llegar empatado al final del tiempo reglamentario es
    /// derrota. El gol de oro de la turba (RF-055b) ya no salva la run.
    /// </summary>
    DrawIsDefeat,
}

/// <summary>
/// Un modificador de regla de un jefe. Es un dato de <c>data/bosses/</c>; su id es lo que devuelve
/// <see cref="IRunSystems.BossRuleModifiers"/> y lo que se registra en el compendio del perfil una vez
/// descubierto (RF-014b).
/// </summary>
/// <param name="Id">Id del modificador, único en todo <c>data/bosses/</c>.</param>
/// <param name="Name">Nombre visible por idioma (es/en), como en <c>data/perks/</c>.</param>
/// <param name="Kind">Qué hace (ver <see cref="BossModifierKind"/>).</param>
/// <param name="Probability">Canal afectado; solo lo usa <see cref="BossModifierKind.BanChannel"/>.</param>
/// <param name="Column">Columna máxima de colocación; solo la usa <see cref="BossModifierKind.PushBack"/>.</param>
public sealed record BossModifier(
    string Id,
    IReadOnlyDictionary<string, string> Name,
    BossModifierKind Kind,
    Underleague.Sim.Perks.ProbabilityKind Probability = Underleague.Sim.Perks.ProbabilityKind.ShotOnTarget,
    int Column = 0);

/// <summary>Condición de derrota adicional del jefe (RF-001c). <see cref="BossDefeatConditionKind.None"/> = no hay.</summary>
public sealed record BossDefeatCondition(
    string Id,
    IReadOnlyDictionary<string, string> Name,
    BossDefeatConditionKind Kind);

/// <summary>Un perk de la plantilla del jefe, asignado por índice de titular (0 GK, 1-2 DEF, 3-5 MID, 6 FWD).</summary>
public sealed record BossPerkAssignment(int Slot, string Perk);

/// <summary>
/// Plantilla del jefe (RF-015: rival estático diseñado a mano). Misma forma que una build de
/// <c>data/balance/builds/</c>, porque un jefe es un rival con nombre: raza, calidad, nivel, rareza,
/// perks por slot, alineación y etiquetas impuestas. El jefe final la lleva íntegramente legendaria
/// (RF-001c).
/// </summary>
public sealed record BossTemplate(
    Race Race,
    int Quality,
    int Level,
    Rarity? UniformRarity,
    IReadOnlyList<BossPerkAssignment> Perks,
    IReadOnlyDictionary<int, Rarity> Rarities,
    IReadOnlyList<Cell>? Lineup,
    IReadOnlyDictionary<int, StyleTag> Styles,
    IReadOnlyDictionary<int, IReadOnlyList<Trait>> Traits)
{
    /// <summary>Titulares de un equipo (GK, DEF, DEF, MID, MID, MID, FWD).</summary>
    public const int StarterCount = 7;

    /// <summary>
    /// Genera el equipo del jefe. Puro: toda la aleatoriedad sale de <paramref name="rng"/>, que el
    /// llamador saca de <c>RngStreams.Generation</c> (RT-021, RT-022).
    /// </summary>
    public TeamSetup ToTeamSetup(ref Pcg32 rng, Catalog catalog, string teamId, int firstPlayerId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var generated = TeamGenerator.Generate(
            ref rng, catalog, teamId, Race, Quality, firstPlayerId, Level, UniformRarity, Styles, Traits);

        var players = generated.Players.ToList();
        foreach (var (slot, rarity) in Rarities.OrderBy(r => r.Key))
        {
            players[slot] = players[slot] with { Rarity = rarity };
        }

        var perksBySlot = new SortedDictionary<int, List<string>>();
        foreach (var assignment in Perks)
        {
            if (!perksBySlot.TryGetValue(assignment.Slot, out var list))
            {
                list = new List<string>();
                perksBySlot[assignment.Slot] = list;
            }

            list.Add(assignment.Perk);
        }

        foreach (var (slot, perkIds) in perksBySlot)
        {
            players[slot] = players[slot] with { Perks = perkIds };
        }

        Lineup lineup;
        if (Lineup is { } cells)
        {
            var slots = new List<LineupSlot>(StarterCount);
            for (int i = 0; i < StarterCount; i++)
            {
                slots.Add(new LineupSlot(players[i].Id, cells[i]));
            }

            lineup = new Lineup(slots);
        }
        else
        {
            lineup = Model.Lineup.Default(players.Take(StarterCount).ToList());
        }

        return new TeamSetup(teamId, teamId, Race, players, lineup);
    }
}

/// <summary>
/// Exigencia de la ADR 0033 para un nivel de calidad de build contra este jefe: la banda de tasa de
/// victoria que la curva de puertas obliga a cumplir. Vive en el dato porque el jefe se diseña
/// <b>contra</b> la tabla: la plantilla y el modificador se calibran hasta que la celda cae dentro.
/// </summary>
/// <param name="Level">Nivel de build: <c>incoherent</c>, <c>correct</c>, <c>good</c> o <c>excellent</c>.</param>
/// <param name="MinPercent">Mínimo de la banda, o null si la ADR solo pone techo.</param>
/// <param name="MaxPercent">Máximo de la banda, o null si solo pone suelo.</param>
public sealed record BossGateTarget(string Level, double? MinPercent, double? MaxPercent);

/// <summary>
/// Un jefe de acto (<c>data/bosses/&lt;id&gt;.json</c>, RF-001b/c). Contiene lo que hace falta para
/// jugarlo (plantilla y modificadores), la condición de derrota propia del jefe final y la fila de la
/// tabla de la ADR 0033 que tiene que cumplir.
/// </summary>
/// <param name="GatePlayerLevel">
/// Nivel al que llega la plantilla del jugador a esta puerta con la progresión de <c>sim/tuning.json</c>
/// (100 de experiencia por partido jugado): acto 1 tras ~6 partidos, acto 2 tras ~12, acto 3 tras ~18.
/// Es el dato con el que la métrica de puertas monta el equipo del jugador.
/// </param>
public sealed record BossDefinition(
    string Id,
    int Act,
    IReadOnlyDictionary<string, string> Name,
    BossTemplate Template,
    IReadOnlyList<BossModifier> Modifiers,
    BossDefeatCondition? DefeatCondition,
    int GatePlayerLevel,
    IReadOnlyList<BossGateTarget> GateTargets)
{
    /// <summary>Ids de los modificadores, en el orden del dato. Es lo que devuelve <see cref="IRunSystems.BossRuleModifiers"/>.</summary>
    public IReadOnlyList<string> ModifierIds => Modifiers.Select(m => m.Id).ToList();

    /// <summary>Banda exigida a ese nivel de build, o null si el jefe no la declara.</summary>
    public BossGateTarget? TargetFor(string level)
    {
        for (int i = 0; i < GateTargets.Count; i++)
        {
            if (string.Equals(GateTargets[i].Level, level, StringComparison.Ordinal))
            {
                return GateTargets[i];
            }
        }

        return null;
    }
}
