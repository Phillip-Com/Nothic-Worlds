namespace NothicWorlds.Core.Model;

/// <summary>
/// The kind of a star (VISION.md BOD-06; owner's choice: picked by type), which sets its color
/// and the color of its light. See <see cref="BodyAppearance.StarColor"/>.
/// </summary>
public enum StarType
{
    /// <summary>A small, cool, red star.</summary>
    RedDwarf,

    /// <summary>An orange star, a little cooler than the Sun.</summary>
    Orange,

    /// <summary>A yellow, Sun-like star (the default).</summary>
    Yellow,

    /// <summary>A hot white star.</summary>
    White,

    /// <summary>A very hot blue star.</summary>
    Blue,
}
