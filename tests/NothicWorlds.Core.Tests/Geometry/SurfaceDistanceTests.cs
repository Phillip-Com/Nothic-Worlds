using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Geometry;

public sealed class SurfaceDistanceTests
{
    private const double Radius = 6371;

    [Fact]
    public void OnAGlobe_ItsTheDistanceAlongTheSurface()
    {
        double km = SurfaceDistance.Km(BodyShape.Sphere, Radius, new(0, 0), new(0, 90));

        Assert.Equal(Math.PI / 2 * Radius, km, 6);
    }

    [Fact]
    public void OnAFlatWorld_DistancesFromTheNorthPoleMatchTheGlobe()
    {
        var pole = new GeoCoordinate(90, 0);
        var south = new GeoCoordinate(-30, 45);

        Assert.Equal(SurfaceDistance.Km(BodyShape.Sphere, Radius, pole, south),
            SurfaceDistance.Km(BodyShape.FlatDisc, Radius, pole, south), 6);
    }

    [Fact]
    public void OnAFlatWorld_DistancesAroundTheDiscAreStraightAcrossIt()
    {
        // Two points on the equator, a quarter turn apart: on the disc's ring of radius π/2.
        double km = SurfaceDistance.Km(BodyShape.FlatDisc, Radius, new(0, 0), new(0, 90));

        Assert.Equal(Math.Sqrt(2) * Math.PI / 2 * Radius, km, 6);
    }
}
