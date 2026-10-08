using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// A lake on a planet or moon (VISION.md BOD-11; owner's choice: click and fill): standing
/// water at its own surface height, separate from the world's sea level, filling the low
/// ground joined to its <see cref="Spot"/> up to <see cref="LevelMeters"/>
/// (<see cref="Simulation.LakeFill"/>). With <see cref="FlowsOut"/>, a natural river leaves it
/// from the lowest point of its shore. Immutable; change a lake by replacing it.
/// </summary>
public sealed record Lake
{
    /// <summary>The longest a name can be, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The lowest and highest a lake's surface can be, in meters.</summary>
    public const int MinLevelMeters = -12_000, MaxLevelMeters = 12_000;

    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The planet or moon it's on.</summary>
    public required Guid BodyId { get; init; }

    /// <summary>The lake's name.</summary>
    public required string Name { get; init; }

    /// <summary>A spot in the lake: the water fills outward from here.</summary>
    public required GeoCoordinate Spot { get; init; }

    /// <summary>The height of the lake's surface, in meters above the body's radius.</summary>
    public required int LevelMeters { get; init; }

    /// <summary>Whether a river flows out of it, from the lowest point of its shore.</summary>
    public bool FlowsOut { get; init; }

    /// <summary>What's wrong with this lake on its own, or null if nothing.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            return $"a lake needs a name of 1 to {MaxNameLength} characters";
        }

        return LevelMeters is >= MinLevelMeters and <= MaxLevelMeters
            ? null
            : $"a lake's surface is {MinLevelMeters:N0} to {MaxLevelMeters:N0} m";
    }
}
