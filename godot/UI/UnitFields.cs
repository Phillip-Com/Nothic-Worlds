using System.Runtime.CompilerServices;
using Godot;
using NothicWorlds.Core.Measurement;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// Number fields that show and take a measurement in the units chosen in File ▸ Settings
/// (VISION.md UI-04; owner's choice: editing switches too). The world stays metric: a field
/// is given metric values (<see cref="ShowMetric"/>) and hands back metric ones
/// (<see cref="MetricValue"/>). A value the user didn't change comes back exactly as it was
/// given, so a field in miles never nudges a radius stored in km. When the units change, every
/// such field relabels itself and shows its value again.
/// </summary>
public static class UnitFields
{
    // Each unit field's measurement, metric limits and steps, and the metric value it shows.
    private static readonly ConditionalWeakTable<SpinBox, State> _fields = [];

    static UnitFields()
    {
        AppSettings.UnitsChanged += () =>
        {
            foreach ((SpinBox field, State state) in _fields)
            {
                if (GodotObject.IsInstanceValid(field))
                {
                    Apply(field, state);
                }
            }
        };
    }

    /// <summary>
    /// Makes a field a measurement of <paramref name="quantity"/>, from
    /// <paramref name="minMetric"/> to <paramref name="maxMetric"/> (in metric), stepping by
    /// <paramref name="metricStep"/> in metric and <paramref name="imperialStep"/> (or the
    /// same number) in imperial. Its suffix is the unit's symbol.
    /// </summary>
    public static SpinBox WithUnit(this SpinBox field, Quantity quantity, double minMetric,
        double maxMetric, double metricStep, double? imperialStep = null)
    {
        var state = new State(quantity, minMetric, maxMetric, metricStep,
            imperialStep ?? metricStep);
        _fields.AddOrUpdate(field, state);
        Apply(field, state);
        return field;
    }

    /// <summary>
    /// Shows a metric value from the world in the chosen units, unless the user is typing in
    /// the field. Doesn't count as an edit.
    /// </summary>
    public static void ShowMetric(this SpinBox field, double metric)
    {
        if (!_fields.TryGetValue(field, out State? state))
        {
            field.ShowValue(metric);
            return;
        }

        state.Metric = metric;
        field.ShowValue(Units.ToShown(state.Quantity, metric, AppSettings.Units));
    }

    /// <summary>
    /// The field's value in metric: what it was given if the user left it as shown, or what
    /// they typed, converted.
    /// </summary>
    public static double MetricValue(this SpinBox field)
    {
        if (!_fields.TryGetValue(field, out State? state))
        {
            return field.Value;
        }

        UnitSystem system = AppSettings.Units;
        if (state.Metric is double given
            && Math.Abs(field.Value - Units.ToShown(state.Quantity, given, system))
                <= field.Step / 2)
        {
            return given;
        }

        return Math.Clamp(Units.ToMetric(state.Quantity, field.Value, system),
            state.MinMetric, state.MaxMetric);
    }

    // Sets the suffix, limits, and step for the units chosen now, and shows the value again.
    // Signals are held meanwhile: new limits make the field round its value again, which
    // would otherwise reach the world as an edit (a radius in miles stored as km).
    private static void Apply(SpinBox field, State state)
    {
        UnitSystem system = AppSettings.Units;
        double step = system == UnitSystem.Metric ? state.MetricStep : state.ImperialStep;
        double low = Units.ToShown(state.Quantity, state.MinMetric, system);
        double high = Units.ToShown(state.Quantity, state.MaxMetric, system);
        field.SetBlockSignals(true);
        field.Suffix = Units.Symbol(state.Quantity, system);
        field.Step = step;
        // On whole steps: the field rounds to steps counted from its lowest value, so a low
        // of 0.62 miles would turn a typed 4,000 into 3,999.62. MetricValue keeps the true
        // limits.
        field.MinValue = Math.Floor(Math.Min(low, high) / step) * step;
        field.MaxValue = Math.Ceiling(Math.Max(low, high) / step) * step;
        if (state.Metric is double metric)
        {
            field.SetValueNoSignal(Units.ToShown(state.Quantity, metric, system));
        }

        field.SetBlockSignals(false);
    }

    private sealed class State(Quantity quantity, double minMetric, double maxMetric,
        double metricStep, double imperialStep)
    {
        public Quantity Quantity { get; } = quantity;

        public double MinMetric { get; } = minMetric;

        public double MaxMetric { get; } = maxMetric;

        public double MetricStep { get; } = metricStep;

        public double ImperialStep { get; } = imperialStep;

        public double? Metric { get; set; }
    }
}
