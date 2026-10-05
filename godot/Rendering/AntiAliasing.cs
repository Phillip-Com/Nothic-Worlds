namespace NothicWorlds.Rendering;

/// <summary>
/// How the jagged edges of globes, rings, and lines are smoothed (VISION.md REN-03).
/// </summary>
public enum AntiAliasing
{
    /// <summary>Not at all: the lightest.</summary>
    Off,

    /// <summary>
    /// A quick blur along edges after drawing (FXAA): nearly free, a little soft.
    /// </summary>
    Fxaa,

    /// <summary>
    /// Four samples a pixel along edges (4× MSAA): the cleanest, at more video memory.
    /// </summary>
    Msaa4X,
}
