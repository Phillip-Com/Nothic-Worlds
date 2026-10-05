using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// Words for the live weather at a spot right now (VISION.md WTH-02; owner's choice: weather
/// pins report today's weather), such as "Stormy · heavy rain, 6.2 mm an hour · wind from the
/// west at 40 km/h".
/// </summary>
public static class LiveWeatherText
{
    private static readonly string[] _compass =
        ["north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest"];

    /// <summary>The sky, what's falling, and the wind, in one line.</summary>
    public static string Describe(WeatherSample sample) =>
        $"{Sky(sample)} · {Falling(sample)} · {Wind(sample)}";

    private static string Sky(WeatherSample sample) => sample.Storm switch
    {
        StormKind.Tropical => "In a tropical storm",
        StormKind.Cyclone => "Stormy",
        _ => sample.CloudCover switch
        {
            < 0.1 => "Clear",
            < 0.4 => "Partly cloudy",
            < 0.85 => "Mostly cloudy",
            _ => "Overcast",
        },
    };

    // How hard rain or snow falls (in mm of water an hour), in words, with the amount.
    private static string Falling(WeatherSample sample)
    {
        if (sample.Precipitation == PrecipitationKind.None)
        {
            return "dry";
        }

        string what = sample.Precipitation == PrecipitationKind.Snow ? "snow" : "rain";
        double rate = sample.PrecipitationMmPerHour;
        if (rate < 0.05)
        {
            return $"a trace of {what}";  // Too little to measure (under 0.05 mm)
        }

        string how = rate < 0.5 ? $"light {what}" : rate < 4 ? what : $"heavy {what}";
        int decimals = UnitText.System == UnitSystem.Imperial ? 2 : 1;
        return $"{how}, {UnitText.Format(Quantity.Precipitation, rate, decimals)} an hour";
    }

    private static string Wind(WeatherSample sample)
    {
        double kmPerHour = sample.WindSpeedMs * 3.6;
        if (kmPerHour < 2)
        {
            return "calm";
        }

        int point = (int)Math.Round(sample.WindFromDegrees / 45) % _compass.Length;
        return $"wind from the {_compass[point]} at {UnitText.Format(Quantity.Speed, kmPerHour)}";
    }
}
