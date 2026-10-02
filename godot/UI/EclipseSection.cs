using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's eclipse list (VISION.md EVT-01): the selected body's eclipses in the
/// coming year (including one under way), each with its type, date, depth, length, and a Go to
/// button that glides the clock to its peak.
/// </summary>
public partial class EclipseSection : VBoxContainer
{
    // The most eclipses listed; a system with many moons can have dozens a year.
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
        AddChild(new Label { Text = "Eclipses" });
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_status);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 8);
        AddChild(_rows);

        Session.TimeChanged += Refresh;
        Session.EclipsesReady += Refresh;
        VisibilityChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        Session.TimeChanged -= Refresh;
        Session.EclipsesReady -= Refresh;
    }

    /// <summary>Shows the selected body's coming eclipses (only while the panel shows).</summary>
    public void Refresh()
    {
        if (!IsNodeReady() || !IsVisibleInTree())
        {
            return;
        }

        Body body = Session.SelectedBody;
        IReadOnlyList<Eclipse>? coming = Session.SelectedEclipses?.Upcoming(Session.TimeDays);
        string signature = coming is null
            ? $"{body.Id}|working"
            : $"{body.Id}|" + string.Join("|", coming.Take(MaxRows).Select(e =>
                $"{e.PeakDays}:{BodyClock.Describe(body, e.PeakDays)}")) + $"|{coming.Count}";
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

        _status.Text = coming switch
        {
            null => "Working them out…",
            [] => "None in the coming year.",
            { Count: > MaxRows } => $"The next {MaxRows} of {coming.Count} in the coming year:",
            _ => "",
        };
        _status.Visible = _status.Text != "";
        foreach (Eclipse eclipse in coming?.Take(MaxRows) ?? [])
        {
            _rows.AddChild(EclipseRow(body, eclipse));
        }
    }

    private Control EclipseRow(Body body, Eclipse eclipse)
    {
        var row = new HBoxContainer();
        var text = new Label
        {
            Text = $"{EclipseText.Title(eclipse, body, Session.World.Bodies)}\n" +
                $"{BodyClock.Describe(body, eclipse.PeakDays)}\n{EclipseText.Details(eclipse)}",
            TooltipText = $"From {BodyClock.Describe(body, eclipse.StartDays)}\n" +
                $"to {BodyClock.Describe(body, eclipse.EndDays)}",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        row.AddChild(text);
        if (Time is TimeControls time)
        {
            var goTo = new Button
            {
                Text = "Go to",
                FocusMode = FocusModeEnum.None,
                TooltipText = "Run the clock to the eclipse's peak",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            double peak = eclipse.PeakDays;
            goTo.Pressed += () => time.GlideTo(peak);
            row.AddChild(goTo);
        }

        return row;
    }
}
