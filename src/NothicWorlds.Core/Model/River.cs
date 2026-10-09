using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// A river on a planet or moon (VISION.md BOD-11; owner's choice: a line, drawn or natural).
/// A drawn river's <see cref="Points"/> are its course, source first; a natural river's only
/// point is its source, and its course is worked out downhill from the ground
/// (<see cref="Simulation.RiverCourse"/>). It widens from a fifth of <see cref="WidthKm"/> at
/// its source to <see cref="WidthKm"/> at its mouth. Immutable; change a river by replacing
/// it.
/// </summary>
public sealed record River
{
    /// <summary>The longest a name can be, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The most points a drawn river can have.</summary>
    public const int MaxPoints = 2000;

    /// <summary>The narrowest and widest a river can be at its mouth, in km.</summary>
    public const double MinWidthKm = 0.01, MaxWidthKm = 100;

    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The planet or moon it's on.</summary>
    public required Guid BodyId { get; init; }

    /// <summary>The river's name.</summary>
    public required string Name { get; init; }

    /// <summary>How it finds its course.</summary>
    public required RiverKind Kind { get; init; }

    /// <summary>
    /// A drawn river's course (2 or more points, source first), or a natural river's source
    /// (exactly 1 point).
    /// </summary>
    public required IReadOnlyList<GeoCoordinate> Points { get; init; }

    /// <summary>How wide it is at its mouth, in km.</summary>
    public double WidthKm { get; init; } = 1;

    /// <summary>How deep it is, and how its bed rises and falls.</summary>
    public RiverDepth Depth { get; init; } = RiverDepth.Auto;

    /// <summary>What's wrong with this river on its own, or null if nothing.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            return $"a river needs a name of 1 to {MaxNameLength} characters";
        }

        if (!Enum.IsDefined(Kind))
        {
            return "a river must be drawn or natural";
        }

        if (Points is null
            || (Kind == RiverKind.Drawn ? Points.Count is < 2 or > MaxPoints : Points.Count != 1))
        {
            return Kind == RiverKind.Drawn
                ? $"a drawn river needs 2 to {MaxPoints:N0} points"
                : "a natural river needs exactly one source";
        }

        if (!(double.IsFinite(WidthKm) && WidthKm is >= MinWidthKm and <= MaxWidthKm))
        {
            return $"a river is {MinWidthKm} to {MaxWidthKm} km wide";
        }

        return Depth is null ? "a river needs a depth" : Depth.Problem();
    }

    /// <inheritdoc />
    public bool Equals(River? other) =>
        other is not null && Id == other.Id && BodyId == other.BodyId && Name == other.Name
        && Kind == other.Kind && WidthKm == other.WidthKm && Depth == other.Depth
        && Points.SequenceEqual(other.Points);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Name, Kind, WidthKm, Points.Count);
}
