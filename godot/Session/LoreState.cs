using NothicWorlds.Core.Model;

namespace NothicWorlds.Session;

/// <summary>
/// A copy of a world's regions, weather pins, rivers, lakes, journal, timelines, events,
/// relationships, and diagrams, plus its terrain types, nebulas, night sky, and visual style,
/// for undo and for telling whether the world still matches its saved file (VISION.md LORE-01
/// to LORE-04, WTH-01, BOD-05, BOD-11, BOD-03, REN-07, REN-05). They are immutable records,
/// so copying the lists is enough: the copies share them.
/// </summary>
internal sealed record LoreState(
    IReadOnlyList<TerrainType> TerrainTypes,
    IReadOnlyList<Region> Regions,
    IReadOnlyList<WeatherPin> WeatherPins,
    IReadOnlyList<River> Rivers,
    IReadOnlyList<Lake> Lakes,
    IReadOnlyList<JournalEntry> Journal,
    IReadOnlyList<Timeline> Timelines,
    IReadOnlyList<TimelineEvent> Events,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<LoreDiagram> Diagrams,
    IReadOnlyList<Nebula> Nebulas,
    int StarSeed,
    IReadOnlyList<Constellation> Constellations,
    bool TerrainShapesGround,
    VisualStyle Style)
{
    /// <summary>The world's lore as it is now.</summary>
    public static LoreState Of(World world)
    {
        return new LoreState([.. world.TerrainTypes], [.. world.Regions],
            [.. world.WeatherPins], [.. world.Rivers], [.. world.Lakes], [.. world.Journal],
            [.. world.Timelines], [.. world.Events], [.. world.Relationships],
            [.. world.Diagrams], [.. world.Nebulas], world.StarSeed, [.. world.Constellations],
            world.TerrainShapesGround, world.Style);
    }

    /// <summary>True if the world's lore is exactly this.</summary>
    public bool Matches(World world)
    {
        return TerrainTypes.SequenceEqual(world.TerrainTypes)
            && Regions.SequenceEqual(world.Regions)
            && WeatherPins.SequenceEqual(world.WeatherPins)
            && Rivers.SequenceEqual(world.Rivers)
            && Lakes.SequenceEqual(world.Lakes)
            && Journal.SequenceEqual(world.Journal)
            && Timelines.SequenceEqual(world.Timelines)
            && Events.SequenceEqual(world.Events)
            && Relationships.SequenceEqual(world.Relationships)
            && Diagrams.SequenceEqual(world.Diagrams)
            && Nebulas.SequenceEqual(world.Nebulas)
            && StarSeed == world.StarSeed
            && Constellations.SequenceEqual(world.Constellations)
            && TerrainShapesGround == world.TerrainShapesGround
            && Style == world.Style;
    }

    /// <summary>Puts this lore back into the world, replacing what's there.</summary>
    public void RestoreTo(World world)
    {
        world.TerrainTypes.Clear();
        world.TerrainTypes.AddRange(TerrainTypes);
        world.Regions.Clear();
        world.Regions.AddRange(Regions);
        world.WeatherPins.Clear();
        world.WeatherPins.AddRange(WeatherPins);
        world.Rivers.Clear();
        world.Rivers.AddRange(Rivers);
        world.Lakes.Clear();
        world.Lakes.AddRange(Lakes);
        world.Journal.Clear();
        world.Journal.AddRange(Journal);
        world.Timelines.Clear();
        world.Timelines.AddRange(Timelines);
        world.Events.Clear();
        world.Events.AddRange(Events);
        world.Relationships.Clear();
        world.Relationships.AddRange(Relationships);
        world.Diagrams.Clear();
        world.Diagrams.AddRange(Diagrams);
        world.Nebulas.Clear();
        world.Nebulas.AddRange(Nebulas);
        world.StarSeed = StarSeed;
        world.Constellations.Clear();
        world.Constellations.AddRange(Constellations);
        world.TerrainShapesGround = TerrainShapesGround;
        world.Style = Style;
    }
}
