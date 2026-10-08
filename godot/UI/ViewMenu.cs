using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The View menu (VISION.md UI-01; owner's choice: the show/hide switches as checkable items in one
/// menu): the world's Style (REN-05), then Pins, Weather Pins, Regions, Terrain, Clouds and Wind
/// (WTH-02), Season Markers, Meteor Shower Markers, Eclipse Markers, Grid (also the G key), and
/// True Scale. The checkmarks
/// are refreshed each time it opens, so they always match what's shown.
/// </summary>
public partial class ViewMenu : Node
{
    private MenuButton _button = null!;

    /// <summary>Where the menu goes.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The journal and event pins.</summary>
    [Export] public PinMarkers? Pins { get; set; }

    /// <summary>The weather pins.</summary>
    [Export] public WeatherMarkers? Weather { get; set; }

    /// <summary>The regions drawn on the globes.</summary>
    [Export] public RegionRenderer? Regions { get; set; }

    /// <summary>The body markers, which draw the solstice and equinox markers.</summary>
    [Export] public BodyMarkers? Bodies { get; set; }

    /// <summary>The eclipse markers.</summary>
    [Export] public EclipseMarkers? Eclipses { get; set; }

    /// <summary>The system view: terrain, the grid, and true scale.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The open world, whose style is chosen here.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The live weather drawn over the globes: clouds, wind, and their detail.</summary>
    [Export] public WeatherDisplay? LiveWeather { get; set; }

    /// <summary>Standing on a world in first person (VISION.md REN-06).</summary>
    [Export] public FirstPersonMode? Standing { get; set; }

    /// <summary>The sky behind the system: its stars and constellation lines.</summary>
    [Export] public NebulaBackdrop? Backdrop { get; set; }

    private enum MenuItem
    {
        Pins,
        WeatherPins,
        Regions,
        SeasonMarkers,
        ShowerMarkers,
        Terrain,
        EclipseMarkers,
        Grid,
        TrueScale,
        OrbitGuide,
        Clouds,
        Wind,
        StarsFromOrbit,
        ConstellationLines,
    }

    private const int StyleMenuId = 100;
    private const int StandMenuId = 102;

    // The relief exaggerations offered (owner's choice: about 1× to 50×).
    private static readonly int[] _reliefChoices = [1, 5, 10, 20, 50];

    public override void _Ready()
    {
        if (Toolbar is null)
        {
            GD.PushError("ViewMenu needs a toolbar.");
            return;
        }

        _button = new MenuButton
        {
            Text = "View",
            Flat = false,
            FocusMode = Control.FocusModeEnum.None,
        };
        PopupMenu menu = _button.GetPopup();
        // Explicit ids: one left out is the item's position, which would clash with MenuItem's.
        menu.AddSubmenuNodeItem("Style", BuildStyleMenu(), StyleMenuId);
        menu.AddItem("Stand Here…", StandMenuId);
        menu.SetItemTooltip(menu.GetItemIndex(StandMenuId),
            "Click a spot on the selected planet or moon to stand there, and look around in " +
            "first person: walk, fly, and watch the sky (Esc to come back)");
        // Grouped under headings (VISION.md UI-07), so the list reads at a glance.
        menu.AddSeparator("On the Globe", StyleMenuId + 1);
        menu.AddCheckItem("Terrain", (int)MenuItem.Terrain);
        menu.AddCheckItem("Regions", (int)MenuItem.Regions);
        menu.AddCheckItem("Pins", (int)MenuItem.Pins);
        menu.AddCheckItem("Weather Pins", (int)MenuItem.WeatherPins);
        menu.AddCheckItem("Grid (G)", (int)MenuItem.Grid);
        menu.AddSubmenuNodeItem("Relief", BuildReliefMenu());
        menu.AddSubmenuNodeItem("Relief Shading", BuildReliefShadingMenu());
        menu.AddSeparator("Weather");
        menu.AddCheckItem("Clouds", (int)MenuItem.Clouds);
        menu.AddCheckItem("Wind", (int)MenuItem.Wind);
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.Clouds),
            "Live weather on planets and moons with air: clouds, rain, and snow");
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.Wind),
            "Streaks flowing with the wind over planets and moons with air");
        menu.AddSeparator("Sky");
        menu.AddCheckItem("Stars from Orbit", (int)MenuItem.StarsFromOrbit);
        menu.AddCheckItem("Constellation Lines", (int)MenuItem.ConstellationLines);
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.StarsFromOrbit),
            "The world's own stars behind the system, as they're seen from its worlds");
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.ConstellationLines),
            "The lines of the constellations, on the night sky and with the stars from orbit");
        menu.AddSeparator("Orbits");
        menu.AddCheckItem("Season Markers", (int)MenuItem.SeasonMarkers);
        menu.AddCheckItem("Meteor Shower Markers", (int)MenuItem.ShowerMarkers);
        menu.AddCheckItem("Eclipse Markers", (int)MenuItem.EclipseMarkers);
        menu.AddCheckItem("Orbit Guide", (int)MenuItem.OrbitGuide);
        menu.AddCheckItem("True Scale", (int)MenuItem.TrueScale);
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.TrueScale),
            "Real sizes and distances (most bodies become tiny dots). Off: a readable view " +
            "with distances compressed and small bodies enlarged.");
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.OrbitGuide),
            "While the System panel is open: green rings where orbits around the selected " +
            "body (and around what it circles) would stay steady, red where they wouldn't.");
        menu.AboutToPopup += () => ShowChecks(menu);
        menu.IdPressed += id =>
        {
            if (id == StandMenuId)
            {
                Standing?.ChooseWhereToStand();
            }
            else
            {
                Toggle((MenuItem)(int)id);
            }
        };
        Toolbar.MenuArea.AddChild(_button);
    }

    private void ShowChecks(PopupMenu menu)
    {
        foreach (MenuItem item in Enum.GetValues<MenuItem>())
        {
            menu.SetItemChecked(menu.GetItemIndex((int)item), IsOn(item));
        }
    }

    private bool IsOn(MenuItem item) => item switch
    {
        MenuItem.Pins => Pins?.ShowPins ?? false,
        MenuItem.WeatherPins => Weather?.ShowPins ?? false,
        MenuItem.Regions => Regions?.ShowRegions ?? false,
        MenuItem.SeasonMarkers => Bodies?.ShowSeasonMarkers ?? false,
        MenuItem.ShowerMarkers => Bodies?.ShowShowerMarkers ?? false,
        MenuItem.EclipseMarkers => Eclipses?.ShowMarkers ?? false,
        MenuItem.Terrain => System?.ShowTerrain ?? false,
        MenuItem.Grid => System?.ShowGrid ?? false,
        MenuItem.OrbitGuide => System?.ShowOrbitGuide ?? false,
        MenuItem.Clouds => LiveWeather?.ShowClouds ?? false,
        MenuItem.Wind => LiveWeather?.ShowWind ?? false,
        MenuItem.StarsFromOrbit => Backdrop?.ShowStars ?? false,
        MenuItem.ConstellationLines => Backdrop?.ShowConstellations ?? false,
        _ => System?.DisplayScale == SystemScale.True,
    };

    private void Toggle(MenuItem item)
    {
        bool on = !IsOn(item);
        switch (item)
        {
            case MenuItem.Pins when Pins is not null:
                Pins.ShowPins = on;
                break;
            case MenuItem.WeatherPins when Weather is not null:
                Weather.ShowPins = on;
                break;
            case MenuItem.Regions when Regions is not null:
                Regions.ShowRegions = on;
                break;
            case MenuItem.SeasonMarkers when Bodies is not null:
                Bodies.ShowSeasonMarkers = on;
                break;
            case MenuItem.ShowerMarkers when Bodies is not null:
                Bodies.ShowShowerMarkers = on;
                break;
            case MenuItem.EclipseMarkers when Eclipses is not null:
                Eclipses.ShowMarkers = on;
                break;
            case MenuItem.Terrain when System is not null:
                System.ShowTerrain = on;
                break;
            case MenuItem.Grid when System is not null:
                System.ShowGrid = on;
                break;
            case MenuItem.TrueScale when System is not null:
                System.DisplayScale = on ? SystemScale.True : SystemScale.Readable;
                break;
            case MenuItem.OrbitGuide when System is not null:
                System.ShowOrbitGuide = on;
                break;
            case MenuItem.Clouds when LiveWeather is not null:
                LiveWeather.ShowClouds = on;
                break;
            case MenuItem.Wind when LiveWeather is not null:
                LiveWeather.ShowWind = on;
                break;
            case MenuItem.StarsFromOrbit when Backdrop is not null:
                Backdrop.ShowStars = on;
                break;
            case MenuItem.ConstellationLines when Backdrop is not null:
                Backdrop.ShowConstellations = on;
                break;
        }
    }

    // How much sculpted relief is exaggerated (VISION.md BOD-04): one choice of several.
    private PopupMenu BuildReliefMenu()
    {
        var relief = new PopupMenu();
        foreach (int times in _reliefChoices)
        {
            relief.AddRadioCheckItem(times == 1 ? "True Scale (1×)" : $"{times}×", times);
        }

        relief.AboutToPopup += () =>
        {
            for (int index = 0; index < relief.ItemCount; index++)
            {
                relief.SetItemChecked(index,
                    relief.GetItemId(index) == (int)(System?.ReliefExaggeration ?? 1));
            }
        };
        relief.IdPressed += id =>
        {
            if (System is not null)
            {
                System.ReliefExaggeration = id;
            }
        };
        return relief;
    }

    // How the world is drawn (owner's choice: saved with the world, so it's an undoable edit).
    private PopupMenu BuildStyleMenu()
    {
        var styles = new PopupMenu();
        (VisualStyle Style, string Tip)[] choices =
        [
            (VisualStyle.Painterly, "Soft bands of light, brush strokes, and gentle outlines"),
            (VisualStyle.Realistic, "True lighting"),
            (VisualStyle.Simple, "Flat colors, a crisp day and night, and clean outlines"),
        ];
        foreach ((VisualStyle style, string tip) in choices)
        {
            styles.AddRadioCheckItem(style.ToString(), (int)style);
            styles.SetItemTooltip(styles.GetItemIndex((int)style), tip);
        }

        styles.AboutToPopup += () =>
        {
            foreach ((VisualStyle style, _) in choices)
            {
                styles.SetItemChecked(styles.GetItemIndex((int)style),
                    Session?.World.Style == style);
            }
        };
        styles.IdPressed += id => Session?.SetStyle((VisualStyle)(int)id);
        return styles;
    }

    // How relief is shaded: by the sunlight, or map-style from a fixed direction.
    private PopupMenu BuildReliefShadingMenu()
    {
        const int sunlight = 0;
        const int mapStyle = 1;
        var shading = new PopupMenu();
        shading.AddRadioCheckItem("Sunlight", sunlight);
        shading.SetItemTooltip(0, "Lit by the star, as it really would be: relief shows best " +
            "near sunrise and sunset");
        shading.AddRadioCheckItem("Map-style", mapStyle);
        shading.SetItemTooltip(1, "Lit from the northwest as on printed maps, so relief always " +
            "shows (the star still makes day and night)");
        shading.AboutToPopup += () =>
        {
            bool map = System?.MapStyleShading ?? false;
            shading.SetItemChecked(shading.GetItemIndex(sunlight), !map);
            shading.SetItemChecked(shading.GetItemIndex(mapStyle), map);
        };
        shading.IdPressed += id =>
        {
            if (System is not null)
            {
                System.MapStyleShading = id == mapStyle;
                AppSettings.MapStyleShading = System.MapStyleShading;
            }
        };
        return shading;
    }
}
