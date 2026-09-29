using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Flujo de RNG del surtido de un nodo (mercado o recompensa), derivado de <c>RngStreams.Rewards</c>
/// (W-12, <c>fase2-diseno.md</c> §13): mismo nodo y mismo número de rerolls producen siempre el mismo
/// surtido, sin necesidad de serializarlo. El mercado nunca se renueva (RF-114), así que siempre deriva
/// con <c>rerollCount = 0</c>; las recompensas usan <c>state.NodeRerolls</c> (RF-071b).
///
/// <para><b>Tabla de desplazamientos</b> (el tercer argumento; cada uno abre un flujo propio dentro del
/// mismo nodo, y que dos usos compartan desplazamiento es una colisión de dados —revisión independiente,
/// 29 sep 2026: el canterano del evento usaba 1, el reroll de la recompensa—). Mantener al añadir uno:</para>
/// <list type="table">
/// <item><term>0..99</term><description>mercado (0) y recompensa: <c>state.NodeRerolls</c> (RF-071b, 0..1)</description></item>
/// <item><term>100, 101, 200, 201...</term><description>recompensa: segunda elección (jefe, ADR 0043), <c>taken * 100 + rerolls</c></description></item>
/// <item><term>5000</term><description><c>EquipmentSystem</c>: roturas de objetos frágiles</description></item>
/// <item><term>6000</term><description><c>LeagueLootSystem</c>: botín de liga (ADR 0161)</description></item>
/// <item><term>7000</term><description><c>EventSystem</c>: el canterano de <c>recruit</c> (ADR 0159)</description></item>
/// <item><term>7100 + índice de efecto</term><description><c>EventSystem</c>: qué objeto o consumible da <c>grantItem</c>/<c>grantConsumable</c> (ADR 0159)</description></item>
/// <item><term>8000</term><description><c>BetSystem</c>: qué apuesta del vestuario se ofrece en el nodo de partido (ADR 0157)</description></item>
/// <item><term>9000 + id de jugador</term><description><c>MedicalSystem.Forge</c>: la tirada del herrero de la clínica sobre ese jugador (ADR 0164)</description></item>
/// <item><term>9500 + id de némesis</term><description><c>NemesisSystem</c>: el título de un némesis, en el nodo de partido donde nace (ADR 0165). No choca con el herrero: ése sólo tira en nodos de clínica</description></item>
/// <item><term>nodo ficticio 9500 + acto, desplazamiento = id de némesis</term><description><c>NemesisSystem.TransferOnActEntry</c>: a qué clan se traspasa (ADR 0165); 9500+ está por encima de cualquier id de nodo real</description></item>
/// <item><term>9600</term><description><c>MobCatalog</c>: el tipo de turba del partido de ese nodo (ADR 0167). Coincidiría con el título de némesis sólo si un id de némesis llegara a 100 en la misma run</description></item>
/// <item><term>(fuera de esta clase) 950.000.000 + clan·1000 + puesto·100 + generación</term><description><c>RivalRoster.SigningName</c> abre <c>RngStreams.Rewards</c> con esa sal directamente: el nombre del fichaje que cubre una vacante de clan (ADR 0165)</description></item>
/// </list>
/// </summary>
public static class OfferStream
{
    public static Pcg32 For(ulong seed, int nodeId, int rerollCount) =>
        RngStreams.Rewards(seed, checked((nodeId * 10_000) + rerollCount));
}

/// <summary>
/// Reparto por rareza para sortear la rareza de un jugador generado en el mercado o en una recompensa.
/// Solo las <b>tres rarezas generables</b> (ADR 0039): el legendario no se sortea nunca, es un personaje
/// único que se desbloquea ganando divisiones (fase 4).
/// </summary>
public sealed record RarityWeights(int Common, int Uncommon, int Rare)
{
    public Rarity Pick(ref Pcg32 rng)
    {
        int total = Common + Uncommon + Rare;
        int roll = rng.Range(0, total);
        if (roll < Common)
        {
            return Rarity.Common;
        }

        return roll < Common + Uncommon ? Rarity.Uncommon : Rarity.Rare;
    }
}

/// <summary>
/// Genera jugadores para el mercado y las recompensas: fichajes, canteranos y mercenarios. Id -1 a
/// propósito (<c>RunState.WithNewPlayer</c> le asigna <c>NextPlayerId</c> al comprarlo/elegirlo; hasta
/// entonces el jugador generado no forma parte de la plantilla).
/// </summary>
public static class GeneratedPlayers
{
    private static readonly Position[] AllPositions =
    {
        Position.Goalkeeper, Position.Defender, Position.Midfielder, Position.Forward,
    };

    /// <summary>
    /// Posiciones de campo, sin portero (ADR 0080): array estático en el orden del enum <see cref="Position"/>
    /// para que el sorteo sea determinista (RT-021), no un <c>Dictionary</c>/<c>HashSet</c> sin ordenar.
    /// </summary>
    private static readonly Position[] OutfieldPositions =
    {
        Position.Defender, Position.Midfielder, Position.Forward,
    };

    private static readonly RarityWeights RecruitWeights = new(60, 32, 8);
    private static readonly RarityWeights MercenaryWeights = new(25, 55, 20);
    private static readonly RarityWeights RewardWeights = new(35, 50, 15);

    /// <summary>Sortea una posición de campo (defensa, centrocampista o delantero), nunca portero (ADR 0080).</summary>
    public static Position PickOutfield(ref Pcg32 rng) => rng.Pick(OutfieldPositions);

    /// <summary>
    /// Fichaje de pago (RF-114): raza del club, rareza sorteada, y el <b>nivel del acto</b>
    /// (<c>economy.recruitLevelByAct</c>). Que un fichaje de pago entre en el nivel 1 en el acto 3 lo
    /// convierte en oro tirado: la plantilla va por el 6 o el 7 y ningún criterio razonable lo alinea,
    /// así que el mercado deja de ser un sumidero justo cuando más oro hay (medido en el paquete Z).
    /// </summary>
    /// <param name="position">
    /// Posición a la que se fuerza el fichaje (RF-114, ADR 0080: el portero garantizado del mercado sale
    /// siempre de aquí). <c>null</c> mantiene el sorteo uniforme entre las cuatro posiciones.
    /// </param>
    public static RunPlayer Recruit(ref Pcg32 rng, Catalog catalog, Race race, int quality, int level = 1, Position? position = null)
    {
        var rarity = RecruitWeights.Pick(ref rng);
        return Generate(ref rng, catalog, race, rarity, quality, level, youth: false, mercenary: false, wage: 0, position);
    }

    /// <summary>Canterano gratuito (RF-114b/c): común, de la raza del club, atributos bajos, +33% de experiencia.</summary>
    /// <param name="position">Posición a la que se fuerza el canterano; <c>null</c> mantiene el sorteo uniforme.</param>
    public static RunPlayer Youth(ref Pcg32 rng, Catalog catalog, Race race, int quality, Position? position = null) =>
        Generate(ref rng, catalog, race, Rarity.Common, quality, level: 1, youth: true, mercenary: false, wage: 0, position);

    /// <summary>
    /// Mercenario (RF-110..113): raza distinta a la del club (RF-004c), estadísticas por encima de la
    /// media de su rareza (calidad más alta), salario por partido, y cuenta como <c>Stranger</c> para las
    /// sinergias de cohesión (RF-111).
    /// </summary>
    /// <param name="position">Posición a la que se fuerza el mercenario; <c>null</c> mantiene el sorteo uniforme.</param>
    public static RunPlayer Mercenary(ref Pcg32 rng, Catalog catalog, Race foreignRace, int quality, int wage, int level = 1, Position? position = null)
    {
        var rarity = MercenaryWeights.Pick(ref rng);
        var player = Generate(ref rng, catalog, foreignRace, rarity, quality, level, youth: false, mercenary: true, wage: wage, position);
        var tags = new List<string>(player.Tags) { "Stranger" };
        return player with { Tags = tags };
    }

    /// <summary>Jugador de recompensa (RF-071): raza del club, rareza sesgada al alza.</summary>
    public static RunPlayer Reward(ref Pcg32 rng, Catalog catalog, Race race, int quality, int level = 1)
    {
        var rarity = RewardWeights.Pick(ref rng);
        return Generate(ref rng, catalog, race, rarity, quality, level, youth: false, mercenary: false, wage: 0);
    }

    private static RunPlayer Generate(
        ref Pcg32 rng,
        Catalog catalog,
        Race race,
        Rarity rarity,
        int quality,
        int level,
        bool youth,
        bool mercenary,
        int wage,
        Position? forcedPosition = null)
    {
        var raceDefinition = catalog.Race(race);
        var nameGenerator = new NameGenerator(raceDefinition);
        // Ver TeamGenerator.GeneratePlayer: PlayerDefinition.Name es un string plano hasta que Sim.Engine
        // lleve el idioma activo, así que aquí se fija a la variante es (RT-073).
        string name = nameGenerator.Next(ref rng).Es;
        var position = forcedPosition ?? rng.Pick(AllPositions);
        var definition = PlayerGenerator.Generate(ref rng, catalog, raceDefinition, position, rarity, level, id: -1, name, quality);

        // La experiencia tiene que corresponder al nivel con el que entra: si no, el primer partido lo
        // recalcularía desde cero y el jugador se quedaría clavado en su nivel durante media run
        // (Progression.LevelUp nunca baja, pero tampoco sube hasta cruzar el umbral).
        var table = catalog.Progression.ExperiencePerLevel;
        int experience = level >= 1 && level <= table.Count ? table[level - 1] : 0;
        return RunPlayer.From(definition) with
        {
            Experience = experience,
            IsYouth = youth,
            IsMercenary = mercenary,
            Wage = wage,
        };
    }
}
