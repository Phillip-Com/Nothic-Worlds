using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Paints a world's nebulas onto the sky (VISION.md BOD-03; owner's choice: a backdrop, the same
/// from every planet), as an image covering every direction: x runs around from longitude −180°
/// to 180°, y down from latitude 90° to −90°, with directions as in
/// <see cref="SphericalCoordinates"/> (+Y up, longitude 0 toward +Z, 90° toward +X).
/// Deterministic: the same nebulas always paint the same sky.
/// </summary>
/// <remarks>
/// Each nebula is a soft glow around its direction whose edge and wisps are shaped by noise
/// seeded from its ID, blending from its main color toward its second color. Nebulas add their
/// light together on top of the background.
/// </remarks>
public static class NebulaSky
{
    // Noise: this many layers, each twice as fine, starting at this many features per radian.
    private const int Octaves = 4;
    private const double BaseFrequency = 3.0;

    /// <summary>
    /// The sky's colors, row by row from the top, three values (red, green, blue, 0 to 1) per
    /// pixel.
    /// </summary>
    public static float[] Paint(IReadOnlyList<Nebula> nebulas, int width, int height,
        RgbColor background)
    {
        var pixels = new float[width * height * 3];
        for (int i = 0; i < width * height; i++)
        {
            pixels[i * 3] = background.R / 255f;
            pixels[i * 3 + 1] = background.G / 255f;
            pixels[i * 3 + 2] = background.B / 255f;
        }

        foreach (Nebula nebula in nebulas)
        {
            PaintOne(pixels, width, height, nebula);
        }

        return pixels;
    }

    /// <summary>How strongly one nebula glows in a direction (unit length), 0 to 1.</summary>
    public static double Glow(Nebula nebula, Vector3D direction) =>
        Glow(nebula, Centre(nebula), Seed(nebula.Id), direction);

    private static double Glow(Nebula nebula, Vector3D centre, Vector3D seed, Vector3D direction)
    {
        double angle = Math.Acos(Math.Clamp(direction.Dot(centre), -1, 1));
        double size = double.DegreesToRadians(nebula.SizeDegrees);
        if (angle > size * 1.4)
        {
            return 0;
        }

        // The edge wanders in and out, and wisps thin the cloud inside.
        double edge = size * (0.6 + 0.8 * Noise(direction + seed));
        double falloff = Math.Clamp(1 - angle / edge, 0, 1);
        double wisps = 0.35 + 0.65 * Noise(direction * 2.3 + seed * 1.7);
        return Math.Pow(falloff, 1.5) * wisps * nebula.Brightness;
    }

    // Rows are painted in parallel: each pixel depends only on its own direction, so the result
    // is the same however the work is shared out.
    private static void PaintOne(float[] pixels, int width, int height, Nebula nebula)
    {
        // Only the rows the nebula can reach (it can't be more than 1.4 sizes from its center).
        double reach = nebula.SizeDegrees * 1.4;
        int top = Math.Max(0, (int)((90 - nebula.LatitudeDegrees - reach) / 180 * height));
        int bottom = Math.Min(height,
            (int)Math.Ceiling((90 - nebula.LatitudeDegrees + reach) / 180 * height));
        Vector3D centre = Centre(nebula);
        Vector3D shapeSeed = Seed(nebula.Id);
        Vector3D seed = shapeSeed * 3.1;
        Parallel.For(top, bottom, y =>
        {
            double latitude = 90 - (y + 0.5) / height * 180;
            for (int x = 0; x < width; x++)
            {
                double longitude = (x + 0.5) / width * 360 - 180;
                Vector3D direction =
                    SphericalPolygon.ToUnit(new GeoCoordinate(latitude, longitude));
                double glow = Glow(nebula, centre, shapeSeed, direction);
                if (glow <= 0)
                {
                    continue;
                }

                double blend = Noise(direction * 1.6 + seed);
                int at = (y * width + x) * 3;
                pixels[at] += (float)(glow * Mix(nebula.Color.R, nebula.SecondColor.R, blend));
                pixels[at + 1] +=
                    (float)(glow * Mix(nebula.Color.G, nebula.SecondColor.G, blend));
                pixels[at + 2] +=
                    (float)(glow * Mix(nebula.Color.B, nebula.SecondColor.B, blend));
            }
        });
    }

    private static Vector3D Centre(Nebula nebula) => SphericalPolygon.ToUnit(
        new GeoCoordinate(nebula.LatitudeDegrees, nebula.LongitudeDegrees));

    private static double Mix(byte from, byte to, double share) =>
        (from + (to - from) * share) / 255.0;

    // A different patch of noise for each nebula.
    private static Vector3D Seed(Guid id)
    {
        byte[] bytes = id.ToByteArray();
        return new Vector3D(bytes[0], bytes[1], bytes[2]) * 0.71;
    }

    // Smooth noise from 0 to 1 over a direction: several layers, each finer and fainter.
    private static double Noise(Vector3D point)
    {
        double total = 0;
        double weight = 0.5;
        double frequency = BaseFrequency;
        for (int i = 0; i < Octaves; i++)
        {
            total += weight * ValueNoise(point * frequency);
            weight *= 0.5;
            frequency *= 2;
        }

        return total / (1 - Math.Pow(0.5, Octaves));
    }

    // Values at whole-number points, blended smoothly in between.
    private static double ValueNoise(Vector3D point)
    {
        int x = (int)Math.Floor(point.X);
        int y = (int)Math.Floor(point.Y);
        int z = (int)Math.Floor(point.Z);
        double fx = Smooth(point.X - x);
        double fy = Smooth(point.Y - y);
        double fz = Smooth(point.Z - z);
        double Corner(int dx, int dy, int dz) => Hash(x + dx, y + dy, z + dz);
        double Lerp(double a, double b, double t) => a + (b - a) * t;
        return Lerp(
            Lerp(Lerp(Corner(0, 0, 0), Corner(1, 0, 0), fx),
                Lerp(Corner(0, 1, 0), Corner(1, 1, 0), fx), fy),
            Lerp(Lerp(Corner(0, 0, 1), Corner(1, 0, 1), fx),
                Lerp(Corner(0, 1, 1), Corner(1, 1, 1), fx), fy),
            fz);
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);

    // A repeatable value from 0 to 1 for a whole-number point.
    private static double Hash(int x, int y, int z)
    {
        uint h = unchecked((uint)(x * 374761393 + y * 668265263 + z * 1274126177));
        h = unchecked((h ^ (h >> 13)) * 1274126177);
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (double)0xFFFFFF;
    }
}
