using Godot;
using NothicWorlds.Controls;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Session;

namespace NothicWorlds.UI;

/// <summary>
/// A weather pin's weather (VISION.md WTH-01; owner's choices: a year chart with today marked,
/// plus today's numbers): its name (editable), place, today's temperature, daylight, noon sun,
/// and season, and the year chart for the current year of the body's calendar (or of its own
/// year, without one). It doesn't block the rest of the app, so the clock can run while it's
/// open; today follows the clock.
/// </summary>
public partial class WeatherWindow : AcceptDialog
{
    private const string DeleteAction = "delete";
    private const string ZoomAction = "zoom";
    private const float ContentWidth = 480;

    private LineEdit _name = null!;
    private Label _place = null!;
    private Label _today = null!;
    private Label _yearLabel = null!;
    private WeatherChart _chart = null!;
    private Label _terrain = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _layout = null!;
    private Label _note = null!;
    private Guid? _pinId;

    // The year shown (its start and end, in standard days) and its weather, worked out again
    // only when the year shown changes or the world does.
    private (double From, double To)? _year;
    private ClimateYear? _climate;
    private SeasonTimeline? _seasons;

    /// <summary>The open world. Set it before adding the window to the tree.</summary>
    public WorldSession Session { get; init; } = null!;

    /// <summary>The camera, for Zoom to (VISION.md REN-04).</summary>
    public PlanetCamera? Camera { get; init; }

    public override void _Ready()
    {
        Exclusive = false;
        OkButtonText = "Close";
        AddButton("Delete Pin", right: false, action: DeleteAction).TooltipText =
            "Delete this weather pin (Ctrl+Z brings it back)";
        AddButton("Zoom to", right: false, action: ZoomAction).TooltipText =
            "Glide down to look at this spot from close up";
        CustomAction += action =>
        {
            if (action == DeleteAction && _pinId is Guid id)
            {
                Hide();
                Session.DeleteWeatherPin(id);
            }
            else if (action == ZoomAction && Pin is WeatherPin pin && Camera is not null)
            {
                Hide();
                _ = ZoomTo.PinAsync(Session, Camera, pin.BodyId, pin.Spot);
            }
        };

        var layout = new VBoxContainer { CustomMinimumSize = new Vector2(ContentWidth, 0) };
        _layout = layout;
        _name = new LineEdit { PlaceholderText = "Name" };
        _name.TextSubmitted += _ => CommitName();
        _name.FocusExited += CommitName;
        layout.AddChild(_name);
        _place = new Label { Modulate = new Color(1, 1, 1, 0.65f) };
        layout.AddChild(_place);
        _today = WrappingLabel();
        layout.AddChild(_today);
        _yearLabel = new Label();
        layout.AddChild(_yearLabel);
        _chart = new WeatherChart();
        layout.AddChild(_chart);
        _terrain = WrappingLabel();
        layout.AddChild(_terrain);
        _note = WrappingLabel();
        _note.Modulate = new Color(1, 1, 1, 0.6f);
        layout.AddChild(_note);
        // Scrolls when the screen is too short for everything (the chart, the rain, the notes).
        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _scroll.AddChild(layout);
        AddChild(_scroll);

        Session.Changed += () =>
        {
            _climate = null;  // The pin, its body, or the system may have changed.
            _seasons = null;
            Refresh();
        };
        Session.TimeChanged += Refresh;
        Session.WorldClosed += _ => Hide();
    }

    /// <summary>Shows a weather pin's weather.</summary>
    public void Open(Guid pinId)
    {
        _pinId = pinId;
        _year = null;
        _climate = null;
        _seasons = null;
        Refresh();
        if (Pin is not null)
        {
            FitToScreen();
            PopupCentered();
        }
    }

    private WeatherPin? Pin => Session.World.WeatherPins.FirstOrDefault(p => p.Id == _pinId);

    // As tall as the content, but never taller than the screen leaves room for (beside the
    // title and the buttons); the rest scrolls.
    private void FitToScreen()
    {
        const float titleAndButtons = 120;
        float room = GetTree().Root.GetVisibleRect().Size.Y - titleAndButtons;
        float content = _layout.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(ContentWidth + 12, Math.Min(content, room));
        ResetSize();
    }

    // A whole number of degrees, never "-0".
    private static string Degrees(double celsius)
    {
        double rounded = Math.Round(celsius);
        return (rounded == 0 ? 0 : rounded).ToString("0");
    }

    private void CommitName()
    {
        if (_pinId is Guid id && Session.RenameWeatherPin(id, _name.Text) is string problem)
        {
            _note.Text = $"Can't rename it: {problem}.";
        }
    }

    private void Refresh()
    {
        if (_pinId is null)
        {
            return;
        }

        if (Pin is not WeatherPin pin
            || Session.World.Bodies.FirstOrDefault(b => b.Id == pin.BodyId) is not Body body)
        {
            Hide();  // Deleted (or undone away).
            return;
        }

        if (!Visible && _climate is not null)
        {
            return;
        }

        Title = $"Weather: {pin.Name}";
        if (!_name.HasFocus())
        {
            _name.Text = pin.Name;
        }

        _place.Text = $"{body.Name}, {PlaceText.Describe(pin.Spot)}";
        double now = Session.TimeDays;
        (double from, double to, string yearName,
            List<(string Label, double From, double To)> months) = YearAround(body, now);
        if (_climate is null || _year != (from, to))
        {
            _year = (from, to);
            _climate = ClimateYear.At(Session.World.Bodies, body, pin.Spot, from,
                TerrainSurroundings.At(body, Session.TerrainTypes, pin.Spot));
        }

        if (_climate is not ClimateYear climate)
        {
            _today.Text = $"{body.Name} has no star, so there's no weather to work out.";
            _yearLabel.Text = "";
            _chart.Show([], null);
            _terrain.Text = "";
            _note.Text = "";
            return;
        }

        List<WeatherChart.Month> chartMonths = [.. months.Select(m =>
        {
            ClimateDay average = climate.Average(m.From, m.To);
            return new WeatherChart.Month(m.Label, average.LowC, average.MeanC, average.HighC,
                average.DaylightHours, average.RainMm * (m.To - m.From));
        })];
        int thisMonth = months.FindIndex(m => now >= m.From && now < m.To);
        ShowToday(body, pin, climate, now,
            thisMonth < 0 ? null : RainText(chartMonths, thisMonth));
        double yearRain = chartMonths.Sum(m => m.RainMm);
        _yearLabel.Text = $"{yearName} · {yearRain:N0} mm of rain a year";
        _chart.Show(chartMonths, (float)((now - from) / (to - from)));
        _terrain.Text = ClimateText.Describe(climate.Terrain);
        _note.Text = $"Estimated from the sunlight here, around {body.Name}'s average of " +
            $"{body.AverageTemperatureC:0.#} °C (set in the System panel), adjusted for the " +
            "painted terrain. Rain comes from a tropical rain belt that follows the sun, storms " +
            "in the middle latitudes, and the moisture nearby. Winds aren't modeled.";
    }

    // This month's rain, and whether it's a wet or dry season (well above or below the
    // year's average month).
    private static string RainText(List<WeatherChart.Month> months, int index)
    {
        double rain = months[index].RainMm;
        double average = months.Average(m => m.RainMm);
        string season = rain > 1.6 * average ? " (wet season)"
            : rain < 0.4 * average ? " (dry season)" : "";
        return $"{rain:N0} mm of rain this month{season}";
    }

    private void ShowToday(
        Body body, WeatherPin pin, ClimateYear climate, double now, string? rain)
    {
        ClimateDay today = climate.DayAt(now);
        var daylight = TimeSpan.FromHours(today.DaylightHours);
        string season = "";
        if (_seasons is null || !_seasons.Covers(now))
        {
            _seasons = SeasonTimeline.Around(Session.World.Bodies, body, now);
        }

        if (_seasons.SeasonAt(now) is { } seasons)
        {
            Season here = pin.Spot.LatitudeDegrees >= 0 ? seasons.Northern : seasons.Southern;
            season = $" · {here.ToString().ToLowerInvariant()}";
        }

        string sun = today.NoonSunDegrees > 0
            ? $"noon sun {today.NoonSunDegrees:0}° up"
            : "the sun doesn't rise";
        _today.Text = $"{BodyClock.Describe(body, now)}\n" +
            $"{Degrees(today.MeanC)} °C (low {Degrees(today.LowC)}, " +
            $"high {Degrees(today.HighC)}) · " +
            $"{(int)daylight.TotalHours} h {daylight.Minutes:00} m of daylight · {sun}{season}" +
            (rain is null ? "" : $"\n{rain}");
    }

    // The year around a time: the calendar year containing it, month by month, or (without a
    // calendar) the body's own year in twelve equal parts.
    private (double From, double To, string Name, List<(string, double, double)> Months)
        YearAround(Body body, double now)
    {
        if (body.Calendar is Calendar calendar)
        {
            long day = BodyClock.LocalTimeOn(body, now).Day - 1;
            long year = CalendarMath.DateOf(calendar, day).Year;
            double Start(long y, int month) =>
                BodyClock.TimeAt(body, CalendarMath.DayIndexOf(calendar, y, month, 1), 0);
            var months = new List<(string, double, double)>();
            for (int m = 0; m < calendar.Months.Count; m++)
            {
                double end = m + 1 < calendar.Months.Count
                    ? Start(year, m + 1)
                    : Start(year + 1, 0);
                months.Add((Short(calendar.Months[m].Name), Start(year, m), end));
            }

            string era = string.IsNullOrWhiteSpace(calendar.Era) ? "" : $" {calendar.Era.Trim()}";
            return (Start(year, 0), Start(year + 1, 0), $"The year {year}{era}", months);
        }

        double yearDays = BodyClock.YearDays(Session.World.Bodies, body);
        double from = Math.Floor(now / yearDays) * yearDays;
        var parts = Enumerable.Range(0, 12)
            .Select(i => ($"{i + 1}", from + yearDays * i / 12, from + yearDays * (i + 1) / 12))
            .ToList();
        return (from, from + yearDays,
            $"Year {Math.Floor(now / yearDays) + 1:N0} of {body.Name} (in twelfths)", parts);
    }

    private static string Short(string name) => name.Length <= 4 ? name : name[..3];

    // A label that wraps at the window's width. (Without a set width, a wrapping label works
    // out its height as if it were very narrow, and the window grows off the screen.)
    private static Label WrappingLabel() => new()
    {
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        CustomMinimumSize = new Vector2(ContentWidth, 0),
    };
}
