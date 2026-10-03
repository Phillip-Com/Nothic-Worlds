namespace NothicWorlds.Core.Model;

/// <summary>
/// How a planet or moon without a map looks (VISION.md BOD-06; owner's choice: a color plus a
/// pattern from a short list), drawn over its <see cref="BodyAppearance.Color"/>.
/// </summary>
public enum SurfacePattern
{
    /// <summary>One even color.</summary>
    Plain,

    /// <summary>Mottled rock with craters, like the Moon.</summary>
    Rocky,

    /// <summary>Bands along the latitudes, like a gas giant.</summary>
    Banded,

    /// <summary>Bright ice with fine cracks.</summary>
    Icy,

    /// <summary>Swirling clouds over the color.</summary>
    Cloudy,
}
