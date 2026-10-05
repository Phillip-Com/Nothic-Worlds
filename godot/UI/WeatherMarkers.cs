using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Weather pins on the globes (VISION.md WTH-01; owner's choices: named, saved, their own icon,
/// added with a toolbar button then a click on the spot): a small sun with the pin's name, on
/// planets and moons drawn large enough to point at a spot. Clicking one opens its weather
/// (<see cref="WeatherWindow"/>). Hidden along with the other pins by the Pins toggle.
/// </summary>
/// <remarks>
/// Must come after the journal pins in the scene, so a click on a weather pin is taken here
/// first.
/// </remarks>
public partial class WeatherMarkers : CanvasLayer
{
    private const float SunRadius = 6.0f;
    private const float MinGlobePixels = 24.0f;
    private const float GrabPixels = 11.0f;
    private const float ClickSlopPixels = 5.0f;

    private static readonly Color _sunColor = new(1.0f, 0.82f, 0.3f);
    private static readonly Color _labelColor = new(1.0f, 0.93f, 0.75f);

    // Where each pin was drawn this frame, for clicks.
    private readonly List<(Vector2 At, Guid PinId)> _drawn = [];

    private Control _overlay = null!;
    private WeatherWindow _window = null!;
    private ConfirmationDialog _nameDialog = null!;
    private LineEdit _nameField = null!;
    private (Guid BodyId, GeoCoordinate Spot)? _naming;
    private (Vector2 At, Guid PinId)? _pressed;
    private bool _shown = true;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the globes.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The camera, for projecting pins onto the screen.</summary>
    [Export] public PlanetCamera? Camera { get; set; }

    /// <summary>The toolbar: pins hide whenever it does, and messages go to it.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>Places a new pin by a click on the globe.</summary>
    [Export] public PinPlacer? Placer { get; set; }

    /// <summary>The live weather, for a pin's weather right now (VISION.md WTH-02).</summary>
    [Export] public WeatherDisplay? LiveWeather { get; set; }

    /// <summary>Whether weather pins are shown (the toolbar's Pins toggle).</summary>
    public bool ShowPins
    {
        get => _shown;
        set
        {
            _shown = value;
            _overlay.QueueRedraw();
        }
    }

    public override void _Ready()
    {
        Layer = 0;
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Draw += DrawPins;
        AddChild(_overlay);
        if (Session is null || System is null || Camera is null)
        {
            GD.PushError("WeatherMarkers needs a world session, system view, and camera.");
            SetProcessUnhandledInput(false);
            return;
        }

        _window = new WeatherWindow
        {
            Session = Session,
            Camera = Camera,
            LiveWeather = LiveWeather,
        };
        AddChild(_window);
        BuildNameDialog();

        // Pins move with the view, so redraw as it moves (only if there are any).
        System.Placed += () =>
        {
            if (Session.World.WeatherPins.Count > 0)
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

    /// <summary>
    /// Starts adding a weather pin on the selected planet or moon: a click on the spot, then
    /// its name.
    /// </summary>
    public void StartAdding()
    {
        if (Session?.SelectedBody is not { HasSurface: true } body || Placer is null)
        {
            Toolbar?.ShowWarning(
                "Select a planet or moon first: stars and comets have no weather.");
            return;
        }

        Placer.Start(body.Id, spot =>
        {
            _naming = (body.Id, spot);
            _nameField.Text = $"Weather pin {Session.World.WeatherPins.Count + 1}";
            _nameDialog.PopupCentered();
            _nameField.GrabFocus();
            _nameField.SelectAll();
        });
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !_shown
            || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        if (click.Pressed)
        {
            _pressed = PinAt(click.Position) is Guid pin ? (click.Position, pin) : null;
            if (_pressed is not null)
            {
                GetViewport().SetInputAsHandled();  // Not a camera drag or a body click.
            }

            return;
        }

        if (_pressed is (Vector2 start, Guid pinId))
        {
            _pressed = null;
            GetViewport().SetInputAsHandled();
            if (start.DistanceTo(click.Position) <= ClickSlopPixels)
            {
                _window.Open(pinId);
            }
        }
    }

    private void BuildNameDialog()
    {
        _nameField = new LineEdit { CustomMinimumSize = new Vector2(280, 0) };
        _nameField.TextSubmitted += _ =>
        {
            _nameDialog.Hide();
            AddNamedPin();
        };
        var layout = new VBoxContainer();
        layout.AddChild(new Label { Text = "Name the weather pin:" });
        layout.AddChild(_nameField);
        _nameDialog = new ConfirmationDialog { Title = "New Weather Pin", OkButtonText = "Add" };
        _nameDialog.AddChild(layout);
        _nameDialog.Confirmed += AddNamedPin;
        _nameDialog.Canceled += () => _naming = null;
        AddChild(_nameDialog);
    }

    private void AddNamedPin()
    {
        if (_naming is not (Guid bodyId, GeoCoordinate spot) || Session is null)
        {
            return;
        }

        _naming = null;
        (WeatherPin? pin, string? problem) = Session.AddWeatherPin(bodyId, spot, _nameField.Text);
        if (pin is null)
        {
            Toolbar?.ShowWarning($"Couldn't add the weather pin: {problem}.");
            return;
        }

        _window.Open(pin.Id);
    }

    private void DrawPins()
    {
        _drawn.Clear();
        if (!Visible || !_shown || Session is null)
        {
            return;
        }

        Font font = _overlay.GetThemeDefaultFont();
        foreach (WeatherPin pin in Session.World.WeatherPins)
        {
            if (ScreenPositionOf(pin) is not Vector2 at)
            {
                continue;
            }

            DrawSun(at);
            Vector2 label = at + new Vector2(SunRadius + 6, 5);
            _overlay.DrawString(font, label + Vector2.One, pin.Name, modulate: Colors.Black);
            _overlay.DrawString(font, label, pin.Name, modulate: _labelColor);
            _drawn.Add((at, pin.Id));
        }
    }

    // A small sun: a disk with eight rays.
    private void DrawSun(Vector2 at)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 ray = Vector2.Right.Rotated(Mathf.Tau * i / 8);
            _overlay.DrawLine(at + ray * (SunRadius + 2), at + ray * (SunRadius + 5),
                Colors.Black, 3);
            _overlay.DrawLine(at + ray * (SunRadius + 2), at + ray * (SunRadius + 5),
                _sunColor, 1.5f);
        }

        _overlay.DrawCircle(at, SunRadius + 1.5f, Colors.Black);
        _overlay.DrawCircle(at, SunRadius, _sunColor);
    }

    private Guid? PinAt(Vector2 position)
    {
        foreach ((Vector2 at, Guid pinId) in _drawn)
        {
            if (at.DistanceTo(position) <= GrabPixels)
            {
                return pinId;
            }
        }

        return null;
    }

    // Where a pin is on screen, or null if it can't be seen there.
    private Vector2? ScreenPositionOf(WeatherPin pin)
    {
        if (System!.SurfaceFor(pin.BodyId) is not PlanetSurface globe
            || Camera!.IsPositionBehind(globe.GlobalPosition))
        {
            return null;
        }

        Vector2 center = Camera.UnprojectPosition(globe.GlobalPosition);
        Vector3 edge = globe.GlobalPosition
            + Camera.GlobalBasis.X * globe.GlobalTransform.Basis.X.Length();
        if (center.DistanceTo(Camera.UnprojectPosition(edge)) < MinGlobePixels)
        {
            return null;
        }

        Vector2? at = GlobePicker.ScreenPositionOf(Camera, globe, pin.Spot);
        return at is Vector2 spot && _overlay.GetRect().Grow(20).HasPoint(spot) ? spot : null;
    }
}
