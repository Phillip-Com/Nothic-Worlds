using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public class FlatWalkTests
{
    private static readonly FlatSpot _center = new(FlatFace.Top, 0, 0);
    private static double RimHeight => 2 * FlatDisc.HalfThickness;

    [Fact]
    public void WalkingSouthFromTheCenter_GoesOverTheRim_DownIt_AndAcrossTheUnderside()
    {
        FlatSpot nearRim = FlatWalk.Walk(_center, Math.PI, FlatDisc.Radius - 0.01);
        FlatSpot onRim = FlatWalk.Walk(_center, Math.PI, FlatDisc.Radius + RimHeight / 2);
        FlatSpot under = FlatWalk.Walk(_center, Math.PI, FlatDisc.Radius + RimHeight + 1);
        FlatSpot farSide = FlatWalk.Walk(_center, Math.PI, 2 * FlatDisc.Radius + RimHeight);

        Assert.Equal(FlatFace.Top, nearRim.Face);
        Assert.Equal(FlatFace.Rim, onRim.Face);
        Assert.Equal(0, onRim.B, 6);  // Halfway down the wall
        Assert.Equal(FlatFace.Bottom, under.Face);
        Assert.Equal(FlatDisc.Radius - 1, Across(under), 6);
        Assert.Equal(FlatFace.Bottom, farSide.Face);
        Assert.Equal(0, Across(farSide), 5);  // The underside's center
    }

    // Away from the top's center, where north swings round fast (like a globe's pole).
    [Theory]
    [InlineData(1.2, -0.8, 2.5, 0.7, FlatFace.Top)]
    [InlineData(2.8, 0.5, 2.9, 0.6, FlatFace.Bottom)]   // Over the rim and onto the underside
    [InlineData(2.9, 0.0, 3.1, 0.3, FlatFace.Rim)]      // Down the wall
    [InlineData(-2.0, 1.5, 1.5, 0.8, FlatFace.Top)]
    public void WalkingBackTheWayYouCame_ReturnsYouToTheStart(double x, double z,
        double bearing, double distance, FlatFace reached)
    {
        var start = new FlatSpot(FlatFace.Top, x, z);

        FlatSpot there = FlatWalk.Walk(start, bearing, distance);
        FlatSpot back = FlatWalk.Walk(there, bearing, -distance);

        Assert.Equal(reached, there.Face);
        Assert.Equal(FlatFace.Top, back.Face);
        Assert.True((FlatWalk.Point(back) - FlatWalk.Point(start)).Length < 1e-3,
            $"{back} against {start}");
    }

    [Fact]
    public void ADistanceWalked_IsTheDistanceCovered_OnTheTop()
    {
        var start = new FlatSpot(FlatFace.Top, -2, 1);

        FlatSpot end = FlatWalk.Walk(start, 0, 0.9);  // North: straight toward the center

        Assert.Equal(0.9, (FlatWalk.Point(end) - FlatWalk.Point(start)).Length, 2);
    }

    [Theory]
    [InlineData(FlatFace.Top, 1.0, 2.0)]
    [InlineData(FlatFace.Top, 0.0, 0.0)]
    [InlineData(FlatFace.Rim, 2.0, 0.01)]
    [InlineData(FlatFace.Bottom, -0.5, 1.5)]
    public void EveryFrame_IsSquare_AndEastCrossNorthIsUp(FlatFace face, double a, double b)
    {
        (Vector3D east, Vector3D north, Vector3D up) = FlatWalk.Frame(new FlatSpot(face, a, b));

        Assert.Equal(1, east.Length, 9);
        Assert.Equal(1, north.Length, 9);
        Assert.Equal(1, up.Length, 9);
        Assert.Equal(0, east.Dot(north), 9);
        Vector3D cross = new(east.Y * north.Z - east.Z * north.Y,
            east.Z * north.X - east.X * north.Z, east.X * north.Y - east.Y * north.X);
        Assert.True((cross - up).Length < 1e-9);
    }

    [Fact]
    public void OnTheTop_NorthPointsToTheCenter_AndUpIsUp()
    {
        (_, Vector3D north, Vector3D up) = FlatWalk.Frame(new FlatSpot(FlatFace.Top, 2, 0));

        Assert.True((north - new Vector3D(-1, 0, 0)).Length < 1e-9);
        Assert.True((up - new Vector3D(0, 1, 0)).Length < 1e-9);
    }

    [Fact]
    public void OnTheTop_ASpotsMapPoint_IsWhereTheDiscPutsIt()
    {
        var globeDirection = SphericalPolygon.ToUnit(new GeoCoordinate(30, 45));
        FlatSpot spot = FlatWalk.OnTop(FlatDisc.TopPointFor(globeDirection));

        Vector3D map = FlatWalk.MapDirection(spot)!.Value;

        Assert.True((map - globeDirection).Length < 1e-9);
        Assert.Null(FlatWalk.MapDirection(new FlatSpot(FlatFace.Bottom, 1, 1)));
        Assert.Null(FlatWalk.MapDirection(new FlatSpot(FlatFace.Rim, 1, 0)));
    }

    [Fact]
    public void PointsAbove_StandOutFromEachFace()
    {
        Assert.Equal(FlatDisc.HalfThickness + 0.1,
            FlatWalk.Point(new FlatSpot(FlatFace.Top, 1, 1), 0.1).Y, 9);
        Assert.Equal(-FlatDisc.HalfThickness - 0.1,
            FlatWalk.Point(new FlatSpot(FlatFace.Bottom, 1, 1), 0.1).Y, 9);
        Vector3D rim = FlatWalk.Point(new FlatSpot(FlatFace.Rim, 0, 0), 0.1);
        Assert.Equal(FlatDisc.Radius + 0.1, rim.Z, 9);
    }

    [Fact]
    public void WalkingNeedsFiniteNumbers()
    {
        Assert.Throws<ArgumentException>(() => FlatWalk.Walk(_center, 0, double.NaN));
    }

    private static double Across(FlatSpot spot) => Math.Sqrt(spot.A * spot.A + spot.B * spot.B);
}
