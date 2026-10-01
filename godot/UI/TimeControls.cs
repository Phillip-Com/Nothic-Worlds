using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Rendering;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The time bar in the bottom-right corner (VISION.md SIM-02, REN-02, CAL-01, CAL-03): play or
/// pause the world clock, pick how fast it runs, step it back or forward by a chosen amount, see
/// the date on the selected body (in its calendar, or "Day 1,204, 14:30" in its own days
/// without one), jump to a date, and switch the system view between readable and true scale.
/// A second line shows the body's seasons and its next solstice or equinox.
/// </summary>
/// <remarks>
/// Steps and jumps glide: the clock runs quickly to the new time (owner's request), so bodies
/// sweep along their real orbits into place instead of popping there.
/// </remarks>
public partial class TimeControls : CanvasLayer
{
    private const int ScreenMargin = 12;

    // How long a step or jump takes to glide into place.
    private const double GlideSeconds = 0.8;

    // How many standard days pass per real second at each speed.
    private static readonly (string Label, double DaysPerSecond)[] _speeds =
    [
        ("1 hour / second", 1.0 / 24.0),
        ("1 day / second", 1.0),
        ("1 week / second", 7.0),
        ("1 month / second", 30.0),
        ("1 year / second", 365.25),
    ];

    // The step sizes, measured on the selected body (see StepDays).
    private static readonly string[] _stepLabels =
        ["1 hour", "1 day", "1 week", "30 days", "1 year"];

    private Button _playButton = null!;
    private OptionButton _step = null!;
    private (double From, double To, double Progress)? _glide;
    private OptionButton _speed = null!;
    private Label _time = null!;
    private Label _seasons = null!;
    private ConfirmationDialog _goToDialog = null!;
    private SpinBox _goToYear = null!;
    private OptionButton _goToMonth = null!;
    private SpinBox _goToDay = null!;
    private SpinBox _goToHour = null!;
    private Control[] _calendarOnly = [];
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

        var rows = new VBoxContainer();
        panel.AddChild(rows);
        var row = new HBoxContainer();
        rows.AddChild(row);
        _seasons = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Pass,
            TooltipText = "The selected body's seasons in each hemisphere, from where its " +
                "star stands, and its next solstice or equinox",
        };
        rows.AddChild(_seasons);

        _playButton = CreateButton(
            "Play", TogglePlaying, "Run the world clock: every body moves and spins");
        row.AddChild(_playButton);

        _speed = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach ((string label, _) in _speeds)
        {
            _speed.AddItem(label);
        }

        _speed.Select(1);
        _speed.TooltipText = "How fast time passes while playing";
        row.AddChild(_speed);

        row.AddChild(CreateButton("−", () => Step(-1),
            "Step the clock back (the bodies glide into place)"));
        _step = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (string label in _stepLabels)
        {
            _step.AddItem(label);
        }

        _step.Select(1);
        _step.TooltipText = "How far each step goes, on the selected body: days and weeks in " +
            "its own day length; a year is one trip around its star (its planet's, for a moon)";
        row.AddChild(_step);
        row.AddChild(CreateButton("+", () => Step(+1),
            "Step the clock forward (the bodies glide into place)"));

        _time = new Label { CustomMinimumSize = new Vector2(220, 0) };
        _time.TooltipText = "The date on the selected body, counted in its own days (in its " +
            "calendar, if it has one)";
        row.AddChild(_time);

        row.AddChild(CreateButton("Go to…", AskForDate, "Jump to a date and hour"));

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
        Session.Changed += ShowTime;  // Edits can move the seasons (tilt, orbit, calendar).
        Session.WorldClosed += _ =>
        {
            SetPlaying(false);
            _glide = null;
        };
        ShowTime();
    }

    public override void _Process(double delta)
    {
        if (Session is null)
        {
            return;
        }

        if (_glide is (double from, double to, double progress))
        {
            progress = Math.Min(1.0, progress + delta / GlideSeconds);
            double eased = progress * progress * (3 - 2 * progress);  // Smooth start and stop
            Session.SetTime(from + (to - from) * eased);
            _glide = progress < 1.0 ? (from, to, progress) : null;
        }
        else if (_playing && !Session.IsBusy)
        {
            Session.SetTime(Session.TimeDays + delta * _speeds[_speed.Selected].DaysPerSecond);
        }
    }

    private void TogglePlaying()
    {
        _glide = null;
        SetPlaying(!_playing);
    }

    // Moves the clock by one step back (-1) or forward (+1). Clicking again mid-glide adds on
    // to where the glide is heading.
    private void Step(int direction)
    {
        if (Session is null)
        {
            return;
        }

        double target = (_glide?.To ?? Session.TimeDays) + direction * StepDays();
        GlideTo(target);
    }

    // The chosen step in standard days, measured on the selected body.
    private double StepDays()
    {
        Body body = Session!.SelectedBody;
        double day = body.DayLengthHours / 24.0;
        return _step.Selected switch
        {
            0 => 1.0 / 24.0,
            1 => day,
            2 => 7 * day,
            3 => 30 * day,
            _ => BodyClock.YearDays(Session.World.Bodies, body),
        };
    }

    /// <summary>
    /// Runs the clock smoothly to a time (standard days); playing stops so the bodies settle
    /// there.
    /// </summary>
    public void GlideTo(double timeDays)
    {
        if (Session is null || !double.IsFinite(timeDays))
        {
            return;
        }

        SetPlaying(false);
        _glide = (Session.TimeDays, timeDays, 0.0);
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
        double now = Session.TimeDays;
        _time.Text = $"{body.Name}: {BodyClock.Describe(body, now)}";

        SeasonTimeline seasons = Session.SelectedSeasons;
        if (seasons.SeasonAt(now) is { } current && seasons.NextEvent(now) is SeasonEvent next)
        {
            _seasons.Text = $"{SeasonText.Current(current)}  ·  Next: " +
                $"{SeasonText.Name(next.Kind)}, {BodyClock.Describe(body, next.TimeDays)} " +
                $"({SeasonText.HowFar(body, now, next.TimeDays)})";
            _seasons.Visible = true;
        }
        else
        {
            _seasons.Visible = false;
        }
    }

    private void AskForDate()
    {
        if (Session is null)
        {
            return;
        }

        Body body = Session.SelectedBody;
        LocalTime now = BodyClock.LocalTimeOn(body, Session.TimeDays);
        foreach (Control field in _calendarOnly)
        {
            field.Visible = body.Calendar is not null;
        }

        if (body.Calendar is Calendar calendar)
        {
            CalendarDate date = CalendarMath.DateOf(calendar, now.Day - 1);
            _goToMonth.Clear();
            foreach (CalendarMonth month in calendar.Months)
            {
                _goToMonth.AddItem(month.Name);
            }

            _goToYear.Value = date.Year;
            _goToMonth.Select(date.Month);
            ShowMonthDays();
            _goToDay.Value = date.Day;
        }
        else
        {
            _goToDay.MinValue = -1e9;
            _goToDay.MaxValue = 1e9;
            _goToDay.Value = now.Day;
        }

        _goToDialog.Title = $"Go to a Date on {body.Name}";
        _goToHour.MaxValue = Math.Max(0, body.DayLengthHours - 0.01);
        _goToHour.Value = now.Hour + now.Minute / 60.0;
        _goToDialog.ResetSize();
        _goToDialog.PopupCentered(new Vector2I(340, 0));
    }

    // Limits the day field to the chosen month's days.
    private void ShowMonthDays()
    {
        if (Session?.SelectedBody.Calendar is Calendar calendar && _goToMonth.Selected >= 0)
        {
            _goToDay.MinValue = 1;
            _goToDay.MaxValue = calendar.Months[_goToMonth.Selected].Days;
        }
    }

    private void GoToDate()
    {
        if (Session is null)
        {
            return;
        }

        // Day index 0 is the day at time 0 (day 1, or the calendar's start date).
        Body body = Session.SelectedBody;
        long dayIndex = body.Calendar is Calendar calendar
            ? CalendarMath.DayIndexOf(calendar, (long)_goToYear.Value,
                Math.Max(0, _goToMonth.Selected), (int)_goToDay.Value)
            : (long)_goToDay.Value - 1;
        GlideTo(BodyClock.TimeAt(body, dayIndex, _goToHour.Value));
    }

    private void BuildGoToDialog()
    {
        _goToYear = new SpinBox
        {
            MinValue = -1e12,
            MaxValue = 1e12,
            Step = 1,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        }.WithArrowKeys();
        _goToMonth = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _goToMonth.ItemSelected += _ => ShowMonthDays();
        _goToDay = new SpinBox
        {
            Step = 1,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        }.WithArrowKeys();
        _goToHour = new SpinBox
        {
            MinValue = 0,
            Step = 0.25,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        }.WithArrowKeys();
        var grid = new GridContainer { Columns = 2 };
        Label yearLabel = AddRow(grid, "Year", _goToYear);
        Label monthLabel = AddRow(grid, "Month", _goToMonth);
        AddRow(grid, "Day", _goToDay);
        AddRow(grid, "Hour", _goToHour);
        _calendarOnly = [yearLabel, _goToYear, monthLabel, _goToMonth];
        _goToDialog = new ConfirmationDialog { OkButtonText = "Go" };
        _goToDialog.AddChild(grid);
        _goToDialog.Confirmed += GoToDate;
        AddChild(_goToDialog);
    }

    private static Label AddRow(GridContainer grid, string text, Control field)
    {
        var label = new Label { Text = text };
        grid.AddChild(label);
        grid.AddChild(field);
        return label;
    }

    private static Button CreateButton(string text, Action pressed, string tooltip)
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
}
