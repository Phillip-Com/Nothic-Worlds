namespace NothicWorlds.Core.Maps;

/// <summary>
/// Rules for map images wrapped onto a planet (VISION.md MAP-01).
/// <list type="bullet">
/// <item>The supported layout is equirectangular: a 2:1 image where latitude and longitude form
/// a straight grid. Other shapes are still allowed, but they look stretched on the globe.</item>
/// <item>Images larger than <see cref="MaxWidth"/> × <see cref="MaxHeight"/> are shrunk,
/// keeping their proportions, to limit graphics memory use.</item>
/// </list>
/// </summary>
public static class MapImageRules
{
    /// <summary>Largest width used on the globe (owner decision: 8192 × 4096).</summary>
    public const int MaxWidth = 8192;

    /// <summary>Largest height used on the globe.</summary>
    public const int MaxHeight = 4096;

    /// <summary>Width divided by height for the supported equirectangular layout.</summary>
    public const double SupportedAspectRatio = 2.0;

    /// <summary>How far (as a fraction) an image's shape may be from 2:1 and still count as
    /// supported. Allows for small differences like 2048 × 1025.</summary>
    public const double AspectRatioTolerance = 0.01;

    /// <summary>Checks an image of the given size.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is less than 1.</exception>
    public static MapImageCheck Check(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        double aspectRatio = (double)width / height;
        bool isSupportedLayout =
            Math.Abs(aspectRatio / SupportedAspectRatio - 1.0) <= AspectRatioTolerance;

        double scale = Math.Min(1.0, Math.Min((double)MaxWidth / width, (double)MaxHeight / height));
        int targetWidth = Math.Max(1, (int)Math.Round(width * scale));
        int targetHeight = Math.Max(1, (int)Math.Round(height * scale));

        return new MapImageCheck(width, height, targetWidth, targetHeight, isSupportedLayout);
    }
}
