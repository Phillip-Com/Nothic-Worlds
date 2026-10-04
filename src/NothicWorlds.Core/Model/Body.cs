namespace NothicWorlds.Core.Model;

/// <summary>A celestial body in a world, such as a planet.</summary>
public sealed class Body
{
    /// <summary>Stable identity, kept across saves.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// True for planets and moons, which have a surface for maps, terrain, regions, and pins;
    /// false for stars and comets.
    /// </summary>
    public bool HasSurface => Kind is BodyKind.Planet or BodyKind.Moon;

    /// <summary>The body's display name.</summary>
    public string Name { get; set; } = "Planet";

    /// <summary>What kind of body this is.</summary>
    public BodyKind Kind { get; set; } = BodyKind.Planet;

    /// <summary>
    /// The body's shape (VISION.md BOD-02): a globe, or for a planet or moon, a flat world.
    /// </summary>
    public BodyShape Shape { get; set; } = BodyShape.Sphere;

    /// <summary>The largest allowed radius, in km (far beyond the biggest known stars).</summary>
    public const double MaxRadiusKm = 1e10;

    /// <summary>The longest allowed day, in hours.</summary>
    public const double MaxDayLengthHours = 1e7;

    /// <summary>
    /// The coldest average temperature a body can have, in °C (just above absolute zero).
    /// </summary>
    public const double MinAverageTemperatureC = -270;

    /// <summary>The hottest average temperature a body can have, in °C.</summary>
    public const double MaxAverageTemperatureC = 2000;

    /// <summary>
    /// How the body looks (VISION.md BOD-06): its color and pattern, or a star's type. Defaults
    /// to a planet's look; <see cref="BodyAppearance.DefaultFor"/> gives each kind's.
    /// </summary>
    public BodyAppearance Appearance { get; set; } = BodyAppearance.DefaultFor(BodyKind.Planet);

    /// <summary>
    /// The body's rings (VISION.md BOD-03), or null for none. Planets and moons only.
    /// </summary>
    public PlanetRings? Rings { get; set; }

    /// <summary>
    /// For a realm (a planet or moon hung on a world tree, VISION.md BOD-02): which of its
    /// tree's great branches it hangs on, counting from 0; null for a body that orbits freely.
    /// Its orbit is then set by the branch (see <c>Simulation.Realms</c>).
    /// </summary>
    public int? Branch { get; set; }

    /// <summary>
    /// How a world tree grows and looks (VISION.md BOD-02); null for every other kind.
    /// </summary>
    public WorldTreeLook? Tree { get; set; }

    /// <summary>
    /// The asteroid belts circling the body (VISION.md BOD-03). Stars only; empty for others.
    /// </summary>
    public IReadOnlyList<AsteroidBelt> Belts { get; set; } = [];

    /// <summary>What's drawn on the body's surface.</summary>
    public SurfaceSettings Surface { get; } = new();

    /// <summary>
    /// The body's radius in km (Earth's by default). For a flat world it's the radius of the
    /// matching globe; the disc itself is π times as wide (see <see cref="Geometry.FlatDisc"/>).
    /// </summary>
    public double RadiusKm { get; set; } = 6371.0;

    /// <summary>
    /// How long the body takes to spin once, in standard hours (VISION.md SIM-02): the length
    /// of its day. 24 by default.
    /// </summary>
    public double DayLengthHours { get; set; } = 24.0;

    /// <summary>
    /// How far the body's spin axis leans, in degrees (0 = upright, 90 = on its side, over 90 =
    /// spinning backwards). Drives seasons later (CAL-03).
    /// </summary>
    public double AxialTiltDegrees { get; set; }

    /// <summary>
    /// Which way the spin axis leans, in degrees: the direction the north pole tips toward,
    /// measured like orbit angles (0 is the reference direction, counting counterclockwise seen
    /// from the north). Together with the orbit, it decides when in the year each season falls
    /// (VISION.md CAL-03).
    /// </summary>
    public double AxialTiltDirectionDegrees { get; set; }

    /// <summary>
    /// The body's average surface temperature over a year, in °C (VISION.md WTH-01; owner's
    /// choice: set by the user, Earth is about 15). Weather pins spread it by latitude and season.
    /// </summary>
    public double AverageTemperatureC { get; set; } = 15.0;

    /// <summary>The body's own calendar (VISION.md CAL-01), or null to count plain days.</summary>
    public Calendar? Calendar { get; set; }

    /// <summary>
    /// The path this body follows around its parent (VISION.md SIM-01), or null if it sits
    /// still at the system's center (usually the main sun).
    /// </summary>
    public Orbit? Orbit { get; set; }

    /// <summary>
    /// What's wrong with the body's size, day, tilt, or orbit, or null if they're usable.
    /// </summary>
    public string? Problem()
    {
        if (!double.IsFinite(RadiusKm) || RadiusKm <= 0 || RadiusKm > MaxRadiusKm)
        {
            return $"a body's radius must be above 0 and at most {MaxRadiusKm:g} km";
        }

        if (!double.IsFinite(DayLengthHours) || DayLengthHours <= 0
            || DayLengthHours > MaxDayLengthHours)
        {
            return $"a body's day must be above 0 and at most {MaxDayLengthHours:g} hours";
        }

        if (!double.IsFinite(AxialTiltDegrees) || AxialTiltDegrees is < 0 or > 180
            || !double.IsFinite(AxialTiltDirectionDegrees))
        {
            return "a body's axial tilt must be 0° to 180°, with a direction";
        }

        if (!double.IsFinite(AverageTemperatureC)
            || AverageTemperatureC is < MinAverageTemperatureC or > MaxAverageTemperatureC)
        {
            return $"a body's average temperature must be {MinAverageTemperatureC} °C to " +
                $"{MaxAverageTemperatureC} °C";
        }

        if (!HasSurface && Shape != BodyShape.Sphere)
        {
            return "only planets and moons can be flat";
        }

        if (Rings is PlanetRings rings && (!HasSurface || rings.Problem() is not null))
        {
            return HasSurface ? rings.Problem() : "only planets and moons can have rings";
        }

        if ((Kind == BodyKind.WorldTree) != (Tree is not null))
        {
            return Tree is null ? "a world tree needs its look" : "only world trees grow branches";
        }

        if (Tree?.Problem() is string treeProblem)
        {
            return treeProblem;
        }

        if (Kind == BodyKind.WorldTree && Calendar is not null)
        {
            return "a world tree can't have a calendar";
        }

        if (Belts.Count > 0 && Kind != BodyKind.Star)
        {
            return "only stars can have asteroid belts";
        }

        if (Belts.Select(belt => belt.Problem()).FirstOrDefault(p => p is not null)
            is string beltProblem)
        {
            return beltProblem;
        }

        if (Belts.Select(belt => belt.Id).Distinct().Count() != Belts.Count)
        {
            return "two asteroid belts share an ID";
        }

        if (Kind == BodyKind.Comet && Calendar is not null)
        {
            return "a comet can't have a calendar";
        }

        return Orbit?.Problem() ?? Calendar?.Problem();
    }

    /// <summary>
    /// True if <paramref name="other"/> would save exactly the same body: the same name, kind,
    /// shape, size, day, tilt, orbit, branch, appearance, rings, belts, tree, and surface. Used
    /// to tell whether the world still matches its saved file.
    /// </summary>
    public bool HasSameContent(Body other)
    {
        return Id == other.Id && Name == other.Name && Kind == other.Kind
            && Shape == other.Shape
            && RadiusKm == other.RadiusKm && DayLengthHours == other.DayLengthHours
            && AxialTiltDegrees == other.AxialTiltDegrees
            && AxialTiltDirectionDegrees == other.AxialTiltDirectionDegrees
            && AverageTemperatureC == other.AverageTemperatureC
            && Orbit == other.Orbit && Calendar == other.Calendar
            && Appearance == other.Appearance && Rings == other.Rings
            && Belts.SequenceEqual(other.Belts) && Tree == other.Tree && Branch == other.Branch
            && Surface.HasSameContent(other.Surface);
    }

    /// <summary>Returns an independent copy of this body.</summary>
    public Body Clone()
    {
        var copy = new Body
        {
            Id = Id,
            Name = Name,
            Kind = Kind,
            Shape = Shape,
            RadiusKm = RadiusKm,
            DayLengthHours = DayLengthHours,
            AxialTiltDegrees = AxialTiltDegrees,
            AxialTiltDirectionDegrees = AxialTiltDirectionDegrees,
            AverageTemperatureC = AverageTemperatureC,
            Orbit = Orbit,  // Immutable, safe to share.
            Calendar = Calendar,  // Immutable, safe to share.
            Appearance = Appearance,  // Immutable, safe to share.
            Rings = Rings,  // Immutable, safe to share.
            Belts = [.. Belts],  // The belts are immutable; the list is copied.
            Tree = Tree,  // Immutable, safe to share.
            Branch = Branch,
        };
        copy.Surface.RestoreFrom(Surface);
        return copy;
    }
}
