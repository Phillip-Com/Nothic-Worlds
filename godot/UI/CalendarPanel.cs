using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Interop;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The Calendar tab (VISION.md CAL-05; owner's choice, after fantasy-calendar.com): the
/// selected body's time as a month or a whole year of its calendar, each day with its moons'
/// phases and what happens on it (seasons, eclipses, meteor showers, the world's events), or as
/// the timeline of events. Click a day to run the clock there, double-click it to add an event.
/// It sits along the bottom of the screen, above the time bar, with the globe still in view.
/// </summary>
public partial class CalendarPanel : CanvasLayer
{
    /// <summary>The ways the calendar can be shown.</summary>
    public enum View
    {
        /// <summary>One month, a row per week.</summary>
        Month,

        /// <summary>Every month of a year, small.</summary>
        Year,

        /// <summary>The timeline of events (<see cref="TimelineStrip"/>).</summary>
        Timeline,
    }

    private const int ScreenMargin = 12;
    private const int BottomOffset = 112;  // Above the time bar, as the timeline strip was

    // Columns for a calendar without weeks, which is laid out in rows of ten days.
    private const int ColumnsWithoutWeeks = 10;

    private static readonly Color _seasonColor = new(0.55f, 0.80f, 1.0f);
    private static readonly Color _eclipseColor = new(1.0f, 0.65f, 0.30f);
    private static readonly Color _showerColor = new(0.80f, 0.65f, 1.0f);

    private readonly Dictionary<View, Button> _viewButtons = [];
    private PanelContainer _panel = null!;
    private Label _title = null!;
    private HBoxContainer _nav = null!;
    private Control _monthView = null!;
    private GridContainer _weekdays = null!;
    private GridContainer _days = null!;
    private ScrollContainer _yearView = null!;
    private HFlowContainer _yearMonths = null!;
    private VBoxContainer _noCalendar = null!;
    private Label _noCalendarText = null!;
    private MarginContainer _timelineHost = null!;
    private Button _nextSolar = null!;
    private Button _nextLunar = null!;
    private CalendarDialog _calendarDialog = null!;
    private bool _open;
    private View _view = View.Month;
    private MonthPage? _page;      // The month (or, in the year view, a month of the year) shown
    private bool _followNow = true; // The page keeps up with the clock until moved by hand
    private string _shownSignature = "";
    private readonly Dictionary<long, PanelContainer> _cells = [];
    private long? _todayCell;

    /// <summary>The open world.</summary>
    [Export] public WorldSession? Session { get; set; }

    /// <summary>The toolbar: the panel hides whenever it does, and shows its hint.</summary>
    [Export] public MapToolbar? Toolbar { get; set; }

    /// <summary>The time bar, which runs the clock to a chosen day.</summary>
    [Export] public TimeControls? Time { get; set; }

    /// <summary>The timeline strip, shown inside the panel as its Timeline view.</summary>
    [Export] public TimelineStrip? Timeline { get; set; }

    /// <summary>The System panel, on the left: the calendar leaves room for it.</summary>
    [Export] public SystemPanel? SystemPanel { get; set; }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        // Just right of the System panel's drawn edge (it can be wider than it asks to be).
        float left = SystemPanel is { IsPanelOpen: true } && SystemPanel.GetChildCount() > 0
            && SystemPanel.GetChild(0) is Control side
            ? side.GetGlobalRect().End.X + ScreenMargin
            : ScreenMargin;
        if (_panel.OffsetLeft != left)
        {
            _panel.OffsetLeft = left;
        }
    }

    /// <summary>Whether the panel is shown (it's still hidden while the toolbar is).</summary>
    public bool IsPanelOpen
    {
        get => _open;
        set
        {
            _open = value;
            if (value)
            {
                _followNow = true;
                Refresh(force: true);
            }

            UpdateVisibility();
        }
    }

    /// <summary>Opens the panel showing the timeline of events.</summary>
    public void ShowTimelineView()
    {
        SetView(View.Timeline);
        IsPanelOpen = true;
    }

    /// <summary>Opens the panel on the month the clock is in.</summary>
    public void ShowMonthView()
    {
        SetView(View.Month);
        IsPanelOpen = true;
    }

    public override void _Ready()
    {
        if (Session is null)
        {
            GD.PushError("CalendarPanel needs a world session.");
            return;
        }

        _panel = new PanelContainer
        {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0.42f,
            AnchorBottom = 1,
            OffsetLeft = ScreenMargin,
            OffsetRight = -ScreenMargin,
            OffsetBottom = -BottomOffset,
        };
        _panel.AddThemeStyleboxOverride("panel", PanelStyle.SidePanel());
        AddChild(_panel);
        var layout = new VBoxContainer();
        _panel.AddChild(layout);
        layout.AddChild(BuildHeader());

        _monthView = BuildMonthView();
        layout.AddChild(_monthView);
        _yearView = BuildYearView();
        layout.AddChild(_yearView);
        _noCalendar = BuildNoCalendar();
        layout.AddChild(_noCalendar);
        _timelineHost = new MarginContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        layout.AddChild(_timelineHost);
        if (Timeline?.DetachContent() is Control strip)
        {
            _timelineHost.AddChild(strip);
        }

        _calendarDialog = new CalendarDialog { Session = Session };
        AddChild(_calendarDialog);

        Session.TimeChanged += OnTimeChanged;
        Session.SelectionChanged += () =>
        {
            _followNow = true;
            Refresh(force: true);
        };
        Session.Changed += () => Refresh(force: true);
        Session.EclipsesReady += () => Refresh(force: true);
        Session.MeteorShowersReady += () => Refresh(force: true);
        if (Toolbar is not null)
        {
            Toolbar.VisibilityChanged += UpdateVisibility;
        }

        SetView(View.Month);
        UpdateVisibility();
    }

    private Control BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        _nav = new HBoxContainer();
        _nav.AddChild(CreateButton("«", () => MovePage(-YearMonths()), "The year before"));
        _nav.AddChild(CreateButton("‹", () => MovePage(_view == View.Year ? -YearMonths() : -1),
            "Back a month (a year, in the year view)"));
        _title = new Label
        {
            CustomMinimumSize = new Vector2(260, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = AppTheme.SectionHeader,
        };
        _nav.AddChild(_title);
        _nav.AddChild(CreateButton("›", () => MovePage(_view == View.Year ? YearMonths() : 1),
            "On a month (a year, in the year view)"));
        _nav.AddChild(CreateButton("»", () => MovePage(YearMonths()), "The year after"));
        _nav.AddChild(CreateButton("Today", () =>
        {
            _followNow = true;
            Refresh(force: true);
        }, "Back to the month the clock is in"));
        row.AddChild(_nav);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        foreach ((View view, string text, string tip) in new[]
        {
            (View.Month, "Month", "One month: each day with its moons and what happens on it"),
            (View.Year, "Year", "The whole year at a glance"),
            (View.Timeline, "Timeline", "Your world's history as lanes of events"),
        })
        {
            Button button = CreateButton(text, () => SetView(view), tip);
            button.ToggleMode = true;
            _viewButtons[view] = button;
            row.AddChild(button);
        }

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _nextSolar = CreateButton("Next Solar Eclipse", () => JumpToEclipse(EclipseKind.Solar),
            "");
        _nextLunar = CreateButton("Next Lunar Eclipse", () => JumpToEclipse(EclipseKind.Lunar),
            "");
        row.AddChild(_nextSolar);
        row.AddChild(_nextLunar);
        row.AddChild(CreateButton("New Event", () => AddEventOn(null),
            "Add an event at the current time, then edit it (or double-click a day)"));
        row.AddChild(CreateButton("Edit Calendar…", EditCalendar,
            "Design this body's calendar: months, weekdays, years, start date"));
        return row;
    }

    private Control BuildMonthView()
    {
        var view = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _weekdays = new GridContainer();
        view.AddChild(_weekdays);
        _days = new GridContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _days.AddThemeConstantOverride("h_separation", 3);
        _days.AddThemeConstantOverride("v_separation", 3);
        view.AddChild(_days);
        return view;
    }

    private ScrollContainer BuildYearView()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _yearMonths = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _yearMonths.AddThemeConstantOverride("h_separation", 18);
        _yearMonths.AddThemeConstantOverride("v_separation", 12);
        scroll.AddChild(_yearMonths);
        return scroll;
    }

    private VBoxContainer BuildNoCalendar()
    {
        var box = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _noCalendarText = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        box.AddChild(_noCalendarText);
        var center = new CenterContainer();
        center.AddChild(CreateButton("Make a Calendar…", EditCalendar,
            "Start from twelve months that fit its year, and rename and reshape them"));
        box.AddChild(center);
        return box;
    }

    private void SetView(View view)
    {
        _view = view;
        foreach ((View each, Button button) in _viewButtons)
        {
            button.SetPressedNoSignal(each == view);
        }

        Refresh(force: true);
        ShowHint();
    }

    private void UpdateVisibility()
    {
        Visible = _open && (Toolbar?.Visible ?? true);
        if (Timeline is not null)
        {
            Timeline.IsStripOpen = Visible && _view == View.Timeline;
        }

        ShowHint();
    }

    private void ShowHint()
    {
        Toolbar?.SetHint(this, !_open ? null : _view switch
        {
            View.Timeline => "New Event adds one at the current date. Drag the strip to scroll " +
                "through time; click an event to go to it, double-click it to edit it.",
            View.Year => "Click a day to run the clock there, or a month's name to open it. " +
                "‹ and › turn the years.",
            _ => "Click a day to run the clock there; double-click it to add an event that " +
                "day. ‹ and › turn the months, « and » the years.",
        });
    }

    private void OnTimeChanged()
    {
        if (!Visible || _view == View.Timeline)
        {
            return;
        }

        // While playing, only the highlight on today moves, unless the clock leaves the page.
        Refresh(force: false);
    }

    // Shows the page (following the clock unless moved by hand), rebuilding only what changed.
    private void Refresh(bool force)
    {
        if (Session is null || !IsNodeReady() || !_open)
        {
            return;
        }

        Body body = Session.SelectedBody;
        Calendar? calendar = body.Calendar;
        long today = BodyClock.LocalTimeOn(body, Session.TimeDays).Day - 1;
        bool hasCalendar = calendar is not null && body.HasSurface;
        bool dated = _view != View.Timeline;

        _nav.Visible = dated && hasCalendar;
        _monthView.Visible = dated && hasCalendar && _view == View.Month;
        _yearView.Visible = dated && hasCalendar && _view == View.Year;
        _noCalendar.Visible = dated && !hasCalendar;
        _timelineHost.Visible = _view == View.Timeline;
        _nextSolar.Visible = _nextLunar.Visible = body.HasSurface;
        UpdateEclipseButtons();
        if (Timeline is not null)
        {
            Timeline.IsStripOpen = Visible && _view == View.Timeline;
        }

        if (!dated || calendar is null || !hasCalendar)
        {
            _noCalendarText.Text = body.HasSurface
                ? $"{body.Name} counts plain days (Day 1, Day 2, …). Give it a calendar to see " +
                    "its months, weeks, and years here. The Timeline view works either way."
                : $"{body.Name} has no days to count. Select a planet or moon to see its " +
                    "calendar.";
            _shownSignature = "";
            return;
        }

        if (_followNow || _page is null)
        {
            _page = MonthPage.Containing(calendar, today);
        }

        string signature = $"{_view}|{body.Id}|{_page.Year}|{_page.Month}";
        if (force || signature != _shownSignature)
        {
            _shownSignature = signature;
            if (_view == View.Month)
            {
                BuildMonth(body, calendar, _page);
            }
            else
            {
                BuildYear(body, calendar, _page.Year);
            }
        }

        // The clock left the page: turn to its month (only if following the clock).
        if (_view == View.Month && _followNow && !_page.Holds(today))
        {
            _shownSignature = "";
            Refresh(force: false);
            return;
        }

        MarkToday(today);
    }

    private void MovePage(int months)
    {
        if (Session?.SelectedBody.Calendar is not Calendar calendar || _page is null)
        {
            return;
        }

        _followNow = false;
        _page = _page.Moved(calendar, months);
        Refresh(force: false);
    }

    private int YearMonths() => Session?.SelectedBody.Calendar?.Months.Count ?? 12;

    // One month: weekday names across the top, then a cell per day in its weekday's column.
    private void BuildMonth(Body body, Calendar calendar, MonthPage page)
    {
        _title.Text = $"{page.Name} {YearText(calendar, page.Year)}";
        int columns = calendar.Weekdays.Count > 0 ? calendar.Weekdays.Count : ColumnsWithoutWeeks;
        Clear(_weekdays);
        Clear(_days);
        _cells.Clear();
        _todayCell = null;
        _weekdays.Columns = columns;
        _days.Columns = columns;
        foreach (string name in calendar.Weekdays)
        {
            _weekdays.AddChild(new Label
            {
                Text = name,
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClipText = true,
                Modulate = new Color(1, 1, 1, 0.7f),
            });
        }

        Dictionary<long, List<(string Text, Color Color)>> marks =
            MarksBetween(body, page.FirstDayIndex, page.FirstDayIndex + page.Days);
        for (int i = 0; i < page.FirstWeekday; i++)
        {
            _days.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        }

        for (int day = 0; day < page.Days; day++)
        {
            long index = page.FirstDayIndex + day;
            PanelContainer cell = DayCell(body, index, day + 1,
                marks.GetValueOrDefault(index) ?? []);
            _cells[index] = cell;
            _days.AddChild(cell);
        }
    }

    private PanelContainer DayCell(Body body, long dayIndex, int dayOfMonth,
        List<(string Text, Color Color)> marks)
    {
        var cell = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = CalendarMath.Format(body.Calendar!,
                    CalendarMath.DateOf(body.Calendar!, dayIndex)) +
                (marks.Count > 0 ? "\n" + string.Join("\n", marks.Select(m => m.Text)) : "") +
                "\nClick: go to this day · double-click: add an event",
        };
        cell.AddThemeStyleboxOverride("panel", CellStyle(today: false));
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 0);
        cell.AddChild(stack);

        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        top.AddChild(new Label
        {
            Text = dayOfMonth.ToString(),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        double noon = BodyClock.TimeAt(body, dayIndex, body.DayLengthHours / 2);
        foreach (MoonPhase phase in MoonPhase.Of(Session!.World.Bodies, body, noon))
        {
            top.AddChild(new MoonIcon(14) { Phase = phase });
        }

        stack.AddChild(top);
        foreach ((string text, Color color) in marks.Take(3))
        {
            var label = new Label
            {
                Text = text,
                ClipText = true,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = color,
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            stack.AddChild(label);
        }

        if (marks.Count > 3)
        {
            stack.AddChild(new Label
            {
                Text = $"+{marks.Count - 3} more",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(1, 1, 1, 0.6f),
            });
        }

        cell.GuiInput += input => OnDayInput(input, dayIndex);
        return cell;
    }

    // Every month of the year, small: click a day to go there, a month's name to open it.
    private void BuildYear(Body body, Calendar calendar, long year)
    {
        _title.Text = YearText(calendar, year);
        Clear(_yearMonths);
        _cells.Clear();
        _todayCell = null;
        MonthPage first = MonthPage.Of(calendar, year, 0);
        MonthPage last = MonthPage.Of(calendar, year, calendar.Months.Count - 1);
        Dictionary<long, List<(string Text, Color Color)>> marks =
            MarksBetween(body, first.FirstDayIndex, last.FirstDayIndex + last.Days);
        int columns = calendar.Weekdays.Count > 0 ? calendar.Weekdays.Count : ColumnsWithoutWeeks;
        for (int month = 0; month < calendar.Months.Count; month++)
        {
            MonthPage page = MonthPage.Of(calendar, year, month);
            var box = new VBoxContainer();
            Button name = CreateButton(page.Name, () =>
            {
                _followNow = false;
                _page = page;
                SetView(View.Month);
            }, $"Open {page.Name}");
            name.Flat = true;
            box.AddChild(name);
            var grid = new GridContainer { Columns = columns };
            grid.AddThemeConstantOverride("h_separation", 1);
            grid.AddThemeConstantOverride("v_separation", 1);
            for (int i = 0; i < page.FirstWeekday; i++)
            {
                grid.AddChild(new Control { CustomMinimumSize = new Vector2(26, 20) });
            }

            for (int day = 0; day < page.Days; day++)
            {
                long index = page.FirstDayIndex + day;
                List<(string Text, Color Color)> dayMarks = marks.GetValueOrDefault(index) ?? [];
                var cell = new PanelContainer
                {
                    CustomMinimumSize = new Vector2(26, 20),
                    MouseFilter = Control.MouseFilterEnum.Stop,
                    MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                    TooltipText = string.Join("\n", dayMarks.Select(m => m.Text)),
                };
                cell.AddThemeStyleboxOverride("panel", CellStyle(today: false, small: true));
                var number = new Label
                {
                    Text = (day + 1).ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Modulate = dayMarks.Count > 0 ? dayMarks[0].Color : Colors.White,
                };
                number.AddThemeFontSizeOverride("font_size", 12);
                cell.AddChild(number);
                cell.GuiInput += input => OnDayInput(input, index);
                _cells[index] = cell;
                grid.AddChild(cell);
            }

            box.AddChild(grid);
            _yearMonths.AddChild(box);
        }
    }

    // What happens on each day between two day indexes (the end not included), by day.
    private Dictionary<long, List<(string Text, Color Color)>> MarksBetween(
        Body body, long fromDay, long toDay)
    {
        var marks = new Dictionary<long, List<(string, Color)>>();
        double from = BodyClock.TimeAt(body, fromDay, 0), to = BodyClock.TimeAt(body, toDay, 0);
        IReadOnlyList<Body> bodies = Session!.World.Bodies;
        void Add(double time, string text, Color color)
        {
            long day = BodyClock.LocalTimeOn(body, time).Day - 1;
            if (day >= fromDay && day < toDay)
            {
                if (!marks.TryGetValue(day, out List<(string, Color)>? list))
                {
                    marks[day] = list = [];
                }

                list.Add((text, color));
            }
        }

        // The world's own events first: they're what the user put there.
        var timelines = Session.World.Timelines.ToDictionary(t => t.Id);
        foreach (TimelineEvent timelineEvent in Session.World.Events)
        {
            if (timelines.GetValueOrDefault(timelineEvent.TimelineId) is { Hidden: false } line)
            {
                Add(timelineEvent.StartDays, timelineEvent.Title, line.Color.ToGodot());
            }
        }

        if (body.HasSurface)
        {
            double middle = (from + to) / 2;
            foreach (SeasonEvent season in SeasonTimeline.Around(bodies, body, middle).Events)
            {
                Add(season.TimeDays, SeasonText.ShortName(season.Kind), _seasonColor);
            }

            if (Session.SelectedEclipses is EclipseTimeline eclipses)
            {
                foreach (Eclipse eclipse in eclipses.Eclipses)
                {
                    Add(eclipse.PeakDays, Capitalized(EclipseText.Short(eclipse)) + " eclipse",
                        _eclipseColor);
                }
            }

            if (Session.SelectedMeteorShowers is MeteorShowerTimeline showers)
            {
                foreach (MeteorShower shower in showers.Between(from, to))
                {
                    Add(shower.PeakDays, MeteorText.Title(shower, bodies), _showerColor);
                }
            }
        }

        return marks;
    }

    private void MarkToday(long today)
    {
        if (_todayCell == today)
        {
            return;
        }

        if (_todayCell is long old && _cells.GetValueOrDefault(old) is PanelContainer previous)
        {
            previous.AddThemeStyleboxOverride("panel",
                CellStyle(today: false, small: _view == View.Year));
        }

        _todayCell = today;
        if (_cells.GetValueOrDefault(today) is PanelContainer cell)
        {
            cell.AddThemeStyleboxOverride("panel",
                CellStyle(today: true, small: _view == View.Year));
        }
    }

    // A click runs the clock to that day, at the same time of day; a double-click adds an event.
    private void OnDayInput(InputEvent input, long dayIndex)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
            click || Session is null)
        {
            return;
        }

        Body body = Session.SelectedBody;
        long today = BodyClock.LocalTimeOn(body, Session.TimeDays).Day - 1;
        double intoDay = Session.TimeDays - BodyClock.TimeAt(body, today, 0);
        double target = BodyClock.TimeAt(body, dayIndex, 0) + intoDay;
        if (click.DoubleClick)
        {
            AddEventOn(target);
        }
        else
        {
            Time?.GlideTo(target);
        }
    }

    private void AddEventOn(double? timeDays)
    {
        if (Session is null || Timeline is null)
        {
            return;
        }

        TimelineEvent added = Session.AddEvent(timeDays ?? Session.TimeDays);
        Timeline.EditEvent(added);
    }

    private void EditCalendar()
    {
        if (Session is null || !Session.SelectedBody.HasSurface)
        {
            Toolbar?.ShowWarning("Select a planet or moon to give it a calendar.");
            return;
        }

        _calendarDialog.Edit(Session.SelectedBody,
            BodyClock.YearDays(Session.World.Bodies, Session.SelectedBody));
    }

    // The next solar and lunar eclipse, one click away (owner's request, kept from Go to).
    private void UpdateEclipseButtons()
    {
        foreach ((EclipseKind kind, Button button) in new[]
        {
            (EclipseKind.Solar, _nextSolar), (EclipseKind.Lunar, _nextLunar),
        })
        {
            string name = kind == EclipseKind.Solar ? "solar" : "lunar";
            string tip = $"Run the clock to the next {name} eclipse's peak";
            if (NextEclipse(kind) is Eclipse next)
            {
                DisabledTip.Apply(button, $"{tip}: " +
                    $"{BodyClock.Describe(Session!.SelectedBody, next.PeakDays)}", null);
            }
            else
            {
                DisabledTip.Apply(button, tip, Session?.SelectedEclipses is null
                    ? "Still working out the eclipses…"
                    : $"No {name} eclipse in the coming year");
            }
        }
    }

    private Eclipse? NextEclipse(EclipseKind kind) => Session?.SelectedEclipses?.Eclipses
        .Where(e => e.Kind == kind && e.PeakDays > Session.TimeDays)
        .Cast<Eclipse?>()
        .FirstOrDefault();

    private void JumpToEclipse(EclipseKind kind)
    {
        if (NextEclipse(kind) is Eclipse next)
        {
            _followNow = true;
            Time?.GlideTo(next.PeakDays);
        }
    }

    private static string YearText(Calendar calendar, long year) =>
        string.IsNullOrWhiteSpace(calendar.Era) ? $"{year}" : $"{year} {calendar.Era.Trim()}";

    private static string Capitalized(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static StyleBoxFlat CellStyle(bool today, bool small = false)
    {
        var box = new StyleBoxFlat
        {
            BgColor = today ? AppTheme.Accent.Darkened(0.45f) : new Color(0.13f, 0.14f, 0.17f),
            BorderColor = today ? AppTheme.Accent.Lightened(0.2f) : new Color(0.22f, 0.23f, 0.28f),
            ContentMarginLeft = small ? 1 : 5,
            ContentMarginRight = small ? 1 : 5,
            ContentMarginTop = small ? 0 : 3,
            ContentMarginBottom = small ? 0 : 3,
        };
        box.SetBorderWidthAll(today ? 2 : 1);
        box.SetCornerRadiusAll(3);
        return box;
    }

    private static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
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
