using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Add menu (VISION.md UI-01): one place to add anything, each item opening the panel it
/// belongs to and starting there: Planet, Moon, Star (System panel), Region (Regions panel,
/// then click its corners), Journal Entry (Journal panel), Timeline Event (the timeline, at the
/// current time), and Weather Pin (click the spot, then name it).
/// </summary>
public partial class AddMenu : Node
{
    private MenuButton _button = null!;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>Where the menu goes; it opens the panels.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Adds bodies.</summary>
    [Export] public SystemPanel? SystemPanel { get; set; }

    /// <summary>Draws a new region.</summary>
    [Export] public RegionEditor? RegionEditor { get; set; }

    /// <summary>Adds journal entries.</summary>
    [Export] public JournalPanel? Journal { get; set; }

    /// <summary>Adds timeline events.</summary>
    [Export] public TimelineStrip? Timeline { get; set; }

    /// <summary>Adds weather pins.</summary>
    [Export] public WeatherMarkers? Weather { get; set; }

    private enum MenuItem
    {
        Planet,
        Moon,
        Star,
        Region,
        JournalEntry,
        TimelineEvent,
        WeatherPin,
    }

    public override void _Ready()
    {
        if (Toolbar is null || Session is null)
        {
            GD.PushError("AddMenu needs a world session and a toolbar.");
            return;
        }

        _button = new MenuButton
        {
            Text = "Add",
            Flat = false,
            FocusMode = Control.FocusModeEnum.None,
        };
        PopupMenu menu = _button.GetPopup();
        menu.AddItem("Planet", (int)MenuItem.Planet);
        menu.AddItem("Moon", (int)MenuItem.Moon);
        menu.AddItem("Star", (int)MenuItem.Star);
        menu.AddSeparator();
        menu.AddItem("Region", (int)MenuItem.Region);
        menu.AddItem("Weather Pin", (int)MenuItem.WeatherPin);
        menu.AddSeparator();
        menu.AddItem("Journal Entry", (int)MenuItem.JournalEntry);
        menu.AddItem("Timeline Event", (int)MenuItem.TimelineEvent);
        menu.AboutToPopup += () => UpdateItems(menu);
        menu.IdPressed += id => _ = AddAsync((MenuItem)(int)id);
        Toolbar.MenuArea.AddChild(_button);
    }

    // Things that go on a surface or circle a planet can't be added while a star is selected.
    private void UpdateItems(PopupMenu menu)
    {
        bool star = Session!.SelectedBody.Kind == BodyKind.Star;
        foreach (MenuItem item in new[] { MenuItem.Moon, MenuItem.Region, MenuItem.WeatherPin })
        {
            int index = menu.GetItemIndex((int)item);
            menu.SetItemDisabled(index, star);
            menu.SetItemTooltip(index, star ? "Select a planet or moon first" : "");
        }
    }

    private async Task AddAsync(MenuItem item)
    {
        switch (item)
        {
            case MenuItem.Planet or MenuItem.Moon or MenuItem.Star:
                Toolbar!.ShowSystem();
                if (SystemPanel is not null)
                {
                    await SystemPanel.AddAsync(item switch
                    {
                        MenuItem.Planet => BodyKind.Planet,
                        MenuItem.Moon => BodyKind.Moon,
                        _ => BodyKind.Star,
                    });
                }

                break;
            case MenuItem.Region:
                Toolbar!.ShowRegions();
                RegionEditor?.StartDrawing();
                break;
            case MenuItem.JournalEntry:
                Toolbar!.ShowJournal();
                Journal?.AddEntry();
                break;
            case MenuItem.TimelineEvent:
                Toolbar!.ShowTimeline();
                Timeline?.AddEvent();
                break;
            case MenuItem.WeatherPin:
                Weather?.StartAdding();
                break;
        }
    }
}
