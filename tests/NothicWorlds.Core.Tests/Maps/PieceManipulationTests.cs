using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Maps;

public class PieceManipulationTests
{
    [Theory]
    [InlineData(0, 0, 0, 10, 30)]
    [InlineData(40, 100, 10, -170, 75)]  // Across the date line
    [InlineData(-60, -20, 200, 50, 15)]
    [InlineData(80, 0, 0, 30, 45)]       // Near the pole
    public void Move_KeepsTheGrabbedSpotUnderTheMouse(
        double lat, double lon, double rotation, double toLat, double toLon)
    {
        var center = new GeoCoordinate(lat, lon);
        var before = new PieceProjection(center, rotation, 20, 1.5);
        GeoCoordinate grabbed = before.FromBoxPosition(0.7, 0.3);  // Off-center grab
        var target = new GeoCoordinate(toLat, toLon);

        (GeoCoordinate newCenter, double newRotation) =
            PieceManipulation.Move(center, rotation, grabbed, target);

        var after = new PieceProjection(newCenter, newRotation, 20, 1.5);
        MapImagePosition spot = after.ToBoxPosition(target);
        Assert.Equal(0.7, spot.U, 1e-6);
        Assert.Equal(0.3, spot.V, 1e-6);
    }

    [Fact]
    public void Move_AlongTheEquator_DoesNotTurnThePiece()
    {
        (GeoCoordinate center, double rotation) = PieceManipulation.Move(
            new GeoCoordinate(0, 0), 30, new GeoCoordinate(0, 0), new GeoCoordinate(0, 50));

        Assert.Equal(0, center.LatitudeDegrees, 1e-9);
        Assert.Equal(50, center.LongitudeDegrees, 1e-9);
        Assert.Equal(30, rotation, 1e-9);
    }

    [Fact]
    public void Move_WithoutMoving_ChangesNothing()
    {
        var center = new GeoCoordinate(12, 34);
        var grabbed = new GeoCoordinate(13, 35);

        (GeoCoordinate newCenter, double rotation) =
            PieceManipulation.Move(center, 45, grabbed, grabbed);

        Assert.Equal(center, newCenter);
        Assert.Equal(45, rotation);
    }

    [Fact]
    public void Rotate_FollowsTheMouseAroundTheCenter()
    {
        // From due north of the center to due east: a quarter turn clockwise.
        double rotation = PieceManipulation.Rotate(
            new GeoCoordinate(0, 0), 10, new GeoCoordinate(10, 0), new GeoCoordinate(0, 10));

        Assert.Equal(100, rotation, 1e-9);
    }

    [Fact]
    public void Rotate_WrapsPast360()
    {
        // From due east to due north: a quarter turn back, from 30° to 300°.
        double rotation = PieceManipulation.Rotate(
            new GeoCoordinate(0, 0), 30, new GeoCoordinate(0, 10), new GeoCoordinate(10, 0));

        Assert.Equal(300, rotation, 1e-9);
    }

    [Fact]
    public void Resize_ScalesWithDistanceFromTheCenter()
    {
        double width = PieceManipulation.Resize(
            new GeoCoordinate(0, 0), 20, new GeoCoordinate(0, 10), new GeoCoordinate(0, 20));

        Assert.Equal(40, width, 1e-9);
    }

    [Theory]
    [InlineData(0.001, 0.1)]  // Dragged onto the center: the smallest allowed size
    [InlineData(90, 180)]     // Dragged far out: the largest allowed size
    public void Resize_StaysWithinTheAllowedSizes(double targetLongitude, double expected)
    {
        double width = PieceManipulation.Resize(
            new GeoCoordinate(0, 0), 20, new GeoCoordinate(0, 5),
            new GeoCoordinate(0, targetLongitude));

        Assert.Equal(expected, width, 1e-9);
    }

    [Theory]
    [InlineData(-30, 330)]
    [InlineData(360, 0)]
    [InlineData(725, 5)]
    [InlineData(-1e-15, 0)]
    public void NormalizeRotation_WrapsInto0To360(double input, double expected)
    {
        Assert.Equal(expected, PieceManipulation.NormalizeRotation(input), 1e-9);
    }

    [Fact]
    public void PieceAt_PicksTheTopmostPiece()
    {
        MapPiece bottom = Rectangle(new GeoCoordinate(0, 0));
        MapPiece top = Rectangle(new GeoCoordinate(0, 5));

        MapPiece? found = PieceManipulation.PieceAt([bottom, top], new GeoCoordinate(0, 3));

        Assert.Same(top, found);
    }

    [Fact]
    public void PieceAt_OutsideEveryPiece_IsNull()
    {
        Assert.Null(PieceManipulation.PieceAt(
            [Rectangle(new GeoCoordinate(0, 0))], new GeoCoordinate(40, 40)));
    }

    [Fact]
    public void PieceAt_ClicksThroughTheTransparentPartOfAFreeformCut()
    {
        MapPiece bottom = Rectangle(new GeoCoordinate(0, 0));

        // Triangle filling the lower-left half of its box; the upper-right is transparent.
        var triangle = new MapPiece
        {
            AssetName = "assets/b.png",
            Outline = PieceOutline.Create([new(0, 0), new(0, 1), new(1, 1)], 1.0),
            Center = new GeoCoordinate(0, 0),
            WidthDegrees = 20,
        };

        Assert.Same(bottom, PieceManipulation.PieceAt([bottom, triangle], new GeoCoordinate(5, 5)));
        Assert.Same(
            triangle, PieceManipulation.PieceAt([bottom, triangle], new GeoCoordinate(-5, -5)));
    }

    private static MapPiece Rectangle(GeoCoordinate center)
    {
        return new MapPiece
        {
            AssetName = "assets/a.png",
            Outline = PieceOutline.Rectangle(new(0, 0), new(1, 1), 1.0),
            Center = center,
            WidthDegrees = 20,
        };
    }
}
