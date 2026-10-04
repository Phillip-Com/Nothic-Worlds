using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class PlanetRingsTests
{
    [Fact]
    public void TheDefaultRings_AreUsable()
    {
        Assert.Null(PlanetRings.Default.Problem());
        Assert.Null(new Body { Rings = PlanetRings.Default }.Problem());
    }

    [Theory]
    [InlineData(1.0, 2.0)]   // Starts at the surface
    [InlineData(2.0, 1.5)]   // Ends before it starts
    [InlineData(1.5, 60)]    // Too far out
    [InlineData(double.NaN, 2.0)]
    public void BadRings_AreFound(double inner, double outer)
    {
        Assert.NotNull(new PlanetRings(inner, outer, new RgbColor(1, 2, 3)).Problem());
    }

    [Fact]
    public void OnlyPlanetsAndMoons_HaveRings()
    {
        var star = new Body { Kind = BodyKind.Star, Rings = PlanetRings.Default };
        var moon = new Body { Kind = BodyKind.Moon, Rings = PlanetRings.Default };

        Assert.NotNull(star.Problem());
        Assert.Null(moon.Problem());
    }

    [Fact]
    public void RingsAreCopied_AndCompared()
    {
        var planet = new Body { Rings = PlanetRings.Default };

        Assert.Equal(PlanetRings.Default, planet.Clone().Rings);
        Assert.False(planet.HasSameContent(new Body { Id = planet.Id }));
    }
}
