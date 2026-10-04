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

    /// <summary>How finely sculpted globes are drawn (View ▸ Relief Detail).</summary>
    public static ReliefDetail ReliefDetail
    {
        get
        {
            ConfigFile file = Load();
            string name = (string)file.GetValue(DisplaySection, ReliefDetailKey,
                nameof(ReliefDetail.Standard));
            return Enum.TryParse(name, out ReliefDetail detail) && Enum.IsDefined(detail)
                ? detail
                : ReliefDetail.Standard;
        }
        set => Save(ReliefDetailKey, value.ToString());
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

    private static void Save(string key, Variant value)
    {
        ConfigFile file = Load();
        file.SetValue(DisplaySection, key, value);
        Error error = file.Save(FilePath);
        if (error != Error.Ok)
        {
            // Not worth stopping for: the setting still applies until the app closes.
            GD.PushWarning($"Couldn't save the settings ({error}).");
        }
    }
}
