using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Paints a world's constellation lines onto the sky (VISION.md REN-07) for drawing: the six
/// faces of a cube side by side, as <see cref="StarField"/> lays them out, one byte a pixel
/// (how much line covers it). Lines follow the shortest way across the sky between their stars.
/// </summary>
public static class ConstellationLines
{
    /// <summary>Pixels along each edge of a cube face (about 0.18° each).</summary>
    public const int FaceSize = 512;

    // How wide a line is drawn, in pixels from its middle to where it has faded out.
    private const double HalfWidth = 1.3;

    // How far apart the dots that make up a line are, in pixels.
    private const double DotSpacing = 0.35;

    /// <summary>
    /// Paints the lines of <paramref name="constellations"/>, whose stars come from
    /// <paramref name="seed"/>; lines to stars not on that sky are skipped.
    /// </summary>
    /// <returns>
    /// <see cref="FaceSize"/> rows of six faces, each <see cref="FaceSize"/> wide.
    /// </returns>
    public static byte[] Paint(int seed, IEnumerable<Constellation> constellations)
    {
        var pixels = new byte[6 * FaceSize * FaceSize];
        double pixelAngle = Math.PI / 2 / FaceSize;
        foreach (StarLink line in constellations.SelectMany(c => c.Lines))
        {
            if (StarField.StarAt(seed, line.From) is StarField.Star from
                && StarField.StarAt(seed, line.To) is StarField.Star to)
            {
                PaintArc(pixels, from.Direction, to.Direction, pixelAngle);
            }
        }

        return pixels;
    }

    // Dots close together along the great circle from one direction to the other.
    private static void PaintArc(byte[] pixels, Vector3D from, Vector3D to, double pixelAngle)
    {
        double angle = Math.Acos(Math.Clamp(from.Dot(to), -1, 1));
        if (angle > Math.PI - 1e-6)
        {
            return;  // Stars exactly opposite: every way round is as short, so there's no line.
        }

        int steps = Math.Max(1, (int)Math.Ceiling(angle / (pixelAngle * DotSpacing)));
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            Vector3D point = Slerp(from, to, angle, t);
            (int face, double a, double b) = StarField.FaceOf(point);
            PaintDot(pixels, face, (a + 1) / 2 * FaceSize, (b + 1) / 2 * FaceSize);
        }
    }

    // A soft round dot centered at (x, y) on one face, kept to that face.
    private static void PaintDot(byte[] pixels, int face, double x, double y)
    {
        int width = 6 * FaceSize;
        int reach = (int)Math.Ceiling(HalfWidth);
        for (int py = (int)y - reach; py <= (int)y + reach; py++)
        {
            for (int px = (int)x - reach; px <= (int)x + reach; px++)
            {
                if (px < 0 || py < 0 || px >= FaceSize || py >= FaceSize)
                {
                    continue;
                }

                double distance = Math.Sqrt(
                    Math.Pow(px + 0.5 - x, 2) + Math.Pow(py + 0.5 - y, 2));
                double cover = Math.Clamp(1 - distance / HalfWidth, 0, 1);
                int at = py * width + face * FaceSize + px;
                pixels[at] = Math.Max(pixels[at], (byte)Math.Round(cover * 255));
            }
        }
    }

    private static Vector3D Slerp(Vector3D from, Vector3D to, double angle, double t)
    {
        if (angle < 1e-9)
        {
            return from;
        }

        double sine = Math.Sin(angle);
        return from * (Math.Sin((1 - t) * angle) / sine) + to * (Math.Sin(t * angle) / sine);
    }
}
