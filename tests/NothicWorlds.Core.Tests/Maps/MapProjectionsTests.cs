using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Tests.Maps;

public class MapProjectionsTests
{
    private const double Tolerance = 1e-4;

    [Theory]
    [InlineData(1.0, 85.0511)]  // Square map: the classic web-map limit
    [InlineData(2.0, 66.5133)]  // 2:1 reaches about the Arctic Circle
    [InlineData(3.0, 51.3260)]  // Wider maps cover less latitude
    public void MercatorLatitudeLimit_KnownShapes(double aspectRatio, double expectedDegrees)
    {
        Assert.Equal(expectedDegrees, MapProjections.MercatorLatitudeLimit(aspectRatio), 3);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    [InlineData(double.NaN)]
    public void InvalidAspectRatio_Throws(double aspectRatio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MapProjections.MercatorLatitudeLimit(aspectRatio));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MapProjections.ToImagePosition(
                new GeoCoordinate(0, 0), MapProjection.Mercator, aspectRatio));
    }

    [Theory]
    [InlineData(0, 0, 0.5, 0.5)]
    [InlineData(90, 0, 0.5, 0.0)]    // North pole is the top edge
    [InlineData(-90, 0, 0.5, 1.0)]   // South pole is the bottom edge
    [InlineData(0, -180, 0.0, 0.5)]  // Left edge
    [InlineData(45, 90, 0.75, 0.25)]
    public void Equirectangular_KnownPoints(double latitude, double longitude, double u, double v)
    {
        MapImagePosition position = MapProjections.ToImagePosition(
            new GeoCoordinate(latitude, longitude), MapProjection.Equirectangular, 2.0);

        Assert.Equal(u, position.U, Tolerance);
        Assert.Equal(v, position.V, Tolerance);
        Assert.False(position.IsOutsideMap);
    }

    [Fact]
    public void Mercator_EquatorIsCentered()
    {
        MapImagePosition position = MapProjections.ToImagePosition(
            new GeoCoordinate(0, 0), MapProjection.Mercator, 2.0);

        Assert.Equal(0.5, position.U, Tolerance);
        Assert.Equal(0.5, position.V, Tolerance);
        Assert.False(position.IsOutsideMap);
    }

    [Fact]
    public void Mercator_KnownMidLatitude()
    {
        // y = ln(tan(45° + 22.5°)) = 0.88137; v = 0.5 - 0.88137 / 2π × 2
        MapImagePosition position = MapProjections.ToImagePosition(
            new GeoCoordinate(45, 0), MapProjection.Mercator, 2.0);

        Assert.Equal(0.21945, position.V, Tolerance);
        Assert.False(position.IsOutsideMap);
    }

    [Fact]
    public void Mercator_IsNorthSouthSymmetric()
    {
        MapImagePosition north = MapProjections.ToImagePosition(
            new GeoCoordinate(30, 0), MapProjection.Mercator, 2.0);
        MapImagePosition south = MapProjections.ToImagePosition(
            new GeoCoordinate(-30, 0), MapProjection.Mercator, 2.0);

        Assert.Equal(1.0 - north.V, south.V, Tolerance);
    }

    [Fact]
    public void Mercator_LatitudeLimitIsTheImageEdge()
    {
        double limit = MapProjections.MercatorLatitudeLimit(2.0);

        MapImagePosition justInside = MapProjections.ToImagePosition(
            new GeoCoordinate(limit - 0.01, 0), MapProjection.Mercator, 2.0);
        MapImagePosition justOutside = MapProjections.ToImagePosition(
            new GeoCoordinate(limit + 0.01, 0), MapProjection.Mercator, 2.0);

        Assert.False(justInside.IsOutsideMap);
        Assert.Equal(0.0, justInside.V, 1e-3);
        Assert.True(justOutside.IsOutsideMap);
    }

    [Theory]
    [InlineData(80, 0.0)]    // Beyond coverage: clamped to the top edge
    [InlineData(90, 0.0)]    // The pole itself: no infinity or NaN
    [InlineData(-80, 1.0)]
    [InlineData(-90, 1.0)]
    public void Mercator_BeyondCoverage_ClampsToEdge(double latitude, double expectedV)
    {
        MapImagePosition position = MapProjections.ToImagePosition(
            new GeoCoordinate(latitude, 0), MapProjection.Mercator, 2.0);

        Assert.True(position.IsOutsideMap);
        Assert.Equal(expectedV, position.V);
    }

    [Theory]
    [InlineData(-180.0)]
    [InlineData(-45.0)]
    [InlineData(120.0)]
    public void HorizontalPosition_IsTheSameForBothProjections(double longitude)
    {
        var coordinate = new GeoCoordinate(20, longitude);

        MapImagePosition globe =
            MapProjections.ToImagePosition(coordinate, MapProjection.Equirectangular, 2.0);
        MapImagePosition flat =
            MapProjections.ToImagePosition(coordinate, MapProjection.Mercator, 2.0);

        Assert.Equal(globe.U, flat.U, Tolerance);
    }
}
