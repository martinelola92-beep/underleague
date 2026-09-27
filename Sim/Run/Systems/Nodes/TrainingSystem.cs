using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;
using ProgressionRules = Underleague.Sim.Progression.Progression;

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
    /// Los seis pares posibles de dos atributos distintos entre los cuatro que suma un nivel (fuerza,
    /// velocidad, técnica, resistencia -sin la correa, que es disciplina posicional y no nivel, mismo
    /// criterio que <c>Progression.AttributesAtLevel</c>-), en un orden fijo para que el índice por
    /// <c>node.Id</c> sea estable entre ejecuciones (RT-021).
    /// </summary>
    private static readonly AttributeKind[][] AttributePairs =
    {
        new[] { AttributeKind.Strength, AttributeKind.Speed },
        new[] { AttributeKind.Strength, AttributeKind.Technique },
        new[] { AttributeKind.Strength, AttributeKind.Stamina },
        new[] { AttributeKind.Speed, AttributeKind.Technique },
        new[] { AttributeKind.Speed, AttributeKind.Stamina },
        new[] { AttributeKind.Technique, AttributeKind.Stamina },
    };

    /// <summary>
    /// La carta de ese nodo: pachanga, más dos especializaciones de atributos distintos -salvo en uno de
    /// cada tres nodos (<c>node.Id % 3 == 0</c>, determinista), donde la segunda especialización se
    /// sustituye por un cambio de puesto (ADR 0160). Derivada, no guardada: mismo nodo, misma carta.
    /// </summary>
    public static TrainingCard Card(MapNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var pair = AttributePairs[node.Id % AttributePairs.Length];
        var sessions = new List<TrainingSession>(3)
        {
            new(TrainingSessionKind.Scrimmage),
            new(TrainingSessionKind.Specialization, pair[0]),
        };

        sessions.Add(node.Id % 3 == 0
            ? new TrainingSession(TrainingSessionKind.Reposition)
            : new TrainingSession(TrainingSessionKind.Specialization, pair[1]));

        return new TrainingCard(node.Id, sessions);
    }

    /// <summary>
    /// Resuelve la sesión elegida. Lanza si la sesión no existe, si pide un señalado que no se ha dado (o
    /// que no está disponible), si el cambio de puesto no trae la posición de destino, o si toca a un
    /// portero en cualquier dirección (ADR 0160: el mercado garantiza exactamente uno, ADR 0080, y la
    /// cuadrícula lo trata aparte).
    /// </summary>
    public static RunState Choose(RunState state, ChooseTrainingSession decision, EconomyConfig economy, Data.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(catalog);
        var node = NodeGuards.RequireOpen(state, NodeKind.Training, "elegir la sesión de entrenamiento");
        var card = Card(node);
        if (decision.SessionIndex < 0 || decision.SessionIndex >= card.Sessions.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision.SessionIndex,
                $"la carta del nodo {node.Id} tiene {card.Sessions.Count} sesiones (0..{card.Sessions.Count - 1})");
        }

        var session = card.Sessions[decision.SessionIndex];
        return session.Kind switch
        {
            TrainingSessionKind.Scrimmage => ServiceNodeSystem.Training(state, economy, catalog),
            TrainingSessionKind.Specialization => Specialize(state, decision.TargetPlayerId, session.Attribute),
            TrainingSessionKind.Reposition => Reposition(state, decision.TargetPlayerId, decision.Position, catalog),
            _ => throw new ArgumentOutOfRangeException(nameof(decision), session.Kind, "sesión de entrenamiento desconocida"),
        };
    }

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

        var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
        var down = ProgressionRules.LevelDown(definition, RepositionLevelCost, catalog.Progression);

        // Regla I: LevelDown no toca la experiencia, así que se acota aquí -mismo mecanismo que el efecto
        // `level` de eventos (ADR 0159)- para que la próxima experiencia ganada no deshaga la pérdida.
        int cap = ProgressionRules.MinExperienceForLevel(down.Level + 1, catalog.Progression) - 1;
        int experience = cap >= 0 ? Math.Min(player.Experience, cap) : player.Experience;

        return state.WithPlayer(player with
        {
            Position = position.Value,
            Level = down.Level,
            Attributes = down.Attributes,
            Experience = experience,
        });
    }
}
