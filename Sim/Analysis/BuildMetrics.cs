using Underleague.Sim.Perks;

namespace Underleague.Sim.Analysis;

/// <summary>
/// Resultado agregado de una celda de la matriz build × rival (docs/fase1-diseno.md §8): todas las
/// estadísticas relevantes de <paramref name="Build"/> a través de todos los partidos que jugó contra
/// <paramref name="Opponent"/> en un lote de <c>/Balance --builds</c> (cualquier local/visitante).
/// </summary>
public readonly record struct BuildCellResult(
    string Build,
    string Opponent,
    int Matches,
    int Wins,
    int GoalsFor,
    int GoalsAgainst,
    int InjuriesFor,
    int InjuriesAgainst,
    int Tackles,
    int PassChains,
    int PassChainTotalLength,
    int Activations)
{
    public double WinRate => Matches > 0 ? 100.0 * Wins / Matches : 0.0;

    public double InjuriesPerMatch => Matches > 0 ? (double)InjuriesFor / Matches : 0.0;

    /// <summary>Lesiones que esta build le ha causado al rival por partido (las que "produce", §8).</summary>
    public double InjuriesCausedPerMatch => Matches > 0 ? (double)InjuriesAgainst / Matches : 0.0;

    public double TacklesPerMatch => Matches > 0 ? (double)Tackles / Matches : 0.0;

    public double PassChainAvgLength => PassChains > 0 ? (double)PassChainTotalLength / PassChains : 0.0;

    public double ActivationsPerMatch => Matches > 0 ? (double)Activations / Matches : 0.0;
}

/// <summary>
/// Activaciones de un perk agregadas sobre todos los partidos de un lote en los que estaba asignado a
/// algún titular de <paramref name="Build"/>, sea cual sea el rival (RF-070, noDeadPerks de §8).
/// </summary>
public readonly record struct PerkActivationResult(
    string PerkId,
    string Build,
    int MatchesAssigned,
    int MatchesWithActivation)
{
    public double ActivationRate => MatchesAssigned > 0 ? 100.0 * MatchesWithActivation / MatchesAssigned : 0.0;
}

/// <summary>
/// Métricas de fase 1 sobre builds y perks (docs/fase1-diseno.md §8): coherencia (las builds coherentes
/// ganan a su referencia, las malas pierden, la aleatoria queda cerca de 50%), diferenciación de estilo de
/// juego, cobertura de activación del catálogo y distribución RF-069. Pura, sin E/S: recibe los resultados
/// ya agregados de un lote (<see cref="BuildCellResult"/>, <see cref="PerkActivationResult"/>) y las listas
/// de builds coherentes/malas/aleatoria como parámetro — nunca las codifica dentro, porque las usa también
/// la puerta estadística de <c>Sim.Tests</c> (paquete I) además de <c>/Balance</c>. Sigue el mismo formato
/// de salida que <see cref="MatchMetrics"/>: una lista de <see cref="MetricResult"/> (nombre, valor, rango,
/// estado IN/OUT/INFO).
/// </summary>
public static class BuildMetrics
{
    /// <summary>Prefijo de la métrica por build de <c>coherentBuildsBeatNone</c> (§8: cada coherente gana ≥ 58% a su referencia).</summary>
    public const string CoherentBuildsBeatNonePrefix = "coherentBuildsBeatNone_";

    /// <summary>Prefijo de la métrica por build de <c>badBuildsLoseToNone</c> (§8: cada mala gana ≤ 45% a su referencia).</summary>
    public const string BadBuildsLoseToNonePrefix = "badBuildsLoseToNone_";

    /// <summary>Prefijo de la métrica por build de <c>randomBuildNearNone</c> (§8: la build aleatoria entre 40% y 60%).</summary>
    public const string RandomBuildNearNonePrefix = "randomBuildNearNone_";

    /// <summary>Nombre de la métrica de más lesiones producidas por la build de contacto que por la técnica (§8).</summary>
    public const string BuildsWinDifferentlyInjuries = "buildsWinDifferently_injuries";

    /// <summary>Nombre de la métrica de mayor cadena media de pases de la build técnica que de la de contacto (§8).</summary>
    public const string BuildsWinDifferentlyPassChain = "buildsWinDifferently_passChain";

    /// <summary>
    /// Umbral de <see cref="BuildsWinDifferentlyPassChain"/> (ADR 0062). <b>Contra qué se mide</b>: contra
    /// lo que el canal de pase puede dar con la escala de cuotas, no contra un canal saturado.
    ///
    /// <para>El 1,30 de antes venía de la fórmula <b>aditiva</b> anterior a la ADR 0050 P1: un
    /// <c>pass +25</c> sumaba 2.500 puntos sobre una base de 7.700 y el pase quedaba clavado en el techo
    /// del 98%, de donde salía una cadena un 30% más larga. La escala de cuotas lo impide por
    /// construcción. Medido aislado —una build de <b>solo</b> siete <c>fine_touch</c> sobre el once contra
    /// <c>elf_none</c>, 2 × 1.440 partidos por escalón— el canal responde así:</para>
    ///
    /// <code>
    /// ×2 (techo común, que es el que lleva la build de medida)  1,108
    /// ×3 (techo poco común)                                     1,143
    /// ×4 (techo raro)                                           1,155
    /// ×6 (techo legendario)                                     1,191
    /// </code>
    ///
    /// <para>Es decir: <b>ni con el techo legendario</b> siete perks de pase alargan su propia cadena un
    /// 20%, y con el techo común —el de <c>fine_touch</c>— llegan al <b>10,8%</b>. Es el mismo hallazgo de
    /// AL-A: en un canal de base alta multiplicar la cuota casi no compra nada. El umbral pasa a ser
    /// exactamente ese 10,8% redondeado a la baja, o sea <b>1,11</b>: lo que el canal da con los perks que
    /// la build de medida puede llevar legalmente.</para>
    ///
    /// <para>No se ha elegido el techo de la escala (1,19 / 1,24 con la normalización) porque
    /// <c>elf_tiki_taka</c> lleva perks <b>comunes</b>: pedirle el techo legendario no es calibrar el
    /// umbral, es cambiar la afirmación. Y cuando AL-A se resuelva y el pase recupere recorrido, este
    /// número hay que <b>volver a derivarlo hacia arriba</b>: hoy lo acota la aritmética, no el diseño de
    /// la build.</para>
    /// </summary>
    public const double MinPassChainRatio = 1.11;

    /// <summary>Prefijo de la métrica por perk de tasa de activación (§8: noDeadPerks, ≥ 1% de los partidos en los que está asignado).</summary>
    public const string ActivationRatePrefix = "activationRate_";

    /// <summary>Nombre del resumen agregado: cuántos perks del catálogo no llegan al 1% de activación (0 esperado).</summary>
    public const string NoDeadPerks = "noDeadPerks";

    /// <summary>Umbral mínimo de activación de un perk para no considerarlo muerto (RF-070, §8).</summary>
    public const double DeadPerkThresholdPercent = 1.0;

    /// <summary>Nombre de la métrica de porcentaje de perks <c>filler</c> del catálogo (RF-069).</summary>
    public const string Rf069Filler = "rf069_filler";

    /// <summary>Nombre de la métrica de porcentaje de perks <c>conditional</c> del catálogo (RF-069).</summary>
    public const string Rf069Conditional = "rf069_conditional";

    /// <summary>Nombre de la métrica de porcentaje de perks <c>ruleBreaker</c> del catálogo (RF-069).</summary>
    public const string Rf069RuleBreaker = "rf069_ruleBreaker";

    /// <summary>Distribución objetivo RF-069: 60% filler, 30% conditional, 10% ruleBreaker, tolerancia ± 8 puntos.</summary>
    public const int Rf069Tolerance = 8;

    /// <summary>
    /// Calcula todas las métricas de fase 1 en el orden: coherentBuildsBeatNone (una fila por build de
    /// <paramref name="coherentBuilds"/>), badBuildsLoseToNone, randomBuildNearNone, buildsWinDifferently
    /// (si se dan <paramref name="physicalBuild"/>/<paramref name="technicalBuild"/>), noDeadPerks (una
    /// fila por perk más el resumen) y la distribución RF-069.
    /// </summary>
    /// <param name="cells">Resultados agregados de la matriz build × rival de un lote.</param>
    /// <param name="coherentBuilds">Ids de las builds que el diseño espera coherentes (§7/§8).</param>
    /// <param name="badBuilds">Ids de las builds mal construidas a propósito.</param>
    /// <param name="randomBuilds">Ids de las builds sin criterio (deben quedar cerca del 50%).</param>
    /// <param name="baselineOpponentByBuild">
    /// Para cada build de <paramref name="coherentBuilds"/>/<paramref name="badBuilds"/>/<paramref name="randomBuilds"/>,
    /// el id de su build de referencia sin perks (misma raza). Resuelto por el llamador a partir de
    /// <c>data/balance/builds/_groups.json</c> (<c>baselineByRace</c>) y la raza de cada build: este código
    /// no conoce razas ni ficheros, solo el mapeo ya resuelto.
    /// </param>
    /// <param name="physicalBuild">
    /// Build de referencia "gana por contacto" para <c>buildsWinDifferently</c> (p. ej. <c>orc_violence</c>);
    /// null omite esa métrica. No se codifica dentro a propósito: qué dos builds representan los dos estilos
    /// a comparar es una decisión de diseño de <c>data/balance/builds/</c>, no de este cálculo.
    /// </param>
    /// <param name="technicalBuild">Build de referencia "gana por técnica" para <c>buildsWinDifferently</c> (p. ej. <c>elf_tiki_taka</c>).</param>
    /// <param name="perkActivations">Activación agregada de cada (perk, build) que lo tiene asignado, para <c>noDeadPerks</c>.</param>
    /// <param name="catalogPerkKinds"><see cref="PerkKind"/> de cada perk del catálogo cargado, para la distribución RF-069.</param>
    public static List<MetricResult> Compute(
        IReadOnlyList<BuildCellResult> cells,
        IReadOnlyList<string> coherentBuilds,
        IReadOnlyList<string> badBuilds,
        IReadOnlyList<string> randomBuilds,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        string? physicalBuild,
        string? technicalBuild,
        IReadOnlyList<PerkActivationResult> perkActivations,
        IReadOnlyList<PerkKind> catalogPerkKinds)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(coherentBuilds);
        ArgumentNullException.ThrowIfNull(badBuilds);
        ArgumentNullException.ThrowIfNull(randomBuilds);
        ArgumentNullException.ThrowIfNull(baselineOpponentByBuild);
        ArgumentNullException.ThrowIfNull(perkActivations);
        ArgumentNullException.ThrowIfNull(catalogPerkKinds);

        var rows = new List<MetricResult>();
        rows.AddRange(CoherentBuildsBeatNone(cells, coherentBuilds, baselineOpponentByBuild));
        rows.AddRange(BadBuildsLoseToNone(cells, badBuilds, baselineOpponentByBuild));
        rows.AddRange(RandomBuildsNearNone(cells, randomBuilds, baselineOpponentByBuild));
        rows.AddRange(BuildsWinDifferently(cells, physicalBuild, technicalBuild, baselineOpponentByBuild));
        rows.AddRange(NoDeadPerksRows(perkActivations));
        rows.AddRange(Rf069Distribution(catalogPerkKinds));
        return rows;
    }

    /// <summary>coherentBuildsBeatNone (§8): cada build coherente gana ≥ 58% contra su referencia de la misma raza.</summary>
    public static List<MetricResult> CoherentBuildsBeatNone(
        IReadOnlyList<BuildCellResult> cells,
        IReadOnlyList<string> coherentBuilds,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        double minWinRate = 58.0) =>
        AtLeast(cells, coherentBuilds, baselineOpponentByBuild, CoherentBuildsBeatNonePrefix, minWinRate);

    /// <summary>badBuildsLoseToNone (§8): cada build mala gana ≤ 45% contra su referencia de la misma raza.</summary>
    public static List<MetricResult> BadBuildsLoseToNone(
        IReadOnlyList<BuildCellResult> cells,
        IReadOnlyList<string> badBuilds,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        double maxWinRate = 45.0) =>
        AtMost(cells, badBuilds, baselineOpponentByBuild, BadBuildsLoseToNonePrefix, maxWinRate);

    /// <summary>randomBuildNearNone (§8): la build sin criterio queda entre 40% y 60% contra su referencia.</summary>
    public static List<MetricResult> RandomBuildsNearNone(
        IReadOnlyList<BuildCellResult> cells,
        IReadOnlyList<string> randomBuilds,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        double minWinRate = 40.0,
        double maxWinRate = 60.0)
    {
        var rows = new List<MetricResult>();
        foreach (var build in randomBuilds)
        {
            if (!TryFindCell(cells, build, baselineOpponentByBuild, out var cell))
            {
                continue;
            }

            double rate = cell.WinRate;
            rows.Add(new MetricResult(
                RandomBuildNearNonePrefix + build, rate, minWinRate, maxWinRate,
                rate >= minWinRate && rate <= maxWinRate ? "IN" : "OUT"));
        }

        return rows;
    }

    /// <summary>
    /// buildsWinDifferently (§8): la build "de contacto" produce ≥ 1,5× las lesiones que la build
    /// "técnica", y la técnica encadena ≥ <see cref="MinPassChainRatio"/> los pases de la de contacto
    /// (ADR 0062; era 1,3 contra un canal que la ADR 0050 P1 hizo imposible de saturar). Las dos se miden
    /// <b>normalizadas contra la referencia sin perks de la propia raza</b> (paquete I, ADR 0012): lo que
    /// se compara es "cuánto multiplica esta build lo que ya hacía su raza", no el valor absoluto.
    ///
    /// Sin normalizar, la métrica medía la raza y no la build: <c>orc_none</c> ya causa 3,9 veces las
    /// lesiones de <c>elf_none</c> sin un solo perk, así que la mitad de lesiones aprobaba con el catálogo
    /// vacío; y la cadena de pases de un equipo de orcos (lentos, correa corta, bloque junto) es más larga
    /// que la de uno de elfos (rápidos, correa larga, que regatean) pase lo que pase con los perks, así que
    /// la mitad de cadena no podía aprobar nunca. Con la normalización las dos miden el efecto de los
    /// perks, que es lo que §8 quiere decir con "ganan de formas distintas".
    ///
    /// El denominador de cada build es su propia referencia <b>en los mismos partidos</b>: la celda
    /// (referencia, build) de la matriz, que es la otra cara de la celda (build, referencia).
    /// </summary>
    /// <param name="baselineOpponentByBuild">Referencia sin perks de cada build (misma raza), ya resuelta.</param>
    public static List<MetricResult> BuildsWinDifferently(
        IReadOnlyList<BuildCellResult> cells,
        string? physicalBuild,
        string? technicalBuild,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        double minInjuryRatio = 1.5,
        double minPassChainRatio = MinPassChainRatio)
    {
        ArgumentNullException.ThrowIfNull(baselineOpponentByBuild);

        var rows = new List<MetricResult>();
        if (physicalBuild is null || technicalBuild is null)
        {
            return rows;
        }

        if (!TryFindPair(cells, physicalBuild, baselineOpponentByBuild, out var physical, out var physicalBase)
            || !TryFindPair(cells, technicalBuild, baselineOpponentByBuild, out var technical, out var technicalBase))
        {
            return rows;
        }

        double physicalInjuries = Relative(physical.InjuriesCausedPerMatch, physicalBase.InjuriesCausedPerMatch);
        double technicalInjuries = Relative(technical.InjuriesCausedPerMatch, technicalBase.InjuriesCausedPerMatch);
        double injuryRatio = Relative(physicalInjuries, technicalInjuries);
        rows.Add(new MetricResult(
            BuildsWinDifferentlyInjuries, injuryRatio, minInjuryRatio, null,
            injuryRatio >= minInjuryRatio ? "IN" : "OUT"));

        double technicalChain = Relative(technical.PassChainAvgLength, technicalBase.PassChainAvgLength);
        double physicalChain = Relative(physical.PassChainAvgLength, physicalBase.PassChainAvgLength);
        double passChainRatio = Relative(technicalChain, physicalChain);
        rows.Add(new MetricResult(
            BuildsWinDifferentlyPassChain, passChainRatio, minPassChainRatio, null,
            passChainRatio >= minPassChainRatio ? "IN" : "OUT"));

        return rows;
    }

    /// <summary>Cociente con el caso degenerado explícito: 0/0 = 0, x/0 = +infinito.</summary>
    private static double Relative(double value, double reference) =>
        reference > 0 ? value / reference : (value > 0 ? double.PositiveInfinity : 0.0);

    /// <summary>Celdas (build, referencia) y (referencia, build): las dos caras de los mismos partidos.</summary>
    private static bool TryFindPair(
        IReadOnlyList<BuildCellResult> cells,
        string build,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        out BuildCellResult cell,
        out BuildCellResult baselineCell)
    {
        baselineCell = default;
        if (!TryFindCell(cells, build, baselineOpponentByBuild, out cell))
        {
            return false;
        }

        string baseline = baselineOpponentByBuild[build];
        for (int i = 0; i < cells.Count; i++)
        {
            if (string.Equals(cells[i].Build, baseline, StringComparison.Ordinal)
                && string.Equals(cells[i].Opponent, build, StringComparison.Ordinal))
            {
                baselineCell = cells[i];
                return baselineCell.Matches > 0;
            }
        }

        return false;
    }

    /// <summary>
    /// noDeadPerks (§8, RF-070): una fila por (perk, build) con su tasa de activación (≥ 1% de los
    /// partidos en los que estaba asignado) más un resumen agregado que cuenta cuántas de esas filas
    /// están OUT (0 esperado: ningún perk muerto en ninguna build que lo use).
    /// </summary>
    public static List<MetricResult> NoDeadPerksRows(
        IReadOnlyList<PerkActivationResult> perkActivations,
        double minActivationRatePercent = DeadPerkThresholdPercent)
    {
        var rows = new List<MetricResult>();

        // Una fila informativa por (perk, build): sirve para ver dónde se activa cada perk, pero NO
        // bloquea. El umbral de §8 es "cada perk se activa en >= 1% de los partidos de ALGUNA build que
        // lo lleve": una build mal construida a propósito (orc_misplaced pone perks técnicos en orcos)
        // existe justamente para que sus perks NO se disparen, y exigirle activación convertía el
        // criterio de salida en su contrario (corregido en el paquete U).
        foreach (var activation in perkActivations.OrderBy(a => a.PerkId, StringComparer.Ordinal).ThenBy(a => a.Build, StringComparer.Ordinal))
        {
            rows.Add(new MetricResult(
                ActivationRatePrefix + activation.PerkId + "_" + activation.Build,
                activation.ActivationRate, minActivationRatePercent, null, "INFO"));
        }

        // Fila con estado por perk: la mejor de sus builds.
        int dead = 0;
        foreach (var group in perkActivations.GroupBy(a => a.PerkId, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            double best = group.Max(a => a.ActivationRate);
            bool ok = best >= minActivationRatePercent;
            if (!ok)
            {
                dead++;
            }

            rows.Add(new MetricResult(
                ActivationRatePrefix + group.Key, best, minActivationRatePercent, null, ok ? "IN" : "OUT"));
        }

        rows.Add(new MetricResult(NoDeadPerks, dead, null, 0, dead == 0 ? "IN" : "OUT"));
        return rows;
    }

    /// <summary>Distribución RF-069 (60/30/10 ± 8 puntos) sobre el catálogo de perks cargado.</summary>
    public static List<MetricResult> Rf069Distribution(IReadOnlyList<PerkKind> catalogPerkKinds)
    {
        var rows = new List<MetricResult>();
        int total = catalogPerkKinds.Count;
        if (total == 0)
        {
            return rows;
        }

        int filler = catalogPerkKinds.Count(k => k == PerkKind.Filler);
        int conditional = catalogPerkKinds.Count(k => k == PerkKind.Conditional);
        int ruleBreaker = catalogPerkKinds.Count(k => k == PerkKind.RuleBreaker);

        rows.Add(DistributionRow(Rf069Filler, filler, total, 60));
        rows.Add(DistributionRow(Rf069Conditional, conditional, total, 30));
        rows.Add(DistributionRow(Rf069RuleBreaker, ruleBreaker, total, 10));
        return rows;
    }

    private static MetricResult DistributionRow(string name, int count, int total, int target)
    {
        double pct = 100.0 * count / total;
        double min = target - Rf069Tolerance;
        double max = target + Rf069Tolerance;
        return new MetricResult(name, pct, min, max, pct >= min && pct <= max ? "IN" : "OUT");
    }

    private static List<MetricResult> AtLeast(
        IReadOnlyList<BuildCellResult> cells,
        IReadOnlyList<string> builds,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        string prefix,
        double minWinRate)
    {
        var rows = new List<MetricResult>();
        foreach (var build in builds)
        {
            if (!TryFindCell(cells, build, baselineOpponentByBuild, out var cell))
            {
                continue;
            }

            double rate = cell.WinRate;
            rows.Add(new MetricResult(prefix + build, rate, minWinRate, null, rate >= minWinRate ? "IN" : "OUT"));
        }

        return rows;
    }

    private static List<MetricResult> AtMost(
        IReadOnlyList<BuildCellResult> cells,
        IReadOnlyList<string> builds,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        string prefix,
        double maxWinRate)
    {
        var rows = new List<MetricResult>();
        foreach (var build in builds)
        {
            if (!TryFindCell(cells, build, baselineOpponentByBuild, out var cell))
            {
                continue;
            }

            double rate = cell.WinRate;
            rows.Add(new MetricResult(prefix + build, rate, null, maxWinRate, rate <= maxWinRate ? "IN" : "OUT"));
        }

        return rows;
    }

    private static bool TryFindCell(
        IReadOnlyList<BuildCellResult> cells,
        string build,
        IReadOnlyDictionary<string, string> baselineOpponentByBuild,
        out BuildCellResult cell)
    {
        cell = default;
        if (!baselineOpponentByBuild.TryGetValue(build, out var baseline))
        {
            return false;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (string.Equals(cells[i].Build, build, StringComparison.Ordinal)
                && string.Equals(cells[i].Opponent, baseline, StringComparison.Ordinal))
            {
                cell = cells[i];
                return cell.Matches > 0;
            }
        }

        return false;
    }
}
