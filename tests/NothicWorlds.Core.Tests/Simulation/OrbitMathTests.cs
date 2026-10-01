using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class OrbitMathTests
{
    private const double Tolerance = 1e-6;  // km, on orbits of thousands of km and more

    [Fact]
    public void Circle_StartsAtItsStartAngle()
    {
        Vector3D at = OrbitMath.OffsetFromParent(Circle(startAngle: 0), 0);

        AssertNear(new Vector3D(1000, 0, 0), at);
    }

    [Fact]
    public void Circle_GoesCounterclockwiseSeenFromTheNorth()
    {
        // A quarter of the way round from +X is -Z (see Vector3D.RotatedAroundY).
        Vector3D at = OrbitMath.OffsetFromParent(Circle(startAngle: 0), 25);

        AssertNear(new Vector3D(0, 0, -1000), at);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37.5)]
    [InlineData(100)]
    [InlineData(-250)]
    [InlineData(1_000_000)]
    public void Circle_KeepsItsDistance(double time)
    {
        Assert.Equal(1000, OrbitMath.OffsetFromParent(Circle(startAngle: 33), time).Length, 1e-6);
    }

    [Fact]
    public void FullPeriod_ComesBackToTheStart()
    {
        Orbit orbit = Elongated();

        AssertNear(OrbitMath.OffsetFromParent(orbit, 3),
            OrbitMath.OffsetFromParent(orbit, 3 + orbit.PeriodDays * 7), 1e-5);
    }

    [Fact]
    public void Elongated_IsClosestAtItsClosestApproach()
    {
        Orbit orbit = Elongated() with { StartAngleDegrees = 60, ClosestApproachDegrees = 60 };

        // At time 0 it's exactly at the closest point: distance × (1 - eccentricity).
        Vector3D at = OrbitMath.OffsetFromParent(orbit, 0);

        Assert.Equal(1000 * (1 - 0.5), at.Length, 1e-6);
        AssertNear(new Vector3D(1, 0, 0).RotatedAroundY(60) * 500, at);
    }

    [Fact]
    public void Elongated_IsFarthestHalfAPeriodLater()
    {
        Orbit orbit = Elongated() with { StartAngleDegrees = 60, ClosestApproachDegrees = 60 };

        Vector3D at = OrbitMath.OffsetFromParent(orbit, orbit.PeriodDays / 2);

        Assert.Equal(1000 * (1 + 0.5), at.Length, 1e-6);
    }

    [Fact]
    public void Elongated_MovesFasterWhenClose()
    {
        // Same time step near the closest and farthest points: more of the orbit is covered
        // near the parent (Kepler's second law).
        Orbit orbit = Elongated() with { StartAngleDegrees = 0, ClosestApproachDegrees = 0 };
        double step = orbit.PeriodDays / 100;

        double nearSweep = Angle(OrbitMath.OffsetFromParent(orbit, 0),
            OrbitMath.OffsetFromParent(orbit, step));
        double farSweep = Angle(OrbitMath.OffsetFromParent(orbit, orbit.PeriodDays / 2),
            OrbitMath.OffsetFromParent(orbit, orbit.PeriodDays / 2 + step));

        Assert.True(nearSweep > 3 * farSweep);
    }

    [Fact]
    public void Flat_StaysInTheReferencePlane()
    {
        for (double time = 0; time < 50; time += 3.7)
        {
            Assert.Equal(0, OrbitMath.OffsetFromParent(Elongated(), time).Y, Tolerance);
        }
    }

    [Fact]
    public void Tilted_RisesNorthAfterCrossingAtItsTiltDirection()
    {
        Orbit orbit = Circle(startAngle: 120) with { TiltDegrees = 30, TiltDirectionDegrees = 120 };

        Vector3D crossing = OrbitMath.OffsetFromParent(orbit, 0);
        Vector3D quarterLater = OrbitMath.OffsetFromParent(orbit, 25);

        Assert.Equal(0, crossing.Y, Tolerance);  // Crossing the reference plane...
        AssertNear(new Vector3D(1, 0, 0).RotatedAroundY(120) * 1000, crossing);
        Assert.Equal(1000 * Math.Sin(double.DegreesToRadians(30)), quarterLater.Y, 1e-6);
    }

    [Fact]
    public void TiltOver90_RunsBackwards()
    {
        Orbit backwards = Circle(startAngle: 0) with { TiltDegrees = 180 };

        Vector3D quarterLater = OrbitMath.OffsetFromParent(backwards, 25);

        AssertNear(new Vector3D(0, 0, 1000), quarterLater);  // Clockwise seen from the north
    }

    [Fact]
    public void SameInputs_AlwaysGiveTheSameAnswer()
    {
        Orbit orbit = Elongated() with { TiltDegrees = 12, TiltDirectionDegrees = 200 };

        Assert.Equal(OrbitMath.OffsetFromParent(orbit, 1234.5678),
            OrbitMath.OffsetFromParent(orbit, 1234.5678));
    }

    private static Orbit Circle(double startAngle)
    {
        return new Orbit
        {
            ParentId = Guid.Empty,
            DistanceKm = 1000,
            PeriodDays = 100,
            StartAngleDegrees = startAngle,
        };
    }

    private static Orbit Elongated()
    {
        return Circle(startAngle: 10) with { Eccentricity = 0.5, ClosestApproachDegrees = 40 };
    }

    private static double Angle(Vector3D a, Vector3D b)
    {
        return Math.Acos(Math.Clamp(a.Dot(b) / (a.Length * b.Length), -1, 1));
    }

    private static void AssertNear(Vector3D expected, Vector3D actual, double tolerance = Tolerance)
    {
        Assert.Equal(expected.X, actual.X, tolerance);
        Assert.Equal(expected.Y, actual.Y, tolerance);
        Assert.Equal(expected.Z, actual.Z, tolerance);
    }
}
