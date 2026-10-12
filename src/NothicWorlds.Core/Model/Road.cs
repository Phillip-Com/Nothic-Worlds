using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// A road on a planet or moon (VISION.md BOD-13; owner's choice: a line, drawn or routed,
/// like a river). Its <see cref="Points"/> are its course (drawn) or the places it passes
/// through (routed), first to last; a routed road's way between them is worked out from the
/// ground (<see cref="Simulation.RoadCourse"/>). Immutable; change a road by replacing it.
/// </summary>
public sealed record Road
{
    /// <summary>The longest a name can be, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The most points a road can have.</summary>
    public const int MaxPoints = 2000;

    /// <summary>The narrowest and widest a road can be, in meters.</summary>
    public const double MinWidthMeters = 1, MaxWidthMeters = 200;

    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The planet or moon it's on.</summary>
    public required Guid BodyId { get; init; }

    /// <summary>The road's name.</summary>
    public required string Name { get; init; }

    /// <summary>What it's like.</summary>
    public required RoadStyle Style { get; init; }

    /// <summary>How it finds its course.</summary>
    public required RoadKind Kind { get; init; }

    /// <summary>Its course, or the places it passes through: 2 or more points, in order.</summary>
    public required IReadOnlyList<GeoCoordinate> Points { get; init; }

    /// <summary>
    /// How wide it is, in meters: a paved or rutted road's own width, or the band dirt paths
    /// wander within.
    /// </summary>
    public double WidthMeters { get; init; } = 8;

    /// <summary>How wide a new road of a style is, in meters.</summary>
    public static double DefaultWidthMeters(RoadStyle style) => style switch
    {
        RoadStyle.DirtPaths => 30,
        RoadStyle.CartTrack => 4,
        RoadStyle.OldRoad => 6,
        _ => 8,
    };

    /// <summary>What's wrong with this road on its own, or null if nothing.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            return $"a road needs a name of 1 to {MaxNameLength} characters";
        }

        if (!Enum.IsDefined(Style) || !Enum.IsDefined(Kind))
        {
            return "a road's style or kind is unknown";
        }

        if (Points is null || Points.Count is < 2 or > MaxPoints)
        {
            return $"a road needs 2 to {MaxPoints:N0} points";
        }

        return double.IsFinite(WidthMeters)
            && WidthMeters is >= MinWidthMeters and <= MaxWidthMeters
            ? null
            : $"a road is {MinWidthMeters} to {MaxWidthMeters} m wide";
    }

    /// <inheritdoc />
    public bool Equals(Road? other) =>
        other is not null && Id == other.Id && BodyId == other.BodyId && Name == other.Name
        && Style == other.Style && Kind == other.Kind && WidthMeters == other.WidthMeters
        && Points.SequenceEqual(other.Points);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Name, Style, Kind, Points.Count);
}
