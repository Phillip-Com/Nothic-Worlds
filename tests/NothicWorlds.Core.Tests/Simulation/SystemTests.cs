using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class SystemTests
{
    private static readonly Guid _sunId = Guid.NewGuid();
    private static readonly Guid _planetId = Guid.NewGuid();
    private static readonly Guid _moonId = Guid.NewGuid();

    [Fact]
    public void Positions_StackUpTheChainOfParents()
    {
        List<Body> bodies = SunPlanetMoon();

        Dictionary<Guid, Vector3D> at = SystemPositions.At(bodies, 0);

        Assert.Equal(Vector3D.Zero, at[_sunId]);
        Assert.Equal(new Vector3D(150_000_000, 0, 0), at[_planetId]);
        Assert.Equal(new Vector3D(150_384_400, 0, 0), at[_moonId]);
    }

    [Fact]
    public void Positions_FollowTheClock()
    {
        List<Body> bodies = SunPlanetMoon();

        // A quarter year: the planet is a quarter of the way round, and the moon (period 25
        // days) has gone round 3.65 times on top of that.
        Dictionary<Guid, Vector3D> at = SystemPositions.At(bodies, 91.25);

        Assert.Equal(150_000_000, at[_planetId].Length, 1e-3);
        Assert.Equal(-150_000_000, at[_planetId].Z, 1e-3);
        Assert.Equal(384_400, (at[_moonId] - at[_planetId]).Length, 1e-3);
    }

    [Fact]
    public void Positions_DontDependOnListOrder()
    {
        List<Body> bodies = SunPlanetMoon();
        List<Body> reversed = [.. Enumerable.Reverse(bodies)];

        Assert.Equal(
            SystemPositions.At(bodies, 77)[_moonId], SystemPositions.At(reversed, 77)[_moonId]);
    }

    [Fact]
    public void ASunCanOrbitAnotherSun()
    {
        List<Body> bodies = SunPlanetMoon();
        bodies.Add(new Body
        {
            Name = "Companion",
            Kind = BodyKind.Star,
            Orbit = new Orbit { ParentId = _sunId, DistanceKm = 5e9, PeriodDays = 30000 },
        });

        Assert.Null(SystemHierarchy.Problem(bodies));
    }

    [Fact]
    public void Hierarchy_MissingParent_IsAProblem()
    {
        List<Body> bodies = SunPlanetMoon();
        bodies.RemoveAt(0);  // The sun

        Assert.Contains("isn't in the world", SystemHierarchy.Problem(bodies));
        Assert.Throws<ArgumentException>(() => SystemPositions.At(bodies, 0));
    }

    [Fact]
    public void Hierarchy_Loop_IsAProblem()
    {
        List<Body> bodies = SunPlanetMoon();
        bodies[0].Orbit = new Orbit { ParentId = _moonId, DistanceKm = 1, PeriodDays = 1 };

        Assert.Contains("loop", SystemHierarchy.Problem(bodies));
    }

    [Fact]
    public void Hierarchy_OrbitingItself_IsAProblem()
    {
        List<Body> bodies = SunPlanetMoon();
        bodies[2].Orbit = bodies[2].Orbit! with { ParentId = _moonId };

        Assert.Contains("loop", SystemHierarchy.Problem(bodies));
    }

    [Fact]
    public void ChildrenOf_ListsDirectChildrenOnly()
    {
        List<Body> bodies = SunPlanetMoon();

        Assert.Equal([_planetId], SystemHierarchy.ChildrenOf(bodies, _sunId).Select(b => b.Id));
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]          // The very start
    [InlineData(0.5, 1, 12, 0)]       // Noon on a 24-hour day
    [InlineData(1.0, 2, 0, 0)]
    [InlineData(1.25, 1, 30, 0)]      // A 36-hour day: day 1, 30:00
    public void LocalTime_CountsTheBodysOwnDays(
        double timeDays, long day, int hour, int minute)
    {
        double dayLength = timeDays == 1.25 ? 36 : 24;
        var body = new Body { DayLengthHours = dayLength };

        Assert.Equal(new LocalTime(day, hour, minute), BodyClock.LocalTimeOn(body, timeDays));
    }

    [Fact]
    public void LocalTime_ShowsMinutes()
    {
        var body = new Body { DayLengthHours = 24 };

        LocalTime time = BodyClock.LocalTimeOn(body, 1203 + (14.5 / 24));

        Assert.Equal(new LocalTime(1204, 14, 30), time);
    }

    [Fact]
    public void LocalTime_BeforeTimeZero_CountsBackwards()
    {
        var body = new Body { DayLengthHours = 24 };

        Assert.Equal(new LocalTime(0, 18, 0), BodyClock.LocalTimeOn(body, -0.25));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.25, 90)]   // A quarter day on a 24-hour body
    [InlineData(1, 0)]       // A full turn
    [InlineData(-0.25, 270)]
    public void Spin_TurnsOncePerDay(double timeDays, double degrees)
    {
        var body = new Body { DayLengthHours = 24 };

        Assert.Equal(degrees, BodyClock.SpinDegrees(body, timeDays), 1e-9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12.3)]
    [InlineData(400)]
    public void MakeCenter_KeepsEveryBodyInTheSamePlaceRelativeToTheOthers(double time)
    {
        List<Body> bodies = SunPlanetMoon();
        // Elongated and tilted, so the flip is checked for every kind of orbit.
        bodies[1].Orbit = bodies[1].Orbit! with
        {
            Eccentricity = 0.3,
            ClosestApproachDegrees = 40,
            TiltDegrees = 12,
            TiltDirectionDegrees = 70,
            StartAngleDegrees = 200,
        };
        Dictionary<Guid, Vector3D> before = SystemPositions.At(bodies, time);

        foreach ((Guid id, Orbit? orbit) in SystemHierarchy.MakeCenter(bodies, _moonId))
        {
            bodies.First(b => b.Id == id).Orbit = orbit;
        }

        Dictionary<Guid, Vector3D> after = SystemPositions.At(bodies, time);
        Assert.Null(SystemHierarchy.Problem(bodies));
        Assert.Equal(Vector3D.Zero, after[_moonId]);
        foreach (Body body in bodies)
        {
            Vector3D expected = before[body.Id] - before[_moonId];
            Assert.Equal(expected.X, after[body.Id].X, 1e-3);
            Assert.Equal(expected.Y, after[body.Id].Y, 1e-3);
            Assert.Equal(expected.Z, after[body.Id].Z, 1e-3);
        }
    }

    [Fact]
    public void MakeCenter_FlipsTheChain_AndLeavesOtherOrbitsAlone()
    {
        List<Body> bodies = SunPlanetMoon();
        var outer = new Body
        {
            Name = "Outer",
            Orbit = new Orbit { ParentId = _sunId, DistanceKm = 5e8, PeriodDays = 4000 },
        };
        bodies.Add(outer);

        Dictionary<Guid, Orbit?> changes = SystemHierarchy.MakeCenter(bodies, _planetId);

        Assert.Null(changes[_planetId]);
        Assert.Equal(_planetId, changes[_sunId]!.ParentId);  // The sun now circles the planet
        Assert.Equal(365, changes[_sunId]!.PeriodDays);       // ...once a year
        Assert.False(changes.ContainsKey(_moonId));           // The moon still orbits the planet
        Assert.False(changes.ContainsKey(outer.Id));          // The outer planet: still the sun
    }

    [Fact]
    public void MakeCenter_OfTheCenter_ChangesNothing()
    {
        Assert.Empty(SystemHierarchy.MakeCenter(SunPlanetMoon(), _sunId));
    }

    [Fact]
    public void Year_IsThePlanetsTripAroundItsStar_EvenForAMoon()
    {
        List<Body> bodies = SunPlanetMoon();

        Assert.Equal(365, BodyClock.YearDays(bodies, bodies[1]));  // The planet
        Assert.Equal(365, BodyClock.YearDays(bodies, bodies[2]));  // Its moon
    }

    [Fact]
    public void Year_InAPlanetCenteredSystem_IsTheStarsTripAroundThePlanet()
    {
        List<Body> bodies = SunPlanetMoon();
        bodies[1].Orbit = bodies[1].Orbit! with { PeriodDays = 500 };
        foreach ((Guid id, Orbit? orbit) in SystemHierarchy.MakeCenter(bodies, _planetId))
        {
            bodies.First(b => b.Id == id).Orbit = orbit;
        }

        Assert.Equal(500, BodyClock.YearDays(bodies, bodies[1]));  // The planet, now central
        Assert.Equal(500, BodyClock.YearDays(bodies, bodies[2]));  // Its moon
    }

    [Fact]
    public void Year_WithoutAStar_IsAnEarthYear()
    {
        List<Body> bodies = SunPlanetMoon();

        Assert.Equal(365.25, BodyClock.YearDays(bodies, bodies[0]));  // The sun itself
        var lonePlanet = new Body();
        Assert.Equal(365.25, BodyClock.YearDays([lonePlanet], lonePlanet));
    }

    [Theory]
    [InlineData(0, 24, 0)]
    [InlineData(6371, 0, 0)]
    [InlineData(6371, 24, 181)]
    [InlineData(double.NaN, 24, 0)]
    public void Body_InvalidSizeDayOrTilt_IsAProblem(double radius, double day, double tilt)
    {
        var body = new Body { RadiusKm = radius, DayLengthHours = day, AxialTiltDegrees = tilt };

        Assert.NotNull(body.Problem());
    }

    [Fact]
    public void Body_WithAnInvalidOrbit_IsAProblem()
    {
        var body = new Body
        {
            Orbit = new Orbit { ParentId = _sunId, DistanceKm = 100, PeriodDays = -5 },
        };

        Assert.Contains("period", body.Problem());
    }

    private static List<Body> SunPlanetMoon()
    {
        return
        [
            new Body { Id = _sunId, Name = "Sun", Kind = BodyKind.Star, RadiusKm = 696_000 },
            new Body
            {
                Id = _planetId,
                Name = "Planet",
                Orbit = new Orbit { ParentId = _sunId, DistanceKm = 150_000_000, PeriodDays = 365 },
            },
            new Body
            {
                Id = _moonId,
                Name = "Moon",
                Kind = BodyKind.Moon,
                Orbit = new Orbit { ParentId = _planetId, DistanceKm = 384_400, PeriodDays = 25 },
            },
        ];
    }
}
