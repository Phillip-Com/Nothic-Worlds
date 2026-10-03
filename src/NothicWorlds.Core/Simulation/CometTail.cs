namespace NothicWorlds.Core.Simulation;

/// <summary>
/// How long and bright a comet's tail is (VISION.md EVT-02; owner's choice: a glowing tail
/// pointing away from the star, longer near it). As with real comets, the star's heat boils off
/// more ice the closer the comet comes, so the tail grows with the inverse square of its
/// distance, and far out there's none at all.
/// </summary>
public static class CometTail
{
    /// <summary>The Earth–Sun distance (one astronomical unit), in km.</summary>
    public const double KmPerAu = 149_597_870.7;

    // About the length of Halley's comet's tail as it passed the Earth's distance.
    private const double LengthAtOneAuKm = 2e7;

    // The longest tails seen are about this long (the Great Comet of 1843).
    private const double MaxLengthKm = 1.5e8;

    // Full brightness within this distance, fading to nothing at the second.
    private const double FullBrightnessAu = 3;
    private const double GoneAu = 5;

    /// <summary>
    /// The tail's length in km when the comet is <paramref name="distanceKm"/> from its star
    /// (0 when it's too far out to have a tail).
    /// </summary>
    public static double LengthKm(double distanceKm)
    {
        if (Brightness(distanceKm) <= 0)
        {
            return 0;
        }

        double au = distanceKm / KmPerAu;
        return Math.Min(MaxLengthKm, LengthAtOneAuKm / (au * au));
    }

    /// <summary>
    /// How bright the tail is, from 0 to 1, when the comet is <paramref name="distanceKm"/> from
    /// its star: full within 3 AU, fading to nothing at 5 AU.
    /// </summary>
    public static double Brightness(double distanceKm)
    {
        double au = distanceKm / KmPerAu;
        return Math.Clamp((GoneAu - au) / (GoneAu - FullBrightnessAu), 0, 1);
    }
}
