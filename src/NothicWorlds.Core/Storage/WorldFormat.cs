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
    public const int CurrentVersion = 32;

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
        [BodyKind.Comet] = "comet",
        [BodyKind.WorldTree] = "world-tree",
    };

    private static readonly Dictionary<CalendarFit, string> _calendarFitNames = new()
    {
        [CalendarFit.YearLength] = "year-length",
        [CalendarFit.DayLength] = "day-length",
    };

    private static readonly Dictionary<ClimateKind, string> _climateNames = new()
    {
        [ClimateKind.OpenLand] = "open-land",
        [ClimateKind.Water] = "water",
        [ClimateKind.Forest] = "forest",
        [ClimateKind.Desert] = "desert",
        [ClimateKind.Wetland] = "wetland",
        [ClimateKind.Mountains] = "mountains",
        [ClimateKind.Ice] = "ice",
    };

    private static readonly Dictionary<ShapeKind, string> _shapeKindNames = new()
    {
        [ShapeKind.Sphere] = "sphere",
        [ShapeKind.Box] = "box",
        [ShapeKind.Cylinder] = "cylinder",
        [ShapeKind.Cone] = "cone",
    };

    private static readonly Dictionary<ShapeOperation, string> _shapeOperationNames = new()
    {
        [ShapeOperation.Add] = "add",
        [ShapeOperation.Cut] = "cut",
    };

    private static readonly Dictionary<BodyShape, string> _shapeNames = new()
    {
        [BodyShape.Sphere] = "sphere",
        [BodyShape.FlatDisc] = "flat-disc",
    };

    private static readonly Dictionary<RiverKind, string> _riverKindNames = new()
    {
        [RiverKind.Drawn] = "drawn",
        [RiverKind.Natural] = "natural",
    };

    private static readonly Dictionary<VisualStyle, string> _styleNames = new()
    {
        [VisualStyle.Painterly] = "painterly",
        [VisualStyle.Realistic] = "realistic",
        [VisualStyle.Simple] = "simple",
    };

    private static readonly Dictionary<LoreKind, string> _loreKindNames = new()
    {
        [LoreKind.Character] = "character",
        [LoreKind.Faction] = "faction",
        [LoreKind.Nation] = "nation",
        [LoreKind.Place] = "place",
        [LoreKind.Other] = "other",
    };

    private static readonly Dictionary<RelationshipKind, string> _relationshipKindNames = new()
    {
        [RelationshipKind.ParentOf] = "parent-of",
        [RelationshipKind.MarriedTo] = "married-to",
        [RelationshipKind.SiblingOf] = "sibling-of",
        [RelationshipKind.AllyOf] = "ally-of",
        [RelationshipKind.RivalOf] = "rival-of",
        [RelationshipKind.AtWarWith] = "at-war-with",
        [RelationshipKind.MemberOf] = "member-of",
        [RelationshipKind.Rules] = "rules",
        [RelationshipKind.Serves] = "serves",
        [RelationshipKind.Other] = "other",
    };

    private static readonly Dictionary<SurfacePattern, string> _patternNames = new()
    {
        [SurfacePattern.Plain] = "plain",
        [SurfacePattern.Rocky] = "rocky",
        [SurfacePattern.Banded] = "banded",
        [SurfacePattern.Icy] = "icy",
        [SurfacePattern.Cloudy] = "cloudy",
    };

    private static readonly Dictionary<StarType, string> _starTypeNames = new()
    {
        [StarType.RedDwarf] = "red-dwarf",
        [StarType.Orange] = "orange",
        [StarType.Yellow] = "yellow",
        [StarType.White] = "white",
        [StarType.Blue] = "blue",
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

        // 11 → 12: terrain types gained a "climate" (terrain-aware weather, M14). Types still
        // named like a default get its climate; the rest are open land.
        AddTerrainClimates,

        // 12 → 13: bodies gained an "appearance" (body appearance, M16). Planets keep the
        // ocean blue they always had, moons become grey and rocky (owner's choice), and stars
        // are yellow, as they always were.
        AddAppearances,

        // 13 → 14: calendars gained an optional "leap" rule (leap years, M17). Older calendars
        // have no leap years, so nothing changes.
        document => document,

        // 14 → 15: bodies can be comets (kind "comet", M18). Older worlds have none.
        document => document,

        // 15 → 16: planets and moons gained an optional "shape" (flat worlds, M19). Older bodies
        // are all spheres, which is what a missing shape means.
        document => document,

        // 16 → 17: planets and moons gained optional "rings" (astral features, M20). Older
        // bodies have none.
        document => document,

        // 17 → 18: stars gained optional "belts" (asteroid belts, M20). Older stars have none.
        document => document,

        // 18 → 19: the world gained optional "nebulas" (M20). Older skies are empty.
        document => document,

        // 19 → 20: bodies can be world trees (kind "world-tree", with a "tree", M21). Older
        // worlds have none.
        document => document,

        // 20 → 21: realms (M21): planets and moons gained an optional "branch", and orbits an
        // optional "height". Older bodies hang on nothing, and their orbits aren't lifted.
        document => document,

        // 21 → 22: bodies gained an optional "density" (M22, the stable orbit guide). Older
        // bodies have the typical density of their kind and size.
        document => document,

        // 22 → 23: surfaces gained optional sculpted "heights" (M24). Older bodies are flat.
        document => document,

        // 23 → 24: surfaces gained optional "shapes" added or cut (M25). Older bodies have none.
        document => document,

        // 24 → 25: worlds gained a "style" (M26). Older worlds are painterly, the default
        // (owner's choice: painterly for all), which a missing style already reads as.
        document => document,

        // 25 → 26: planets and moons gained "atmosphere" (M27, live weather). Older planets
        // have air and moons don't (owner's choice), which a missing value already reads as.
        document => document,

        // 26 → 27: journal entries gained an optional "kind", and worlds optional
        // "relationships" and "diagrams" (M31, lore diagrams). Older worlds have none.
        document => document,

        // 27 → 28: worlds gained a "starSeed" and optional "constellations" (M34, designed
        // night skies). Older worlds get a seed made from their id, so each keeps one fixed
        // sky, and have no constellations.
        AddStarSeed,

        // 28 → 29: terrain types gained a "height" and an "edge", and worlds an optional
        // "terrainShapesGround" (M35, terrain shapes the ground). Older worlds don't shape
        // their ground (a missing value is off); their types named like the defaults get the
        // default heights and edges, the rest stay level and gentle, ready for when it's on.
        AddTerrainHeights,

        // 29 → 30: terrain types gained a "variation" and a "featureSize", and planets and
        // moons an optional "waterLevel" (M36, peaks and water). Types named like the defaults
        // get the default variations and sizes, the rest stay level; no body has water.
        AddTerrainVariations,

        // 30 → 31: the world gained optional "rivers" and "lakes" (M42, rivers and lakes).
        // Older worlds have none.
        document => document,

        // 31 → 32: rivers gained an optional "depth", "depthVariation", "depthSpacing" and
        // "depthSmoothness" (BOD-11, river depth). Older rivers are Auto with an even bed.
        document => document,
    ];

    // The variations and feature sizes the version 30 upgrade gives types by name. Deliberately
    // a copy, so later changes to the defaults can't change old upgrades.
    private static readonly Dictionary<string, (int Variation, double SizeKm)>
        _version30Variations = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ocean"] = (800, 200),
            ["Shallow Water"] = (40, 60),
            ["Plains"] = (40, 80),
            ["Fields"] = (30, 80),
            ["Forest"] = (80, 50),
            ["Jungle"] = (100, 40),
            ["Hills"] = (350, 30),
            ["Mountains"] = (1_500, 40),
            ["Desert"] = (120, 30),
            ["Swamp"] = (5, 40),
            ["Tundra"] = (60, 60),
            ["Ice"] = (300, 60),
        };

    // The heights and edges the version 29 upgrade gives types by name. Deliberately a copy,
    // like _version12Climates, so later changes to the defaults can't change old upgrades.
    private static readonly Dictionary<string, (int Height, double Edge)> _version29Heights =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ocean"] = (-3_000, 0.3),
            ["Shallow Water"] = (-150, 0),
            ["Plains"] = (150, 0),
            ["Fields"] = (150, 0),
            ["Forest"] = (300, 0),
            ["Jungle"] = (200, 0),
            ["Hills"] = (800, 0.15),
            ["Mountains"] = (2_500, 0.5),
            ["Desert"] = (400, 0),
            ["Swamp"] = (20, 0),
            ["Tundra"] = (300, 0),
            ["Ice"] = (1_000, 0.3),
        };

    // The climates the version 12 upgrade gives types by name. Deliberately a copy, like
    // _version10TerrainTypes, so later changes to the defaults can't change old upgrades.
    private static readonly Dictionary<string, string> _version12Climates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ocean"] = "water",
            ["Shallow Water"] = "water",
            ["Forest"] = "forest",
            ["Jungle"] = "forest",
            ["Mountains"] = "mountains",
            ["Desert"] = "desert",
            ["Swamp"] = "wetland",
            ["Ice"] = "ice",
        };

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

    // A damaged id is left for loading to refuse, with its usual message.
    private static JsonObject AddStarSeed(JsonObject document)
    {
        if (document["id"]?.GetValueKind() == System.Text.Json.JsonValueKind.String
            && Guid.TryParse((string?)document["id"], out Guid id))
        {
            document["starSeed"] ??= Simulation.StarField.SeedFor(id);
        }

        return document;
    }

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

    private static JsonObject AddAppearances(JsonObject document)
    {
        if (document["bodies"] is JsonArray bodies)
        {
            foreach (JsonObject body in bodies.OfType<JsonObject>())
            {
                body["appearance"] ??= body["kind"]?.GetValue<string>() switch
                {
                    "star" => new JsonObject { ["starType"] = "yellow" },
                    "moon" => new JsonObject { ["color"] = "#8A8A8A", ["pattern"] = "rocky" },
                    _ => new JsonObject { ["color"] = "#214573", ["pattern"] = "plain" },
                };
            }
        }

        return document;
    }

    private static JsonObject AddTerrainVariations(JsonObject document)
    {
        if (document["terrainTypes"] is JsonArray types)
        {
            foreach (JsonObject type in types.OfType<JsonObject>())
            {
                if (TypeName(type) is string name
                    && _version30Variations.TryGetValue(name, out var look))
                {
                    type["variation"] ??= look.Variation;
                    type["featureSize"] ??= look.SizeKm;
                }
            }
        }

        return document;
    }

    // A terrain type's name in an older document, trimmed, or null if it isn't text (loading
    // then refuses it with its usual message).
    private static string? TypeName(JsonObject type) =>
        type["name"]?.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? type["name"]!.GetValue<string>().Trim()
            : null;

    private static JsonObject AddTerrainHeights(JsonObject document)
    {
        if (document["terrainTypes"] is JsonArray types)
        {
            foreach (JsonObject type in types.OfType<JsonObject>())
            {
                string? name = type["name"]?.GetValueKind() == System.Text.Json.JsonValueKind.String
                    ? type["name"]!.GetValue<string>().Trim()
                    : null;
                if (name is not null && _version29Heights.TryGetValue(name, out var look))
                {
                    type["height"] ??= look.Height;
                    type["edge"] ??= look.Edge;
                }
            }
        }

        return document;
    }

    private static JsonObject AddTerrainClimates(JsonObject document)
    {
        if (document["terrainTypes"] is JsonArray types)
        {
            foreach (JsonObject type in types.OfType<JsonObject>())
            {
                string? name = type["name"]?.GetValue<string>()?.Trim();
                type["climate"] ??= name is not null
                    && _version12Climates.TryGetValue(name, out string? climate)
                        ? climate
                        : "open-land";
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

    public static string ClimateName(ClimateKind kind) => _climateNames[kind];

    public static string ShapeName(BodyShape shape) => _shapeNames[shape];

    public static BodyShape ParseShape(string? name) => Parse(_shapeNames, name, "body shape");

    public static string ShapeKindName(ShapeKind kind) => _shapeKindNames[kind];

    public static ShapeKind ParseShapeKind(string? name) => Parse(_shapeKindNames, name, "shape");

    public static string ShapeOperationName(ShapeOperation operation) =>
        _shapeOperationNames[operation];

    public static ShapeOperation ParseShapeOperation(string? name) =>
        Parse(_shapeOperationNames, name, "shape operation");

    public static string StyleName(VisualStyle style) => _styleNames[style];

    public static string RiverKindName(RiverKind kind) => _riverKindNames[kind];

    public static RiverKind ParseRiverKind(string? name) =>
        Parse(_riverKindNames, name, "river kind");

    public static VisualStyle ParseStyle(string? name) => Parse(_styleNames, name, "style");

    public static string LoreKindName(LoreKind kind) => _loreKindNames[kind];

    public static LoreKind ParseLoreKind(string? name) =>
        Parse(_loreKindNames, name, "journal entry kind");

    public static string RelationshipKindName(RelationshipKind kind) =>
        _relationshipKindNames[kind];

    public static RelationshipKind ParseRelationshipKind(string? name) =>
        Parse(_relationshipKindNames, name, "relationship kind");

    public static string PatternName(SurfacePattern pattern) => _patternNames[pattern];

    public static SurfacePattern ParsePattern(string? name) =>
        Parse(_patternNames, name, "surface pattern");

    public static string StarTypeName(StarType type) => _starTypeNames[type];

    public static StarType ParseStarType(string? name) => Parse(_starTypeNames, name, "star type");

    public static ClimateKind ParseClimate(string? name) =>
        Parse(_climateNames, name, "terrain climate");

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
    /// The name of a body's height image inside the world file, e.g.
    /// <c>heights/1a2b….png</c> (named after the body).
    /// </summary>
    public static string HeightsEntryName(Guid bodyId) => $"heights/{bodyId:N}.png";

    /// <summary>True if a height image name has the expected safe form.</summary>
    public static bool IsValidHeightsName(string? name)
    {
        return name is not null && HeightsNamePattern().IsMatch(name);
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

    // The same for height images.
    [GeneratedRegex(@"^heights/[0-9a-f]{32}\.png$")]
    private static partial Regex HeightsNamePattern();
}
