using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// Words for seasons, solstices, and equinoxes (VISION.md CAL-03), shared by the time bar, the
/// System panel, and the orbit markers.
/// </summary>
public static class SeasonText
{
    /// <summary>
    /// An event's name, from the north's point of view: "Northern summer solstice".
    /// </summary>
    public static string Name(SeasonEventKind kind)
    {
        return kind switch
        {
            SeasonEventKind.NorthernSpringEquinox => "Northern spring equinox",
            SeasonEventKind.NorthernSummerSolstice => "Northern summer solstice",
            SeasonEventKind.NorthernAutumnEquinox => "Northern autumn equinox",
            _ => "Northern winter solstice",
        };
    }

    /// <summary>A short name for labels on the orbit: "N. summer solstice".</summary>
    public static string ShortName(SeasonEventKind kind)
    {
        return Name(kind).Replace("Northern", "N.");
    }

    /// <summary>What the south has at the same moment: "southern winter begins".</summary>
    public static string SouthernNote(SeasonEventKind kind)
    {
        return $"southern {Lower(Seasons.SeasonsAfter(kind).Southern)} begins";
    }

    /// <summary>Both hemispheres' seasons: "Northern summer, southern winter".</summary>
    public static string Current((Season Northern, Season Southern) seasons)
    {
        return $"Northern {Lower(seasons.Northern)}, southern {Lower(seasons.Southern)}";
    }

    /// <summary>
    /// How far off a time is, in the body's own days: "in 23 days", "today", "5 days ago".
    /// </summary>
    public static string HowFar(Body body, double fromDays, double toDays)
    {
        long days = (long)Math.Round((toDays - fromDays) * 24.0 / body.DayLengthHours);
        return days switch
        {
            0 => "today",
            1 => "in 1 day",
            -1 => "1 day ago",
            > 0 => $"in {days:N0} days",
            _ => $"{-days:N0} days ago",
        };
    }

    private static string Lower(Season season) => season.ToString().ToLowerInvariant();
}
