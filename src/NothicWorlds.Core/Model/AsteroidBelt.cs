namespace NothicWorlds.Core.Model;

/// <summary>
/// A belt of asteroids circling a star (VISION.md BOD-03; owner's choice: one feature per belt,
/// whose rocks are drawn rather than saved or edited one by one). The rocks lie between two
/// distances from the star, in orbits tilted up to <see cref="ThicknessDegrees"/> from its
/// reference plane. Immutable.
/// </summary>
/// <param name="Id">Stable identity, kept across saves.</param>
/// <param name="Name">The belt's name, e.g. "Main Belt".</param>
/// <param name="InnerKm">Where the belt starts, in km from the star.</param>
/// <param name="OuterKm">Where it ends, in km from the star.</param>
/// <param name="ThicknessDegrees">How far the rocks' orbits tilt, at most (0 is flat).</param>
/// <param name="Density">How crowded it is, from 0.01 (sparse) to 1 (densest).</param>
/// <param name="Color">The rocks' color.</param>
public sealed record AsteroidBelt(
    Guid Id,
    string Name,
    double InnerKm,
    double OuterKm,
    double ThicknessDegrees,
    double Density,
    RgbColor Color)
{
    /// <summary>The longest name a belt can have.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The most the rocks' orbits can tilt, in degrees.</summary>
    public const double MaxThicknessDegrees = 45;

    /// <summary>The sparsest a belt can be.</summary>
    public const double MinDensity = 0.01;

    /// <summary>What's wrong with the belt, or null if it's usable.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            return $"a belt needs a name of up to {MaxNameLength} characters";
        }

        if (!double.IsFinite(InnerKm) || !double.IsFinite(OuterKm) || InnerKm <= 0
            || OuterKm <= InnerKm || OuterKm > Orbit.MaxDistanceKm)
        {
            return $"a belt must start above 0 km, end farther out, and reach at most " +
                $"{Orbit.MaxDistanceKm:g} km";
        }

        if (!double.IsFinite(ThicknessDegrees) || ThicknessDegrees is < 0 or > MaxThicknessDegrees)
        {
            return $"a belt's thickness must be 0° to {MaxThicknessDegrees}°";
        }

        return double.IsFinite(Density) && Density is >= MinDensity and <= 1
            ? null
            : $"a belt's density must be {MinDensity:0%} to 100%";
    }
}
