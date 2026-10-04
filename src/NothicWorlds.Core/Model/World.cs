namespace NothicWorlds.Core.Model;

/// <summary>
/// A user's world: everything that gets saved to a <c>.nworld</c> file (see
/// docs/world-format.md). This is the in-memory form; <c>Storage.WorldPackage</c> reads and
/// writes it.
/// </summary>
public sealed class World
{
    /// <summary>
    /// Stable identity, kept across saves (e.g. for recovery copies, or future sharing).
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The world's display name.</summary>
    public string Name { get; set; } = "Untitled World";

    /// <summary>When the world was first created.</summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When the world was last saved.</summary>
    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The world's celestial bodies: suns, planets, and moons (VISION.md BOD-01). Bodies with an
    /// orbit circle their parent; see <c>Simulation.SystemHierarchy</c> for the rules.
    /// </summary>
    public List<Body> Bodies { get; } = [];

    /// <summary>
    /// The world clock (VISION.md SIM-02), in standard days since the world's time 0. Every
    /// body's position and spin follow from it.
    /// </summary>
    public double TimeDays { get; set; }

    /// <summary>
    /// The world's journal entries (VISION.md LORE-02), in the order they were added.
    /// </summary>
    public List<JournalEntry> Journal { get; } = [];

    /// <summary>
    /// The areas outlined on the world's planets and moons (VISION.md LORE-01), in the order
    /// they were drawn (later ones draw on top).
    /// </summary>
    public List<Region> Regions { get; } = [];

    /// <summary>
    /// The named spots whose weather is shown (VISION.md WTH-01), in the order they were added.
    /// </summary>
    public List<WeatherPin> WeatherPins { get; } = [];

    /// <summary>
    /// The kinds of terrain that can be painted onto the world's bodies (VISION.md BOD-05), in
    /// the order they're listed. New worlds start with <see cref="TerrainType.Defaults"/>.
    /// </summary>
    public List<TerrainType> TerrainTypes { get; } = [];

    /// <summary>The world's named timelines (VISION.md LORE-03), in lane order.</summary>
    public List<Timeline> Timelines { get; } = [];

    /// <summary>
    /// The events on the timelines (VISION.md LORE-03). See <see cref="LoreRules"/> for how
    /// they tie to timelines, entries, and bodies.
    /// </summary>
    public List<TimelineEvent> Events { get; } = [];

    /// <summary>The nebulas on the sky around the system (VISION.md BOD-03).</summary>
    public List<Nebula> Nebulas { get; } = [];

    /// <summary>
    /// Where the camera was looking when the world was saved, or null for the default.
    /// </summary>
    public CameraView? View { get; set; }

    /// <summary>
    /// Returns an independent copy of this world. Saving works on a copy, so it can run in the
    /// background while the user keeps editing.
    /// </summary>
    public World Clone()
    {
        var copy = new World
        {
            Id = Id,
            Name = Name,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            View = View,  // Immutable record, safe to share.
            TimeDays = TimeDays,
        };
        copy.Bodies.AddRange(Bodies.Select(body => body.Clone()));

        // Terrain types, regions, weather pins, entries, timelines, and events are immutable
        // records, safe to share.
        copy.TerrainTypes.AddRange(TerrainTypes);
        copy.Regions.AddRange(Regions);
        copy.WeatherPins.AddRange(WeatherPins);
        copy.Journal.AddRange(Journal);
        copy.Timelines.AddRange(Timelines);
        copy.Events.AddRange(Events);
        return copy;
    }

    /// <summary>
    /// Creates a new world: a Sun-like star with one unmapped, Earth-like planet circling it once
    /// a year (VISION.md BOD-01), and the default terrain types (BOD-05). The planet comes first,
    /// as the body most users start with.
    /// </summary>
    public static World CreateNew(string name = "Untitled World")
    {
        var world = new World { Name = name };
        world.TerrainTypes.AddRange(TerrainType.Defaults);
        var sun = new Body
        {
            Name = "Sun",
            Kind = BodyKind.Star,
            RadiusKm = 696_000,
            DayLengthHours = 609.6,
        };
        world.Bodies.Add(new Body
        {
            Name = "Planet",
            Kind = BodyKind.Planet,
            AxialTiltDegrees = 23.4,
            Orbit = new Orbit
            {
                ParentId = sun.Id,
                DistanceKm = 149_600_000,
                PeriodDays = 365.25,
            },
        });
        world.Bodies.Add(sun);
        return world;
    }
}
