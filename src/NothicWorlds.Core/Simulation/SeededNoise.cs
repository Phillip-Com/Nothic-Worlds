using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Random values that are the same on every machine for the same seed (VISION.md BOD-13): what
/// makes a dirt path's strands wander and an old road's bridge walls broken in the same places
/// every time.
/// </summary>
internal static class SeededNoise
{
    /// <summary>A seed from a road's id.</summary>
    public static ulong Seed(Guid id)
    {
        byte[] bytes = id.ToByteArray();
        return BitConverter.ToUInt64(bytes, 0) ^ BitConverter.ToUInt64(bytes, 8);
    }

    /// <summary>A seed from a place (a unit direction), for things laid there.</summary>
    public static ulong Seed(Vector3D place) =>
        (ulong)BitConverter.DoubleToInt64Bits(place.X)
        ^ ((ulong)BitConverter.DoubleToInt64Bits(place.Y) * 0x9E3779B97F4A7C15UL)
        ^ ((ulong)BitConverter.DoubleToInt64Bits(place.Z) * 0xBF58476D1CE4E5B9UL);

    /// <summary>
    /// Smooth noise from -1 to 1 along a line, for channel <paramref name="channel"/> of a
    /// seed: random values at whole numbers of <paramref name="x"/>, eased between.
    /// </summary>
    public static double Noise(ulong seed, int channel, double x)
    {
        double floor = Math.Floor(x);
        long knot = (long)floor;
        double t = x - floor;
        t = t * t * (3 - 2 * t);
        double a = Hashed(seed, channel, knot), b = Hashed(seed, channel, knot + 1);
        return a + (b - a) * t;
    }

    /// <summary>
    /// A random value from -1 to 1 for a seed, channel, and knot (SplitMix64's mixing).
    /// </summary>
    public static double Hashed(ulong seed, int channel, long knot)
    {
        ulong z = seed ^ ((ulong)channel * 0x9E3779B97F4A7C15UL)
            ^ ((ulong)knot * 0xBF58476D1CE4E5B9UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (2.0 / (1UL << 53)) - 1;
    }
}
