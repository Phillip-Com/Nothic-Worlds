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
    /// The rivers on the world's planets and moons (VISION.md BOD-11), in the order they were
    /// added.
    /// </summary>
    public List<River> Rivers { get; } = [];

    /// <summary>
    /// The lakes on the world's planets and moons (VISION.md BOD-11), in the order they were
    /// added.
    /// </summary>
    public List<Lake> Lakes { get; } = [];

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

    /// <summary>
    /// The ties between journal entries (VISION.md LORE-04), shown in every diagram that holds
    /// both ends.
    /// </summary>
    public List<Relationship> Relationships { get; } = [];

    /// <summary>The world's relationship diagrams (VISION.md LORE-04), in list order.</summary>
    public List<LoreDiagram> Diagrams { get; } = [];

    /// <summary>The nebulas on the sky around the system (VISION.md BOD-03).</summary>
    public List<Nebula> Nebulas { get; } = [];

    /// <summary>
    /// Where the night sky's stars come from (VISION.md REN-07; see
    /// <c>Simulation.StarField</c>): the same seed always gives the same stars.
    /// </summary>
    public int StarSeed { get; set; }

    /// <summary>
    /// The named star patterns on the night sky (VISION.md REN-07), in list order. Their lines
    /// join stars of the sky from <see cref="StarSeed"/>.
    /// </summary>
    public List<Constellation> Constellations { get; } = [];

    /// <summary>
    /// Whether painted terrain shapes the ground (VISION.md BOD-07): each type sets the ground
    /// to its height, with sculpting on top (see <see cref="TerrainRelief"/>). Off unless
    /// turned on.
    /// </summary>
    public bool TerrainShapesGround { get; set; }

    /// <summary>
    /// Where the camera was looking when the world was saved, or null for the default.
    /// </summary>
    public CameraView? View { get; set; }

    /// <summary>How the world is drawn (VISION.md REN-05).</summary>
    public VisualStyle Style { get; set; } = VisualStyle.Painterly;

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
            Style = Style,
            StarSeed = StarSeed,
            TerrainShapesGround = TerrainShapesGround,
        };
        copy.Bodies.AddRange(Bodies.Select(body => body.Clone()));

        // Terrain types, regions, weather pins, rivers, lakes, entries, timelines, events,
        // relationships, diagrams, nebulas, and constellations are immutable records, safe to
        // share.
        copy.TerrainTypes.AddRange(TerrainTypes);
        copy.Regions.AddRange(Regions);
        copy.WeatherPins.AddRange(WeatherPins);
        copy.Rivers.AddRange(Rivers);
        copy.Lakes.AddRange(Lakes);
        copy.Journal.AddRange(Journal);
        copy.Timelines.AddRange(Timelines);
        copy.Events.AddRange(Events);
        copy.Relationships.AddRange(Relationships);
        copy.Diagrams.AddRange(Diagrams);
        copy.Nebulas.AddRange(Nebulas);
        copy.Constellations.AddRange(Constellations);
        return copy;
    }

    /// <summary>
    /// Creates a new world: a Sun-like star with one unmapped, Earth-like planet circling it once
    /// a year (VISION.md BOD-01), and the default terrain types (BOD-05). The planet comes first,
    /// as the body most users start with.
    /// </summary>
    public static World CreateNew(string name = "Untitled World")
    {
        var world = new World { Name = name, StarSeed = Simulation.StarField.NewSeed() };
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
