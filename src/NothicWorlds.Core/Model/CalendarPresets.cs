namespace NothicWorlds.Core.Model;

/// <summary>
/// Ready-made calendars to start a body's calendar from (VISION.md CAL-06; owner's choice of
/// four), each the user's own to rename and reshape after. None of them is saved as a kind: a
/// calendar made from one is an ordinary calendar.
/// </summary>
public static class CalendarPresets
{
    /// <summary>The four starting points, in the order they're offered.</summary>
    public enum Kind
    {
        /// <summary>Twelve months sharing the body's own year, a seven-day week.</summary>
        Fitted,

        /// <summary>Earth's months, weeks, and leap years.</summary>
        Gregorian,

        /// <summary>Thirteen four-week months and a year's-end day.</summary>
        ThirteenMonths,

        /// <summary>Twelve months of three ten-day weeks, and five festival days.</summary>
        TenDayWeeks,
    }

    private static readonly string[] _sevenDays =
        ["Moonday", "Tirsday", "Wenday", "Thorday", "Freyday", "Starday", "Sunday"];

    /// <summary>
    /// A calendar of <paramref name="kind"/> for a body whose year is
    /// <paramref name="yearDays"/> of its own days. Only <see cref="Kind.Fitted"/> follows the
    /// body's year (with leap years where it's longer than the months, if a simple rule fits);
    /// the others are their own length, for the user to fit (the calendar editor shows how
    /// far it drifts).
    /// </summary>
    public static Calendar Make(Kind kind, double yearDays) => kind switch
    {
        Kind.Gregorian => Gregorian(),
        Kind.ThirteenMonths => ThirteenMonths(),
        Kind.TenDayWeeks => TenDayWeeks(),
        _ => Fitted(yearDays),
    };

    // Twelve months sharing the year's whole days (the first ones a day longer), a seven-day
    // week, and leap days in the last month for any part day left over.
    private static Calendar Fitted(double yearDays)
    {
        int totalDays = (int)Math.Clamp(Math.Floor(yearDays), 12, 12L * Calendar.MaxMonthDays);
        LeapRule? leap = LeapRule.Suggest(yearDays - totalDays, 11);
        return new Calendar
        {
            Months = [.. Enumerable.Range(0, 12).Select(i => new CalendarMonth(
                $"Month {i + 1}", totalDays / 12 + (i < totalDays % 12 ? 1 : 0)))],
            Weekdays = [.. _sevenDays],
            Leap = leap,
        };
    }

    private static Calendar Gregorian() => new()
    {
        Months = [new("January", 31), new("February", 28), new("March", 31), new("April", 30),
            new("May", 31), new("June", 30), new("July", 31), new("August", 31),
            new("September", 30), new("October", 31), new("November", 30),
            new("December", 31)],
        Weekdays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
            "Sunday"],
        Leap = new LeapRule(4, 100, 400, 1, 1),
    };

    // 13 × 28 = 364 days, every month starting on the same weekday, and one day to end the year.
    private static Calendar ThirteenMonths() => new()
    {
        Months = [.. new[]
            {
                "Frostmoon", "Snowmoon", "Thawmoon", "Rainmoon", "Seedmoon", "Bloommoon",
                "Sunmoon", "Goldmoon", "Harvestmoon", "Fallmoon", "Mistmoon", "Darkmoon",
                "Starmoon",
            }.Select(name => new CalendarMonth(name, 28)),
            new("Year's End", 1)],
        Weekdays = [.. _sevenDays],
    };

    // 12 × 30 = 360 days in ten-day weeks, then five festival days.
    private static Calendar TenDayWeeks() => new()
    {
        Months = [.. new[]
            {
                "Deepwinter", "Clearsky", "Thawing", "Rainfall", "Greening", "Blossom",
                "Highsun", "Harvest", "Goldleaf", "Fading", "Mists", "Longnight",
            }.Select(name => new CalendarMonth(name, 30)),
            new("Festival Days", 5)],
        Weekdays = ["Firstday", "Secondday", "Thirdday", "Fourthday", "Fifthday", "Sixthday",
            "Seventhday", "Eighthday", "Ninthday", "Tenthday"],
    };
}
