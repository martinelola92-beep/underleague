namespace Underleague.Sim.Random;

/// <summary>
/// PCG32 (Melissa O'Neill, pcg-random.org). Estado de 64 bits, incremento de 64 bits (siempre impar),
/// salida de 32 bits (XSH-RR). Único generador de aleatoriedad permitido en /Sim (RT-021, ADR 0004).
/// </summary>
public struct Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private readonly ulong _inc;

    /// <summary>pcg32_srandom_r: incremento derivado del flujo, un avance de calentamiento y suma de la semilla.</summary>
    public Pcg32(ulong seed, ulong stream)
    {
        _state = 0UL;
        _inc = (stream << 1) | 1UL;
        Next();
        _state = unchecked(_state + seed);
        Next();
    }

    /// <summary>Estado interno actual, solo para inspección en tests.</summary>
    public readonly ulong State => _state;

    /// <summary>pcg32_random_r: LCG de 64 bits con salida XSH-RR de 32 bits.</summary>
    public uint Next()
    {
        ulong oldState = _state;
        _state = unchecked(oldState * Multiplier + _inc);
        uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rot = (int)(oldState >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <summary>
    /// Entero uniforme en [minInclusive, maxExclusive). Sin sesgo: rechazo con umbral
    /// (pcg32_boundedrand_r), no el método de Lemire.
    /// </summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentException("maxExclusive debe ser mayor que minInclusive", nameof(maxExclusive));
        }

        uint bound = (uint)(maxExclusive - minInclusive);
        uint threshold = unchecked(0u - bound) % bound;
        uint result;
        do
        {
            result = Next();
        }
        while (result < threshold);

        return minInclusive + (int)(result % bound);
    }

    /// <summary>Probabilidad en base 10000 (RT-023): true con probabilidad probabilityBase10000 / 10000.</summary>
    public bool Chance(int probabilityBase10000) => Range(0, 10000) < probabilityBase10000;

    /// <summary>Ayudante equivalente a Chance pero en base 100, devuelto como 0/1 entero.</summary>
    public int Percent(int probabilityPercent) => Range(0, 100) < probabilityPercent ? 1 : 0;

    /// <summary>Elige un elemento uniforme de la lista.</summary>
    public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];

    /// <summary>Fisher-Yates desde el final, in place.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = Range(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}

/// <summary>SplitMix64 (Vigna): generador auxiliar usado solo para derivar semillas de flujos de RNG.</summary>
public static class SplitMix64
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>Avanza el estado y devuelve el siguiente valor de la secuencia SplitMix64.</summary>
    public static ulong Next(ref ulong state)
    {
        state = unchecked(state + GoldenGamma);
        ulong z = state;
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }

    /// <summary>
    /// Combina dos valores de 64 bits en uno solo, determinista. Decisión fuera de la especificación:
    /// se usa un avance de SplitMix64 sobre seed para decorrelar, un XOR con salt, y un segundo avance.
    /// </summary>
    public static ulong Mix(ulong seed, ulong salt)
    {
        ulong state = seed;
        _ = Next(ref state);
        state ^= salt;
        return Next(ref state);
    }
}

/// <summary>Fábrica de flujos de RNG independientes por dominio (RT-022): partido, mapa, recompensas, generación.</summary>
public static class RngStreams
{
    private const ulong MatchKind = 1UL;
    private const ulong MapKind = 2UL;
    private const ulong RewardsKind = 3UL;
    private const ulong GenerationKind = 4UL;

    /// <summary>Flujo de RNG para el partido del nodo nodeIndex de la run.</summary>
    public static Pcg32 Match(ulong runSeed, int nodeIndex) => Create(runSeed, MatchKind, nodeIndex);

    /// <summary>Flujo de RNG para la generación del mapa del acto act.</summary>
    public static Pcg32 Map(ulong runSeed, int act) => Create(runSeed, MapKind, act);

    /// <summary>Flujo de RNG para las recompensas del nodo nodeIndex.</summary>
    public static Pcg32 Rewards(ulong runSeed, int nodeIndex) => Create(runSeed, RewardsKind, nodeIndex);

    /// <summary>Flujo de RNG para la generación de jugadores/equipos con el índice index.</summary>
    public static Pcg32 Generation(ulong runSeed, int index) => Create(runSeed, GenerationKind, index);

    private static Pcg32 Create(ulong runSeed, ulong kind, int index)
    {
        ulong a = SplitMix64.Mix(runSeed, kind);
        ulong seed = SplitMix64.Mix(a, unchecked((ulong)index));
        ulong stream = SplitMix64.Mix(seed, kind);
        return new Pcg32(seed, stream);
    }
}
