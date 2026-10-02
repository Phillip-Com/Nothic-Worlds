using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// Fields for entering a moment on a body (VISION.md CAL-01, SIM-02): year, month, day, and
/// hour in the body's calendar, or day and hour without one. Used by the Go to dialog and the
/// event editor.
/// </summary>
public partial class DateFields : GridContainer
{
    private SpinBox _year = null!;
    private OptionButton _month = null!;
    private SpinBox _day = null!;
    private SpinBox _hour = null!;
    private Control[] _calendarOnly = [];
    private Body? _body;

    public override void _Ready()
    {
        Columns = 2;
        _year = new SpinBox
        {
            MinValue = -1e12,
            MaxValue = 1e12,
            Step = 1,
            UpdateOnTextChanged = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }.WithArrowKeys();
        _month = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _month.ItemSelected += _ => LimitDayToMonth();
        _day = new SpinBox
        {
            Step = 1,
            UpdateOnTextChanged = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }.WithArrowKeys();
        _hour = new SpinBox
        {
            MinValue = 0,
            Step = 0.25,
            UpdateOnTextChanged = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }.WithArrowKeys();
        Label yearLabel = AddRow("Year", _year);
        Label monthLabel = AddRow("Month", _month);
        AddRow("Day", _day);
        AddRow("Hour", _hour);
        _calendarOnly = [yearLabel, _year, monthLabel, _month];
    }

    /// <summary>
    /// Shows a moment (standard days) in the body's calendar, or as a day number.
    /// </summary>
    public void ShowTime(Body body, double timeDays)
    {
        _body = body;
        LocalTime local = BodyClock.LocalTimeOn(body, timeDays);
        foreach (Control field in _calendarOnly)
        {
            field.Visible = body.Calendar is not null;
        }

        if (body.Calendar is Calendar calendar)
        {
            CalendarDate date = CalendarMath.DateOf(calendar, local.Day - 1);
            _month.Clear();
            foreach (CalendarMonth month in calendar.Months)
            {
                _month.AddItem(month.Name);
            }

            _year.Value = date.Year;
            _month.Select(date.Month);
            LimitDayToMonth();
            _day.Value = date.Day;
        }
        else
        {
            _day.MinValue = -1e9;
            _day.MaxValue = 1e9;
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

            long dayIndex = body.Calendar is Calendar calendar
                ? CalendarMath.DayIndexOf(calendar, (long)_year.Value,
                    Math.Max(0, _month.Selected), (int)_day.Value)
                : (long)_day.Value - 1;
            return BodyClock.TimeAt(body, dayIndex, _hour.Value);
        }
    }

    // Limits the day field to the chosen month's days.
    private void LimitDayToMonth()
    {
        if (_body?.Calendar is Calendar calendar && _month.Selected >= 0)
        {
            _day.MinValue = 1;
            _day.MaxValue = calendar.Months[_month.Selected].Days;
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
