using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The weather through a year at one spot on a planet or moon (VISION.md WTH-01): daylight,
/// how high the star climbs, and temperatures. Deterministic, like everything in the
/// simulation.
/// </summary>
/// <remarks>
/// <para><b>Sunlight is exact.</b> Each day, the star's height over the equator (its
/// declination, see <see cref="Seasons"/>) and the spot's latitude give how long the star is up
/// and how high it gets at noon, including polar day and night; the day's total sunlight also
/// allows for the star's distance changing on an elongated orbit.</para>
/// <para><b>Temperatures are a lightweight estimate</b> (owner's choice: around the body's
/// own <see cref="Body.AverageTemperatureC"/>), since air, oceans, height, and terrain aren't
/// modeled. A spot's yearly average follows its share of sunlight (warmer at the equator); its
/// seasons follow the day's sunlight, delayed and softened by about a month, the way land and
/// sea hold heat; and the day/night swing grows with the length of the day. The constants are
/// tuned so an Earth-like planet gives Earth-like numbers.</para>
/// <para><b>Painted terrain</b> (VISION.md WTH-03) adjusts the estimate when it's given:
/// water nearby softens the seasons (and delays them) and the day/night swing, the way coasts
/// are milder than inland; the kind of ground at the spot widens or narrows the day/night swing
/// (deserts swing most) and makes ice and mountains colder. Without terrain, nothing
/// changes.</para>
/// <para><b>Rain is an estimate too</b> (VISION.md WTH-03; owner's choice: temperature and
/// rainfall). It adds a tropical rain belt that follows the star a month behind (wet seasons
/// near the equator, dry ones beside them), a storm belt in the middle latitudes that shifts
/// with the seasons, and a little drizzle everywhere; then scales by the moisture the terrain
/// gives (sea air wetter, deserts very dry; average without painted terrain) and by the cold
/// (cold air holds little water). Tuned so an Earth-like planet gets about 1,750 mm a year at
/// the equator, 250 at 30°, 1,000 at 50°, and almost none at the poles.</para>
/// <para>The year repeats: times outside the year worked out are wrapped into it (exact for a
/// planet circling its star; close for a moon).</para>
/// </remarks>
public sealed class ClimateYear
{
    // Days worked out per year.
    private const int SamplesPerYear = 360;

    // °C per unit of sunlight (as a share of the body's average) between latitudes, and through
    // the seasons (smaller: the seasons are softened by stored heat).
    private const double LatitudeDegreesPerSunlight = 62.5;
    private const double SeasonDegreesPerSunlight = 20.0;

    // How long land and sea take to warm and cool, in standard days: seasons lag about this.
    private const double HeatLagDays = 30.0;

    // The day/night swing for a 24-hour day, in °C; longer days swing more (up to the cap).
    private const double DayNightSwingC = 10.0;
    private const double MaxDayNightSwingC = 60.0;

    // How much fully maritime spots (on or surrounded by water) soften the seasons and the
    // day/night swing, and how much longer their heat lag is.
    private const double MaritimeSeasonSoftening = 0.6;
    private const double MaritimeSwingSoftening = 0.7;
    private const double MaritimeExtraLag = 1.0;

    // Rain, in mm per standard day: the tropical belt's peak (it sits at this share of the
    // star's lagged latitude, this many degrees wide), the storm belt's peak (centred at this
    // latitude, shifted by this share of the season's star latitude), and the drizzle anywhere.
    private const double TropicalRainMm = 9.0;
    private const double TropicalBeltShare = 0.6;
    private const double TropicalBeltWidth = 11.0;
    private const double StormRainMm = 2.6;
    private const double StormBeltLatitude = 48.0;
    private const double StormBeltShift = 0.35;
    private const double StormBeltWidth = 13.0;
    private const double DrizzleMm = 0.25;

    // Cold air holds little water: rain fades from full at 10 °C to its least at −25 °C.
    private const double RainFullAtC = 10.0;
    private const double RainLeastAtC = -25.0;
    private const double ColdestRainShare = 0.15;

    private readonly ClimateDay[] _days;

    private ClimateYear(
        ClimateDay[] days, double fromDays, double yearDays, TerrainSurroundings? terrain)
    {
        _days = days;
        FromDays = fromDays;
        YearDays = yearDays;
        Terrain = terrain;
    }

    /// <summary>The painted terrain the weather allowed for, or null if none was given.</summary>
    public TerrainSurroundings? Terrain { get; }

    /// <summary>When the year worked out starts, in standard days.</summary>
    public double FromDays { get; }

    /// <summary>How long the year is, in standard days (the body's own year).</summary>
    public double YearDays { get; }

    /// <summary>The days worked out, in order through the year.</summary>
    public IReadOnlyList<ClimateDay> Days => _days;

    /// <summary>
    /// Works out the year starting at <paramref name="fromDays"/> at a spot on a body, or null
    /// if the body has no star (or is one). <paramref name="terrain"/>, if given, is the painted
    /// terrain around the spot (see the class notes).
    /// </summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static ClimateYear? At(IReadOnlyList<Body> bodies, Body body, GeoCoordinate spot,
        double fromDays, TerrainSurroundings? terrain = null)
    {
        if (body.Kind == BodyKind.Star || Seasons.StarFor(bodies, body) is not Body star)
        {
            return null;
        }

        if (SystemHierarchy.Problem(bodies) is string problem)
        {
            throw new ArgumentException($"The star system is invalid: {problem}.", nameof(bodies));
        }

        double year = BodyClock.YearDays(bodies, body);
        double step = year / SamplesPerYear;
        var byId = bodies.ToDictionary(b => b.Id);
        OrbitChain bodyChain = OrbitChain.Of(body, byId);
        OrbitChain starChain = OrbitChain.Of(star, byId);
        Vector3D pole = BodyOrientation.NorthPole(body);
        double latitude = double.DegreesToRadians(spot.LatitudeDegrees);

        // The sun first: each day's declination and distance.
        var declinations = new double[SamplesPerYear];
        var distances = new double[SamplesPerYear];
        for (int i = 0; i < SamplesPerYear; i++)
        {
            double time = fromDays + (i + 0.5) * step;
            Vector3D toStar = starChain.PositionAt(time) - bodyChain.PositionAt(time);
            distances[i] = toStar.Length;
            declinations[i] = Math.Asin(Math.Clamp(toStar.Dot(pole) / toStar.Length, -1, 1));
        }

        // Sunlight relative to the body's average: a whole sphere averages a quarter of the
        // light it intercepts, at the year's mean of 1/distance².
        double meanInverseSquare = distances.Average(d => 1 / (d * d));
        var sunlight = new double[SamplesPerYear];
        var daylight = new double[SamplesPerYear];
        for (int i = 0; i < SamplesPerYear; i++)
        {
            (double dayFraction, double dailyMean) = Sun(latitude, declinations[i]);
            daylight[i] = dayFraction * body.DayLengthHours;
            double distanceFactor = 1 / (distances[i] * distances[i]) / meanInverseSquare;
            sunlight[i] = dailyMean * distanceFactor / 0.25;
        }

        double maritime = terrain?.Maritime ?? 0;
        ClimateKind? ground = terrain?.Here;
        double spotAverage = sunlight.Average();
        double[] felt = Lagged(sunlight, step, HeatLagDays * (1 + MaritimeExtraLag * maritime));
        double[] beltLatitude = Lagged(
            [.. declinations.Select(d => double.RadiansToDegrees(d))], step, HeatLagDays);
        double moisture = Moisture(terrain);
        double swing = Math.Min(MaxDayNightSwingC,
            DayNightSwingC * Math.Sqrt(body.DayLengthHours / 24.0)
                * (1 - MaritimeSwingSoftening * maritime) * SwingFactor(ground));
        double baseline = body.AverageTemperatureC
            + LatitudeDegreesPerSunlight * (spotAverage - 1) + Offset(ground);
        double seasons = SeasonDegreesPerSunlight * (1 - MaritimeSeasonSoftening * maritime);

        var days = new ClimateDay[SamplesPerYear];
        for (int i = 0; i < SamplesPerYear; i++)
        {
            double mean = baseline + seasons * (felt[i] - spotAverage);
            double noon = 90 - Math.Abs(spot.LatitudeDegrees
                - double.RadiansToDegrees(declinations[i]));
            double rain = Rain(spot.LatitudeDegrees, beltLatitude[i], mean) * moisture;
            days[i] = new ClimateDay(fromDays + (i + 0.5) * step, mean, mean - swing / 2,
                mean + swing / 2, daylight[i], Math.Max(-90, noon), sunlight[i], rain);
        }

        return new ClimateYear(days, fromDays, year, terrain);
    }

    /// <summary>The weather on the day containing a time (wrapped into the year).</summary>
    public ClimateDay DayAt(double timeDays)
    {
        double intoYear = (timeDays - FromDays) % YearDays;
        if (intoYear < 0)
        {
            intoYear += YearDays;
        }

        int index = Math.Clamp((int)(intoYear / YearDays * _days.Length), 0, _days.Length - 1);
        return _days[index] with { TimeDays = timeDays };
    }

    /// <summary>
    /// How much the terrain makes it rain compared with average ground (1): sea air brings
    /// more, inland less; deserts very little, wetlands, forests, and mountains more. 1
    /// without painted terrain.
    /// </summary>
    public static double Moisture(TerrainSurroundings? terrain)
    {
        if (terrain is null || (terrain.Here is null && terrain.WaterShare == 0))
        {
            return 1.0;
        }

        // About two-thirds water around (like Earth on average) gives 1.
        double air = 0.4 + 0.9 * terrain.Maritime;
        return air * terrain.Here switch
        {
            ClimateKind.Desert => 0.15,
            ClimateKind.Ice => 0.5,
            ClimateKind.Forest => 1.2,
            ClimateKind.Wetland or ClimateKind.Mountains => 1.3,
            _ => 1.0,
        };
    }

    /// <summary>
    /// The average weather over a stretch of time (e.g. a month), wrapped into the year: the
    /// mean of the days' lows, highs, means, daylight, noon heights, sunlight, and rain (still
    /// per day; multiply by the stretch's length for its total).
    /// </summary>
    public ClimateDay Average(double fromDays, double toDays)
    {
        double step = YearDays / _days.Length;
        int count = Math.Max(1, (int)Math.Round((toDays - fromDays) / step));
        double low = 0, high = 0, mean = 0, light = 0, noon = 0, sun = 0, rain = 0;
        for (int i = 0; i < count; i++)
        {
            ClimateDay day = DayAt(fromDays + (i + 0.5) * (toDays - fromDays) / count);
            low += day.LowC;
            high += day.HighC;
            mean += day.MeanC;
            light += day.DaylightHours;
            noon += day.NoonSunDegrees;
            sun += day.Sunlight;
            rain += day.RainMm;
        }

        return new ClimateDay((fromDays + toDays) / 2, mean / count, low / count, high / count,
            light / count, noon / count, sun / count, rain / count);
    }

    // The share of the day the star is up, and the day's average of sin(star height) (its
    // light on level ground, per unit of light facing it), at a latitude and declination.
    private static (double DayFraction, double DailyMean) Sun(double latitude, double declination)
    {
        double cosHourAngle = -Math.Tan(latitude) * Math.Tan(declination);
        double hourAngle = cosHourAngle >= 1 ? 0          // Polar night
            : cosHourAngle <= -1 ? Math.PI                 // Polar day
            : Math.Acos(cosHourAngle);
        double dailyMean = (hourAngle * Math.Sin(latitude) * Math.Sin(declination)
            + Math.Cos(latitude) * Math.Cos(declination) * Math.Sin(hourAngle)) / Math.PI;
        return (hourAngle / Math.PI, Math.Max(0, dailyMean));
    }

    // Rain on average ground, in mm per day, at a latitude, with the star's (lagged) latitude
    // and the day's mean temperature.
    private static double Rain(double latitude, double starLatitude, double meanC)
    {
        double belt = TropicalBeltShare * starLatitude;
        double tropical = TropicalRainMm * Bell(latitude - belt, TropicalBeltWidth);

        // The storm belt sits farther from the equator in summer (when the star is on this
        // side), nearer in winter.
        double summer = latitude >= 0 ? starLatitude : -starLatitude;
        double stormLatitude = StormBeltLatitude + StormBeltShift * summer;
        double storms = StormRainMm * Bell(Math.Abs(latitude) - stormLatitude, StormBeltWidth);

        double warmth = Math.Clamp(
            (meanC - RainLeastAtC) / (RainFullAtC - RainLeastAtC), ColdestRainShare, 1.0);
        return (tropical + storms + DrizzleMm) * warmth;
    }

    private static double Bell(double offset, double width) =>
        Math.Exp(-(offset / width) * (offset / width));

    // How the kind of ground widens or narrows the day/night swing: dry air lets deserts heat
    // and cool most; plants and wet ground hold heat.
    private static double SwingFactor(ClimateKind? ground) => ground switch
    {
        ClimateKind.Desert => 2.0,
        ClimateKind.Mountains => 1.2,
        ClimateKind.Forest => 0.8,
        ClimateKind.Wetland => 0.7,
        _ => 1.0,
    };

    // How much colder (or warmer) the kind of ground is than the latitude alone gives, in °C:
    // ice reflects most sunlight, mountains stand high in thinner air, deserts have clear skies.
    private static double Offset(ClimateKind? ground) => ground switch
    {
        ClimateKind.Ice => -8.0,
        ClimateKind.Mountains => -6.0,
        ClimateKind.Desert => 2.0,
        _ => 0.0,
    };

    // Sunlight as the ground feels it: each day moves part of the way toward the day's light
    // (an exponential average with the heat lag), run round the year twice so the start
    // doesn't matter.
    private static double[] Lagged(double[] sunlight, double step, double lagDays)
    {
        double keep = Math.Exp(-step / lagDays);
        double felt = sunlight.Average();
        var result = new double[sunlight.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < sunlight.Length; i++)
            {
                felt = keep * felt + (1 - keep) * sunlight[i];
                result[i] = felt;
            }
        }

        return result;
    }
}
