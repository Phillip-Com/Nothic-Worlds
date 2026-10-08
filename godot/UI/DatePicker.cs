using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.UI;

/// <summary>
/// A date picked from a calendar (VISION.md CAL-06; owner's choice: everywhere a date is
/// asked): a button showing the date that opens a small month to click a day in, with arrows
/// for months and years, the month's name to choose from, and the year to type for dates far
/// away.
/// </summary>
public partial class DatePicker : Button
{
    private readonly PopupPanel _popup = new();
    private readonly Dropdown _month = new() { FocusMode = FocusModeEnum.None };
    private readonly SpinBox _year = new()
    {
        MinValue = -1e12,
        MaxValue = 1e12,
        Step = 1,
        CustomMinimumSize = new Vector2(110, 0),
        UpdateOnTextChanged = true,
    };
    private readonly GridContainer _weekdays = new();
    private readonly GridContainer _days = new();
    private Control[] _yearControls = [];
    private Calendar? _calendar;
    private MonthPage? _page;
    private long _dayIndex;
    private bool _syncing;

    /// <summary>Makes a picker; give it a calendar and a day with <see cref="Show"/>.</summary>
    public DatePicker()
    {
        Alignment = HorizontalAlignment.Left;
        FocusMode = FocusModeEnum.None;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        TooltipText = "Click to pick the day from a calendar";
        Pressed += OpenPopup;
    }

    /// <summary>Called with the day index when a day is picked.</summary>
    public event Action<long>? DayPicked;

    /// <summary>The day shown (see <see cref="CalendarMath"/>: 0 is the start date).</summary>
    public long DayIndex => _dayIndex;

    /// <summary>
    /// Whether only one year can be shown (for a calendar's own start date, which lies in its
    /// first year): the year arrows and field are hidden.
    /// </summary>
    public bool SingleYear { get; set; }

    public override void _Ready()
    {
        var layout = new VBoxContainer();
        var header = new HBoxContainer();
        Button yearBack = Arrow("«", () => Move(-MonthsInYear()), "The year before");
        header.AddChild(yearBack);
        header.AddChild(Arrow("‹", () => Move(-1), "The month before"));
        _month.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _month.ItemSelected += index =>
        {
            if (!_syncing && _calendar is not null && _page is not null)
            {
                ShowPage(MonthPage.Of(_calendar, _page.Year, (int)index));
            }
        };
        header.AddChild(_month);
        _year.ValueChanged += value =>
        {
            if (!_syncing && _calendar is not null && _page is not null)
            {
                ShowPage(MonthPage.Of(_calendar, (long)value, _page.Month));
            }
        };
        header.AddChild(_year);
        header.AddChild(Arrow("›", () => Move(1), "The month after"));
        Button yearOn = Arrow("»", () => Move(MonthsInYear()), "The year after");
        header.AddChild(yearOn);
        _yearControls = [yearBack, _year, yearOn];
        layout.AddChild(header);
        layout.AddChild(_weekdays);
        _days.AddThemeConstantOverride("h_separation", 2);
        _days.AddThemeConstantOverride("v_separation", 2);
        layout.AddChild(_days);
        _popup.AddThemeStyleboxOverride("panel", PanelStyle.SidePanel());  // Solid
        _popup.AddChild(layout);
        AddChild(_popup);
    }

    /// <summary>Shows a day of a calendar.</summary>
    public void Show(Calendar calendar, long dayIndex)
    {
        _calendar = calendar;
        _dayIndex = dayIndex;
        Text = CalendarMath.Format(calendar, CalendarMath.DateOf(calendar, dayIndex));
    }

    private void OpenPopup()
    {
        if (_calendar is null)
        {
            return;
        }

        foreach (Control control in _yearControls)
        {
            control.Visible = !SingleYear;
        }

        _syncing = true;
        _month.Clear();
        foreach (CalendarMonth month in _calendar.Months)
        {
            _month.AddItem(month.Name);
        }

        _syncing = false;
        ShowPage(MonthPage.Containing(_calendar, _dayIndex));
        // Just under the button, on screen (the button may be in a dialog of its own).
        Vector2 at = GetScreenPosition();
        _popup.Popup(new Rect2I((int)at.X, (int)(at.Y + Size.Y), 0, 0));
    }

    private void Move(int months)
    {
        if (_calendar is null || _page is null)
        {
            return;
        }

        MonthPage moved = _page.Moved(_calendar, months);
        if (!SingleYear || moved.Year == _page.Year)
        {
            ShowPage(moved);
        }
    }

    private int MonthsInYear() => _calendar?.Months.Count ?? 12;

    // A month as a grid of day buttons under its weekday names; the shown day is highlighted.
    private void ShowPage(MonthPage page)
    {
        Calendar calendar = _calendar!;
        _page = page;
        _syncing = true;
        _month.Select(page.Month);
        _year.Value = page.Year;
        _syncing = false;

        int columns = calendar.Weekdays.Count > 0 ? calendar.Weekdays.Count : 10;
        Clear(_weekdays);
        Clear(_days);
        _weekdays.Columns = _days.Columns = columns;
        foreach (string name in calendar.Weekdays)
        {
            var label = new Label
            {
                Text = name.Length > 3 ? name[..3] : name,
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(36, 0),
                TooltipText = name,
                MouseFilter = MouseFilterEnum.Pass,
                Modulate = new Color(1, 1, 1, 0.65f),
            };
            _weekdays.AddChild(label);
        }

        for (int i = 0; i < page.FirstWeekday; i++)
        {
            _days.AddChild(new Control { CustomMinimumSize = new Vector2(36, 0) });
        }

        for (int day = 0; day < page.Days; day++)
        {
            long index = page.FirstDayIndex + day;
            var button = new Button
            {
                Text = (day + 1).ToString(),
                CustomMinimumSize = new Vector2(36, 28),
                FocusMode = FocusModeEnum.None,
                ToggleMode = true,
                ButtonPressed = index == _dayIndex,
            };
            button.Pressed += () => Pick(index);
            _days.AddChild(button);
        }

        _popup.Size = Vector2I.Zero;  // Fit the new month
    }

    private void Pick(long dayIndex)
    {
        Show(_calendar!, dayIndex);
        _popup.Hide();
        DayPicked?.Invoke(dayIndex);
    }

    private static Button Arrow(string text, Action pressed, string tooltip)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            FocusMode = FocusModeEnum.None,
        };
        button.Pressed += pressed;
        return button;
    }

    private static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
