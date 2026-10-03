using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public class SphericalPolygonTests
{
    private static readonly GeoCoordinate[] _square =
        [new(10, -35), new(10, -25), new(16, -25), new(16, -35)];

    [Fact]
    public void ASpotInside_IsContained_AndOneOutsideIsNot()
    {
        Assert.True(SphericalPolygon.Contains(_square, new GeoCoordinate(13, -30)));
        Assert.False(SphericalPolygon.Contains(_square, new GeoCoordinate(20, -30)));
        Assert.False(SphericalPolygon.Contains(_square, new GeoCoordinate(13, -40)));
    }

    [Fact]
    public void TheWayRoundTheCornersGo_DoesntMatter()
    {
        GeoCoordinate[] reversed = [.. _square.Reverse()];

        Assert.True(SphericalPolygon.Contains(reversed, new GeoCoordinate(13, -30)));
        Assert.False(SphericalPolygon.Contains(reversed, new GeoCoordinate(20, -30)));
    }

    [Fact]
    public void AnOutlineAcrossTheDateLine_Works()
    {
        GeoCoordinate[] islands = [new(-5, 175), new(-5, -175), new(5, -175), new(5, 175)];

        Assert.True(SphericalPolygon.Contains(islands, new GeoCoordinate(0, -180)));
        Assert.True(SphericalPolygon.Contains(islands, new GeoCoordinate(0, 178)));
        Assert.False(SphericalPolygon.Contains(islands, new GeoCoordinate(0, 170)));
    }

    [Fact]
    public void AnOutlineAroundAPole_ContainsThePole()
    {
        GeoCoordinate[] cap = [new(80, 0), new(80, 90), new(80, 180), new(80, -90)];

        Assert.True(SphericalPolygon.Contains(cap, new GeoCoordinate(90, 0)));
        Assert.True(SphericalPolygon.Contains(cap, new GeoCoordinate(85, 45)));
        Assert.False(SphericalPolygon.Contains(cap, new GeoCoordinate(70, 0)));
    }

    [Fact]
    public void TheFarSideOfTheGlobe_IsNeverInside()
    {
        Assert.False(SphericalPolygon.Contains(_square, new GeoCoordinate(-13, 150)));
    }

    [Fact]
    public void EdgesFollowGreatCircles()
    {
        // Two corners on the equator: the edge between them stays on it.
        GeoCoordinate[] triangle = [new(0, 0), new(0, 40), new(30, 20)];

        List<GeoCoordinate> path = SphericalPolygon.EdgePath(triangle, maxStepDegrees: 2);

        Assert.Equal(triangle[0], path[0]);
        Assert.Equal(triangle[0], path[^1]);  // Closed
        IEnumerable<GeoCoordinate> firstEdge = path.TakeWhile(p => p.LongitudeDegrees < 39.99);
        Assert.All(firstEdge, p => Assert.Equal(0, p.LatitudeDegrees, 1e-9));
        for (int i = 1; i < path.Count; i++)
        {
            Assert.InRange(SphericalCoordinates.ArcDegrees(path[i - 1], path[i]), 0, 2.0001);
        }
    }

    [Fact]
    public void OutlinesSpreadRoundTheGlobe_DontFitInAHemisphere()
    {
        GeoCoordinate[] belt = [new(0, 0), new(0, 120), new(0, -120)];
        GeoCoordinate[] huge = [new(-60, -100), new(-60, 100), new(60, 100), new(60, -100)];

        Assert.False(SphericalPolygon.FitsInHemisphere(belt));
        Assert.False(SphericalPolygon.FitsInHemisphere(huge));
        Assert.True(SphericalPolygon.FitsInHemisphere(_square));
        Assert.False(SphericalPolygon.Contains(huge, new GeoCoordinate(0, 0)));
    }

    [Fact]
    public void DirectionsAndCoordinates_RoundTrip()
    {
        var spot = new GeoCoordinate(-33.25, 151.5);

        GeoCoordinate back = SphericalPolygon.FromUnit(SphericalPolygon.ToUnit(spot));

        Assert.Equal(spot.LatitudeDegrees, back.LatitudeDegrees, 1e-9);
        Assert.Equal(spot.LongitudeDegrees, back.LongitudeDegrees, 1e-9);
        // The same axes as the single-precision helper.
        var single = SphericalCoordinates.ToDirection(spot);
        var precise = SphericalPolygon.ToUnit(spot);
        Assert.Equal(single.X, precise.X, 1e-6);
        Assert.Equal(single.Z, precise.Z, 1e-6);
    }
}
