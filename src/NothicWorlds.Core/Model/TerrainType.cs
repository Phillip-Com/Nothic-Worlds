namespace NothicWorlds.Core.Model;

/// <summary>
/// A kind of terrain that can be painted onto bodies (VISION.md BOD-05), such as Forest. Each
/// world has its own list, which the user can rename, recolor, add to, and delete from
/// (owner's choice).
/// </summary>
/// <param name="Code">
/// What painted cells store (<see cref="TerrainGrid"/>): 1 to 255, unique in the world. 0 means
/// unpainted. Deleting a type clears its cells, so its code can be reused afterwards.
/// </param>
/// <param name="Name">The name shown in the Terrain panel.</param>
/// <param name="Color">How the terrain is drawn.</param>
/// <param name="Climate">How it affects the weather (VISION.md WTH-03).</param>
/// <param name="HeightMeters">
/// How high the ground is where it's painted, when the world's terrain shapes the ground
/// (VISION.md BOD-07; see <see cref="TerrainRelief"/>): meters above (or, negative, below) the
/// body's radius, <see cref="MinHeightMeters"/> to <see cref="MaxHeightMeters"/>.
/// </param>
/// <param name="Edge">
/// How it meets other terrain, from 0 (gentle: long, smooth slopes) to 1 (a cliff). Where two
/// types meet, the steeper edge wins.
/// </param>
public sealed record TerrainType(
    byte Code, string Name, RgbColor Color, ClimateKind Climate = ClimateKind.OpenLand,
    int HeightMeters = 0, double Edge = 0)
{
    /// <summary>The most terrain types a world can have (one per code).</summary>
    public const int MaxCount = byte.MaxValue;

    /// <summary>The longest name allowed.</summary>
    public const int MaxNameLength = 60;

    /// <summary>
    /// The lowest a type can set the ground, in meters (deeper than Earth's oceans).
    /// </summary>
    public const int MinHeightMeters = -12_000;

    /// <summary>The highest a type can set the ground, in meters (taller than Everest).</summary>
    public const int MaxHeightMeters = 12_000;

    /// <summary>
    /// The types every new world starts with (owner's choice), in list order.
    /// </summary>
    public static IReadOnlyList<TerrainType> Defaults { get; } =
    [
        new(1, "Ocean", new RgbColor(0x1F, 0x4E, 0x79), ClimateKind.Water, -3_000, 0.3),
        new(2, "Shallow Water", new RgbColor(0x3A, 0x86, 0xB8), ClimateKind.Water, -150),
        new(3, "Plains", new RgbColor(0xA8, 0xC6, 0x6C), ClimateKind.OpenLand, 150),
        new(4, "Fields", new RgbColor(0xD8, 0xC8, 0x78), ClimateKind.OpenLand, 150),
        new(5, "Forest", new RgbColor(0x2F, 0x6B, 0x35), ClimateKind.Forest, 300),
        new(6, "Jungle", new RgbColor(0x1E, 0x56, 0x31), ClimateKind.Forest, 200),
        new(7, "Hills", new RgbColor(0x8C, 0x9A, 0x5B), ClimateKind.OpenLand, 800, 0.15),
        new(8, "Mountains", new RgbColor(0x7D, 0x6E, 0x62), ClimateKind.Mountains, 2_500, 0.5),
        new(9, "Desert", new RgbColor(0xE3, 0xC7, 0x8F), ClimateKind.Desert, 400),
        new(10, "Swamp", new RgbColor(0x4F, 0x6B, 0x4A), ClimateKind.Wetland, 20),
        new(11, "Tundra", new RgbColor(0xA3, 0xA8, 0x8E), ClimateKind.OpenLand, 300),
        new(12, "Ice", new RgbColor(0xEE, 0xF3, 0xF7), ClimateKind.Ice, 1_000, 0.3),
    ];

    /// <summary>
    /// What's wrong with a world's list of terrain types, or null if it's usable: every code
    /// must be 1 to 255 and unique, and every name non-blank and at most
    /// <see cref="MaxNameLength"/> characters.
    /// </summary>
    public static string? Problem(IReadOnlyList<TerrainType> types)
    {
        if (types.Any(type => type.Code == 0))
        {
            return "a terrain type has code 0, which means unpainted";
        }

        if (types.Select(type => type.Code).Distinct().Count() != types.Count)
        {
            return "two terrain types share a code";
        }

        if (types.Any(type => string.IsNullOrWhiteSpace(type.Name)
                || type.Name.Length > MaxNameLength))
        {
            return $"a terrain type's name is blank or longer than {MaxNameLength} characters";
        }

        if (types.Any(type => type.HeightMeters is < MinHeightMeters or > MaxHeightMeters))
        {
            return $"a terrain type's height is outside {MinHeightMeters:N0} to " +
                $"{MaxHeightMeters:N0} m";
        }

        if (types.Any(type => !double.IsFinite(type.Edge) || type.Edge is < 0 or > 1))
        {
            return "a terrain type's edge is outside 0 (gentle) to 1 (cliff)";
        }

        return null;
    }

    /// <summary>
    /// The lowest code not used by <paramref name="types"/>, or null if all 255 are taken.
    /// </summary>
    public static byte? FreeCode(IEnumerable<TerrainType> types)
    {
        var used = types.Select(type => type.Code).ToHashSet();
        for (int code = 1; code <= byte.MaxValue; code++)
        {
            if (!used.Contains((byte)code))
            {
                return (byte)code;
            }
        }

        return null;
    }
}
