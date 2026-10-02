using Godot;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// The calendar editor for a planet or moon (VISION.md CAL-01): its months (any number, any
/// lengths), weekdays, year numbering with an optional era, and the date the world's clock
/// starts on. Changes apply together when the user presses Save, as one undo step.
/// </summary>
/// <remarks>
/// A body without a calendar starts from a simple one that fits its year: twelve months sharing
/// its days, and a seven-day week, for the user to rename and reshape.
/// </remarks>
public partial class CalendarDialog : ConfirmationDialog
{
    private const string RemoveAction = "remove";

    private readonly List<ListRow> _months = [];
    private readonly List<ListRow> _weekdays = [];
    private VBoxContainer _monthRows = null!;
    private VBoxContainer _weekdayRows = null!;
    private SpinBox _firstYear = null!;
    private LineEdit _era = null!;
    private OptionButton _startMonth = null!;
    private SpinBox _startDay = null!;
    private OptionButton _startWeekday = null!;
    private Label _summary = null!;
    private Label _problem = null!;
    private Button _removeButton = null!;
    private Guid _bodyId;
    private double _bodyYearDays;

    /// <summary>The open world, which the calendar is saved into.</summary>
    public WorldSession? Session { get; set; }

    // One editable entry of the month or weekday list. Weekdays have no day count.
    private sealed record ListRow(HBoxContainer Row, LineEdit Name, SpinBox? Days);

    public override void _Ready()
    {
        OkButtonText = "Save";
        DialogHideOnOk = false;
        _removeButton = AddButton("Remove Calendar", right: false, action: RemoveAction);
        _removeButton.TooltipText = "Go back to counting days (Day 1, Day 2, …)";
        Confirmed += Save;
        CustomAction += action =>
        {
            if (action == RemoveAction)
            {
                Session?.SetCalendar(_bodyId, null);
                Hide();
            }
        };

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(480, 460),
        };
        var layout = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(layout);
        AddChild(scroll);

        layout.AddChild(Heading("Months"));
        _monthRows = new VBoxContainer();
        layout.AddChild(_monthRows);
        layout.AddChild(AddButtonFor("Add Month", () =>
            AddRow(_months, _monthRows, $"Month {_months.Count + 1}", 30)));
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        layout.AddChild(_summary);

        layout.AddChild(Heading("Weekdays"));
        _weekdayRows = new VBoxContainer();
        layout.AddChild(_weekdayRows);
        layout.AddChild(AddButtonFor("Add Weekday", () =>
            AddRow(_weekdays, _weekdayRows, $"Weekday {_weekdays.Count + 1}", null)));

        layout.AddChild(Heading("Years and Start"));
        layout.AddChild(BuildYearAndStartFields());

        _problem = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1.0f, 0.55f, 0.5f),
        };
        layout.AddChild(_problem);
    }

    /// <summary>Opens the editor on a body's calendar (or a starter one if it has none).</summary>
    public void Edit(Body body, double yearDays)
    {
        _bodyId = body.Id;
        _bodyYearDays = yearDays * 24.0 / body.DayLengthHours;
        Title = $"{body.Name}'s Calendar";
        _removeButton.Visible = body.Calendar is not null;
        _problem.Text = "";
        ShowCalendar(body.Calendar ?? Starter(_bodyYearDays));
        PopupCentered();
    }

    // A simple calendar that fits a year of this many of the body's days.
    private static Calendar Starter(double yearDays)
    {
        int totalDays = (int)Math.Clamp(Math.Round(yearDays), 12, 12L * Calendar.MaxMonthDays);
        return new Calendar
        {
            Months = [.. Enumerable.Range(0, 12).Select(i => new CalendarMonth(
                $"Month {i + 1}", totalDays / 12 + (i < totalDays % 12 ? 1 : 0)))],
            Weekdays = [.. Enumerable.Range(1, 7).Select(i => $"Weekday {i}")],
        };
    }

    private void ShowCalendar(Calendar calendar)
    {
        ClearRows(_months);
        ClearRows(_weekdays);
        foreach (CalendarMonth month in calendar.Months)
        {
            AddRow(_months, _monthRows, month.Name, month.Days);
        }

        foreach (string weekday in calendar.Weekdays)
        {
            AddRow(_weekdays, _weekdayRows, weekday, null);
        }

        _firstYear.Value = calendar.FirstYear;
        _era.Text = calendar.Era ?? "";
        RefreshStartChoices();
        _startMonth.Select(calendar.StartMonth);
        RefreshStartDay();
        _startDay.Value = calendar.StartDay;
        _startWeekday.Select(calendar.Weekdays.Count == 0 ? 0 : calendar.StartWeekday);
    }

    private void Save()
    {
        if (Session is null)
        {
            return;
        }

        var calendar = new Calendar
        {
            Months = [.. _months.Select(row =>
                new CalendarMonth(row.Name.Text.Trim(), (int)row.Days!.Value))],
            Weekdays = [.. _weekdays.Select(row => row.Name.Text.Trim())],
            FirstYear = (long)_firstYear.Value,
            Era = string.IsNullOrWhiteSpace(_era.Text) ? null : _era.Text.Trim(),
            StartMonth = Math.Max(0, _startMonth.Selected),
            StartDay = (int)_startDay.Value,
            StartWeekday = _weekdays.Count == 0 ? 0 : Math.Max(0, _startWeekday.Selected),
        };
        if (Session.SetCalendar(_bodyId, calendar) is string problem)
        {
            _problem.Text = $"Can't save: {problem}.";
            return;
        }

        Hide();
    }

    private Control BuildYearAndStartFields()
    {
        var grid = new GridContainer { Columns = 2 };
        _firstYear = new SpinBox
        {
            MinValue = -1e12,
            MaxValue = 1e12,
            Step = 1,
            UpdateOnTextChanged = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "The year number on the start date",
        }.WithArrowKeys();
        AddLabelled(grid, "First year", _firstYear);
        _era = new LineEdit
        {
            PlaceholderText = "Optional, e.g. \"of the Third Age\"",
            TooltipText = "Words shown after the year number",
        };
        AddLabelled(grid, "Era", _era);
        _startMonth = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _startMonth.ItemSelected += _ => RefreshStartDay();
        AddLabelled(grid, "Start month", _startMonth);
        _startDay = new SpinBox { MinValue = 1, Step = 1, UpdateOnTextChanged = true }
            .WithArrowKeys();
        AddLabelled(grid, "Start day", _startDay);
        _startWeekday = new OptionButton();
        AddLabelled(grid, "Start weekday", _startWeekday);
        grid.TooltipText = "The date at the world's day 1 (time 0)";
        return grid;
    }

    private void AddRow(List<ListRow> rows, VBoxContainer container, string name, int? days)
    {
        var row = new HBoxContainer();
        var nameField = new LineEdit
        {
            Text = name,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        nameField.TextChanged += _ => RefreshStartChoices();
        row.AddChild(nameField);

        SpinBox? daysField = null;
        if (days is int count)
        {
            daysField = new SpinBox
            {
                MinValue = 1,
                MaxValue = Calendar.MaxMonthDays,
                Step = 1,
                Value = count,
                Suffix = "days",
                UpdateOnTextChanged = true,
                TooltipText = "How many of the body's days this month has",
            }.WithArrowKeys();
            daysField.ValueChanged += _ =>
            {
                RefreshStartDay();
                RefreshSummary();
            };
            row.AddChild(daysField);
        }

        var entry = new ListRow(row, nameField, daysField);
        row.AddChild(SmallButton("↑", "Move up", () => Move(rows, container, entry, -1)));
        row.AddChild(SmallButton("↓", "Move down", () => Move(rows, container, entry, +1)));
        row.AddChild(SmallButton("✕", "Remove", () => Remove(rows, entry)));
        container.AddChild(row);
        rows.Add(entry);
        RefreshStartChoices();
    }

    private void Move(List<ListRow> rows, VBoxContainer container, ListRow entry, int steps)
    {
        int from = rows.IndexOf(entry);
        int to = from + steps;
        if (from < 0 || to < 0 || to >= rows.Count)
        {
            return;
        }

        int startMonth = _startMonth.Selected;
        int startWeekday = _startWeekday.Selected;
        rows.RemoveAt(from);
        rows.Insert(to, entry);
        container.MoveChild(entry.Row, to);
        RefreshStartChoices();
        // The start date stays on the same month or weekday, wherever it moved to.
        _startMonth.Select(rows == _months ? Follow(startMonth, from, to) : startMonth);
        _startWeekday.Select(rows == _weekdays ? Follow(startWeekday, from, to) : startWeekday);
        RefreshStartDay();
    }

    private void Remove(List<ListRow> rows, ListRow entry)
    {
        rows.Remove(entry);
        entry.Row.GetParent().RemoveChild(entry.Row);
        entry.Row.QueueFree();
        RefreshStartChoices();
    }

    // Where a selected item ends up after the item at `from` moves to `to`.
    private static int Follow(int selected, int from, int to)
    {
        if (selected == from)
        {
            return to;
        }

        return selected == to ? from : selected;
    }

    private static void ClearRows(List<ListRow> rows)
    {
        foreach (ListRow row in rows)
        {
            row.Row.GetParent().RemoveChild(row.Row);
            row.Row.QueueFree();
        }

        rows.Clear();
    }

    // Keeps the start month and weekday choices matching the lists, keeping the selections.
    private void RefreshStartChoices()
    {
        if (_startMonth is null)
        {
            return;  // Still building the dialog.
        }

        Refill(_startMonth, _months.Select(row => row.Name.Text), "(no months)");
        Refill(_startWeekday, _weekdays.Select(row => row.Name.Text), "(no weekdays)");
        RefreshStartDay();
        RefreshSummary();
    }

    private static void Refill(OptionButton choice, IEnumerable<string> names, string empty)
    {
        int selected = Math.Max(0, choice.Selected);
        choice.Clear();
        foreach (string name in names)
        {
            choice.AddItem(name);
        }

        if (choice.ItemCount == 0)
        {
            choice.AddItem(empty);
        }

        choice.Select(Math.Min(selected, choice.ItemCount - 1));
    }

    private void RefreshStartDay()
    {
        int month = _startMonth.Selected;
        _startDay.MaxValue = month >= 0 && month < _months.Count
            ? _months[month].Days!.Value
            : 1;
    }

    // How the calendar's year compares with the body's real year.
    private void RefreshSummary()
    {
        double days = _months.Sum(row => row.Days!.Value);
        _summary.Text = $"A calendar year has {days:N0} days. One trip around the star takes " +
            $"{_bodyYearDays:N2} of this body's days.";
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 17);
        return label;
    }

    private static Button AddButtonFor(string text, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        button.Pressed += pressed;
        return button;
    }

    private static Button SmallButton(string text, string tooltip, Action pressed)
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

    private static void AddLabelled(GridContainer grid, string text, Control field)
    {
        field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddChild(new Label { Text = text });
        grid.AddChild(field);
    }
}
