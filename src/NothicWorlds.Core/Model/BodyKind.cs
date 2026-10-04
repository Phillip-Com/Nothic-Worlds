namespace NothicWorlds.Core.Model;

/// <summary>What kind of celestial body something is (VISION.md BOD-01, BOD-02).</summary>
public enum BodyKind
{
    Planet,
    Star,
    Moon,

    /// <summary>
    /// A small icy body on a long, elongated orbit around a star (VISION.md EVT-02). It has no
    /// surface or calendar, and nothing circles it; its dust makes meteor showers.
    /// </summary>
    Comet,

    /// <summary>
    /// A vast glowing tree standing in the system (VISION.md BOD-02), whose great branches can
    /// hold realms. Its radius is half its height, and its day is how long it takes to turn.
    /// Its look is <see cref="Body.Tree"/>.
    /// </summary>
    WorldTree,
}
