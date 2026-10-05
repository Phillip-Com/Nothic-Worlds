namespace NothicWorlds.Rendering;

/// <summary>
/// The graphics quality presets (VISION.md REN-03; owner's choice: Low, Standard, and High, set
/// in File ▸ Settings), each setting every graphics option at once (see
/// <see cref="GraphicsOptions"/>); Custom when the options have been set one by one.
/// </summary>
public enum GraphicsQuality
{
    /// <summary>The lightest: for graphics weaker than the baseline laptop's.</summary>
    Low,

    /// <summary>The default on integrated graphics, like the baseline laptop's.</summary>
    Standard,

    /// <summary>The finest: the default on dedicated graphics cards.</summary>
    High,

    /// <summary>The options don't match any preset.</summary>
    Custom,
}
