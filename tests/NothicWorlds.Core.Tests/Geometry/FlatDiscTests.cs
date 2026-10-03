using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class FlatDiscTests
{
    [Fact]
    public void TheNorthPole_IsAtTheCenterOfTheTopFace()
    {
        Vector3D center = FlatDisc.TopPointFor(new Vector3D(0, 1, 0));

        Assert.Equal(new Vector3D(0, FlatDisc.HalfThickness, 0), center);
    }

    [Fact]
    public void TheFarSouth_IsAtTheRim()
    {
        Vector3D nearSouth = FlatDisc.TopPointFor(Direction(-89.999, 30));

        Assert.Equal(FlatDisc.Radius, Across(nearSouth), 3);
    }

    [Theory]
    [InlineData(60, 0)]
    [InlineData(0, 90)]
    [InlineData(-45, -120)]
    [InlineData(10, 179)]
    public void DistanceFromTheCenter_IsTheAngleFromTheNorthPole(double latitude, double longitude)
    {
        Vector3D point = FlatDisc.TopPointFor(Direction(latitude, longitude));

        Assert.Equal(double.DegreesToRadians(90 - latitude), Across(point), 9);
    }

    [Fact]
    public void Longitude_RunsAroundTheCenterAsOnAGlobe()
    {
        Vector3D zero = FlatDisc.TopPointFor(Direction(0, 0));
        Vector3D east = FlatDisc.TopPointFor(Direction(0, 90));

        Assert.True(zero.Z > 0 && Math.Abs(zero.X) < 1e-9);  // Longitude 0 toward +Z
        Assert.True(east.X > 0 && Math.Abs(east.Z) < 1e-9);  // 90° east toward +X
    }

    [Theory]
    [InlineData(89.5, 10)]
    [InlineData(23.4, -77)]
    [InlineData(-30, 150)]
    [InlineData(-85, -5)]
    public void PointsAndDirections_RoundTrip(double latitude, double longitude)
    {
        Vector3D direction = Direction(latitude, longitude);

        Vector3D back = FlatDisc.DirectionFor(FlatDisc.TopPointFor(direction));

        Assert.True((back - direction).Length < 1e-9);
    }

    [Fact]
    public void OnlyPlanetsAndMoons_CanBeFlat()
    {
        var planet = new Body { Kind = BodyKind.Planet, Shape = BodyShape.FlatDisc };
        var star = new Body { Kind = BodyKind.Star, Shape = BodyShape.FlatDisc };

        Assert.Null(planet.Problem());
        Assert.NotNull(star.Problem());
        Assert.Equal(BodyShape.FlatDisc, planet.Clone().Shape);
        Assert.False(planet.HasSameContent(new Body { Id = planet.Id, Kind = BodyKind.Planet }));
    }

    // SphericalCoordinates.ToDirection's convention, in double precision for exact checks.
    private static Vector3D Direction(double latitude, double longitude)
    {
        double lat = double.DegreesToRadians(latitude);
        double lon = double.DegreesToRadians(longitude);
        return new Vector3D(Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat),
            Math.Cos(lat) * Math.Cos(lon));
    }

    private static double Across(Vector3D point) =>
        Math.Sqrt(point.X * point.X + point.Z * point.Z);
}
