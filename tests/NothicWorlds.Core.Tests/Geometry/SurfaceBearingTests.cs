using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Geometry;

public class SurfaceBearingTests
{
    [Theory]
    [InlineData(1, 0, 0)]    // North
    [InlineData(0, 1, 90)]   // East
    [InlineData(-1, 0, 180)] // South
    [InlineData(0, -1, 270)] // West
    public void OnGlobe_FromTheEquator_PointsTheRightWay(double north, double east,
        double bearing)
    {
        Vector3D spot = SphericalPolygon.ToUnit(new GeoCoordinate(0, 0));

        SurfaceBearing.Toward toward = SurfaceBearing.OnGlobe(spot,
            new GeoCoordinate(north, east), 6_371);

        Assert.Equal(bearing, toward.BearingDegrees, 6);
    }

    [Fact]
    public void OnGlobe_Distance_MatchesSurfaceDistance()
    {
        var from = new GeoCoordinate(39, -51.75);
        var to = new GeoCoordinate(39.2, -51.5);

        SurfaceBearing.Toward toward = SurfaceBearing.OnGlobe(SphericalPolygon.ToUnit(from),
            to, 6_371);

        Assert.Equal(SurfaceDistance.Km(BodyShape.Sphere, 6_371, from, to),
            toward.Km, 6);
    }

    [Fact]
    public void OnFlat_TowardTheCenter_IsNorth()
    {
        FlatSpot spot = FlatWalk.OnTop(FlatDisc.TopPointFor(
            SphericalPolygon.ToUnit(new GeoCoordinate(0, 0))));

        SurfaceBearing.Toward? toward = SurfaceBearing.OnFlat(spot,
            new GeoCoordinate(45, 0), 6_371);

        Assert.NotNull(toward);
        Assert.Equal(0, toward.Value.BearingDegrees, 6);
        Assert.Equal(SurfaceDistance.Km(BodyShape.FlatDisc, 6_371,
            new GeoCoordinate(0, 0), new GeoCoordinate(45, 0)), toward.Value.Km, 6);
    }

    [Fact]
    public void OnFlat_OffTheTopFace_HasNoWay()
    {
        var rim = new FlatSpot(FlatFace.Rim, 0, 0);

        Assert.Null(SurfaceBearing.OnFlat(rim, new GeoCoordinate(45, 0), 6_371));
    }
}
