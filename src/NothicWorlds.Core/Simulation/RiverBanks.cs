using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The fine strip of ground along the rivers near a first-person eye (VISION.md BOD-11): the
/// bed and banks of each carved stretch (<see cref="RiverChannels"/>), drawn more finely than
/// the coarser ground around the eye could, with a skirt beyond each bank that blends into
/// that coarser ground, so its cells' flat faces never show along a river. Each row runs
/// across the river from one skirt's edge to the other.
/// </summary>
public static class RiverBanks
{
    // Where the rows' points are across the river (one side; mirrored for the other): the
    // bed's middle, halfway out, and its edge, as shares of the half-width...
    private static readonly double[] _bed = [0, 0.5, 1];

    // ...the bank, as shares of its width past the bed...
    private static readonly double[] _bank = [1 / 6.0, 2 / 6.0, 3 / 6.0, 4 / 6.0, 5 / 6.0, 1];

    // ...and the skirt, as shares of its width past the bank.
    private static readonly double[] _skirt = [0.5, 1];

    // How far apart the rows are along the river: at least this, or this share of their
    // distance from the eye, in meters (the bed and banks change slowly along it).
    private const double MinRowMeters = 2, RowShare = 0.015;

    // How far the skirt's edge lies over the coarser ground, so they don't flicker against
    // each other: this share of its distance from the eye plus this, in meters.
    private const double LiftShare = 0.005, MinLiftMeters = 0.05;

    /// <summary>
    /// The strips along the carved stretches of <paramref name="channels"/>, around
    /// <paramref name="eye"/> on a body <paramref name="radiusKm"/> in radius:
    /// <paramref name="groundMeters"/> gives the ground's height at a direction and
    /// <paramref name="coarseMeters"/> the coarser drawn ground's (where the skirts end).
    /// </summary>
    public static List<List<BankPoint[]>> Strips(RiverChannels channels, Vector3D eye,
        double radiusKm, Func<Vector3D, double> groundMeters, Func<Vector3D, double> coarseMeters)
    {
        double radiusMeters = radiusKm * 1000;

        // Which steps get a row, strip by strip; the rows are worked out after, all at once
        // (each is independent, and there are thousands of points to measure).
        var strips = new List<List<int>>();
        var stretchOf = new List<IReadOnlyList<ChannelPoint>>();
        foreach (IReadOnlyList<ChannelPoint> stretch in channels.Stretches)
        {
            List<int>? strip = null;
            double lastRow = double.NegativeInfinity;
            for (int i = 0; i < stretch.Count; i++)
            {
                // A step is in the strip if it or a step beside it is carved.
                bool carved = stretch[Math.Max(i - 1, 0)].Carved > 0 || stretch[i].Carved > 0
                    || stretch[Math.Min(i + 1, stretch.Count - 1)].Carved > 0;
                if (!carved)
                {
                    strip = null;
                    continue;
                }

                double fromEye = radiusMeters
                    * Math.Acos(Math.Clamp(stretch[i].Direction.Dot(eye), -1, 1));
                bool last = i == stretch.Count - 1 || stretch[i + 1].Carved <= 0;
                if (strip is not null && !last && stretch[i].AlongMeters - lastRow
                    < Math.Max(MinRowMeters, RowShare * fromEye))
                {
                    continue;
                }

                if (strip is null)
                {
                    strip = [];
                    strips.Add(strip);
                    stretchOf.Add(stretch);
                }

                lastRow = stretch[i].AlongMeters;
                strip.Add(i);
            }
        }

        var rows = new List<(int Strip, int Step)>();
        for (int s = 0; s < strips.Count; s++)
        {
            if (strips[s].Count >= 2)
            {
                rows.AddRange(strips[s].Select(step => (s, step)));
            }
        }

        var built = new BankPoint[rows.Count][];
        Parallel.For(0, rows.Count, r => built[r] = Row(channels, stretchOf[rows[r].Strip],
            rows[r].Step, eye, radiusMeters, groundMeters, coarseMeters));
        return [.. rows.Select((row, r) => (row.Strip, Row: built[r]))
            .GroupBy(row => row.Strip)
            .Select(group => group.Select(row => row.Row).ToList())];
    }

    // The row across the river at step i of a stretch.
    private static BankPoint[] Row(RiverChannels channels, IReadOnlyList<ChannelPoint> stretch,
        int i, Vector3D eye, double radiusMeters, Func<Vector3D, double> groundMeters,
        Func<Vector3D, double> coarseMeters)
    {
        ChannelPoint point = stretch[i];
        Vector3D along = stretch[Math.Min(i + 1, stretch.Count - 1)].Direction
            - stretch[Math.Max(i - 1, 0)].Direction;
        Vector3D side = Cross(point.Direction, along);
        side *= 1 / Math.Max(side.Length, 1e-12);
        double fromEye = radiusMeters * Math.Acos(Math.Clamp(point.Direction.Dot(eye), -1, 1));
        double halfWidth = point.HalfWidthMeters;
        double bankTop = halfWidth + RiverChannels.BankWidthMeters(halfWidth);
        double skirt = RiverChannels.SkirtMeters(fromEye);
        double lift = MinLiftMeters + LiftShare * fromEye;

        // The offsets across, from one skirt's edge to the other.
        List<(double Across, double Skirt)> offsets = [];
        offsets.AddRange(_bed.Select(share => (share * halfWidth, 0.0)));
        offsets.AddRange(_bank.Select(share => (halfWidth + share * (bankTop - halfWidth), 0.0)));
        offsets.AddRange(_skirt.Select(share => (bankTop + share * skirt, share)));
        List<(double Across, double Skirt)> row =
            [.. offsets.Skip(1).Reverse().Select(o => (-o.Across, o.Skirt)), .. offsets];

        return [.. row.Select(offset =>
        {
            Vector3D direction = point.Direction + side * (offset.Across / radiusMeters);
            direction *= 1 / direction.Length;
            double carved = channels.Carve(direction, groundMeters(direction));
            double meters = offset.Skirt == 0
                ? carved
                : carved + (coarseMeters(direction) + lift - carved) * offset.Skirt;
            return new BankPoint(direction, meters, 1 - offset.Skirt);
        })];
    }

    private static Vector3D Cross(Vector3D a, Vector3D b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
