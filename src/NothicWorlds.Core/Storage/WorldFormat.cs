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
    public const int CurrentVersion = 5;

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
    ];

    public static string ProjectionName(MapProjection projection) => _projectionNames[projection];

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
}
