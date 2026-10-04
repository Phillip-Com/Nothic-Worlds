using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's asteroid events (VISION.md EVT-02; owner's choice: worked out from the
/// belts, and impacts only listed): the selected body's close passes and impacts in the coming
/// year, each with its date, the asteroid's size, how close it comes or where it hits, and Go
/// to (plus Zoom to for an impact's spot).
/// </summary>
public partial class AsteroidEventsSection : VBoxContainer
{
    // The most events listed; a body deep in a dense belt can have dozens a year.
    private const int MaxRows = 12;

    private Label _status = null!;
    private VBoxContainer _rows = null!;

    // What the rows show, to skip rebuilding them while nothing they show has changed.
    private string _signature = "";

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>The time bar, for gliding to a date (no Go to buttons without it).</summary>
    public TimeControls? Time { get; init; }

    public override void _Ready()
    {
        AddChild(new Label { Text = "Asteroid Events" });
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_status);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 8);
        AddChild(_rows);

        Session.TimeChanged += Refresh;
        Session.Changed += Refresh;
        VisibilityChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        Session.TimeChanged -= Refresh;
        Session.Changed -= Refresh;
    }

    /// <summary>Shows the selected body's coming events (only while the panel shows).</summary>
    public void Refresh()
    {
        if (!IsNodeReady() || !IsVisibleInTree())
        {
            return;
        }

        Body body = Session.SelectedBody;
        double now = Session.TimeDays;
        IReadOnlyList<Body> bodies = Session.World.Bodies;
        List<AsteroidEvent> coming = AsteroidEvents.Between(
            bodies, body, now, now + BodyClock.YearDays(bodies, body));
        string signature = $"{body.Id}|" + string.Join("|", coming.Take(MaxRows).Select(e =>
            $"{BodyClock.Describe(body, e.TimeDays)}:{Details(e)}")) + $"|{coming.Count}";
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        foreach (Node row in _rows.GetChildren())
        {
            _rows.RemoveChild(row);
            row.QueueFree();
        }

        _status.Text = coming.Count switch
        {
            0 when !bodies.Any(b => b.Belts.Count > 0) =>
                "None: there are no asteroid belts (select a star to add one).",
            0 => "None in the coming year.",
            > MaxRows => $"The next {MaxRows} of {coming.Count} in the coming year:",
            _ => "",
        };
        _status.Visible = _status.Text != "";
        foreach (AsteroidEvent asteroid in coming.Take(MaxRows))
        {
            _rows.AddChild(EventRow(body, asteroid, bodies));
        }
    }

    private Control EventRow(Body body, AsteroidEvent asteroid, IReadOnlyList<Body> bodies)
    {
        var row = new HBoxContainer();
        bool impact = asteroid.Kind == AsteroidEventKind.Impact;
        row.AddChild(new Label
        {
            Text = $"{(impact ? "Impact" : "Close pass")}\n" +
                $"{BodyClock.Describe(body, asteroid.TimeDays)}\n{Details(asteroid)}",
            TooltipText = $"An asteroid from {BeltName(asteroid, bodies)}",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var buttons = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        if (Time is TimeControls time)
        {
            double when = asteroid.TimeDays;
            buttons.AddChild(Button("Go to", "Run the clock to the moment", () =>
                time.GlideTo(when)));
        }

        if (asteroid.Spot is GeoCoordinate spot && Session.Camera is PlanetCamera camera)
        {
            buttons.AddChild(Button("Zoom to", "Glide down to where it hits", () =>
                _ = ZoomTo.PinAsync(Session, camera, body.Id, spot)));
        }

        row.AddChild(buttons);
        return row;
    }

    // "A 340 m asteroid, 1,234,000 km away", or "A 120 m asteroid hits at 23.4° N, 45.1° E".
    private static string Details(AsteroidEvent asteroid)
    {
        string size = asteroid.SizeMeters < 1000
            ? $"{asteroid.SizeMeters:N0} m"
            : $"{asteroid.SizeMeters / 1000:0.#} km";
        return asteroid.Spot is GeoCoordinate spot
            ? $"A {size} asteroid hits at {PlaceText.Describe(spot)}"
            : $"A {size} asteroid, {Rounded(asteroid.DistanceKm):N0} km away";
    }

    // Three significant figures: the distances are rolled by chance, so more would be false
    // precision (e.g. 1,830,000 km, not 1,827,643).
    private static double Rounded(double km)
    {
        double unit = Math.Pow(10, Math.Max(0, Math.Floor(Math.Log10(km)) - 2));
        return Math.Round(km / unit) * unit;
    }

    private static string BeltName(AsteroidEvent asteroid, IReadOnlyList<Body> bodies) =>
        bodies.SelectMany(b => b.Belts).FirstOrDefault(b => b.Id == asteroid.BeltId)?.Name
            ?? "a belt";

    private static Button Button(string text, string tooltip, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = FocusModeEnum.None,
            TooltipText = tooltip,
        };
        button.Pressed += pressed;
        return button;
    }
}
