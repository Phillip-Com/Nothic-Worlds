using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public class GlobeWalkTests
{
    private static readonly Vector3D _equatorAtZero = SphericalPolygon.ToUnit(new(0, 0));

    [Fact]
    public void WalkingEast_AlongTheEquator_GainsLongitude()
    {
        Vector3D end = GlobeWalk.Walk(_equatorAtZero, Math.PI / 2, double.DegreesToRadians(10));

        GeoCoordinate place = SphericalPolygon.FromUnit(end);
        Assert.Equal(0, place.LatitudeDegrees, 6);
        Assert.Equal(10, place.LongitudeDegrees, 6);
    }

    [Fact]
    public void WalkingNorth_GainsLatitude()
    {
        Vector3D end = GlobeWalk.Walk(_equatorAtZero, 0, double.DegreesToRadians(25));

        Assert.Equal(25, SphericalPolygon.FromUnit(end).LatitudeDegrees, 6);
    }

    [Fact]
    public void APointSeenFromOverhead_IsAsFarAsItsArc()
    {
        // Half a radius out on a map drawn straight down is an arc of 30 degrees.
        Vector3D end = GlobeWalk.FromOverhead(_equatorAtZero, Math.PI / 2, 0.5);

        Assert.Equal(30, SphericalPolygon.FromUnit(end).LongitudeDegrees, 6);
    }

    [Fact]
    public void PastTheGlobesEdge_IsTheEdge()
    {
        Vector3D end = GlobeWalk.FromOverhead(_equatorAtZero, Math.PI / 2, 3);

        Assert.Equal(90, SphericalPolygon.FromUnit(end).LongitudeDegrees, 6);
    }

    [Fact]
    public void AtAPole_ThereIsStillAWayToGo()
    {
        Vector3D end = GlobeWalk.Walk(new Vector3D(0, 1, 0), 0, double.DegreesToRadians(10));

        Assert.Equal(80, SphericalPolygon.FromUnit(end).LatitudeDegrees, 6);
    }
}
