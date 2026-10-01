using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Maps;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Calibrate workspace (VISION.md MAP-05, owner's choice: side by side). The flat map and its
/// guide lines fill the left half of the screen, and a live view of the globe fills the right
/// half with the true latitude/longitude grid showing. Every drag updates the globe immediately.
/// Done keeps the changes, Cancel (or Esc) puts everything back, and Reset removes the
/// calibration.
/// </summary>
public partial class CalibrationWorkspace : CanvasLayer
{
    private const float PanelPadding = 10.0f;

    private CalibrationCanvas _canvas = null!;
    private ConfirmationDialog _addDialog = null!;
    private Label _addPrompt = null!;
    private SpinBox _addValue = null!;
    private bool _addingLatitude;
    private CalibrationSnapshot? _snapshot;
    private bool _gridWasShown;
    private Camera3D _previewCamera = null!;

    /// <summary>The open world, whose map is being calibrated.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The camera, moved to the right half while calibrating.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>Where the outcome is reported.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>True while the workspace is open.</summary>
    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 10;  // Above the toolbar and other overlays.
        Visible = false;

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        panel.AnchorRight = 0.5f;
        // Solid, so nothing behind the panel shows through.
        panel.AddThemeStyleboxOverride(
            "panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.11f, 0.14f) });
        AddChild(panel);
        BuildGlobePreview();

        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "top", "right", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", (int)PanelPadding);
        }

        panel.AddChild(margin);
        var layout = new VBoxContainer();
        margin.AddChild(layout);

        layout.AddChild(new Label { Text = "Calibrate Map" });

        // Buttons wrap onto another row when the window is narrow, so the panel always fits in
        // its half of the screen.
        var buttons = new HFlowContainer();
        buttons.AddChild(CreateButton("Add Latitude…", () => AskToAdd(latitude: true)));
        buttons.AddChild(CreateButton("Add Longitude…", () => AskToAdd(latitude: false)));
        buttons.AddChild(CreateButton("Reset", Reset));
        buttons.AddChild(CreateButton("Cancel", Cancel));
        buttons.AddChild(CreateButton("Done", Done));
        layout.AddChild(buttons);

        layout.AddChild(new Label
        {
            Text = "Drag each line to where that latitude or longitude really is on your map; " +
                "the globe on the right updates as you go. Right-click a line to remove it.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _canvas = new CalibrationCanvas { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _canvas.CalibrationChanged += calibration => Session?.SetCalibration(calibration);
        layout.AddChild(_canvas);

        // The dialog's own text would sit under a custom child, so use a label of our own.
        _addPrompt = new Label();
        _addValue = new SpinBox { Step = 0.5, CustomMinimumSize = new Vector2(120, 0) }
            .WithArrowKeys();
        var addLayout = new VBoxContainer();
        addLayout.AddChild(_addPrompt);
        addLayout.AddChild(_addValue);
        _addDialog = new ConfirmationDialog { OkButtonText = "Add" };
        _addDialog.AddChild(addLayout);
        _addDialog.Confirmed += AddGuide;
        AddChild(_addDialog);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            Cancel();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode is Key.Enter or Key.KpEnter)
        {
            Done();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Opens the workspace for the current map. Does nothing if there's no map.</summary>
    public void Open()
    {
        if (IsOpen || Session?.Surface?.MapTexture is not Texture2D texture
            || Session.MapCheck is not MapImageCheck check)
        {
            return;
        }

        _snapshot = Session.BeginCalibration();
        _canvas.Load(texture, Session.Projection, check.AspectRatio,
            Session.Calibration ?? MapCalibration.CreateDefault());
        _gridWasShown = Session.Surface.ShowGrid;
        Session.Surface.ShowGrid = true;
        SetToolbarVisible(false);

        // The right half becomes its own view, centered on the planet (a view shifted off
        // center stretches the globe). The full-screen 3D view is hidden meanwhile, so it
        // pauses instead of rendering twice.
        GetTree().Root.Disable3D = true;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (IsOpen && Camera is not null)
        {
            // The preview looks exactly where the planet camera looks, so orbit/zoom still work.
            _previewCamera.GlobalTransform = Camera.GlobalTransform;
            _previewCamera.Fov = Camera.Fov;
            _previewCamera.Near = Camera.Near;
            _previewCamera.Far = Camera.Far;
        }
    }

    private void Done()
    {
        Close();
        Toolbar?.ShowInfo(Session?.Calibration is null
            ? "No calibration applied."
            : "Calibration applied. Save (Ctrl+S) to keep it in your world.");
    }

    private void Cancel()
    {
        if (_snapshot is not null)
        {
            Session?.CancelCalibration(_snapshot);
        }

        Close();
        Toolbar?.ShowInfo("Calibration changes cancelled.");
    }

    // Back to reading the map exactly as its type says (the default lines, unmoved).
    private void Reset()
    {
        Session?.SetCalibration(null);
        _canvas.SetCalibration(MapCalibration.CreateDefault());
    }

    private void AskToAdd(bool latitude)
    {
        _addingLatitude = latitude;
        _addDialog.Title = latitude ? "Add a Latitude Line" : "Add a Longitude Line";
        _addPrompt.Text = latitude
            ? "Latitude in degrees (north positive, south negative):"
            : "Longitude in degrees (east positive, west negative):";
        _addValue.MinValue = latitude ? -89.5 : -180;
        _addValue.MaxValue = latitude ? 89.5 : 179.5;
        _addValue.Value = 0;
        _addDialog.PopupCentered(new Vector2I(360, 140));
    }

    private void AddGuide()
    {
        MapCalibration current = _canvas.Calibration;
        MapCalibration updated = _addingLatitude
            ? current.AddLatitude(_addValue.Value)
            : current.AddLongitude(_addValue.Value);
        if (ReferenceEquals(updated, current))
        {
            Toolbar?.ShowWarning(
                "There's already a line there (lines must be at least half a degree apart).");
            return;
        }

        _canvas.SetCalibration(updated);
        Session?.SetCalibration(updated);
    }

    private void Close()
    {
        Visible = false;
        _snapshot = null;
        Session?.EndCalibration();
        GetTree().Root.Disable3D = false;
        SetToolbarVisible(true);

        if (Session?.Surface is not null)
        {
            Session.Surface.ShowGrid = _gridWasShown;
        }
    }

    // A separate view of the same 3D world, filling the right half of the screen. Mouse input
    // passes through it to the planet camera, which it mirrors each frame.
    private void BuildGlobePreview()
    {
        var container = new SubViewportContainer
        {
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        container.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        container.AnchorLeft = 0.5f;

        var viewport = new SubViewport { World3D = GetViewport().FindWorld3D() };
        _previewCamera = new Camera3D { Current = true };
        viewport.AddChild(_previewCamera);
        container.AddChild(viewport);
        AddChild(container);
    }

    // The toolbar's map type and fill controls must not change mid-calibration.
    private void SetToolbarVisible(bool visible)
    {
        if (Toolbar is not null)
        {
            Toolbar.Visible = visible;
        }
    }

    private static Button CreateButton(string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }
}
