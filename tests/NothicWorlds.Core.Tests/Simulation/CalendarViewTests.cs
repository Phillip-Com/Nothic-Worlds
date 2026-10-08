using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

// The calendar view's sums (VISION.md CAL-05): month pages, stepping by months and years, and
// moon phases.
public class CalendarViewTests
{
    // A Gregorian calendar starting on Monday 1 January 2024 (a leap year).
    private static readonly Calendar _gregorian = new()
    {
        Months = [new("January", 31), new("February", 28), new("March", 31), new("April", 30),
            new("May", 31), new("June", 30), new("July", 31), new("August", 31),
            new("September", 30), new("October", 31), new("November", 30),
            new("December", 31)],
        Weekdays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
            "Sunday"],
        FirstYear = 2024,
        Leap = new LeapRule(4, 100, 400, 1, 1),
    };

    [Fact]
    public void AMonthPage_StartsInTheRightColumn_WithItsDays()
    {
        MonthPage march = MonthPage.Of(_gregorian, 2024, 2);

        Assert.Equal("March", march.Name);
        Assert.Equal(31, march.Days);
        Assert.Equal(31 + 29, march.FirstDayIndex);
        Assert.Equal(4, march.FirstWeekday);  // 1 March 2024 was a Friday
        Assert.Equal(29, MonthPage.Of(_gregorian, 2024, 1).Days);  // A leap February
    }

    [Fact]
    public void MonthPages_MoveAcrossYears_BothWays()
    {
        MonthPage january = MonthPage.Of(_gregorian, 2024, 0);

        Assert.Equal((2023L, 11), Year(january.Moved(_gregorian, -1)));
        Assert.Equal((2025L, 1), Year(january.Moved(_gregorian, 13)));
        Assert.Equal(january, january.Moved(_gregorian, 12).Moved(_gregorian, -12));
    }

    [Fact]
    public void AMonthPage_HoldsItsOwnDays()
    {
        MonthPage page = MonthPage.Containing(_gregorian, 40);  // 10 February

        Assert.Equal((2024L, 1), Year(page));
        Assert.True(page.Holds(40));
        Assert.False(page.Holds(page.FirstDayIndex + page.Days));
    }

    [Theory]
    [InlineData(30, 1, 59)]      // 31 January + 1 month = 29 February (a leap year)
    [InlineData(30, 13, 366 + 58)]  // + 13 months = 28 February 2025
    [InlineData(45, -1, 14)]     // 15 February - 1 month = 15 January
    public void AddingMonths_KeepsTheDay_OrTheMonthsLast(long from, int months, long expected)
    {
        Assert.Equal(expected, CalendarMath.AddMonths(_gregorian, from, months));
    }

    [Fact]
    public void AddingAYear_FromTheLeapDay_LandsOnTheLastOfFebruary()
    {
        Assert.Equal(366 + 58, CalendarMath.AddYears(_gregorian, 59, 1));  // 28 February 2025
    }

    [Fact]
    public void StepsByMonthAndYear_KeepTheTimeOfDay()
    {
        var body = new Body { DayLengthHours = 24, Calendar = _gregorian };
        double time = 30.75;  // 31 January, 18:00

        double? month = TimeSteps.Apply([body], body, time, TimeUnit.Month, 1);

        Assert.Equal(59.75, month!.Value, 9);  // 29 February, 18:00
        Assert.Equal(time + 1, TimeSteps.Apply([body], body, time, TimeUnit.Day, 1)!.Value, 9);
        Assert.Equal(time + 1 / 24.0,
            TimeSteps.Apply([body], body, time, TimeUnit.Hour, 1)!.Value, 9);
    }

    [Fact]
    public void WithoutACalendar_ThereAreNoMonths_AndAYearIsAnOrbit()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies.First(b => b.Kind == BodyKind.Planet);

        Assert.Null(TimeSteps.Apply(world.Bodies, planet, 0, TimeUnit.Month, 1));
        Assert.Equal(BodyClock.YearDays(world.Bodies, planet),
            TimeSteps.Apply(world.Bodies, planet, 0, TimeUnit.Year, 1)!.Value, 9);
    }

    [Fact]
    public void AMoon_WaxesToFull_ThenWanes()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies.First(b => b.Kind == BodyKind.Planet);
        Body moon = NewBodies.Moon(world.Bodies, planet);
        world.Bodies.Add(moon);

        List<MoonPhase> month = [.. Enumerable.Range(0, 60).Select(i =>
            MoonPhase.Of(world.Bodies, planet, i * moon.Orbit!.PeriodDays / 60).Single())];

        Assert.True(month.Min(p => p.Lit) < 0.05);
        Assert.True(month.Max(p => p.Lit) > 0.95);
        int full = month.FindIndex(p => p.Lit == month.Max(m => m.Lit));
        int beforeFull = (full + 55) % 60, afterFull = (full + 5) % 60;
        Assert.True(month[beforeFull].Waxing);
        Assert.False(month[afterFull].Waxing);
        Assert.Equal("Full moon", month[full].PhaseName);
    }

    [Fact]
    public void ABodyWithoutMoons_HasNoPhases()
    {
        World world = World.CreateNew();

        Assert.Empty(MoonPhase.Of(world.Bodies, world.Bodies[0], 0));
    }

    private static (long, int) Year(MonthPage page) => (page.Year, page.Month);
}
