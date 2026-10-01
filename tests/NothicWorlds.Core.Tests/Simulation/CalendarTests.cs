using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class CalendarTests
{
    // Three months (10 + 20 + 5 = 35 days a year), a three-day week, starting on day 5 of the
    // second month in year 1203, on the second weekday.
    private static readonly Calendar _calendar = new()
    {
        Months = [new("Frost", 10), new("Highsun", 20), new("Ember", 5)],
        Weekdays = ["Moonday", "Sunday", "Starday"],
        FirstYear = 1203,
        Era = "of the Third Age",
        StartMonth = 1,
        StartDay = 5,
        StartWeekday = 1,
    };

    [Fact]
    public void TimeZero_IsTheStartDate()
    {
        Assert.Equal(new CalendarDate(1203, 1, 5, 1), CalendarMath.DateOf(_calendar, 0));
    }

    [Theory]
    [InlineData(1, 1203, 1, 6, 2)]     // The next day
    [InlineData(15, 1203, 1, 20, 1)]   // Last day of Highsun
    [InlineData(16, 1203, 2, 1, 2)]    // Into Ember
    [InlineData(21, 1204, 0, 1, 1)]    // New year
    [InlineData(-5, 1203, 0, 10, 2)]   // Back into Frost
    [InlineData(-14, 1203, 0, 1, 2)]   // The first day of the year
    [InlineData(-15, 1202, 2, 5, 1)]   // Back into last year
    public void DaysCount_ThroughMonthsAndYears(
        long dayIndex, long year, int month, int day, int weekday)
    {
        Assert.Equal(new CalendarDate(year, month, day, weekday),
            CalendarMath.DateOf(_calendar, dayIndex));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    [InlineData(-1000)]
    [InlineData(123_456_789)]
    public void DayIndexOf_UndoesDateOf(long dayIndex)
    {
        CalendarDate date = CalendarMath.DateOf(_calendar, dayIndex);

        Assert.Equal(dayIndex, CalendarMath.DayIndexOf(_calendar, date.Year, date.Month, date.Day));
    }

    [Fact]
    public void Format_ReadsLikeADate()
    {
        Assert.Equal("6 Highsun 1203 of the Third Age, Starday",
            CalendarMath.Format(_calendar, CalendarMath.DateOf(_calendar, 1)));
    }

    [Fact]
    public void Format_WithoutWeeksOrEra()
    {
        Calendar plain = _calendar with { Weekdays = [], Era = null, StartWeekday = 0 };

        Assert.Equal("5 Highsun 1203", CalendarMath.Format(plain, CalendarMath.DateOf(plain, 0)));
    }

    [Fact]
    public void Describe_UsesTheBodysCalendar_OrPlainDays()
    {
        var body = new Body { DayLengthHours = 24 };
        double time = 1 + 14.5 / 24;  // Second day, 14:30

        Assert.Equal("Day 2, 14:30", BodyClock.Describe(body, time));
        body.Calendar = _calendar;
        Assert.Equal("6 Highsun 1203 of the Third Age, Starday, 14:30",
            BodyClock.Describe(body, time));
    }

    [Fact]
    public void TimeAt_IsTheStartOfTheDayPlusHours()
    {
        var body = new Body { DayLengthHours = 30 };

        Assert.Equal((2 * 30 + 6) / 24.0, BodyClock.TimeAt(body, 2, 6), 1e-12);
        Assert.Equal(new LocalTime(3, 6, 0),
            BodyClock.LocalTimeOn(body, BodyClock.TimeAt(body, 2, 6)));
    }

    [Fact]
    public void Equality_ComparesTheLists()
    {
        Calendar copy = _calendar with
        {
            Months = [new("Frost", 10), new("Highsun", 20), new("Ember", 5)],
        };

        Assert.Equal(_calendar, copy);
        Assert.NotEqual(_calendar, copy with { Months = [new("Frost", 11)] });
    }

    [Theory]
    [InlineData("no months")]
    [InlineData("empty month name")]
    [InlineData("zero-day month")]
    [InlineData("start day past month")]
    [InlineData("start weekday past week")]
    [InlineData("blank weekday")]
    public void Problem_FindsBadCalendars(string what)
    {
        Calendar bad = what switch
        {
            "no months" => _calendar with { Months = [] },
            "empty month name" => _calendar with { Months = [new(" ", 10)], StartMonth = 0 },
            "zero-day month" => _calendar with { Months = [new("A", 0)], StartMonth = 0 },
            "start day past month" => _calendar with { StartDay = 21 },
            "start weekday past week" => _calendar with { StartWeekday = 3 },
            _ => _calendar with { Weekdays = ["Moonday", ""] },
        };

        Assert.NotNull(bad.Problem());
        Assert.Null(_calendar.Problem());
    }
}
