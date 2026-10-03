using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's meteor shower list (VISION.md EVT-02): the selected body's showers in the
/// coming year (including one under way), each with its comet, peak date, strength, length,
/// and a Go to button that glides the clock to its peak.
/// </summary>
public partial class MeteorShowerSection : VBoxContainer
{
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
        AddChild(new Label { Text = "Meteor Showers" });
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_status);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 8);
        AddChild(_rows);

        Session.TimeChanged += Refresh;
        Session.Changed += Refresh;
        Session.MeteorShowersReady += Refresh;
        VisibilityChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        Session.TimeChanged -= Refresh;
        Session.Changed -= Refresh;
        Session.MeteorShowersReady -= Refresh;
    }

    /// <summary>Shows the selected body's coming showers (only while the panel shows).</summary>
    public void Refresh()
    {
        if (!IsNodeReady() || !IsVisibleInTree())
        {
            return;
        }

        Body body = Session.SelectedBody;
        double now = Session.TimeDays;
        MeteorShowerTimeline? timeline = Session.SelectedMeteorShowers;
        List<MeteorShower>? coming = timeline?.Between(now, now + timeline.YearDays);
        IReadOnlyList<Body> bodies = Session.World.Bodies;
        string signature = coming is null
            ? $"{body.Id}|working"
            : $"{body.Id}|" + string.Join("|", coming.Select(shower =>
                $"{MeteorText.Title(shower, bodies)}:{BodyClock.Describe(body, shower.PeakDays)}"
                + $":{MeteorText.Details(shower, body)}"));
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
            [] => "None: no comet's orbit passes close to this body's year.",
            _ => "",
        };
        _status.Visible = _status.Text != "";
        foreach (MeteorShower shower in coming ?? [])
        {
            _rows.AddChild(ShowerRow(body, shower));
        }
    }

    private Control ShowerRow(Body body, MeteorShower shower)
    {
        var row = new HBoxContainer();
        var text = new Label
        {
            Text = $"{MeteorText.Title(shower, Session.World.Bodies)}\n" +
                $"{BodyClock.Describe(body, shower.PeakDays)}\n{MeteorText.Details(shower, body)}",
            TooltipText = $"From {BodyClock.Describe(body, shower.StartDays)}\n" +
                $"to {BodyClock.Describe(body, shower.EndDays)}",
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
                TooltipText = "Run the clock to the shower's peak",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            double peak = shower.PeakDays;
            goTo.Pressed += () => time.GlideTo(peak);
            row.AddChild(goTo);
        }

        return row;
    }
}
