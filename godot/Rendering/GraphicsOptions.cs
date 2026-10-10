using Godot;

namespace NothicWorlds.Rendering;

/// <summary>
/// Every graphics option together (VISION.md REN-03), and the presets that set them all at once
/// (owner's choice). Settings for this computer, not for a world.
/// </summary>
/// <param name="ReliefDetail">How finely sculpted globes' shapes are drawn.</param>
/// <param name="CloudDetail">How finely live weather's clouds are drawn.</param>
/// <param name="StandingGroundDetail">How finely the ground is drawn while standing.</param>
/// <param name="StandingPlants">How many plants are drawn while standing, and how far.</param>
/// <param name="AntiAliasing">How edges are smoothed.</param>
/// <param name="RenderScale">The 3D view's resolution.</param>
/// <param name="FrameRateLimit">The most frames a second.</param>
public sealed record GraphicsOptions(
    ReliefDetail ReliefDetail,
    CloudDetail CloudDetail,
    StandingGroundDetail StandingGroundDetail,
    StandingPlantDetail StandingPlants,
    AntiAliasing AntiAliasing,
    RenderScale RenderScale,
    FrameRateLimit FrameRateLimit)
{
    /// <summary>The options a preset sets (Custom has none of its own: Standard's).</summary>
    public static GraphicsOptions For(GraphicsQuality quality) => quality switch
    {
        GraphicsQuality.Low => new(ReliefDetail.Low, CloudDetail.Low, StandingGroundDetail.Low,
            StandingPlantDetail.Low, AntiAliasing.Off, RenderScale.ThreeQuarters,
            FrameRateLimit.Thirty),
        GraphicsQuality.High => new(ReliefDetail.High, CloudDetail.High, StandingGroundDetail.High,
            StandingPlantDetail.High, AntiAliasing.Msaa4X, RenderScale.Full,
            FrameRateLimit.MatchScreen),
        _ => new(ReliefDetail.Standard, CloudDetail.High, StandingGroundDetail.Standard,
            StandingPlantDetail.Standard, AntiAliasing.Fxaa, RenderScale.Full,
            FrameRateLimit.Sixty),
    };

    /// <summary>
    /// The preset to start on before any is chosen (owner's choice, after measuring the baseline
    /// laptop): Standard on integrated graphics (and anything unknown), High on a dedicated card.
    /// </summary>
    public static GraphicsQuality DefaultFor(RenderingDevice.DeviceType adapter) =>
        adapter == RenderingDevice.DeviceType.DiscreteGpu
            ? GraphicsQuality.High
            : GraphicsQuality.Standard;

    /// <summary>The preset these options match, or Custom.</summary>
    public GraphicsQuality Quality =>
        new[] { GraphicsQuality.Low, GraphicsQuality.Standard, GraphicsQuality.High }
            .Cast<GraphicsQuality?>()
            .FirstOrDefault(quality => For(quality!.Value) == this) ?? GraphicsQuality.Custom;
}
