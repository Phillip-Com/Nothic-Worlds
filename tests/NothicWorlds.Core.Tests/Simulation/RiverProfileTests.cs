using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class RiverProfileTests
{
    private const double RadiusKm = 6371;

    [Fact]
    public void ADrawnRiver_IsCutIntoShortSteps_EndsKept()
    {
        RiverCourseShown course = Drawn(1, WaterGround.At(0, 0), WaterGround.At(0, 3));

        List<Vector3D> path = RiverLine.PathOf(course);

        Assert.InRange(path.Count, 7, 8);  // 3° in steps of up to 0.5°
        Assert.All(path.Zip(path.Skip(1)), pair => Assert.True(
            double.RadiansToDegrees(Math.Acos(Math.Clamp(pair.First.Dot(pair.Second), -1, 1)))
                <= RiverLine.StepDegrees + 1e-9));
        Assert.Equal(course.Points[0], path[0]);
        Assert.Equal(course.Points[^1], path[^1]);
    }

    [Fact]
    public void ANaturalRiver_IsSmoothed_EndsKept()
    {
        RiverCourseShown course = new(null, null, RiverKind.Natural, 1,
            [WaterGround.At(0, 0), WaterGround.At(0.1, 0.1), WaterGround.At(0, 0.2)], true);

        List<Vector3D> path = RiverLine.PathOf(course);

        Assert.True(path.Count > 3);
        Assert.Equal(course.Points[0], path[0]);
        Assert.Equal(course.Points[^1], path[^1]);
    }

    [Theory]
    [InlineData(5, 1)]       // Narrow: as shallow as a river gets
    [InlineData(100, 5)]     // A twentieth of its width
    [InlineData(2000, 20)]   // Wide: as deep as a river gets
    public void Depth_FollowsWidth(double widthMeters, double depthMeters)
    {
        Assert.Equal(depthMeters, RiverProfile.DepthFor(widthMeters));
    }

    [Fact]
    public void TheWater_FollowsTheGround_EvenUphill_InShortSteps()
    {
        // Ground falling, then rising over a ridge, then falling again: the water rides over
        // it (the owner's choice) instead of cutting a canyon through it.
        double Ground(Vector3D at) =>
            500 - 100 * Longitude(at) + 400 * Math.Exp(-Square(Longitude(at) - 1));
        RiverCourseShown course = Drawn(0.1, WaterGround.At(0, 0), WaterGround.At(0, 2));
        RiverProfile river = RiverProfile.For(course, RadiusKm, Ground)!;
        Assert.True(river.Points.Count > 1000);  // About 222 km, at most 200 m apart
        for (int i = 0; i < river.Points.Count; i++)
        {
            double depth = RiverProfile.DepthFor(2 * river.HalfWidthMeters[i]);
            double below = Ground(river.Points[i]) - river.WaterMeters[i];
            Assert.InRange(below, RiverProfile.WaterBelowGround * depth - 0.01,
                RiverProfile.WaterBelowGround * depth + 0.5);
            Assert.Equal(river.WaterMeters[i] - (1 - RiverProfile.WaterBelowGround) * depth,
                river.BedMeters[i], 9);
        }

        Assert.All(river.Points.Zip(river.Points.Skip(1)), pair => Assert.True(
            Math.Acos(Math.Clamp(pair.First.Dot(pair.Second), -1, 1)) * RadiusKm * 1000
                <= RiverProfile.SampleMeters + 1e-6));
    }

    [Fact]
    public void OnASideSlope_TheWaterLiesBelowTheGroundOnItsLowerSide()
    {
        // A river running east along the equator, over ground falling 1 m for every 10 m
        // south: the water lies below the ground at its lower edge, not its middle.
        const double metersPerDegree = RadiusKm * 1000 * Math.PI / 180;
        double Ground(Vector3D at) =>
            1000 + 0.1 * SphericalPolygon.FromUnit(at).LatitudeDegrees * metersPerDegree;
        RiverCourseShown course = Drawn(0.2, WaterGround.At(0, 0), WaterGround.At(0, 0.1));
        RiverProfile river = RiverProfile.For(course, RadiusKm, Ground)!;
        int last = river.Points.Count - 1;  // 200 m wide, 10 m deep
        double edge = 100 + (1 - RiverProfile.WaterBelowGround) * 10 / RiverCarving.BankSlope;
        Assert.Equal(1000 - 0.1 * edge - RiverProfile.WaterBelowGround * 10,
            river.WaterMeters[last], 0);
    }

    [Fact]
    public void WhereTheGroundRises_TheWaterIsCalm()
    {
        RiverCourseShown course = Drawn(0.1, WaterGround.At(0, 0), WaterGround.At(0, 0.1));
        RiverProfile rising = RiverProfile.For(course, RadiusKm,
            at => 20_000 * Longitude(at))!;
        int middle = rising.Points.Count / 2;
        Assert.Equal(RiverProfile.MinFlow, rising.FlowMetersPerSecond[middle]);
        Assert.Equal(0, rising.Rapids[middle]);
    }

    [Fact]
    public void ARiver_WidensFromAFifthAtItsSource()
    {
        RiverCourseShown course = Drawn(2, WaterGround.At(0, 0), WaterGround.At(0, 1));

        RiverProfile river = RiverProfile.For(course, RadiusKm, _ => 0)!;

        Assert.Equal(200, river.HalfWidthMeters[0], 6);
        Assert.Equal(1000, river.HalfWidthMeters[^1], 6);
    }

    [Fact]
    public void SteepWater_FlowsFaster_WithRapids()
    {
        RiverCourseShown course = Drawn(0.1, WaterGround.At(0, 0), WaterGround.At(0, 0.1));
        RiverProfile flat = RiverProfile.For(course, RadiusKm, _ => 0)!;
        RiverProfile steep = RiverProfile.For(course, RadiusKm,
            at => 2000 - 20_000 * Longitude(at))!;

        // On level ground the water falls only as the river deepens downstream.
        Assert.True(flat.FlowMetersPerSecond[1] < 1);
        Assert.Equal(0, flat.Rapids[1]);
        Assert.True(steep.FlowMetersPerSecond[1] > 2);
        Assert.Equal(1, steep.Rapids[1]);
    }

    [Fact]
    public void ACourseOfOnePoint_HasNoProfile()
    {
        RiverCourseShown course = new(null, null, RiverKind.Natural, 1, [WaterGround.At(0, 0)],
            false);

        Assert.Null(RiverProfile.For(course, RadiusKm, _ => 0));
    }

    [Fact]
    public void ASetDepth_LowersOnlyTheBed_ShallowerTowardTheSource()
    {
        // 100 m wide at its mouth: Auto is 5 m deep there and 1 m at its source.
        RiverCourseShown auto = Drawn(0.1, WaterGround.At(0, 0), WaterGround.At(0, 0.2));
        RiverProfile shallow = RiverProfile.For(auto, RadiusKm, _ => 100)!;
        RiverProfile deep = RiverProfile.For(
            auto with { Depth = new RiverDepth(MouthMeters: 30) }, RadiusKm, _ => 100)!;

        Assert.Equal(100 - 30, deep.BedMeters[^1], 6);
        Assert.Equal(100 - 6, deep.BedMeters[0], 6);  // A fifth of it at the source
        Assert.Equal(shallow.WaterMeters, deep.WaterMeters);
        Assert.Equal(shallow.WaterHalfWidthMeters, deep.WaterHalfWidthMeters);
    }

    [Fact]
    public void ADepthSetShallowerThanAuto_RaisesTheWaterToo()
    {
        RiverCourseShown course = Drawn(0.1, WaterGround.At(0, 0), WaterGround.At(0, 0.2))
            with
        { Depth = new RiverDepth(MouthMeters: 2) };

        RiverProfile river = RiverProfile.For(course, RadiusKm, _ => 100)!;

        Assert.Equal(100 - RiverProfile.WaterBelowGround * 2, river.WaterMeters[^1], 6);
        Assert.Equal(100 - 2, river.BedMeters[^1], 6);
    }

    [Fact]
    public void AVaryingBed_RisesAndFallsUnderStillWater_NeverAboveIt()
    {
        RiverCourseShown even = Drawn(0.1, WaterGround.At(0, 0), WaterGround.At(0, 0.5));
        RiverCourseShown varied = even with
        {
            Depth = new RiverDepth(VariationMeters: 8, SpacingKm: 0.8),
        };
        RiverProfile flat = RiverProfile.For(even, RadiusKm, _ => 100)!;
        RiverProfile river = RiverProfile.For(varied, RadiusKm, _ => 100)!;

        // Finer steps (an eighth of its spacing) so the rises and falls show.
        Assert.True(river.Points.Count >= 2 * flat.Points.Count - 2);
        double[] beds = [.. river.BedMeters];
        Assert.True(beds.Max() - beds.Min() > 8);
        for (int i = 0; i < river.Points.Count; i++)
        {
            Assert.True(river.BedMeters[i]
                <= river.WaterMeters[i] - RiverProfile.MinWaterDepthMeters + 1e-9);
        }

        Assert.Equal(flat.WaterMeters[^1], river.WaterMeters[^1], 6);
    }

    internal static RiverCourseShown Drawn(double widthKm, params Vector3D[] points) =>
        new(Guid.NewGuid(), null, RiverKind.Drawn, widthKm, points, true);

    internal static double Longitude(Vector3D at) =>
        SphericalPolygon.FromUnit(at).LongitudeDegrees;

    private static double Square(double x) => x * x;
}
