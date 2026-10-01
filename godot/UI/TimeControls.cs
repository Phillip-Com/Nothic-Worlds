using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The time bar in the bottom-right corner (VISION.md SIM-02, REN-02): play or pause the world
/// clock, pick how fast it runs, see the date on the selected body ("Day 1,204, 14:30" in that
/// body's own days, owner's choice until calendars exist), jump to a day, and switch the system
/// view between readable and true scale.
/// </summary>
public partial class TimeControls : CanvasLayer
{
    private const int ScreenMargin = 12;

    // How many standard days pass per real second at each speed.
    private static readonly (string Label, double DaysPerSecond)[] _speeds =
    [
        ("1 hour / second", 1.0 / 24.0),
        ("1 day / second", 1.0),
        ("1 week / second", 7.0),
        ("1 month / second", 30.0),
        ("1 year / second", 365.25),
    ];

    private Button _playButton = null!;
    private OptionButton _speed = null!;
    private Label _time = null!;
    private ConfirmationDialog _goToDialog = null!;
    private SpinBox _goToDay = null!;
    private SpinBox _goToHour = null!;
    private bool _playing;

    /// <summary>The open world, whose clock this runs.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The system view, for the true-scale switch.</summary>
    [Export] public SystemView? System { get; set; }

    /// <summary>The toolbar: the time bar hides whenever it does (calibrating, cutting).</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    public override void _Ready()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.BottomRight, Control.LayoutPresetMode.Minsize, ScreenMargin);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.GrowVertical = Control.GrowDirection.Begin;
        AddChild(panel);

        var row = new HBoxContainer();
        panel.AddChild(row);

        _playButton = CreateButton("Play", TogglePlaying);
        _playButton.TooltipText = "Run the world clock: every body moves and spins";
        row.AddChild(_playButton);

        _speed = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach ((string label, _) in _speeds)
        {
            _speed.AddItem(label);
        }

        _speed.Select(1);
        _speed.TooltipText = "How fast time passes while playing";
        row.AddChild(_speed);

        _time = new Label { CustomMinimumSize = new Vector2(260, 0) };
        _time.TooltipText = "The date on the selected body, counted in its own days";
        row.AddChild(_time);

        row.AddChild(CreateButton("Go to…", AskForDay));

        var trueScale = new CheckButton
        {
            Text = "True scale",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Show real sizes and distances (most bodies become tiny dots). " +
                "Off: a readable view with distances compressed and small bodies enlarged.",
        };
        trueScale.Toggled += on =>
        {
            if (System is not null)
            {
                System.DisplayScale = on ? SystemScale.True : SystemScale.Readable;
            }
        };
        row.AddChild(trueScale);

        BuildGoToDialog();

        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += () => Visible = Toolbar.Visible;
        }

        if (Session is null)
        {
            GD.PushError("TimeControls needs a world session.");
            return;
        }

        Session.TimeChanged += ShowTime;
        Session.SelectionChanged += ShowTime;
        Session.WorldClosed += _ => SetPlaying(false);
        ShowTime();
    }

    public override void _Process(double delta)
    {
        if (_playing && Session is { IsBusy: false })
        {
            Session.SetTime(Session.TimeDays + delta * _speeds[_speed.Selected].DaysPerSecond);
        }
    }

    private void TogglePlaying()
    {
        SetPlaying(!_playing);
    }

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        _playButton.Text = playing ? "Pause" : "Play";
    }

    private void ShowTime()
    {
        if (Session is null)
        {
            return;
        }

        Body body = Session.SelectedBody;
        _time.Text = $"{body.Name}: {BodyClock.LocalTimeOn(body, Session.TimeDays)}";
    }

    private void AskForDay()
    {
        if (Session is null)
        {
            return;
        }

        Body body = Session.SelectedBody;
        LocalTime now = BodyClock.LocalTimeOn(body, Session.TimeDays);
        _goToDialog.Title = $"Go to a Day on {body.Name}";
        _goToDay.Value = now.Day;
        _goToHour.MaxValue = Math.Max(0, body.DayLengthHours - 0.01);
        _goToHour.Value = now.Hour + now.Minute / 60.0;
        _goToDialog.PopupCentered(new Vector2I(340, 160));
    }

    private void GoToDay()
    {
        if (Session is null)
        {
            return;
        }

        // Day 1 starts at time 0; hours are standard hours into the body's day.
        double dayLength = Session.SelectedBody.DayLengthHours;
        double hours = (_goToDay.Value - 1) * dayLength + _goToHour.Value;
        Session.SetTime(hours / 24.0);
    }

    private void BuildGoToDialog()
    {
        _goToDay = new SpinBox
        {
            MinValue = -1e9,
            MaxValue = 1e9,
            Step = 1,
            AllowGreater = true,
            AllowLesser = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _goToHour = new SpinBox
        {
            MinValue = 0,
            Step = 0.25,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var grid = new GridContainer { Columns = 2 };
        grid.AddChild(new Label { Text = "Day" });
        grid.AddChild(_goToDay);
        grid.AddChild(new Label { Text = "Hour" });
        grid.AddChild(_goToHour);
        _goToDialog = new ConfirmationDialog { OkButtonText = "Go" };
        _goToDialog.AddChild(grid);
        _goToDialog.Confirmed += GoToDay;
        AddChild(_goToDialog);
    }

    private static Button CreateButton(string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }
}
