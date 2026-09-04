using Underleague.Sim.Events;

namespace Underleague.Sim.Engine;

/// <summary>
/// Una activación de perk con su contexto (RT-043), para el informe post-partido (RF-119).
/// <c>Detail</c> es el del evento disparador, con sufijo <c>":else"</c> si lo que se aplicó fueron los
/// <c>elseEffects</c> del perk (§7).
/// </summary>
public sealed record PerkActivation(string PerkId, int OwnerId, int Tick, EventType EventType, string Detail);

/// <summary>Activaciones agregadas de un perk de un jugador en el partido (RT-043).</summary>
public sealed record PerkActivationSummary(string PerkId, int OwnerId, int Activations);

/// <summary>
/// Lo que un contador de un jugador ha sumado en el partido a través de perks con
/// <c>accumulatesAcrossMatches: true</c> (RF-070, §6). La campaña lo suma al PlayerDefinition siguiente.
/// </summary>
public sealed record PlayerCounterDelta(int PlayerId, string Counter, int Delta);

/// <summary>Estadísticas de un jugador en un partido concreto.</summary>
public sealed record PlayerMatchStats(
    int PlayerId,
    int Team,
    int Goals,
    int Assists,
    int Shots,
    int PassesAttempted,
    int PassesCompleted,
    int Tackles,
    int TacklesWon,
    int Fouls,
    int Cards,
    bool Injured,
    int TicksOnPitch);

/// <summary>Una fila de la tabla de utilidad de una acción evaluada (RT-098).</summary>
public sealed record UtilityRow(PlayerAction Action, int Score, int Base, int TacticalMultiplier, int TraitMultiplier, int Context, bool LeashFiltered);

/// <summary>Volcado de la tabla de utilidad de un jugador en un tick concreto (SimConfig.DumpUtility, RT-098).</summary>
public sealed record UtilityDump(int PlayerId, int Tick, PlayerState State, IReadOnlyList<UtilityRow> Rows, PlayerAction Chosen);

/// <summary>
/// Informe final de un partido. Propiedades de solo lectura; solo se construye a partir de un
/// <see cref="MatchReportBuilder"/> (el motor, paquete B, es quien lo rellena tick a tick).
/// </summary>
public sealed class MatchReport
{
    /// <summary>Goles por equipo, [2].</summary>
    public int[] Goals { get; }

    /// <summary>Equipo ganador, 0 o 1; nunca hay empate (gol de oro; desempate si se agota el tiempo, 3.9).</summary>
    public int Winner { get; }

    /// <summary>Tick en el que terminó el partido.</summary>
    public int Ticks { get; }

    /// <summary>True si el partido llegó a MobGoldenGoal.</summary>
    public bool WentToGoldenGoal { get; }

    /// <summary>True si el partido terminó por incomparecencia (RF-059).</summary>
    public bool Forfeit { get; }

    /// <summary>Número de cambios de posesión (3.10), sin contar el saque inicial.</summary>
    public int PossessionChanges { get; }

    /// <summary>Número de cadenas de pases completadas (>= 1 pase completado antes de perder la posesión).</summary>
    public int PassChains { get; }

    /// <summary>Suma de longitudes de todas las cadenas de pases.</summary>
    public int PassChainTotalLength { get; }

    /// <summary>Tiros por equipo, [2].</summary>
    public int[] Shots { get; }

    /// <summary>Tiros a puerta por equipo, [2].</summary>
    public int[] ShotsOnTarget { get; }

    /// <summary>Entradas totales.</summary>
    public int Tackles { get; }

    /// <summary>Faltas totales.</summary>
    public int Fouls { get; }

    /// <summary>Tarjetas amarillas totales.</summary>
    public int YellowCards { get; }

    /// <summary>Tarjetas rojas totales.</summary>
    public int RedCards { get; }

    /// <summary>Lesiones totales.</summary>
    public int Injuries { get; }

    /// <summary>Muertes totales (siempre 0 en fase 0, RF-093 requiere estado previo).</summary>
    public int Deaths { get; }

    /// <summary>Ticks que el balón pasó en cada tercio absoluto del campo, [3].</summary>
    public int[] BallTicksByThird { get; }

    /// <summary>Ticks de posesión por equipo, [2].</summary>
    public int[] PossessionTicks { get; }

    /// <summary>Sesgo del árbitro al terminar el partido; en fase 0 es fijo e igual a InitialBias (§3).</summary>
    public int FinalBias { get; }

    /// <summary>Estadísticas por jugador que participó en el partido.</summary>
    public IReadOnlyList<PlayerMatchStats> Players { get; }

    /// <summary>Log de eventos relevantes en texto (RF-121); vacío si SimConfig.CollectLog es false.</summary>
    public IReadOnlyList<string> Log { get; }

    /// <summary>Tabla de utilidad volcada si SimConfig.DumpUtility coincidió con algún tick; null si no.</summary>
    public UtilityDump? UtilityDump { get; }

    /// <summary>True si el portero de algún equipo salió de su área en algún tick del partido (RF-057b).</summary>
    public bool GoalkeeperLeftArea { get; }

    /// <summary>Activaciones de perk en orden cronológico y, dentro de un evento, en el orden de RT-041.</summary>
    public IReadOnlyList<PerkActivation> PerkActivations { get; }

    /// <summary>Activaciones agregadas por (perk, jugador), ordenadas por id de perk y de jugador.</summary>
    public IReadOnlyList<PerkActivationSummary> PerksSummary { get; }

    /// <summary>Publicaciones descartadas por superar SimConfig.MaxDepth (RT-042).</summary>
    public int RecursionCuts { get; }

    internal MatchReport(MatchReportBuilder builder)
    {
        Goals = (int[])builder.Goals.Clone();
        Winner = builder.Winner;
        Ticks = builder.Ticks;
        WentToGoldenGoal = builder.WentToGoldenGoal;
        Forfeit = builder.Forfeit;
        PossessionChanges = builder.PossessionChanges;
        PassChains = builder.PassChains;
        PassChainTotalLength = builder.PassChainTotalLength;
        Shots = (int[])builder.Shots.Clone();
        ShotsOnTarget = (int[])builder.ShotsOnTarget.Clone();
        Tackles = builder.Tackles;
        Fouls = builder.Fouls;
        YellowCards = builder.YellowCards;
        RedCards = builder.RedCards;
        Injuries = builder.Injuries;
        Deaths = builder.Deaths;
        BallTicksByThird = (int[])builder.BallTicksByThird.Clone();
        PossessionTicks = (int[])builder.PossessionTicks.Clone();
        FinalBias = builder.FinalBias;
        Players = builder.Players.ToArray();
        Log = builder.Log.ToArray();
        UtilityDump = builder.UtilityDump;
        GoalkeeperLeftArea = builder.GoalkeeperLeftArea;
        PerkActivations = builder.PerkActivations.ToArray();
        PerksSummary = builder.PerksSummary.ToArray();
        RecursionCuts = builder.RecursionCuts;
    }
}

/// <summary>
/// Builder mutable de <see cref="MatchReport"/>. El motor (paquete B) va rellenando estos campos
/// durante el bucle de ticks (3.2-3.10) y llama a <see cref="Build"/> al terminar el partido.
/// </summary>
internal sealed class MatchReportBuilder
{
    /// <summary>Goles por equipo; se incrementa al resolver cada Goal (3.7).</summary>
    public int[] Goals { get; } = new int[2];

    /// <summary>Equipo ganador; se fija al terminar el partido (3.9).</summary>
    public int Winner { get; set; }

    /// <summary>Tick en el que terminó el partido; se fija en el último tick del bucle (3.2).</summary>
    public int Ticks { get; set; }

    /// <summary>Se pone a true si el partido entra en fase MobGoldenGoal (3.9).</summary>
    public bool WentToGoldenGoal { get; set; }

    /// <summary>Se pone a true si el partido termina por incomparecencia (3.8).</summary>
    public bool Forfeit { get; set; }

    /// <summary>Se incrementa en cada cambio de posesión detectado (3.10), sin contar el saque inicial.</summary>
    public int PossessionChanges { get; set; }

    /// <summary>Se incrementa al terminar una posesión con >= 1 pase completado (3.10).</summary>
    public int PassChains { get; set; }

    /// <summary>Se incrementa con la longitud de cada cadena de pases al terminar la posesión (3.10).</summary>
    public int PassChainTotalLength { get; set; }

    /// <summary>Tiros por equipo; se incrementa al resolver cada Shot (3.7).</summary>
    public int[] Shots { get; } = new int[2];

    /// <summary>Tiros a puerta por equipo; se incrementa cuando un Shot tiene Detail "onTarget" (3.7).</summary>
    public int[] ShotsOnTarget { get; } = new int[2];

    /// <summary>Entradas totales; se incrementa al resolver cada Tackle (3.7).</summary>
    public int Tackles { get; set; }

    /// <summary>Faltas totales; se incrementa en cada Foul (3.7).</summary>
    public int Fouls { get; set; }

    /// <summary>Tarjetas amarillas totales; se incrementa en cada Card amarilla (3.7).</summary>
    public int YellowCards { get; set; }

    /// <summary>Tarjetas rojas totales; se incrementa en cada Card roja (3.7).</summary>
    public int RedCards { get; set; }

    /// <summary>Lesiones totales; se incrementa en cada Injury (3.7).</summary>
    public int Injuries { get; set; }

    /// <summary>Muertes totales; se incrementa en cada Death (siempre 0 en fase 0).</summary>
    public int Deaths { get; set; }

    /// <summary>Ticks que el balón pasó en cada tercio absoluto del campo; se incrementa cada tick (3.10).</summary>
    public int[] BallTicksByThird { get; } = new int[3];

    /// <summary>Ticks de posesión por equipo; se incrementa cada tick con dueño de ese equipo (3.10).</summary>
    public int[] PossessionTicks { get; } = new int[2];

    /// <summary>
    /// Sesgo del árbitro al terminar el partido. En fase 0 es fijo e igual a InitialBias (§3, RF-060): el
    /// motor no tiene ningún mecanismo que lo mueva durante el partido todavía, así que se lee una sola
    /// vez al final en <see cref="MatchEngine.Run"/> (revisión independiente, fase 0: el comentario
    /// anterior decía "se actualiza cuando cambie", que no describía código existente).
    /// </summary>
    public int FinalBias { get; set; }

    /// <summary>Estadísticas por jugador; se acumulan durante el partido y se listan al terminar.</summary>
    public List<PlayerMatchStats> Players { get; } = new();

    /// <summary>Líneas de log (RF-121); se añade una por evento relevante si SimConfig.CollectLog es true.</summary>
    public List<string> Log { get; } = new();

    /// <summary>Tabla de utilidad volcada; se fija cuando el tick y jugador coinciden con SimConfig.DumpUtility.</summary>
    public UtilityDump? UtilityDump { get; set; }

    /// <summary>Se pone a true la primera vez que un portero sale de su área (RF-057b, MatchRulesTests).</summary>
    public bool GoalkeeperLeftArea { get; set; }

    /// <summary>Activaciones de perk registradas por el motor de efectos (RT-043).</summary>
    public List<PerkActivation> PerkActivations { get; } = new();

    /// <summary>Resumen por (perk, jugador); lo rellena el motor de efectos al terminar el partido.</summary>
    public List<PerkActivationSummary> PerksSummary { get; } = new();

    /// <summary>Publicaciones cortadas por profundidad de recursión (RT-042).</summary>
    public int RecursionCuts { get; set; }

    /// <summary>Construye el MatchReport inmutable final a partir del estado acumulado.</summary>
    public MatchReport Build() => new(this);
}
