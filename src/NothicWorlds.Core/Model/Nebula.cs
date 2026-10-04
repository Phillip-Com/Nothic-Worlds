namespace NothicWorlds.Core.Model;

/// <summary>
/// A nebula on the sky around the system (VISION.md BOD-03; owner's choice: a backdrop, the
/// same from every planet): a glowing cloud of gas far beyond the stars, seen in one direction.
/// Immutable.
/// </summary>
/// <param name="Id">Stable identity, kept across saves (it also shapes the cloud's wisps).</param>
/// <param name="Name">The nebula's name.</param>
/// <param name="LatitudeDegrees">
/// How far above (positive) or below the system's reference plane it is, −90 to 90.
/// </param>
/// <param name="LongitudeDegrees">Which way around it is, measured like orbit angles.</param>
/// <param name="SizeDegrees">How big it looks: its radius on the sky, 2° to 120°.</param>
/// <param name="Brightness">How strongly it glows, 0.05 to 1.</param>
/// <param name="Color">Its main color.</param>
/// <param name="SecondColor">The color its wisps blend toward.</param>
public sealed record Nebula(
    Guid Id,
    string Name,
    double LatitudeDegrees,
    double LongitudeDegrees,
    double SizeDegrees,
    double Brightness,
    RgbColor Color,
    RgbColor SecondColor)
{
    /// <summary>The most nebulas a world can have.</summary>
    public const int MaxCount = 20;

    /// <summary>The longest name a nebula can have.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The smallest a nebula can look, in degrees of radius.</summary>
    public const double MinSizeDegrees = 2;

    /// <summary>The biggest a nebula can look, in degrees of radius.</summary>
    public const double MaxSizeDegrees = 120;

    /// <summary>The faintest a nebula can glow.</summary>
    public const double MinBrightness = 0.05;

    /// <summary>What's wrong with the nebula, or null if it's usable.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            return $"a nebula needs a name of up to {MaxNameLength} characters";
        }

        if (!double.IsFinite(LatitudeDegrees) || LatitudeDegrees is < -90 or > 90
            || !double.IsFinite(LongitudeDegrees))
        {
            return "a nebula's direction must be up to 90° above or below, and any way around";
        }

        if (!double.IsFinite(SizeDegrees) || SizeDegrees is < MinSizeDegrees or > MaxSizeDegrees)
        {
            return $"a nebula's size must be {MinSizeDegrees}° to {MaxSizeDegrees}°";
        }

        return double.IsFinite(Brightness) && Brightness is >= MinBrightness and <= 1
            ? null
            : $"a nebula's brightness must be {MinBrightness:0%} to 100%";
    }

    /// <summary>
    /// What's wrong with a world's nebulas as a whole, or null if they're usable.
    /// </summary>
    public static string? Problem(IReadOnlyList<Nebula> nebulas)
    {
        if (nebulas.Count > MaxCount)
        {
            return $"a world can have up to {MaxCount} nebulas";
        }

        if (nebulas.Select(n => n.Id).Distinct().Count() != nebulas.Count)
        {
            return "two nebulas share an ID";
        }

        return nebulas.Select(n => n.Problem()).FirstOrDefault(p => p is not null);
    }
}
