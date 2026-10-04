using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The System panel's calendar and seasons part (VISION.md CAL-01, CAL-03): the selected body's
/// calendar, with a button to edit it, and its year of seasons (the solstice or equinox that
/// began the current season and the next three) with their dates and Go to buttons.
/// </summary>
public partial class CalendarSection : VBoxContainer
{
    private Control _calendarRow = null!;
    private Label _calendarSummary = null!;
    private CalendarDialog _dialog = null!;
    private VBoxContainer _seasonRows = null!;
    private Label _noSeasons = null!;

    // What the season rows show, to skip rebuilding them while nothing they show has changed.
    private string _seasonsSignature = "";

    /// <summary>The open world. Set it before adding the section to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>The time bar, for gliding to a date (no Go to buttons without it).</summary>
    public TimeControls? Time { get; init; }

    public override void _Ready()
    {
        _calendarRow = BuildCalendarRow();
        AddChild(_calendarRow);

        AddChild(new Label { Text = "Seasons" });
        _noSeasons = new Label
        {
            Text = "None: the body needs a star and some axial tilt.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_noSeasons);
        _seasonRows = new VBoxContainer();
        _seasonRows.AddThemeConstantOverride("separation", 8);
        AddChild(_seasonRows);

        _dialog = new CalendarDialog { Session = Session };
        AddChild(_dialog);

        Session.TimeChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        Session.TimeChanged -= Refresh;
    }

    /// <summary>Shows the selected body's calendar and seasons.</summary>
    public void Refresh()
    {
        if (!IsNodeReady())
        {
            return;
        }

        Body body = Session.SelectedBody;
        _calendarRow.Visible = body.HasSurface;
        _calendarSummary.Text = body.Calendar is Calendar calendar
            ? Summarize(calendar)
            : "None (counting days)";
        ShowSeasons(body);
    }

    private Control BuildCalendarRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Label { Text = "Calendar" });
        _calendarSummary = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        row.AddChild(_calendarSummary);
        var edit = new Button
        {
            Text = "Edit…",
            FocusMode = FocusModeEnum.None,
            TooltipText = "Design this body's calendar: months, weekdays, years, start date",
        };
        edit.Pressed += () => _dialog.Edit(Session.SelectedBody,
            BodyClock.YearDays(Session.World.Bodies, Session.SelectedBody));
        row.AddChild(edit);
        return row;
    }

    private static string Summarize(Calendar calendar)
    {
        string months = calendar.Months.Count == 1 ? "1 month" : $"{calendar.Months.Count} months";
        return calendar.Weekdays.Count == 0
            ? months
            : $"{months}, {calendar.Weekdays.Count}-day week";
    }

    private void ShowSeasons(Body body)
    {
        double now = Session.TimeDays;
        IReadOnlyList<SeasonEvent> year = Session.SelectedSeasons.YearAround(now);

        // Rebuild only when the rows would read differently (the dates follow the calendar).
        string signature = $"{body.Id}|" + string.Join("|", year.Select(e =>
            $"{e.Kind}:{BodyClock.Describe(body, e.TimeDays)}"));
        if (signature == _seasonsSignature)
        {
            return;
        }

        _seasonsSignature = signature;
        _noSeasons.Visible = year.Count == 0;
        foreach (Node row in _seasonRows.GetChildren())
        {
            _seasonRows.RemoveChild(row);
            row.QueueFree();
        }

        foreach (SeasonEvent seasonEvent in year)
        {
            _seasonRows.AddChild(SeasonRow(body, seasonEvent));
        }
    }

    private Control SeasonRow(Body body, SeasonEvent seasonEvent)
    {
        var row = new HBoxContainer();
        var text = new Label
        {
            Text = $"{SeasonText.Name(seasonEvent.Kind)}\n" +
                $"{BodyClock.Describe(body, seasonEvent.TimeDays)}",
            TooltipText = $"At the same moment, {SeasonText.Note(seasonEvent.Kind)}",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        row.AddChild(text);
        if (Time is not null)
        {
            var goTo = new Button
            {
                Text = "Go to",
                FocusMode = FocusModeEnum.None,
                TooltipText = "Run the clock to this moment",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            double target = seasonEvent.TimeDays;
            TimeControls time = Time;
            goTo.Pressed += () => time.GlideTo(target);
            row.AddChild(goTo);
        }

        return row;
    }
}
