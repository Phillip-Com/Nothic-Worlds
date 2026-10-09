namespace NothicWorlds.Core.Simulation;

/// <summary>
/// How a river's bed rises and falls along it (VISION.md BOD-11; owner's choices: like the
/// variation within terrain, but worn smooth by the water unless made sharp on purpose): seeded
/// noise by distance from its source, so it stays fixed to the river wherever its course runs.
/// The same seed and distance always give the same value.
/// </summary>
/// <remarks>
/// <b>Never change how the noise is made</b> without a format change: rivers are saved with
/// only their settings, so the beds they show depend on it (docs/world-format.md).
/// </remarks>
public static class RiverBedNoise
{
    // The narrowest share of each rise or fall that's sloped at the least smoothness: the rest
    // is level, so the bed goes in steps.
    private const double SharpestRamp = 0.02;

    /// <summary>
    /// How much deeper (positive) or shallower the bed is than its depth, in meters, within
    /// ±<paramref name="variationMeters"/>, <paramref name="alongMeters"/> from the source.
    /// </summary>
    /// <param name="alongMeters">How far along the river, from its source.</param>
    /// <param name="variationMeters">How far the bed rises and falls (0 gives 0).</param>
    /// <param name="spacingKm">How far apart its rises and falls are.</param>
    /// <param name="smoothness">From 1 (smooth) to 0 (in steps).</param>
    /// <param name="seed">Gives each river its own bed.</param>
    public static double Offset(double alongMeters, double variationMeters, double spacingKm,
        double smoothness, int seed)
    {
        if (variationMeters <= 0 || spacingKm <= 0)
        {
            return 0;
        }

        double x = alongMeters / (spacingKm * 1000);
        double ramp = SharpestRamp + (1 - SharpestRamp) * Math.Clamp(smoothness, 0, 1);
        double big = Value(x, ramp, seed);
        double small = Value(2.03 * x + 0.37, ramp, seed + 101);
        return variationMeters * (big + 0.5 * small) / 1.5;
    }

    /// <summary>A river's seed, from its identity.</summary>
    public static int SeedFor(Guid riverId)
    {
        byte[] bytes = riverId.ToByteArray();
        return BitConverter.ToInt32(bytes, 0) ^ BitConverter.ToInt32(bytes, 4)
            ^ BitConverter.ToInt32(bytes, 8) ^ BitConverter.ToInt32(bytes, 12);
    }

    // Noise from -1 to 1, varying over about one unit: a random value at each whole number,
    // blended over the middle `ramp` share of the way to the next and level either side.
    private static double Value(double x, double ramp, int seed)
    {
        double floor = Math.Floor(x);
        int i = (int)floor;
        double t = Fade(Math.Clamp((x - floor - 0.5) / ramp + 0.5, 0, 1));
        double a = Hash(i, seed), b = Hash(i + 1, seed);
        return 2 * (a + (b - a) * t) - 1;
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    // A value from 0 to 1 for a whole number (a 32-bit integer mix).
    private static double Hash(int i, int seed)
    {
        uint h = (uint)seed;
        h ^= (uint)i * 0x8DA6B343u;
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h / (double)uint.MaxValue;
    }
}
