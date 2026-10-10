using Godot;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Core.Storage;
using NothicWorlds.Rendering;

namespace NothicWorlds.Session;

/// <summary>
/// Settings that belong to this computer rather than to any world (VISION.md REN-03), such as
/// how much detail to draw and the worlds opened lately: kept in <c>user://settings.cfg</c> in
/// Godot's per-user data folder, and never saved in world files.
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
    private const string StandingGroundDetailKey = "ground_detail";
    private const string StandingPlantsKey = "standing_plants";
    private const string AntiAliasingKey = "anti_aliasing";
    private const string RenderScaleKey = "render_scale";
    private const string FrameRateKey = "frame_rate_limit";
    private const string HighQualityMapsKey = "high_quality_maps";
    private const string UnitsKey = "units";
    private const string CloudsKey = "show_clouds";
    private const string FogKey = "standing_fog";
    private const string NightVisionKey = "night_vision";
    private const string ShowPlantsKey = "show_plants";
    // Renamed with the ground materials (it used to default to on), so everyone starts on them.
    private const string StandingTerrainKey = "standing_painted_colors";
    private const string FilesSection = "files";
    private const string FoldsSection = "folds";
    private const string RecentWorldsKey = "recent_worlds";

    // Read once, then kept: it's asked for every time a measurement is shown.
    private static UnitSystem? _units;

    /// <summary>
    /// Raised when <see cref="Units"/> changes, so measurements shown can follow.
    /// </summary>
    public static event Action? UnitsChanged;

    /// <summary>
    /// The units measurements are shown and typed in (File ▸ Settings; VISION.md UI-04). Until
    /// chosen, it follows the computer's region (owner's choice).
    /// </summary>
    public static UnitSystem Units
    {
        get => _units ??= Read(Load(), UnitsKey,
            Core.Measurement.Units.DefaultFor(OS.GetLocale()));
        set
        {
            if (value == Units)
            {
                return;
            }

            _units = value;
            Save(UnitsKey, value.ToString());
            UnitsChanged?.Invoke();
        }
    }

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
            Read(file, StandingGroundDetailKey, defaults.StandingGroundDetail),
            Read(file, StandingPlantsKey, defaults.StandingPlants),
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
        file.SetValue(DisplaySection, StandingGroundDetailKey,
            options.StandingGroundDetail.ToString());
        file.SetValue(DisplaySection, StandingPlantsKey, options.StandingPlants.ToString());
        file.SetValue(DisplaySection, AntiAliasingKey, options.AntiAliasing.ToString());
        file.SetValue(DisplaySection, RenderScaleKey, options.RenderScale.ToString());
        file.SetValue(DisplaySection, FrameRateKey, options.FrameRateLimit.ToString());
        Write(file);
    }

    /// <summary>
    /// Whether imported maps are kept uncompressed (File ▸ Settings, Advanced; VISION.md REN-03).
    /// </summary>
    public static bool HighQualityMaps
    {
        get => Load().GetValue(DisplaySection, HighQualityMapsKey, false).AsBool();
        set => Save(HighQualityMapsKey, value);
    }

    /// <summary>
    /// Whether a panel's folding section (<paramref name="id"/>) was left open on this
    /// computer, or <paramref name="fallback"/> if it's never been folded or opened (VISION.md
    /// UI-07).
    /// </summary>
    public static bool IsSectionOpen(string id, bool fallback) =>
        Load().GetValue(FoldsSection, id, fallback).AsBool();

    /// <summary>Remembers whether a panel's folding section is open.</summary>
    public static void SetSectionOpen(string id, bool open)
    {
        ConfigFile file = Load();
        file.SetValue(FoldsSection, id, open);
        Write(file);
    }

    /// <summary>Whether clouds show, on globes and while standing (View ▸ Clouds).</summary>
    public static bool ShowClouds
    {
        get => Load().GetValue(DisplaySection, CloudsKey, true).AsBool();
        set => Save(CloudsKey, value);
    }

    /// <summary>Whether the haze shows while standing (VISION.md REN-06).</summary>
    public static bool StandingFog
    {
        get => Load().GetValue(DisplaySection, FogKey, true).AsBool();
        set => Save(FogKey, value);
    }

    /// <summary>
    /// Whether the ground shows its painted terrain colors while standing, instead of the
    /// photo materials (VISION.md REN-06, BOD-05). Off by default (owner's choice).
    /// </summary>
    public static bool StandingTerrain
    {
        get => Load().GetValue(DisplaySection, StandingTerrainKey, false).AsBool();
        set => Save(StandingTerrainKey, value);
    }

    /// <summary>
    /// Whether plants show while standing (VISION.md REN-06): the Plants switch, apart from
    /// the Standing plants setting, which says how many there are.
    /// </summary>
    public static bool ShowPlants
    {
        get => Load().GetValue(DisplaySection, ShowPlantsKey, true).AsBool();
        set => Save(ShowPlantsKey, value);
    }

    /// <summary>Whether night vision is on while standing (VISION.md REN-06).</summary>
    public static bool NightVision
    {
        get => Load().GetValue(DisplaySection, NightVisionKey, false).AsBool();
        set => Save(NightVisionKey, value);
    }

    /// <summary>Whether relief is shaded map-style (View ▸ Relief Shading).</summary>
    public static bool MapStyleShading
    {
        get => Load().GetValue(DisplaySection, MapShadingKey, false).AsBool();
        set => Save(MapShadingKey, value);
    }

    /// <summary>
    /// The world files opened or saved lately on this computer, newest first (for the start
    /// screen; VISION.md UI-06).
    /// </summary>
    public static IReadOnlyList<string> RecentWorldPaths =>
        Load().GetValue(FilesSection, RecentWorldsKey, Array.Empty<string>()).AsStringArray();

    /// <summary>Puts a world file at the top of the recent worlds.</summary>
    public static void RememberWorld(string path)
    {
        SaveRecentWorlds(RecentWorlds.Add(RecentWorldPaths, path));
    }

    /// <summary>Takes a world file off the recent worlds (e.g. it's been moved).</summary>
    public static void ForgetWorld(string path)
    {
        SaveRecentWorlds(RecentWorlds.Remove(RecentWorldPaths, path));
    }

    private static void SaveRecentWorlds(IReadOnlyList<string> paths)
    {
        ConfigFile file = Load();
        file.SetValue(FilesSection, RecentWorldsKey, paths.ToArray());
        Write(file);
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
