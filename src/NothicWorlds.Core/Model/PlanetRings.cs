namespace NothicWorlds.Core.Model;

/// <summary>
/// Rings around a planet or moon (VISION.md BOD-03; owner's choice: banded, in the owner's color,
/// with shadows): a flat band in the body's equatorial plane, measured in the body's radii so
/// they keep their shape if it's resized. Immutable.
/// </summary>
/// <param name="InnerRadii">Where the rings start, in the body's radii (above its surface).</param>
/// <param name="OuterRadii">Where they end, in the body's radii.</param>
/// <param name="Color">Their main color; the bands are lighter and darker shades of it.</param>
public sealed record PlanetRings(double InnerRadii, double OuterRadii, RgbColor Color)
{
    /// <summary>The nearest rings can start: just above the surface.</summary>
    public const double MinInnerRadii = 1.05;

    /// <summary>The farthest rings can reach.</summary>
    public const double MaxOuterRadii = 50;

    /// <summary>Rings a planet starts with when they're switched on: Saturn's main rings.</summary>
    public static PlanetRings Default { get; } = new(1.25, 2.3, new RgbColor(0xD8, 0xC8, 0xA8));

    /// <summary>What's wrong with the rings, or null if they're usable.</summary>
    public string? Problem()
    {
        if (!double.IsFinite(InnerRadii) || !double.IsFinite(OuterRadii)
            || InnerRadii < MinInnerRadii || OuterRadii > MaxOuterRadii
            || OuterRadii <= InnerRadii)
        {
            return $"rings must start at least {MinInnerRadii} radii out, end farther out, and " +
                $"reach at most {MaxOuterRadii} radii";
        }

        return null;
    }
}
