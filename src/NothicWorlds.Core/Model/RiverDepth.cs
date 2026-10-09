namespace NothicWorlds.Core.Model;

/// <summary>
/// How deep a river is (VISION.md BOD-11; owner's choices, 2026-10-09): Auto (from its width)
/// or a set depth at its mouth, shallower toward its source as it narrows, with its bed rising
/// and falling along it by up to ±<see cref="VariationMeters"/>, the rises
/// <see cref="SpacingKm"/> apart, smooth or (with less <see cref="Smoothness"/>) in steps. Only
/// the bed changes: the water and its banks stay as they'd be.
/// </summary>
/// <param name="MouthMeters">
/// How deep its channel is at its mouth, from the ground to the bed, or null for Auto.
/// </param>
/// <param name="VariationMeters">How far its bed rises and falls along it (0: evenly).</param>
/// <param name="SpacingKm">How far apart the rises and falls are.</param>
/// <param name="Smoothness">
/// From 1 (worn smooth) to 0 (sharp steps, like weirs).
/// </param>
public sealed record RiverDepth(double? MouthMeters = null, double VariationMeters = 0,
    double SpacingKm = 1, double Smoothness = 1)
{
    /// <summary>The shallowest and deepest a set depth can be, in meters.</summary>
    public const double MinMouthMeters = 0.5, MaxMouthMeters = 500;

    /// <summary>The most a bed can rise and fall, in meters.</summary>
    public const double MaxVariationMeters = 200;

    /// <summary>The closest and farthest apart its rises and falls can be, in km.</summary>
    public const double MinSpacingKm = 0.5, MaxSpacingKm = 100;

    /// <summary>Today's look: Auto, with an even bed.</summary>
    public static RiverDepth Auto { get; } = new();

    /// <summary>What's wrong with these settings, or null if nothing.</summary>
    public string? Problem()
    {
        if (MouthMeters is double mouth
            && !(double.IsFinite(mouth) && mouth is >= MinMouthMeters and <= MaxMouthMeters))
        {
            return $"a river's depth is {MinMouthMeters} to {MaxMouthMeters} m";
        }

        if (!(double.IsFinite(VariationMeters)
            && VariationMeters is >= 0 and <= MaxVariationMeters))
        {
            return $"a river's depth varies by 0 to {MaxVariationMeters} m";
        }

        if (!(double.IsFinite(SpacingKm) && SpacingKm is >= MinSpacingKm and <= MaxSpacingKm))
        {
            return $"a river's depth changes every {MinSpacingKm} to {MaxSpacingKm} km";
        }

        return double.IsFinite(Smoothness) && Smoothness is >= 0 and <= 1
            ? null
            : "a river's smoothness is 0 to 1";
    }
}
