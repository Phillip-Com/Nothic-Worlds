using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// A dirt path up close (VISION.md BOD-13; owner's choices, 2026-10-11 and 2026-10-12): from
/// <see cref="MinStrands"/> to <see cref="MaxStrands"/> worn strands wandering, splitting, and
/// rejoining within its band, leaning toward its flatter side, that come together into one
/// where it crosses something and at its ends. Steeper than <see cref="MaxGrade"/>, the one
/// strand climbs in switchbacks reaching up to <see cref="MaxSwingMeters"/> either side. It
/// fords rivers whose water is at most <see cref="MaxFordWidthMeters"/> wide, crosses wider
/// ones on a wooden bridge, and crosses marsh on a boardwalk (<see cref="WoodenBridge"/>).
/// The strands lie on the ground as it is (<see cref="PathStrands"/> draws them); only the
/// crossings shape it, as short roads (<see cref="Crossings"/>, laid as highways are, through
/// <see cref="RoadCarving"/>). It depends only on the path, the ground, the rivers, and where
/// marsh is, never on where an eye is; the same path gives the same strands every time.
/// A cart track (owner's choices, 2026-10-12) is laid the same way, but as two ruts
/// <see cref="RutGaugeMeters"/> apart that never split, switchbacking above
/// <see cref="CartMaxGrade"/> up to <see cref="CartMaxSwingMeters"/> either side, its fords,
/// bridges, and boardwalks <see cref="CartHalfWidthMeters"/> to each side.
/// </summary>
public sealed class DirtPath
{
    /// <summary>The step between the points it's worked out at, in meters.</summary>
    public const double StepMeters = 5;

    /// <summary>The fewest and most strands at any place along it, away from crossings.</summary>
    public const int MinStrands = 3, MaxStrands = 7;

    /// <summary>The steepest a path climbs straight up a slope; steeper, it switchbacks.</summary>
    public const double MaxGrade = 0.15;

    /// <summary>What its switchbacks climb at, as near as the ground lets them.</summary>
    public const double SwitchbackGrade = 0.12;

    /// <summary>How far its switchbacks reach either side of its way, at most, in meters.</summary>
    public const double MaxSwingMeters = 40;

    /// <summary>The widest water it fords, in meters; wider, it crosses on a bridge.</summary>
    public const double MaxFordWidthMeters = 12;

    /// <summary>How deep the water is where it fords, in meters.</summary>
    public const double FordDepthMeters = 0.4;

    /// <summary>
    /// How far before a crossing or an end the strands start coming together, in meters.
    /// </summary>
    public const double GatherMeters = 40;

    /// <summary>
    /// How far a crossing's shaped stretch (<see cref="Crossings"/>) reaches past the water or
    /// marsh, in meters, to bring the path down to a ford or up to a bridge.
    /// </summary>
    public const double ApproachMeters = 30;

    /// <summary>Half the width of the main strand and of the others, in meters.</summary>
    public const double TrunkHalfWidthMeters = 1, StrandHalfWidthMeters = 0.7;

    /// <summary>Half the width of a wooden bridge's deck and a boardwalk's, in meters.</summary>
    public const double BridgeHalfWidthMeters = 1.5, BoardwalkHalfWidthMeters = 0.75;

    /// <summary>
    /// The steepest a cart track climbs straight up a slope (steeper, it switchbacks), and what
    /// its switchbacks climb at.
    /// </summary>
    public const double CartMaxGrade = 0.10, CartSwitchbackGrade = 0.08;

    /// <summary>
    /// How far a cart track's switchbacks reach either side of its way, in meters.
    /// </summary>
    public const double CartMaxSwingMeters = 60;

    /// <summary>
    /// Half the width of a cart track where it crosses something (its ford's or bridge's road,
    /// and its boardwalk), in meters.
    /// </summary>
    public const double CartHalfWidthMeters = 1.5;

    /// <summary>How far apart a cart track's ruts are, middle to middle, in meters.</summary>
    public const double RutGaugeMeters = 1.5;

    /// <summary>Half the width of each of a cart track's ruts, in meters.</summary>
    public const double RutHalfWidthMeters = 0.3;

    /// <summary>The level ground either side of a crossing's path, in meters.</summary>
    public const double ShoulderMeters = 0.3;

    // How far beside its way a river is looked for, in meters.
    private const double WaterMarginMeters = 2;

    // The shortest marsh crossed on a boardwalk, and the shortest steep stretch climbed in
    // switchbacks, in meters; and gaps between them shorter than these are bridged over.
    private const double LeastMarshMeters = 15, LeastSteepMeters = 20;
    private const double MarshGapMeters = 20, SteepGapMeters = 30;

    // How far either side the ground is averaged to find where it's steep, and how far past a
    // steep stretch its switchbacks start, in meters.
    private const double SteepWindowMeters = 15, SteepPaddingMeters = 10;

    // How many steps each switchback's corner is rounded off in.
    private const int HairpinSteps = 6;

    // What a dirt path and a cart track can climb and how they're laid where they cross things
    // (how far either side of each switchback's corner it's rounded off, in meters).
    private static readonly Limits _pathLimits = new(MaxGrade, SwitchbackGrade, MaxSwingMeters,
        4, TrunkHalfWidthMeters, BoardwalkHalfWidthMeters);
    private static readonly Limits _cartLimits = new(CartMaxGrade, CartSwitchbackGrade,
        CartMaxSwingMeters, 8, CartHalfWidthMeters, CartHalfWidthMeters);

    // How far the strands wander from their lanes (as a share of the band's half-width), over
    // about how long a stretch, in meters; and over how long each extra strand comes and goes.
    private const double WanderShare = 0.35, WanderMeters = 60, ComeAndGoMeters = 150;

    // How hard the strands lean toward the flatter side of the band (a share of its
    // half-width), the ground averaged over this far either side, in meters.
    private const double LeanShare = 0.4, LeanWindowMeters = 30;

    // How far into the band its steepness is felt either side, as a share of its half-width.
    private const double LeanProbeShare = 0.7;

    // Below this much of its spread, a strand has joined the main one and isn't drawn.
    private const double JoinedShare = 0.05;

    // Each strand's lane across the band (a share of its half-width): the main strand and two
    // others always; up to four more come and go, each rejoining the one named beside it.
    private static readonly double[] _lanes = [0, -0.55, 0.55, -0.3, 0.3, -0.8, 0.8];
    private static readonly int[] _parents = [0, 0, 0, 1, 2, 1, 2];

    private DirtPath(List<PathStrand> strands, List<RoadProfile> crossings)
    {
        (Strands, Crossings) = (strands, crossings);
    }

    /// <summary>
    /// The strands to draw: pieces of each, as they come and go, the main one (index 0) broken
    /// where it's on a crossing.
    /// </summary>
    public IReadOnlyList<PathStrand> Strands { get; }

    /// <summary>
    /// Where the path crosses water or marsh: short roads of worn ground carrying its fords,
    /// wooden bridges, and boardwalks, which shape the ground as highways do.
    /// </summary>
    public IReadOnlyList<RoadProfile> Crossings { get; }

    /// <summary>
    /// The path <paramref name="course"/> up close on a body <paramref name="radiusKm"/> in
    /// radius, whose smooth ground is <paramref name="groundMeters"/> high at each direction,
    /// crossing <paramref name="rivers"/> (none: null), with marsh wherever
    /// <paramref name="isMarsh"/> says. Null if it isn't a dirt path or a cart track, or its
    /// way is a single point.
    /// </summary>
    public static DirtPath? For(RoadCourseShown course, double radiusKm,
        Func<Vector3D, double> groundMeters, RiverCarving? rivers, Func<Vector3D, bool> isMarsh)
    {
        if (course.Style is not (RoadStyle.DirtPaths or RoadStyle.CartTrack)
            || course.Path.Count < 2)
        {
            return null;
        }

        bool cart = course.Style == RoadStyle.CartTrack;
        Limits limits = cart ? _cartLimits : _pathLimits;

        double radiusMeters = radiusKm * 1000;
        Vector3D[] center = RiverProfile.Sampled([.. course.Path], radiusMeters, StepMeters);
        if (center.Length < 2)
        {
            return null;
        }

        var way = new Way(center, radiusMeters);
        double[] ground = [.. center.Select(groundMeters)];

        // Where it crosses rivers and marsh, and where it's too steep to climb straight.
        bool[] wet = [.. center.Select(point =>
            rivers?.WaterOver(point, WaterMarginMeters) is not null)];
        List<Run> wetRuns = Runs(wet, way.Along, 0, MarshGapMeters);
        double[] fromWet = Distances(wetRuns, way.Along);
        bool[] marsh = [.. center.Select((point, i) =>
            fromWet[i] > ApproachMeters && isMarsh(point))];
        List<Run> marshRuns = Runs(marsh, way.Along, LeastMarshMeters, MarshGapMeters);
        List<Run> crossings = [.. wetRuns.Concat(marshRuns).OrderBy(run => run.First)];
        double[] fromCrossing = Distances(crossings, way.Along);
        double[] smooth = RoadProfile.Averaged(ground, way.Along, SteepWindowMeters);
        List<Run> switchbacks = SteepRuns(smooth, way.Along, fromCrossing, limits.MaxGrade);

        // How far the strands spread: none at crossings, switchbacks, and the ends.
        double[] fromAny = Distances([.. crossings.Concat(switchbacks)], way.Along);
        double total = way.Along[^1];
        double[] spread = [.. fromAny.Select((distance, i) => SmoothStep(Math.Min(distance,
            Math.Min(way.Along[i], total - way.Along[i])) / GatherMeters))];

        ulong seed = SeededNoise.Seed(course.RoadId);
        double halfBand = Math.Max(0, course.WidthMeters / 2 - StrandHalfWidthMeters);
        // A cart track keeps to its way (its two ruts never split); a path's strands wander.
        double[] lean = cart
            ? new double[center.Length]
            : Lean(way, groundMeters, course.WidthMeters / 2);
        double[][] offsets = Offsets(way.Along, spread, lean, halfBand, seed,
            course.WidthMeters, out double[][] presence);
        double[] middle = cart ? new double[center.Length] : offsets[0];

        (List<Vector3D> trunk, int[] trunkAt, List<int> stations) = Trunk(way, middle,
            switchbacks, smooth, lean, limits);
        bool[] boardwalk = InRuns(marshRuns, center.Length);
        List<(int First, int Last)> pieces = Pieces(crossings, way.Along, trunkAt);
        var strands = new List<PathStrand>();
        int from = 0;
        foreach ((int first, int last) in pieces)
        {
            AddTrunkPiece(strands, trunk, from, first, cart, course.WidthMeters, radiusMeters);
            from = last;
        }

        AddTrunkPiece(strands, trunk, from, trunk.Count - 1, cart, course.WidthMeters,
            radiusMeters);
        for (int j = 1; j < _lanes.Length && !cart; j++)
        {
            AddPresent(strands, way, offsets[j], presence[j], j);
        }

        var laid = new List<RoadProfile>();
        foreach ((int first, int last) in pieces)
        {
            laid.Add(Crossing(trunk.GetRange(first, last - first + 1), radiusMeters,
                groundMeters, rivers, i => stations[first + i] is >= 0 and int station
                    && boardwalk[station], limits));
        }

        return new DirtPath(strands, laid);
    }

    // The strands' offsets across the band at each point, in meters (to the left), and how
    // far each is there (1 fully, 0 joined to its neighbor): the main strand and two others
    // always, the rest coming and going; all leaning toward the flatter side.
    private static double[][] Offsets(double[] along, double[] spread, double[] lean,
        double halfBand, ulong seed, double widthMeters, out double[][] presence)
    {
        int count = along.Length, strands = _lanes.Length;
        double typical = Math.Clamp(widthMeters / 6, MinStrands, MaxStrands);
        var own = new double[strands][];
        var offsets = new double[strands][];
        presence = new double[strands][];
        for (int j = 0; j < strands; j++)
        {
            own[j] = new double[count];
            offsets[j] = new double[count];
            presence[j] = new double[count];
            for (int i = 0; i < count; i++)
            {
                own[j][i] = Math.Clamp(_lanes[j] + WanderShare * SeededNoise.Noise(seed, j,
                    along[i] / WanderMeters) + lean[i], -1, 1);
            }
        }

        for (int j = 0; j < strands; j++)
        {
            // An extra strand's on where its own noise passes its bar: higher for each, so a
            // wide band has more of them than a narrow one.
            double bar = (j + 1 - typical) * 0.4;
            for (int i = 0; i < count; i++)
            {
                double on = j < MinStrands ? 1 : SmoothStep((SeededNoise.Noise(seed, j + 16,
                    along[i] / ComeAndGoMeters) - bar + 0.25) / 0.5);
                double across = on * own[j][i] + (1 - on) * own[_parents[j]][i];
                offsets[j][i] = spread[i] * halfBand * across;
                presence[j][i] = spread[i] * on;
            }
        }

        return offsets;
    }

    // How far the strands lean, as a share of the band's half-width, toward its flatter side at
    // each point: how steeply the ground climbs along the way a little to each side of it.
    private static double[] Lean(Way way, Func<Vector3D, double> groundMeters, double halfBand)
    {
        int count = way.Points.Length;
        double probe = LeanProbeShare * halfBand;
        double[] left = [.. Enumerable.Range(0, count).Select(i =>
            groundMeters(way.Beside(i, probe)))];
        double[] right = [.. Enumerable.Range(0, count).Select(i =>
            groundMeters(way.Beside(i, -probe)))];
        var lean = new double[count];
        for (int i = 0; i < count; i++)
        {
            int before = Math.Max(i - 1, 0), after = Math.Min(i + 1, count - 1);
            double span = Math.Max(way.Along[after] - way.Along[before], 1e-9);
            double steepLeft = Math.Abs(left[after] - left[before]) / span;
            double steepRight = Math.Abs(right[after] - right[before]) / span;
            lean[i] = LeanShare * (steepRight - steepLeft) / (steepLeft + steepRight + 0.05);
        }

        return RoadProfile.Averaged(lean, way.Along, LeanWindowMeters);
    }

    // The stretches too steep to climb straight (by the ground averaged a little, `smooth`),
    // padded a little either side, and kept clear of the crossings (their approaches come
    // first); steep is over `maxGrade`.
    private static List<Run> SteepRuns(double[] smooth, double[] along, double[] fromCrossing,
        double maxGrade)
    {
        int count = smooth.Length;
        var steep = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int before = i, after = i;
            while (before > 0 && along[i] - along[before] < SteepWindowMeters)
            {
                before--;
            }

            while (after < count - 1 && along[after] - along[i] < SteepWindowMeters)
            {
                after++;
            }

            double span = along[after] - along[before];
            steep[i] = span > 0
                && Math.Abs(smooth[after] - smooth[before]) / span > maxGrade
                && fromCrossing[i] > ApproachMeters + GatherMeters;
        }

        var runs = new List<Run>();
        foreach (Run run in Runs(steep, along, LeastSteepMeters, SteepGapMeters))
        {
            int first = run.First, last = run.Last;
            while (first > 0 && along[run.First] - along[first - 1] <= SteepPaddingMeters
                && fromCrossing[first - 1] > ApproachMeters + GatherMeters)
            {
                first--;
            }

            while (last < count - 1 && along[last + 1] - along[run.Last] <= SteepPaddingMeters
                && fromCrossing[last + 1] > ApproachMeters + GatherMeters)
            {
                last++;
            }

            double rise = Math.Abs(smooth[last] - smooth[first]);
            if (last > first && rise / (along[last] - along[first]) > maxGrade)
            {
                runs.Add(new Run(first, last));
            }
        }

        return runs;
    }

    // The main strand, as points (unit directions); which of them each point of the way is
    // (-1 inside switchbacks); and which point of the way each of them is (-1 likewise): its
    // offset at each point, but zigzagging up each steep stretch (of the ground averaged a
    // little, `smooth`) as its `limits` allow.
    private static (List<Vector3D> Points, int[] At, List<int> Stations) Trunk(Way way,
        double[] offsets, List<Run> switchbacks, double[] smooth, double[] lean, Limits limits)
    {
        var points = new List<Vector3D>();
        var stations = new List<int>();
        var at = new int[way.Points.Length];
        Array.Fill(at, -1);
        int next = 0;
        for (int i = 0; i < way.Points.Length; i++)
        {
            if (next < switchbacks.Count && switchbacks[next].First == i)
            {
                Run run = switchbacks[next++];
                at[run.First] = points.Count;
                foreach ((double along, double across) in Zigzag(way.Along, smooth, run,
                    lean[run.First] >= 0 ? 1 : -1, limits))
                {
                    points.Add(way.At(along, across));
                    stations.Add(-1);
                }

                stations[at[run.First]] = run.First;
                stations[^1] = run.Last;
                at[run.Last] = points.Count - 1;
                i = run.Last;
                continue;
            }

            at[i] = points.Count;
            points.Add(way.Beside(i, offsets[i]));
            stations.Add(i);
        }

        return (points, at, stations);
    }

    // A steep stretch's switchbacks, over the ground averaged a little (`smooth`) at points
    // `along` the way: as (along, across) points, across the slope and back as often as it
    // takes to climb at its limits' switchback grade, reaching at most their swing either side
    // (first to `side`), each leg climbing as much (so they bunch up where it's steepest), its
    // corners rounded, at most 2.5 m apart.
    private static List<(double Along, double Across)> Zigzag(double[] along, double[] smooth,
        Run run, int side, Limits limits)
    {
        var climbed = new double[run.Last - run.First + 1];
        for (int i = 1; i < climbed.Length; i++)
        {
            climbed[i] = climbed[i - 1]
                + Math.Abs(smooth[run.First + i] - smooth[run.First + i - 1]);
        }

        double rise = climbed[^1];
        double length = rise / limits.SwitchbackGrade;
        int legs = Math.Max(1, (int)Math.Ceiling(length / (2 * limits.SwingMeters)));
        double swing = length / (2 * legs);
        var corners = new List<(double Along, double Across)> { (along[run.First], 0) };
        for (int k = 0; k < legs; k++)
        {
            double where = AlongAt(climbed, along, run.First, rise * (k + 0.5) / legs);
            corners.Add((where, side * swing * (k % 2 == 0 ? 1 : -1)));
        }

        corners.Add((along[run.Last], 0));
        corners = Rounded(corners, limits.HairpinMeters);

        var points = new List<(double Along, double Across)> { corners[0] };
        for (int k = 1; k < corners.Count; k++)
        {
            (double a0, double x0) = corners[k - 1];
            (double a1, double x1) = corners[k];
            double leg = Math.Sqrt((a1 - a0) * (a1 - a0) + (x1 - x0) * (x1 - x0));
            int steps = Math.Max(1, (int)Math.Ceiling(leg / 2.5));
            for (int step = 1; step <= steps; step++)
            {
                double t = (double)step / steps;
                points.Add((a0 + (a1 - a0) * t, x0 + (x1 - x0) * t));
            }
        }

        return points;
    }

    // How far along the way a stretch from point `first` has climbed `target` meters (by
    // `climbed`, how much it's climbed by each of its points).
    private static double AlongAt(double[] climbed, double[] along, int first, double target)
    {
        int after = Array.FindIndex(climbed, value => value >= target);
        if (after <= 0)
        {
            return along[first + Math.Max(after, 0)];
        }

        double t = (target - climbed[after - 1])
            / Math.Max(climbed[after] - climbed[after - 1], 1e-12);
        return along[first + after - 1] + t * (along[first + after] - along[first + after - 1]);
    }

    // A line with each corner rounded off over `hairpinMeters` either side of it (less on a
    // short leg), keeping its ends.
    private static List<(double Along, double Across)> Rounded(
        List<(double Along, double Across)> line, double hairpinMeters)
    {
        var rounded = new List<(double Along, double Across)> { line[0] };
        for (int k = 1; k + 1 < line.Count; k++)
        {
            (double a, double x) = line[k];
            (double ab, double xb) = line[k - 1];
            (double aa, double xa) = line[k + 1];
            double before = Math.Sqrt((ab - a) * (ab - a) + (xb - x) * (xb - x));
            double after = Math.Sqrt((aa - a) * (aa - a) + (xa - x) * (xa - x));
            double cut = Math.Min(hairpinMeters, 0.4 * Math.Min(before, after));
            if (cut <= 0)
            {
                continue;
            }

            (double, double) start = (a + (ab - a) * cut / before, x + (xb - x) * cut / before);
            (double, double) end = (a + (aa - a) * cut / after, x + (xa - x) * cut / after);
            for (int step = 0; step <= HairpinSteps; step++)
            {
                double t = (double)step / HairpinSteps, u = 1 - t;
                rounded.Add((u * u * start.Item1 + 2 * u * t * a + t * t * end.Item1,
                    u * u * start.Item2 + 2 * u * t * x + t * t * end.Item2));
            }
        }

        rounded.Add(line[^1]);
        return rounded;
    }

    // The stretches of the main strand each crossing's laid over (as its first and last
    // points): from ApproachMeters before its water or marsh to as far after, joined where
    // they meet.
    private static List<(int First, int Last)> Pieces(List<Run> crossings, double[] along,
        int[] trunkAt)
    {
        var pieces = new List<(int First, int Last)>();
        foreach (Run run in crossings)
        {
            int first = run.First, last = run.Last;
            while (first > 0 && along[run.First] - along[first - 1] <= ApproachMeters
                && trunkAt[first - 1] >= 0)
            {
                first--;
            }

            while (last < along.Length - 1 && along[last + 1] - along[run.Last] <= ApproachMeters
                && trunkAt[last + 1] >= 0)
            {
                last++;
            }

            (int from, int to) = (trunkAt[first], trunkAt[last]);
            if (pieces.Count > 0 && pieces[^1].Last >= from)
            {
                pieces[^1] = (pieces[^1].First, Math.Max(pieces[^1].Last, to));
            }
            else if (to > from)
            {
                pieces.Add((from, to));
            }
        }

        return pieces;
    }

    // Whether each of `count` points is in one of the runs.
    private static bool[] InRuns(IEnumerable<Run> runs, int count)
    {
        var inside = new bool[count];
        foreach (Run run in runs)
        {
            Array.Fill(inside, true, run.First, run.Last - run.First + 1);
        }

        return inside;
    }

    // A crossing laid along `points` of the main strand: on the ground (as the rivers cut it),
    // down to a ford through water at most MaxFordWidthMeters wide, up onto a wooden bridge
    // over wider water, and onto a boardwalk over marsh (where `isMarsh` says, by point); as
    // wide and no steeper than its `limits`.
    private static RoadProfile Crossing(List<Vector3D> points, double radiusMeters,
        Func<Vector3D, double> groundMeters, RiverCarving? rivers, Func<int, bool> isMarsh,
        Limits limits)
    {
        Vector3D[] at = [.. points];
        double[] along = RoadProfile.AlongOf(at, radiusMeters);
        double[] ground = [.. at.Select(point =>
            rivers?.Carve(point, groundMeters(point)) ?? groundMeters(point))];
        double[] road = [.. ground];
        var bridges = new List<RoadBridge>();
        List<RoadBridge> waters = rivers is null
            ? []
            : RoadProfile.BridgesOver(at, along, rivers, StepMeters / 2);
        var fords = new List<RoadBridge>();
        foreach (RoadBridge water in waters)
        {
            if (water.WaterWidthMeters <= MaxFordWidthMeters)
            {
                fords.Add(water);
            }
            else
            {
                bridges.Add(water with
                {
                    Kind = BridgeKind.Wooden,
                    HalfWidthMeters = BridgeHalfWidthMeters,
                });
            }
        }

        Ford(road, along, fords, limits.MaxGrade);
        RoadProfile.RaiseForBridges(road, along, bridges, limits.MaxGrade, bridge => Math.Max(
            bridge.WaterMeters + WoodenBridge.ClearanceMeters,
            Math.Max(ground[bridge.First], ground[bridge.Last])));
        Boardwalks(road, ground, along, bridges, waters, isMarsh,
            limits.BoardwalkHalfMeters);
        bridges.Sort((a, b) => a.First.CompareTo(b.First));
        return RoadProfile.Laid(at, along, limits.RoadHalfMeters, ShoulderMeters, road,
            ground,
            bridges, RoadSurface.Dirt);
    }

    // Brings the path down to each ford: FordDepthMeters under the water across it, and down
    // to that on either side no steeper than `maxGrade` (cutting into the banks).
    private static void Ford(double[] road, double[] along, List<RoadBridge> fords,
        double maxGrade)
    {
        if (fords.Count == 0)
        {
            return;
        }

        var most = new double[road.Length];
        Array.Fill(most, double.PositiveInfinity);
        foreach (RoadBridge ford in fords)
        {
            double level = ford.WaterMeters - FordDepthMeters;
            for (int i = ford.First + 1; i < ford.Last; i++)
            {
                road[i] = level;
                most[i] = level;
            }

            most[ford.First] = Math.Min(most[ford.First], level);
            most[ford.Last] = Math.Min(most[ford.Last], level);
        }

        for (int i = 1; i < road.Length; i++)
        {
            most[i] = Math.Min(most[i], most[i - 1] + maxGrade * (along[i] - along[i - 1]));
        }

        for (int i = road.Length - 2; i >= 0; i--)
        {
            most[i] = Math.Min(most[i], most[i + 1] + maxGrade * (along[i + 1] - along[i]));
        }

        for (int i = 0; i < road.Length; i++)
        {
            road[i] = Math.Min(road[i], most[i]);
        }
    }

    // Lays a boardwalk over each stretch of marsh not over water: its deck BoardwalkMeters over
    // the ground, coming down to it at its ends, one bridge for each step, so it follows the
    // ground; `halfWidth` to each side.
    private static void Boardwalks(double[] road, double[] ground, double[] along,
        List<RoadBridge> bridges, List<RoadBridge> waters, Func<int, bool> isMarsh,
        double halfWidth)
    {
        bool Wet(int i) => waters.Any(water => i >= water.First && i <= water.Last);
        int count = road.Length;
        for (int i = 0; i < count; i++)
        {
            if (!isMarsh(i) || Wet(i))
            {
                continue;
            }

            int last = i;
            while (last + 1 < count && isMarsh(last + 1) && !Wet(last + 1))
            {
                last++;
            }

            int first = Math.Max(i - 1, 0), end = Math.Min(last + 1, count - 1);
            for (int k = first + 1; k < end; k++)
            {
                road[k] = ground[k] + WoodenBridge.BoardwalkMeters;
            }

            for (int k = first; k < end; k++)
            {
                double low = Math.Min(ground[k], ground[k + 1]);
                bridges.Add(new RoadBridge(k, k + 1, low, low)
                {
                    LengthMeters = along[k + 1] - along[k],
                    Kind = BridgeKind.Boardwalk,
                    HalfWidthMeters = halfWidth,
                });
            }

            i = end;
        }
    }

    // Adds the piece of the main strand from point `first` to `last`, if it has length: a
    // path's one worn strand, or a cart track's two ruts and the ground kept clear of plants
    // across its width (`widthMeters`, at least between its ruts).
    private static void AddTrunkPiece(List<PathStrand> strands, List<Vector3D> points,
        int first, int last, bool cart, double widthMeters, double radiusMeters)
    {
        if (last <= first)
        {
            return;
        }

        List<Vector3D> piece = points.GetRange(first, last - first + 1);
        if (!cart)
        {
            strands.Add(new PathStrand(piece, TrunkHalfWidthMeters, 0));
            return;
        }

        var way = new Way([.. piece], radiusMeters);
        foreach ((double side, int index) in new[] { (1.0, 0), (-1.0, 1) })
        {
            strands.Add(new PathStrand([.. Enumerable.Range(0, piece.Count)
                .Select(i => way.Beside(i, side * RutGaugeMeters / 2))], RutHalfWidthMeters,
                index)
            { Rut = true });
        }

        strands.Add(new PathStrand(piece, Math.Max(RutGaugeMeters, widthMeters) / 2, 0)
        {
            Worn = false,
        });
    }

    // Adds the pieces of strand `index` where it's apart from the main one.
    private static void AddPresent(List<PathStrand> strands, Way way, double[] offsets,
        double[] presence, int index)
    {
        List<Vector3D>? piece = null;
        for (int i = 0; i < offsets.Length; i++)
        {
            if (presence[i] < JoinedShare)
            {
                if (piece is { Count: > 1 })
                {
                    strands.Add(new PathStrand(piece, StrandHalfWidthMeters, index));
                }

                piece = null;
                continue;
            }

            (piece ??= []).Add(way.Beside(i, offsets[i]));
        }

        if (piece is { Count: > 1 })
        {
            strands.Add(new PathStrand(piece, StrandHalfWidthMeters, index));
        }
    }

    // The runs of points flagged, at least `least` meters long, those less than `gap` meters
    // apart joined.
    private static List<Run> Runs(bool[] flags, double[] along, double least, double gap)
    {
        var runs = new List<Run>();
        for (int i = 0; i < flags.Length; i++)
        {
            if (!flags[i])
            {
                continue;
            }

            int last = i;
            while (last + 1 < flags.Length && flags[last + 1])
            {
                last++;
            }

            if (runs.Count > 0 && along[i] - along[runs[^1].Last] < gap)
            {
                runs[^1] = runs[^1] with { Last = last };
            }
            else
            {
                runs.Add(new Run(i, last));
            }

            i = last;
        }

        return [.. runs.Where(run => along[run.Last] - along[run.First] >= least)];
    }

    // How far along the way each point is from the nearest of the runs (0 in one), in meters;
    // infinite with none.
    private static double[] Distances(IReadOnlyList<Run> runs, double[] along)
    {
        int count = along.Length;
        var distances = new double[count];
        Array.Fill(distances, double.PositiveInfinity);
        foreach (Run run in runs)
        {
            for (int i = run.First; i <= run.Last; i++)
            {
                distances[i] = 0;
            }
        }

        for (int i = 1; i < count; i++)
        {
            distances[i] = Math.Min(distances[i], distances[i - 1] + along[i] - along[i - 1]);
        }

        for (int i = count - 2; i >= 0; i--)
        {
            distances[i] = Math.Min(distances[i], distances[i + 1] + along[i + 1] - along[i]);
        }

        return distances;
    }

    // 0 below 0, 1 above 1, and smoothly between.
    private static double SmoothStep(double x)
    {
        double t = Math.Clamp(x, 0, 1);
        return t * t * (3 - 2 * t);
    }

    // What a path or track can climb (straight, and in its switchbacks), how far its
    // switchbacks swing either side and how far their corners are rounded, and how wide its
    // crossings' road and boardwalks are to each side, in meters.
    private sealed record Limits(double MaxGrade, double SwitchbackGrade, double SwingMeters,
        double HairpinMeters, double RoadHalfMeters, double BoardwalkHalfMeters);

    // A run of points, from its first to its last.
    private readonly record struct Run(int First, int Last);

    // The path's way, as points at most StepMeters apart, with how far along each is and the
    // way across it (to the left) there.
    private sealed class Way
    {
        private readonly double _radiusMeters;
        private readonly Vector3D[] _sides;

        public Way(Vector3D[] points, double radiusMeters)
        {
            (Points, _radiusMeters) = (points, radiusMeters);
            Along = RoadProfile.AlongOf(points, radiusMeters);
            _sides = new Vector3D[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3D before = points[Math.Max(i - 1, 0)];
                Vector3D after = points[Math.Min(i + 1, points.Length - 1)];
                Vector3D side = points[i].Cross(after - before);
                _sides[i] = side * (1 / Math.Max(side.Length, 1e-12));
            }
        }

        public Vector3D[] Points { get; }

        public double[] Along { get; }

        // The unit direction `across` meters to the left of point i.
        public Vector3D Beside(int i, double across) =>
            Unit(Points[i] + _sides[i] * (across / _radiusMeters));

        // The unit direction `across` meters to the left of the way, `along` meters along it.
        public Vector3D At(double along, double across)
        {
            int after = Array.BinarySearch(Along, along);
            after = after >= 0 ? after : ~after;
            after = Math.Clamp(after, 1, Points.Length - 1);
            int before = after - 1;
            double span = Along[after] - Along[before];
            double t = span > 0 ? Math.Clamp((along - Along[before]) / span, 0, 1) : 0;
            Vector3D point = Points[before] * (1 - t) + Points[after] * t;
            Vector3D side = _sides[before] * (1 - t) + _sides[after] * t;
            return Unit(Unit(point) + Unit(side) * (across / _radiusMeters));
        }

        private static Vector3D Unit(Vector3D v) => v * (1 / Math.Max(v.Length, 1e-30));
    }
}
