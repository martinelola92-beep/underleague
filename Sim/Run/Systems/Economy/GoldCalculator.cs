using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Oro por partido ganado (RF-114g..k). Solo depende de datos conocidos antes de jugar (acto, dificultad,
/// tipo de nodo): nunca del resultado fino dentro del partido (RF-114i). El «partido excelente» (RF-114h) ya
/// no existe: lo sustituye la apuesta del vestuario (ADR 0157), que se resuelve aparte en
/// <c>BetSystem.Resolve</c>.
/// </summary>
public static class GoldCalculator
{
    public static int GoldForWin(RunState state, MapNode node, RunMatchSummary summary, EconomyConfig economy) =>
        Breakdown(state, node, summary, economy).Total;

    /// <summary>
    /// El mismo cálculo, sumando aparte (RF-119). El informe post-partido tiene que poder decir
    /// <b>por qué</b> se ha cobrado esa cantidad, y la única forma de que el desglose no mienta es que sea
    /// el propio cálculo: <see cref="GoldForWin"/> es su total, no una fórmula paralela.
    /// </summary>
    public static GoldForWinBreakdown Breakdown(RunState state, MapNode node, RunMatchSummary summary, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(economy);

        int actBase = economy.GoldForAct(node.Act);
        int difficultyPercent = economy.MultiplierForDifficulty(node.Difficulty);
        int afterDifficulty = actBase * difficultyPercent / 100;

        // ADR 0043: el escalón por tipo de nodo. La liga paga la base, el élite paga más y el jefe mucho
        // más: sin ese salto, superar un acto no cambia la trayectoria de la run.
        int nodeBonusPercent = economy.RewardFor(node.Kind).GoldBonusPercent;
        int nodeBonus = afterDifficulty * nodeBonusPercent / 100;

        return new GoldForWinBreakdown(
            node.Act,
            actBase,
            node.Difficulty,
            difficultyPercent,
            afterDifficulty,
            node.Kind,
            nodeBonusPercent,
            nodeBonus,
            afterDifficulty + nodeBonus);
    }

    /// <summary>
    /// Oro que pagan los contadores de este partido, <b>se haya ganado o no</b> (ADR 0113). No es premio
    /// de partido y por eso no entra en <see cref="Breakdown"/>: el premio lo cobra quien gana (RF-114g),
    /// mientras que esto es el retorno de una inversión que el jugador ya pagó al gastar un slot.
    /// </summary>
    public static CounterGold CounterGold(RunState state, RunMatchSummary summary, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(economy);
        var rows = CounterGoldRows(state, summary, economy.CounterGold);
        return new CounterGold(rows, SumGold(rows));
    }

    /// <summary>
    /// Filas de oro por contador (paquete Z, primitiva D): los contadores propios de este partido
    /// (<see cref="RunMatchSummary.CounterDeltas"/>) que tienen tarifa en <paramref name="rates"/>. Solo
    /// cuentan los del propio club -<paramref name="state"/> es la run, y su plantilla es la única que
    /// puede estar en <c>state.Roster</c>-, así que un contador del rival nunca paga a este club aunque
    /// lleve el mismo perk. Ordenadas por id de jugador y luego por nombre de contador (RT-041), heredado
    /// del orden de <see cref="RunMatchSummary.CounterDeltas"/>.
    /// </summary>
    private static IReadOnlyList<CounterGoldRow> CounterGoldRows(
        RunState state, RunMatchSummary summary, CounterGoldTable rates)
    {
        var deltas = summary.CounterDeltas;
        if (deltas.Count == 0 || rates.Count == 0)
        {
            return Array.Empty<CounterGoldRow>();
        }

        var rows = new List<CounterGoldRow>();
        for (int i = 0; i < deltas.Count; i++)
        {
            var delta = deltas[i];
            if (state.FindPlayer(delta.PlayerId) is null)
            {
                continue;
            }

            int rate = rates.RateFor(delta.Counter);
            if (rate == 0)
            {
                continue;
            }

            rows.Add(new CounterGoldRow(delta.PlayerId, delta.Counter, delta.Delta, rate, delta.Delta * rate));
        }

        return rows;
    }

    private static int SumGold(IReadOnlyList<CounterGoldRow> rows)
    {
        int total = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            total += rows[i].Gold;
        }

        return total;
    }

    /// <summary>
    /// Oro que pagan las muertes de este partido (paquete BB, consumidor Seguro de vida), <b>se haya
    /// ganado o no</b>: mismo canal aparte que <see cref="CounterGold"/> (ADR 0113), pagado una vez por
    /// muerte y no por unidad de contador.
    /// </summary>
    public static DeathGold DeathGold(RunState state, RunMatchSummary summary, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(economy);
        return DeathGold(state, summary.DeathDetails, economy);
    }

    /// <summary>
    /// Igual que la sobrecarga de partido, pero sobre una lista de muertes suelta: es lo que comparten la
    /// muerte de partido y las muertes de fuera del partido (sacrificio, matasanos: <c>DeathConsequences</c>).
    /// </summary>
    public static DeathGold DeathGold(RunState state, IReadOnlyList<PlayerDeathDetail> deaths, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(deaths);
        ArgumentNullException.ThrowIfNull(economy);
        var rows = DeathGoldRows(state, deaths, economy.DeathGold);
        return new DeathGold(rows, SumDeathGold(rows));
    }

    /// <summary>
    /// Filas de oro por muerte (paquete BB): las muertes propias de este partido
    /// (<see cref="RunMatchSummary.DeathDetails"/>) cuyos perks tienen tarifa en <paramref name="rates"/>.
    /// Solo cuentan las del propio club -<c>MatchResolution</c> solo registra muertes de
    /// <c>matchEvent.Team == 0</c>-, con la misma comprobación de pertenencia a la plantilla que
    /// <see cref="CounterGoldRows"/> por si la instantánea cambiara entre el partido y este cálculo.
    /// Ordenadas por orden del propio evento y, dentro, por id de perk ascendente (RT-041, heredado del
    /// orden ya ordinal de <see cref="Model.RunPlayer.Perks"/>).
    /// </summary>
    private static IReadOnlyList<DeathGoldRow> DeathGoldRows(
        RunState state, IReadOnlyList<PlayerDeathDetail> details, DeathGoldTable rates)
    {
        if (details.Count == 0 || rates.Count == 0)
        {
            return Array.Empty<DeathGoldRow>();
        }

        var rows = new List<DeathGoldRow>();
        for (int i = 0; i < details.Count; i++)
        {
            var detail = details[i];
            if (state.FindPlayer(detail.PlayerId) is null)
            {
                continue;
            }

            var perks = detail.Perks;
            for (int p = 0; p < perks.Count; p++)
            {
                int rate = rates.RateFor(perks[p]);
                if (rate == 0)
                {
                    continue;
                }

                rows.Add(new DeathGoldRow(detail.PlayerId, perks[p], rate));
            }
        }

        return rows;
    }

    private static int SumDeathGold(IReadOnlyList<DeathGoldRow> rows)
    {
        int total = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            total += rows[i].Gold;
        }

        return total;
    }
}

/// <summary>
/// Desglose del oro de una victoria (RF-114g..i, RF-119): de dónde sale cada moneda. Todo son datos
/// conocidos antes de jugar (RF-114i: el oro nunca escala con el rendimiento).
/// </summary>
/// <param name="ActBase">Oro fijo por victoria del acto (RF-114g).</param>
/// <param name="Difficulty">Distintivo de dificultad del nodo, 1..5 (RF-012).</param>
/// <param name="DifficultyPercent">Multiplicador de esa dificultad, en tanto por ciento.</param>
/// <param name="AfterDifficulty">Oro tras aplicar la dificultad.</param>
/// <param name="NodeBonusPercent">Escalón por tipo de nodo, en tanto por ciento (ADR 0043).</param>
/// <param name="NodeBonus">Oro que suma ese escalón.</param>
/// <param name="Total">Oro cobrado, idéntico al de <see cref="GoldCalculator.GoldForWin"/>. <b>No</b>
/// incluye el oro de contador, que es otro canal y se cobra también al perder (ADR 0113).</param>
public sealed record GoldForWinBreakdown(
    int Act,
    int ActBase,
    int Difficulty,
    int DifficultyPercent,
    int AfterDifficulty,
    NodeKind NodeKind,
    int NodeBonusPercent,
    int NodeBonus,
    int Total);

/// <summary>
/// Una fila del oro que ha pagado un contador de partido (paquete Z, primitiva D). Es la mitad visible
/// del informe (RF-119) de un perk como "cada gol suyo llena la grada": sin esta fila, el oro que
/// paga aparecería mezclado en el total sin que el jugador pueda ver de dónde salió.
/// </summary>
/// <param name="PlayerId">Jugador propio cuyo contador ha generado el oro.</param>
/// <param name="Counter">Nombre del contador, el mismo id que declara el efecto <c>addCounter</c> del perk.</param>
/// <param name="Delta">Unidades que ese contador sumó en este partido.</param>
/// <param name="RatePerUnit">Oro que paga cada unidad, de <c>data/economy/counter-gold.json</c>.</param>
/// <param name="Gold">Oro de esta fila: <c>Delta * RatePerUnit</c> (RT-023, aritmética entera).</param>
public sealed record CounterGoldRow(int PlayerId, string Counter, int Delta, int RatePerUnit, int Gold);

/// <summary>
/// Oro que los contadores de un partido han pagado a la run (ADR 0113), con su desglose. Es un canal
/// <b>aparte</b> del premio de partido: el premio lo cobra quien gana (RF-114g), y esto se cobra siempre,
/// porque el jugador ya lo pagó por adelantado al gastar un slot en el perk. Un perk que solo rindiera en
/// las victorias no sería una inversión, sería una propina.
/// </summary>
/// <param name="Rows">Una fila por contador propio con tarifa; vacía si ninguno la tiene.</param>
/// <param name="Total">Suma de <see cref="CounterGoldRow.Gold"/> de todas las filas.</param>
public sealed record CounterGold(IReadOnlyList<CounterGoldRow> Rows, int Total)
{
    /// <summary>Ningún contador ha pagado nada: lo que devuelve un partido sin perks de negocio.</summary>
    public static CounterGold None { get; } = new(Array.Empty<CounterGoldRow>(), 0);
}

/// <summary>
/// Una fila del oro que ha pagado la muerte de un jugador (paquete BB, consumidor Seguro de vida). A
/// diferencia de <see cref="CounterGoldRow"/> no hay "unidades": una muerte es un suceso único, y
/// <see cref="Gold"/> es directamente la tarifa del perk.
/// </summary>
/// <param name="PlayerId">Jugador propio que ha muerto.</param>
/// <param name="PerkId">Perk cuya tarifa ha pagado esta fila.</param>
/// <param name="Gold">Oro de esta fila, de <c>data/economy/death-gold.json</c>.</param>
public sealed record DeathGoldRow(int PlayerId, string PerkId, int Gold);

/// <summary>
/// Oro que las muertes de un partido han pagado a la run (paquete BB), con su desglose. Canal aparte del
/// premio de partido y de <see cref="CounterGold"/>: se cobra se gane o se pierda, porque el jugador ya
/// pagó por adelantado el slot del perk.
/// </summary>
/// <param name="Rows">Una fila por muerte con perk de tarifa; vacía si ninguna la tiene.</param>
/// <param name="Total">Suma de <see cref="DeathGoldRow.Gold"/> de todas las filas.</param>
public sealed record DeathGold(IReadOnlyList<DeathGoldRow> Rows, int Total)
{
    /// <summary>Ninguna muerte ha pagado nada: lo que devuelve un partido sin muertes o sin perks de esta tabla.</summary>
    public static DeathGold None { get; } = new(Array.Empty<DeathGoldRow>(), 0);
}
