using Godot;
using NothicWorlds.Core.Measurement;

namespace NothicWorlds.UI;

/// <summary>
/// A year of weather at a spot (VISION.md WTH-01; owner's choice: a year chart with today
/// marked): for each month a bar from the average low to the average high (colored from cold
/// blue to hot red) with a dot at the mean, the hours of daylight as a line (scale on the
/// right), and a line at today. Below, the month's rain as blue bars (WTH-03).
/// </summary>
public partial class WeatherChart : Control
{
    private const float LeftMargin = 40;
    private const float RightMargin = 40;
    private const float TopMargin = 8;
    private const float BottomMargin = 20;

    // The rain strip under the temperatures, and the gap above it.
    private const float RainHeight = 46;
    private const float RainGap = 6;

    private static readonly Color _axis = new(0.75f, 0.78f, 0.85f);
    private static readonly Color _grid = new(1, 1, 1, 0.08f);
    private static readonly Color _daylight = new(1.0f, 0.85f, 0.35f);
    private static readonly Color _today = new(1, 1, 1, 0.85f);
    private static readonly Color _rain = new(0.45f, 0.7f, 1.0f);

    private IReadOnlyList<Month> _months = [];
    private float? _todayFraction;

    /// <summary>One month's averages.</summary>
    /// <param name="Label">A short name for the axis.</param>
    /// <param name="LowC">The average daily low, in °C.</param>
    /// <param name="MeanC">The average temperature, in °C.</param>
    /// <param name="HighC">The average daily high, in °C.</param>
    /// <param name="DaylightHours">The average hours of daylight.</param>
    /// <param name="RainMm">The month's total rain, in mm.</param>
    public readonly record struct Month(string Label, double LowC, double MeanC, double HighC,
        double DaylightHours, double RainMm);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(460, 210 + RainHeight + RainGap);
    }

    /// <summary>
    /// Shows a year of months, with today at a fraction of the way through it (null: not in
    /// this year).
    /// </summary>
    public void Show(IReadOnlyList<Month> months, float? todayFraction)
    {
        _months = months;
        _todayFraction = todayFraction;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_months.Count == 0)
        {
            return;
        }

        Font font = GetThemeDefaultFont();
        int fontSize = GetThemeDefaultFontSize() - 3;
        var plot = new Rect2(LeftMargin, TopMargin, Size.X - LeftMargin - RightMargin,
            Size.Y - TopMargin - BottomMargin - RainHeight - RainGap);
        var rainStrip = new Rect2(plot.Position.X, plot.End.Y + RainGap, plot.Size.X, RainHeight);
        double mostRain = Math.Max(_months.Max(m => m.RainMm), 1);
        double mostShown = UnitText.Shown(Quantity.Precipitation, mostRain);
        DrawString(font, new Vector2(2, rainStrip.Position.Y + 20),
            mostShown.ToString(mostShown < 10 ? "0.#" : "0"),
            width: LeftMargin - 6, alignment: HorizontalAlignment.Right, fontSize: fontSize,
            modulate: _rain);
        DrawString(font, new Vector2(2, rainStrip.End.Y), UnitText.Symbol(Quantity.Precipitation),
            width: LeftMargin - 6,
            alignment: HorizontalAlignment.Right, fontSize: fontSize, modulate: _rain);
        DrawRect(rainStrip, _axis with { A = 0.2f }, filled: false);

        // Temperature scale: round steps around the year's range, in °C or °F.
        static double Shown(double celsius) => UnitText.Shown(Quantity.Temperature, celsius);
        double low = Shown(_months.Min(m => m.LowC));
        double high = Shown(_months.Max(m => m.HighC));
        double step = NiceStep(high - low);
        double bottom = Math.Floor(low / step) * step;
        double top = Math.Max(Math.Ceiling(high / step) * step, bottom + step);
        float Y(double celsius) =>
            plot.End.Y - (float)((Shown(celsius) - bottom) / (top - bottom)) * plot.Size.Y;
        for (double t = bottom; t <= top + 1e-9; t += step)
        {
            float y = plot.End.Y - (float)((t - bottom) / (top - bottom)) * plot.Size.Y;
            DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), _grid);
            DrawString(font, new Vector2(2, y + 4), $"{t:0}°", width: LeftMargin - 6,
                alignment: HorizontalAlignment.Right, fontSize: fontSize, modulate: _axis);
        }

        // Daylight scale on the right, in steps of 6 hours (or a quarter, for very long days).
        double maxDaylight = Math.Max(12, Math.Ceiling(_months.Max(m => m.DaylightHours) / 6) * 6);
        double hourStep = maxDaylight <= 48 ? 6 : maxDaylight / 4;
        float DaylightY(double hours) =>
            plot.End.Y - (float)(hours / maxDaylight) * plot.Size.Y;
        for (double h = 0; h <= maxDaylight + 1e-9; h += hourStep)
        {
            DrawString(font, new Vector2(plot.End.X + 4, DaylightY(h) + 4), $"{h:0}h",
                fontSize: fontSize, modulate: _daylight);
        }

        float band = plot.Size.X / _months.Count;
        int labelEvery = Math.Max(1, (int)Math.Ceiling(_months.Count / 12.0));
        var daylightLine = new Vector2[_months.Count];
        for (int i = 0; i < _months.Count; i++)
        {
            Month month = _months[i];
            float middle = plot.Position.X + band * (i + 0.5f);
            float width = Math.Max(2, band * 0.55f);
            var bar = new Rect2(middle - width / 2, Y(month.HighC), width,
                Math.Max(2, Y(month.LowC) - Y(month.HighC)));
            DrawRect(bar, TemperatureColor(month.MeanC));
            DrawCircle(new Vector2(middle, Y(month.MeanC)), Math.Min(3, width / 2), Colors.White);
            daylightLine[i] = new Vector2(middle, DaylightY(month.DaylightHours));
            float rainHeight = (float)(month.RainMm / mostRain) * (RainHeight - 2);
            DrawRect(new Rect2(middle - width / 2, rainStrip.End.Y - rainHeight, width,
                rainHeight), _rain);
            if (i % labelEvery == 0)
            {
                DrawString(font, new Vector2(middle - band * labelEvery / 2, Size.Y - 4),
                    month.Label, width: band * labelEvery, alignment: HorizontalAlignment.Center,
                    fontSize: fontSize, modulate: _axis);
            }
        }

        if (daylightLine.Length > 1)
        {
            DrawPolyline(daylightLine, _daylight, 2, antialiased: true);
        }

        if (_todayFraction is float fraction)
        {
            float x = plot.Position.X + plot.Size.X * Math.Clamp(fraction, 0, 1);
            DrawDashedLine(new Vector2(x, plot.Position.Y), new Vector2(x, rainStrip.End.Y),
                _today, 1.5f, 4);
        }

        DrawRect(plot, _axis with { A = 0.3f }, filled: false);
    }

    // Cold blue through green to hot red.
    private static Color TemperatureColor(double celsius)
    {
        float t = (float)Math.Clamp((celsius + 30) / 70, 0, 1);  // −30 °C to 40 °C
        return Color.FromHsv(0.66f * (1 - t), 0.6f, 0.95f);
    }

    // A round step (1, 2, 5, 10, 20, …) giving about four to eight gridlines.
    private static double NiceStep(double span)
    {
        double raw = Math.Max(span, 1) / 5;
        double scale = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (double multiple in new[] { 1.0, 2.0, 5.0, 10.0 })
        {
            if (multiple * scale >= raw)
            {
                return multiple * scale;
            }
        }

        return 10 * scale;
    }
}
