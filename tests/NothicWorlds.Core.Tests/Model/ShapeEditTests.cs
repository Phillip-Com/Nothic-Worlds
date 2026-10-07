using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public sealed class ShapeEditTests
{
    private const double RadiusKm = 6371;

    [Fact]
    public void AShape_StandsUpFromItsSpot_FacingNorth()
    {
        ShapeFrame frame = Shape(new GeoCoordinate(0, 0), depthKm: 637.1).FrameOn(RadiusKm);

        AssertNear(new Vector3D(0, 0, 1.1), frame.Center);  // Longitude 0 faces +Z
        AssertNear(new Vector3D(0, 0, 1), frame.Up);
        AssertNear(new Vector3D(0, 1, 0), frame.Along);  // North
        AssertNear(new Vector3D(1, 0, 0), frame.Across);  // East
    }

    [Fact]
    public void Turning_SwingsItsLengthFromNorthTowardEast()
    {
        ShapeFrame frame = Shape(new GeoCoordinate(0, 0), turn: 90).FrameOn(RadiusKm);

        AssertNear(new Vector3D(1, 0, 0), frame.Along);
        AssertNear(new Vector3D(0, -1, 0), frame.Across);
    }

    [Fact]
    public void AShapeAtAPole_StillHasAFrame()
    {
        ShapeFrame frame = Shape(new GeoCoordinate(90, 0)).FrameOn(RadiusKm);

        AssertNear(new Vector3D(0, 1, 0), frame.Up);
        Assert.Equal(1, frame.Along.Length, 9);
        Assert.Equal(0, frame.Along.Dot(frame.Up), 9);
    }

    [Fact]
    public void OnAFlatWorld_AShapeStandsStraightUpFromTheFace_FacingTheCenter()
    {
        // Latitude 0 is a quarter turn from the north pole: π/2 from the face's center.
        ShapeFrame frame = Shape(new GeoCoordinate(0, 0), depthKm: 637.1)
            .FrameOn(RadiusKm, BodyShape.FlatDisc);

        AssertNear(new Vector3D(0, FlatDisc.HalfThickness + 0.1, Math.PI / 2), frame.Center);
        AssertNear(new Vector3D(0, 1, 0), frame.Up);
        AssertNear(new Vector3D(0, 0, -1), frame.Along);  // North: toward the center
        AssertNear(new Vector3D(1, 0, 0), frame.Across);  // East, as on a globe
    }

    [Fact]
    public void OnAFlatWorld_AShapeAtTheCenter_StillHasAFrame()
    {
        ShapeFrame frame = Shape(new GeoCoordinate(90, 0)).FrameOn(RadiusKm, BodyShape.FlatDisc);

        // As at a globe's pole, +Z stands in for north there.
        AssertNear(new Vector3D(0, FlatDisc.HalfThickness, 0), frame.Center);
        AssertNear(new Vector3D(0, 0, 1), frame.Along);
        AssertNear(new Vector3D(-1, 0, 0), frame.Across);
    }

    [Fact]
    public void ACylinderThroughTheWorld_AndAHollowCenter_AreAllowed()
    {
        ShapeEdit hole = Shape(new GeoCoordinate(10, 20), depthKm: -RadiusKm,
            heightKm: 2.5 * RadiusKm) with
        {
            Kind = ShapeKind.Cylinder,
            Operation = ShapeOperation.Cut,
        };
        ShapeEdit hollow = Shape(new GeoCoordinate(0, 0), depthKm: -RadiusKm,
            widthKm: 1.6 * RadiusKm) with
        {
            Operation = ShapeOperation.Cut,
        };

        Assert.Null(hole.Problem(RadiusKm));
        Assert.Null(hollow.Problem(RadiusKm));
        AssertNear(Vector3D.Zero, hollow.FrameOn(RadiusKm).Center);
    }

    [Theory]
    [InlineData(0.0, 100.0)]  // No size
    [InlineData(-5.0, 100.0)]
    [InlineData(30_000.0, 100.0)]  // Over 4 radii
    [InlineData(100.0, 30_000.0)]  // Middle too far from the surface
    [InlineData(double.NaN, 100.0)]
    public void AShapeOutOfRange_IsAProblem(double widthKm, double depthKm)
    {
        Assert.NotNull(Shape(new GeoCoordinate(0, 0), depthKm: depthKm, widthKm: widthKm)
            .Problem(RadiusKm));
    }

    [Fact]
    public void Shapes_GoOnPlanetsAndMoonsOnly_UpToAMaximum_EachWithItsOwnId()
    {
        var star = new Body { Kind = BodyKind.Star, RadiusKm = 696_000 };
        star.Surface.Shapes.Add(Shape(new GeoCoordinate(0, 0)));
        Assert.NotNull(star.Problem());

        var planet = new Body { RadiusKm = RadiusKm };
        ShapeEdit shape = Shape(new GeoCoordinate(0, 0));
        planet.Surface.Shapes.Add(shape);
        Assert.Null(planet.Problem());
        planet.Surface.Shapes.Add(shape);
        Assert.Contains("share an ID", planet.Problem());

        planet.Surface.Shapes.Clear();
        for (int i = 0; i <= ShapeEdit.MaxPerBody; i++)
        {
            planet.Surface.Shapes.Add(Shape(new GeoCoordinate(0, i)));
        }

        Assert.Contains("up to", planet.Problem());
    }

    [Fact]
    public void Shapes_AreCopiedAndCompared()
    {
        var planet = new Body();
        planet.Surface.Shapes.Add(Shape(new GeoCoordinate(5, 5)));

        Body copy = planet.Clone();

        Assert.True(planet.HasSameContent(copy));
        copy.Surface.Shapes[0] = copy.Surface.Shapes[0] with { DepthKm = 1 };
        Assert.False(planet.HasSameContent(copy));
        Assert.Equal(0, planet.Surface.Shapes[0].DepthKm);
    }

    private static ShapeEdit Shape(GeoCoordinate spot, double depthKm = 0, double widthKm = 100,
        double heightKm = 50, double turn = 0) =>
        new(Guid.NewGuid(), ShapeKind.Sphere, ShapeOperation.Add, spot, depthKm, widthKm,
            heightKm, 80, turn);

    private static void AssertNear(Vector3D expected, Vector3D actual) =>
        Assert.True((expected - actual).Length < 1e-6, $"Expected {expected}, got {actual}");
}
