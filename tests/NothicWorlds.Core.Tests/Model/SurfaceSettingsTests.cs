using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class SurfaceSettingsTests
{
    [Fact]
    public void RestoreFrom_PutsBackEverything()
    {
        SurfaceSettings before = Sample();
        SurfaceSettings snapshot = before.Clone();
        before.Map = null;
        before.FillColor = new RgbColor(0, 0, 0);
        before.Pieces[0].Center = new GeoCoordinate(-40, 90);
        before.Pieces.Add(Piece("Extra"));

        before.RestoreFrom(snapshot);

        Assert.Equal("assets/map.png", before.Map!.AssetName);
        Assert.Equal(MapProjection.Robinson, before.Map.Projection);
        Assert.Same(snapshot.Map!.Calibration, before.Map.Calibration);  // Immutable, shared
        Assert.Equal(new RgbColor(1, 2, 3), before.FillColor);
        MapPiece piece = Assert.Single(before.Pieces);
        Assert.Equal("First", piece.Name);
        Assert.Equal(new GeoCoordinate(10, 20), piece.Center);
    }

    [Fact]
    public void Clone_IsIndependentOfTheOriginal()
    {
        SurfaceSettings original = Sample();
        SurfaceSettings copy = original.Clone();

        original.Map!.Projection = MapProjection.Polar;
        original.Pieces[0].Name = "Renamed";
        original.Pieces.Clear();

        Assert.Equal(MapProjection.Robinson, copy.Map!.Projection);
        Assert.Equal("First", Assert.Single(copy.Pieces).Name);
    }

    [Fact]
    public void RestoreFrom_DoesNotShareMutableParts()
    {
        SurfaceSettings snapshot = Sample();
        var surface = new SurfaceSettings();

        surface.RestoreFrom(snapshot);
        surface.Pieces[0].Name = "Changed";
        surface.Map!.Projection = MapProjection.Polar;

        Assert.Equal("First", snapshot.Pieces[0].Name);
        Assert.Equal(MapProjection.Robinson, snapshot.Map!.Projection);
    }

    [Fact]
    public void HasSameContent_ForACopy_IsTrue()
    {
        SurfaceSettings original = Sample();

        Assert.True(original.HasSameContent(original.Clone()));
    }

    [Fact]
    public void HasSameContent_AfterChangingAndChangingBack_IsTrue()
    {
        SurfaceSettings saved = Sample();
        SurfaceSettings current = saved.Clone();

        current.Pieces[0].Center = new GeoCoordinate(-40, 90);
        current.Pieces[0].Center = new GeoCoordinate(10, 20);

        Assert.True(current.HasSameContent(saved));
    }

    [Fact]
    public void HasSameContent_WithAnEqualButSeparateCalibration_IsTrue()
    {
        SurfaceSettings saved = Sample();
        SurfaceSettings current = saved.Clone();

        current.Map!.Calibration = MapCalibration.CreateDefault();

        Assert.True(current.HasSameContent(saved));
    }

    [Theory]
    [InlineData("fill")]
    [InlineData("no map")]
    [InlineData("map type")]
    [InlineData("calibration")]
    [InlineData("piece moved")]
    [InlineData("piece turned")]
    [InlineData("piece resized")]
    [InlineData("piece renamed")]
    [InlineData("piece added")]
    [InlineData("pieces reordered")]
    public void HasSameContent_NoticesEveryKindOfEdit(string edit)
    {
        SurfaceSettings saved = Sample();
        saved.Pieces.Add(Piece("Second"));
        SurfaceSettings current = saved.Clone();

        switch (edit)
        {
            case "fill": current.FillColor = new RgbColor(0, 0, 0); break;
            case "no map": current.Map = null; break;
            case "map type": current.Map!.Projection = MapProjection.Polar; break;
            case "calibration":
                current.Map!.Calibration =
                    MapCalibration.CreateDefault().WithLatitudeDrawnAs(0, -55);
                break;
            case "piece moved": current.Pieces[0].Center = new GeoCoordinate(11, 20); break;
            case "piece turned": current.Pieces[0].RotationDegrees = 5; break;
            case "piece resized": current.Pieces[0].WidthDegrees = 31; break;
            case "piece renamed": current.Pieces[0].Name = "Other"; break;
            case "piece added": current.Pieces.Add(Piece("Third")); break;
            case "pieces reordered": current.Pieces.Reverse(); break;
        }

        Assert.False(current.HasSameContent(saved));
    }

    private static SurfaceSettings Sample()
    {
        var surface = new SurfaceSettings
        {
            FillColor = new RgbColor(1, 2, 3),
            Map = new SurfaceMap
            {
                AssetName = "assets/map.png",
                Projection = MapProjection.Robinson,
                Calibration = MapCalibration.CreateDefault(),
            },
        };
        surface.Pieces.Add(Piece("First"));
        return surface;
    }

    private static MapPiece Piece(string name)
    {
        return new MapPiece
        {
            Name = name,
            AssetName = "assets/piece.png",
            Outline = PieceOutline.Rectangle(new(0.1, 0.1), new(0.4, 0.3), 2.0),
            Center = new GeoCoordinate(10, 20),
        };
    }
}
