using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Where live weather's storms are at a moment (VISION.md WTH-02). Storms are made up from the
/// body's seed and fixed slots of time, so asking about any moment finds the same storms
/// without stepping through the days before it: each slot may start one storm, and a moment
/// looks back only as far as a storm can live.
/// </summary>
internal static class StormTracks
{
    // Cyclones: a chance of one each slot in each hemisphere; Earth has about five at a time
    // in each. They start a little nearer the equator than the storm belt and move east and
    // toward the pole.
    private const double CycloneSlotDays = 1.3;
    private const double CycloneChance = 0.85;
    private const double CycloneMinLifeDays = 3.5;
    private const double CycloneMaxLifeDays = 7.0;
    private const double CyclonePolewardDegreesPerDay = 1.3;

    // Tropical storms: rarer, likeliest late in the hemisphere's summer, only where the
    // tropics are warm enough, and only forming over water (or anywhere unpainted).
    private const double TropicalSlotDays = 3.0;
    private const double TropicalMinLifeDays = 5.0;
    private const double TropicalMaxLifeDays = 10.0;
    private const double TropicalWarmestNeededC = 20.0;

    // How much a tropical storm keeps over land.
    private const double TropicalOverLand = 0.35;

    // Storms are this size on an Earth-sized body, but at most these shares of a small one's
    // radius.
    private const double MaxCycloneShareOfRadius = 0.35;
    private const double MaxTropicalShareOfRadius = 0.2;

    private const double EarthRadiusKm = 6371.0;

    /// <summary>The storms on a body at a moment, with how to draw each.</summary>
    /// <param name="weather">The body's live weather.</param>
    /// <param name="timeDays">The moment.</param>
    /// <param name="starLatitude">The star's latitude as felt, now (degrees).</param>
    /// <param name="tropicsC">The tropics' mean temperature now (°C).</param>
    public static List<StormShape> At(
        LiveWeather weather, double timeDays, double starLatitude, double tropicsC)
    {
        var storms = new List<StormShape>();
        foreach (int hemisphere in new[] { 1, -1 })
        {
            AddCyclones(storms, weather, timeDays, hemisphere);
            if (!weather.IsFlat && tropicsC >= TropicalWarmestNeededC)
            {
                AddTropical(storms, weather, timeDays, hemisphere, starLatitude);
            }
        }

        return storms;
    }

    private static void AddCyclones(
        List<StormShape> storms, LiveWeather weather, double time, int hemisphere)
    {
        long first = (long)Math.Floor((time - CycloneMaxLifeDays) / CycloneSlotDays);
        long last = (long)Math.Floor(time / CycloneSlotDays);
        for (long slot = first; slot <= last; slot++)
        {
            var random = new SeededRandom(weather.Seed, 1, (ulong)(hemisphere + 2),
                unchecked((ulong)slot));
            if (random.Next() > CycloneChance)
            {
                continue;
            }

            double born = (slot + random.Next()) * CycloneSlotDays;
            double life = random.Between(CycloneMinLifeDays, CycloneMaxLifeDays);
            double age = time - born;
            double startLongitude = random.Between(-180, 180);
            double startLatitude = weather.IsFlat
                ? random.Between(15, 65)
                : StormBelt(weather.FeltStarLatitude(born), hemisphere) - random.Between(4, 12);
            double speedKmPerDay = random.Between(700, 1100);
            double radiusKm = Math.Min(random.Between(800, 1500),
                MaxCycloneShareOfRadius * weather.RadiusKm);
            double peak = random.Between(0.6, 1.0);
            if (age < 0 || age > life)
            {
                continue;
            }

            double latitude = Math.Min(80, startLatitude + CyclonePolewardDegreesPerDay * age);
            double middle = double.DegreesToRadians((startLatitude + latitude) / 2);
            double longitude = startLongitude + double.RadiansToDegrees(
                speedKmPerDay * age / (weather.RadiusKm * Math.Max(0.2, Math.Cos(middle))));
            var storm = new Storm(StormKind.Cyclone,
                new GeoCoordinate(hemisphere * latitude, longitude), radiusKm,
                peak * Envelope(age / life), born, life);
            storms.Add(new StormShape(storm, weather.RadiusKm));
        }
    }

    private static void AddTropical(List<StormShape> storms, LiveWeather weather, double time,
        int hemisphere, double starLatitude)
    {
        long first = (long)Math.Floor((time - TropicalMaxLifeDays) / TropicalSlotDays);
        long last = (long)Math.Floor(time / TropicalSlotDays);
        double sizeScale = weather.RadiusKm / EarthRadiusKm;
        for (long slot = first; slot <= last; slot++)
        {
            var random = new SeededRandom(weather.Seed, 2, (ulong)(hemisphere + 2),
                unchecked((ulong)slot));
            double born = (slot + random.Next()) * TropicalSlotDays;

            // Likeliest when the star has been over this hemisphere a while (late summer).
            double summer = hemisphere * weather.FeltStarLatitude(born);
            double chance = 0.1 + 0.45 * Math.Clamp(summer / 15, 0, 1);
            if (random.Next() > chance)
            {
                continue;
            }

            double life = random.Between(TropicalMinLifeDays, TropicalMaxLifeDays);
            double startLatitude = random.Between(8, 18);
            double startLongitude = random.Between(-180, 180);
            double radiusKm = Math.Min(random.Between(350, 650),
                MaxTropicalShareOfRadius * weather.RadiusKm);
            double peak = random.Between(0.7, 1.0);
            double recurve = random.Between(0.6, 1.2);
            double age = time - born;
            if (age < 0 || age > life
                || weather.GroundAt(hemisphere * startLatitude, startLongitude) is
                    Model.ClimateKind ground && ground != Model.ClimateKind.Water)
            {
                continue;
            }

            (double latitude, double longitude) =
                TropicalPath(startLatitude, startLongitude, age, life, recurve, sizeScale);
            double signedLatitude = hemisphere * latitude;
            double strength = peak * Envelope(age / life);
            if (weather.GroundAt(signedLatitude, longitude) is Model.ClimateKind here
                && here != Model.ClimateKind.Water)
            {
                strength *= TropicalOverLand;
            }

            var storm = new Storm(StormKind.Tropical,
                new GeoCoordinate(signedLatitude, longitude), radiusKm, strength, born, life);
            storms.Add(new StormShape(storm, weather.RadiusKm));
        }
    }

    // A tropical storm's track: west with the trade winds at first, then curving away from the
    // equator and east (degrees of latitude from the equator, and longitude). Stepped a
    // quarter-day at a time; on a smaller body the same speed covers more degrees.
    private static (double Latitude, double Longitude) TropicalPath(double latitude,
        double longitude, double age, double life, double recurve, double sizeScale)
    {
        const double step = 0.25;
        for (double t = 0; t < age; t += step)
        {
            double dt = Math.Min(step, age - t);
            double turned = SmoothStep(0.45, 1.0, t / life);
            latitude += recurve * (0.5 + 2.0 * turned) * dt / sizeScale;
            longitude += (-4.0 + 9.0 * turned) * dt / sizeScale;
        }

        return (Math.Min(latitude, 60), longitude);
    }

    // The storm belt's latitude in a hemisphere (positive degrees from the equator): farther
    // from the equator in its summer, as the climate's.
    private static double StormBelt(double starLatitude, int hemisphere) =>
        ClimateYear.StormBeltLatitude + ClimateYear.StormBeltShift * hemisphere * starLatitude;

    // How strong a storm is through its life (0 to 1 of the way): growing, then fading.
    private static double Envelope(double share) => Math.Pow(Math.Sin(Math.PI * share), 0.8);

    private static double SmoothStep(double from, double to, double value)
    {
        double t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
