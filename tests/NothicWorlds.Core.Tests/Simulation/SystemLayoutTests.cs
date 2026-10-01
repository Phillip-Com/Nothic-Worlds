using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class SystemLayoutTests
{
    [Fact]
    public void EarthSizedBodies_AreOneUnitInBothScales()
    {
        Assert.Equal(1, SystemLayout.DisplayRadius(6371, SystemScale.True), 1e-12);
        Assert.Equal(1, SystemLayout.DisplayRadius(6371, SystemScale.Readable), 1e-12);
    }

    [Fact]
    public void TrueScale_KeepsRealProportions()
    {
        World world = World.CreateNew();

        Dictionary<Guid, DisplayBody> layout =
            SystemLayout.At(world.Bodies, 0, SystemScale.True);

        DisplayBody planet = layout[world.Bodies[0].Id];
        DisplayBody sun = layout[world.Bodies[1].Id];
        Assert.Equal(696_000 / 6371.0, sun.Radius, 1e-9);
        Assert.Equal(149_600_000 / 6371.0, (planet.Position - sun.Position).Length, 1e-6);
    }

    [Fact]
    public void Readable_ShrinksBigThingsMoreThanSmallOnes()
    {
        double sun = SystemLayout.DisplayRadius(696_000, SystemScale.Readable);
        double moon = SystemLayout.DisplayRadius(1737, SystemScale.Readable);

        Assert.InRange(sun, 5, 8);        // Instead of 109
        Assert.InRange(moon, 0.5, 0.7);   // Instead of 0.27: small things grow
    }

    [Fact]
    public void Readable_KeepsDirections_AndCompressesDistances()
    {
        World world = World.CreateNew();

        Dictionary<Guid, DisplayBody> layout =
            SystemLayout.At(world.Bodies, 91.3125, SystemScale.Readable);
        Vector3D offset = layout[world.Bodies[0].Id].Position;

        // A quarter year in, the planet is due "-Z" of its sun in both scales.
        Assert.Equal(0, offset.X, 1e-6);
        Assert.True(offset.Z < 0);
        Assert.InRange(offset.Length, 40, 100);  // Instead of about 23,500
    }

    [Fact]
    public void Readable_OrbitsNeverTouchTheirParent()
    {
        // A moon set (by the user) closer than the parent's own size.
        Vector3D drawn = SystemLayout.DisplayOffset(
            new Vector3D(1000, 0, 0), parentDisplayRadius: 5, childDisplayRadius: 1,
            SystemScale.Readable);

        Assert.True(drawn.Length > 6);
    }

    [Fact]
    public void Readable_KeepsTheOrderOfDistances()
    {
        double Near(double km) => SystemLayout.DisplayOffset(
            new Vector3D(km, 0, 0), 1, 0.5, SystemScale.Readable).Length;

        Assert.True(Near(400_000) < Near(1_000_000));
        Assert.True(Near(1_000_000) < Near(150_000_000));
    }

    [Fact]
    public void Positions_StackThroughParents()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        var moon = new Body
        {
            Name = "Moon",
            Kind = BodyKind.Moon,
            RadiusKm = 1737,
            Orbit = new Orbit { ParentId = planet.Id, DistanceKm = 384_400, PeriodDays = 27.3 },
        };
        world.Bodies.Add(moon);

        Dictionary<Guid, DisplayBody> layout =
            SystemLayout.At(world.Bodies, 10, SystemScale.Readable);

        double moonDistance = (layout[moon.Id].Position - layout[planet.Id].Position).Length;
        double expected = 1 + layout[moon.Id].Radius + Math.Pow(384_400 / 6371.0, 0.4);
        Assert.Equal(expected, moonDistance, 1e-9);
    }

    [Fact]
    public void OrbitPath_GoesOnceAroundTheParent()
    {
        World world = World.CreateNew();

        IReadOnlyList<Vector3D> path = SystemLayout.OrbitPath(
            world.Bodies[0], world.Bodies[1], SystemScale.True, samples: 4);

        Assert.Equal(4, path.Count);
        double radius = 149_600_000 / 6371.0;
        Assert.Equal(radius, path[0].X, 1e-6);    // Start
        Assert.Equal(-radius, path[1].Z, 1e-6);   // A quarter later
        Assert.Equal(-radius, path[2].X, 1e-6);   // Half
    }

    [Theory]
    [InlineData(SystemScale.True)]
    [InlineData(SystemScale.Readable)]
    public void OrbitPath_StaysSmooth_OnAVeryElongatedOrbit(SystemScale scale)
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        planet.Orbit = planet.Orbit! with { Eccentricity = 0.95, ClosestApproachDegrees = 30 };

        IReadOnlyList<Vector3D> path =
            SystemLayout.OrbitPath(planet, world.Bodies[1], scale, samples: 256);

        // No step around the path turns more than a few degrees as seen from the parent, even
        // at the closest approach where the body moves fastest.
        for (int i = 0; i < path.Count; i++)
        {
            Vector3D a = path[i];
            Vector3D b = path[(i + 1) % path.Count];
            double turn = double.RadiansToDegrees(
                Math.Acos(Math.Clamp(a.Dot(b) / (a.Length * b.Length), -1, 1)));
            Assert.True(turn < 15, $"step {i} turns {turn:0.0}°");
        }
    }

    [Fact]
    public void OrbitPath_PointsLieOnTheBodysActualPath()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        planet.Orbit = planet.Orbit! with { Eccentricity = 0.9, StartAngleDegrees = 70 };

        IReadOnlyList<double> times = OrbitMath.EvenlySpacedTimes(planet.Orbit, 64);
        IReadOnlyList<Vector3D> path =
            SystemLayout.OrbitPath(planet, world.Bodies[1], SystemScale.True, samples: 64);

        Assert.Equal(0, times[0], 1e-9);  // Starts where the body is at time 0
        for (int i = 0; i < 64; i++)
        {
            Vector3D actual = OrbitMath.OffsetFromParent(planet.Orbit, times[i]) * (1 / 6371.0);
            Assert.Equal(actual.X, path[i].X, 1e-6);
            Assert.Equal(actual.Z, path[i].Z, 1e-6);
        }
    }

    [Fact]
    public void OrbitPath_OfABodyWithoutAnOrbit_IsEmpty()
    {
        World world = World.CreateNew();

        Assert.Empty(SystemLayout.OrbitPath(
            world.Bodies[1], world.Bodies[0], SystemScale.Readable));
    }
}
