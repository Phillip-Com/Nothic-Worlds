using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Maps;
using NothicWorlds.Session;

namespace NothicWorlds.Rendering;

/// <summary>
/// Puts the graphics options into effect (VISION.md REN-03): smoothing edges, the 3D view's
/// resolution, and the frame-rate cap on the window; the relief and cloud detail on the globes
/// and the weather; the ground detail while standing. On start it takes the options remembered
/// on this computer, or, for any never chosen, the preset for its graphics (owner's choice:
/// Standard on integrated graphics, High on a dedicated card). That default isn't written
/// down, so nothing is saved until the user changes something.
/// </summary>
public partial class GraphicsSettings : Node
{
    /// <summary>The system view, for how finely sculpted globes are drawn.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The live weather, for how finely clouds are drawn.</summary>
    [Export] public WeatherDisplay? LiveWeather { get; set; }

    /// <summary>The first-person view, for how finely the ground is drawn while standing.</summary>
    [Export] public FirstPersonMode? Standing { get; set; }

    /// <summary>The open world, whose maps load again when high-quality maps change.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>
    /// Whether imported maps are kept uncompressed (the Advanced tier, owner's choice; see
    /// <see cref="MapQuality"/>). Not part of any preset. Changing it loads the maps shown again
    /// and remembers it on this computer.
    /// </summary>
    public bool HighQualityMaps
    {
        get => MapQuality.Uncompressed;
        set
        {
            if (value == MapQuality.Uncompressed)
            {
                return;
            }

            MapQuality.Uncompressed = value;
            AppSettings.HighQualityMaps = value;
            if (Session is not null)
            {
                _ = Session.ReloadMapsAsync();
            }
        }
    }

    /// <summary>The options in effect.</summary>
    public GraphicsOptions Options { get; private set; } =
        GraphicsOptions.For(GraphicsQuality.Standard);

    /// <summary>The preset for this computer's graphics, used for anything not chosen.</summary>
    public static GraphicsQuality DefaultQuality =>
        GraphicsOptions.DefaultFor(RenderingServer.GetVideoAdapterType());

    // Before any node's _Ready, so it's set before the session can start loading a map.
    public override void _EnterTree() => MapQuality.Uncompressed = AppSettings.HighQualityMaps;

    public override void _Ready()
    {
        Options = AppSettings.LoadGraphics(GraphicsOptions.For(DefaultQuality));
        Apply();
    }

    /// <summary>Puts new options into effect and remembers them on this computer.</summary>
    public void Change(GraphicsOptions options)
    {
        if (options == Options)
        {
            return;
        }

        Options = options;
        Apply();
        AppSettings.SaveGraphics(options);
    }

    private void Apply()
    {
        Viewport viewport = GetViewport();
        viewport.Msaa3D = Options.AntiAliasing == AntiAliasing.Msaa4X
            ? Viewport.Msaa.Msaa4X
            : Viewport.Msaa.Disabled;
        viewport.ScreenSpaceAA = Options.AntiAliasing == AntiAliasing.Fxaa
            ? Viewport.ScreenSpaceAAEnum.Fxaa
            : Viewport.ScreenSpaceAAEnum.Disabled;
        viewport.Scaling3DScale = (int)Options.RenderScale / 100f;
        Engine.MaxFps = (int)Options.FrameRateLimit;  // 0: as often as the screen refreshes
        if (System is not null)
        {
            System.ReliefDetail = Options.ReliefDetail;
        }

        if (LiveWeather is not null)
        {
            LiveWeather.Detail = Options.CloudDetail;
        }

        if (Standing is not null)
        {
            Standing.StandingGroundDetail = Options.StandingGroundDetail;
        }
    }
}
