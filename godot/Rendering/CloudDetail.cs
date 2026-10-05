namespace NothicWorlds.Rendering;

/// <summary>
/// How finely live weather's clouds are drawn (VISION.md WTH-02, REN-03; owner's choice: a
/// quality setting): how many spots across the weather is worked out at, and whether fine
/// noise roughens the clouds' edges. The value is the snapshot's width in spots.
/// </summary>
public enum CloudDetail
{
    /// <summary>128 × 64 spots (about 3° apart) with soft edges: the lightest.</summary>
    Low = 128,

    /// <summary>256 × 128 spots (about 1.4° apart) with fine edges: the default.</summary>
    High = 256,
}
