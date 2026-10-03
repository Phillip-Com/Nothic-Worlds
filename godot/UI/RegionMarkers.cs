using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Region names on the globes, and the pop-up when a region is clicked (VISION.md LORE-01;
/// owner's choices: the name in the middle when close enough; clicking a region shows its
/// notes and everything placed in it). Each item in the pop-up opens it; "Edit Region" opens
/// the Regions panel on it.
/// </summary>
/// <remarks>
/// Only the selected body's regions answer clicks (other globes are for flying to). A click
/// counts only if the mouse barely moved, so dragging the camera never opens it. Clicks are left
/// alone while pins are being placed, regions edited, or map pieces worked on. Must come after
/// the body markers and before the pin markers in the scene, so pins take their clicks first.
/// </remarks>
public partial class RegionMarkers : CanvasLayer
{
    private const float MinGlobePixels = 60.0f;
    private const float ClickSlopPixels = 5.0f;
    private const int NotesPreviewLength = 400;

    private Control _overlay = null!;
    private PopupPanel _popup = null!;
    private VBoxContainer _popupList = null!;
    private Vector2? _pressedAt;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the globes.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for projecting names and clicks.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar: names hide whenever it does; it opens the side panels.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Draws the regions (its Regions toggle hides the names too).</summary>
    [Export] public RegionRenderer? Renderer { get; set; }

    /// <summary>While it's drawing or editing, clicks belong to it.</summary>
    [Export] public RegionEditor? Editor { get; set; }

    /// <summary>While a pin is being placed, clicks belong to it.</summary>
    [Export] public PinPlacer? Placer { get; set; }

    /// <summary>While the Map panel is open, clicks on the globe are for map pieces.</summary>
    [Export] public MapPanel? Map { get; set; }

    /// <summary>The Regions panel, for "Edit Region".</summary>
    [Export] public RegionsPanel? Panel { get; set; }

    /// <summary>The Journal panel, to open an entry placed in a region.</summary>
    [Export] public JournalPanel? Journal { get; set; }

    /// <summary>The timeline strip, whose editor opens an event placed in a region.</summary>
    [Export] public TimelineStrip? Timeline { get; set; }

    public override void _Ready()
    {
        Layer = 0;
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawNames;
        AddChild(_overlay);
        _popup = new PopupPanel();
        _popup.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.09f, 0.11f, 0.97f),
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });
        _popupList = new VBoxContainer();
        _popup.AddChild(_popupList);
        AddChild(_popup);

        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("RegionMarkers needs a world session, system view, and camera.");
            SetProcessUnhandledInput(false);
            return;
        }

        // Names move with the view, so redraw as it moves (only if there are any).
        System.Placed += () =>
        {
            if (Session.World.Regions.Count > 0)
            {
                _overlay.QueueRedraw();
            }
        };
        Session.Changed += _overlay.QueueRedraw;
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += () => Visible = Toolbar.Visible;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click
            || !Visible || !(Renderer?.ShowRegions ?? true) || IsSomethingElseActive())
        {
            return;
        }

        if (click.Pressed)
        {
            _pressedAt = click.Position;  // Just noted: the camera may still drag from here.
            return;
        }

        if (_pressedAt is Vector2 start && start.DistanceTo(click.Position) <= ClickSlopPixels
            && RegionsUnder(click.Position) is { Count: > 0 } regions)
        {
            ShowPopup(click.Position, regions);
            GetViewport().SetInputAsHandled();
        }

        _pressedAt = null;
    }

    private bool IsSomethingElseActive()
    {
        return (Editor?.IsDrawing ?? false) || Editor?.EditingRegionId is not null
            || (Placer?.IsPlacing ?? false) || (Map?.IsPanelOpen ?? false);
    }

    // The selected body's regions under a screen position, topmost (latest drawn) first.
    private List<Region> RegionsUnder(Vector2 position)
    {
        if (System!.SurfaceFor(Session!.SelectedBodyId) is not PlanetSurface globe
            || GlobePicker.CoordinateAt(Camera!, globe, position) is not GeoCoordinate spot)
        {
            return [];
        }

        return [.. LoreRules.RegionsAt(Session.World, Session.SelectedBodyId, spot).Reverse()];
    }

    private void DrawNames()
    {
        if (!Visible || !(Renderer?.ShowRegions ?? true) || Session is null)
        {
            return;
        }

        Font font = _overlay.GetThemeDefaultFont();
        foreach (Region region in Session.World.Regions)
        {
            if (System!.SurfaceFor(region.BodyId) is not PlanetSurface globe
                || !IsBigEnough(globe)
                || SphericalPolygon.Center(region.Corners) is not GeoCoordinate center
                || GlobePicker.ScreenPositionOf(Camera!, globe, center) is not Vector2 at)
            {
                continue;
            }

            Vector2 size = font.GetStringSize(region.Name);
            Vector2 corner = at - size / 2 + new Vector2(0, size.Y * 0.75f);
            _overlay.DrawString(font, corner + Vector2.One, region.Name, modulate: Colors.Black);
            _overlay.DrawString(font, corner, region.Name, modulate: region.Color.ToGodot());
        }
    }

    // Only globes drawn large enough to read names on (and in front of the camera).
    private bool IsBigEnough(PlanetSurface globe)
    {
        if (Camera!.IsPositionBehind(globe.GlobalPosition))
        {
            return false;
        }

        Vector2 center = Camera.UnprojectPosition(globe.GlobalPosition);
        Vector3 edge = globe.GlobalPosition
            + Camera.GlobalBasis.X * globe.GlobalTransform.Basis.X.Length();
        return center.DistanceTo(Camera.UnprojectPosition(edge)) >= MinGlobePixels;
    }

    private void ShowPopup(Vector2 at, List<Region> regions)
    {
        foreach (Node child in _popupList.GetChildren())
        {
            _popupList.RemoveChild(child);
            child.QueueFree();
        }

        Body body = Session!.SelectedBody;
        for (int i = 0; i < regions.Count; i++)
        {
            if (i > 0)
            {
                _popupList.AddChild(new HSeparator());
            }

            AddRegionSection(regions[i], body);
        }

        _popup.ResetSize();
        _popup.Position = (Vector2I)(at + new Vector2(12, -12));
        _popup.Popup();
    }

    private void AddRegionSection(Region region, Body body)
    {
        var name = new Label { Text = region.Name, Modulate = region.Color.ToGodot() };
        _popupList.AddChild(name);
        if (region.Notes.Trim() is { Length: > 0 } notes)
        {
            _popupList.AddChild(new Label
            {
                Text = notes.Length > NotesPreviewLength
                    ? notes[..NotesPreviewLength] + "…"
                    : notes,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(300, 0),
            });
        }

        (IEnumerable<JournalEntry> entries, IEnumerable<TimelineEvent> events) =
            LoreRules.PlacedIn(Session!.World, region.Id);
        foreach (JournalEntry entry in entries)
        {
            Guid id = entry.Id;
            AddButton($"Journal: {entry.Title}", () =>
            {
                Toolbar?.ShowJournal();
                Journal?.SelectEntry(id);
            });
        }

        foreach (TimelineEvent timelineEvent in events.OrderBy(e => e.StartDays))
        {
            TimelineEvent opened = timelineEvent;
            string when = BodyClock.Describe(body, timelineEvent.StartDays);
            AddButton($"Event: {timelineEvent.Title}, {when}",
                () => Timeline?.EditEvent(opened));
        }

        Guid regionId = region.Id;
        AddButton("Edit Region…", () =>
        {
            Toolbar?.ShowRegions();
            Panel?.SelectRegion(regionId);
        });
    }

    private void AddButton(string text, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.Pressed += () =>
        {
            _popup.Hide();
            pressed();
        };
        _popupList.AddChild(button);
    }
}
