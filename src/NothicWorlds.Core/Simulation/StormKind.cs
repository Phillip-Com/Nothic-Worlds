namespace NothicWorlds.Core.Simulation;

/// <summary>The kinds of storm live weather makes (VISION.md WTH-02).</summary>
public enum StormKind
{
    /// <summary>
    /// A storm of the middle latitudes: a wide spiral of cloud with a trailing front, riding the
    /// westerlies toward the pole over a few days.
    /// </summary>
    Cyclone,

    /// <summary>
    /// A tropical storm (a hurricane or typhoon): small, fierce, with an eye. Forms over warm
    /// water late in summer, drifts west, then curves away from the equator; weakens over land.
    /// </summary>
    Tropical,
}
