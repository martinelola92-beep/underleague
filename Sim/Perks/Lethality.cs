using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// <c>tuning.injury.lethality</c> (ADR 0048). Desde que <b>un jugador sano puede morir</b>, alcanzar a
/// una víctima ya no es matarla: es tirar por ella. Esta es la tirada, y sus tres factores son
/// exactamente las tres cosas que el jugador controla antes de confirmar la alineación (RF-012c):
/// <list type="number">
/// <item><b>A quién alineas</b>: la resistencia de la víctima entra con
/// <see cref="RelativeFactor"/> × (fuerza del portador − aguante de la víctima), el mismo criterio
/// relativo de la ADR 0041 con el que se resuelve una lesión.</item>
/// <item><b>En qué estado lo alineas</b>: un tocado muere mucho más fácil
/// (<see cref="MinorInjuryPercent"/>, <see cref="SevereInjuryPercent"/>). Es lo que queda de la regla
/// vieja de RF-093, convertida de puerta en multiplicador.</item>
/// <item><b>Dónde lo colocas</b>: <see cref="ProximityStepPercent"/> por casilla de
/// <see cref="Lethality.Matchup"/>, con suelo en <see cref="ProximityMinPercent"/>. Alejar a alguien de
/// la banda del asesino baja su número sin sacarlo del once.</item>
/// </list>
/// </summary>
public sealed record LethalityTuning(
    int RelativeFactor,
    int ProximityBasePercent,
    int ProximityStepPercent,
    int ProximityMinPercent,
    int MinorInjuryPercent,
    int SevereInjuryPercent,
    int ResistanceMinPercent,
    int ResistanceMaxPercent,
    int MaxChance,
    int VictimsPerActivation);

/// <summary>
/// La tirada de muerte de un perk letal (RF-093 vía 2, ADR 0048), <b>pura y entera</b>: la usa el motor
/// para matar (<c>MatchEngine.LethalHits</c>) y la usa el indicador de riesgo por jugador para
/// enseñar el número antes de jugar (<c>RunEngine.LineupWarnings</c>, RF-012c). Es el mismo código en los
/// dos sitios a propósito: si el indicador y el motor pudieran divergir, el número dejaría de ser una
/// promesa y RF-012d se rompería en silencio.
/// </summary>
public static class Lethality
{
    /// <summary>
    /// Distancia de <b>emparejamiento</b> entre dos jugadores de equipos contrarios, en casillas.
    ///
    /// <para>No es la distancia en el saque inicial —ahí los dos equipos están cada uno en su mitad y
    /// todo el mundo está lejos de todo el mundo— sino la del <b>enfrentamiento</b>: un delantero rival
    /// y mi defensa central se van a encontrar, y mi delantero y su defensa también. Se obtiene
    /// reflejando al portador sobre el eje de colocación: con columnas locales 0..7 medidas desde la
    /// portería propia (RF-040..045), el reflejo del portador es <c>7 − columna</c>, así que la distancia
    /// en columnas es <c>|victimaCol + portadorCol − 7|</c>, simétrica y sin marco de referencia. Las
    /// filas no se reflejan: la banda derecha del campo es la misma para los dos equipos.</para>
    ///
    /// <para>Que dependa solo de las casillas-hogar, y no de dónde estén cuando el perk se dispara, es
    /// deliberado: es lo que hace que el indicador previo sea <b>exacto</b> y no una estimación
    /// (RF-012d).</para>
    /// </summary>
    public static int Matchup(Cell victimHome, Cell killerHome)
    {
        int columns = Math.Abs(victimHome.Column + killerHome.Column - (Pitch.PlacementColumns - 1));
        int rows = Math.Abs(victimHome.Row - killerHome.Row);
        return Math.Max(columns, rows);
    }

    /// <summary>
    /// La misma distancia a partir de las casillas <b>absolutas</b> del partido: el motor refleja las
    /// columnas del equipo 1 al colocar (<c>MatchEngine</c>), así que hay que deshacer el reflejo antes
    /// de emparejar.
    /// </summary>
    public static int MatchupAbsolute(Cell victimHome, int victimTeam, Cell killerHome, int killerTeam) =>
        Matchup(ToLocal(victimHome, victimTeam), ToLocal(killerHome, killerTeam));

    /// <summary>Casilla de colocación (columna 0..7 desde la portería propia) de una casilla absoluta.</summary>
    public static Cell ToLocal(Cell absolute, int team) =>
        team == 0 ? absolute : new Cell(Pitch.Columns - 1 - absolute.Column, absolute.Row);

    /// <summary>Factor de cercanía, en tanto por ciento, de una distancia de emparejamiento.</summary>
    public static int ProximityPercent(LethalityTuning tuning, int distance)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        int percent = tuning.ProximityBasePercent - (distance * tuning.ProximityStepPercent);
        return Math.Clamp(percent, tuning.ProximityMinPercent, tuning.ProximityBasePercent);
    }

    /// <summary>
    /// Factor de estado, en tanto por ciento. Un sano vale 100 —ADR 0048: ya no es inmune— y cada
    /// escalón de desgaste lo multiplica.
    /// </summary>
    public static int StatePercent(LethalityTuning tuning, PhysicalState state, bool hurtInThisMatch)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        if (state == PhysicalState.SevereInjury)
        {
            return tuning.SevereInjuryPercent;
        }

        return state == PhysicalState.MinorInjury || hurtInThisMatch ? tuning.MinorInjuryPercent : 100;
    }

    /// <summary>
    /// Probabilidad de que ese portador mate a esa víctima, en base 10.000, acotada a
    /// <see cref="LethalityTuning.MaxChance"/>. Es la tirada del motor y el número del indicador.
    /// </summary>
    public static int Chance(
        LethalityTuning tuning,
        int perkLethalChance,
        int killerStrength,
        int victimStamina,
        int statePercent,
        int distance)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        int chance = perkLethalChance * statePercent / 100;
        chance = chance * ProximityPercent(tuning, distance) / 100;
        chance = chance * ResistancePercent(tuning, killerStrength, victimStamina) / 100;
        return Math.Clamp(chance, 0, tuning.MaxChance);
    }

    /// <summary>
    /// Factor de resistencia, en tanto por ciento: fuerza del portador contra aguante de la víctima, el
    /// criterio relativo de la ADR 0041. Es <b>multiplicativo</b> y no un sumando, y eso importa: con una
    /// probabilidad base de miles de puntos, sumarle dos por punto de atributo hacía que el aguante no
    /// significara nada y que elegir a quién alinear no cambiara el número (medido: 1,62 muertes contra
    /// 1,82 leyendo o sin leer el indicador, indistinguible). Multiplicando, un aguante de 75 recibe
    /// aproximadamente la mitad de lo que recibe uno de 35, que es lo que convierte "pon al duro donde
    /// muerde" en una decisión con efecto.
    /// </summary>
    public static int ResistancePercent(LethalityTuning tuning, int killerStrength, int victimStamina)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        int percent = 100 + (tuning.RelativeFactor * (killerStrength - victimStamina));
        return Math.Clamp(percent, tuning.ResistanceMinPercent, tuning.ResistanceMaxPercent);
    }

    /// <summary>
    /// Un portador de perk letal ya resuelto para el cálculo: dónde se coloca, con cuánta fuerza y con
    /// qué probabilidad base mata. Es lo que hace falta para poner número al riesgo de un titular sin
    /// volver a recorrer el catálogo por cada casilla que el jugador pruebe.
    /// </summary>
    public readonly record struct LethalCarrier(Cell Home, int Strength, int LethalChance);

    /// <summary>
    /// Portadores de perk letal <b>titulares</b> de ese equipo (RF-013), por id de jugador y de perk
    /// ascendentes. Un portador que no está en el once no cuenta: no va a saltar al campo, y anunciar un
    /// peligro que no existe está tan prohibido como callarse uno que sí (RF-012d).
    /// </summary>
    public static IReadOnlyList<LethalCarrier> CarriersOf(TeamSetup team, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(catalog);

        var threats = Scouting.LethalPerks(team, catalog);
        var carriers = new List<LethalCarrier>(threats.Count);
        for (int i = 0; i < threats.Count; i++)
        {
            Cell? home = null;
            for (int s = 0; s < team.Lineup.Slots.Count; s++)
            {
                if (team.Lineup.Slots[s].PlayerId == threats[i].PlayerId)
                {
                    home = team.Lineup.Slots[s].HomeCell;
                    break;
                }
            }

            if (home is null)
            {
                continue;
            }

            int strength = 0;
            for (int p = 0; p < team.Players.Count; p++)
            {
                if (team.Players[p].Id == threats[i].PlayerId)
                {
                    strength = team.Players[p].Attributes.Strength;
                    break;
                }
            }

            carriers.Add(new LethalCarrier(home.Value, strength, catalog.Perks.Get(threats[i].PerkId).LethalChance));
        }

        return carriers;
    }

    /// <summary>Un titular tal y como lo ve el cálculo de riesgo: cómo llega, cuánto aguanta y dónde está.</summary>
    public readonly record struct Exposed(int PlayerId, PhysicalState State, int Stamina, Cell Cell);

    /// <summary>
    /// <b>Exposición</b> de un titular a esos portadores, en base 10.000: lo que le pasaría <b>si lo
    /// marcaran todos</b>. No es su probabilidad de morir —solo muere el marcado, ver
    /// <see cref="MarkedRisks"/>— sino la medida de lo apetecible que es como víctima, y es el número con
    /// el que se comparan dos candidatos para una casilla.
    /// </summary>
    public static int Exposure(
        LethalityTuning tuning,
        IReadOnlyList<LethalCarrier> carriers,
        PhysicalState state,
        int stamina,
        Cell cell)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(carriers);

        int statePercent = StatePercent(tuning, state, hurtInThisMatch: false);
        int survives = 10000;
        for (int i = 0; i < carriers.Count; i++)
        {
            survives = survives * (10000 - ChanceAgainst(tuning, carriers[i], statePercent, stamina, cell)) / 10000;
        }

        return 10000 - survives;
    }

    /// <summary>Probabilidad de que ese portador concreto mate a esa víctima concreta.</summary>
    public static int ChanceAgainst(
        LethalityTuning tuning, LethalCarrier carrier, int statePercent, int stamina, Cell cell) =>
        Chance(tuning, carrier.LethalChance, carrier.Strength, stamina, statePercent, Matchup(carrier.Home, cell));

    /// <summary>
    /// Probabilidad real de morir de <b>cada</b> titular de ese once, en base 10.000 y en el orden en que
    /// llegan (RF-012c, ADR 0048).
    ///
    /// <para>Un perk letal no siega el equipo entero: <b>marca</b>
    /// <see cref="LethalityTuning.VictimsPerActivation"/> rivales por activación, y marca al que peor lo
    /// tiene —mayor probabilidad de morir, y a igualdad el de menor id (RT-041)—. Es lo que convierte la
    /// letalidad en una decisión de alineación en vez de en un impuesto: el peligro se concentra en el
    /// eslabón más débil del once, así que <b>quitar ese eslabón baja el número</b>. Sacar al tocado,
    /// poner al más duro en la casilla que el carnicero cubre o alejar de él al frágil cambian quién es
    /// el marcado y cuánto vale su tirada; los tres son movimientos del jugador, y los tres se ven en
    /// este vector antes de confirmar (RF-012d).</para>
    /// </summary>
    public static IReadOnlyList<int> MarkedRisks(
        LethalityTuning tuning, IReadOnlyList<LethalCarrier> carriers, IReadOnlyList<Exposed> starters)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(carriers);
        ArgumentNullException.ThrowIfNull(starters);

        var survives = new int[starters.Count];
        for (int i = 0; i < starters.Count; i++)
        {
            survives[i] = 10000;
        }

        var chances = new int[starters.Count];
        for (int c = 0; c < carriers.Count; c++)
        {
            for (int i = 0; i < starters.Count; i++)
            {
                chances[i] = ChanceAgainst(
                    tuning,
                    carriers[c],
                    StatePercent(tuning, starters[i].State, hurtInThisMatch: false),
                    starters[i].Stamina,
                    starters[i].Cell);
            }

            int marks = Math.Min(tuning.VictimsPerActivation, starters.Count);
            for (int m = 0; m < marks; m++)
            {
                int pick = -1;
                for (int i = 0; i < starters.Count; i++)
                {
                    if (chances[i] < 0)
                    {
                        continue;
                    }

                    if (pick < 0
                        || chances[i] > chances[pick]
                        || (chances[i] == chances[pick] && starters[i].PlayerId < starters[pick].PlayerId))
                    {
                        pick = i;
                    }
                }

                if (pick < 0)
                {
                    break;
                }

                survives[pick] = survives[pick] * (10000 - chances[pick]) / 10000;
                chances[pick] = -1;
            }
        }

        var risks = new int[starters.Count];
        for (int i = 0; i < starters.Count; i++)
        {
            risks[i] = 10000 - survives[i];
        }

        return risks;
    }
}
