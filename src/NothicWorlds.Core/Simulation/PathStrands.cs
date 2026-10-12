using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// All the dirt paths' worn strands on a body (VISION.md BOD-13, <see cref="DirtPath"/>): where
/// they are, so plants keep off them, and their worn ribbons near a first-person eye. A strand
/// lies on the ground as it's drawn (it doesn't shape it), its ribbon a little over it (over
/// the coarser ground's flat triangles too, which can lie above the ground between their
/// corners), so it needs no strip down its sides; past <see cref="RibbonReachMeters"/> it's
/// too narrow to see.
/// Its search tree holds every strand's segments, so finding what's near a point takes about
/// as long however many paths there are. Safe to read from several threads at once.
/// </summary>
public sealed class PathStrands
{
    /// <summary>How far from the eye the strands' ribbons are drawn, at most, in meters.</summary>
    public const double RibbonReachMeters = 800;

    // Where a ribbon's points are across it, as shares of its half-width.
    private static readonly double[] _across = [-1, -0.5, 0, 0.5, 1];

    // How far apart a ribbon's rows are along it: at least this, or this share of their
    // distance from the eye, in meters.
    private const double MinRowMeters = 1, RowShare = 0.015;

    // How far a ribbon lies over the ground: this, plus this share of its distance from the
    // eye, in meters (less than a highway's paving, so paving shows over it), and a little
    // more for each strand after the main one, so where they meet one's drawn over the other.
    private const double MinLiftMeters = 0.012, LiftShare = 0.0006, LiftPerStrand = 0.08;

    // How many segments (or groups) each group of the search tree holds.
    private const int GroupSize = 8;

    private readonly double _radiusMeters;
    private readonly (int Strand, int Segment)[] _segments;  // In the tree's order
    private readonly Level[] _tree;                         // Its segments' level first

    private PathStrands(IReadOnlyList<PathStrand> strands, double radiusMeters)
    {
        (Strands, _radiusMeters) = (strands, radiusMeters);
        _segments = [.. strands.SelectMany((strand, s) => Enumerable
            .Range(0, Math.Max(0, strand.Points.Count - 1)).Select(i => (s, i)))];
        var centers = new Vector3D[_segments.Length];
        var radii = new double[_segments.Length];
        for (int k = 0; k < _segments.Length; k++)
        {
            (int s, int i) = _segments[k];
            Vector3D from = strands[s].Points[i], to = strands[s].Points[i + 1];
            centers[k] = (from + to) * 0.5;
            radii[k] = (to - from).Length / 2 + strands[s].HalfWidthMeters / radiusMeters;
        }

        List<Level> levels = [new Level(centers, radii)];
        while (levels[^1].Centers.Length > 1)
        {
            levels.Add(Grouped(levels[^1]));
        }

        _tree = [.. levels];
    }

    /// <summary>The strands.</summary>
    public IReadOnlyList<PathStrand> Strands { get; }

    /// <summary>Whether there are no strands.</summary>
    public bool IsEmpty => _segments.Length == 0;

    /// <summary>The strands of all of <paramref name="paths"/>, on a body this big.</summary>
    public static PathStrands For(IEnumerable<DirtPath> paths, double radiusKm) =>
        new([.. paths.SelectMany(path => path.Strands)], radiusKm * 1000);

    /// <summary>
    /// Whether a unit direction is on a strand's worn ground, or within
    /// <paramref name="marginMeters"/> of it.
    /// </summary>
    public bool OnPath(Vector3D direction, double marginMeters)
    {
        if (_segments.Length == 0)
        {
            return false;
        }

        return Visit(_tree.Length - 1, 0, direction, marginMeters / _radiusMeters, marginMeters);
    }

    /// <summary>
    /// The worn ribbons of the strands within <paramref name="reachMeters"/> (at most
    /// <see cref="RibbonReachMeters"/>) of <paramref name="eye"/> (a unit direction), as strips
    /// of rows across them: <paramref name="carve"/> gives the ground's height as it's shaped at
    /// a direction from its height there, <paramref name="groundMeters"/> that height, and
    /// <paramref name="coarseMeters"/> the coarser drawn ground's. Worked out on one thread
    /// (it's meant for a worker).
    /// </summary>
    public List<List<BankPoint[]>> RibbonsNear(Func<Vector3D, double, double> carve,
        Func<Vector3D, double> groundMeters, Func<Vector3D, double> coarseMeters, Vector3D eye,
        double reachMeters)
    {
        double reach = Math.Min(reachMeters, RibbonReachMeters);
        var strips = new List<List<BankPoint[]>>();
        foreach (PathStrand strand in Strands.Where(strand => strand.Worn))
        {
            List<BankPoint[]>? strip = null;
            double lastRow = double.NegativeInfinity, along = 0;
            for (int i = 0; i + 1 < strand.Points.Count; i++)
            {
                Vector3D from = strand.Points[i], to = strand.Points[i + 1];
                double length = Math.Acos(Math.Clamp(from.Dot(to), -1, 1)) * _radiusMeters;
                if ((eye - from).Length * _radiusMeters - length > reach)
                {
                    strip = End(strips, strip);
                    along += length;
                    continue;
                }

                Vector3D side = from.Cross(to - from);
                side *= 1 / Math.Max(side.Length, 1e-12);
                for (double at = 0; ;)
                {
                    double t = length > 0 ? Math.Min(at / length, 1) : 1;
                    Vector3D direction = from * (1 - t) + to * t;
                    direction *= 1 / direction.Length;
                    double fromEye = _radiusMeters
                        * Math.Acos(Math.Clamp(direction.Dot(eye), -1, 1));
                    double spacing = Math.Max(MinRowMeters, RowShare * fromEye);
                    bool last = t >= 1 && i + 2 == strand.Points.Count;
                    if (fromEye > reach)
                    {
                        strip = End(strips, strip);
                    }
                    else if (strip is null || along + at - lastRow >= spacing || last)
                    {
                        (strip ??= []).Add(Row(strand, direction, side, fromEye, carve,
                            groundMeters, coarseMeters));
                        lastRow = along + at;
                    }

                    if (t >= 1)
                    {
                        break;
                    }

                    at += Math.Min(spacing, length);
                }

                along += length;
            }

            End(strips, strip);
        }

        return strips;
    }

    // Keeps a strip if it has at least two rows; null, to start the next.
    private static List<BankPoint[]>? End(List<List<BankPoint[]>> strips,
        List<BankPoint[]>? strip)
    {
        if (strip is { Count: > 1 })
        {
            strips.Add(strip);
        }

        return null;
    }

    // A row across a strand's ribbon at a point on it, over the ground as it's shaped or as
    // the coarser ground's drawn, whichever is higher.
    private BankPoint[] Row(PathStrand strand, Vector3D direction, Vector3D side,
        double fromEye, Func<Vector3D, double, double> carve,
        Func<Vector3D, double> groundMeters, Func<Vector3D, double> coarseMeters)
    {
        double lift = (MinLiftMeters + LiftShare * fromEye) * (1 + LiftPerStrand * strand.Index);
        var row = new BankPoint[_across.Length];
        for (int k = 0; k < _across.Length; k++)
        {
            double share = _across[k];
            Vector3D at = direction + side * (share * strand.HalfWidthMeters / _radiusMeters);
            at *= 1 / at.Length;
            double ground = Math.Max(carve(at, groundMeters(at)), coarseMeters(at));
            row[k] = new BankPoint(at, ground + lift, 1,
                strand.Rut ? StripSurface.Rut : StripSurface.Worn, 1 - Math.Abs(share));
        }

        return row;
    }

    // Whether any segment under a group of the tree comes within `margin` radii (and
    // `marginMeters` meters) past its worn edge of a unit direction.
    private bool Visit(int level, int index, Vector3D point, double margin, double marginMeters)
    {
        Level nodes = _tree[level];
        if ((point - nodes.Centers[index]).Length - nodes.Radii[index] > margin)
        {
            return false;
        }

        if (level == 0)
        {
            (int s, int i) = _segments[index];
            PathStrand strand = Strands[s];
            Vector3D from = strand.Points[i], along = strand.Points[i + 1] - from;
            double lengthSquared = along.Dot(along);
            double t = lengthSquared > 0
                ? Math.Clamp((point - from).Dot(along) / lengthSquared, 0, 1)
                : 0;
            double away = (point - (from + along * t)).Length * _radiusMeters;
            return away <= strand.HalfWidthMeters + marginMeters;
        }

        int end = Math.Min((index + 1) * GroupSize, _tree[level - 1].Centers.Length);
        for (int child = index * GroupSize; child < end; child++)
        {
            if (Visit(level - 1, child, point, margin, marginMeters))
            {
                return true;
            }
        }

        return false;
    }

    // The level above: a ball around each group of the level's balls.
    private static Level Grouped(Level below)
    {
        int count = (below.Centers.Length + GroupSize - 1) / GroupSize;
        var centers = new Vector3D[count];
        var radii = new double[count];
        for (int g = 0; g < count; g++)
        {
            int start = g * GroupSize, end = Math.Min(start + GroupSize, below.Centers.Length);
            Vector3D sum = Vector3D.Zero;
            for (int i = start; i < end; i++)
            {
                sum += below.Centers[i];
            }

            centers[g] = sum * (1.0 / (end - start));
            for (int i = start; i < end; i++)
            {
                radii[g] = Math.Max(radii[g],
                    (below.Centers[i] - centers[g]).Length + below.Radii[i]);
            }
        }

        return new Level(centers, radii);
    }

    // One level of the search tree: each ball's middle and radius (in radii), the strands'
    // worn width inside it included.
    private sealed record Level(Vector3D[] Centers, double[] Radii);
}
