using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run;

/// <summary>
/// Equipo del jugador listo para <c>Simulator.Run</c>: titulares, suplentes y colocación.
/// </summary>
/// <param name="Starters">Titulares, ya convertidos a <see cref="PlayerDefinition"/> (con la penalización de lesión leve aplicada).</param>
/// <param name="Bench">Disponibles que no juegan; cobran su parte de experiencia (RF-025).</param>
/// <param name="Lineup">Colocación en la cuadrícula (RF-041).</param>
/// <param name="EmergencyGoalkeeperId">
/// Id del jugador de campo que ha tenido que ponerse de portero, o -1. Ver
/// <see cref="RunLineup"/> para el porqué.
/// </param>
public sealed record MatchLineup(
    IReadOnlyList<PlayerDefinition> Starters,
    IReadOnlyList<PlayerDefinition> Bench,
    Lineup Lineup,
    int EmergencyGoalkeeperId);

/// <summary>
/// <b>El once efectivo</b> (ADR 0134): quién va a saltar al campo de verdad, que no es lo mismo que
/// <c>RunState.Lineup</c>. Esa es la alineación <b>guardada</b> —la intención del jugador—, y el partido se
/// juega con la que <see cref="RunLineup.Build"/> construye completando los huecos por rol y por id
/// (RF-002d). Mientras ese cálculo vivió solo dentro de <c>Build</c>, todos los de fuera —el aviso previo,
/// el riesgo letal de RF-012c, «TU ONCE»— lo volvían a derivar de la guardada y decían otra cosa que la que
/// pasaba, que es la contradicción entre dos representaciones del mismo hecho que RT-014 prohíbe.
/// </summary>
/// <param name="Lineup">La colocación con la que se juega, casillas incluidas.</param>
/// <param name="FilledIds">
/// Los que entran <b>de relleno</b>: están en el once efectivo y no en la alineación guardada, así que el
/// jugador no los puso. Por id ascendente (RT-041). Es lo que permite que el aviso diga «entra X de oficio»
/// en vez de mentir con una inferioridad que no va a ocurrir, y lo que obliga a que el riesgo de muerte de
/// RF-012c les alcance: no se puede reducir con la alineación (condición 3 de la ADR 0048) el riesgo de
/// alguien que no sabías que jugaba.
/// </param>
public sealed record EffectiveLineup(Lineup Lineup, IReadOnlyList<int> FilledIds)
{
    /// <summary>
    /// Inferioridad <b>real</b> (RF-002d): ni con el banquillo entero se llega a once. Distinto de que la
    /// alineación guardada tenga huecos, que es lo que el aviso confundía.
    /// </summary>
    public bool IsShorthanded => Lineup.Slots.Count < RunRules.MaxStarters;
}

/// <summary>
/// Construye la alineación con la que se entra en un nodo de partido. Es responsabilidad del paquete W
/// porque es lo que cierra el contrato con <c>Simulator.Run</c>, que exige entre 5 y 7 titulares y
/// <b>exactamente un portero</b>.
///
/// <para><b>Portero de emergencia.</b> Una plantilla puede quedarse sin portero disponible: el club
/// inicial trae uno solo (RF-005) y una lesión grave lo aparta hasta la clínica (RF-092). Como RF-002b
/// dice que la run solo termina por dos vías, quedarse sin portero <b>no puede</b> terminarla, así que
/// el jugador de campo disponible de menor id se coloca en la portería para ese partido. Es una
/// decisión de implementación del paquete W, no una regla nueva: el cambio vive solo en el
/// <see cref="PlayerDefinition"/> que recibe el simulador y no toca el <see cref="RunPlayer"/> de la
/// plantilla. Lo mismo, al revés, con el segundo portero: si la plantilla tiene dos y los dos son
/// titulares, el sobrante juega de defensa.</para>
///
/// <para><b>Colocación.</b> Casillas fijas por rol, en coordenadas relativas al equipo propio (columna
/// 0..7 desde la portería propia): portero en (0,3), defensas en (2,2) y (2,4), centrocampistas en
/// (3,3), (4,2) y (4,4), delantero en (6,3). Es el 2-3-1 por defecto del paquete U (con las siete filas
/// sucesoras de la ADR 0103: <see cref="Lineup.Default"/> explica el reparto de filas), con
/// el que se midió el balance de la fase 1. Con menos de 7 disponibles (RF-002d, inferioridad) sobran
/// casillas y quedan vacías; nunca se repite una, que es lo que <c>Simulator.Run</c> rechaza.</para>
/// </summary>
public static class RunLineup
{
    /// <summary>
    /// Prefijo del contador de run que marca a los jugadores que el jugador ha decidido alinear
    /// <b>a sabiendas</b> de que arrastran una lesión grave sin tratar (RF-092, RF-093 vía 1). Lo pone
    /// <c>RunEngine.Apply(SetLineup)</c> —la única puerta por la que pasa una decisión del jugador— y lo
    /// borra <c>MatchResolution</c> al terminar el partido: el riesgo se asume partido a partido, nunca
    /// se hereda. Es el mecanismo genérico de contadores del paquete W (W-11), así que no sube la versión
    /// del esquema del guardado.
    /// </summary>
    public const string RiskCounterPrefix = "fieldSevereInjured:";

    /// <summary>Casilla del portero (RF-041: casilla fija dentro del área).</summary>
    public static Cell GoalkeeperCell { get; } = new(0, 3);

    /// <summary>
    /// Deja anotado en el estado quién sale al campo con una lesión grave sin tratar, y borra la marca de
    /// quien ya no lo hace (RF-093 vía 1). La marca vale para <b>este</b> partido: sin ella,
    /// <see cref="CanStart"/> no alinea a un lesionado grave ni aunque su nombre siga en la alineación
    /// guardada, de modo que arriesgarse es siempre una decisión tomada, nunca una herencia.
    ///
    /// <para>Vive aquí, junto a <see cref="CanStart"/> —que es quien la lee— y no en <c>RunEngine</c>,
    /// porque la usan dos: confirmar una alineación (<c>Apply(SetLineup)</c>) y <b>prever</b> una
    /// (<see cref="Effective"/>). Si la previsión no marcara igual que la confirmación, enseñaría un once
    /// distinto del que saldría al pulsar el botón.</para>
    /// </summary>
    public static RunState MarkSevereInjuryRisks(RunState state, IReadOnlyList<LineupSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(slots);
        var next = state;
        foreach (var (name, value) in state.Counters)
        {
            if (value != 0 && name.StartsWith(RiskCounterPrefix, StringComparison.Ordinal))
            {
                next = next.WithCounter(name, 0);
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            var player = state.FindPlayer(slots[i].PlayerId);
            if (player is { PhysicalState: PhysicalState.SevereInjury })
            {
                next = next.WithCounter(
                    RiskCounterPrefix + player.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
            }
        }

        return next;
    }

    /// <summary>
    /// True si este jugador puede salir al campo: disponible (sano o con lesión leve, RF-090/091) o bien
    /// con lesión grave y marcado explícitamente para arriesgarse (RF-093). El muerto, nunca.
    /// </summary>
    public static bool CanStart(RunState state, RunPlayer player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        return player.IsAvailable
            || (player.PhysicalState == PhysicalState.SevereInjury
                && state.Counter(RiskCounterPrefix + player.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)) > 0);
    }

    // Mismas casillas que Model.Lineup.Default (sucesora de la ADR 0103): con Rows=7 vuelve a haber una
    // única fila central (3), así que el par defensa/mediocentro-banda vuelve a las filas 2 y 4,
    // equidistantes del centro, y portero, mediocentro y delantero -sin pareja- se quedan en la fila 3.
    private static readonly Cell[] DefenderCells = { new(2, 2), new(2, 4) };
    private static readonly Cell[] MidfielderCells = { new(3, 3), new(4, 2), new(4, 4) };
    private static readonly Cell[] ForwardCells = { new(6, 3) };
    private static readonly Cell[] OutfieldCells =
    {
        new(2, 2), new(2, 4), new(3, 3), new(4, 2), new(4, 4), new(6, 3),
    };

    /// <summary>
    /// Alineación por defecto de una plantilla: la que se usa al empezar la run y la que repara una
    /// alineación que ha quedado obsoleta por una baja.
    /// </summary>
    public static Lineup Default(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Build(state, null).Lineup;
    }

    /// <summary>
    /// El once efectivo de esta plantilla: <b>quién juega de verdad</b> y quién entra de relleno (ADR 0134).
    ///
    /// <para><b>Es literalmente la misma llamada que construye el partido</b>, con el catálogo a null para
    /// no convertir a <see cref="PlayerDefinition"/>: no es una reimplementación «de acuerdo» con
    /// <see cref="Build"/> que haya que mantener sincronizada, es <see cref="Build"/>. Esa era la causa de
    /// [BC-H] —el aviso previo y el riesgo letal de RF-012c derivaban el once por su cuenta de la
    /// alineación guardada— y por eso el arreglo no es «calcularlo igual en los dos sitios» sino que solo
    /// exista un sitio.</para>
    ///
    /// <para>Pide una run viva: por debajo de <c>RunRules.MinimumAvailablePlayers</c> lanza, igual que
    /// <see cref="Build"/>, porque RF-002b dice que ahí la run ya había terminado.</para>
    /// </summary>
    /// <param name="intended">
    /// La intención que se quiere resolver, o null para la alineación guardada. Sirve para <b>prever</b>:
    /// la pantalla de Equipo pregunta «si confirmo ESTA colocación, ¿quién juega y qué riesgo corre?»
    /// mientras el jugador arrastra fichas. Se completa igual que la guardada —si no, la previsión
    /// enseñaría seis y el partido jugaría siete, que es el mismo engaño que esta ADR cierra— y los
    /// <see cref="EffectiveLineup.FilledIds"/> se cuentan contra ella, no contra lo guardado.
    /// </param>
    public static EffectiveLineup Effective(RunState state, Lineup? intended = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (intended is not null)
        {
            // Prever es responder «¿qué pasaría SI confirmo esto?», así que hay que marcar lo mismo que
            // marcaría confirmarlo. Sin esto, un lesionado grave puesto a mano no pasaba CanStart —su marca
            // de riesgo la pone RunEngine.Apply(SetLineup), que aún no ha corrido— y la previsión lo
            // sustituía por un suplente: el aviso de «puede morir» (RF-093 vía 1) desaparecía justo antes
            // de confirmar, que es el único momento en que sirve de algo (RF-012d).
            state = MarkSevereInjuryRisks(state.WithLineup(intended), intended.Slots);
        }

        var slots = Build(state, null).Lineup;

        var saved = state.Lineup.Slots;
        var filled = new List<int>();
        for (int i = 0; i < slots.Slots.Count; i++)
        {
            int id = slots.Slots[i].PlayerId;
            bool wasChosen = false;
            for (int j = 0; j < saved.Count; j++)
            {
                wasChosen |= saved[j].PlayerId == id;
            }

            if (!wasChosen)
            {
                filled.Add(id);
            }
        }

        filled.Sort();
        return new EffectiveLineup(slots, filled);
    }

    /// <summary>
    /// Los atributos que este titular tendría <b>si arrastrase <paramref name="extra"/> lesiones leves
    /// más</b> — el precio de «seguir jugando» de la ADR 0134 E, que viaja resuelto dentro de
    /// <see cref="PlayOn"/> porque el motor no puede calcularlo (ver el doc de ese record).
    ///
    /// <para><b>Se obtiene volviendo a construir el partido</b> con una sola entrada cambiada, y no con una
    /// fórmula aparte, porque la fórmula aparte estaría mal: al atributo de un titular no solo le afecta la
    /// penalización de RF-091, también el equipo que lleva (<c>Equipped</c>) y la reposición si juega fuera
    /// de su puesto o de portero de emergencia (<c>Repositioned</c>). Reproducir ese orden a mano sería
    /// exactamente la segunda implementación «de acuerdo» con la primera que esta ADR existe para eliminar.
    /// Cuesta un <see cref="Build"/> de más en una pulsación de interfaz, que no está en ningún camino
    /// caliente.</para>
    /// </summary>
    public static Attributes AttributesWithExtraMinorInjuries(RunState state, Catalog catalog, int playerId, int extra)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        if (extra < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(extra), extra, "seguir jugando cuesta al menos una lesión leve");
        }

        var roster = new List<RunPlayer>(state.Roster);
        bool found = false;
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i].Id == playerId)
            {
                roster[i] = roster[i] with { MinorInjuries = roster[i].MinorInjuries + extra };
                found = true;
            }
        }

        if (!found)
        {
            throw new ArgumentException($"el jugador {playerId} no está en la plantilla", nameof(playerId));
        }

        var hurt = Build(state.WithRoster(roster), catalog);
        for (int i = 0; i < hurt.Starters.Count; i++)
        {
            if (hurt.Starters[i].Id == playerId)
            {
                return hurt.Starters[i].Attributes;
            }
        }

        // También en el banquillo: un suplente que ya entró por una sustitución está EN EL CAMPO y puede
        // lesionarse y quedarse (SubstitutionPoints.WasOnPitch lo cuenta, el motor lo acepta y la bandeja se
        // lo ofrece), pero para RunLineup.Build sigue siendo banquillo porque no estaba en el once inicial.
        // Buscarlo solo entre titulares hacía que pulsar «que siga jugando» sobre él lanzara — con ironman,
        // una run. Es el mismo patrón que ValidatePlayOns tuvo que arreglar una capa más abajo.
        for (int i = 0; i < hurt.Bench.Count; i++)
        {
            if (hurt.Bench[i].Id == playerId)
            {
                return hurt.Bench[i].Attributes;
            }
        }

        throw new ArgumentException(
            $"el jugador {playerId} no está ni en el once ni en el banquillo de este partido", nameof(playerId));
    }

    /// <summary>
    /// Coloca en la cuadrícula un conjunto de titulares <b>ya elegido</b>, con las mismas casillas y el
    /// mismo criterio que <see cref="Build"/>: portero en <see cref="GoalkeeperCell"/> y el resto por su
    /// posición sobre el 2-3-1 por defecto. Es lo que necesita cualquier política automática que decida
    /// <i>quién</i> juega —<c>/Balance --full-runs</c>, y mañana la pantalla de alineación— sin tener que
    /// reimplementar la colocación: la decisión es el once, no las casillas.
    /// </summary>
    public static Lineup Compose(IReadOnlyList<RunPlayer> starters)
    {
        ArgumentNullException.ThrowIfNull(starters);
        if (starters.Count is < RunRules.MinimumAvailablePlayers or > RunRules.MaxStarters)
        {
            throw new ArgumentException(
                $"un once tiene entre {RunRules.MinimumAvailablePlayers} y {RunRules.MaxStarters} titulares "
                    + $"y se han pasado {starters.Count} (RF-002d, RF-059)",
                nameof(starters));
        }

        var goalkeeper = PickGoalkeeper(starters);
        var slots = new List<LineupSlot>(starters.Count) { new(goalkeeper.Id, GoalkeeperCell) };
        var taken = new List<Cell>(OutfieldCells.Length);
        for (int i = 0; i < starters.Count; i++)
        {
            if (starters[i].Id == goalkeeper.Id)
            {
                continue;
            }

            var cell = CellFor(starters[i].Position, taken);
            taken.Add(cell);
            slots.Add(new LineupSlot(starters[i].Id, cell));
        }

        return new Lineup(slots);
    }

    /// <summary>
    /// Construye el equipo para un partido. Si <paramref name="catalog"/> es null no se convierte a
    /// <see cref="PlayerDefinition"/> (se usa solo para calcular la colocación).
    /// </summary>
    public static MatchLineup Build(RunState state, Catalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(state);

        var available = state.AvailablePlayers;
        if (available.Count < RunRules.MinimumAvailablePlayers)
        {
            throw new InvalidOperationException(
                $"la plantilla tiene {available.Count} jugadores disponibles y el mínimo son "
                    + $"{RunRules.MinimumAvailablePlayers} (RF-002b): la run debería haber terminado ya");
        }

        var starters = SelectStarters(state, available);
        var goalkeeper = PickGoalkeeper(starters);

        var slots = new List<LineupSlot>(starters.Count);
        var definitions = new List<PlayerDefinition>(starters.Count);
        int emergencyGoalkeeperId = -1;

        slots.Add(new LineupSlot(goalkeeper.Id, GoalkeeperCell));
        if (goalkeeper.Position != Position.Goalkeeper)
        {
            emergencyGoalkeeperId = goalkeeper.Id;
        }

        if (catalog is not null)
        {
            definitions.Add(Equipped(state, goalkeeper, Repositioned(goalkeeper.ToDefinition(catalog), Position.Goalkeeper, goalkeeper.Position, catalog)));
        }

        var taken = new List<Cell>(OutfieldCells.Length);
        var outfield = new List<RunPlayer>(starters.Count);
        for (int i = 0; i < starters.Count; i++)
        {
            if (starters[i].Id != goalkeeper.Id)
            {
                outfield.Add(starters[i]);
            }
        }

        var cells = PlaceOutfield(state, outfield, taken);
        for (int p = 0; p < outfield.Count; p++)
        {
            var player = outfield[p];
            var cell = cells[p];
            slots.Add(new LineupSlot(player.Id, cell));
            if (catalog is null)
            {
                continue;
            }

            // El portero sobrante juega de defensa: el simulador solo admite un portero alineado.
            var position = player.Position == Position.Goalkeeper ? Position.Defender : player.Position;
            definitions.Add(Equipped(state, player, Repositioned(player.ToDefinition(catalog), position, player.Position, catalog)));
        }

        var bench = new List<PlayerDefinition>();
        if (catalog is not null)
        {
            for (int i = 0; i < available.Count; i++)
            {
                if (!Contains(starters, available[i].Id))
                {
                    bench.Add(Equipped(state, available[i], available[i].ToDefinition(catalog)));
                }
            }
        }

        return new MatchLineup(definitions, bench, new Lineup(slots), emergencyGoalkeeperId);
    }

    /// <summary>
    /// Titulares: primero los de la alineación guardada que siguen en pie, en su orden, y luego se
    /// completa con los disponibles de menor id hasta 7 (o hasta agotarlos, RF-002d).
    ///
    /// <para><b>Lesión grave (RF-092, RF-093).</b> Un jugador con lesión grave sin tratar sale al campo
    /// <b>solo si el jugador lo ha decidido para este partido</b>: está en la alineación guardada y lleva
    /// la marca de <see cref="RiskCounterPrefix"/>. Nunca entra por el relleno automático, que sigue
    /// mirando únicamente a los disponibles, ni por una alineación vieja que se quedó con su nombre
    /// dentro, así que la decisión de arriesgarse a la muerte es siempre explícita y nunca se toma sola.
    /// Al muerto no se le alinea jamás.</para>
    /// </summary>
    private static List<RunPlayer> SelectStarters(RunState state, IReadOnlyList<RunPlayer> available)
    {
        var starters = new List<RunPlayer>(RunRules.MaxStarters);
        var slots = state.Lineup.Slots;
        for (int i = 0; i < slots.Count && starters.Count < RunRules.MaxStarters; i++)
        {
            var player = state.FindPlayer(slots[i].PlayerId);
            if (player is not null && CanStart(state, player) && !Contains(starters, player.Id))
            {
                starters.Add(player);
            }
        }

        // Relleno por rol, para que una plantilla sin alineación guardada salga con forma de equipo y
        // no con siete centrocampistas: portero, defensas, centrocampistas, delantero, y el resto.
        AddByPosition(starters, available, Position.Goalkeeper, 1);
        AddByPosition(starters, available, Position.Defender, DefenderCells.Length);
        AddByPosition(starters, available, Position.Midfielder, MidfielderCells.Length);
        AddByPosition(starters, available, Position.Forward, ForwardCells.Length);

        for (int i = 0; i < available.Count && starters.Count < RunRules.MaxStarters; i++)
        {
            if (!Contains(starters, available[i].Id))
            {
                starters.Add(available[i]);
            }
        }

        return starters;
    }

    private static void AddByPosition(List<RunPlayer> starters, IReadOnlyList<RunPlayer> available, Position position, int max)
    {
        int already = 0;
        for (int i = 0; i < starters.Count; i++)
        {
            if (starters[i].Position == position)
            {
                already++;
            }
        }

        for (int i = 0; i < available.Count && starters.Count < RunRules.MaxStarters && already < max; i++)
        {
            if (available[i].Position == position && !Contains(starters, available[i].Id))
            {
                starters.Add(available[i]);
                already++;
            }
        }
    }

    /// <summary>Portero titular: el de verdad si hay alguno entre los titulares, y si no, el de menor id.</summary>
    private static RunPlayer PickGoalkeeper(IReadOnlyList<RunPlayer> starters)
    {
        RunPlayer? goalkeeper = null;
        for (int i = 0; i < starters.Count; i++)
        {
            if (starters[i].Position != Position.Goalkeeper)
            {
                continue;
            }

            if (goalkeeper is null || starters[i].Id < goalkeeper.Id)
            {
                goalkeeper = starters[i];
            }
        }

        if (goalkeeper is not null)
        {
            return goalkeeper;
        }

        var emergency = starters[0];
        for (int i = 1; i < starters.Count; i++)
        {
            if (starters[i].Id < emergency.Id)
            {
                emergency = starters[i];
            }
        }

        return emergency;
    }

    /// <summary>
    /// Casilla que le toca a esa posición dadas las ya ocupadas, con el mismo criterio que
    /// <see cref="Compose"/>: primero las preferidas de su posición en orden y, si no queda ninguna, la
    /// primera libre del 2-3-1. Es público porque una política que quiera <b>colocar</b> con criterio
    /// —y desde la ADR 0048 el riesgo de muerte depende de la casilla— necesita saber dónde va a caer
    /// cada jugador antes de decidir el once, y reimplementarlo sería garantizar que los dos se separen.
    /// </summary>
    public static Cell CellFor(Position position, IReadOnlyList<Cell> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);
        return NextCell(position, taken);
    }

    /// <summary>
    /// La misma casilla, sin excepción cuando ya no queda ninguna libre. Pasa de verdad: sin portero en
    /// la plantilla, <see cref="Compose"/> recoloca al titular de menor id en la portería y las seis
    /// casillas de campo pueden agotarse con el once todavía incompleto.
    /// </summary>
    public static bool TryCellFor(Position position, IReadOnlyList<Cell> taken, out Cell cell)
    {
        ArgumentNullException.ThrowIfNull(taken);
        for (int i = 0; i < OutfieldCells.Length; i++)
        {
            if (!Taken(taken, OutfieldCells[i]))
            {
                cell = NextCell(position, taken);
                return true;
            }
        }

        cell = default;
        return false;
    }

    /// <summary>
    /// La casilla de cada titular de campo, <b>respetando la que el jugador le dio</b> en la alineación
    /// guardada y repartiendo el 2-3-1 por rol solo entre los que no tienen ninguna (ADR 0134).
    ///
    /// <para><b>Hasta aquí la colocación del jugador se tiraba.</b> Este método reasignaba las siete
    /// casillas por rol siempre, así que arrastrar un defensa al puesto de delantero en la pantalla de
    /// Equipo se guardaba en el estado y después <c>Build</c> lo devolvía a su casilla de defensa. El
    /// síntoma que lo destapó: el indicador de riesgo de RF-012c <b>sí</b> leía las casillas guardadas, de
    /// modo que mover fichas movía el número y no movía el partido — el jugador «reducía el riesgo con la
    /// alineación» (condición 3 de la ADR 0048) contra una colocación que no se iba a jugar. Lo dejó a la
    /// vista el test <c>LethalRiskTests.MovingThePlayersChangesTheNumber</c> en cuanto los avisos pasaron a
    /// mirar el once efectivo. Detalle en el pendiente BG-B.</para>
    ///
    /// <para>Dos pasadas, no una, y es lo que hace que el resultado no dependa del orden: primero se
    /// reclaman todas las casillas elegidas, y solo después se rellenan las libres por rol. En una sola
    /// pasada, el relleno de un jugador temprano podría ocupar la casilla que otro había elegido.</para>
    ///
    /// <para>Sin alineación guardada —el arranque de la run, <see cref="Default"/>, y todo lo que compone
    /// con <see cref="Compose"/>, incluidas las políticas de <c>/Balance</c>— no hay ninguna casilla que
    /// reclamar y sale exactamente el mismo 2-3-1 de antes.</para>
    /// </summary>
    private static List<Cell> PlaceOutfield(RunState state, List<RunPlayer> outfield, List<Cell> taken)
    {
        var chosen = new Cell?[outfield.Count];
        var saved = state.Lineup.Slots;
        for (int i = 0; i < outfield.Count; i++)
        {
            for (int j = 0; j < saved.Count; j++)
            {
                if (saved[j].PlayerId != outfield[i].Id)
                {
                    continue;
                }

                // La portería es casilla fija (RF-041) y ya la ocupa el portero, de emergencia o no: una
                // alineación guardada que mande ahí a un jugador de campo no puede cumplirse.
                var cell = saved[j].HomeCell;
                if (!cell.Equals(GoalkeeperCell) && !Taken(taken, cell))
                {
                    chosen[i] = cell;
                    taken.Add(cell);
                }

                break;
            }
        }

        var cells = new List<Cell>(outfield.Count);
        for (int i = 0; i < outfield.Count; i++)
        {
            var cell = chosen[i] ?? CellFor(outfield[i].Position, taken);
            if (chosen[i] is null)
            {
                taken.Add(cell);
            }

            cells.Add(cell);
        }

        return cells;
    }

    private static Cell CellFor(Position position, List<Cell> taken) => NextCell(position, taken);

    private static Cell NextCell(Position position, IReadOnlyList<Cell> taken)
    {
        var preferred = position switch
        {
            Position.Defender => DefenderCells,
            Position.Midfielder => MidfielderCells,
            Position.Forward => ForwardCells,
            _ => DefenderCells,
        };

        for (int i = 0; i < preferred.Length; i++)
        {
            if (!Taken(taken, preferred[i]))
            {
                return preferred[i];
            }
        }

        for (int i = 0; i < OutfieldCells.Length; i++)
        {
            if (!Taken(taken, OutfieldCells[i]))
            {
                return OutfieldCells[i];
            }
        }

        throw new InvalidOperationException("no quedan casillas libres para colocar a un titular");
    }

    private static bool Taken(IReadOnlyList<Cell> cells, Cell cell)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == cell)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Pone el objeto equipado del jugador en la definición que recibe el simulador (RF-075..078). No
    /// ocupa slot de perk (RF-076) y se resuelve con el catálogo de la instantánea de la run
    /// (<c>RunState.Equipment</c>): sin instantánea, sin objeto.
    /// </summary>
    private static PlayerDefinition Equipped(RunState state, RunPlayer player, PlayerDefinition definition) =>
        state.Equipment.MatchItemOf(player.Item) is { } item ? definition with { Item = item } : definition;

    /// <summary>
    /// Cambia la posición de una definición solo para este partido, manteniendo <c>Tags</c> coherente
    /// (la etiqueta de posición forma parte de las etiquetas del jugador, ADR 0024) y <b>quitando los
    /// perks que su posición nueva no admite</b>.
    ///
    /// <para>Lo segundo no es una regla nueva, es la consecuencia de la primera: un portero de más juega
    /// de defensa (el simulador solo admite un portero alineado) y un jugador de campo puede acabar
    /// de portero de emergencia, y un perk con <c>positionOnly</c> en la posición que deja de tener no
    /// puede activarse de ninguna manera. Sin quitarlo, <c>Simulator.Run</c> rechaza el equipo entero y
    /// la run se cae con "asigna al jugador N (Defender) el perk 'clean_sheet_legacy', que solo admite
    /// Goalkeeper" — medido en el bucle de run en cuanto un portero suplente cobra un perk de portero.
    /// El perk <b>no</b> se pierde: sigue en el estado y vuelve en cuanto el jugador juegue en su
    /// posición.</para>
    /// </summary>
    private static PlayerDefinition Repositioned(
        PlayerDefinition definition, Position position, Position original, Catalog catalog)
    {
        if (position == original)
        {
            return definition;
        }

        var tags = new List<string>(definition.Tags.Count);
        for (int i = 0; i < definition.Tags.Count; i++)
        {
            tags.Add(string.Equals(definition.Tags[i], original.ToString(), StringComparison.Ordinal)
                ? position.ToString()
                : definition.Tags[i]);
        }

        var perks = definition.Perks;
        for (int i = 0; i < definition.Perks.Count; i++)
        {
            var perk = catalog.Perks.Find(definition.Perks[i]);
            if (perk?.PositionOnly is { } required && required != position)
            {
                var kept = new List<string>(definition.Perks.Count - 1);
                for (int k = 0; k < definition.Perks.Count; k++)
                {
                    var candidate = catalog.Perks.Find(definition.Perks[k]);
                    if (candidate?.PositionOnly is not { } only || only == position)
                    {
                        kept.Add(definition.Perks[k]);
                    }
                }

                perks = kept;
                break;
            }
        }

        return definition with { Position = position, Tags = tags, Perks = perks };
    }

    private static bool Contains(IReadOnlyList<RunPlayer> players, int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
            {
                return true;
            }
        }

        return false;
    }
}
