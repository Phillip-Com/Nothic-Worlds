using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The fine strip of ground along the rivers near a first-person eye (VISION.md BOD-11): the
/// bed and banks of each stretch in reach (<see cref="RiverChannels"/>), as
/// <see cref="RiverCarving"/> cuts them, drawn more finely than
/// the coarser ground around the eye could, with a skirt beyond each bank that blends into
/// that coarser ground, so its cells' flat faces never show along a river. Each row runs
/// across the river from one skirt's edge to the other. Worked out on one thread (it's meant
/// for a worker, where it shouldn't crowd out the drawing).
/// </summary>
public static class RiverBanks
{
    // Where the rows' points are across the river (one side; mirrored for the other): the
    // bed's middle, halfway out, and its edge, as shares of the half-width...
    private static readonly double[] _bed = [0, 0.5, 1];

    // ...the bank, as shares of its width past the bed (with a point where it reaches the
    // water, where its slope changes)...
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
    /// The strips along the stretches of <paramref name="channels"/>, cut by
    /// <paramref name="carving"/>, around <paramref name="eye"/> on a body
    /// <paramref name="radiusKm"/> in radius: <paramref name="groundMeters"/> gives the
    /// ground's height at a direction, <paramref name="coarseMeters"/> the coarser drawn
    /// ground's (where the skirts end), and <paramref name="skirtMeters"/> how wide the skirt
    /// beyond each bank must be there (past where the coarser ground is sunk under it).
    /// </summary>
    public static List<List<BankPoint[]>> Strips(RiverChannels channels, RiverCarving carving,
        Vector3D eye, double radiusKm, Func<Vector3D, double> groundMeters,
        Func<Vector3D, double> coarseMeters, Func<Vector3D, double> skirtMeters)
    {
        double radiusMeters = radiusKm * 1000;

        // Which steps get a row, strip by strip; the rows are worked out after.
        var strips = new List<List<int>>();
        var stretchOf = new List<IReadOnlyList<ChannelPoint>>();
        foreach (IReadOnlyList<ChannelPoint> stretch in channels.Stretches)
        {
            var strip = new List<int>();
            double lastRow = double.NegativeInfinity;
            for (int i = 0; i < stretch.Count; i++)
            {
                double fromEye = radiusMeters
                    * Math.Acos(Math.Clamp(stretch[i].Direction.Dot(eye), -1, 1));
                if (i > 0 && i < stretch.Count - 1 && stretch[i].AlongMeters - lastRow
                    < Math.Max(MinRowMeters, RowShare * fromEye))
                {
                    continue;
                }

                lastRow = stretch[i].AlongMeters;
                strip.Add(i);
            }

            strips.Add(strip);
            stretchOf.Add(stretch);
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
        for (int r = 0; r < rows.Count; r++)
        {
            built[r] = Row(carving, stretchOf[rows[r].Strip], rows[r].Step, eye, radiusMeters,
                groundMeters, coarseMeters, skirtMeters);
        }
        return [.. rows.Select((row, r) => (row.Strip, Row: built[r]))
            .GroupBy(row => row.Strip)
            .Select(group => group.Select(row => row.Row).ToList())];
    }

    // The row across the river at step i of a stretch.
    private static BankPoint[] Row(RiverCarving carving, IReadOnlyList<ChannelPoint> stretch,
        int i, Vector3D eye, double radiusMeters, Func<Vector3D, double> groundMeters,
        Func<Vector3D, double> coarseMeters, Func<Vector3D, double> skirtMeters)
    {
        ChannelPoint point = stretch[i];
        Vector3D along = stretch[Math.Min(i + 1, stretch.Count - 1)].Direction
            - stretch[Math.Max(i - 1, 0)].Direction;
        Vector3D side = point.Direction.Cross(along);
        side *= 1 / Math.Max(side.Length, 1e-12);
        double fromEye = radiusMeters * Math.Acos(Math.Clamp(point.Direction.Dot(eye), -1, 1));
        double halfWidth = point.HalfWidthMeters;
        double bankTop = halfWidth + RiverCarving.BankWidthMeters(halfWidth);
        double skirt = skirtMeters(point.Direction);
        double lift = MinLiftMeters + LiftShare * fromEye;

        // The offsets across, from one skirt's edge to the other.
        List<(double Across, double Skirt)> offsets = [];
        offsets.AddRange(_bed.Select(share => (share * halfWidth, 0.0)));
        offsets.AddRange(_bank.Select(share => (halfWidth + share * (bankTop - halfWidth), 0.0)));
        offsets.Add((Math.Clamp(point.WaterHalfWidthMeters, halfWidth, bankTop), 0.0));
        offsets.Sort((a, b) => a.Across.CompareTo(b.Across));
        offsets.AddRange(_skirt.Select(share => (bankTop + share * skirt, share)));
        List<(double Across, double Skirt)> row =
            [.. offsets.Skip(1).Reverse().Select(o => (-o.Across, o.Skirt)), .. offsets];

        return [.. row.Select(offset =>
        {
            Vector3D direction = point.Direction + side * (offset.Across / radiusMeters);
            direction *= 1 / direction.Length;
            double carved = carving.Carve(direction, groundMeters(direction));
            double meters = offset.Skirt == 0
                ? carved
                : carved + (coarseMeters(direction) + lift - carved) * offset.Skirt;
            return new BankPoint(direction, meters, 1 - offset.Skirt);
        })];
    }
}
