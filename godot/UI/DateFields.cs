using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// Fields for entering a moment on a body (VISION.md CAL-01, CAL-06): the date picked from its
/// calendar (<see cref="DatePicker"/>) and the hour, or a day number and the hour without a
/// calendar. Used by the event and relationship editors.
/// </summary>
public partial class DateFields : GridContainer
{
    private DatePicker _date = null!;
    private SpinBox _day = null!;
    private SpinBox _hour = null!;
    private Label _dateLabel = null!;
    private Label _dayLabel = null!;
    private Body? _body;

    public override void _Ready()
    {
        Columns = 2;
        _date = new DatePicker();
        _day = new SpinBox
        {
            MinValue = -1e9,
            MaxValue = 1e9,
            Step = 1,
            UpdateOnTextChanged = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "The day number (Day 1 is the world's first day)",
        }.WithArrowKeys();
        _hour = new SpinBox
        {
            MinValue = 0,
            Step = 0.25,
            UpdateOnTextChanged = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "The hour of the day, in standard hours from midnight",
        }.WithArrowKeys();
        _dateLabel = AddRow("Date", _date);
        _dayLabel = AddRow("Day", _day);
        AddRow("Hour", _hour);
    }

    /// <summary>
    /// Shows a moment (standard days) in the body's calendar, or as a day number.
    /// </summary>
    public void ShowTime(Body body, double timeDays)
    {
        _body = body;
        LocalTime local = BodyClock.LocalTimeOn(body, timeDays);
        bool dated = body.Calendar is not null;
        _date.Visible = _dateLabel.Visible = dated;
        _day.Visible = _dayLabel.Visible = !dated;
        if (body.Calendar is Calendar calendar)
        {
            _date.Show(calendar, local.Day - 1);
        }
        else
        {
            _day.Value = local.Day;
        }

        _hour.MaxValue = Math.Max(0, body.DayLengthHours - 0.01);
        _hour.Value = local.Hour + local.Minute / 60.0;
    }

    /// <summary>
    /// The moment entered, in standard days (day index 0 is the day at time 0: day 1, or the
    /// calendar's start date).
    /// </summary>
    public double TimeDays
    {
        get
        {
            if (_body is not Body body)
            {
                return 0;
            }

            long dayIndex = body.Calendar is not null ? _date.DayIndex : (long)_day.Value - 1;
            return BodyClock.TimeAt(body, dayIndex, _hour.Value);
        }
    }

    private Label AddRow(string text, Control field)
    {
        var label = new Label { Text = text };
        AddChild(label);
        AddChild(field);
        return label;
    }
}
