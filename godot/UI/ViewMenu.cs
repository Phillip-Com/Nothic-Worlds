using Godot;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;

namespace NothicWorlds.UI;

/// <summary>
/// The View menu (VISION.md UI-01; owner's choice: the show/hide switches as checkable items in one
/// menu): Pins, Weather Pins, Regions, Terrain, Season Markers, Meteor Shower Markers, Eclipse
/// Markers, Grid (also the G
/// key), and True Scale. The checkmarks are refreshed each time it opens, so they always match
/// what's shown.
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
    }

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
        menu.AddCheckItem("Pins", (int)MenuItem.Pins);
        menu.AddCheckItem("Weather Pins", (int)MenuItem.WeatherPins);
        menu.AddCheckItem("Regions", (int)MenuItem.Regions);
        menu.AddCheckItem("Terrain", (int)MenuItem.Terrain);
        menu.AddSeparator();
        menu.AddCheckItem("Season Markers", (int)MenuItem.SeasonMarkers);
        menu.AddCheckItem("Meteor Shower Markers", (int)MenuItem.ShowerMarkers);
        menu.AddCheckItem("Eclipse Markers", (int)MenuItem.EclipseMarkers);
        menu.AddSeparator();
        menu.AddCheckItem("Grid (G)", (int)MenuItem.Grid);
        menu.AddCheckItem("True Scale", (int)MenuItem.TrueScale);
        menu.SetItemTooltip(menu.GetItemIndex((int)MenuItem.TrueScale),
            "Real sizes and distances (most bodies become tiny dots). Off: a readable view " +
            "with distances compressed and small bodies enlarged.");
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
        }
    }
}
