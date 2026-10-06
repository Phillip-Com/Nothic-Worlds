using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Tests.Maps;

public class GridSnapTests
{
    [Fact]
    public void NearestCrossing_RoundsBothWays_AndWrapsAt180Degrees()
    {
        GeoCoordinate crossing = GridSnap.NearestCrossing(new(37.4, 178.0), 15);

        Assert.Equal(30.0, crossing.LatitudeDegrees, 1e-12);
        Assert.Equal(-180.0, crossing.LongitudeDegrees, 1e-12);
    }

    [Fact]
    public void SnapPiece_MovesTheCenterOntoALine_WhenItIsNearest()
    {
        // A 4°-wide square piece: its edges sit about 2° either side, farther from the 5° lines
        // than its center is.
        GeoCoordinate snapped = GridSnap.SnapPiece(new(9.2, 20.3), 0, 4.0, 1.0, 5);

        Assert.Equal(10.0, snapped.LatitudeDegrees, 1e-9);
        Assert.Equal(20.0, snapped.LongitudeDegrees, 1e-9);
    }

    [Fact]
    public void SnapPiece_LinesAnEdgeUpWithTheGrid_WhenAnEdgeIsNearer()
    {
        // 8° x 4° on the equator at 13°E: the center is 2° from 15°E, the east edge
        // (17°E) 2° from 15°E, but the west edge (9°E) only 1° from 10°E, so it snaps.
        GeoCoordinate snapped = GridSnap.SnapPiece(new(0.0, 13.0), 0, 8.0, 2.0, 5);
        var projection = new PieceProjection(snapped, 0, 8.0, 2.0);

        Assert.Equal(10.0, projection.FromBoxPosition(0, 0.5).LongitudeDegrees, 1e-6);
        Assert.Equal(14.0, snapped.LongitudeDegrees, 1e-6);
        Assert.Equal(0.0, snapped.LatitudeDegrees, 1e-9);  // Already on the equator.
    }

    [Fact]
    public void SnapPiece_PutsAPointExactlyOnALine_EvenForALargeTurnedPiece()
    {
        const double width = 80.0, aspect = 1.8, rotation = 23.0, step = 5.0;
        GeoCoordinate snapped = GridSnap.SnapPiece(new(16.9, -12.9), rotation, width, aspect, step);
        var projection = new PieceProjection(snapped, rotation, width, aspect);
        GeoCoordinate[] points = [snapped, projection.FromBoxPosition(0, 0.5),
            projection.FromBoxPosition(1, 0.5), projection.FromBoxPosition(0.5, 0),
            projection.FromBoxPosition(0.5, 1)];

        static double OffLine(double degrees) =>
            Math.Abs(degrees / step - Math.Round(degrees / step)) * step;
        Assert.Contains(points, p => OffLine(p.LatitudeDegrees) < 1e-6);
        Assert.Contains(points, p => OffLine(p.LongitudeDegrees) < 1e-6);
    }

    [Fact]
    public void SnapPiece_IsTheSameEveryTime()
    {
        GeoCoordinate first = GridSnap.SnapPiece(new(41.3, -73.2), 25, 12.0, 1.5, 1);
        GeoCoordinate second = GridSnap.SnapPiece(new(41.3, -73.2), 25, 12.0, 1.5, 1);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(91.0)]
    [InlineData(double.NaN)]
    public void Steps_OutOfRange_AreRejected(double step)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GridSnap.NearestCrossing(new(0, 0), step));
    }
}
