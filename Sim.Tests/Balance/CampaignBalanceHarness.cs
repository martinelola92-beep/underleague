using Underleague.Balance;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Envoltorio sobre <c>Balance/PerkValueRunner.cs</c> (ADR 0070/0087) para los perks
/// <see cref="MetricReadiness.NeedsCampaignHarness"/> (§16, punto 1 del encargo del 18 sep 2026):
/// <b>no reimplementa</b> el arrastre de contador entre partidos — ya existe, probado y usado por
/// <c>/Balance --perk-values</c> — solo lo invoca con un filtro de un perk y convierte su resultado en la
/// forma que ya entienden <see cref="BalancePowerCheck"/>/<see cref="BalanceDecisionRules"/>.
/// </summary>
public static class CampaignBalanceHarness
{
    /// <summary>
    /// Mide <paramref name="perkId"/> con la campaña completa (<see cref="PerkValueRunner.CampaignMatches"/>,
    /// 8 partidos — el recorrido real del contador según ADR 0070, no un número elegido aquí).
    /// Devuelve <c>null</c> si ningún titular generado puede llevar el perk (mismo criterio que
    /// `PerkValueRunner.Measure` — no se inventa una plantilla a medida).
    /// </summary>
    public static PerkValueRow? Run(Catalog catalog, string perkId, ulong seed, int rosters, int? matchesPerRoster = null)
    {
        var rows = PerkValueRunner.Run(
            catalog, seed, rosters, matchesPerRoster ?? PerkValueRunner.CampaignMatches,
            new HashSet<string> { perkId });
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>
    /// Convierte una fila de campaña en la observación armado/control que necesita el motor de decisión:
    /// la tasa de victoria emparejada como proporción binomial (media <c>p</c>, varianza <c>p(1-p)</c> —
    /// la fórmula estándar de una proporción, no una aproximación inventada para este protocolo) sobre
    /// <see cref="PerkValueRow.Matches"/> partidos. Es la misma métrica que ya usa la ADR 0087 para decidir
    /// si un perk vale algo (<see cref="PerkValueRow.PairedValueMilli"/>), reutilizada, no una nueva.
    /// </summary>
    public static (double DeltaWinRate, double VarianceArmed, double VarianceControl, int Matches) ToWinRateObservation(PerkValueRow row)
    {
        double pArmed = row.Matches > 0 ? (double)row.Wins / row.Matches : 0.0;
        double pControl = row.Matches > 0 ? (double)row.ControlWins / row.Matches : 0.0;
        return (pArmed - pControl, pArmed * (1 - pArmed), pControl * (1 - pControl), row.Matches);
    }
}

/// <summary>
/// Selecciona el harness correcto para un perk ya clasificado (§16, punto 1): quien orquesta ("perk real
/// → classifier → campaign harness correcto → control/treatment → métricas → decision engine", tal cual
/// pide el encargo) consulta esto, no decide a mano cuándo usar cada uno.
/// </summary>
public enum HarnessKind
{
    /// <summary>Un solo portador, un partido suelto por muestra (<see cref="PairedBalanceHarness"/>).</summary>
    SingleMatch,

    /// <summary>Campaña con arrastre de contador entre partidos (<see cref="CampaignBalanceHarness"/>).</summary>
    Campaign,

    /// <summary>Ningún harness de este protocolo puede medirlo todavía (bloqueado, con motivo explícito).</summary>
    None,
}

public static class HarnessSelector
{
    public static HarnessKind SelectHarness(PerkAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.PrimaryMetricReadiness == MetricReadiness.NeedsCampaignHarness)
        {
            return HarnessKind.Campaign;
        }

        return entry.FinalReadiness == AuditReadiness.ReadyForScreening ? HarnessKind.SingleMatch : HarnessKind.None;
    }
}
