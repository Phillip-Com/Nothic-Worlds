using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Tests.Maps;

public class PieceStartingPlacementTests
{
    [Fact]
    public void GlobeMapCut_StartsWhereItAlreadyIs()
    {
        // On a 2:1 globe map, u 0.25–0.5 is 90°W–0° and v 0.4–0.6 is 18°N–18°S.
        PieceOutline cut = PieceOutline.Rectangle(new(0.25, 0.4), new(0.5, 0.6), 2.0);

        var placement = PieceStartingPlacement.FromMainMap(
            cut, MapProjection.Equirectangular, 2.0, calibration: null);

        Assert.NotNull(placement);
        Assert.Equal(0.0, placement.Value.Center.LatitudeDegrees, 1e-3);
        Assert.Equal(-45.0, placement.Value.Center.LongitudeDegrees, 1e-3);
        Assert.Equal(90.0, placement.Value.WidthDegrees, 1e-2);  // Along the equator
    }

    [Fact]
    public void CutAwayFromTheEquator_UsesTheTrueArc()
    {
        // Centered on 60°N: 60° of longitude there is only about 29.7° of arc.
        PieceOutline cut = PieceOutline.Rectangle(
            new(150.0 / 360.0, 0.15), new(210.0 / 360.0, 0.18333333333), 2.0);

        var placement = PieceStartingPlacement.FromMainMap(
            cut, MapProjection.Equirectangular, 2.0, calibration: null);

        Assert.NotNull(placement);
        Assert.Equal(60.0, placement.Value.Center.LatitudeDegrees, 1e-2);
        Assert.InRange(placement.Value.WidthDegrees, 29.0, 30.5);
    }

    [Fact]
    public void Calibration_IsUndone()
    {
        // 30°N is drawn where the map type puts 40°N; a cut centered on drawn 40°N is really
        // at 30°N.
        MapCalibration calibration = MapCalibration.CreateDefault();
        int index = calibration.Latitudes.ToList().FindIndex(g => g.Degrees == 30);
        calibration = calibration.WithLatitudeDrawnAs(index, 40);
        double v = 0.5 - 40.0 / 180.0;
        PieceOutline cut = PieceOutline.Rectangle(new(0.45, v - 0.01), new(0.55, v + 0.01), 2.0);

        var placement = PieceStartingPlacement.FromMainMap(
            cut, MapProjection.Equirectangular, 2.0, calibration);

        Assert.NotNull(placement);
        Assert.Equal(30.0, placement.Value.Center.LatitudeDegrees, 1e-3);
    }

    [Fact]
    public void CutCenteredOffTheMap_ReturnsNull()
    {
        // Robinson's top-left corner is outside the map's outline.
        PieceOutline cut = PieceOutline.Rectangle(new(0.0, 0.0), new(0.04, 0.04), 1.97);

        Assert.Null(PieceStartingPlacement.FromMainMap(
            cut, MapProjection.Robinson, 1.97, calibration: null));
    }

    [Theory]
    [InlineData(-10.0)]
    [InlineData(35.0)]
    [InlineData(88.0)]
    public void TrueLatitude_ReversesDrawnLatitude(double latitude)
    {
        MapCalibration c = MapCalibration.CreateDefault();
        c = c.WithLatitudeDrawnAs(c.Latitudes.ToList().FindIndex(g => g.Degrees == 30), 38);

        Assert.Equal(latitude, c.TrueLatitude(c.DrawnLatitude(latitude)), 1e-9);
    }

    [Theory]
    [InlineData(-179.5)]
    [InlineData(-45.0)]
    [InlineData(0.0)]
    [InlineData(178.0)]
    public void TrueLongitude_ReversesDrawnLongitude_AcrossTheSeam(double longitude)
    {
        MapCalibration c = MapCalibration.CreateDefault();
        c = c.WithLongitudeDrawnAs(0, 175);  // The 180° guide drawn 5° further west
        c = c.WithLongitudeDrawnAs(c.Longitudes.ToList().FindIndex(g => g.Degrees == 0), 8);

        double back = c.TrueLongitude(c.DrawnLongitude(longitude));

        Assert.Equal(0.0, SphericalCoordinates.LongitudeDelta(longitude, back), 1e-9);
    }
}
