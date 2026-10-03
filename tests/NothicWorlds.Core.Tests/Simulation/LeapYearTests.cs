using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class LeapYearTests
{
    private static readonly LeapRule _gregorian = new(4, 100, 400, 1, 1);

    [Theory]
    [InlineData(2024, true)]
    [InlineData(2023, false)]
    [InlineData(1900, false)]  // Divides by 100
    [InlineData(2000, true)]   // ...but also by 400
    [InlineData(0, true)]
    [InlineData(-4, true)]
    [InlineData(-100, false)]
    public void TheGregorianRule_PicksTheRightYears(long year, bool leap)
    {
        Assert.Equal(leap, _gregorian.IsLeap(year));
    }

    [Fact]
    public void TheAverageYear_CountsEveryTier()
    {
        Assert.Equal(0.2425, _gregorian.AverageExtraDays, 12);
        Assert.Equal(365.2425, Gregorian(2000).AverageDaysPerYear, 12);
    }

    [Fact]
    public void ALeapYear_AddsItsDayToTheChosenMonth()
    {
        Calendar calendar = Gregorian(2024);

        Assert.Equal(29, calendar.DaysInMonth(2024, 1));
        Assert.Equal(28, calendar.DaysInMonth(2023, 1));
        Assert.Equal(31, calendar.DaysInMonth(2024, 0));
    }

    [Fact]
    public void Dates_RunThroughTheLeapDay()
    {
        Calendar calendar = Gregorian(2024);  // Day 0 is 1 January 2024

        Assert.Equal((2024, 1, 29), Date(calendar, 31 + 28));        // 29 February
        Assert.Equal((2024, 2, 1), Date(calendar, 31 + 29));         // 1 March
        Assert.Equal((2025, 0, 1), Date(calendar, 366));             // A leap year is 366 days
        Assert.Equal((2026, 0, 1), Date(calendar, 366 + 365));
    }

    [Fact]
    public void Dates_MatchOurCalendarOverFourCenturies()
    {
        // 1 January 1600 to 1 January 2000 is 146,097 days in the Gregorian calendar.
        Calendar calendar = Gregorian(1600);

        Assert.Equal((2000, 0, 1), Date(calendar, 146_097));
        Assert.Equal((1599, 11, 31), Date(calendar, -1));
    }

    [Fact]
    public void DatesAndDayIndexes_RoundTrip_EvenFarAway()
    {
        Calendar calendar = Gregorian(1) with { StartMonth = 2, StartDay = 15 };
        foreach (long day in new long[] { 0, 1, 59, 365, 1460, 1461, -1, -366, 36_524, -146_097,
            3_652_425, -3_652_425 })
        {
            CalendarDate date = CalendarMath.DateOf(calendar, day);
            Assert.Equal(day, CalendarMath.DayIndexOf(calendar, date.Year, date.Month, date.Day));
        }
    }

    [Fact]
    public void Weekdays_KeepCountingThroughLeapDays()
    {
        Calendar calendar = Gregorian(2024) with
        {
            Weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
            StartWeekday = 0,  // 1 January 2024 was a Monday
        };

        CalendarDate date = CalendarMath.DateOf(calendar, 366);  // 1 January 2025

        Assert.Equal(2, date.Weekday);  // A Wednesday
    }

    [Fact]
    public void Suggest_FindsAGoodRuleForEarthsYear()
    {
        LeapRule rule = LeapRule.Suggest(365.2422 - 365, month: 1)!;

        Assert.True(rule.YearsPerDayOfDrift(0.2422) is null or > 2000);
        Assert.Equal(4, rule.Every);
        Assert.Equal(1, rule.Month);
    }

    [Fact]
    public void Suggest_KeepsItSimpleWhenOneTierIsEnough()
    {
        LeapRule rule = LeapRule.Suggest(0.25, month: 0)!;

        Assert.Equal((4, (int?)null, (int?)null), (rule.Every, rule.Except, rule.ExceptAgain));
        Assert.Null(rule.YearsPerDayOfDrift(0.25));
    }

    [Theory]
    [InlineData(-0.3)]   // The year is shorter than the months
    [InlineData(1.5)]    // Longer by more than a day
    [InlineData(0.0)]    // Already exact
    public void Suggest_GivesUpWhenAddingADayCantHelp(double extraDays)
    {
        Assert.Null(LeapRule.Suggest(extraDays, month: 0));
    }

    [Theory]
    [InlineData(0, null, null, 0, 1)]     // Every 0 years
    [InlineData(4, 6, null, 0, 1)]        // 6 isn't a multiple of 4
    [InlineData(4, 100, 350, 0, 1)]       // 350 isn't a multiple of 100
    [InlineData(4, null, 400, 0, 1)]      // A second exception without a first
    [InlineData(4, null, null, 12, 1)]    // No such month
    [InlineData(4, null, null, 0, 0)]     // Adds no days
    public void Problem_FindsBadRules(
        int every, int? except, int? again, int month, int days)
    {
        Assert.NotNull(new LeapRule(every, except, again, month, days).Problem(12));
    }

    [Fact]
    public void FittingTheYear_UsesTheAverageCalendarYear()
    {
        World world = World.CreateNew();
        Body planet = world.Bodies[0];
        planet.Calendar = Gregorian(2000) with { Fit = CalendarFit.YearLength };

        CalendarFitting.Apply(world.Bodies);

        Assert.Equal(365.2425, planet.Orbit!.PeriodDays, 9);
    }

    private static (long, int, int) Date(Calendar calendar, long day)
    {
        CalendarDate date = CalendarMath.DateOf(calendar, day);
        return (date.Year, date.Month, date.Day);
    }

    // Our calendar's months, starting on 1 January of `firstYear`.
    private static Calendar Gregorian(long firstYear) => new()
    {
        Months = [new("January", 31), new("February", 28), new("March", 31), new("April", 30),
            new("May", 31), new("June", 30), new("July", 31), new("August", 31),
            new("September", 30), new("October", 31), new("November", 30),
            new("December", 31)],
        FirstYear = firstYear,
        Leap = _gregorian,
    };
}
