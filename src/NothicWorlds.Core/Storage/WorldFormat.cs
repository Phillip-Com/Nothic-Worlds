using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// The fixed parts of the <c>.nworld</c> file format (docs/world-format.md): its version, entry
/// names, size limits, the names written for enums, and upgrades from older versions.
/// </summary>
/// <remarks>
/// <b>Never change or remove an existing name in this file.</b> Files already saved depend on
/// them. To change the format, raise <see cref="CurrentVersion"/> and add a migration.
/// </remarks>
internal static partial class WorldFormat
{
    /// <summary>The format version this code writes, and the newest it can read.</summary>
    public const int CurrentVersion = 11;

    /// <summary>Name of the world data entry inside the file.</summary>
    public const string DocumentEntryName = "world.json";

    /// <summary>
    /// Largest world data entry accepted (guards against damaged or hostile files).
    /// </summary>
    public const long MaxDocumentBytes = 16L * 1024 * 1024;

    // Written to files for each map type. Deliberately not the C# enum names, so renaming
    // code never changes the file format.
    private static readonly Dictionary<MapProjection, string> _projectionNames = new()
    {
        [MapProjection.Equirectangular] = "equirectangular",
        [MapProjection.Mercator] = "mercator",
        [MapProjection.Robinson] = "robinson",
        [MapProjection.WinkelTripel] = "winkel-tripel",
        [MapProjection.Mollweide] = "mollweide",
        [MapProjection.GallPeters] = "gall-peters",
        [MapProjection.Polar] = "polar",
        [MapProjection.TwoHemispheres] = "two-hemispheres",
    };

    private static readonly Dictionary<BodyKind, string> _bodyKindNames = new()
    {
        [BodyKind.Planet] = "planet",
        [BodyKind.Star] = "star",
        [BodyKind.Moon] = "moon",
    };

    private static readonly Dictionary<CalendarFit, string> _calendarFitNames = new()
    {
        [CalendarFit.YearLength] = "year-length",
        [CalendarFit.DayLength] = "day-length",
    };

    // Upgrades older documents one version at a time: entry 0 turns version 1 into version 2,
    // and so on. See docs/world-format.md, "Version history".
    private static readonly Func<JsonObject, JsonObject>[] _migrations =
    [
        // 1 → 2: maps gained an optional "calibration" (MAP-05). Version 1 maps simply have
        // none, so nothing needs changing beyond the version number.
        document => document,

        // 2 → 3: surfaces gained an optional "pieces" list (MAP-02). Version 2 worlds have
        // none, so again only the version number changes.
        document => document,

        // 3 → 4: pieces gained an optional "warp" (Edit Points, MAP-02). Version 3 pieces are
        // simply unwarped.
        document => document,

        // 4 → 5: bodies gained a size, day length, and axial tilt, plus an optional orbit, and
        // the world a clock (star systems, M4). Older worlds hold one planet, which gets Earth's
        // size, a 24-hour day, and no tilt, and stays at the center with no orbit.
        AddBodyDefaults,

        // 5 → 6: bodies gained an axial tilt direction and an optional calendar (calendars and
        // seasons, M5). Older bodies lean toward direction 0 and count plain days.
        document => SetOnEveryBody(document, "axialTiltDirection", 0.0),

        // 6 → 7: the world gained optional "journal", "timelines", and "events" lists
        // (journals and timelines, M7). Older worlds simply have none.
        document => document,

        // 7 → 8: the world gained an optional "regions" list, and places an optional "region"
        // (region outlines, M8). Older worlds have none.
        document => document,

        // 8 → 9: bodies gained an average temperature, and the world an optional "weatherPins"
        // list (weather pins, M9). Older bodies get Earth's 15 °C, and worlds have no pins.
        document => SetOnEveryBody(document, "averageTemperature", 15.0),

        // 9 → 10: the world gained a "terrainTypes" list, and surfaces an optional "terrain"
        // image (terrain painting, M11). Older worlds get the version 10 default types and no
        // painting.
        AddDefaultTerrainTypes,

        // 10 → 11: calendars gained an optional "fit" and "monthMoon" (calendar fitting, M12).
        // Older calendars aren't fitted, so nothing changes.
        document => document,
    ];

    // The terrain types worlds got when version 10 arrived. Deliberately a copy, not
    // TerrainType.Defaults: if the defaults for new worlds change later, upgrading an old
    // file must still give the same result.
    private static readonly (int Code, string Name, string Color)[] _version10TerrainTypes =
    [
        (1, "Ocean", "#1F4E79"),
        (2, "Shallow Water", "#3A86B8"),
        (3, "Plains", "#A8C66C"),
        (4, "Fields", "#D8C878"),
        (5, "Forest", "#2F6B35"),
        (6, "Jungle", "#1E5631"),
        (7, "Hills", "#8C9A5B"),
        (8, "Mountains", "#7D6E62"),
        (9, "Desert", "#E3C78F"),
        (10, "Swamp", "#4F6B4A"),
        (11, "Tundra", "#A3A88E"),
        (12, "Ice", "#EEF3F7"),
    ];

    public static string ProjectionName(MapProjection projection) => _projectionNames[projection];

    private static JsonObject SetOnEveryBody(JsonObject document, string name, double value)
    {
        if (document["bodies"] is JsonArray bodies)
        {
            foreach (JsonObject body in bodies.OfType<JsonObject>())
            {
                body[name] ??= value;
            }
        }

        return document;
    }

    private static JsonObject AddDefaultTerrainTypes(JsonObject document)
    {
        var types = new JsonArray();
        foreach ((int code, string name, string color) in _version10TerrainTypes)
        {
            types.Add(new JsonObject { ["code"] = code, ["name"] = name, ["color"] = color });
        }

        document["terrainTypes"] ??= types;
        return document;
    }

    private static JsonObject AddBodyDefaults(JsonObject document)
    {
        if (document["bodies"] is JsonArray bodies)
        {
            foreach (JsonObject body in bodies.OfType<JsonObject>())
            {
                body["radiusKm"] ??= 6371.0;
                body["dayLengthHours"] ??= 24.0;
                body["axialTilt"] ??= 0.0;
            }
        }

        return document;
    }

    public static MapProjection ParseProjection(string? name) =>
        Parse(_projectionNames, name, "map type");

    public static string BodyKindName(BodyKind kind) => _bodyKindNames[kind];

    public static BodyKind ParseBodyKind(string? name) => Parse(_bodyKindNames, name, "body kind");

    /// <summary>The name written for a calendar fit, or null for none (left out).</summary>
    public static string? CalendarFitName(CalendarFit fit) =>
        fit == CalendarFit.None ? null : _calendarFitNames[fit];

    /// <summary>Reads a calendar fit's name; a missing one means not fitted.</summary>
    public static CalendarFit ParseCalendarFit(string? name) =>
        name is null ? CalendarFit.None : Parse(_calendarFitNames, name, "calendar fit");

    /// <summary>
    /// Creates a new, unique asset name for an image with the given file extension.
    /// </summary>
    /// <exception cref="ArgumentException">The extension isn't a supported image type.</exception>
    public static string NewAssetName(string extension)
    {
        string normalized = extension.TrimStart('.').ToLowerInvariant();
        if (!MapImageRules.SupportedExtensions.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unsupported image type '{extension}'.", nameof(extension));
        }

        return $"assets/{Guid.NewGuid():N}.{normalized}";
    }

    /// <summary>
    /// True if an asset name has the expected safe form, e.g. <c>assets/1a2b….png</c>.
    /// </summary>
    public static bool IsValidAssetName(string? name)
    {
        return name is not null && AssetNamePattern().IsMatch(name);
    }

    /// <summary>
    /// The name of a body's terrain image inside the world file, e.g.
    /// <c>terrain/1a2b….png</c> (named after the body).
    /// </summary>
    public static string TerrainEntryName(Guid bodyId) => $"terrain/{bodyId:N}.png";

    /// <summary>True if a terrain image name has the expected safe form.</summary>
    public static bool IsValidTerrainName(string? name)
    {
        return name is not null && TerrainNamePattern().IsMatch(name);
    }

    /// <summary>
    /// Checks a document's format version and upgrades it to <see cref="CurrentVersion"/>.
    /// </summary>
    /// <exception cref="WorldFileException">
    /// The version is missing, invalid, or too new.
    /// </exception>
    public static JsonObject Upgrade(JsonObject document)
    {
        if (document["formatVersion"] is not JsonValue versionValue
            || !versionValue.TryGetValue(out int version)
            || version < 1)
        {
            throw new WorldFileException(
                "The world data is damaged: its format version is missing.");
        }

        if (version > CurrentVersion)
        {
            throw new WorldFileException(
                "This world was saved by a newer version of Nothic Worlds (file format " +
                $"{version}; this version reads up to {CurrentVersion}). " +
                "Update the app to open it.");
        }

        for (; version < CurrentVersion; version++)
        {
            document = _migrations[version - 1](document);
            document["formatVersion"] = version + 1;
        }

        return document;
    }

    private static T Parse<T>(Dictionary<T, string> names, string? name, string what)
        where T : struct
    {
        foreach ((T value, string written) in names)
        {
            if (written == name)
            {
                return value;
            }
        }

        throw new WorldFileException($"The world data is damaged: unknown {what} '{name}'.");
    }

    // Only lowercase hex names in the assets folder, with a supported image extension. This
    // also rules out paths that try to escape the file (e.g. "../").
    [GeneratedRegex(@"^assets/[0-9a-f]{32}\.(png|jpg|jpeg|webp)$")]
    private static partial Regex AssetNamePattern();

    // The same safety rule for terrain images, which are always PNG.
    [GeneratedRegex(@"^terrain/[0-9a-f]{32}\.png$")]
    private static partial Regex TerrainNamePattern();
}
