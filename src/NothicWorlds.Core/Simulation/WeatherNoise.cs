using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Smooth repeatable noise in 3D for live weather's clouds (VISION.md WTH-02): the same seed and
/// point always give the same value, on every machine. Values run from 0 to 1, varying over
/// about one unit.
/// </summary>
internal static class WeatherNoise
{
    // Layers of finer detail added on, each half as strong.
    private const int Octaves = 3;

    /// <summary>Noise with finer detail layered on, from about 0 to 1.</summary>
    public static double Layered(Vector3D point, ulong seed)
    {
        double total = 0, amplitude = 0.5, weights = 0;
        for (int octave = 0; octave < Octaves; octave++)
        {
            total += amplitude * Value(point, seed + (ulong)octave);
            weights += amplitude;
            point = point * 2.03 + new Vector3D(1.7, 9.2, 3.1);
            amplitude *= 0.5;
        }

        return total / weights;
    }

    /// <summary>
    /// Value noise: a random value at each whole-number corner, blended smoothly between them.
    /// </summary>
    public static double Value(Vector3D point, ulong seed)
    {
        double fx = Math.Floor(point.X), fy = Math.Floor(point.Y), fz = Math.Floor(point.Z);
        long x = (long)fx, y = (long)fy, z = (long)fz;
        double tx = Smooth(point.X - fx), ty = Smooth(point.Y - fy), tz = Smooth(point.Z - fz);
        double Corner(long dx, long dy, long dz) => Hash(x + dx, y + dy, z + dz, seed);

        double bottom = Lerp(
            Lerp(Corner(0, 0, 0), Corner(1, 0, 0), tx),
            Lerp(Corner(0, 1, 0), Corner(1, 1, 0), tx), ty);
        double top = Lerp(
            Lerp(Corner(0, 0, 1), Corner(1, 0, 1), tx),
            Lerp(Corner(0, 1, 1), Corner(1, 1, 1), tx), ty);
        return Lerp(bottom, top, tz);
    }

    // A random number from 0 to 1 for a corner (SplitMix64's finisher on the mixed coordinates).
    private static double Hash(long x, long y, long z, ulong seed)
    {
        ulong h = unchecked(seed
            ^ ((ulong)x * 0x9E3779B97F4A7C15)
            ^ ((ulong)y * 0xC2B2AE3D27D4EB4F)
            ^ ((ulong)z * 0x165667B19E3779F9));
        h = unchecked((h ^ (h >> 30)) * 0xBF58476D1CE4E5B9);
        h = unchecked((h ^ (h >> 27)) * 0x94D049BB133111EB);
        h ^= h >> 31;
        return (h >> 11) * (1.0 / (1UL << 53));
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
