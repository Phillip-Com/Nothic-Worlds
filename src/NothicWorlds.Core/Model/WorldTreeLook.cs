namespace NothicWorlds.Core.Model;

/// <summary>
/// How a world tree grows and looks (VISION.md BOD-02; owner's choice: grown from settings, and
/// glowing). Its shape comes from these settings and its seed (see
/// <see cref="Simulation.WorldTreeShape"/>); its size is the body's radius (half its height).
/// Immutable.
/// </summary>
/// <param name="Branches">How many great branches it has, each able to hold a realm.</param>
/// <param name="Spread">How far its branches reach, as a share of its size (0.3 to 1.5).</param>
/// <param name="Seed">Shapes the tree: the same seed always grows the same tree.</param>
/// <param name="Bark">Its trunk and branches' color.</param>
/// <param name="Leaves">Its foliage's color.</param>
/// <param name="Glow">The color of the light it gives off.</param>
/// <param name="GlowStrength">How brightly it glows, 0 (dark) to 4.</param>
public sealed record WorldTreeLook(
    int Branches,
    double Spread,
    int Seed,
    RgbColor Bark,
    RgbColor Leaves,
    RgbColor Glow,
    double GlowStrength)
{
    /// <summary>The fewest great branches a tree can have.</summary>
    public const int MinBranches = 3;

    /// <summary>The most great branches a tree can have.</summary>
    public const int MaxBranches = 16;

    /// <summary>The least its branches can spread.</summary>
    public const double MinSpread = 0.3;

    /// <summary>The most its branches can spread.</summary>
    public const double MaxSpread = 1.5;

    /// <summary>The brightest a tree can glow.</summary>
    public const double MaxGlowStrength = 4;

    /// <summary>The look a new tree starts with: nine branches, green, glowing gold.</summary>
    public static WorldTreeLook Default { get; } = new(9, 0.9, 1, new RgbColor(0x5A, 0x42, 0x30),
        new RgbColor(0x4F, 0x8A, 0x4A), new RgbColor(0xFF, 0xD9, 0x8A), 1);

    /// <summary>What's wrong with the look, or null if it's usable.</summary>
    public string? Problem()
    {
        if (Branches is < MinBranches or > MaxBranches)
        {
            return $"a world tree has {MinBranches} to {MaxBranches} great branches";
        }

        if (!double.IsFinite(Spread) || Spread is < MinSpread or > MaxSpread)
        {
            return $"a world tree's spread must be {MinSpread} to {MaxSpread}";
        }

        if (Seed < 0)
        {
            return "a world tree's shape number can't be negative";
        }

        return double.IsFinite(GlowStrength) && GlowStrength is >= 0 and <= MaxGlowStrength
            ? null
            : $"a world tree's glow must be 0 to {MaxGlowStrength}";
    }
}
