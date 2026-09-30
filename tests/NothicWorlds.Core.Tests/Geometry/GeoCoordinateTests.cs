using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Tests.Geometry;

public class GeoCoordinateTests
{
    [Theory]
    [InlineData(95, 90)]
    [InlineData(-95, -90)]
    [InlineData(45, 45)]
    public void Constructor_ClampsLatitude(double input, double expected)
    {
        Assert.Equal(expected, new GeoCoordinate(input, 0).LatitudeDegrees);
    }

    [Fact]
    public void Constructor_WrapsLongitude()
    {
        Assert.Equal(-170.0, new GeoCoordinate(0, 190).LongitudeDegrees, 1e-9);
    }

    [Fact]
    public void Constructor_NaNLatitude_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GeoCoordinate(double.NaN, 0));
    }

    [Fact]
    public void Constructor_NaNLongitude_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GeoCoordinate(0, double.NaN));
    }

    [Fact]
    public void Equality_ComparesValues()
    {
        Assert.Equal(new GeoCoordinate(10, 180), new GeoCoordinate(10, -180));
    }
}
