namespace NothicWorlds.Core.Maps;

/// <summary>
/// The result of checking a map image against <see cref="MapImageRules"/>: what size it should
/// be used at. Whether its shape suits a map type is checked separately, with
/// <see cref="MapProjections.ShapeMatches"/>, because the map type can change after import.
/// </summary>
/// <param name="OriginalWidth">The image's width in pixels.</param>
/// <param name="OriginalHeight">The image's height in pixels.</param>
/// <param name="TargetWidth">
/// Width to use: the original, or smaller if it exceeds the limit.
/// </param>
/// <param name="TargetHeight">
/// Height to use: the original, or smaller if it exceeds the limit.
/// </param>
public sealed record MapImageCheck(
    int OriginalWidth,
    int OriginalHeight,
    int TargetWidth,
    int TargetHeight)
{
    /// <summary>True if the image must be shrunk to fit the size limit.</summary>
    public bool NeedsResize => TargetWidth != OriginalWidth || TargetHeight != OriginalHeight;

    /// <summary>Width divided by height of the image as used.</summary>
    public double AspectRatio => (double)TargetWidth / TargetHeight;
}
