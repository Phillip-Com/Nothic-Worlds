using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class OrbitElementsTests
{
    [Theory]
    [InlineData(0.0, 0.0, 0.0, 0.0, 0.0, 0.0)]  // Round and flat
    [InlineData(0.3, 0.0, 0.0, 40.0, 0.0, 10.0)]  // Elongated
    [InlineData(0.0, 25.0, 70.0, 0.0, 120.0, 400.5)]  // Tilted
    [InlineData(0.6, 35.0, 200.0, 300.0, 80.0, 1234.0)]  // Both
    [InlineData(0.2, 150.0, 30.0, 100.0, 250.0, 77.0)]  // Going backwards
    public void AnOrbit_IsFoundAgain_FromWhereTheBodyIsAndHowItMoves(double eccentricity,
        double tilt, double tiltDirection, double closest, double start, double timeDays)
    {
        var design = new Orbit
        {
            ParentId = Guid.NewGuid(),
            DistanceKm = 1_000_000,
            PeriodDays = 50,
            Eccentricity = eccentricity,
            TiltDegrees = tilt,
            TiltDirectionDegrees = tiltDirection,
            ClosestApproachDegrees = closest,
            StartAngleDegrees = start,
        };

        Orbit found = Found(design, timeDays);

        Assert.Equal(design.DistanceKm, found.DistanceKm, 1);  // Within a few meters
        Assert.Equal(design.PeriodDays, found.PeriodDays, 5);
        Assert.Equal(design.Eccentricity, found.Eccentricity, 6);
        Assert.Equal(design.TiltDegrees, found.TiltDegrees, 4);
        foreach (double time in new[] { timeDays, timeDays + 13, timeDays + 1000 })
        {
            Vector3D expected = OrbitMath.OffsetFromParent(design, time);
            Vector3D actual = OrbitMath.OffsetFromParent(found, time);
            // Within 1e-5 of the orbit: the tiny difference in period adds up over 20 trips.
            Assert.True((expected - actual).Length < 10, $"At {time}: {expected} vs {actual}");
        }
    }

    [Fact]
    public void ARoundFlatOrbit_KeepsItsDesignedDirections()
    {
        var design = new Orbit
        {
            ParentId = Guid.NewGuid(),
            DistanceKm = 384_400,
            PeriodDays = 27.3,
            ClosestApproachDegrees = 33,
            TiltDirectionDegrees = 44,
            StartAngleDegrees = 10,
        };

        Orbit found = Found(design, 5);

        Assert.Equal(0, found.Eccentricity);
        Assert.Equal(0, found.TiltDegrees);
        Assert.Equal(33, found.ClosestApproachDegrees, 9);
        Assert.Equal(44, found.TiltDirectionDegrees, 9);
        Assert.Equal(10, found.StartAngleDegrees, 4);
    }

    [Fact]
    public void ABodyMovingTooFast_IsEscaping()
    {
        var design = new Orbit { ParentId = Guid.NewGuid(), DistanceKm = 1e6, PeriodDays = 50 };
        double gravity = Gravity(design);
        double escape = Math.Sqrt(2 * gravity / 1e6);

        (Orbit? orbit, string? problem) = OrbitElements.FromMotion(new Vector3D(1e6, 0, 0),
            new Vector3D(0, 0, -1.01 * escape), gravity, 0, design);

        Assert.Null(orbit);
        Assert.Contains("escaping", problem);
    }

    // The orbit found from the designed one's position and velocity, with the gravity that
    // matches its period.
    private static Orbit Found(Orbit design, double timeDays)
    {
        double step = design.PeriodDays * 1e-6;
        Vector3D offset = OrbitMath.OffsetFromParent(design, timeDays);
        Vector3D velocity = (OrbitMath.OffsetFromParent(design, timeDays + step)
            - OrbitMath.OffsetFromParent(design, timeDays - step)) * (1 / (2 * step));
        (Orbit? found, string? problem) =
            OrbitElements.FromMotion(offset, velocity, Gravity(design), timeDays, design);
        Assert.Null(problem);
        return found!;
    }

    private static double Gravity(Orbit orbit) =>
        4 * Math.PI * Math.PI * Math.Pow(orbit.DistanceKm, 3)
            / (orbit.PeriodDays * orbit.PeriodDays);
}
