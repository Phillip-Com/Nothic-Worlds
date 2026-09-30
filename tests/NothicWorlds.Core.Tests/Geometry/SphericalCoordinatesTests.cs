using System.Numerics;
using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public class SphericalCoordinatesTests
{
    private const double DegreeTolerance = 1e-4;
    private const float VectorTolerance = 1e-6f;

    [Theory]
    [InlineData(0, 0, 0, 0, 1)]      // Longitude 0 faces +Z
    [InlineData(0, 90, 1, 0, 0)]     // 90° east faces +X
    [InlineData(0, -90, -1, 0, 0)]   // 90° west faces -X
    [InlineData(0, -180, 0, 0, -1)]  // Date line faces -Z
    [InlineData(90, 0, 0, 1, 0)]     // North pole is +Y
    [InlineData(-90, 0, 0, -1, 0)]   // South pole is -Y
    public void ToDirection_KnownPoints(
        double latitude, double longitude, float x, float y, float z)
    {
        Vector3 direction = SphericalCoordinates.ToDirection(new GeoCoordinate(latitude, longitude));

        Assert.Equal(x, direction.X, VectorTolerance);
        Assert.Equal(y, direction.Y, VectorTolerance);
        Assert.Equal(z, direction.Z, VectorTolerance);
    }

    [Fact]
    public void ToDirection_IsUnitLength()
    {
        Vector3 direction = SphericalCoordinates.ToDirection(new GeoCoordinate(37.5, -122.25));

        Assert.Equal(1f, direction.Length(), VectorTolerance);
    }

    [Fact]
    public void FromDirection_RoundTripsAcrossTheGlobe()
    {
        for (int latitude = -80; latitude <= 80; latitude += 20)
        {
            for (int longitude = -180; longitude < 180; longitude += 20)
            {
                var original = new GeoCoordinate(latitude, longitude);

                GeoCoordinate result =
                    SphericalCoordinates.FromDirection(SphericalCoordinates.ToDirection(original));

                Assert.Equal(original.LatitudeDegrees, result.LatitudeDegrees, DegreeTolerance);
                Assert.Equal(
                    0.0,
                    SphericalCoordinates.LongitudeDelta(
                        original.LongitudeDegrees, result.LongitudeDegrees),
                    DegreeTolerance);
            }
        }
    }

    [Fact]
    public void FromDirection_AcceptsNonUnitVectors()
    {
        GeoCoordinate result = SphericalCoordinates.FromDirection(new Vector3(5, 0, 0));

        Assert.Equal(0.0, result.LatitudeDegrees, DegreeTolerance);
        Assert.Equal(90.0, result.LongitudeDegrees, DegreeTolerance);
    }

    [Fact]
    public void FromDirection_AtPole_ReturnsLongitudeZero()
    {
        GeoCoordinate result = SphericalCoordinates.FromDirection(Vector3.UnitY);

        Assert.Equal(90.0, result.LatitudeDegrees, DegreeTolerance);
        Assert.Equal(0.0, result.LongitudeDegrees, DegreeTolerance);
    }

    [Fact]
    public void FromDirection_ZeroVector_Throws()
    {
        Assert.Throws<ArgumentException>(() => SphericalCoordinates.FromDirection(Vector3.Zero));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(179.5, 179.5)]
    [InlineData(180, -180)]
    [InlineData(-180, -180)]
    [InlineData(190, -170)]
    [InlineData(-190, 170)]
    [InlineData(540, -180)]
    [InlineData(720, 0)]
    [InlineData(-1e-15, -1e-15)]
    public void WrapLongitude_WrapsIntoRange(double input, double expected)
    {
        double result = SphericalCoordinates.WrapLongitude(input);

        Assert.Equal(expected, result, 1e-9);
        Assert.InRange(result, -180.0, 179.999999999);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void WrapLongitude_NonFinite_Throws(double input)
    {
        Assert.Throws<ArgumentException>(() => SphericalCoordinates.WrapLongitude(input));
    }

    [Theory]
    [InlineData(10, 20, 10)]
    [InlineData(20, 10, -10)]
    [InlineData(170, -170, 20)]   // East across the date line
    [InlineData(-170, 170, -20)]  // West across the date line
    public void LongitudeDelta_TakesShortestPath(double from, double to, double expected)
    {
        Assert.Equal(expected, SphericalCoordinates.LongitudeDelta(from, to), 1e-9);
    }
}
