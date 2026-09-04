using Underleague.Sim.Engine;
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
    private readonly List<AttributeEntry> _playAttributes = new();
    private readonly List<LeashEntry> _playLeash = new();
    private readonly List<ProbabilityEntry> _playProbability = new();

    public Modifiers(MatchPlayer[] players)
    {
        _players = players;
        _probability = new int[players.Length * ProbabilityKindCount];
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

    /// <summary>Modificador acumulado de una probabilidad del jugador; 0 si no hay ninguno.</summary>
    public int Probability(MatchPlayer player, ProbabilityKind kind) =>
        _probability[(player.Index * ProbabilityKindCount) + (int)kind];

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
    }

    private readonly record struct AttributeEntry(int PlayerIndex, AttributeKind Kind, int Delta);

    private readonly record struct LeashEntry(int PlayerIndex, int Delta);

    private readonly record struct ProbabilityEntry(int PlayerIndex, ProbabilityKind Kind, int Delta);
}
