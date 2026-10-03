using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Model;

/// <summary>
/// A named spot on a planet or moon whose weather is shown (VISION.md WTH-01; owner's choice:
/// weather pins are named and saved with the world). Its weather is worked out, not stored (see
/// <c>Simulation.ClimateYear</c>). Immutable; change a pin by replacing it.
/// </summary>
public sealed record WeatherPin
{
    /// <summary>The longest a name can be, in characters.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The most weather pins a world can hold.</summary>
    public const int MaxPins = 1000;

    /// <summary>Stable identity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The planet or moon it's on.</summary>
    public required Guid BodyId { get; init; }

    /// <summary>The pin's name, e.g. "Aster Bay".</summary>
    public required string Name { get; init; }

    /// <summary>The spot on the body's surface.</summary>
    public required GeoCoordinate Spot { get; init; }

    /// <summary>What's wrong with this pin on its own, or null if nothing.</summary>
    public string? Problem()
    {
        return string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength
            ? $"a weather pin needs a name of up to {MaxNameLength} characters"
            : null;
    }

    /// <summary>
    /// What's wrong with a world's weather pins together (each on a planet or moon that
    /// exists, unique IDs, the limit), or null if nothing.
    /// </summary>
    public static string? Problem(World world)
    {
        if (world.WeatherPins.Count > MaxPins)
        {
            return $"a world can hold up to {MaxPins:N0} weather pins";
        }

        if (world.WeatherPins.Select(p => p.Problem()).FirstOrDefault(p => p is not null)
            is string own)
        {
            return own;
        }

        if (world.WeatherPins.Select(p => p.Id).Distinct().Count() != world.WeatherPins.Count)
        {
            return "two weather pins share an ID";
        }

        var surfaces = world.Bodies.Where(b => b.HasSurface)
            .Select(b => b.Id).ToHashSet();
        return world.WeatherPins.Any(p => !surfaces.Contains(p.BodyId))
            ? "a weather pin is on a body that doesn't exist (or a star)"
            : null;
    }
}
