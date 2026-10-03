namespace NothicWorlds.Core.Model;

/// <summary>A celestial body in a world, such as a planet.</summary>
public sealed class Body
{
    /// <summary>Stable identity, kept across saves.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The body's display name.</summary>
    public string Name { get; set; } = "Planet";

    /// <summary>What kind of body this is.</summary>
    public BodyKind Kind { get; set; } = BodyKind.Planet;

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

    /// <summary>What's drawn on the body's surface.</summary>
    public SurfaceSettings Surface { get; } = new();

    /// <summary>The body's radius in km (Earth's by default).</summary>
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

        return Orbit?.Problem() ?? Calendar?.Problem();
    }

    /// <summary>
    /// True if <paramref name="other"/> would save exactly the same body: the same name, kind,
    /// size, day, tilt, orbit, and surface. Used to tell whether the world still matches its
    /// saved file.
    /// </summary>
    public bool HasSameContent(Body other)
    {
        return Id == other.Id && Name == other.Name && Kind == other.Kind
            && RadiusKm == other.RadiusKm && DayLengthHours == other.DayLengthHours
            && AxialTiltDegrees == other.AxialTiltDegrees
            && AxialTiltDirectionDegrees == other.AxialTiltDirectionDegrees
            && AverageTemperatureC == other.AverageTemperatureC
            && Orbit == other.Orbit && Calendar == other.Calendar
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
            RadiusKm = RadiusKm,
            DayLengthHours = DayLengthHours,
            AxialTiltDegrees = AxialTiltDegrees,
            AxialTiltDirectionDegrees = AxialTiltDirectionDegrees,
            AverageTemperatureC = AverageTemperatureC,
            Orbit = Orbit,  // Immutable, safe to share.
            Calendar = Calendar,  // Immutable, safe to share.
        };
        copy.Surface.RestoreFrom(Surface);
        return copy;
    }
}
