namespace NothicWorlds.Rendering;

/// <summary>
/// How many plants are drawn around the eye while standing, and how far (File ▸ Settings,
/// Standing plants; VISION.md REN-06, owner's choice). High is the Advanced tier: for a
/// dedicated graphics card.
/// </summary>
public enum StandingPlantDetail
{
    /// <summary>No plants.</summary>
    Off,

    /// <summary>Fewer plants, nearer: the lightest.</summary>
    Low,

    /// <summary>The default (Base tier).</summary>
    Standard,

    /// <summary>More plants, farther (Advanced tier).</summary>
    High,
}
