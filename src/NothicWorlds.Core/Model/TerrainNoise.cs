using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// The features inside a terrain type's ground (VISION.md BOD-08; owner's choice: how high and
/// how far apart, per type): smooth rolling noise for small variations, turning into sharp
/// ridged peaks for big ones, so mountains have peaks and ridges while plains only undulate.
/// The same seed and place always give the same value.
/// </summary>
/// <remarks>
/// <b>Never change how the noise is made</b> without a format change: worlds are saved with
/// only their settings, so the ground they show depends on it (docs/world-format.md).
/// </remarks>
public static class TerrainNoise
{
    /// <summary>Variations from this height up begin to turn ridged.</summary>
    public const double RidgedFromMeters = 400;

    /// <summary>Variations from this height up are fully ridged.</summary>
    public const double FullyRidgedMeters = 1_500;

    private const int MaxOctaves = 6;

    /// <summary>
    /// How far the ground rises (positive) or sinks around a type's height at a place, in
    /// meters, within ±<paramref name="variationMeters"/>.
    /// </summary>
    /// <param name="direction">The place: a unit direction from the body's center.</param>
    /// <param name="radiusKm">The body's radius.</param>
    /// <param name="variationMeters">The type's variation (0 gives 0).</param>
    /// <param name="sizeKm">How far apart its biggest features are.</param>
    /// <param name="smallestKm">
    /// The smallest features worth making (finer ones can't be drawn): about two grid cells.
    /// </param>
    /// <param name="seed">Gives each body and type its own features.</param>
    public static double Offset(Vector3D direction, double radiusKm, double variationMeters,
        double sizeKm, double smallestKm, int seed)
    {
        if (variationMeters <= 0 || sizeKm <= 0)
        {
            return 0;
        }

        Vector3D p = direction * (radiusKm / sizeKm);
        double ridged = SmoothStep((variationMeters - RidgedFromMeters)
            / (FullyRidgedMeters - RidgedFromMeters));
        double smooth = 0, sharp = 0, weight = 0, amplitude = 1, wavelength = sizeKm;
        for (int octave = 0; octave < MaxOctaves && (octave == 0 || wavelength >= smallestKm);
            octave++)
        {
            double n = Value(p, seed + octave * 101);  // 0 to 1
            smooth += amplitude * (2 * n - 1);
            double ridge = 1 - Math.Abs(2 * n - 1);
            sharp += amplitude * (2 * ridge * ridge - 1);
            weight += amplitude;
            amplitude *= 0.5;
            wavelength *= 0.5;
            p = p * 2.03 + new Vector3D(17.1, 3.7, 9.4);
        }

        return variationMeters * (smooth + (sharp - smooth) * ridged) / weight;
    }

    // Smooth noise from 0 to 1, varying over about one unit: a random value at each point of a
    // whole-number lattice, blended with a gentle curve.
    private static double Value(Vector3D p, int seed)
    {
        double fx = Math.Floor(p.X), fy = Math.Floor(p.Y), fz = Math.Floor(p.Z);
        int x = (int)fx, y = (int)fy, z = (int)fz;
        double tx = Fade(p.X - fx), ty = Fade(p.Y - fy), tz = Fade(p.Z - fz);
        double Lerp(double a, double b, double t) => a + (b - a) * t;
        double c00 = Lerp(Hash(x, y, z, seed), Hash(x + 1, y, z, seed), tx);
        double c10 = Lerp(Hash(x, y + 1, z, seed), Hash(x + 1, y + 1, z, seed), tx);
        double c01 = Lerp(Hash(x, y, z + 1, seed), Hash(x + 1, y, z + 1, seed), tx);
        double c11 = Lerp(Hash(x, y + 1, z + 1, seed), Hash(x + 1, y + 1, z + 1, seed), tx);
        return Lerp(Lerp(c00, c10, ty), Lerp(c01, c11, ty), tz);
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static double SmoothStep(double x)
    {
        double t = Math.Clamp(x, 0, 1);
        return t * t * (3 - 2 * t);
    }

    // A value from 0 to 1 for a lattice point (a 32-bit integer mix).
    private static double Hash(int x, int y, int z, int seed)
    {
        uint h = (uint)seed;
        h ^= (uint)x * 0x8DA6B343u;
        h ^= (uint)y * 0xD8163841u;
        h ^= (uint)z * 0xCB1AB31Fu;
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h / (double)uint.MaxValue;
    }
}
