namespace NothicWorlds.Core.Model;

/// <summary>
/// How a body looks (VISION.md BOD-06): a planet's or moon's color and surface pattern (shown
/// where it has no map), or a star's type. Immutable.
/// </summary>
/// <param name="Color">A planet's or moon's base color (stars ignore it).</param>
/// <param name="Pattern">A planet's or moon's surface pattern (stars ignore it).</param>
/// <param name="StarType">A star's type (planets and moons ignore it).</param>
public sealed record BodyAppearance(RgbColor Color, SurfacePattern Pattern, StarType StarType)
{
    /// <summary>The ocean blue planets have always had (matches <c>planet.gdshader</c>).</summary>
    public static readonly RgbColor PlanetBlue = new(0x21, 0x45, 0x73);

    /// <summary>The grey moons start with.</summary>
    public static readonly RgbColor MoonGrey = new(0x8A, 0x8A, 0x8A);

    /// <summary>
    /// The look a new body of a kind starts with (owner's choice): planets ocean blue and
    /// plain, moons grey and rocky, stars yellow.
    /// </summary>
    public static BodyAppearance DefaultFor(BodyKind kind) => kind switch
    {
        BodyKind.Moon => new BodyAppearance(MoonGrey, SurfacePattern.Rocky, StarType.Yellow),
        _ => new BodyAppearance(PlanetBlue, SurfacePattern.Plain, StarType.Yellow),
    };

    /// <summary>The color of a type of star, and of its light.</summary>
    public static RgbColor StarColor(StarType type) => type switch
    {
        StarType.RedDwarf => new RgbColor(0xFF, 0x8A, 0x5C),
        StarType.Orange => new RgbColor(0xFF, 0xB8, 0x6E),
        StarType.White => new RgbColor(0xF2, 0xF4, 0xFF),
        StarType.Blue => new RgbColor(0xA6, 0xC2, 0xFF),
        _ => new RgbColor(0xFF, 0xDB, 0x8C),  // Yellow: the color stars have always had
    };
}
