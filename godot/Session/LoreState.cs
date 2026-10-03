using NothicWorlds.Core.Model;

namespace NothicWorlds.Session;

/// <summary>
/// A copy of a world's regions, weather pins, journal, timelines, and events, for undo and for
/// telling whether the world still matches its saved file (VISION.md LORE-01, LORE-02, LORE-03,
/// WTH-01). They are immutable records, so copying the lists is enough: the copies share them.
/// </summary>
internal sealed record LoreState(
    IReadOnlyList<Region> Regions,
    IReadOnlyList<WeatherPin> WeatherPins,
    IReadOnlyList<JournalEntry> Journal,
    IReadOnlyList<Timeline> Timelines,
    IReadOnlyList<TimelineEvent> Events)
{
    /// <summary>The world's lore as it is now.</summary>
    public static LoreState Of(World world)
    {
        return new LoreState([.. world.Regions], [.. world.WeatherPins], [.. world.Journal],
            [.. world.Timelines], [.. world.Events]);
    }

    /// <summary>True if the world's lore is exactly this.</summary>
    public bool Matches(World world)
    {
        return Regions.SequenceEqual(world.Regions)
            && WeatherPins.SequenceEqual(world.WeatherPins)
            && Journal.SequenceEqual(world.Journal)
            && Timelines.SequenceEqual(world.Timelines)
            && Events.SequenceEqual(world.Events);
    }

    /// <summary>Puts this lore back into the world, replacing what's there.</summary>
    public void RestoreTo(World world)
    {
        world.Regions.Clear();
        world.Regions.AddRange(Regions);
        world.WeatherPins.Clear();
        world.WeatherPins.AddRange(WeatherPins);
        world.Journal.Clear();
        world.Journal.AddRange(Journal);
        world.Timelines.Clear();
        world.Timelines.AddRange(Timelines);
        world.Events.Clear();
        world.Events.AddRange(Events);
    }
}
