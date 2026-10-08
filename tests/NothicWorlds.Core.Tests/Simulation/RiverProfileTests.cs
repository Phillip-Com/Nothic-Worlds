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
    public void TheWater_NeverRunsUphill_AndTheBedIsBelowIt()
    {
        // Ground falling, then rising over a ridge, then falling again.
        RiverCourseShown course = Drawn(0.1, WaterGround.At(0, 0), WaterGround.At(0, 2));
        RiverProfile river = RiverProfile.For(course, RadiusKm,
            at => 500 - 100 * Longitude(at) + 400 * Math.Exp(-Square(Longitude(at) - 1)))!;

        for (int i = 1; i < river.Points.Count; i++)
        {
            Assert.True(river.WaterMeters[i] <= river.WaterMeters[i - 1]);
            Assert.True(river.BedMeters[i] < river.WaterMeters[i]);
        }
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

    internal static RiverCourseShown Drawn(double widthKm, params Vector3D[] points) =>
        new(Guid.NewGuid(), null, RiverKind.Drawn, widthKm, points, true);

    internal static double Longitude(Vector3D at) =>
        SphericalPolygon.FromUnit(at).LongitudeDegrees;

    private static double Square(double x) => x * x;
}
