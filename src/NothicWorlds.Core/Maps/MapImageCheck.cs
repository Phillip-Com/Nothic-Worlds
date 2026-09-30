namespace NothicWorlds.Core.Maps;

/// <summary>
/// The result of checking a map image against <see cref="MapImageRules"/>: whether it's in the
/// supported layout, and what size it should be used at.
/// </summary>
/// <param name="OriginalWidth">The image's width in pixels.</param>
/// <param name="OriginalHeight">The image's height in pixels.</param>
/// <param name="TargetWidth">Width to use: the original, or smaller if it exceeds the limit.</param>
/// <param name="TargetHeight">Height to use: the original, or smaller if it exceeds the limit.</param>
/// <param name="IsSupportedLayout">True if the image is (close to) the supported 2:1 layout.</param>
public sealed record MapImageCheck(
    int OriginalWidth,
    int OriginalHeight,
    int TargetWidth,
    int TargetHeight,
    bool IsSupportedLayout)
{
    /// <summary>True if the image must be shrunk to fit the size limit.</summary>
    public bool NeedsResize => TargetWidth != OriginalWidth || TargetHeight != OriginalHeight;
}
