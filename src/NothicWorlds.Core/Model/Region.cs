using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// A named area outlined on a planet or moon (VISION.md LORE-01): a country, a forest, a sea.
/// Its outline is corner points joined by the shortest paths between them (see
/// <see cref="SphericalPolygon"/>). Regions can overlap. Journal entries and timeline events
/// can be placed in one (see <see cref="LoreLocation.RegionId"/>). Immutable; change a region
/// by replacing it.
/// </summary>
public sealed record Region
{
    /// <summary>The longest a name can be, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The longest a region's notes can be, in characters.</summary>
    public const int MaxNotesLength = 20_000;

    /// <summary>The most corner points an outline can have.</summary>
    public const int MaxCorners = 1000;

    /// <summary>Stable identity, used by places.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The planet or moon it's on.</summary>
    public required Guid BodyId { get; init; }

    /// <summary>The region's name.</summary>
    public required string Name { get; init; }

    /// <summary>Notes about the region (plain text).</summary>
    public string Notes { get; init; } = "";

    /// <summary>The color its outline and fill are drawn in.</summary>
    public RgbColor Color { get; init; } = new(0xE6, 0xC8, 0x78);

    /// <summary>The outline's corners, in order around it (3 or more).</summary>
    public required IReadOnlyList<GeoCoordinate> Corners { get; init; }

    /// <summary>What's wrong with this region on its own, or null if nothing.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            return $"a region needs a name of up to {MaxNameLength} characters";
        }

        if (Notes is null || Notes.Length > MaxNotesLength)
        {
            return $"a region's notes can be up to {MaxNotesLength:N0} characters";
        }

        if (Corners is null || Corners.Count is < 3 or > MaxCorners
            || Corners.Distinct().Count() < 3)
        {
            return $"a region's outline needs 3 to {MaxCorners} different points";
        }

        return SphericalPolygon.FitsInHemisphere(Corners)
            ? null
            : "a region can't cover more than about half the globe";
    }

    /// <summary>True if a spot on the body is inside the region.</summary>
    public bool Contains(GeoCoordinate spot) => SphericalPolygon.Contains(Corners, spot);

    /// <summary>Regions are equal when every part matches, including the outline.</summary>
    public bool Equals(Region? other)
    {
        return other is not null
            && Id == other.Id
            && BodyId == other.BodyId
            && Name == other.Name
            && Notes == other.Notes
            && Color == other.Color
            && Corners.SequenceEqual(other.Corners);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, BodyId, Name, Corners.Count);
}
