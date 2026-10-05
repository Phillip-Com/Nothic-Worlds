using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The live weather everywhere on a body at one moment (VISION.md WTH-02), from
/// <see cref="LiveWeather.At"/>: ask it about any spot with <see cref="SampleAt(Vector3D)"/>.
/// Fixed once made, so it can be asked from several threads at once.
/// </summary>
public sealed class WeatherMoment
{
    // Clouds: noise features about this many km across (fewer, larger ones on a small body),
    // carried by the wind. Each of two layers of noise starts afresh every PhaseDays and fades
    // in and out, so the wind never stretches the clouds out too far.
    private const double CloudFeatureKm = 900;
    private const double PhaseDays = 2.0;

    // How cloudy the sky is on average, from the climate's rain here (mm per day): a little
    // even in deserts, mostly cloudy where it rains most.
    private const double ClearestCover = 0.1;
    private const double CoverFromRain = 0.75;
    private const double HumidRainMm = 3.0;
    private const double CloudEdge = 0.18;

    // Rain falls from the thickest part of the background cloud, this share of its usual
    // cover, as hard as it takes for a year of it to bring this share of the climate's rain
    // (the storms bring the rest).
    private const double RainingShareOfCover = 0.5;
    private const double BackgroundShareOfRain = 1.0;

    // Snow falls at or below this mean temperature.
    private const double SnowBelowC = 0.5;

    // A spot counts as in a storm where the storm's cloud is at least this thick.
    private const double InStormCloud = 0.5;

    // The gentle wind on a flat world, toward the east, in m/s.
    private const double FlatWindMs = 4.0;

    private readonly LiveWeather _weather;
    private readonly double[] _bandMeanC = new double[LiveWeather.BandCount];
    private readonly double[] _bandRainMm = new double[LiveWeather.BandCount];
    private readonly double _beltShift;
    private readonly double _cloudFrequency;
    private readonly List<StormShape> _storms;

    internal WeatherMoment(LiveWeather weather, double timeDays)
    {
        _weather = weather;
        TimeDays = timeDays;
        for (int i = 0; i < LiveWeather.BandCount; i++)
        {
            ClimateDay day = weather.BandDay(i, timeDays);
            _bandMeanC[i] = day.MeanC;
            _bandRainMm[i] = day.RainMm;
        }

        double starLatitude = weather.FeltStarLatitude(timeDays);
        _beltShift = weather.IsFlat ? 0 : ClimateYear.TropicalBeltShare * starLatitude;
        _cloudFrequency = Math.Clamp(weather.RadiusKm / CloudFeatureKm, 1.5, 40);
        double tropicsC = (Band(15).MeanC + Band(-15).MeanC) / 2;
        _storms = StormTracks.At(weather, timeDays, starLatitude, tropicsC);
        Storms = [.. _storms.Select(shape => shape.Storm)];
    }

    /// <summary>The moment, in standard days.</summary>
    public double TimeDays { get; }

    /// <summary>The storms at this moment.</summary>
    public IReadOnlyList<Storm> Storms { get; }

    /// <summary>The weather at a spot.</summary>
    public WeatherSample SampleAt(GeoCoordinate spot) => SampleAt(SphericalPolygon.ToUnit(spot));

    /// <summary>
    /// The weather in a direction from the body's middle (it needn't be unit length).
    /// </summary>
    public WeatherSample SampleAt(Vector3D direction)
    {
        direction *= 1 / direction.Length;
        double latitude = double.RadiansToDegrees(Math.Asin(Math.Clamp(direction.Y, -1, 1)));
        double longitude = double.RadiansToDegrees(Math.Atan2(direction.X, direction.Z));

        (double meanC, double bandRainMm) = Band(latitude);
        double temperature = meanC + ClimateYear.Offset(_weather.GroundAt(latitude, longitude));
        double moisture = _weather.MoistureAt(latitude, longitude);
        double climateRain = bandRainMm * moisture;
        (double windEast, double windNorth) = BeltWind(latitude);

        double humidity = 1 - Math.Exp(-climateRain / HumidRainMm);
        double usualCover = ClearestCover + CoverFromRain * humidity;
        double noise = DriftingNoise(latitude, longitude, windEast);
        double background = SmoothStep(1 - usualCover - CloudEdge, 1 - usualCover + CloudEdge,
            noise);
        double raining = RainingShareOfCover * usualCover;
        double backgroundRain = BackgroundShareOfRain * climateRain / raining
            * SmoothStep(1 - raining - 0.05, 1 - raining + 0.05, noise);

        double stormCloud = 0, stormRain = 0, thickest = 0;
        StormKind? storm = null;
        foreach (StormShape shape in _storms)
        {
            (double cloud, double rain, double east, double north) = shape.At(direction);
            stormCloud = Math.Max(stormCloud, cloud);
            stormRain += rain;
            windEast += east;
            windNorth += north;
            if (cloud >= InStormCloud && cloud > thickest)
            {
                thickest = cloud;
                storm = shape.Storm.Kind;
            }
        }

        double cover = 1 - (1 - background) * (1 - stormCloud);
        double rainMm = backgroundRain
            + stormRain * ClimateYear.Warmth(temperature) * Math.Min(moisture, 1.3);
        PrecipitationKind falling = rainMm < 0.05 ? PrecipitationKind.None
            : temperature <= SnowBelowC ? PrecipitationKind.Snow
            : PrecipitationKind.Rain;
        return new WeatherSample(cover, falling == PrecipitationKind.None ? 0 : rainMm / 24,
            falling, windEast, windNorth, temperature, storm);
    }

    // The climate at a latitude now: the day's mean temperature and expected rain (mm per
    // day, without terrain), between the two nearest bands.
    private (double MeanC, double RainMm) Band(double latitude)
    {
        double position = Math.Clamp((latitude + 90) / LiveWeather.BandDegrees, 0,
            LiveWeather.BandCount - 1);
        int below = Math.Min((int)position, LiveWeather.BandCount - 2);
        double t = position - below;
        return (_bandMeanC[below] + (_bandMeanC[below + 1] - _bandMeanC[below]) * t,
            _bandRainMm[below] + (_bandRainMm[below + 1] - _bandRainMm[below]) * t);
    }

    // The prevailing wind at a latitude (m/s east and north): three belts each side of the
    // equator, shifted toward the summer hemisphere with the star (less so near the poles).
    private (double East, double North) BeltWind(double latitude)
    {
        if (_weather.IsFlat)
        {
            return (FlatWindMs, 0);
        }

        double shifted = latitude - _beltShift * (1 - Math.Abs(latitude) / 90);
        double away = Math.Abs(shifted);
        double side = Math.Sign(shifted);
        if (away < 30)
        {
            double wave = Math.Sin(Math.PI * away / 30);  // Trade winds, toward the equator
            return (-7 * wave, -side * 2.5 * wave);
        }

        if (away < 60)
        {
            double wave = Math.Sin(Math.PI * (away - 30) / 30);  // Westerlies, toward the pole
            return (11 * wave, side * 1.5 * wave);
        }

        double polar = Math.Sin(Math.PI * (away - 60) / 30);  // Polar easterlies
        return (-5 * polar, -side * 1.5 * polar);
    }

    // Cloud noise (0 to 1, spread about evenly) carried east or west by the wind. Two layers
    // take turns: each is carried for one phase from where it started, fading in and out, so
    // differing winds at different latitudes never shear it into streaks.
    private double DriftingNoise(double latitude, double longitude, double windEastMs)
    {
        double latitudeRadians = double.DegreesToRadians(latitude);
        double radiansPerDay = windEastMs * 86400
            / (_weather.RadiusKm * 1000 * Math.Max(0.25, Math.Cos(latitudeRadians)));
        double total = 0, weights = 0;
        for (int layer = 0; layer < 2; layer++)
        {
            double phases = TimeDays / PhaseDays + 0.5 * layer;
            double cycle = Math.Floor(phases);
            double share = phases - cycle;
            double weight = 1 - Math.Abs(2 * share - 1);
            double carried = double.DegreesToRadians(longitude) - radiansPerDay * share * PhaseDays;
            double cosLatitude = Math.Cos(latitudeRadians);
            var point = new Vector3D(cosLatitude * Math.Sin(carried), Math.Sin(latitudeRadians),
                cosLatitude * Math.Cos(carried)) * _cloudFrequency;
            ulong seed = unchecked(
                _weather.Seed + (ulong)(long)(cycle * 2 + layer) * 0x9E3779B97F4A7C15);
            total += weight * (WeatherNoise.Layered(point, seed) - 0.5);
            weights += weight * weight;
        }

        // Fading two layers together flattens the noise; this keeps its spread the same, and
        // stretches it to cover 0 to 1 about evenly.
        return Math.Clamp(0.5 + 2.4 * total / Math.Sqrt(weights), 0, 1);
    }

    private static double SmoothStep(double from, double to, double value)
    {
        double t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
