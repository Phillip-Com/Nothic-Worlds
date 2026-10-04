using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Model;

public sealed class AsteroidBeltTests
{
    [Fact]
    public void ANewBelt_IsLikeOurMainBelt()
    {
        Body sun = World.CreateNew().Bodies.Single(b => b.Kind == BodyKind.Star);

        AsteroidBelt belt = NewBodies.Belt(sun);

        Assert.Null(belt.Problem());
        Assert.Equal("Belt 1", belt.Name);
        Assert.InRange(belt.InnerKm / 149_600_000, 2.1, 2.3);
        Assert.InRange(belt.OuterKm / 149_600_000, 3.2, 3.4);
    }

    [Fact]
    public void ASecondBelt_GoesBeyondTheFirst()
    {
        Body sun = World.CreateNew().Bodies.Single(b => b.Kind == BodyKind.Star);
        sun.Belts = [NewBodies.Belt(sun)];

        AsteroidBelt second = NewBodies.Belt(sun);

        Assert.Equal("Belt 2", second.Name);
        Assert.True(second.InnerKm > sun.Belts[0].OuterKm);
    }

    [Theory]
    [InlineData("", 1e8, 2e8, 10, 0.5)]        // No name
    [InlineData("Belt", 2e8, 1e8, 10, 0.5)]    // Ends before it starts
    [InlineData("Belt", 0, 1e8, 10, 0.5)]      // Starts at the star
    [InlineData("Belt", 1e8, 2e8, 60, 0.5)]    // Too thick
    [InlineData("Belt", 1e8, 2e8, 10, 0)]      // Empty
    public void BadBelts_AreFound(string name, double inner, double outer, double thickness,
        double density)
    {
        var belt = new AsteroidBelt(Guid.NewGuid(), name, inner, outer, thickness, density,
            new RgbColor(1, 2, 3));

        Assert.NotNull(belt.Problem());
    }

    [Fact]
    public void OnlyStars_HaveBelts()
    {
        Body sun = World.CreateNew().Bodies.Single(b => b.Kind == BodyKind.Star);
        var planet = new Body { Belts = [NewBodies.Belt(sun)] };
        sun.Belts = [NewBodies.Belt(sun)];

        Assert.NotNull(planet.Problem());
        Assert.Null(sun.Problem());
    }

    [Fact]
    public void BeltsAreCopied_AndCompared()
    {
        Body sun = World.CreateNew().Bodies.Single(b => b.Kind == BodyKind.Star);
        sun.Belts = [NewBodies.Belt(sun)];

        Body copy = sun.Clone();

        Assert.True(copy.HasSameContent(sun));
        copy.Belts = [];
        Assert.False(copy.HasSameContent(sun));
        Assert.Single(sun.Belts);
    }

    [Fact]
    public void TheNaturalPeriod_GrowsWithDistance()
    {
        Body sun = World.CreateNew().Bodies.Single(b => b.Kind == BodyKind.Star);

        Assert.Equal(365.25, NewBodies.NaturalPeriodDays(149_600_000, sun), 6);
        Assert.Equal(365.25 * 8, NewBodies.NaturalPeriodDays(4 * 149_600_000, sun), 6);
    }
}
