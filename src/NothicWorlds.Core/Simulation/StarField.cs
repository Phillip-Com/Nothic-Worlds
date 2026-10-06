using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The fixed stars on a world's night sky (VISION.md REN-07; owner's choice: a star field from
/// a seed that can be re-rolled), the same from every planet, like the nebulas. The sky is
/// split into the cells of a cube's six faces; each cell holds at most one star, which is its
/// id. Everything follows from the seed alone.
/// </summary>
/// <remarks>
/// <b>Never change how stars are made.</b> Saved constellations name stars by id, so the same
/// seed must always give the same stars. godot/Rendering/star_sky.gdshaderinc finds a direction's
/// cell the same way <see cref="CellOf"/> does; keep the two in step.
/// <para>A cube face's cells near its corners cover less of the sky than those in its middle,
/// so each cell's chance of a star follows its size, keeping the stars evenly spread.</para>
/// </remarks>
public static class StarField
{
    /// <summary>Cells along each edge of a cube face.</summary>
    public const int FaceCells = 128;

    /// <summary>How many cells (possible stars) the whole sky has.</summary>
    public const int CellCount = 6 * FaceCells * FaceCells;

    // The chance of a star in a cell of average size: about 5,000 stars in all.
    private const double AverageChance = 0.05;

    // A star sits this far or more from its cell's edges (as a share of the cell), so a star's
    // glow never reaches the next cell and drawing it needs only its own cell.
    private const double EdgeMargin = 0.2;

    /// <summary>A star of the field.</summary>
    /// <param name="Id">Its id: the cell it's in.</param>
    /// <param name="Direction">Which way it is (a unit vector in the system's frame).</param>
    /// <param name="Brightness">How bright it looks, from about 0.08 (faint) to 1.</param>
    /// <param name="Temperature">Its color: 0 red, through 0.5 white, to 1 blue.</param>
    public readonly record struct Star(
        int Id, Vector3D Direction, double Brightness, double Temperature);

    /// <summary>A seed for a world that has none yet: the same every time for that world.</summary>
    public static int SeedFor(Guid worldId) =>
        BitConverter.ToInt32(worldId.ToByteArray(), 0) & int.MaxValue;

    /// <summary>A fresh seed, for a new world or a re-roll.</summary>
    public static int NewSeed() => Random.Shared.Next();

    /// <summary>Every star on the sky from <paramref name="seed"/>, in id order.</summary>
    public static IReadOnlyList<Star> Stars(int seed)
    {
        var stars = new List<Star>(6000);
        for (int id = 0; id < CellCount; id++)
        {
            if (StarAt(seed, id) is Star star)
            {
                stars.Add(star);
            }
        }

        return stars;
    }

    /// <summary>Whether cell <paramref name="id"/> holds a star on the sky from the seed.</summary>
    public static bool HasStar(int seed, int id) => StarAt(seed, id) is not null;

    /// <summary>
    /// The star in cell <paramref name="id"/>, or null if it's empty (or isn't a cell).
    /// </summary>
    public static Star? StarAt(int seed, int id)
    {
        if (id is < 0 or >= CellCount)
        {
            return null;
        }

        (int face, int x, int y) = Split(id);
        double a = (x + 0.5) / FaceCells * 2 - 1;
        double b = (y + 0.5) / FaceCells * 2 - 1;
        var random = new CellRandom(seed, id);
        if (random.Next() >= AverageChance * RelativeCellSize(a, b))
        {
            return null;
        }

        double across = EdgeMargin + (1 - 2 * EdgeMargin) * random.Next();
        double down = EdgeMargin + (1 - 2 * EdgeMargin) * random.Next();
        double brightness = 0.08 + 0.92 * Math.Pow(random.Next(), 7);
        double temperature = 0.5 + 0.5 * (random.Next() - random.Next());
        Vector3D direction = DirectionOf(face,
            (x + across) / FaceCells * 2 - 1, (y + down) / FaceCells * 2 - 1);
        return new Star(id, direction, brightness, temperature);
    }

    /// <summary>
    /// The cell a direction falls in, which a star there would have as its id. The direction
    /// needn't be a unit vector, but mustn't be zero.
    /// </summary>
    public static int CellOf(Vector3D direction)
    {
        (int face, double a, double b) = FaceOf(direction);
        int x = Math.Clamp((int)Math.Floor((a + 1) / 2 * FaceCells), 0, FaceCells - 1);
        int y = Math.Clamp((int)Math.Floor((b + 1) / 2 * FaceCells), 0, FaceCells - 1);
        return (face * FaceCells + y) * FaceCells + x;
    }

    /// <summary>
    /// Which cube face a direction meets and where on it (−1 to 1 each way): the faces are
    /// +X, −X, +Y, −Y, +Z, −Z in that order (see <see cref="DirectionOf"/>).
    /// </summary>
    public static (int Face, double A, double B) FaceOf(Vector3D d)
    {
        double ax = Math.Abs(d.X), ay = Math.Abs(d.Y), az = Math.Abs(d.Z);
        if (ax >= ay && ax >= az)
        {
            return d.X > 0 ? (0, -d.Z / ax, d.Y / ax) : (1, d.Z / ax, d.Y / ax);
        }

        if (ay >= az)
        {
            return d.Y > 0 ? (2, d.X / ay, -d.Z / ay) : (3, d.X / ay, d.Z / ay);
        }

        return d.Z > 0 ? (4, d.X / az, d.Y / az) : (5, -d.X / az, d.Y / az);
    }

    /// <summary>
    /// The unit direction through point (a, b) of cube face <paramref name="face"/>.
    /// </summary>
    public static Vector3D DirectionOf(int face, double a, double b)
    {
        Vector3D d = face switch
        {
            0 => new Vector3D(1, b, -a),
            1 => new Vector3D(-1, b, a),
            2 => new Vector3D(a, 1, -b),
            3 => new Vector3D(a, -1, b),
            4 => new Vector3D(a, b, 1),
            _ => new Vector3D(-a, b, -1),
        };
        return d * (1 / d.Length);
    }

    /// <summary>
    /// A star's color, from red (temperature 0) through white (0.5) to blue (1).
    /// </summary>
    public static RgbColor ColorOf(double temperature)
    {
        double t = Math.Clamp(temperature, 0, 1);
        (double r, double g, double b) = t < 0.5
            ? (1.0, 0.72 + 0.28 * (t * 2), 0.5 + 0.5 * (t * 2))
            : (1.0 - 0.35 * (t * 2 - 1), 1.0 - 0.15 * (t * 2 - 1), 1.0);
        return new RgbColor((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    /// <summary>
    /// The star field as cube-face cells for drawing: six faces side by side, each
    /// <see cref="FaceCells"/> square, four bytes a cell: where the star is across and down its
    /// cell (0–255), its brightness (0 for no star), and its temperature.
    /// </summary>
    public static byte[] CellImage(int seed)
    {
        int width = 6 * FaceCells;
        var pixels = new byte[width * FaceCells * 4];
        foreach (Star star in Stars(seed))
        {
            (int face, int x, int y) = Split(star.Id);
            (_, double a, double b) = FaceOf(star.Direction);
            double across = (a + 1) / 2 * FaceCells - x;
            double down = (b + 1) / 2 * FaceCells - y;
            int at = (y * width + face * FaceCells + x) * 4;
            pixels[at] = ToByte(across);
            pixels[at + 1] = ToByte(down);
            pixels[at + 2] = (byte)Math.Max(1, Math.Round(star.Brightness * 255));
            pixels[at + 3] = ToByte(star.Temperature);
        }

        return pixels;
    }

    private static byte ToByte(double share) => (byte)Math.Clamp(Math.Round(share * 255), 0, 255);

    private static (int Face, int X, int Y) Split(int id) =>
        (id / (FaceCells * FaceCells), id % FaceCells, id / FaceCells % FaceCells);

    // A cell's share of the sky compared with an average cell's (from about 0.37 at a face's
    // corners to 1.9 in its middle).
    private static double RelativeCellSize(double a, double b) =>
        6 / (Math.PI * Math.Pow(1 + a * a + b * b, 1.5));

    // Numbers from 0 to 1 that depend only on the seed and the cell (SplitMix64).
    private struct CellRandom(int seed, int cell)
    {
        private ulong _state = ((ulong)(uint)seed << 32) | (uint)cell;

        public double Next()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / (1UL << 53));
        }
    }
}
