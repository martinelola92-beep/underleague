using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Run.Systems.Nodes;

/// <summary>Qué hace una sesión de la carta de entrenamiento (ADR 0160).</summary>
public enum TrainingSessionKind
{
    /// <summary>La opción segura: experiencia de hoy para todos los disponibles. Sin coste, sin objetivo.</summary>
    Scrimmage,

    /// <summary>+8 permanente a <see cref="TrainingSession.Attribute"/> del señalado. Cuesta la experiencia del resto.</summary>
    Specialization,

    /// <summary>El señalado pasa a otra posición de campo. Cuesta un nivel (con su pérdida de atributos).</summary>
    Reposition,
}

/// <summary>Una sesión de la carta (ADR 0160). <see cref="Attribute"/> solo aplica a <see cref="TrainingSessionKind.Specialization"/>.</summary>
public sealed record TrainingSession(TrainingSessionKind Kind, AttributeKind Attribute = AttributeKind.Strength);

/// <summary>La carta de un nodo de entrenamiento: siempre tres sesiones (ADR 0160).</summary>
public sealed record TrainingCard(int NodeId, IReadOnlyList<TrainingSession> Sessions);

/// <summary>
/// El entrenamiento se elige (ADR 0160): pachanga, especialización o cambio de puesto. Cumple RF-026
/// ("experiencia dirigida al jugador que el usuario elija"), que el nodo automático no cumplía. Enmienda
/// RF-022b: la posición deja de ser fija, solo por esta vía.
///
/// <para>La carta -tres sesiones- se <b>deriva del nodo</b>, no se guarda (mismo principio W-12 que la
/// carta de evento, ADR 0100): mismo nodo, misma carta. Sin RNG: qué dos atributos ofrece la
/// especialización y si el nodo sustituye una de ellas por el cambio de puesto sale de
/// <c>node.Id</c>, no de un flujo de <c>Random</c> -no hay nada que "sortear" aquí, sólo una tabla fija
/// recorrida por índice (RT-021: no hace falta gastar un flujo de RNG para algo determinista por
/// construcción).</para>
///
/// <para><b>Una sesión se elige una vez</b> (revisión independiente, 29 sep 2026): elegir resuelve el nodo
/// (<see cref="RunState.NodeResolvedCounter"/>) y una segunda elección lanza; solo queda salir. Antes
/// <c>Choose</c> no cerraba nada y tres pachangas seguidas daban 3x40 de experiencia.</para>
/// </summary>
public static class TrainingSystem
{
    /// <summary>
    /// +8 por especialización (ADR 0160, provisional, sin medir): tiene que valer claramente más que un
    /// nivel en ese atributo (<c>progression.attributesPerLevel</c>) o la sesión no compensa nunca a la
    /// pachanga (la objeción del revisor a la "doble sesión").
    /// </summary>
    public const int SpecializationBonus = 8;

    /// <summary>Niveles que cuesta el cambio de puesto (ADR 0160): siempre uno.</summary>
    public const int RepositionLevelCost = 1;

    /// <summary>
    /// Los cuatro atributos que puede especializar una sesión: fuerza, velocidad, técnica y resistencia, en
    /// un orden fijo. <b>Sin la correa</b> (disciplina posicional, no nivel: mismo criterio que
    /// <c>Progression.AttributesAtLevel</c>, el esquema de eventos y RF-027).
    /// </summary>
    private static readonly AttributeKind[] Specializable =
    {
        AttributeKind.Strength, AttributeKind.Speed, AttributeKind.Technique, AttributeKind.Stamina,
    };

    /// <summary>
    /// El par de atributos de un nodo: el primero es <c>node.Id % 4</c> y el segundo el que está
    /// <c>1 + (node.Id / 4) % 3</c> lugares después (siempre distinto del primero). El reparto anterior
    /// -seis pares indexados por <c>node.Id % 6</c>- dejaba en los nodos con cambio de puesto
    /// (<c>node.Id % 3 == 0</c>, donde solo se ofrece el primero) únicamente fuerza y velocidad, y nunca
    /// técnica ni resistencia (revisión independiente). Con <c>gcd(3, 4) = 1</c>, los nodos múltiplos de tres
    /// recorren los cuatro residuos por igual, y en el resto de nodos el primero y el segundo también salen
    /// parejos.
    /// </summary>
    private static (AttributeKind First, AttributeKind Second) PairOf(MapNode node)
    {
        int first = node.Id % Specializable.Length;
        int second = (first + 1 + ((node.Id / Specializable.Length) % (Specializable.Length - 1))) % Specializable.Length;
        return (Specializable[first], Specializable[second]);
    }

    /// <summary>
    /// La carta de ese nodo: pachanga, más dos especializaciones de atributos distintos -salvo en uno de
    /// cada tres nodos (<c>node.Id % 3 == 0</c>, determinista), donde la segunda especialización se
    /// sustituye por un cambio de puesto (ADR 0160). Derivada, no guardada: mismo nodo, misma carta.
    /// </summary>
    public static TrainingCard Card(MapNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var pair = PairOf(node);
        var sessions = new List<TrainingSession>(3)
        {
            new(TrainingSessionKind.Scrimmage),
            new(TrainingSessionKind.Specialization, pair.First),
        };

        sessions.Add(node.Id % 3 == 0
            ? new TrainingSession(TrainingSessionKind.Reposition)
            : new TrainingSession(TrainingSessionKind.Specialization, pair.Second));

        return new TrainingCard(node.Id, sessions);
    }

    /// <summary>
    /// Resuelve la sesión elegida <b>y con ella el nodo</b>: una segunda elección lanza
    /// (<see cref="RunState.NodeResolvedCounter"/>). Lanza si la sesión no existe, si pide un señalado que no
    /// se ha dado (o que no está disponible), si la especialización apunta a un atributo ya en 99, si el
    /// cambio de puesto no trae la posición de destino, apunta a un jugador de nivel 1 (perder un nivel sería
    /// gratis) o a una posición donde un perk del jugador no vale (<see cref="BlockingPerk"/>), o si toca a
    /// un portero en cualquier dirección (ADR 0160: el mercado garantiza exactamente uno, ADR 0080, y la
    /// cuadrícula lo trata aparte).
    /// </summary>
    public static RunState Choose(RunState state, ChooseTrainingSession decision, EconomyConfig economy, Data.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(catalog);
        var node = NodeGuards.RequireOpen(state, NodeKind.Training, "elegir la sesión de entrenamiento");
        NodeGuards.RequireUnresolved(state, node, "elegir otra sesión de entrenamiento");
        var card = Card(node);
        if (decision.SessionIndex < 0 || decision.SessionIndex >= card.Sessions.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision.SessionIndex,
                $"la carta del nodo {node.Id} tiene {card.Sessions.Count} sesiones (0..{card.Sessions.Count - 1})");
        }

        var session = card.Sessions[decision.SessionIndex];
        var next = session.Kind switch
        {
            TrainingSessionKind.Scrimmage => ServiceNodeSystem.Training(state, economy, catalog),
            TrainingSessionKind.Specialization => Specialize(state, decision.TargetPlayerId, session.Attribute),
            TrainingSessionKind.Reposition => Reposition(state, decision.TargetPlayerId, decision.Position, catalog),
            _ => throw new ArgumentOutOfRangeException(nameof(decision), session.Kind, "sesión de entrenamiento desconocida"),
        };

        return NodeGuards.MarkResolved(next, node);
    }

    /// <summary>Si la especialización en ese atributo cambia algo: a 99 ya no se sube más (<c>Attributes.Clamp</c>).</summary>
    public static bool CanSpecialize(RunPlayer player, AttributeKind attribute) =>
        player.Attributes.Get(attribute) < Events.EventSystem.AttributeCap;

    /// <summary>
    /// El id del primer perk del jugador que <b>no vale</b> en <paramref name="destination"/> (su
    /// <c>positionOnly</c> es otra posición, le falta una etiqueta que exige o tiene una que prohíbe con la
    /// etiqueta de posición ya cambiada), o null si todos valen. Es la misma regla que aplica
    /// <c>Simulator</c> al validar el equipo (un perk fuera de sitio tira el partido entero) y
    /// <c>PerkAssignment.Eligible</c> al asignar: un jugador con un perk de su posición no puede cambiar a
    /// donde ese perk no vale, y la vista lo deshabilita diciendo cuál (RF-012d).
    /// </summary>
    public static string? BlockingPerk(RunPlayer player, Position destination, Data.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(catalog);
        var tags = RunLineup.WithPositionTag(player.Tags, player.Position, destination);
        for (int i = 0; i < player.Perks.Count; i++)
        {
            var perk = catalog.Perks.Find(player.Perks[i]);
            if (perk is null)
            {
                continue;
            }

            if (perk.PositionOnly is { } only && only != destination)
            {
                return perk.Id;
            }

            for (int t = 0; t < perk.TagsRequired.Count; t++)
            {
                if (!tags.Contains(perk.TagsRequired[t], StringComparer.Ordinal))
                {
                    return perk.Id;
                }
            }

            for (int t = 0; t < perk.TagsForbidden.Count; t++)
            {
                if (tags.Contains(perk.TagsForbidden[t], StringComparer.Ordinal))
                {
                    return perk.Id;
                }
            }
        }

        return null;
    }

    /// <summary>Posiciones de campo a las que puede cambiar ese jugador, en orden fijo: nunca portero (ADR 0080).</summary>
    public static readonly IReadOnlyList<Position> FieldPositions =
        new[] { Position.Defender, Position.Midfielder, Position.Forward };

    private static RunPlayer TargetPlayer(RunState state, int targetId, string action)
    {
        if (targetId < 0)
        {
            throw new ArgumentException($"{action} necesita un jugador señalado", nameof(targetId));
        }

        var player = state.GetPlayer(targetId);
        if (!player.IsAvailable)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} no está disponible: no se entrena a alguien que ya está fuera",
                nameof(targetId));
        }

        return player;
    }

    private static RunState Specialize(RunState state, int targetId, AttributeKind attribute)
    {
        var player = TargetPlayer(state, targetId, "la especialización");
        if (!CanSpecialize(player, attribute))
        {
            throw new ArgumentException(
                $"el jugador {player.Id} ya está en 99 de {attribute}: la especialización no le sumaría nada",
                nameof(targetId));
        }

        var attributes = Attributes.Clamp(player.Attributes.With(attribute, player.Attributes.Get(attribute) + SpecializationBonus));
        return state.WithPlayer(player with { Attributes = attributes });
    }

    private static RunState Reposition(RunState state, int targetId, Position? position, Data.Catalog catalog)
    {
        var player = TargetPlayer(state, targetId, "el cambio de puesto");
        if (player.Position == Position.Goalkeeper)
        {
            throw new ArgumentException(
                "el portero no cambia de puesto (ADR 0080, ADR 0160): el mercado garantiza exactamente uno",
                nameof(targetId));
        }

        if (position is null)
        {
            throw new ArgumentException("el cambio de puesto necesita la posición de destino", nameof(position));
        }

        if (position == Position.Goalkeeper)
        {
            throw new ArgumentException(
                "nadie pasa a portero por entrenamiento (ADR 0080, ADR 0160): el mercado garantiza exactamente uno",
                nameof(position));
        }

        if (position == player.Position)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} ya juega de {player.Position}: cambiar a la misma posición solo cuesta un nivel",
                nameof(position));
        }

        if (!LevelLoss.CanLose(player))
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está en el nivel 1: el cambio de puesto no le costaría nada y no se ofrece",
                nameof(targetId));
        }

        if (BlockingPerk(player, position.Value, catalog) is { } blocking)
        {
            throw new ArgumentException(
                $"el perk '{blocking}' del jugador {player.Id} no vale de {position}: un perk de una posición no se cambia de sitio",
                nameof(position));
        }

        // La posición va también en las etiquetas (ADR 0024): sin cambiarla, un perk que exige o prohíbe
        // una etiqueta de posición fallaba en el simulador. RunLineup.Repositioned ya sabe mantenerlas.
        var lost = LevelLoss.Apply(player, RepositionLevelCost, catalog);
        return state.WithPlayer(lost with
        {
            Position = position.Value,
            Tags = RunLineup.WithPositionTag(player.Tags, player.Position, position.Value),
        });
    }
}
