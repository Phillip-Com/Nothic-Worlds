using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Tests.Maps;

public class MapProjectionInverterTests
{
    private const double Tolerance = 1e-3;  // Degrees

    [Theory]
    [InlineData(MapProjection.Equirectangular)]
    [InlineData(MapProjection.Mercator)]
    [InlineData(MapProjection.Robinson)]
    [InlineData(MapProjection.WinkelTripel)]
    [InlineData(MapProjection.Mollweide)]
    [InlineData(MapProjection.GallPeters)]
    [InlineData(MapProjection.Polar)]
    [InlineData(MapProjection.TwoHemispheres)]
    public void Invert_FindsTheOriginalPosition(MapProjection projection)
    {
        var inverter = new MapProjectionInverter(projection, 2.0);

        foreach (double lat in new[] { -60.0, -25.0, 0.0, 17.5, 45.0, 60.0 })
        {
            foreach (double lon in new[] { -150.0, -89.0, -30.0, 0.5, 45.0, 120.0, 175.0 })
            {
                if (projection == MapProjection.Polar && lat < 0)
                {
                    continue;  // The southern hemisphere isn't part of a polar map.
                }

                var original = new GeoCoordinate(lat, lon);
                MapImagePosition position =
                    MapProjections.ToImagePosition(original, projection, 2.0);

                GeoCoordinate? found = inverter.Invert(position.U, position.V);

                Assert.True(found.HasValue, $"{projection}: nothing found for {lat}, {lon}");
                Assert.Equal(lat, found.Value.LatitudeDegrees, Tolerance);
                Assert.Equal(0.0,
                    SphericalCoordinates.LongitudeDelta(lon, found.Value.LongitudeDegrees),
                    Tolerance);
            }
        }
    }

    [Theory]
    [InlineData(MapProjection.Robinson, 0.02, 0.02)]        // Corner outside the outline
    [InlineData(MapProjection.Mollweide, 0.03, 0.05)]       // Corner outside the oval
    [InlineData(MapProjection.Polar, 0.03, 0.03)]           // Corner outside the circle
    [InlineData(MapProjection.TwoHemispheres, 0.5, 0.02)]   // Between the two circles
    public void Invert_OutsideTheMap_ReturnsNull(MapProjection projection, double u, double v)
    {
        Assert.Null(new MapProjectionInverter(projection, 2.0).Invert(u, v));
    }

    [Fact]
    public void Invert_PolarCenter_IsTheNorthPole()
    {
        GeoCoordinate? found = new MapProjectionInverter(MapProjection.Polar, 1.0).Invert(0.5, 0.5);

        Assert.True(found.HasValue);
        Assert.Equal(90.0, found.Value.LatitudeDegrees, 0.01);
    }
}
