using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class CalendarFittingTests
{
    [Fact]
    public void FittingTheYearLength_SetsTheOrbitToExactlyOneCalendarYear()
    {
        (List<Body> bodies, Body planet, _, _) = System(dayHours: 26);
        planet.Calendar = Months(12, 30) with { Fit = CalendarFit.YearLength };

        Assert.True(CalendarFitting.Apply(bodies));

        Assert.Equal(360 * 26 / 24.0, planet.Orbit!.PeriodDays, 9);
        Assert.Equal(360, YearInBodyDays(bodies, planet), 9);
    }

    [Fact]
    public void FittingTheDayLength_SpinsTheBodySoTheYearFits()
    {
        (List<Body> bodies, Body planet, _, _) = System();
        planet.Calendar = Months(12, 30) with { Fit = CalendarFit.DayLength };

        CalendarFitting.Apply(bodies);

        Assert.Equal(365.25, planet.Orbit!.PeriodDays);  // The year itself stays.
        Assert.Equal(365.25 * 24 / 360, planet.DayLengthHours, 9);
        Assert.Equal(360, YearInBodyDays(bodies, planet), 9);
    }

    [Fact]
    public void AMoonFittingItsYearLength_SetsItsPlanetsOrbit()
    {
        (List<Body> bodies, Body planet, Body moon, _) = System();
        moon.Calendar = Months(10, 30) with { Fit = CalendarFit.YearLength };

        CalendarFitting.Apply(bodies);

        Assert.Equal(300 * moon.DayLengthHours / 24, planet.Orbit!.PeriodDays, 9);
        Assert.Equal(300, YearInBodyDays(bodies, moon), 9);
    }

    [Fact]
    public void InAPlanetCenteredSystem_TheStarsOrbitIsFitted()
    {
        var planet = new Body { Name = "Terra", Kind = BodyKind.Planet };
        var star = new Body
        {
            Name = "Sol",
            Kind = BodyKind.Star,
            Orbit = new Orbit { ParentId = planet.Id, DistanceKm = 1e8, PeriodDays = 400 },
        };
        planet.Calendar = Months(12, 30) with { Fit = CalendarFit.YearLength };
        List<Body> bodies = [planet, star];

        CalendarFitting.Apply(bodies);

        Assert.Equal(360, star.Orbit!.PeriodDays, 9);
    }

    [Fact]
    public void ApplyingTwice_ChangesNothingTheSecondTime()
    {
        (List<Body> bodies, Body planet, _, _) = System();
        planet.Calendar = Months(12, 30) with { Fit = CalendarFit.YearLength };

        CalendarFitting.Apply(bodies);

        Assert.False(CalendarFitting.Apply(bodies));
    }

    [Fact]
    public void AnUnfittedCalendar_ChangesNothing()
    {
        (List<Body> bodies, Body planet, _, _) = System();
        planet.Calendar = Months(12, 30);

        Assert.False(CalendarFitting.Apply(bodies));
        Assert.Equal(365.25, planet.Orbit!.PeriodDays);
    }

    [Theory]
    [InlineData(0.0)]    // Circling the same way round as its planet's year
    [InlineData(180.0)]  // Circling the other way round
    public void AMonthMoon_GoesFromNewMoonToNewMoonOncePerMonth(double moonTilt)
    {
        (List<Body> bodies, Body planet, Body moon, _) = System();
        moon.Orbit = moon.Orbit! with { TiltDegrees = moonTilt };
        planet.Calendar = Months(12, 30) with { MonthMoonId = moon.Id };

        CalendarFitting.Apply(bodies);

        double month = 30 * planet.DayLengthHours / 24;
        Assert.InRange(Math.Abs(PhaseChange(planet, moon, month / 2)), Math.PI - 1e-6,
            Math.PI);  // The opposite phase halfway through the month
        Assert.InRange(PhaseChange(planet, moon, month), -1e-9, 1e-9);  // The same again
        Assert.InRange(PhaseChange(planet, moon, 12 * month), -1e-9, 1e-9);
    }

    [Fact]
    public void AMonthMoon_UsesTheAverageMonth()
    {
        (List<Body> bodies, Body planet, Body moon, _) = System();
        planet.Calendar = new Calendar
        {
            Months = [new("Long", 31), new("Short", 29)],
            MonthMoonId = moon.Id,
        };

        CalendarFitting.Apply(bodies);

        Assert.InRange(PhaseChange(planet, moon, 30), -1e-6, 1e-6);
    }

    [Fact]
    public void YearAndMonthFits_Combine()
    {
        (List<Body> bodies, Body planet, Body moon, _) = System();
        planet.Calendar = Months(12, 30) with
        {
            Fit = CalendarFit.YearLength,
            MonthMoonId = moon.Id,
        };

        CalendarFitting.Apply(bodies);

        // Exactly twelve months, and twelve new moons, a year.
        Assert.Equal(360, planet.Orbit!.PeriodDays, 9);
        Assert.InRange(PhaseChange(planet, moon, 360), -1e-6, 1e-6);
    }

    [Fact]
    public void FittedBy_NamesTheCalendarThatSetsEachValue()
    {
        (List<Body> bodies, Body planet, Body moon, _) = System();
        planet.Calendar = Months(12, 30) with
        {
            Fit = CalendarFit.YearLength,
            MonthMoonId = moon.Id,
        };
        moon.Calendar = Months(10, 30) with { Fit = CalendarFit.DayLength };

        Assert.Equal((null, planet), CalendarFitting.FittedBy(bodies, planet));
        Assert.Equal((moon, planet), CalendarFitting.FittedBy(bodies, moon));
    }

    [Fact]
    public void Preview_ListsTheChanges_WithoutMakingThem()
    {
        (List<Body> bodies, Body planet, _, _) = System();
        Calendar calendar = Months(12, 30) with { Fit = CalendarFit.YearLength };

        IReadOnlyList<FitChange> changes = CalendarFitting.Preview(bodies, planet, calendar);

        FitChange change = Assert.Single(changes);
        Assert.Equal((planet.Id, FitTarget.OrbitPeriod, 365.25, 360.0),
            (change.BodyId, change.What, change.Before, change.After));
        Assert.Equal(365.25, planet.Orbit!.PeriodDays);
        Assert.Null(planet.Calendar);
    }

    [Fact]
    public void Problem_FittingWithoutAStar_IsExplained()
    {
        var planet = new Body { Name = "Rogue" };
        Calendar calendar = Months(12, 30) with { Fit = CalendarFit.DayLength };

        Assert.Contains("needs a star", CalendarFitting.Problem([planet], planet, calendar));
    }

    [Fact]
    public void Problem_TwoCalendarsSettingTheSameOrbit_IsRefused()
    {
        (List<Body> bodies, Body planet, Body moon, _) = System();
        planet.Calendar = Months(12, 30) with { Fit = CalendarFit.YearLength };

        string? problem = CalendarFitting.Problem(
            bodies, moon, Months(10, 30) with { Fit = CalendarFit.YearLength });

        Assert.Contains("already sets", problem);
        Assert.Null(CalendarFitting.Problem(
            bodies, moon, Months(10, 30) with { Fit = CalendarFit.DayLength }));
    }

    [Fact]
    public void Problem_AMonthMoonMustCircleTheBody()
    {
        (List<Body> bodies, Body planet, _, Body star) = System();

        string? problem = CalendarFitting.Problem(
            bodies, planet, Months(12, 30) with { MonthMoonId = star.Id });

        Assert.Contains("month moon", problem);
    }

    [Fact]
    public void Problem_AMonthNoMoonCanMatch_IsExplained()
    {
        // A moon circling the other way round can't take a month longer than the year.
        (List<Body> bodies, Body planet, Body moon, _) = System();
        moon.Orbit = moon.Orbit! with { TiltDegrees = 180 };

        string? problem = CalendarFitting.Problem(
            bodies, planet, Months(1, 400) with { MonthMoonId = moon.Id });

        Assert.Contains("stand still", problem);
    }

    [Fact]
    public void Problem_AFittedValueOutOfRange_IsExplained()
    {
        (List<Body> bodies, Body planet, _, _) = System(dayHours: 1e5);
        Calendar calendar = Months(100, 100_000) with { Fit = CalendarFit.YearLength };

        Assert.Contains("orbit would take", CalendarFitting.Problem(bodies, planet, calendar));
    }

    // A star, an Earth-like planet circling it once a year, and a moon of the planet.
    private static (List<Body> Bodies, Body Planet, Body Moon, Body Star) System(
        double dayHours = 24)
    {
        var star = new Body { Name = "Sol", Kind = BodyKind.Star, RadiusKm = 696_000 };
        var planet = new Body
        {
            Name = "Terra",
            DayLengthHours = dayHours,
            Orbit = new Orbit { ParentId = star.Id, DistanceKm = 1.496e8, PeriodDays = 365.25 },
        };
        var moon = new Body
        {
            Name = "Luna",
            Kind = BodyKind.Moon,
            RadiusKm = 1737,
            DayLengthHours = 655,
            Orbit = new Orbit { ParentId = planet.Id, DistanceKm = 384_400, PeriodDays = 27.3 },
        };
        return ([star, planet, moon], planet, moon, star);
    }

    private static Calendar Months(int count, int days) => new()
    {
        Months = [.. Enumerable.Range(1, count).Select(i => new CalendarMonth($"M{i}", days))],
    };

    private static double YearInBodyDays(List<Body> bodies, Body body) =>
        BodyClock.YearDays(bodies, body) * 24 / body.DayLengthHours;

    // How far the moon's phase has moved since time 0, in radians from -π to π (0: the same
    // phase again, ±π: the opposite one). The phase is the angle from the star to the moon,
    // seen from the planet. The test orbits lie flat, so angles in the plane are exact.
    private static double PhaseChange(Body planet, Body moon, double timeDays)
    {
        return Math.IEEERemainder(Phase(planet, moon, timeDays) - Phase(planet, moon, 0),
            2 * Math.PI);
    }

    private static double Phase(Body planet, Body moon, double timeDays)
    {
        Vector3D toStar = OrbitMath.OffsetFromParent(planet.Orbit!, timeDays) * -1;
        Vector3D toMoon = OrbitMath.OffsetFromParent(moon.Orbit!, timeDays);
        return Math.Atan2(toMoon.Z, toMoon.X) - Math.Atan2(toStar.Z, toStar.X);
    }
}
