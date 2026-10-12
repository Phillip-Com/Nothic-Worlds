using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The fine strips along the roads laid up close near a first-person eye (VISION.md BOD-13), as
/// <see cref="RiverBanks"/> are along rivers: for each stretch in reach, its paving (marked so
/// the ground's material paves it; on a dirt path's crossing, worn ground) and a strip down
/// each side, over its shoulder and its
/// cutting's or bank's slope (as <see cref="GroundCarving"/> shapes them, rivers too) to a
/// skirt that blends into the coarser ground; the bridges in reach
/// (<see cref="StoneBridge"/>, <see cref="WoodenBridge"/>); and the tunnels' vaults and portals
/// in reach (<see cref="StoneTunnel"/>). Rows run across, from one skirt's
/// edge to the other, in the order <see cref="RiverBanks"/>' rows do. Worked out on one thread
/// (it's meant for a worker).
/// </summary>
public static class RoadStrips
{
    // Where the paving's points are across, as shares of its half-width.
    private static readonly double[] _paving = [-1, -0.5, 0, 0.5, 1];

    // Where a side's points are past its shoulder, as shares of the rest of its reach...
    private static readonly double[] _side = [0.15, 0.3, 0.5, 0.75, 1];

    // ...and the skirt's, as shares of its width past that.
    private static readonly double[] _skirt = [0.5, 1];

    // How far apart the rows are along the road: at least this, or this share of their
    // distance from the eye, in meters (as RiverBanks').
    private const double MinRowMeters = 2, RowShare = 0.015;

    // How far the paving lies over the ground it's on, so where roads meet another's sides
    // never show through it: this, plus this share of its distance from the eye, in meters.
    private const double MinPavingLiftMeters = 0.02, PavingLiftShare = 0.001;

    // How far the skirt's edge lies over the coarser ground, as RiverBanks'.
    private const double LiftShare = 0.005, MinLiftMeters = 0.05;

    /// <summary>
    /// The strips, bridges (with the tunnels' pieces), and tunnels' portals of
    /// <paramref name="roads"/> within
    /// <paramref name="reachMeters"/> of <paramref name="eye"/> (a unit direction) on a body
    /// <paramref name="radiusKm"/> in radius: <paramref name="carve"/> gives the ground's
    /// shaped height at a direction from its height there, <paramref name="groundMeters"/>
    /// that height, <paramref name="coarseMeters"/> the coarser drawn ground's (where the
    /// skirts end), and <paramref name="skirtMeters"/> how wide each skirt must be there.
    /// </summary>
    public static (List<List<BankPoint[]>> Strips, List<BridgeShown> Bridges,
        List<TunnelPortal> Portals) Near(RoadCarving roads,
        Func<Vector3D, double, double> carve, Vector3D eye, double radiusKm, double reachMeters,
        Func<Vector3D, double> groundMeters,
        Func<Vector3D, double> coarseMeters, Func<Vector3D, double> skirtMeters)
    {
        double radiusMeters = radiusKm * 1000;
        var strips = new List<List<BankPoint[]>>();
        var bridges = new List<BridgeShown>();
        var portals = new List<TunnelPortal>();
        for (int r = 0; r < roads.Roads.Count; r++)
        {
            RoadProfile road = roads.Roads[r];
            foreach (List<Row> stretch in Stretches(roads, r, eye, radiusMeters, reachMeters))
            {
                if (stretch.Count < 2)
                {
                    continue;
                }

                var left = new List<BankPoint[]>();
                var paving = new List<BankPoint[]>();
                var right = new List<BankPoint[]>();
                foreach (Row row in stretch)
                {
                    (BankPoint[] l, BankPoint[] p, BankPoint[] rr) = Across(row, road, carve,
                        radiusMeters, groundMeters, coarseMeters, skirtMeters);
                    left.Add(l);
                    paving.Add(p);
                    right.Add(rr);
                }

                strips.AddRange([left, paving, right]);
            }

            foreach (RoadBridge bridge in road.Bridges)
            {
                if (InReach(road, bridge, eye, radiusMeters, reachMeters))
                {
                    bridges.Add(BridgeShown.Of(road, bridge, radiusMeters));
                }
            }

            foreach (RoadTunnel tunnel in road.Tunnels)
            {
                TunnelNear(road, tunnel, eye, radiusMeters, reachMeters, bridges, portals);
            }
        }

        return (strips, bridges, portals);
    }

    // The pieces of a tunnel's vault in reach (one for each of its segments), and its portals
    // in reach, each with its front and block.
    private static void TunnelNear(RoadProfile road, RoadTunnel tunnel, Vector3D eye,
        double radiusMeters, double reachMeters, List<BridgeShown> bridges,
        List<TunnelPortal> portals)
    {
        double inner = road.HalfWidthMeters + road.ShoulderMeters;
        bool Near(int i) => radiusMeters * Math.Acos(Math.Clamp(road.Points[i].Dot(eye), -1, 1))
            <= reachMeters;
        for (int s = tunnel.First; s < tunnel.Last; s++)
        {
            if (!Near(s) && !Near(s + 1))
            {
                continue;
            }

            double length = road.AlongMeters[s + 1] - road.AlongMeters[s];
            (List<BridgePoint> points, List<int> triangles) = StoneTunnel.Vault(length, inner,
                road.RoadMeters[s], road.RoadMeters[s + 1],
                road.AlongMeters[s] - road.AlongMeters[tunnel.First], tunnel.LengthMeters);
            bridges.Add(BridgeShown.Between(road.Points[s], road.Points[s + 1], length,
                radiusMeters, points, triangles));
        }

        foreach ((int at, int inward) in new[] { (tunnel.First, 1), (tunnel.Last, -1) })
        {
            if (!Near(at))
            {
                continue;
            }

            double depth = road.GroundMeters[at] - road.RoadMeters[at];
            (List<BridgePoint> points, List<int> triangles) = StoneTunnel.Portal(inner,
                road.RoadMeters[at], depth);
            BridgeShown shown = BridgeShown.Between(road.Points[at], road.Points[at + inward],
                Math.Abs(road.AlongMeters[at + inward] - road.AlongMeters[at]), radiusMeters,
                points, triangles);
            bridges.Add(shown);
            portals.Add(new TunnelPortal(shown,
                StoneTunnel.HiddenHalfWidthMeters(inner, depth)));
        }
    }

    // The rows of a road in reach, in stretches broken where it's out of reach, on a bridge,
    // or in a tunnel.
    private static List<List<Row>> Stretches(RoadCarving roads, int r, Vector3D eye,
        double radiusMeters, double reachMeters)
    {
        RoadProfile road = roads.Roads[r];
        var stretches = new List<List<Row>>();
        List<Row>? stretch = null;
        double lastRow = double.NegativeInfinity;
        for (int s = 0; s < road.Points.Count - 1; s++)
        {
            Vector3D from = road.Points[s], to = road.Points[s + 1];
            double length = road.AlongMeters[s + 1] - road.AlongMeters[s];
            if (roads.IsOffGround(r, s)
                || (eye - from).Length * radiusMeters - length > reachMeters)
            {
                stretch = null;
                continue;
            }

            double reach = roads.SegmentReachMeters(r, s);
            for (double at = 0; ;)
            {
                double t = length > 0 ? Math.Min(at / length, 1) : 1;
                Vector3D direction = from * (1 - t) + to * t;
                direction *= 1 / direction.Length;
                double fromEye = radiusMeters
                    * Math.Acos(Math.Clamp(direction.Dot(eye), -1, 1));
                double spacing = Math.Max(MinRowMeters, RowShare * fromEye);
                double here = road.AlongMeters[s] + at;
                bool last = t >= 1 && (s + 1 >= road.Points.Count - 1
                    || roads.IsOffGround(r, s + 1));
                if (fromEye > reachMeters)
                {
                    stretch = null;
                }
                else if (stretch is null || here - lastRow >= spacing || last)
                {
                    if (stretch is null)
                    {
                        stretch = [];
                        stretches.Add(stretch);
                    }

                    stretch.Add(new Row(direction, Side(from, to, direction), fromEye, reach));
                    lastRow = here;
                }

                if (t >= 1)
                {
                    break;
                }

                at += Math.Min(spacing, length);
            }
        }

        return stretches;
    }

    // The points across a row: one side (outer edge first), its paving, and the other side
    // (inner edge first).
    private static (BankPoint[] Left, BankPoint[] Paving, BankPoint[] Right) Across(Row row,
        RoadProfile road, Func<Vector3D, double, double> carve, double radiusMeters,
        Func<Vector3D, double> groundMeters, Func<Vector3D, double> coarseMeters,
        Func<Vector3D, double> skirtMeters)
    {
        double halfWidth = road.HalfWidthMeters;
        double shoulder = halfWidth + road.ShoulderMeters;
        double reach = Math.Max(row.ReachMeters, shoulder + 1);
        double skirt = skirtMeters(row.Direction);
        double lift = MinLiftMeters + LiftShare * row.FromEye;
        double pavingLift = MinPavingLiftMeters + PavingLiftShare * row.FromEye;

        // One side's offsets out from the paving's edge: the share of the skirt, and the lift.
        List<(double Across, double Skirt, double Lift)> side =
            [(halfWidth, 0, pavingLift), (shoulder, 0, 0)];
        side.AddRange(_side.Select(share => (shoulder + share * (reach - shoulder), 0.0, 0.0)));
        side.AddRange(_skirt.Select(share => (reach + share * skirt, share, 0.0)));

        StripSurface surface = road.Surface == RoadSurface.Dirt
            ? StripSurface.Worn
            : StripSurface.Paving;
        BankPoint At(double across, double skirtShare, double raise, StripSurface surface)
        {
            Vector3D direction = row.Direction + row.Side * (across / radiusMeters);
            direction *= 1 / direction.Length;
            double shaped = carve(direction, groundMeters(direction)) + raise;
            double meters = skirtShare == 0
                ? shaped
                : shaped + (coarseMeters(direction) + lift - shaped) * skirtShare;
            return new BankPoint(direction, meters, 1 - skirtShare, surface);
        }

        BankPoint[] right = [.. side.Select(o =>
            At(o.Across, o.Skirt, o.Lift, StripSurface.Ground))];
        BankPoint[] left = [.. Enumerable.Reverse(side)
            .Select(o => At(-o.Across, o.Skirt, o.Lift, StripSurface.Ground))];
        BankPoint[] paving = [.. _paving.Select(share =>
            At(share * halfWidth, 0, pavingLift, surface) with
            {
                Wear = surface == StripSurface.Worn ? 1 - Math.Abs(share) : 0,
            })];
        return (left, paving, right);
    }

    // Whether any of a bridge is within reach of the eye.
    private static bool InReach(RoadProfile road, RoadBridge bridge, Vector3D eye,
        double radiusMeters, double reachMeters)
    {
        for (int i = bridge.First; i <= bridge.Last; i++)
        {
            if (radiusMeters * Math.Acos(Math.Clamp(road.Points[i].Dot(eye), -1, 1))
                <= reachMeters)
            {
                return true;
            }
        }

        return false;
    }

    // The unit direction across a road at a point on the segment between two others (to its
    // left, looking along it).
    private static Vector3D Side(Vector3D from, Vector3D to, Vector3D at)
    {
        Vector3D side = at.Cross(to - from);
        return side * (1 / Math.Max(side.Length, 1e-12));
    }

    // A row across a road: where it is, the way across it, how far it is from the eye (in
    // meters), and how far the road's sides reach there (in meters).
    private readonly record struct Row(Vector3D Direction, Vector3D Side, double FromEye,
        double ReachMeters);
}
