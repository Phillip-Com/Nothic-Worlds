using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Tests.Maps;

public class PieceProjectionTests
{
    private const double Tolerance = 1e-9;  // Full precision throughout

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(45, 120, 30)]
    [InlineData(-70, -170, 200)]
    [InlineData(89, 10, 0)]  // Near the pole
    public void Center_IsTheMiddleOfTheBox(double lat, double lon, double rotation)
    {
        var projection = new PieceProjection(new GeoCoordinate(lat, lon), rotation, 20, 1.5);

        MapImagePosition position = projection.ToBoxPosition(new GeoCoordinate(lat, lon));

        Assert.Equal(0.5, position.U, Tolerance);
        Assert.Equal(0.5, position.V, Tolerance);
        Assert.False(position.IsOutsideMap);
    }

    [Theory]
    [InlineData(0, 0, 0, 20)]
    [InlineData(30, 175, 45, 40)]   // Straddling the 180° line
    [InlineData(-60, -30, 300, 90)]
    [InlineData(80, 60, 10, 25)]    // Close to the pole
    public void BoxPositions_RoundTrip(double lat, double lon, double rotation, double width)
    {
        var projection = new PieceProjection(new GeoCoordinate(lat, lon), rotation, width, 1.7);

        foreach (double u in new[] { 0.0, 0.1, 0.5, 0.77, 1.0 })
        {
            foreach (double v in new[] { 0.0, 0.3, 0.5, 0.9, 1.0 })
            {
                MapImagePosition back = projection.ToBoxPosition(projection.FromBoxPosition(u, v));

                Assert.Equal(u, back.U, Tolerance);
                Assert.Equal(v, back.V, Tolerance);
            }
        }
    }

    [Fact]
    public void TinyPieces_ArePlacedPrecisely()
    {
        // A piece a tenth of a degree across: every point must round-trip exactly, which the
        // earlier acos-based distance couldn't manage near the center.
        var projection = new PieceProjection(new GeoCoordinate(-70, -170), 200, 0.1, 1.0);

        MapImagePosition nearCenter =
            projection.ToBoxPosition(projection.FromBoxPosition(0.51, 0.49));

        Assert.Equal(0.51, nearCenter.U, 1e-6);
        Assert.Equal(0.49, nearCenter.V, 1e-6);
    }

    [Fact]
    public void Unrotated_UpIsNorth_AndRightIsEast()
    {
        var projection = new PieceProjection(new GeoCoordinate(0, 0), 0, 20, 2.0);

        GeoCoordinate top = projection.FromBoxPosition(0.5, 0.0);
        GeoCoordinate right = projection.FromBoxPosition(1.0, 0.5);

        Assert.Equal(5.0, top.LatitudeDegrees, 1e-3);     // Height 10°, so the top edge is 5° N
        Assert.Equal(0.0, top.LongitudeDegrees, 1e-3);
        Assert.Equal(10.0, right.LongitudeDegrees, 1e-3); // Width 20°, so the right edge is 10° E
        Assert.Equal(0.0, right.LatitudeDegrees, 1e-3);
    }

    [Fact]
    public void Rotated90_UpPointsEast()
    {
        var projection = new PieceProjection(new GeoCoordinate(0, 0), 90, 20, 2.0);

        GeoCoordinate top = projection.FromBoxPosition(0.5, 0.0);

        Assert.Equal(0.0, top.LatitudeDegrees, 1e-3);
        Assert.Equal(5.0, top.LongitudeDegrees, 1e-3);
    }

    [Fact]
    public void DistancesFromTheCenter_AreTrue()
    {
        // A wide piece: its left and right edges are exactly half its width from the center,
        // measured along the globe.
        var projection = new PieceProjection(new GeoCoordinate(40, 0), 0, 100, 2.0);

        GeoCoordinate right = projection.FromBoxPosition(1.0, 0.5);

        double arc = double.RadiansToDegrees(Math.Acos(System.Numerics.Vector3.Dot(
            SphericalCoordinates.ToDirection(new GeoCoordinate(40, 0)),
            SphericalCoordinates.ToDirection(right))));
        Assert.Equal(50.0, arc, 1e-2);
    }

    [Theory]
    [InlineData(20, 0)]     // Well beyond the top edge
    [InlineData(0, 30)]     // Beyond the right edge
    [InlineData(0, 180)]    // The far side of the globe
    public void PointsOutsideThePiece_AreFlagged(double lat, double lon)
    {
        var projection = new PieceProjection(new GeoCoordinate(0, 0), 0, 20, 2.0);

        Assert.True(projection.ToBoxPosition(new GeoCoordinate(lat, lon)).IsOutsideMap);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(200.0)]
    [InlineData(double.NaN)]
    public void InvalidWidth_Throws(double width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PieceProjection(new GeoCoordinate(0, 0), 0, width, 1.0));
    }

    [Fact]
    public void Directions_AreUnitAndPerpendicular()
    {
        var projection = new PieceProjection(new GeoCoordinate(33, -70), 15, 20, 1.0);

        System.Numerics.Vector3 c = projection.CenterDirection;
        System.Numerics.Vector3 e = projection.EastDirection;
        System.Numerics.Vector3 n = projection.NorthDirection;
        Assert.Equal(1f, c.Length(), 1e-5f);
        Assert.Equal(1f, e.Length(), 1e-5f);
        Assert.Equal(1f, n.Length(), 1e-5f);
        Assert.Equal(0f, System.Numerics.Vector3.Dot(c, e), 1e-5f);
        Assert.Equal(0f, System.Numerics.Vector3.Dot(c, n), 1e-5f);
        Assert.Equal(0f, System.Numerics.Vector3.Dot(e, n), 1e-5f);
    }
}
