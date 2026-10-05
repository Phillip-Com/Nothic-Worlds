using Godot;
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
    }

    private const int StyleMenuId = 100;

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
        menu.AddSeparator(id: StyleMenuId + 1);
        menu.AddCheckItem("Pins", (int)MenuItem.Pins);
        menu.AddCheckItem("Weather Pins", (int)MenuItem.WeatherPins);
        menu.AddCheckItem("Regions", (int)MenuItem.Regions);
        menu.AddCheckItem("Terrain", (int)MenuItem.Terrain);
        menu.AddCheckItem("Clouds", (int)MenuItem.Clouds);
        menu.AddCheckItem("Wind", (int)MenuItem.Wind);
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.Clouds),
            "Live weather on planets and moons with air: clouds, rain, and snow");
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.Wind),
            "Streaks flowing with the wind over planets and moons with air");
        menu.AddSeparator();
        menu.AddCheckItem("Season Markers", (int)MenuItem.SeasonMarkers);
        menu.AddCheckItem("Meteor Shower Markers", (int)MenuItem.ShowerMarkers);
        menu.AddCheckItem("Eclipse Markers", (int)MenuItem.EclipseMarkers);
        menu.AddSeparator();
        menu.AddCheckItem("Grid (G)", (int)MenuItem.Grid);
        menu.AddCheckItem("True Scale", (int)MenuItem.TrueScale);
        menu.AddCheckItem("Orbit Guide", (int)MenuItem.OrbitGuide);
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.TrueScale),
            "Real sizes and distances (most bodies become tiny dots). Off: a readable view " +
            "with distances compressed and small bodies enlarged.");
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.OrbitGuide),
            "While the System panel is open: green rings where orbits around the selected " +
            "body (and around what it circles) would stay steady, red where they wouldn't.");
        menu.AddSubmenuNodeItem("Relief", BuildReliefMenu());
        menu.AddSubmenuNodeItem("Relief Detail", BuildReliefDetailMenu());
        menu.AddSubmenuNodeItem("Relief Shading", BuildReliefShadingMenu());
        menu.AddSubmenuNodeItem("Cloud Detail", BuildCloudDetailMenu());
        menu.AboutToPopup += () => ShowChecks(menu);
        menu.IdPressed += id => Toggle((MenuItem)(int)id);
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

    // How finely sculpted globes are drawn (a quality setting, remembered on this computer).
    private PopupMenu BuildReliefDetailMenu()
    {
        var detail = new PopupMenu();
        detail.AddRadioCheckItem("Low (lightest)", (int)ReliefDetail.Low);
        detail.AddRadioCheckItem("Standard", (int)ReliefDetail.Standard);
        detail.AddRadioCheckItem("High (sharpest outlines)", (int)ReliefDetail.High);
        detail.AboutToPopup += () =>
        {
            for (int index = 0; index < detail.ItemCount; index++)
            {
                detail.SetItemChecked(index,
                    detail.GetItemId(index) == (int)(System?.ReliefDetail ?? 0));
            }
        };
        detail.IdPressed += id =>
        {
            if (System is not null)
            {
                System.ReliefDetail = (ReliefDetail)(int)id;
                AppSettings.ReliefDetail = System.ReliefDetail;
            }
        };
        return detail;
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

    // How finely clouds are drawn (owner's choice: a quality setting, remembered here).
    private PopupMenu BuildCloudDetailMenu()
    {
        var detail = new PopupMenu();
        detail.AddRadioCheckItem("Low", (int)CloudDetail.Low);
        detail.SetItemTooltip(0, "Softer, coarser clouds: the lightest to draw");
        detail.AddRadioCheckItem("High", (int)CloudDetail.High);
        detail.SetItemTooltip(1, "Finer clouds with ragged edges");
        detail.AboutToPopup += () =>
        {
            foreach (CloudDetail level in Enum.GetValues<CloudDetail>())
            {
                detail.SetItemChecked(detail.GetItemIndex((int)level),
                    LiveWeather?.Detail == level);
            }
        };
        detail.IdPressed += id =>
        {
            if (LiveWeather is not null)
            {
                LiveWeather.Detail = (CloudDetail)(int)id;
                AppSettings.CloudDetail = LiveWeather.Detail;
            }
        };
        return detail;
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
