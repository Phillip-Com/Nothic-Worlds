using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class WorldTests
{
    [Fact]
    public void CreateNew_HasOneUnmappedPlanet()
    {
        World world = World.CreateNew("Test");

        Body planet = Assert.Single(world.Bodies);
        Assert.Equal(BodyKind.Planet, planet.Kind);
        Assert.Null(planet.Surface.Map);
        Assert.Equal(SurfaceSettings.DefaultFillColor, planet.Surface.FillColor);
    }

    [Fact]
    public void Clone_CopiesEverything()
    {
        World original = MappedWorld();

        World copy = original.Clone();

        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.View, copy.View);
        Assert.Equal(original.Bodies[0].Id, copy.Bodies[0].Id);
        Assert.Equal("assets/x.png", copy.Bodies[0].Surface.Map!.AssetName);
        Assert.Equal(MapProjection.Robinson, copy.Bodies[0].Surface.Map!.Projection);
        Assert.Equal(new RgbColor(9, 8, 7), copy.Bodies[0].Surface.FillColor);
        Assert.Same(
            original.Bodies[0].Surface.Map!.Calibration, copy.Bodies[0].Surface.Map!.Calibration);
    }

    [Fact]
    public void Clone_IsIndependentOfLaterEdits()
    {
        World original = MappedWorld();
        World copy = original.Clone();

        original.Name = "Changed";
        original.Bodies[0].Surface.Map!.Projection = MapProjection.Polar;
        original.Bodies[0].Surface.FillColor = new RgbColor(0, 0, 0);
        original.Bodies.Add(new Body());

        Assert.NotEqual("Changed", copy.Name);
        Assert.Equal(MapProjection.Robinson, copy.Bodies[0].Surface.Map!.Projection);
        Assert.Equal(new RgbColor(9, 8, 7), copy.Bodies[0].Surface.FillColor);
        Assert.Single(copy.Bodies);
    }

    private static World MappedWorld()
    {
        World world = World.CreateNew("Original");
        world.View = new CameraView(10, 20, 1.5);
        world.Bodies[0].Surface.Map = new SurfaceMap
        {
            AssetName = "assets/x.png",
            Projection = MapProjection.Robinson,
            Calibration = MapCalibration.CreateDefault().WithLatitudeDrawnAs(0, -55),
        };
        world.Bodies[0].Surface.FillColor = new RgbColor(9, 8, 7);
        return world;
    }
}
