using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Tests.Model;

public class CalendarPresetsTests
{
    [Theory]
    [InlineData(CalendarPresets.Kind.Fitted)]
    [InlineData(CalendarPresets.Kind.Gregorian)]
    [InlineData(CalendarPresets.Kind.ThirteenMonths)]
    [InlineData(CalendarPresets.Kind.TenDayWeeks)]
    public void EveryPreset_IsAUsableCalendar(CalendarPresets.Kind kind)
    {
        Assert.Null(CalendarPresets.Make(kind, 365.2422).Problem());
    }

    [Theory]
    [InlineData(CalendarPresets.Kind.Gregorian, 365.2425)]
    [InlineData(CalendarPresets.Kind.ThirteenMonths, 365)]
    [InlineData(CalendarPresets.Kind.TenDayWeeks, 365)]
    public void Presets_HaveTheirOwnLength(CalendarPresets.Kind kind, double expected)
    {
        Assert.Equal(expected, CalendarPresets.Make(kind, 500).AverageDaysPerYear, 9);
    }

    [Theory]
    [InlineData(365.2422)]
    [InlineData(400.5)]
    [InlineData(97)]
    public void TheFittedCalendar_KeepsUpWithTheBodysYear(double yearDays)
    {
        Calendar fitted = CalendarPresets.Make(CalendarPresets.Kind.Fitted, yearDays);

        Assert.Equal(12, fitted.Months.Count);
        Assert.InRange(fitted.AverageDaysPerYear, yearDays - 0.01, yearDays + 0.01);
    }

    [Fact]
    public void TheGregorianCalendar_HasEarthsLeapYears()
    {
        Calendar gregorian = CalendarPresets.Make(CalendarPresets.Kind.Gregorian, 0);

        Assert.Equal(29, gregorian.DaysInMonth(2024, 1));
        Assert.Equal(28, gregorian.DaysInMonth(1900, 1));
        Assert.Equal(29, gregorian.DaysInMonth(2000, 1));
    }

    [Fact]
    public void TenDayWeeks_HaveTenWeekdays()
    {
        Assert.Equal(10,
            CalendarPresets.Make(CalendarPresets.Kind.TenDayWeeks, 0).Weekdays.Count);
    }
}
