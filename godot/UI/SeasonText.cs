using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// Words for seasons, solstices, and equinoxes (VISION.md CAL-03), shared by the time bar, the
/// System panel, and the orbit markers. A flat world's year turns on midsummer and midwinter
/// instead, with one season everywhere (VISION.md BOD-02).
/// </summary>
public static class SeasonText
{
    /// <summary>
    /// An event's name, from the north's point of view: "Northern summer solstice" (or
    /// "Midsummer" on a flat world).
    /// </summary>
    public static string Name(SeasonEventKind kind)
    {
        return kind switch
        {
            SeasonEventKind.Midsummer => "Midsummer",
            SeasonEventKind.Midwinter => "Midwinter",
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

    /// <summary>
    /// What else is true at that moment: "southern winter begins", or on a flat world, "the
    /// noon sun is highest everywhere".
    /// </summary>
    public static string Note(SeasonEventKind kind)
    {
        return kind switch
        {
            SeasonEventKind.Midsummer => "the noon sun is highest everywhere",
            SeasonEventKind.Midwinter => "the noon sun is lowest everywhere",
            _ => $"southern {Lower(Seasons.SeasonsAfter(kind).Southern)} begins",
        };
    }

    /// <summary>
    /// Both hemispheres' seasons: "Northern summer, southern winter". A flat world has one
    /// season everywhere (both the same): "Summer".
    /// </summary>
    public static string Current((Season Northern, Season Southern) seasons)
    {
        return seasons.Northern == seasons.Southern
            ? seasons.Northern.ToString()
            : $"Northern {Lower(seasons.Northern)}, southern {Lower(seasons.Southern)}";
    }

    /// <summary>
    /// What the time bar's season line shows, for its tooltip: the seasons by hemisphere, or a
    /// flat world's one season.
    /// </summary>
    public static string Explanation(Body body) => body.Shape == BodyShape.FlatDisc
        ? "The season on this flat world, from how high the noon sun climbs (the same " +
            "everywhere)."
        : "The seasons in each hemisphere, from where the star stands.";

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
