using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class DirtPathTests
{
    private const double RadiusKm = RoadProfileTests.RadiusKm;
    private const double MetersPerDegree = RoadProfileTests.MetersPerDegree;

    // How far either side of longitude 0 the paths run, in degrees (about 1.1 km).
    private const double Degrees = 0.01;
    private const double HalfLengthMeters = Degrees * MetersPerDegree;

    // A dirt path with a band 30 m wide drawn east along the equator.
    private static RoadCourseShown Path(Guid? id = null, RoadStyle style = RoadStyle.DirtPaths,
        double widthMeters = 30) =>
        new(id ?? Guid.NewGuid(), style, RoadKind.Drawn, widthMeters,
            RiverLine.Densified([WaterGround.At(0, -Degrees), WaterGround.At(0, Degrees)]),
            null);

    private static DirtPath Laid(RoadCourseShown course, Func<Vector3D, double>? ground = null,
        RiverCarving? rivers = null, Func<Vector3D, bool>? isMarsh = null) =>
        DirtPath.For(course, RadiusKm, ground ?? (_ => 100), rivers, isMarsh ?? (_ => false))!;

    private static double East(Vector3D direction) => RoadProfileTests.East(direction);

    // How far north of the equator a direction is, in meters.
    private static double North(Vector3D direction) =>
        Math.Asin(Math.Clamp(direction.Y, -1, 1)) * RadiusKm * 1000;

    // A river `widthKm` wide running north across the equator at longitude 0, over level
    // ground 100 m up.
    private static RiverCarving River(double widthKm) => RiverCarving.For([RiverProfile.For(
        RiverProfileTests.Drawn(widthKm, WaterGround.At(-0.05, 0), WaterGround.At(0.05, 0)),
        RadiusKm, _ => 100)!], RadiusKm);

    [Fact]
    public void OnlyDirtPathsAndCartTracks_AreLaidAsStrands()
    {
        Assert.Null(DirtPath.For(Path(style: RoadStyle.StoneHighway), RadiusKm, _ => 100, null,
            _ => false));
        Assert.Null(DirtPath.For(Path(style: RoadStyle.OldRoad), RadiusKm, _ => 100, null,
            _ => false));
        Assert.NotNull(DirtPath.For(Path(), RadiusKm, _ => 100, null, _ => false));
        Assert.NotNull(DirtPath.For(Path(style: RoadStyle.CartTrack), RadiusKm, _ => 100, null,
            _ => false));
    }

    [Fact]
    public void TheStrands_StayInTheBand_AndNumberThreeToSeven_AwayFromTheEnds()
    {
        DirtPath path = Laid(Path());

        Assert.All(path.Strands, strand => Assert.All(strand.Points, point =>
            Assert.InRange(Math.Abs(North(point)), 0, 15 + 1e-6)));
        for (double east = -HalfLengthMeters + 100; east <= HalfLengthMeters - 100; east += 10)
        {
            int count = path.Strands.Count(strand =>
                strand.Points.Min(East) <= east && strand.Points.Max(East) >= east);
            Assert.InRange(count, DirtPath.MinStrands, DirtPath.MaxStrands);
        }
    }

    [Fact]
    public void TheStrands_ComeTogether_AtTheEnds()
    {
        DirtPath path = Laid(Path());

        // Near each end, only the main strand is left, on the path's way.
        foreach (PathStrand strand in path.Strands)
        {
            foreach (Vector3D point in strand.Points)
            {
                double fromEnd = HalfLengthMeters - Math.Abs(East(point));
                if (fromEnd < 2)
                {
                    Assert.Equal(0, strand.Index);
                    Assert.InRange(Math.Abs(North(point)), 0, 0.5);
                }
            }
        }
    }

    [Fact]
    public void ThePath_IsTheSameEveryTime_AndEachPathHasItsOwn()
    {
        var id = Guid.NewGuid();
        DirtPath first = Laid(Path(id)), again = Laid(Path(id)), other = Laid(Path());

        Assert.Equal(first.Strands.Count, again.Strands.Count);
        for (int s = 0; s < first.Strands.Count; s++)
        {
            Assert.Equal(first.Strands[s].Points, again.Strands[s].Points);
        }

        Assert.NotEqual(first.Strands.SelectMany(strand => strand.Points).Select(North),
            other.Strands.SelectMany(strand => strand.Points).Select(North));
    }

    [Fact]
    public void TheStrands_LeanAwayFromTheSteeperSide()
    {
        // Level to the south of the equator, rising steeply to the north, more so the farther.
        static double Ground(Vector3D direction)
        {
            double north = Math.Max(0, Math.Asin(direction.Y) * RadiusKm * 1000);
            return 100 + 0.2 * north * Math.Sin(East(direction) / 20);
        }

        DirtPath level = Laid(Path(Guid.Empty)), leaning = Laid(Path(Guid.Empty), Ground);

        static double Mean(DirtPath path) =>
            path.Strands.SelectMany(strand => strand.Points).Average(North);
        Assert.True(Mean(leaning) < Mean(level) - 1, $"{Mean(leaning)} vs {Mean(level)}");
    }

    [Fact]
    public void AcrossANarrowStream_ItFords_JustUnderTheWater()
    {
        RiverCarving stream = River(0.008);
        DirtPath path = Laid(Path(), rivers: stream);

        RoadProfile crossing = Assert.Single(path.Crossings);
        Assert.Empty(crossing.Bridges);
        Assert.Equal(RoadSurface.Dirt, crossing.Surface);
        (double water, double bed, double width) = stream.WaterOver(WaterGround.At(0, 0), 0)!.Value;
        Assert.InRange(width, 1, DirtPath.MaxFordWidthMeters);
        int middle = Enumerable.Range(0, crossing.Points.Count)
            .MinBy(i => Math.Abs(East(crossing.Points[i])));
        Assert.Equal(water - DirtPath.FordDepthMeters, crossing.RoadMeters[middle], 6);
        Assert.True(crossing.RoadMeters[middle] > bed);

        // Down to it no steeper than a path climbs, and back on the ground at its ends.
        for (int i = 1; i < crossing.Points.Count; i++)
        {
            double grade = Math.Abs(crossing.RoadMeters[i] - crossing.RoadMeters[i - 1])
                / (crossing.AlongMeters[i] - crossing.AlongMeters[i - 1]);
            Assert.True(grade <= DirtPath.MaxGrade + 1e-9, $"{grade} at {i}");
        }

        Assert.Equal(crossing.GroundMeters[0], crossing.RoadMeters[0], 6);
        Assert.Equal(crossing.GroundMeters[^1], crossing.RoadMeters[^1], 6);

        // Only the main strand reaches the water, and it's on the crossing there.
        Assert.All(path.Strands, strand => Assert.All(strand.Points, point =>
            Assert.True(Math.Abs(East(point)) > width / 2, $"{East(point)}")));
    }

    [Fact]
    public void AcrossAWideRiver_ItCrossesOnAWoodenBridge_OverTheWater()
    {
        RiverCarving river = River(0.05);
        DirtPath path = Laid(Path(), rivers: river);

        RoadProfile crossing = Assert.Single(path.Crossings);
        RoadBridge bridge = Assert.Single(crossing.Bridges);
        Assert.Equal(BridgeKind.Wooden, bridge.Kind);
        Assert.Equal(DirtPath.BridgeHalfWidthMeters, bridge.HalfWidthMeters);
        Assert.True(bridge.WaterWidthMeters > DirtPath.MaxFordWidthMeters);
        Assert.True(crossing.RoadMeters[bridge.First]
            >= bridge.WaterMeters + WoodenBridge.ClearanceMeters - 1e-9);
        Assert.True(crossing.RoadMeters[bridge.Last]
            >= bridge.WaterMeters + WoodenBridge.ClearanceMeters - 1e-9);
        Assert.True(East(crossing.Points[bridge.First]) < -bridge.WaterWidthMeters / 2);
        Assert.True(East(crossing.Points[bridge.Last]) > bridge.WaterWidthMeters / 2);

        // Walkers cross on its deck.
        RoadCarving carving = RoadCarving.For(path.Crossings, RadiusKm);
        Assert.NotNull(carving.DeckMeters(crossing.Points[(bridge.First + bridge.Last) / 2]));
    }

    [Fact]
    public void OverMarsh_ItCrossesOnABoardwalk_FollowingTheGround()
    {
        static bool Marsh(Vector3D direction) => Math.Abs(East(direction)) < 100;
        static double Ground(Vector3D direction) => 100 + 0.01 * East(direction);
        DirtPath path = Laid(Path(), Ground, isMarsh: Marsh);

        RoadProfile crossing = Assert.Single(path.Crossings);
        Assert.NotEmpty(crossing.Bridges);
        Assert.All(crossing.Bridges, bridge =>
        {
            Assert.Equal(BridgeKind.Boardwalk, bridge.Kind);
            Assert.Equal(DirtPath.BoardwalkHalfWidthMeters, bridge.HalfWidthMeters);
        });
        double covered = crossing.Bridges.Sum(bridge => bridge.LengthMeters);
        Assert.InRange(covered, 195, 215);
        for (int i = 0; i < crossing.Points.Count; i++)
        {
            if (Math.Abs(East(crossing.Points[i])) < 90)
            {
                Assert.Equal(Ground(crossing.Points[i]) + WoodenBridge.BoardwalkMeters,
                    crossing.RoadMeters[i], 6);
            }
        }

        // The boardwalk is narrower than the path: walkers off its edge aren't on it.
        RoadCarving carving = RoadCarving.For(path.Crossings, RadiusKm);
        Vector3D middle = crossing.Points[crossing.Points.Count / 2];
        Assert.NotNull(carving.DeckMeters(middle));
        Vector3D aside = middle + new Vector3D(0, 1, 0) * (1.2 / (RadiusKm * 1000));
        Assert.Null(carving.DeckMeters(aside * (1 / aside.Length)));
    }

    [Fact]
    public void UpASteepSlope_TheMainStrandSwitchesBack_AtAboutItsGrade()
    {
        // A slope 30% steep for 200 m in the middle.
        static double Ground(Vector3D direction) =>
            100 + 0.3 * Math.Clamp(East(direction), -100, 100);
        DirtPath path = Laid(Path(), Ground);

        PathStrand trunk = path.Strands.Single(strand => strand.Index == 0);
        double widest = trunk.Points.Max(point => Math.Abs(North(point)));
        Assert.InRange(widest, 15, DirtPath.MaxSwingMeters + 1);

        // It climbs the slope over a longer way than straight, at about its grade.
        var climb = trunk.Points.Where(point => Math.Abs(East(point)) < 100).ToList();
        double length = climb.Zip(climb.Skip(1)).Sum(pair =>
            (pair.First - pair.Second).Length * RadiusKm * 1000);
        Assert.InRange(60 / length, 0.08, DirtPath.MaxGrade);

        // The other strands keep off the slope.
        Assert.All(path.Strands.Where(strand => strand.Index != 0), strand =>
            Assert.All(strand.Points, point => Assert.True(Math.Abs(East(point)) > 100)));
        Assert.Empty(path.Crossings);
    }

    [Fact]
    public void ItsStrands_KeepPlantsOff_AndAreDrawnAsWornRibbons()
    {
        DirtPath path = Laid(Path(Guid.Empty));
        PathStrands strands = PathStrands.For([path], RadiusKm);
        PathStrand trunk = path.Strands.First(strand => strand.Index == 0);
        Vector3D on = trunk.Points[trunk.Points.Count / 2];

        Assert.True(strands.OnPath(on, 0));
        Vector3D far = on + new Vector3D(0, 1, 0) * (40 / (RadiusKm * 1000));
        Assert.False(strands.OnPath(far * (1 / far.Length), 1));

        List<List<BankPoint[]>> ribbons = strands.RibbonsNear((_, ground) => ground, _ => 100,
            _ => 99, on, 200);
        Assert.NotEmpty(ribbons);
        Assert.All(ribbons.SelectMany(strip => strip).SelectMany(row => row), point =>
        {
            Assert.Equal(StripSurface.Worn, point.Surface);
            Assert.InRange(point.Meters, 100, 100.5);
            Assert.InRange(Math.Acos(Math.Clamp(point.Direction.Dot(on), -1, 1))
                * RadiusKm * 1000, 0, 205);
        });
        Assert.Contains(ribbons.SelectMany(strip => strip), row => row[2].Wear == 1
            && row[0].Wear == 0 && row[^1].Wear == 0);

        // Where the coarser ground's drawn higher than the ground, the ribbons lie over it.
        Assert.All(strands.RibbonsNear((_, ground) => ground, _ => 100, _ => 101, on, 200)
            .SelectMany(strip => strip).SelectMany(row => row), point =>
                Assert.InRange(point.Meters, 101, 101.5));
    }

    // A cart track 4 m wide drawn east along the equator.
    private static RoadCourseShown Track(Guid? id = null) =>
        Path(id, RoadStyle.CartTrack, 4);

    [Fact]
    public void ACartTrack_IsTwoRuts_AGaugeApart_TheGroundBetweenKeptClear()
    {
        DirtPath track = Laid(Track(Guid.Empty));

        List<PathStrand> ruts = [.. track.Strands.Where(strand => strand.Worn)];
        Assert.Equal(2, ruts.Count);
        Assert.All(ruts, rut =>
        {
            Assert.True(rut.Rut);
            Assert.Equal(DirtPath.RutHalfWidthMeters, rut.HalfWidthMeters);
            Assert.All(rut.Points, point =>
                Assert.Equal(DirtPath.RutGaugeMeters / 2, Math.Abs(North(point)), 3));
        });
        Assert.Contains(ruts, rut => North(rut.Points[0]) > 0);
        Assert.Contains(ruts, rut => North(rut.Points[0]) < 0);

        // Plants keep off its width (between the ruts too), but grow beside it.
        PathStrands strands = PathStrands.For([track], RadiusKm);
        Assert.True(strands.OnPath(WaterGround.At(0, 0), 0));
        Vector3D edge = WaterGround.At(1.9 / MetersPerDegree, 0);
        Assert.True(strands.OnPath(edge, 0));
        Vector3D beside = WaterGround.At(3 / MetersPerDegree, 0);
        Assert.False(strands.OnPath(beside, 0.3));

        // Only the ruts are drawn, as ruts.
        List<List<BankPoint[]>> ribbons = strands.RibbonsNear((_, ground) => ground, _ => 100,
            _ => 99, WaterGround.At(0, 0), 200);
        Assert.Equal(2, ribbons.Count);
        Assert.All(ribbons.SelectMany(strip => strip).SelectMany(row => row), point =>
        {
            Assert.Equal(StripSurface.Rut, point.Surface);
            Assert.InRange(Math.Abs(North(point.Direction)), 0.4, 1.1);
        });
    }

    [Fact]
    public void UpASlope_ACartTrackSwitchesBack_WherePathsWouldClimbStraight()
    {
        // A slope 12% steep for 200 m in the middle: under a path's limit, over a cart's.
        static double Ground(Vector3D direction) =>
            100 + 0.12 * Math.Clamp(East(direction), -100, 100);
        Assert.All(Laid(Path(), Ground).Strands.Where(strand => strand.Index == 0), strand =>
            Assert.All(strand.Points, point => Assert.InRange(Math.Abs(North(point)), 0, 15)));

        DirtPath track = Laid(Track(), Ground);
        PathStrand rut = track.Strands.First(strand => strand.Rut);
        double widest = rut.Points.Max(point => Math.Abs(North(point)));
        Assert.InRange(widest, 20, DirtPath.CartMaxSwingMeters + 2);

        // It climbs the 24 m over a longer way than straight, at about its grade.
        var climb = rut.Points.Where(point => Math.Abs(East(point)) < 100).ToList();
        double length = climb.Zip(climb.Skip(1)).Sum(pair =>
            (pair.First - pair.Second).Length * RadiusKm * 1000);
        Assert.InRange(24 / length, 0.05, DirtPath.CartMaxGrade);
    }

    [Fact]
    public void ACartTrack_CrossesWaterAndMarsh_ThreeMetersWide()
    {
        DirtPath bridged = Laid(Track(), rivers: River(0.05));
        RoadProfile crossing = Assert.Single(bridged.Crossings);
        Assert.Equal(DirtPath.CartHalfWidthMeters, crossing.HalfWidthMeters);
        RoadBridge bridge = Assert.Single(crossing.Bridges);
        Assert.Equal(BridgeKind.Wooden, bridge.Kind);
        Assert.Equal(DirtPath.CartHalfWidthMeters, bridge.HalfWidthMeters);

        // Its ruts break off where it crosses, as a path's strand does.
        Assert.Equal(4, bridged.Strands.Count(strand => strand.Rut));

        static bool Marsh(Vector3D direction) => Math.Abs(East(direction)) < 100;
        DirtPath boardwalked = Laid(Track(), isMarsh: Marsh);
        Assert.All(Assert.Single(boardwalked.Crossings).Bridges, board =>
        {
            Assert.Equal(BridgeKind.Boardwalk, board.Kind);
            Assert.Equal(DirtPath.CartHalfWidthMeters, board.HalfWidthMeters);
        });
    }
}
