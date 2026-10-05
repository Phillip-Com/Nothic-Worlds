namespace NothicWorlds.Rendering;

/// <summary>
/// The most frames a second drawn (VISION.md REN-03): fewer saves battery and heat on a laptop.
/// The value is the cap (0: the screen's own rate).
/// </summary>
public enum FrameRateLimit
{
    /// <summary>As often as the screen refreshes (VSync).</summary>
    MatchScreen = 0,

    /// <summary>60 frames a second, at most.</summary>
    Sixty = 60,

    /// <summary>30 frames a second, at most: the lightest on battery.</summary>
    Thirty = 30,
}
