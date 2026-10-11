namespace NothicWorlds.Core.Simulation;

/// <summary>
/// What the air does to a star's light on its way down to the ground (VISION.md REN-06): the
/// lower the sun, the longer its path through the air, so the dimmer and redder its light, as
/// at sunrise and sunset; and the higher the eye, the thinner the air above it. Deterministic.
/// </summary>
/// <remarks>
/// Each color is dimmed by how much air the light crosses (the air mass, after Kasten and
/// Young, 1989), blue most and red least, as by air with a light haze. The result is measured
/// against the sun straight overhead from sea level, so a high noon sun gives white at full
/// strength, and the star's own color is left to the caller.
/// </remarks>
public static class SunlightThroughAir
{
    /// <summary>
    /// How quickly the air thins with height, in km: a third as thick this much higher (about
    /// Earth's).
    /// </summary>
    public const double AirScaleHeightKm = 8.5;

    // How much of each color a straight-down path through all the air takes out (red, green,
    // blue): the air's own scattering plus a light haze.
    private const double RedDepth = 0.06, GreenDepth = 0.11, BlueDepth = 0.24;

    // How far below the horizon the sun's middle can go and still shine past the edge of the
    // world: half its width on Earth, in degrees.
    private const double DiscHalfDegrees = 0.5;

    /// <summary>
    /// How thick the air is <paramref name="heightKm"/> above the base, as a share of the
    /// air at the base (1 there, less higher up; 1 below it).
    /// </summary>
    public static double AirShare(double heightKm) =>
        Math.Exp(-Math.Max(heightKm, 0) / AirScaleHeightKm);

    /// <summary>
    /// How much air the light of a sun <paramref name="altitudeDegrees"/> above the horizon
    /// crosses, in units of the air straight up: 1 overhead, about 38 at the horizon (and no
    /// more below it).
    /// </summary>
    public static double AirMass(double altitudeDegrees)
    {
        double altitude = Math.Clamp(altitudeDegrees, 0, 90);
        return 1 / (Math.Sin(double.DegreesToRadians(altitude))
            + 0.50572 * Math.Pow(altitude + 6.07995, -1.6364));
    }

    /// <summary>
    /// The sunlight at an eye <paramref name="heightKm"/> above the base, with the sun
    /// <paramref name="altitudeDegrees"/> above the horizon: its color (the brightest of red,
    /// green and blue at 1) and its strength (0 once the sun has set, 1 for a sun overhead at
    /// sea level). With no air, white, at full strength while any of the sun is up.
    /// </summary>
    public static (double Red, double Green, double Blue, double Strength) At(
        double altitudeDegrees, double heightKm, bool hasAir)
    {
        double up = SmoothStep(-DiscHalfDegrees, DiscHalfDegrees, altitudeDegrees);
        if (!hasAir)
        {
            return (1, 1, 1, up);
        }

        // The air crossed beyond what a sun overhead at sea level crosses (none less).
        double extra = Math.Max(AirMass(altitudeDegrees) * AirShare(heightKm) - 1, 0);
        double red = Math.Exp(-RedDepth * extra);
        double green = Math.Exp(-GreenDepth * extra);
        double blue = Math.Exp(-BlueDepth * extra);
        double brightest = Math.Max(red, Math.Max(green, blue));
        double strength = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
        return (red / brightest, green / brightest, blue / brightest,
            Math.Min(strength, 1) * up);
    }

    private static double SmoothStep(double from, double to, double value)
    {
        double t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
