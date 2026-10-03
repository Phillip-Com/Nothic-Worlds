using Godot;
using NothicWorlds.Core.Model;

namespace NothicWorlds.UI;

/// <summary>
/// The calendar editor's leap years (VISION.md CAL-04; owner's choices: "every N years,
/// except…" in up to three tiers, adding days to a chosen month, with a Suggest button): the
/// rule's fields, and how far the calendar drifts from the body's real year.
/// </summary>
public partial class LeapYearSection : VBoxContainer
{
    private CheckBox _on = null!;
    private Control _fields = null!;
    private SpinBox _every = null!;
    private SpinBox _except = null!;
    private SpinBox _exceptAgain = null!;
    private SpinBox _days = null!;
    private OptionButton _month = null!;
    private Label _drift = null!;
    private bool _showing;

    // The calendar's common-year length and the body's real year, both in the body's days.
    private double _commonYear;
    private double _realYear;

    /// <summary>Raised when the user changes the rule.</summary>
    public event Action? Changed;

    /// <summary>The rule as the fields show it, or null for no leap years.</summary>
    public LeapRule? Rule => _on.ButtonPressed
        ? new LeapRule((int)_every.Value, NullIfZero(_except.Value),
            NullIfZero(_exceptAgain.Value), Math.Max(0, _month.Selected), (int)_days.Value)
        : null;

    public override void _Ready()
    {
        _on = new CheckBox
        {
            Text = "Leap years",
            TooltipText = "Add days in some years, so the calendar keeps up with the real year",
        };
        _on.Toggled += _ => Edited();
        AddChild(_on);

        var grid = new GridContainer { Columns = 2 };
        _every = Field(grid, "Every", 1, "years", "A year whose number divides by this is a " +
            "leap year (4 for ours)");
        _except = Field(grid, "Except every", 0, "years", "...unless it also divides by " +
            "this (100 for ours; 0 for no exception)");
        _exceptAgain = Field(grid, "But every", 0, "years", "...unless it also divides by " +
            "this (400 for ours; 0 for none)");
        _days = Field(grid, "Leap days", 1, "", "How many days a leap year adds");
        grid.AddChild(new Label { Text = "Added to" });
        _month = new Dropdown { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _month.ItemSelected += _ => Edited();
        grid.AddChild(_month);
        _every.Value = 4;

        var suggest = new Button
        {
            Text = "Suggest",
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = "Work out the simplest rule that keeps the calendar with the real year",
        };
        suggest.Pressed += Suggest;
        var fields = new VBoxContainer();
        fields.AddChild(grid);
        fields.AddChild(suggest);
        _fields = fields;
        AddChild(fields);

        _drift = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_drift);
    }

    /// <summary>Shows a calendar's rule (or none), for its months.</summary>
    public void Show(LeapRule? rule, IEnumerable<string> monthNames)
    {
        _showing = true;
        SetMonths(monthNames);
        _on.ButtonPressed = rule is not null;
        if (rule is not null)
        {
            _every.Value = rule.Every;
            _except.Value = rule.Except ?? 0;
            _exceptAgain.Value = rule.ExceptAgain ?? 0;
            _days.Value = rule.Days;
            _month.Select(Math.Min(rule.Month, _month.ItemCount - 1));
        }
        else
        {
            // A sensible start if it's turned on: our own rule, in the second month.
            (_every.Value, _except.Value, _exceptAgain.Value, _days.Value) = (4, 100, 400, 1);
            _month.Select(Math.Min(1, _month.ItemCount - 1));
        }

        _showing = false;
        RefreshDrift();
    }

    /// <summary>Keeps the month choice matching the months, keeping the selection.</summary>
    public void SetMonths(IEnumerable<string> monthNames)
    {
        int selected = Math.Max(0, _month.Selected);
        _month.Clear();
        foreach (string name in monthNames)
        {
            _month.AddItem(name);
        }

        if (_month.ItemCount > 0)
        {
            _month.Select(Math.Min(selected, _month.ItemCount - 1));
        }
    }

    /// <summary>
    /// Sets the lengths to compare: the calendar's common year and the body's real year, in the
    /// body's days.
    /// </summary>
    public void SetYears(double commonYear, double realYear)
    {
        (_commonYear, _realYear) = (commonYear, realYear);
        RefreshDrift();
    }

    private void Suggest()
    {
        double extra = _realYear - _commonYear;
        if (LeapRule.Suggest(extra, Math.Max(0, _month.Selected), (int)_days.Value)
            is not LeapRule rule)
        {
            _drift.Text = extra <= 0
                ? $"Can't suggest one: the real year ({_realYear:N4} days) isn't longer than the " +
                  $"months ({_commonYear:N0} days). Shorten a month first."
                : $"Can't suggest one: the real year is {extra:N2} days longer than the " +
                  "months; add days to a month (or more leap days) first.";
            return;
        }

        _showing = true;
        _every.Value = rule.Every;
        _except.Value = rule.Except ?? 0;
        _exceptAgain.Value = rule.ExceptAgain ?? 0;
        _showing = false;
        Edited();
    }

    private void Edited()
    {
        if (_showing)
        {
            return;
        }

        RefreshDrift();
        Changed?.Invoke();
    }

    private void RefreshDrift()
    {
        if (_drift is null)
        {
            return;  // Still building.
        }

        _fields.Visible = _on.ButtonPressed;
        double average = _commonYear + (Rule?.AverageExtraDays ?? 0);
        double error = Math.Abs(average - _realYear);
        string drift = error < 1e-9 ? "it never drifts"
            : 1 / error >= 1_000_000 ? "it drifts a day in over a million years"
            : $"it drifts a day every {1 / error:N0} years";
        _drift.Text = $"On average a calendar year is {average:N4} days; the real year is " +
            $"{_realYear:N4}, so {drift}.";
    }

    private SpinBox Field(GridContainer grid, string label, double min, string suffix,
        string tooltip)
    {
        grid.AddChild(new Label { Text = label });
        var field = new SpinBox
        {
            MinValue = min,
            MaxValue = LeapRule.MaxYears,
            Step = 1,
            Suffix = suffix,
            UpdateOnTextChanged = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = tooltip,
        }.WithArrowKeys();
        field.ValueChanged += _ => Edited();
        grid.AddChild(field);
        return field;
    }

    private static int? NullIfZero(double value) => value < 1 ? null : (int)value;
}
