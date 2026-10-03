using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Model;

public sealed class BodyAppearanceTests
{
    [Fact]
    public void EachKind_StartsWithItsOwnLook()
    {
        Assert.Equal((BodyAppearance.PlanetBlue, SurfacePattern.Plain),
            Look(BodyAppearance.DefaultFor(BodyKind.Planet)));
        Assert.Equal((BodyAppearance.MoonGrey, SurfacePattern.Rocky),
            Look(BodyAppearance.DefaultFor(BodyKind.Moon)));
        Assert.Equal(StarType.Yellow, BodyAppearance.DefaultFor(BodyKind.Star).StarType);
    }

    [Fact]
    public void NewMoons_AreGreyAndRocky()
    {
        World world = World.CreateNew();

        Body moon = NewBodies.Moon(world.Bodies, world.Bodies[0]);

        Assert.Equal(BodyAppearance.DefaultFor(BodyKind.Moon), moon.Appearance);
    }

    [Fact]
    public void EveryStarType_HasADistinctColor()
    {
        RgbColor[] colors = [.. Enum.GetValues<StarType>().Select(BodyAppearance.StarColor)];

        Assert.Equal(colors.Length, colors.Distinct().Count());
    }

    [Fact]
    public void ChangingTheLook_CountsAsAChangeToTheBody()
    {
        var body = new Body();
        Body copy = body.Clone();

        copy.Appearance = copy.Appearance with { Pattern = SurfacePattern.Banded };

        Assert.Equal(SurfacePattern.Plain, body.Appearance.Pattern);
        Assert.False(body.HasSameContent(copy));
        Assert.True(copy.HasSameContent(copy.Clone()));
    }

    private static (RgbColor, SurfacePattern) Look(BodyAppearance appearance) =>
        (appearance.Color, appearance.Pattern);
}
