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

    // ----- Shape checks -----

    [Theory]
    [InlineData(2048, 1024)]
    [InlineData(2048, 1025)]  // Within 1% of 2:1
    public void ShapeMatches_GlobeMap_TwoToOne(int width, int height)
    {
        Assert.True(
            MapProjections.ShapeMatches(MapProjection.Equirectangular, (double)width / height));
    }

    [Theory]
    [InlineData(1000, 1000)]  // Square
    [InlineData(3000, 1000)]  // 3:1
    [InlineData(2048, 1060)]  // About 3.5% off: too far for a Globe map
    [InlineData(1024, 2048)]  // Upright 1:2
    public void ShapeMatches_GlobeMap_OtherShapes(int width, int height)
    {
        Assert.False(
            MapProjections.ShapeMatches(MapProjection.Equirectangular, (double)width / height));
    }

    [Fact]
    public void ShapeMatches_FlatMap_AcceptsAnyShape()
    {
        Assert.True(MapProjections.ShapeMatches(MapProjection.Mercator, 3.7));
        Assert.Null(MapProjections.ExpectedAspectRatio(MapProjection.Mercator));
    }

    [Theory]
    [InlineData(MapProjection.Robinson, 1.97166)]
    [InlineData(MapProjection.WinkelTripel, 1.63662)]
    [InlineData(MapProjection.Mollweide, 2.0)]
    [InlineData(MapProjection.GallPeters, 1.57080)]
    [InlineData(MapProjection.Polar, 1.0)]
    [InlineData(MapProjection.TwoHemispheres, 2.0)]
    public void ExpectedAspectRatio_AtlasTypes(MapProjection projection, double expected)
    {
        Assert.Equal(expected, MapProjections.ExpectedAspectRatio(projection)!.Value, Tolerance);
    }

    [Theory]
    [InlineData(2.0, true)]    // A 2:1 export of a Robinson template: 1.4% off, fine
    [InlineData(1.85, false)]  // 6% off
    public void ShapeMatches_AtlasTypes_AllowFivePercent(double aspectRatio, bool expected)
    {
        Assert.Equal(expected, MapProjections.ShapeMatches(MapProjection.Robinson, aspectRatio));
    }

    [Theory]
    [InlineData(MapProjection.Equirectangular, true)]
    [InlineData(MapProjection.Mercator, false)]  // Polar caps
    [InlineData(MapProjection.Robinson, true)]
    [InlineData(MapProjection.WinkelTripel, true)]
    [InlineData(MapProjection.Mollweide, true)]
    [InlineData(MapProjection.GallPeters, true)]
    [InlineData(MapProjection.Polar, false)]     // Southern hemisphere
    [InlineData(MapProjection.TwoHemispheres, true)]
    public void CoversWholeGlobe(MapProjection projection, bool expected)
    {
        Assert.Equal(expected, MapProjections.CoversWholeGlobe(projection));
    }

    // ----- Atlas and circular projections -----
    // Reference values computed independently (separate script), not with this code.

    [Theory]
    [InlineData(MapProjection.Robinson, 45, 90, 0.72405, 0.22145)]
    [InlineData(MapProjection.WinkelTripel, 45, 90, 0.71484, 0.23892)]
    [InlineData(MapProjection.Mollweide, 45, 90, 0.70148, 0.20398)]
    [InlineData(MapProjection.GallPeters, 45, 90, 0.75, 0.14645)]
    [InlineData(MapProjection.TwoHemispheres, 30, 45, 0.63760, 0.31645)]
    public void KnownMidPoints(
        MapProjection projection, double latitude, double longitude, double u, double v)
    {
        MapImagePosition position = Project(projection, latitude, longitude);

        Assert.Equal(u, position.U, Tolerance);
        Assert.Equal(v, position.V, Tolerance);
        Assert.False(position.IsOutsideMap);
    }

    [Theory]
    [InlineData(MapProjection.Robinson)]
    [InlineData(MapProjection.WinkelTripel)]
    [InlineData(MapProjection.Mollweide)]
    [InlineData(MapProjection.GallPeters)]
    public void RectangularAtlasTypes_FillTheImage(MapProjection projection)
    {
        // Center, left/right ends of the equator, and both poles touch the image's edges.
        AssertAt(Project(projection, 0, 0), 0.5, 0.5);
        AssertAt(Project(projection, 0, -180), 0.0, 0.5);
        AssertAt(Project(projection, 0, 179.9999), 1.0, 0.5);
        Assert.Equal(0.0, Project(projection, 90, 0).V, Tolerance);
        Assert.Equal(1.0, Project(projection, -90, 0).V, Tolerance);
    }

    [Theory]
    [InlineData(MapProjection.Robinson)]
    [InlineData(MapProjection.WinkelTripel)]
    [InlineData(MapProjection.Mollweide)]
    [InlineData(MapProjection.GallPeters)]
    [InlineData(MapProjection.TwoHemispheres)]
    public void AtlasTypes_AreNorthSouthSymmetric(MapProjection projection)
    {
        MapImagePosition north = Project(projection, 35, -60);
        MapImagePosition south = Project(projection, -35, -60);

        Assert.Equal(north.U, south.U, Tolerance);
        Assert.Equal(1.0 - north.V, south.V, Tolerance);
    }

    [Fact]
    public void Mollweide_NearThePole_StaysFinite()
    {
        MapImagePosition position = Project(MapProjection.Mollweide, 89.99, 120);

        Assert.True(double.IsFinite(position.U) && double.IsFinite(position.V));
        Assert.InRange(position.V, 0.0, 0.01);
    }

    [Theory]
    [InlineData(90, 0, 0.5, 0.5)]    // North pole at the center
    [InlineData(0, 0, 0.5, 1.0)]     // Longitude 0 at the bottom of the rim
    [InlineData(0, 90, 1.0, 0.5)]    // 90°E on the right
    [InlineData(0, -90, 0.0, 0.5)]   // 90°W on the left
    [InlineData(45, 180, 0.5, 0.25)] // Halfway out, toward the top
    public void Polar_KnownPoints(double latitude, double longitude, double u, double v)
    {
        MapImagePosition position = Project(MapProjection.Polar, latitude, longitude);

        AssertAt(position, u, v);
        Assert.False(position.IsOutsideMap);
    }

    [Fact]
    public void Polar_SouthernHemisphere_IsOutsideAtTheRim()
    {
        MapImagePosition position = Project(MapProjection.Polar, -30, 90);

        Assert.True(position.IsOutsideMap);
        AssertAt(position, 1.0, 0.5);  // The nearest edge: the equator's rim
    }

    [Theory]
    [InlineData(0, -90, 0.25, 0.5)]  // Center of the western circle
    [InlineData(0, 90, 0.75, 0.5)]   // Center of the eastern circle
    [InlineData(0, -180, 0.0, 0.5)]  // Western circle's left edge
    [InlineData(0, 0, 0.5, 0.5)]     // Longitude 0 starts the eastern circle's left edge
    [InlineData(90, 90, 0.75, 0.0)]  // North pole at the top of each circle
    public void TwoHemispheres_KnownPoints(double latitude, double longitude, double u, double v)
    {
        AssertAt(Project(MapProjection.TwoHemispheres, latitude, longitude), u, v);
    }

    private static MapImagePosition Project(
        MapProjection projection, double latitude, double longitude)
    {
        return MapProjections.ToImagePosition(
            new GeoCoordinate(latitude, longitude), projection, aspectRatio: 2.0);
    }

    private static void AssertAt(MapImagePosition position, double u, double v)
    {
        Assert.Equal(u, position.U, Tolerance);
        Assert.Equal(v, position.V, Tolerance);
    }
}
