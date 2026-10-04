using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class NebulaSkyTests
{
    private static readonly RgbColor _black = new(0, 0, 0);
    private static readonly Nebula _veil = new(Guid.Parse("ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb"),
        "Veil", 20, 135, 30, 1, new RgbColor(200, 60, 120), new RgbColor(60, 90, 200));

    [Fact]
    public void AnEmptySky_IsTheBackground()
    {
        float[] sky = NebulaSky.Paint([], 8, 4, new RgbColor(255, 0, 51));

        Assert.All(Enumerable.Range(0, 32), i =>
        {
            Assert.Equal(1f, sky[i * 3]);
            Assert.Equal(0f, sky[i * 3 + 1]);
            Assert.Equal(0.2f, sky[i * 3 + 2], 3);
        });
    }

    [Fact]
    public void ANebula_GlowsAroundItsDirection_AndNowhereFarAway()
    {
        Vector3D centre = SphericalPolygon.ToUnit(new GeoCoordinate(20, 135));
        Vector3D opposite = centre * -1;

        // Its glow is patchy, so look at a few spots near its center.
        double near = new[] { (20.0, 135.0), (22.0, 130.0), (18.0, 140.0) }
            .Max(spot => NebulaSky.Glow(_veil,
                SphericalPolygon.ToUnit(new GeoCoordinate(spot.Item1, spot.Item2))));
        Assert.True(near > 0.1);
        Assert.Equal(0, NebulaSky.Glow(_veil, opposite));
    }

    [Fact]
    public void ThePaintedSky_IsLitOnlyNearTheNebula()
    {
        float[] sky = NebulaSky.Paint([_veil], 360, 180, _black);

        double Brightness(double latitude, double longitude)
        {
            int x = (int)((longitude + 180) / 360 * 360);
            int y = (int)((90 - latitude) / 180 * 180);
            int at = (y * 360 + x) * 3;
            return sky[at] + sky[at + 1] + sky[at + 2];
        }

        double nearby = Enumerable.Range(-5, 11).Max(d => Brightness(20 + d, 135 + d));
        Assert.True(nearby > 0.1);
        Assert.Equal(0, Brightness(-20, -45));  // Opposite side of the sky
    }

    [Fact]
    public void TheSameNebulas_PaintTheSameSky()
    {
        Assert.Equal(NebulaSky.Paint([_veil], 120, 60, _black),
            NebulaSky.Paint([_veil], 120, 60, _black));
    }

    [Theory]
    [InlineData(95, 0, 30, 0.5)]    // Past straight up
    [InlineData(0, 0, 1, 0.5)]      // Too small
    [InlineData(0, 0, 30, 0)]       // Too faint
    public void BadNebulas_AreFound(double latitude, double longitude, double size,
        double brightness)
    {
        Assert.NotNull((_veil with
        {
            LatitudeDegrees = latitude,
            LongitudeDegrees = longitude,
            SizeDegrees = size,
            Brightness = brightness,
        }).Problem());
    }

    [Fact]
    public void NewNebulas_AreUsable_AndTurnedAwayFromEachOther()
    {
        Nebula first = NewBodies.Nebula([]);
        Nebula second = NewBodies.Nebula([first]);

        Assert.Null(Nebula.Problem([first, second]));
        Assert.Equal(("Nebula 1", "Nebula 2"), (first.Name, second.Name));
        Assert.NotEqual(first.LongitudeDegrees, second.LongitudeDegrees);
    }

    [Fact]
    public void AWorld_HasAtMostTwentyNebulas_EachWithItsOwnId()
    {
        Assert.Null(Nebula.Problem([_veil]));
        Assert.NotNull(Nebula.Problem([_veil, _veil]));
        Assert.NotNull(Nebula.Problem(
            [.. Enumerable.Range(0, 21).Select(_ => _veil with { Id = Guid.NewGuid() })]));
    }
}
