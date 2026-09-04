using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Estado mutable de un jugador durante un partido (§3). Se construye una vez al arrancar el motor y
/// se reutiliza tick a tick: no se asignan objetos por decisión ni por acción (RT-051).
/// Los multiplicadores y bonos de rasgo se agregan aquí una sola vez (RT-094).
/// </summary>
internal sealed class MatchPlayer
{
    private const int ActionCount = (int)PlayerAction.Retreat + 1;

    /// <summary>Número de atributos de <see cref="AttributeKind"/>.</summary>
    private const int AttributeCount = (int)AttributeKind.Leash + 1;

    private readonly int[] _actionMultipliers = new int[ActionCount];
    private readonly int[] _baseAttributes = new int[AttributeCount];
    private readonly int[] _attributeDeltas = new int[AttributeCount];
    private readonly int[] _effectiveAttributes = new int[AttributeCount];
    private readonly int _traitMask;
    private readonly int _leashMinCells;
    private readonly int _leashCellsPer99;
    private int _leashCellDelta;
    private float _leashCells;

    public MatchPlayer(PlayerDefinition definition, int team, Cell homeCell, Catalog catalog)
    {
        Definition = definition;
        Team = team;
        HomeCell = homeCell;
        HomeCenter = Pitch.CellCenter(homeCell);
        EffectiveHome = HomeCenter;
        Position = HomeCenter;

        var attributes = definition.Attributes;
        for (int i = 0; i < AttributeCount; i++)
        {
            _baseAttributes[i] = attributes.Get((AttributeKind)i);
        }

        for (int i = 0; i < ActionCount; i++)
        {
            _actionMultipliers[i] = 100;
        }

        var traits = definition.Traits;
        for (int i = 0; i < traits.Count; i++)
        {
            var trait = catalog.Trait(traits[i]);
            var multipliers = trait.ActionMultipliers;
            for (int j = 0; j < multipliers.Count; j++)
            {
                int slot = (int)multipliers[j].Action;
                _actionMultipliers[slot] = _actionMultipliers[slot] * multipliers[j].MultiplierPercent / 100;
            }

            _traitMask |= 1 << (int)traits[i];
            HardTackleBonus += trait.HardTackleBonus;
            SpeedBonusPercent += trait.SpeedBonusPercent;
            ShotQualityBonus += trait.ShotQualityBonus;
            ShootRangeBonusCells += trait.ShootRangeBonusCells;
            PassQualityBonus += trait.PassQualityBonus;
            FoulChanceBonus += trait.FoulChanceBonus;
            InjuryChanceBonus += trait.InjuryChanceBonus;
            FatigueResistancePercent += trait.FatigueResistancePercent;
            InjuryResistanceBonus += trait.InjuryResistanceBonus;
            AdjacentTeammateBonusPercent += trait.AdjacentTeammateBonusPercent;
            SaveBonusClose += trait.SaveBonusClose;
            SaveBonusFar += trait.SaveBonusFar;
            LeashBonus += trait.LeashBonus;
        }

        var leash = catalog.Tuning.Leash;
        _leashMinCells = leash.MinCells;
        _leashCellsPer99 = leash.CellsPer99;
        Recalculate();
    }

    /// <summary>Definición estática de origen (atributos, rasgos, nombre).</summary>
    public PlayerDefinition Definition { get; }

    /// <summary>Identificador global del jugador; el motor itera siempre por id ascendente (RT-041).</summary>
    public int Id => Definition.Id;

    /// <summary>
    /// Índice del jugador en el array del motor (ordenado por id ascendente). Lo fija MatchEngine al
    /// construirse; es la clave de las tablas planas de modificadores y contadores del motor de efectos.
    /// </summary>
    public int Index { get; set; }

    /// <summary>Nombre visible, usado solo en el log (RF-121).</summary>
    public string Name => Definition.Name;

    /// <summary>Posición nominal del jugador (rol); la posición espacial es <see cref="Position"/>.</summary>
    public Position Role => Definition.Position;

    /// <summary>True si no es portero.</summary>
    public bool IsOutfield => Definition.Position != Model.Position.Goalkeeper;

    /// <summary>Equipo: 0 local, 1 visitante.</summary>
    public int Team { get; }

    /// <summary>Casilla-hogar absoluta (ya reflejada para el equipo 1).</summary>
    public Cell HomeCell { get; }

    /// <summary>Centro continuo de la casilla-hogar.</summary>
    public Vec2 HomeCenter { get; }

    /// <summary>Casilla-hogar desplazada por el bloque táctico (§3.4); se recalcula cada tick.</summary>
    public Vec2 EffectiveHome { get; set; }

    /// <summary>
    /// Radio de correa en casillas (§2.6), incluido el bono de rasgo, el atributo efectivo de correa y
    /// los modificadores de <c>modifyLeash</c> (fase 1, §2). Mínimo 1 casilla. Cacheado: solo se
    /// recalcula cuando un modificador entra o expira.
    /// </summary>
    public float LeashCells => _leashCells;

    /// <summary>Fuerza efectiva: base de nivel más modificadores activos, acotada a 1..99 (§3).</summary>
    public int Strength => _effectiveAttributes[(int)AttributeKind.Strength];

    /// <summary>Velocidad efectiva (§3).</summary>
    public int Speed => _effectiveAttributes[(int)AttributeKind.Speed];

    /// <summary>Técnica efectiva (§3).</summary>
    public int Technique => _effectiveAttributes[(int)AttributeKind.Technique];

    /// <summary>Resistencia efectiva (§3).</summary>
    public int Stamina => _effectiveAttributes[(int)AttributeKind.Stamina];

    public int HardTackleBonus { get; }

    public int SpeedBonusPercent { get; }

    public int ShotQualityBonus { get; }

    public int ShootRangeBonusCells { get; }

    public int PassQualityBonus { get; }

    public int FoulChanceBonus { get; }

    public int InjuryChanceBonus { get; }

    public int FatigueResistancePercent { get; }

    public int InjuryResistanceBonus { get; }

    /// <summary>Bono porcentual que este jugador da a los compañeros con casilla-hogar contigua (Leader, RT-094).</summary>
    public int AdjacentTeammateBonusPercent { get; }

    /// <summary>
    /// Suma de los bonos de los Leader del equipo con casilla-hogar contigua a la suya (§3.5). Se calcula
    /// una vez al construir el motor: las casillas-hogar no cambian durante el partido.
    /// </summary>
    public int LeaderBonusPercent { get; set; }

    public int SaveBonusClose { get; }

    public int SaveBonusFar { get; }

    public int LeashBonus { get; }

    /// <summary>Posición continua actual en casillas.</summary>
    public Vec2 Position { get; set; }

    /// <summary>Desplazamiento del último tick; se usa para anticipar el pase (§3.7).</summary>
    public Vec2 Velocity { get; set; }

    /// <summary>Estado de la máquina de estados del jugador (§3.6).</summary>
    public PlayerState State { get; set; } = PlayerState.Positioning;

    /// <summary>Ticks que quedan en el estado actual; 0 si el estado no expira.</summary>
    public int StateTicksLeft { get; set; }

    /// <summary>Última acción elegida por la utilidad (§3.5).</summary>
    public PlayerAction CurrentAction { get; set; } = PlayerAction.Retreat;

    /// <summary>Punto objetivo de movimiento, ya acotado a la correa (§3.3).</summary>
    public Vec2 TargetPoint { get; set; }

    /// <summary>Receptor elegido al decidir Pass; se lee al expirar Passing.</summary>
    public MatchPlayer? PassReceiver { get; set; }

    /// <summary>Rival objetivo al decidir Tackle; se lee al expirar Tackling.</summary>
    public MatchPlayer? TackleTarget { get; set; }

    /// <summary>Ticks que faltan para poder volver a disputar un regate (§3.7).</summary>
    public int DribbleDuelCooldown { get; set; }

    /// <summary>Ticks que faltan para poder volver a decidir una entrada (§3.5); mientras sea &gt; 0, Tackle se descarta.</summary>
    public int TackleCooldown { get; set; }

    /// <summary>Tarjetas amarillas acumuladas (la segunda es roja si lo dice tuning).</summary>
    public int YellowCards { get; set; }

    /// <summary>True mientras el jugador está en el campo (no lesionado ni expulsado).</summary>
    public bool OnPitch { get; set; } = true;

    /// <summary>Paradas consecutivas sin encajar; alimenta el decaimiento de parada (§3.7).</summary>
    public int ConsecutiveSaves { get; set; }

    public int Goals { get; set; }

    public int Assists { get; set; }

    public int Shots { get; set; }

    public int PassesAttempted { get; set; }

    public int PassesCompleted { get; set; }

    public int Tackles { get; set; }

    public int TacklesWon { get; set; }

    public int Fouls { get; set; }

    public int Cards { get; set; }

    public bool Injured { get; set; }

    public int TicksOnPitch { get; set; }

    /// <summary>
    /// Atributo efectivo (§3, RF-065): base del jugador más la suma de los modificadores de perk activos,
    /// acotado a 1..99. Es el valor que lee todo el motor; nadie consulta ya el atributo base.
    /// </summary>
    public int Effective(AttributeKind kind) => _effectiveAttributes[(int)kind];

    /// <summary>Atributo base (sin modificadores), tal cual viene de <see cref="Definition"/>.</summary>
    public int BaseAttribute(AttributeKind kind) => _baseAttributes[(int)kind];

    /// <summary>
    /// Suma delta al modificador acumulado del atributo y recalcula el efectivo. Recalcular al cambiar
    /// (y no en cada lectura) es lo que mantiene el coste de los atributos efectivos en cero cuando no
    /// hay perks (§3).
    /// </summary>
    internal void AddAttributeDelta(AttributeKind kind, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        _attributeDeltas[(int)kind] += delta;
        Recalculate();
    }

    /// <summary>Suma delta al radio de correa en casillas (efecto modifyLeash, §2) y recalcula.</summary>
    internal void AddLeashCellDelta(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        _leashCellDelta += delta;
        Recalculate();
    }

    /// <summary>Recalcula los cinco atributos efectivos y el radio de correa a partir de base + deltas.</summary>
    private void Recalculate()
    {
        for (int i = 0; i < AttributeCount; i++)
        {
            _effectiveAttributes[i] = Math.Clamp(_baseAttributes[i] + _attributeDeltas[i], 1, 99);
        }

        int cells = _leashMinCells
            + (_effectiveAttributes[(int)AttributeKind.Leash] * _leashCellsPer99 / 99)
            + LeashBonus
            + _leashCellDelta;
        _leashCells = cells < 1 ? 1 : cells;
    }

    /// <summary>True si el jugador tiene el rasgo indicado (consulta sin asignar, RT-051).</summary>
    public bool HasTrait(Trait trait) => (_traitMask & (1 << (int)trait)) != 0;

    /// <summary>Multiplicador de rasgo acumulado para la acción (porcentaje, 100 = neutro; RT-094).</summary>
    public int ActionMultiplier(PlayerAction action) => _actionMultipliers[(int)action];

    /// <summary>Entra en un estado con duración; ticks &lt;= 0 deja el estado sin temporizador.</summary>
    public void EnterState(PlayerState state, int ticks)
    {
        State = state;
        StateTicksLeft = ticks > 0 ? ticks : 0;
    }

    /// <summary>Saca al jugador del campo (lesión o expulsión): posición (-1,-1), no decide ni cuenta.</summary>
    public void LeavePitch(PlayerState state)
    {
        State = state;
        StateTicksLeft = 0;
        OnPitch = false;
        Position = new Vec2(-1f, -1f);
        Velocity = new Vec2(0f, 0f);
    }

    /// <summary>Estadísticas finales del jugador para el informe.</summary>
    public PlayerMatchStats ToStats() => new(
        Id, Team, Goals, Assists, Shots, PassesAttempted, PassesCompleted,
        Tackles, TacklesWon, Fouls, Cards, Injured, TicksOnPitch);
}
