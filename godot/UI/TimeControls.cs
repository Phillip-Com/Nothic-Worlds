using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The time bar in the bottom-right corner (VISION.md SIM-02, CAL-05; owner's choice, after
/// fantasy-calendar.com): the date on the selected body in its own calendar (or "Day 1,204"
/// without one), with a clock for the time of day and its moons' phases; clicking the date
/// opens the Calendar tab. Buttons step the clock back or forward a year, a month, a day, or an
/// hour, and Play runs it at a chosen speed. A second line shows the body's seasons and its
/// next solstice or equinox, and a third any meteor shower under way (EVT-02).
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

    // The step buttons on each side of Play, biggest outermost: their unit, label, and name.
    private static readonly (TimeUnit Unit, string Label, string Name)[] _steps =
    [
        (TimeUnit.Year, "Y", "year"),
        (TimeUnit.Month, "M", "month"),
        (TimeUnit.Day, "D", "day"),
        (TimeUnit.Hour, "H", "hour"),
    ];

    private CheckButton _physics = null!;
    private Button _playButton = null!;
    private readonly List<(Button Button, string Tip)> _monthSteps = [];
    private (double From, double To, double Progress)? _glide;
    private OptionButton _speed = null!;
    private Button _time = null!;
    private ClockFace _clock = null!;
    private HBoxContainer _moons = null!;
    private Label _seasons = null!;
    private Label _shower = null!;
    private bool _playing;

    /// <summary>The open world, whose clock this runs.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>
    /// The toolbar: the time bar hides whenever it does (calibrating, cutting), and clicking
    /// the date opens its Calendar tab.
    /// </summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    public override void _Ready()
    {
        // The date and seasons sit in their own panel above the buttons, so long calendar
        // dates never widen the button row into the camera text at the bottom-left.
        var stack = new VBoxContainer();
        stack.SetAnchorsAndOffsetsPreset(
            Control.LayoutPreset.BottomRight, Control.LayoutPresetMode.Minsize, ScreenMargin);
        stack.GrowHorizontal = Control.GrowDirection.Begin;
        stack.GrowVertical = Control.GrowDirection.Begin;
        AddChild(stack);

        var info = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        stack.AddChild(info);
        var infoRows = new VBoxContainer();
        info.AddChild(infoRows);
        // The date, with a clock for the time of day and the moons as they look tonight.
        var dateRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _clock = new ClockFace(22)
        {
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            TooltipText = "The time of day: noon at the top, midnight at the bottom",
        };
        dateRow.AddChild(_clock);
        _moons = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        dateRow.AddChild(_moons);
        _time = new Button
        {
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            Alignment = HorizontalAlignment.Right,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = "The date on the selected body, in its calendar (or counted in its " +
                "own days). Click to open the calendar",
        };
        _time.AddThemeFontSizeOverride("font_size", 17);
        _time.Pressed += () => Toolbar?.ShowCalendar();
        dateRow.AddChild(_time);
        infoRows.AddChild(dateRow);
        _seasons = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        infoRows.AddChild(_seasons);
        _shower = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Pass,
            Visible = false,
        };
        infoRows.AddChild(_shower);

        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        stack.AddChild(panel);
        var row = new HBoxContainer();
        panel.AddChild(row);

        // Back a year, month, day, hour; Play and its speed; on an hour, day, month, year.
        foreach ((TimeUnit unit, string label, string name) in _steps)
        {
            Button back = CreateButton($"‹ {label}", () => Step(unit, -1),
                $"Back a {name} (the bodies glide into place)");
            row.AddChild(back);
            if (unit == TimeUnit.Month)
            {
                _monthSteps.Add((back, back.TooltipText));
            }
        }

        _playButton = CreateButton(
            "Play", TogglePlaying, "Run the world clock: every body moves and spins");
        row.AddChild(_playButton);

        _speed = new Dropdown { FocusMode = Control.FocusModeEnum.None };
        foreach ((string label, _) in _speeds)
        {
            _speed.AddItem(label);
        }

        _speed.Select(1);
        _speed.TooltipText = "How fast time passes while playing";
        row.AddChild(_speed);

        foreach ((TimeUnit unit, string label, string name) in Enumerable.Reverse(_steps))
        {
            Button on = CreateButton($"{label} ›", () => Step(unit, +1),
                $"On a {name} (the bodies glide into place)");
            row.AddChild(on);
            if (unit == TimeUnit.Month)
            {
                _monthSteps.Add((on, on.TooltipText));
            }
        }

        // A switch, so it's plain whether it's on.
        _physics = new CheckButton
        {
            Text = "Physics",
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Move the bodies by real gravity from now on, starting each at its " +
                "orbit's natural speed (seasons, calendars, and weather stay on the designed " +
                "orbits). Off goes back to the design",
        };
        _physics.Pressed += TogglePhysics;
        row.AddChild(_physics);

        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += UpdateVisibility;
        }

        if (Session is null)
        {
            GD.PushError("TimeControls needs a world session.");
            return;
        }

        Session.TimeChanged += ShowTime;
        Session.SelectionChanged += ShowTime;
        Session.Changed += ShowTime;  // Edits can move the seasons (tilt, orbit, calendar).
        Session.MeteorShowersReady += ShowTime;
        Session.WorldClosed += _ =>
        {
            SetPlaying(false);
            _glide = null;
        };
        Session.Physics.StateChanged += () => _physics.SetPressedNoSignal(Session.Physics.IsOn);
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

    /// <summary>
    /// Whether the time bar stays shown while the toolbar is hidden (standing on a world, where
    /// time runs as from orbit; VISION.md REN-08).
    /// </summary>
    public bool KeepShown
    {
        get => _keepShown;
        set
        {
            _keepShown = value;
            UpdateVisibility();
        }
    }

    private bool _keepShown;

    private void UpdateVisibility()
    {
        Visible = (Toolbar?.Visible ?? true) || _keepShown;
    }

    private void TogglePlaying()
    {
        _glide = null;
        SetPlaying(!_playing);
    }

    // Moves the clock a step back (-1) or forward (+1), measured on the selected body (a
    // month or year is the same day of the month in its calendar). Clicking again mid-glide
    // adds on to where the glide is heading.
    private void Step(TimeUnit unit, int direction)
    {
        if (Session is null)
        {
            return;
        }

        double from = _glide?.To ?? Session.TimeDays;
        if (TimeSteps.Apply(Session.World.Bodies, Session.SelectedBody, from, unit, direction)
            is double target)
        {
            GlideTo(target);
        }
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
        LocalTime local = BodyClock.LocalTimeOn(body, now);
        _clock.DayFraction = (local.Hour + local.Minute / 60.0) / body.DayLengthHours;
        _clock.Visible = body.Kind != BodyKind.WorldTree;
        ShowMoons(body, now);
        foreach ((Button month, string tip) in _monthSteps)
        {
            DisabledTip.Apply(month, tip, body.Calendar is null
                    ? $"{body.Name} has no calendar, so no months: give it one in the Calendar " +
                        "tab (Edit Calendar…)"
                    : null);
        }

        SeasonTimeline seasons = Session.SelectedSeasons;
        if (seasons.SeasonAt(now) is { } current && seasons.NextEvent(now) is SeasonEvent next)
        {
            _seasons.Text = $"{SeasonText.Current(current)}  ·  " +
                $"{SeasonText.Name(next.Kind)} {SeasonText.HowFar(body, now, next.TimeDays)}";
            _seasons.TooltipText = $"{SeasonText.Explanation(body)}\n" +
                $"Next: {SeasonText.Name(next.Kind)}, " +
                $"{BodyClock.Describe(body, next.TimeDays)} ({SeasonText.Note(next.Kind)}).";
            _seasons.Visible = true;
        }
        else
        {
            _seasons.Visible = false;
        }

        ShowShower(body, now);
    }

    // The selected body's moons as they look now, in the time bar.
    private void ShowMoons(Body body, double now)
    {
        IReadOnlyList<MoonPhase> phases = MoonPhase.Of(Session!.World.Bodies, body, now);
        while (_moons.GetChildCount() > phases.Count)
        {
            Node extra = _moons.GetChild(_moons.GetChildCount() - 1);
            _moons.RemoveChild(extra);
            extra.QueueFree();
        }

        while (_moons.GetChildCount() < phases.Count)
        {
            _moons.AddChild(new MoonIcon(18));
        }

        for (int i = 0; i < phases.Count; i++)
        {
            _moons.GetChild<MoonIcon>(i).Phase = phases[i];
        }
    }

    private void ShowShower(Body body, double now)
    {
        if (!body.HasSurface || Session!.SelectedMeteorShowers?.ActiveAt(now) is not { } shower)
        {
            _shower.Visible = false;
            return;
        }

        _shower.Text = MeteorText.UnderWay(shower, body, Session.World.Bodies, now);
        _shower.TooltipText = $"A meteor shower: {body.Name} is passing through the dust " +
            $"along a comet's orbit.\nPeak: {BodyClock.Describe(body, shower.PeakDays)}, " +
            $"{MeteorText.Details(shower, body).ToLowerInvariant()}.";
        _shower.Visible = true;
    }

    // Physics mode on or off (VISION.md SIM-03).
    private void TogglePhysics()
    {
        if (Session is null)
        {
            return;
        }

        if (!_physics.ButtonPressed)
        {
            Session.Physics.Stop();
            return;
        }

        try
        {
            Session.Physics.Start(Session.World.Bodies, Session.TimeDays);
        }
        catch (ArgumentException error)
        {
            _physics.SetPressedNoSignal(false);
            Toolbar?.ShowError($"Couldn't start physics: {error.Message}");
        }
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
