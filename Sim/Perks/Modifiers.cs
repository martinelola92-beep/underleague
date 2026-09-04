using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// Registro de modificadores activos de un partido (§3): atributos, correa en casillas y probabilidades.
/// <para>
/// Los modificadores de duración <c>match</c> y <c>run</c> no caducan dentro del partido, así que solo se
/// guarda una entrada por modificador de duración <c>play</c>: lo único que hay que poder deshacer al
/// terminar la jugada. Los de atributo y correa se aplican directamente sobre el jugador, que cachea su
/// valor efectivo; los de probabilidad viven en una tabla plana <c>jugador x tipo</c> para que cada
/// resolución del motor los consulte con un acceso a array.
/// </para>
/// </summary>
internal sealed class Modifiers
{
    private static readonly int ProbabilityKindCount = Enum.GetValues<ProbabilityKind>().Length;

    private readonly MatchPlayer[] _players;
    private readonly int[] _probability;
    private readonly int[] _knockdownTicks;
    private readonly int[] _immunities;
    private readonly List<AttributeEntry> _playAttributes = new();
    private readonly List<LeashEntry> _playLeash = new();
    private readonly List<ProbabilityEntry> _playProbability = new();
    private readonly List<PairEntry> _pairs = new();

    private EventType _eventType;
    private int _eventActor = -1;
    private int _eventTarget = -1;
    private int _eventOpponent = -1;

    public Modifiers(MatchPlayer[] players)
    {
        _players = players;
        _probability = new int[players.Length * ProbabilityKindCount];
        _knockdownTicks = new int[players.Length];
        _immunities = new int[players.Length];
    }

    /// <summary>Suma delta al atributo del jugador; si expira con la jugada, se anota para deshacerlo.</summary>
    public void AddAttribute(int playerIndex, AttributeKind kind, int delta, bool expiresAtPlayEnd)
    {
        if (delta == 0)
        {
            return;
        }

        _players[playerIndex].AddAttributeDelta(kind, delta);
        if (expiresAtPlayEnd)
        {
            _playAttributes.Add(new AttributeEntry(playerIndex, kind, delta));
        }
    }

    /// <summary>Suma delta al radio de correa en casillas del jugador (§2, modifyLeash).</summary>
    public void AddLeash(int playerIndex, int delta, bool expiresAtPlayEnd)
    {
        if (delta == 0)
        {
            return;
        }

        _players[playerIndex].AddLeashCellDelta(delta);
        if (expiresAtPlayEnd)
        {
            _playLeash.Add(new LeashEntry(playerIndex, delta));
        }
    }

    /// <summary>Suma delta (puntos base 10000) a una probabilidad del jugador (§2, modifyProbability).</summary>
    public void AddProbability(int playerIndex, ProbabilityKind kind, int delta, bool expiresAtPlayEnd)
    {
        if (delta == 0)
        {
            return;
        }

        _probability[(playerIndex * ProbabilityKindCount) + (int)kind] += delta;
        if (expiresAtPlayEnd)
        {
            _playProbability.Add(new ProbabilityEntry(playerIndex, kind, delta));
        }
    }

    /// <summary>
    /// Registra un modificador **por par** (ADR 0021, §2.4): vale solo en la resolución que enfrenta a
    /// <paramref name="fromIndex"/> con <paramref name="toIndex"/>, no en las demás. Es lo que expresa
    /// "mejora el pase **hacia** ese compañero concreto" en vez de "mejora el pase".
    /// </summary>
    public void AddPairProbability(int fromIndex, int toIndex, ProbabilityKind kind, int delta, bool expiresAtPlayEnd)
    {
        if (delta == 0 || fromIndex == toIndex)
        {
            return;
        }

        _pairs.Add(new PairEntry(fromIndex, toIndex, kind, delta, expiresAtPlayEnd));
    }

    /// <summary>
    /// Fija el evento cuya resolución está a punto de ocurrir. Lo llama <see cref="EffectEngine"/> en cada
    /// publicación, y es lo que da contexto a los modificadores por par: el motor publica PASS_ATTEMPTED,
    /// TACKLE y SHOT **antes** de tirar sus dados (semántica pre-resolución de MatchEngine), así que al
    /// consultarse la probabilidad el par (sujeto, contraparte) de la jugada es exactamente el de este
    /// evento. Sin evento aplicable no hay bono por par: el modificador desaparece, nunca se aplica mal.
    /// </summary>
    public void SetResolutionContext(EventType type, MatchPlayer? actor, MatchPlayer? target, MatchPlayer? opponent)
    {
        _eventType = type;
        _eventActor = actor?.Index ?? -1;
        _eventTarget = target?.Index ?? -1;
        _eventOpponent = opponent?.Index ?? -1;
    }

    /// <summary>Modificador acumulado de una probabilidad del jugador; 0 si no hay ninguno.</summary>
    public int Probability(MatchPlayer player, ProbabilityKind kind)
    {
        int total = _probability[(player.Index * ProbabilityKindCount) + (int)kind];
        if (_pairs.Count == 0)
        {
            return total;
        }

        for (int i = 0; i < _pairs.Count; i++)
        {
            var pair = _pairs[i];
            if (pair.Kind == kind && pair.FromIndex == player.Index && IsCounterpartOfCurrentEvent(pair, kind))
            {
                total += pair.Delta;
            }
        }

        return total;
    }

    /// <summary>
    /// Ticks que hay que sumar al derribo que provoca este jugador (efecto <c>modifyKnockdownTicks</c>,
    /// habilidad Sangre caliente de los orcos, ADR 0026). El motor lo suma a la duración del estado
    /// KnockedDown que aplica al rival tras una entrada.
    /// </summary>
    public int KnockdownTicks(MatchPlayer player) => _knockdownTicks[player.Index];

    /// <summary>Suma ticks al derribo que provoca el jugador.</summary>
    public void AddKnockdownTicks(int playerIndex, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        _knockdownTicks[playerIndex] += delta;
    }

    /// <summary>True si el jugador tiene esa inmunidad (efecto <c>immunity</c>, ADR 0026).</summary>
    public bool HasImmunity(MatchPlayer player, ImmunityKind kind) =>
        (_immunities[player.Index] & (1 << (int)kind)) != 0;

    /// <summary>
    /// Concede una inmunidad al jugador. <see cref="ImmunityKind.Push"/> se refleja además en
    /// <c>MatchPlayer.Immovable</c>, que es lo que consulta la separación de cuerpos (ADR 0020): la
    /// habilidad racial la siembra el motor al construir el jugador, y este efecto permite que cualquier
    /// otro perk, objeto o consumible la encienda.
    /// </summary>
    public void AddImmunity(int playerIndex, ImmunityKind kind)
    {
        _immunities[playerIndex] |= 1 << (int)kind;
        if (kind == ImmunityKind.Push)
        {
            _players[playerIndex].Immovable = true;
        }
    }

    /// <summary>
    /// Evento que establece el par de cada resolución probabilística. Es una tabla explícita y cerrada, y
    /// no "el último evento publicado", para que un cambio en el motor haga desaparecer el bono por par en
    /// vez de aplicarlo a la disputa equivocada.
    /// </summary>
    private static EventType PairEventFor(ProbabilityKind kind) => kind switch
    {
        ProbabilityKind.Pass or ProbabilityKind.Intercept or ProbabilityKind.InterceptEvasion => EventType.PassAttempted,
        ProbabilityKind.Dribble => EventType.DribbleAttempted,
        ProbabilityKind.ShotOnTarget or ProbabilityKind.Save => EventType.Shot,
        _ => EventType.Tackle,
    };

    private bool IsCounterpartOfCurrentEvent(PairEntry pair, ProbabilityKind kind)
    {
        if (_eventType != PairEventFor(kind))
        {
            return false;
        }

        int to = pair.ToIndex;
        return to == _eventTarget || to == _eventOpponent || to == _eventActor;
    }

    /// <summary>
    /// Retira todos los modificadores de duración <c>play</c> (§2). Lo llama el motor justo después de
    /// publicar PLAY_END, no antes: un perk disparado por PLAY_END que aplique un modificador de jugada
    /// lo aplica sobre la jugada siguiente, no sobre una que ya ha terminado.
    /// </summary>
    public void ExpirePlayModifiers()
    {
        for (int i = 0; i < _playAttributes.Count; i++)
        {
            var entry = _playAttributes[i];
            _players[entry.PlayerIndex].AddAttributeDelta(entry.Kind, -entry.Delta);
        }

        for (int i = 0; i < _playLeash.Count; i++)
        {
            var entry = _playLeash[i];
            _players[entry.PlayerIndex].AddLeashCellDelta(-entry.Delta);
        }

        for (int i = 0; i < _playProbability.Count; i++)
        {
            var entry = _playProbability[i];
            _probability[(entry.PlayerIndex * ProbabilityKindCount) + (int)entry.Kind] -= entry.Delta;
        }

        _playAttributes.Clear();
        _playLeash.Clear();
        _playProbability.Clear();
        _pairs.RemoveAll(static p => p.ExpiresAtPlayEnd);
    }

    private readonly record struct AttributeEntry(int PlayerIndex, AttributeKind Kind, int Delta);

    private readonly record struct LeashEntry(int PlayerIndex, int Delta);

    private readonly record struct ProbabilityEntry(int PlayerIndex, ProbabilityKind Kind, int Delta);

    private readonly record struct PairEntry(
        int FromIndex, int ToIndex, ProbabilityKind Kind, int Delta, bool ExpiresAtPlayEnd);
}
