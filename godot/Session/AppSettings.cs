using Godot;
using NothicWorlds.Rendering;

namespace NothicWorlds.Session;

/// <summary>
/// Settings that belong to this computer rather than to any world (VISION.md REN-03), such as
/// how much detail to draw: kept in <c>user://settings.cfg</c> in Godot's per-user data folder,
/// and never saved in world files.
/// </summary>
/// <remarks>
/// A missing or damaged file just gives the defaults; settings never stop the app starting.
/// </remarks>
public static class AppSettings
{
    private const string FilePath = "user://settings.cfg";
    private const string DisplaySection = "display";
    private const string ReliefDetailKey = "relief_detail";
    private const string MapShadingKey = "map_style_shading";
    private const string CloudDetailKey = "cloud_detail";
    private const string AntiAliasingKey = "anti_aliasing";
    private const string RenderScaleKey = "render_scale";
    private const string FrameRateKey = "frame_rate_limit";

    /// <summary>
    /// The graphics options (File ▸ Settings; VISION.md REN-03). Any never saved (or unreadable)
    /// is taken from <paramref name="defaults"/>, the preset for this computer's graphics.
    /// </summary>
    public static GraphicsOptions LoadGraphics(GraphicsOptions defaults)
    {
        ConfigFile file = Load();
        return new GraphicsOptions(
            Read(file, ReliefDetailKey, defaults.ReliefDetail),
            Read(file, CloudDetailKey, defaults.CloudDetail),
            Read(file, AntiAliasingKey, defaults.AntiAliasing),
            Read(file, RenderScaleKey, defaults.RenderScale),
            Read(file, FrameRateKey, defaults.FrameRateLimit));
    }

    /// <summary>Remembers the graphics options.</summary>
    public static void SaveGraphics(GraphicsOptions options)
    {
        ConfigFile file = Load();
        file.SetValue(DisplaySection, ReliefDetailKey, options.ReliefDetail.ToString());
        file.SetValue(DisplaySection, CloudDetailKey, options.CloudDetail.ToString());
        file.SetValue(DisplaySection, AntiAliasingKey, options.AntiAliasing.ToString());
        file.SetValue(DisplaySection, RenderScaleKey, options.RenderScale.ToString());
        file.SetValue(DisplaySection, FrameRateKey, options.FrameRateLimit.ToString());
        Write(file);
    }

    /// <summary>Whether relief is shaded map-style (View ▸ Relief Shading).</summary>
    public static bool MapStyleShading
    {
        get => Load().GetValue(DisplaySection, MapShadingKey, false).AsBool();
        set => Save(MapShadingKey, value);
    }

    private static ConfigFile Load()
    {
        var file = new ConfigFile();
        file.Load(FilePath);  // A missing or unreadable file leaves it empty: the defaults.
        return file;
    }

    // An option stored by name, or the fallback if it's missing or not a known name.
    private static T Read<T>(ConfigFile file, string key, T fallback)
        where T : struct, Enum
    {
        string name = (string)file.GetValue(DisplaySection, key, fallback.ToString());
        return Enum.TryParse(name, out T value) && Enum.IsDefined(value) ? value : fallback;
    }

    private static void Save(string key, Variant value)
    {
        ConfigFile file = Load();
        file.SetValue(DisplaySection, key, value);
        Write(file);
    }

    private static void Write(ConfigFile file)
    {
        Error error = file.Save(FilePath);
        if (error != Error.Ok)
        {
            // Not worth stopping for: the setting still applies until the app closes.
            GD.PushWarning($"Couldn't save the settings ({error}).");
        }
    }
}
