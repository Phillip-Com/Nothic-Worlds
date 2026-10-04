namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Repeatable chance (SplitMix64): the same seeds always give the same numbers, on every
/// machine and .NET version (unlike <see cref="System.Random"/>, whose numbers may change
/// between versions). Used wherever the world makes things up, so the same world always comes
/// out the same.
/// </summary>
internal sealed class SeededRandom
{
    private ulong _state;

    /// <summary>Starts from any mix of seeds (e.g. IDs and a year number).</summary>
    public SeededRandom(params ulong[] seeds)
    {
        foreach (ulong seed in seeds)
        {
            _state ^= Mix(unchecked(seed + 0x9E3779B97F4A7C15 + _state));
        }
    }

    private SeededRandom()
    {
    }

    /// <summary>
    /// Starts from an exact state: for recipes that must keep giving the numbers they always
    /// have (see <see cref="AsteroidEvents"/>).
    /// </summary>
    public static SeededRandom FromState(ulong state) => new() { _state = state };

    /// <summary>A seed made from an ID.</summary>
    public static ulong SeedOf(Guid id)
    {
        byte[] bytes = id.ToByteArray();
        return BitConverter.ToUInt64(bytes, 0) ^ Mix(BitConverter.ToUInt64(bytes, 8));
    }

    /// <summary>A number from 0 (included) to 1 (not included).</summary>
    public double Next()
    {
        _state = unchecked(_state + 0x9E3779B97F4A7C15);
        return (Mix(_state) >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>A number between two values.</summary>
    public double Between(double low, double high) => low + (high - low) * Next();

    /// <summary>
    /// How many of something that happens <paramref name="mean"/> times on average (Knuth's
    /// method; fine for the small means used here).
    /// </summary>
    public int Poisson(double mean)
    {
        double limit = Math.Exp(-mean);
        int count = 0;
        for (double product = Next(); product > limit && count < 1000; product *= Next())
        {
            count++;
        }

        return count;
    }

    /// <summary>Scrambles a number thoroughly (SplitMix64's finishing step).</summary>
    public static ulong Mix(ulong value)
    {
        unchecked
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
            return value ^ (value >> 31);
        }
    }
}
