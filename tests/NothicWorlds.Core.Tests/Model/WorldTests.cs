using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Model;

public class WorldTests
{
    [Fact]
    public void CreateNew_IsPainterly()
    {
        Assert.Equal(VisualStyle.Painterly, World.CreateNew("Test").Style);
    }

    [Fact]
    public void CreateNew_IsAnUnmappedPlanetCirclingASun()
    {
        World world = World.CreateNew("Test");

        Assert.Equal(2, world.Bodies.Count);
        Body planet = world.Bodies[0];
        Body sun = world.Bodies[1];
        Assert.Equal(BodyKind.Planet, planet.Kind);
        Assert.Equal(BodyKind.Star, sun.Kind);
        Assert.Null(planet.Surface.Map);
        Assert.Equal(SurfaceSettings.DefaultFillColor, planet.Surface.FillColor);
        Assert.Equal(sun.Id, planet.Orbit!.ParentId);
        Assert.Null(sun.Orbit);
        Assert.Null(SystemHierarchy.Problem(world.Bodies));
        Assert.All(world.Bodies, body => Assert.Null(body.Problem()));
    }

    [Fact]
    public void Clone_CopiesEverything()
    {
        World original = MappedWorld();

        World copy = original.Clone();

        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.View, copy.View);
        Assert.Equal(original.Style, copy.Style);
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
        Assert.Equal(original.Bodies.Count - 1, copy.Bodies.Count);
    }

    [Fact]
    public void Clone_CopiesPiecesIndependently()
    {
        World original = MappedWorld();
        original.Bodies[0].Surface.Pieces.Add(new MapPiece
        {
            AssetName = "assets/piece.png",
            Outline = PieceOutline.Rectangle(new(0.1, 0.1), new(0.4, 0.3), 2.0),
            Center = new GeoCoordinate(10, 20),
            WidthDegrees = 15,
        });

        World copy = original.Clone();
        MapPiece originalPiece = original.Bodies[0].Surface.Pieces[0];
        originalPiece.Center = new GeoCoordinate(-40, -40);
        originalPiece.WidthDegrees = 60;
        originalPiece.Name = "Moved";

        MapPiece copied = Assert.Single(copy.Bodies[0].Surface.Pieces);
        Assert.Equal(originalPiece.Id, copied.Id);
        Assert.Equal(new GeoCoordinate(10, 20), copied.Center);
        Assert.Equal(15, copied.WidthDegrees);
        Assert.Equal("Piece", copied.Name);
        Assert.Same(originalPiece.Outline, copied.Outline);  // Immutable, safely shared
    }

    private static World MappedWorld()
    {
        World world = World.CreateNew("Original");
        world.View = new CameraView(10, 20, 1.5);
        world.Style = VisualStyle.Simple;
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
