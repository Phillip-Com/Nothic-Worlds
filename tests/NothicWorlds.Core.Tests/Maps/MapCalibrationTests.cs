using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Tests.Maps;

public class MapCalibrationTests
{
    private const double Tolerance = 1e-9;

    private static int LatIndex(MapCalibration c, double degrees) =>
        c.Latitudes.ToList().FindIndex(g => g.Degrees == degrees);

    private static int LonIndex(MapCalibration c, double degrees) =>
        c.Longitudes.ToList().FindIndex(g => g.Degrees == degrees);

    [Fact]
    public void Default_HasGuidesEvery30And60Degrees_AndChangesNothing()
    {
        MapCalibration calibration = MapCalibration.CreateDefault();

        Assert.Equal([-60.0, -30, 0, 30, 60], calibration.Latitudes.Select(g => g.Degrees));
        Assert.Equal(
            [-180.0, -120, -60, 0, 60, 120], calibration.Longitudes.Select(g => g.Degrees));
        Assert.True(calibration.IsIdentity);
        foreach (double x in new[] { -90.0, -47.3, 0, 12.5, 89, 90 })
        {
            Assert.Equal(x, calibration.DrawnLatitude(x), Tolerance);
        }

        foreach (double x in new[] { -180.0, -91.2, 0, 33.3, 179.9 })
        {
            Assert.Equal(x, calibration.DrawnLongitude(x), Tolerance);
        }
    }

    [Fact]
    public void MovingALatitudeGuide_PassesThroughIt_AndKeepsPolesFixed()
    {
        MapCalibration start = MapCalibration.CreateDefault();

        MapCalibration moved = start.WithLatitudeDrawnAs(LatIndex(start, 30), 35);

        Assert.False(moved.IsIdentity);
        Assert.Equal(35.0, moved.DrawnLatitude(30), Tolerance);
        Assert.Equal(0.0, moved.DrawnLatitude(0), Tolerance);  // Other guides unaffected
        Assert.Equal(90.0, moved.DrawnLatitude(90), Tolerance);
        Assert.Equal(-90.0, moved.DrawnLatitude(-90), Tolerance);
    }

    [Fact]
    public void Curves_NeverReverse_EvenWithLargeMoves()
    {
        MapCalibration c = MapCalibration.CreateDefault();
        c = c.WithLatitudeDrawnAs(LatIndex(c, 30), 55);   // Squeezed toward 60
        c = c.WithLatitudeDrawnAs(LatIndex(c, -60), -35); // Squeezed toward -30
        c = c.WithLongitudeDrawnAs(LonIndex(c, 0), 50);   // Squeezed toward 60

        double previousLat = double.NegativeInfinity;
        for (double lat = -90; lat <= 90; lat += 0.25)
        {
            double drawn = c.DrawnLatitude(lat);
            Assert.True(drawn > previousLat, $"Latitude curve reversed at {lat}");
            previousLat = drawn;
        }

        double previousLon = double.NegativeInfinity;
        float[] table = c.BakeLongitudeTable(1441);
        foreach (float drawn in table)
        {
            Assert.True(drawn > previousLon, "Longitude curve reversed");
            previousLon = drawn;
        }
    }

    [Fact]
    public void MovingAGuide_IsKeptBetweenItsNeighbors()
    {
        MapCalibration start = MapCalibration.CreateDefault();

        MapCalibration moved = start.WithLatitudeDrawnAs(LatIndex(start, 30), 80);

        Assert.Equal(60 - MapCalibration.MinimumGapDegrees, moved.DrawnLatitude(30), Tolerance);
    }

    [Fact]
    public void MovingALongitudeGuide_PassesThroughIt()
    {
        MapCalibration start = MapCalibration.CreateDefault();

        MapCalibration moved = start.WithLongitudeDrawnAs(LonIndex(start, 0), 10);

        Assert.Equal(10.0, moved.DrawnLongitude(0), Tolerance);
        Assert.Equal(60.0, moved.DrawnLongitude(60), Tolerance);
    }

    [Fact]
    public void LongitudeWrapsSmoothlyAcross180()
    {
        MapCalibration start = MapCalibration.CreateDefault();

        // The 180° guide is drawn 5° further west, i.e. at 175° (-185° unwrapped).
        MapCalibration moved = start.WithLongitudeDrawnAs(LonIndex(start, -180), 175);

        Assert.Equal(175.0, moved.DrawnLongitude(-180), Tolerance);
        Assert.Equal(175.0, moved.DrawnLongitude(180), Tolerance);  // Same meridian
        double justWest = moved.DrawnLongitude(179.9);
        double justEast = moved.DrawnLongitude(-179.9);
        Assert.InRange(SphericalCoordinates.LongitudeDelta(justWest, justEast), 0.0, 0.5);
    }

    [Fact]
    public void LongitudeGuideAt180_CanMoveEastAcrossTheSeamToo()
    {
        MapCalibration start = MapCalibration.CreateDefault();

        MapCalibration moved = start.WithLongitudeDrawnAs(LonIndex(start, -180), -175);

        Assert.Equal(-175.0, moved.DrawnLongitude(180), Tolerance);
    }

    [Fact]
    public void AddingAGuide_StartsWhereThatLatitudeIsDrawn()
    {
        MapCalibration c = MapCalibration.CreateDefault();
        c = c.WithLatitudeDrawnAs(LatIndex(c, 30), 36);
        double before = c.DrawnLatitude(45);

        MapCalibration added = c.AddLatitude(45);

        Assert.Equal(6, added.Latitudes.Count);
        Assert.Equal(before, added.DrawnLatitude(45), Tolerance);
        for (double lat = -90; lat <= 90; lat += 1)
        {
            Assert.Equal(c.DrawnLatitude(lat), added.DrawnLatitude(lat), 0.5);  // Barely changes
        }
    }

    [Fact]
    public void AddingAGuideNextToAnother_OrAtAPole_DoesNothing()
    {
        MapCalibration c = MapCalibration.CreateDefault();

        Assert.Same(c, c.AddLatitude(30.2));
        Assert.Same(c, c.AddLatitude(90));
        Assert.Same(c, c.AddLongitude(-179.9));  // Next to -180 across the seam
    }

    [Fact]
    public void AddingALongitudeGuide_KeepsOrder()
    {
        MapCalibration c = MapCalibration.CreateDefault().AddLongitude(90);

        Assert.Equal(
            [-180.0, -120, -60, 0, 60, 90, 120], c.Longitudes.Select(g => g.Degrees));
        Assert.True(c.IsIdentity);
    }

    [Fact]
    public void RemovingGuides()
    {
        MapCalibration c = MapCalibration.CreateDefault();

        Assert.Equal(4, c.RemoveLatitude(0).Latitudes.Count);
        Assert.Equal(5, c.RemoveLongitude(0).Longitudes.Count);

        MapCalibration single = MapCalibration.Create([], [new CalibrationGuide(0, 5)]);
        Assert.Same(single, single.RemoveLongitude(0));  // The last one stays
        Assert.Equal(5.0, single.DrawnLongitude(0), Tolerance);
        Assert.Equal(-175.0, single.DrawnLongitude(180), Tolerance);  // Whole map shifted 5°
    }

    [Fact]
    public void Create_SortsGuides()
    {
        MapCalibration c = MapCalibration.Create(
            [new(30, 32), new(-30, -28)], [new(60, 61), new(-60, -59)]);

        Assert.Equal([-30.0, 30], c.Latitudes.Select(g => g.Degrees));
        Assert.Equal([-60.0, 60], c.Longitudes.Select(g => g.Degrees));
    }

    [Theory]
    [MemberData(nameof(InvalidCalibrations))]
    public void Create_RejectsInvalidGuides(
        CalibrationGuide[] latitudes, CalibrationGuide[] longitudes)
    {
        Assert.Throws<ArgumentException>(() => MapCalibration.Create(latitudes, longitudes));
    }

    public static TheoryData<CalibrationGuide[], CalibrationGuide[]> InvalidCalibrations => new()
    {
        { [new(30, 40), new(40, 35)], [] },            // Latitudes drawn out of order
        { [new(90, 90)], [] },                          // At the pole
        { [new(30, 95)], [] },                          // Drawn beyond the pole
        { [new(double.NaN, 0)], [] },                   // Not a number
        { [], [new(180, 180)] },                        // Longitude must be below 180
        { [], [new(0, 200)] },                          // Moved half a turn or more
        { [], [new(-60, 10), new(60, 0)] },             // Longitudes drawn out of order
        { [], [new(-170, -100), new(170, 270)] },       // Out of order across 180°
    };

    [Fact]
    public void BakedTables_SpanTheWholeRange()
    {
        MapCalibration c = MapCalibration.CreateDefault();

        float[] latitudes = c.BakeLatitudeTable(181);
        float[] longitudes = c.BakeLongitudeTable(361);

        Assert.Equal(181, latitudes.Length);
        Assert.Equal(-90f, latitudes[0], 1e-4f);
        Assert.Equal(0f, latitudes[90], 1e-4f);
        Assert.Equal(90f, latitudes[180], 1e-4f);
        Assert.Equal(-180f, longitudes[0], 1e-4f);
        Assert.Equal(180f, longitudes[360], 1e-4f);  // Unwrapped for smooth blending
    }
}
