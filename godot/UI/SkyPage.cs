using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The page for designing the world's night sky (VISION.md REN-07; owner's choice: a star field
/// from a seed that can be re-rolled, and named constellations drawn by joining its stars). It
/// covers the window; the 3D view stops drawing behind it. On the left: the stars' New Stars
/// button, the constellations (new, rename, delete), and how to draw. The rest is the
/// <see cref="SkyCanvas"/>: clicking one star and then another joins them in the chosen
/// constellation, and clicking on keeps drawing from the last star. Every change can be undone.
/// </summary>
public partial class SkyPage : CanvasLayer
{
    private const float SideWidth = 260;
    private const string NewStarsTip = "Scatter a new set of stars over the sky (Ctrl+Z undoes it)";

    private static readonly Color _problemColor = new(1.0f, 0.8f, 0.35f);

    private SkyCanvas _canvas = null!;
    private Label _starCount = null!;
    private Button _newStars = null!;
    private ItemList _list = null!;
    private LineEdit _name = null!;
    private Button _delete = null!;
    private Label _message = null!;
    private Guid? _chosenId;
    private ProcessModeEnum _savedCameraMode;
    private bool _refreshing;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>
    /// The globe's camera, paused while the page is open (its keys would move it).
    /// </summary>
    [Export] public PlanetCamera? GlobeCamera { get; set; }

    /// <summary>Whether the page is showing.</summary>
    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 15;  // Over the toolbar and panels; under the start screen.
        Visible = false;
        if (Session is null)
        {
            GD.PushError("SkyPage needs a world session.");
            return;
        }

        var root = new Panel { MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.13f, 0.16f),
        });
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _canvas = new SkyCanvas();
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.OffsetLeft = SideWidth;
        _canvas.StarClicked += StarClicked;
        _canvas.LineClicked += EraseLine;
        _canvas.EmptyClicked += () => SetPending(null);
        root.AddChild(_canvas);

        var side = new VBoxContainer
        {
            OffsetLeft = 12,
            OffsetTop = 12,
            OffsetRight = SideWidth - 10,
            AnchorBottom = 1,
            OffsetBottom = -12,
        };
        side.AddThemeConstantOverride("separation", 8);
        root.AddChild(side);
        BuildSide(side);

        Session.Changed += () =>
        {
            if (IsOpen)
            {
                Refresh();
            }
        };
        Session.WorldClosed += _ => Close();
    }

    /// <summary>Shows the page, with the whole sky in view.</summary>
    public void Open()
    {
        if (IsOpen || Session is null)
        {
            return;
        }

        Visible = true;
        GetViewport().Disable3D = true;
        if (GlobeCamera is not null)
        {
            _savedCameraMode = GlobeCamera.ProcessMode;
            GlobeCamera.ProcessMode = ProcessModeEnum.Disabled;
        }

        _chosenId ??= Session.World.Constellations.FirstOrDefault()?.Id;
        Refresh();
        _canvas.FitView();
        _canvas.GrabFocus();
        ShowMessage("");
    }

    /// <summary>Hides the page, back to the system.</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        SetPending(null);
        Visible = false;
        GetViewport().Disable3D = false;
        if (GlobeCamera is not null)
        {
            GlobeCamera.ProcessMode = _savedCameraMode;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            return;
        }

        // Esc stops a line first, then leaves the page.
        if (_canvas.PendingStar is not null)
        {
            SetPending(null);
        }
        else
        {
            Close();
        }

        GetViewport().SetInputAsHandled();
    }

    private void BuildSide(VBoxContainer side)
    {
        var title = new Label { Text = "Night Sky" };
        title.AddThemeFontSizeOverride("font_size", 22);
        side.AddChild(title);
        _starCount = new Label { Modulate = new Color(1, 1, 1, 0.7f) };
        side.AddChild(_starCount);
        _newStars = CreateButton("New Stars", NewStarsTip, NewStars);
        side.AddChild(_newStars);
        side.AddChild(new HSeparator());

        side.AddChild(new Label { Text = "Constellations" });
        _list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 200),
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _list.ItemSelected += index =>
        {
            _chosenId = Guid.Parse(_list.GetItemMetadata((int)index).AsString());
            SetPending(null);
            Refresh();
        };
        side.AddChild(_list);

        var buttons = new HBoxContainer();
        buttons.AddChild(CreateButton("New", "Start a new constellation", NewConstellation));
        _delete = CreateButton("Delete",
            "Delete the chosen constellation (Ctrl+Z brings it back); its stars stay", Delete);
        buttons.AddChild(_delete);
        side.AddChild(buttons);
        _name = new LineEdit { PlaceholderText = "Constellation name" };
        _name.TextChanged += Rename;
        side.AddChild(_name);

        side.AddChild(new Label
        {
            Text = "Click a star, then another, to join them in the chosen constellation; " +
                "keep clicking to go on from the last. Click one of its lines to erase it. " +
                "Esc stops drawing, then closes. Right-drag to pan, the wheel zooms.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.6f),
        });
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _message.AddThemeColorOverride("font_color", _problemColor);
        side.AddChild(_message);
        side.AddChild(CreateButton("Done", "Back to the system (Esc)", Close));
    }

    // Shows the world's sky and constellations as they are now.
    private void Refresh()
    {
        World world = Session!.World;
        if (_chosenId is Guid id && world.Constellations.All(c => c.Id != id))
        {
            _chosenId = null;
            SetPending(null);
        }

        _refreshing = true;
        _canvas.ShowSky(world.StarSeed);
        _canvas.Constellations = [.. world.Constellations];
        _canvas.ChosenId = _chosenId;
        _starCount.Text = $"{_canvas.StarCount:N0} stars";
        DisabledTip.Apply(_newStars, NewStarsTip, world.Constellations.Count > 0
            ? "Delete the constellations first: they're drawn on these stars"
            : null);

        _list.Clear();
        foreach (Constellation constellation in world.Constellations)
        {
            int index = _list.AddItem($"{constellation.Name}  ({constellation.Lines.Count})");
            _list.SetItemMetadata(index, constellation.Id.ToString());
            if (constellation.Id == _chosenId)
            {
                _list.Select(index);
            }
        }

        Constellation? chosen = world.Constellations.FirstOrDefault(c => c.Id == _chosenId);
        _name.Editable = chosen is not null;
        if (!_name.HasFocus())
        {
            _name.Text = chosen?.Name ?? "";
        }

        DisabledTip.Apply(_delete, "Delete the chosen constellation (Ctrl+Z brings it back)",
            chosen is null ? "Choose a constellation in the list first" : null);
        _refreshing = false;
        _canvas.QueueRedraw();
    }

    private void StarClicked(int star)
    {
        if (_chosenId is null && !NewConstellation())
        {
            return;
        }

        if (_canvas.PendingStar is not int from)
        {
            SetPending(star);
            return;
        }

        if (from == star)
        {
            SetPending(null);  // Clicking the same star again stops drawing.
            return;
        }

        string? problem = Session!.AddConstellationLine(_chosenId!.Value, new StarLink(from, star));
        ShowMessage(problem is null ? "" : $"Couldn't draw that line: {problem}.");
        SetPending(star);
    }

    private void EraseLine(StarLink line)
    {
        if (_chosenId is Guid id)
        {
            Session!.RemoveConstellationLine(id, line);
            ShowMessage("Erased a line (Ctrl+Z brings it back).");
        }
    }

    // Starts a constellation and chooses it; false if the sky is full.
    private bool NewConstellation()
    {
        if (Session!.AddConstellation() is not Constellation added)
        {
            ShowMessage($"A sky can have up to {Constellation.MaxCount} constellations.");
            return false;
        }

        _chosenId = added.Id;
        SetPending(null);
        Refresh();
        return true;
    }

    private void Delete()
    {
        if (_chosenId is Guid id)
        {
            Session!.DeleteConstellation(id);
            ShowMessage("Deleted it (Ctrl+Z brings it back).");
        }
    }

    private void Rename(string name)
    {
        if (_refreshing || _chosenId is not Guid id)
        {
            return;
        }

        string? problem = Session!.RenameConstellation(id, name);
        ShowMessage(problem is null ? "" : $"Couldn't rename it: {problem}.");
    }

    private void NewStars()
    {
        if (Session!.RerollStars() is string problem)
        {
            ShowMessage($"{problem}.");
            return;
        }

        ShowMessage("New stars (Ctrl+Z brings the old ones back).");
    }

    private void SetPending(int? star)
    {
        _canvas.PendingStar = star;
        _canvas.QueueRedraw();
    }

    private void ShowMessage(string text)
    {
        _message.Text = text;
    }

    private static Button CreateButton(string text, string tooltip, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.Pressed += pressed;
        return button;
    }

    private static Button CreateButton(string text, string tooltip, Func<bool> pressed) =>
        CreateButton(text, tooltip, () => { pressed(); });
}
