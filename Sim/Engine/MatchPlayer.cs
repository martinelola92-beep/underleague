using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Engine;

/// <summary>
/// Estado mutable de un jugador durante un partido (§3). Se construye una vez al arrancar el motor y
/// se reutiliza tick a tick: no se asignan objetos por decisión ni por acción (RT-051).
/// Los multiplicadores y bonos de rasgo se agregan aquí una sola vez (RT-094).
/// </summary>
internal sealed class MatchPlayer
{
    private const int ActionCount = (int)PlayerAction.Block + 1;

    /// <summary>Número de atributos de <see cref="AttributeKind"/>.</summary>
    private const int AttributeCount = (int)AttributeKind.Leash + 1;

    /// <summary>
    /// Id del perk de habilidad racial que concede inmunidad al empuje (Raíces, enanos; ADR 0020 §2.1.5).
    /// El motor no pregunta por la raza: pregunta por la habilidad, que es un dato de
    /// <c>data/races/*.json</c>. Lo único que hace con ella es sembrar <see cref="Immovable"/>, que a
    /// partir de ahí es una propiedad del jugador que cualquier efecto de perk puede encender o apagar.
    /// </summary>
    private const string ImmovableAbility = "roots";

    private readonly int[] _actionMultipliers = new int[ActionCount];
    private readonly int[] _baseAttributes = new int[AttributeCount];
    private readonly int[] _attributeDeltas = new int[AttributeCount];
    private readonly int[] _effectiveAttributes = new int[AttributeCount];
    private readonly int _traitMask;
    private readonly int _shapeForward;
    private readonly int _shapeBack;
    private readonly int _shapeSides;
    private readonly int _leashScaleAt1;
    private readonly int _leashScaleAt99;
    private readonly int _outerLimitMultiplier;
    private readonly int _massStrengthWeight;
    private readonly int _massRadiusWeight;
    private int _leashCellDelta;

    /// <summary>Casillas extra por dimensión de la zona de acción (C8, <c>modifyZoneShape</c>), en el orden de <see cref="ZoneDimension"/>.</summary>
    private readonly int[] _zoneShapeDeltaCells = new int[3];
    private ActionZone _zone;
    private ActionZone _outerZone;
    private int _mass;

    public MatchPlayer(PlayerDefinition definition, int team, Cell homeCell, Catalog catalog)
    {
        Definition = definition;
        Team = team;
        HomeCell = homeCell;
        HomeCenter = Pitch.CellCenter(homeCell);
        EffectiveHome = HomeCenter;
        Position = HomeCenter;

        // Cuerpo y disciplina salen de la raza (ADR 0020, ADR 0028). bodyRadius viene en centésimas de
        // casilla; se guarda en casillas porque solo se usa en geometría, junto a las posiciones.
        var race = catalog.Race(definition.Race);
        BodyRadiusCentiCells = race.BodyRadius;
        BodyRadius = race.BodyRadius / 100f;
        Discipline = race.Discipline;
        Immovable = string.Equals(race.Ability, ImmovableAbility, StringComparison.Ordinal);

        var zone = catalog.Tuning.ActionZone;
        var shape = definition.Position switch
        {
            Model.Position.Goalkeeper => zone.Shape.Goalkeeper,
            Model.Position.Defender => zone.Shape.Defender,
            Model.Position.Midfielder => zone.Shape.Midfielder,
            Model.Position.Forward => zone.Shape.Forward,
            _ => throw new ArgumentOutOfRangeException(nameof(definition)),
        };

        _shapeForward = shape.Forward;
        _shapeBack = shape.Back;
        _shapeSides = shape.Sides;
        _leashScaleAt1 = zone.ScaleFromLeashPercent.At1;
        _leashScaleAt99 = zone.ScaleFromLeashPercent.At99;
        _outerLimitMultiplier = zone.OuterLimitMultiplier;

        var bodies = catalog.Tuning.Bodies;
        _massStrengthWeight = bodies.MassStrengthWeight;
        _massRadiusWeight = bodies.MassRadiusWeight;

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
    /// Zona de acción con forma del jugador (ADR 0028, §2.2): la región <b>blanda</b>, la que penaliza
    /// salir. Cacheada: solo se recalcula cuando cambia el atributo de correa o entra o expira un
    /// modificador (la casilla-hogar efectiva se pasa aparte, porque cambia cada tick con el bloque).
    /// </summary>
    public ActionZone Zone => _zone;

    /// <summary>
    /// Límite duro exterior (§2.2): la misma zona multiplicada por <c>outerLimitMultiplier</c>. Fuera de
    /// aquí la acción se descarta y el movimiento se acota; es la red de seguridad que impide que un
    /// defensa se instale en el área rival el resto del partido.
    /// </summary>
    public ActionZone OuterZone => _outerZone;

    /// <summary>
    /// Extensión lateral de la zona en casillas. Es la única dirección finita en las cuatro posiciones
    /// de la tabla de formas, así que sirve de escalar representativo de "cuánta correa tiene este
    /// jugador" para el efecto <c>modifyLeash</c> y para los tests que lo comprueban. Conserva el nombre
    /// que tenía el radio circular de la fase 0 porque es la magnitud que ese efecto sigue moviendo.
    /// </summary>
    public float LeashCells => ActionZone.Cells(_zone.SidesMilli);

    /// <summary>Radio del cuerpo en casillas (ADR 0020, RT-016); dato de raza, constante durante el partido.</summary>
    public float BodyRadius { get; }

    /// <summary>El mismo radio en centésimas de casilla, tal cual viene del dato, para la masa entera.</summary>
    public int BodyRadiusCentiCells { get; }

    /// <summary>
    /// Masa entera del jugador (§2.1.2): <c>fuerza × massStrengthWeight/100 + bodyRadius ×
    /// massRadiusWeight/100</c>. El reparto del empuje es inverso a la masa, así que un orco abre hueco y
    /// un elfo sale despedido. Se recalcula con la fuerza efectiva, no con la base: un perk de fuerza
    /// también hace al jugador más difícil de mover.
    /// </summary>
    public int Mass => _mass;

    /// <summary>
    /// Disciplina 0-100 (ADR 0028): cuánto le penaliza la utilidad salirse de su zona. Sale de la raza,
    /// pero es una propiedad del jugador, no una consulta a la raza: los rasgos y los perks la moverán.
    /// </summary>
    public int Discipline { get; set; }

    /// <summary>
    /// Inmunidad al empuje (§2.1.5): el jugador recibe desplazamiento 0 en la separación de cuerpos y en
    /// el empuje de una entrada. Se siembra desde la habilidad racial <c>roots</c> (Raíces, enanos), pero
    /// el motor solo lee esta propiedad: un efecto de perk puede encenderla o apagarla sin que haya un
    /// <c>if</c> por raza en medio de la separación.
    /// </summary>
    public bool Immovable { get; set; }

    /// <summary>
    /// Rival asignado por el marcaje estable (ADR 0022, §2.3). Lo fija <see cref="Marking"/> una vez por
    /// posesión y se mantiene mientras siga siendo válido; null si todavía no hay asignación.
    /// </summary>
    public MatchPlayer? MarkTarget { get; set; }

    /// <summary>Fuerza efectiva: base de nivel más modificadores activos, acotada a 1..99 (§3).</summary>
    public int Strength => _effectiveAttributes[(int)AttributeKind.Strength];

    /// <summary>Velocidad efectiva (§3).</summary>
    public int Speed => _effectiveAttributes[(int)AttributeKind.Speed];

    /// <summary>Técnica efectiva (§3).</summary>
    public int Technique => _effectiveAttributes[(int)AttributeKind.Technique];

    /// <summary>Resistencia efectiva (§3).</summary>
    public int Stamina => _effectiveAttributes[(int)AttributeKind.Stamina];

    public int HardTackleBonus { get; private set; }

    public int SpeedBonusPercent { get; private set; }

    public int ShotQualityBonus { get; private set; }

    public int ShootRangeBonusCells { get; private set; }

    public int PassQualityBonus { get; private set; }

    public int FoulChanceBonus { get; private set; }

    public int InjuryChanceBonus { get; private set; }

    public int FatigueResistancePercent { get; private set; }

    public int InjuryResistanceBonus { get; private set; }

    /// <summary>Bono porcentual que este jugador da a los compañeros con casilla-hogar contigua (Leader, RT-094).</summary>
    public int AdjacentTeammateBonusPercent { get; private set; }

    /// <summary>
    /// Suma de los bonos de los Leader del equipo con casilla-hogar contigua a la suya (§3.5). Se calcula
    /// una vez al construir el motor: las casillas-hogar no cambian durante el partido.
    /// </summary>
    public int LeaderBonusPercent { get; set; }

    public int SaveBonusClose { get; private set; }

    public int SaveBonusFar { get; private set; }

    public int LeashBonus { get; private set; }

    /// <summary>
    /// Suma delta a uno de los trece escalares de rasgo (efecto <c>modifyTraitScalar</c>, C4). Es la
    /// misma unidad que ya usa el rasgo correspondiente (puntos, casillas o puntos porcentuales según el
    /// escalar); el motor no distingue de dónde viene el número. <see cref="TraitScalarKind.LeashBonus"/>
    /// recalcula la zona de acción, porque es el único de los trece que alimenta
    /// <see cref="Recalculate"/>; los otros doce se leen directamente donde ya se leían.
    /// </summary>
    internal void AddTraitScalarDelta(TraitScalarKind kind, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        switch (kind)
        {
            case TraitScalarKind.HardTackleBonus:
                HardTackleBonus += delta;
                break;
            case TraitScalarKind.SpeedBonusPercent:
                SpeedBonusPercent += delta;
                break;
            case TraitScalarKind.ShotQualityBonus:
                ShotQualityBonus += delta;
                break;
            case TraitScalarKind.ShootRangeBonusCells:
                ShootRangeBonusCells += delta;
                break;
            case TraitScalarKind.PassQualityBonus:
                PassQualityBonus += delta;
                break;
            case TraitScalarKind.FoulChanceBonus:
                FoulChanceBonus += delta;
                break;
            case TraitScalarKind.InjuryChanceBonus:
                InjuryChanceBonus += delta;
                break;
            case TraitScalarKind.FatigueResistancePercent:
                FatigueResistancePercent += delta;
                break;
            case TraitScalarKind.InjuryResistanceBonus:
                InjuryResistanceBonus += delta;
                break;
            case TraitScalarKind.AdjacentTeammateBonusPercent:
                AdjacentTeammateBonusPercent += delta;
                break;
            case TraitScalarKind.SaveBonusClose:
                SaveBonusClose += delta;
                break;
            case TraitScalarKind.SaveBonusFar:
                SaveBonusFar += delta;
                break;
            case TraitScalarKind.LeashBonus:
                LeashBonus += delta;
                Recalculate();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>
    /// Casillas que un efecto <c>shiftHome</c> (C8) suma a la casilla-hogar efectiva de este jugador,
    /// positivo hacia la portería rival: lo lee <c>MatchEngine.UpdateBlockShift</c> y se SUMA al
    /// desplazamiento de bloque táctico que ya existía, no lo sustituye.
    /// </summary>
    public int HomeShiftCells { get; private set; }

    /// <summary>Suma delta a <see cref="HomeShiftCells"/> (efecto <c>shiftHome</c>, C8).</summary>
    internal void AddHomeShiftDelta(int delta) => HomeShiftCells += delta;

    /// <summary>Suma delta a una dimensión de la zona de acción (efecto <c>modifyZoneShape</c>, C8) y recalcula.</summary>
    internal void AddZoneShapeDelta(ZoneDimension dimension, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        _zoneShapeDeltaCells[(int)dimension] += delta;
        Recalculate();
    }

    /// <summary>
    /// Etiqueta de rival preferida en el reparto de marcas (C5, <c>modifyMarkBias</c> con variante
    /// <see cref="MarkBiasKind.PreferTag"/>, "Perro de presa"). Cadena vacía = sin preferencia.
    /// </summary>
    public string MarkPreferredTag { get; set; } = string.Empty;

    /// <summary>Descuento en casillas de <see cref="MarkPreferredTag"/> sobre el coste de <see cref="Marking"/>.</summary>
    public int MarkPreferredBonusCells { get; set; }

    /// <summary>
    /// Compañero que este jugador protege en el reparto de marcas (C5, <c>modifyMarkBias</c> con variante
    /// <see cref="MarkBiasKind.ProtectLinked"/>, "Guardaespaldas"): un rival cerca de él es más barato de
    /// marcar para este jugador. Null = no protege a nadie.
    /// </summary>
    public MatchPlayer? MarkProtect { get; set; }

    /// <summary>Descuento en casillas por estar cerca de <see cref="MarkProtect"/>.</summary>
    public int MarkProtectBonusCells { get; set; }

    /// <summary>
    /// Recargo en casillas que paga CUALQUIER marcador rival al considerar a este jugador como candidato
    /// (C5, <c>modifyMarkBias</c> con variante <see cref="MarkBiasKind.Avoided"/>, "Hombre libre"): es la
    /// única de las tres variantes de C5 que actúa sobre el emparejamiento del equipo CONTRARIO.
    /// </summary>
    public int MarkAvoidanceCells { get; set; }

    /// <summary>
    /// True si este jugador, al decidir una entrada sin balón (ADR 0105), prefiere al rival ya derribado
    /// en vez del marcado (C7, <c>modifyTackleBias</c> con variante <see cref="TackleBiasKind.KnockedDown"/>,
    /// "Olfato de sangre").
    /// </summary>
    public bool PreferKnockedDownTackleTarget { get; set; }

    /// <summary>
    /// Rival al que este jugador entra en vez de al marcado, mientras siga al alcance (C7,
    /// <c>modifyTackleBias</c> con variante <see cref="TackleBiasKind.Fouled"/>, "Rabia"): lo escribe el
    /// efecto en el momento de la falta y no caduca solo; un perk nuevo que lo sustituya o lo limpie es
    /// decisión de ese perk, no de esta primitiva.
    /// </summary>
    public MatchPlayer? TackleNemesis { get; set; }

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

    /// <summary>Casillas por tick que este jugador recorre este tick, en milésimas (entero, RT-023); lo recalcula <c>MatchEngine.UpdateContextCaches</c>. AZ-B paso 1.</summary>
    public int SpeedPerTickMilli { get; set; }

    /// <summary>Receptor elegido al decidir Pass; se lee al expirar Passing.</summary>
    public MatchPlayer? PassReceiver { get; set; }

    /// <summary>Rival objetivo al decidir Tackle; se lee al expirar Tackling.</summary>
    public MatchPlayer? TackleTarget { get; set; }

    /// <summary>
    /// True si la entrada decidida este tick es una <b>entrada sin balón</b> al marcado (ADR 0105): no
    /// había poseedor rival al alcance y el objetivo es <see cref="MarkTarget"/>. La escribe
    /// <c>Utility.Choose</c> junto a <see cref="TackleTarget"/> y la lee <c>ResolveTackle</c> al expirar
    /// Tackling, que es el único sitio donde el motor puede saber con qué intención se tiró la entrada:
    /// deducirla de "el objetivo ya no tiene el balón" confundiría la entrada sin balón con la
    /// <b>entrada a destiempo</b> (el conductor soltó el balón mientras duraba Tackling), que es otra
    /// cosa y sigue pagando la falta normal.
    /// </summary>
    public bool TackleOffBall { get; set; }

    /// <summary>
    /// Rival objetivo al decidir Block (ADR 0030 §2); se lee al expirar Blocking. Va en una propiedad
    /// propia y no en <see cref="TackleTarget"/> aunque las dos acciones nunca coincidan en el mismo
    /// tick: el volcado de utilidad y la depuración necesitan poder distinguir a quién iba a entrar un
    /// jugador de a quién iba a cargar.
    /// </summary>
    public MatchPlayer? BlockTarget { get; set; }

    /// <summary>Ticks que faltan para poder volver a disputar un regate (§3.7).</summary>
    public int DribbleDuelCooldown { get; set; }

    /// <summary>Ticks que faltan para poder volver a decidir una entrada (§3.5); mientras sea &gt; 0, Tackle se descarta.</summary>
    public int TackleCooldown { get; set; }

    /// <summary>
    /// Ticks que faltan para poder volver a decidir un bloqueo sin balón (ADR 0030 §2); mientras sea
    /// &gt; 0, Block se descarta. Contador propio, separado del de la entrada (paquete U): compartirlos
    /// (fase1b-diseno.md §6.8) hacía que cargar sin balón dejara al jugador sin poder disputar el balón
    /// durante 150 ticks, y se llevaba por delante la mitad de las entradas por partido.
    /// </summary>
    public int BlockCooldown { get; set; }

    /// <summary>Tarjetas amarillas acumuladas (la segunda es roja si lo dice tuning).</summary>
    public int YellowCards { get; set; }

    /// <summary>True mientras el jugador está en el campo (no lesionado ni expulsado).</summary>
    public bool OnPitch { get; set; } = true;

    /// <summary>Tick en el que dejó el campo (lesión, muerte o expulsión); −1 si sigue en él (ADR 0094).</summary>
    public int LeftPitchTick { get; set; } = -1;

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

    /// <summary>
    /// True si el jugador ha muerto en este partido (RF-093). Evita que dos vías —una segunda lesión y un
    /// perk letal en el mismo tick— cuenten dos muertes del mismo jugador.
    /// </summary>
    public bool Dead { get; set; }

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

    /// <summary>
    /// Recalcula los cinco atributos efectivos, la masa y la zona de acción a partir de base + deltas.
    ///
    /// <para>Zona (§2.2): la <b>forma</b> la da la posición (<c>tuning.actionZone.shape</c>) y el
    /// <b>tamaño</b> lo escala el atributo de correa, con el porcentaje interpolado linealmente entre
    /// <c>at1</c> y <c>at99</c>. El bono de rasgo y los modificadores <c>modifyLeash</c> se suman después,
    /// en casillas enteras, a cada dirección finita: son "más correa", no "otra forma". Una dirección sin
    /// límite (-1) lo sigue siendo pase lo que pase.</para>
    /// </summary>
    private void Recalculate()
    {
        for (int i = 0; i < AttributeCount; i++)
        {
            _effectiveAttributes[i] = Math.Clamp(_baseAttributes[i] + _attributeDeltas[i], 1, 99);
        }

        _mass = ((_effectiveAttributes[(int)AttributeKind.Strength] * _massStrengthWeight / 100)
            + (BodyRadiusCentiCells * _massRadiusWeight / 100));
        if (_mass < 1)
        {
            _mass = 1;
        }

        int leash = _effectiveAttributes[(int)AttributeKind.Leash];
        int percent = _leashScaleAt1 + ((_leashScaleAt99 - _leashScaleAt1) * (leash - 1) / 98);
        int extraMilli = (LeashBonus + _leashCellDelta) * 1000;

        // C8 (modifyZoneShape): a diferencia de extraMilli -que ensancha las tres direcciones por igual-,
        // esto es un delta POR DIMENSIÓN, sin escalar por el porcentaje de correa: es "más profundidad",
        // no "más correa", igual que extraMilli tampoco se escala por percent.
        _zone = new ActionZone(
            ExtentMilli(_shapeForward, percent, extraMilli + (_zoneShapeDeltaCells[(int)ZoneDimension.Forward] * 1000)),
            ExtentMilli(_shapeBack, percent, extraMilli + (_zoneShapeDeltaCells[(int)ZoneDimension.Back] * 1000)),
            ExtentMilli(_shapeSides, percent, extraMilli + (_zoneShapeDeltaCells[(int)ZoneDimension.Sides] * 1000)));
        _outerZone = _zone.Scaled(_outerLimitMultiplier);
    }

    /// <summary>Extensión en milicasillas de una dirección de la forma; -1 (sin límite) se propaga tal cual.</summary>
    private static int ExtentMilli(int shapeCells, int percent, int extraMilli)
    {
        if (shapeCells == ActionZone.Unlimited)
        {
            return ActionZone.Unlimited;
        }

        int milli = (shapeCells * 1000 * percent / 100) + extraMilli;
        return milli < 0 ? 0 : milli;
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

    /// <summary>Al banquillo antes del saque (ADR 0094): como fuera del campo, con su propio estado.</summary>
    public void Bench()
    {
        State = PlayerState.Benched;
        StateTicksLeft = 0;
        OnPitch = false;
        Position = new Vec2(-1f, -1f);
        Velocity = new Vec2(0f, 0f);
    }

    /// <summary>Entra al campo por una sustitución forzada (ADR 0094): en su casilla-hogar, posicionándose.</summary>
    public void EnterPitch()
    {
        State = PlayerState.Positioning;
        StateTicksLeft = 0;
        OnPitch = true;
        Position = HomeCenter;
        Velocity = new Vec2(0f, 0f);
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
        Tackles, TacklesWon, Fouls, Cards, Injured, TicksOnPitch, LeftPitchTick);
}
