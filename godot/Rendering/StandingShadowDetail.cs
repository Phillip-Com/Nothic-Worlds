namespace NothicWorlds.Rendering;

/// <summary>
/// How far the sun's shadows reach around the eye while standing, and how sharp (File ▸
/// Settings, Standing shadows; VISION.md REN-06, owner's choice). High is the Advanced tier:
/// for a dedicated graphics card.
/// </summary>
public enum StandingShadowDetail
{
    /// <summary>No shadows.</summary>
    Off,

    /// <summary>Shadows close by only: the lightest.</summary>
    Low,

    /// <summary>The default (Base tier).</summary>
    Standard,

    /// <summary>Shadows farther out, and sharper (Advanced tier).</summary>
    High,
}
