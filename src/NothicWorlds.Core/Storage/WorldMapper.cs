using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Storage;

/// <summary>
/// Converts between the in-memory <see cref="World"/> and the file's
/// <see cref="WorldDocument"/>. Everything read from a file is validated here, because world
/// files are untrusted input (CLAUDE.md §4).
/// </summary>
internal static class WorldMapper
{
    public static WorldDocument ToDocument(World world)
    {
        return new WorldDocument
        {
            FormatVersion = WorldFormat.CurrentVersion,
            Id = world.Id,
            Name = world.Name,
            CreatedUtc = world.CreatedUtc,
            ModifiedUtc = world.ModifiedUtc,
            TimeDays = world.TimeDays,
            Style = WorldFormat.StyleName(world.Style),
            Bodies = world.Bodies.Select(ToDocument).ToList(),
            TerrainTypes = NullIfEmpty(world.TerrainTypes.Select(ToDocument)),
            Regions = NullIfEmpty(world.Regions.Select(ToDocument)),
            WeatherPins = NullIfEmpty(world.WeatherPins.Select(ToDocument)),
            Rivers = NullIfEmpty(world.Rivers.Select(r => (RiverDocument?)ToDocument(r))),
            Lakes = NullIfEmpty(world.Lakes.Select(l => (LakeDocument?)ToDocument(l))),
            Journal = NullIfEmpty(world.Journal.Select(ToDocument)),
            Timelines = NullIfEmpty(world.Timelines.Select(ToDocument)),
            Events = NullIfEmpty(world.Events.Select(ToDocument)),
            Relationships = NullIfEmpty(world.Relationships.Select(ToDocument)),
            Diagrams = NullIfEmpty(world.Diagrams.Select(ToDocument)),
            Nebulas = world.Nebulas.Count == 0
                ? null
                : [.. world.Nebulas.Select(nebula => new NebulaDocument
                {
                    Id = nebula.Id,
                    Name = nebula.Name,
                    Latitude = nebula.LatitudeDegrees,
                    Longitude = nebula.LongitudeDegrees,
                    Size = nebula.SizeDegrees,
                    Brightness = nebula.Brightness,
                    Color = nebula.Color.ToHex(),
                    SecondColor = nebula.SecondColor.ToHex(),
                })],
            StarSeed = world.StarSeed,
            TerrainShapesGround = world.TerrainShapesGround ? true : null,
            Constellations = NullIfEmpty(world.Constellations.Select(c =>
                (ConstellationDocument?)new ConstellationDocument
                {
                    Id = c.Id,
                    Name = c.Name,
                    Lines = [.. c.Lines.Select(line => (int[]?)[line.From, line.To])],
                })),
            View = world.View is null ? null : ToDocument(world.View),
        };
    }

    /// <param name="document">The world data read from the file.</param>
    /// <param name="readTerrain">
    /// Reads a body's terrain image from the file, given its (already validated) name.
    /// </param>
    /// <param name="readHeights">Reads a body's height image, likewise.</param>
    /// <exception cref="WorldFileException">The document has missing or invalid data.</exception>
    public static World ToWorld(WorldDocument document, Func<string, TerrainGrid> readTerrain,
        Func<string, HeightGrid> readHeights)
    {
        Require(document.Bodies is { Count: > 0 }, "it has no bodies");
        Require(document.TimeDays is null || double.IsFinite(document.TimeDays.Value),
            "its clock isn't a number");

        var world = new World
        {
            Id = document.Id,
            Name = RequireText(document.Name, "world name"),
            CreatedUtc = document.CreatedUtc,
            ModifiedUtc = document.ModifiedUtc,
            View = document.View is null ? null : ToView(document.View),
            TimeDays = document.TimeDays ?? 0,
            StarSeed = document.StarSeed,
            TerrainShapesGround = document.TerrainShapesGround ?? false,
            Style = document.Style is null
                ? VisualStyle.Painterly
                : WorldFormat.ParseStyle(document.Style),
        };
        world.Bodies.AddRange(
            document.Bodies.Select(body => ToBody(body, readTerrain, readHeights)));
        RequireNoProblem(Simulation.SystemHierarchy.Problem(world.Bodies));
        RequireNoProblem(Simulation.Realms.Problem(world.Bodies));
        Simulation.Realms.Apply(world.Bodies);
        Require(world.Bodies.All(body => body.Calendar?.MonthMoonId is not Guid moon
                || world.Bodies.Any(other => other.Id == moon)),
            "a calendar's month moon doesn't exist");
        world.TerrainTypes.AddRange((document.TerrainTypes ?? []).Select(ToTerrainType));
        RequireNoProblem(TerrainType.Problem(world.TerrainTypes));
        world.Regions.AddRange((document.Regions ?? []).Select(ToRegion));
        world.WeatherPins.AddRange((document.WeatherPins ?? []).Select(ToWeatherPin));
        world.Nebulas.AddRange((document.Nebulas ?? []).Select(ToNebula));
        RequireNoProblem(Nebula.Problem(world.Nebulas));
        world.Constellations.AddRange((document.Constellations ?? []).Select(ToConstellation));
        RequireNoProblem(Constellation.Problem(world.Constellations, world.StarSeed));
        RequireNoProblem(WeatherPin.Problem(world));
        world.Rivers.AddRange((document.Rivers ?? []).Select(ToRiver));
        world.Lakes.AddRange((document.Lakes ?? []).Select(ToLake));
        RequireNoProblem(WaterRules.Problem(world));
        world.Journal.AddRange((document.Journal ?? []).Select(ToEntry));
        world.Timelines.AddRange((document.Timelines ?? []).Select(ToTimeline));
        world.Events.AddRange((document.Events ?? []).Select(ToEvent));
        world.Relationships.AddRange((document.Relationships ?? []).Select(ToRelationship));
        world.Diagrams.AddRange((document.Diagrams ?? []).Select(ToDiagram));
        RequireNoProblem(LoreRules.Problem(world));
        return world;
    }

    // Lists that are empty are left out of the file.
    private static List<T>? NullIfEmpty<T>(IEnumerable<T> items)
    {
        List<T> list = [.. items];
        return list.Count == 0 ? null : list;
    }

    private static TerrainTypeDocument ToDocument(TerrainType type)
    {
        return new TerrainTypeDocument
        {
            Code = type.Code,
            Name = type.Name,
            Color = type.Color.ToHex(),
            Climate = WorldFormat.ClimateName(type.Climate),
            Height = type.HeightMeters == 0 ? null : type.HeightMeters,
            Edge = type.Edge == 0 ? null : type.Edge,
            Variation = type.VariationMeters == 0 ? null : type.VariationMeters,
            FeatureSize = type.FeatureSizeKm == TerrainType.DefaultFeatureSizeKm
                ? null
                : type.FeatureSizeKm,
        };
    }

    // Checked fully afterwards by TerrainType.Problem.
    private static TerrainType ToTerrainType(TerrainTypeDocument document)
    {
        Require(document is not null, "a terrain type is empty");
        Require(document!.Code is >= 1 and <= byte.MaxValue,
            $"a terrain type's code ({document.Code}) isn't 1 to 255");
        Require(RgbColor.TryParseHex(document.Color, out RgbColor color),
            $"invalid terrain color '{document.Color}'");
        return new TerrainType((byte)document.Code, document.Name ?? "", color,
            WorldFormat.ParseClimate(document.Climate), document.Height ?? 0,
            document.Edge ?? 0, document.Variation ?? 0,
            document.FeatureSize ?? TerrainType.DefaultFeatureSizeKm);
    }

    private static WeatherPinDocument ToDocument(WeatherPin pin)
    {
        return new WeatherPinDocument
        {
            Id = pin.Id,
            Body = pin.BodyId,
            Name = pin.Name,
            Latitude = pin.Spot.LatitudeDegrees,
            Longitude = pin.Spot.LongitudeDegrees,
        };
    }

    // Checked fully afterwards by WeatherPin.Problem.
    private static WeatherPin ToWeatherPin(WeatherPinDocument document)
    {
        Require(document is not null, "a weather pin is empty");
        Require(double.IsFinite(document!.Latitude) && document.Latitude is >= -90 and <= 90
                && double.IsFinite(document.Longitude)
                && document.Longitude is >= -180 and <= 180,
            "a weather pin's spot is invalid");
        return new WeatherPin
        {
            Id = document.Id,
            BodyId = document.Body,
            Name = document.Name ?? "",
            Spot = new GeoCoordinate(document.Latitude, document.Longitude),
        };
    }

    private static RiverDocument ToDocument(River river)
    {
        return new RiverDocument
        {
            Id = river.Id,
            Body = river.BodyId,
            Name = river.Name,
            Kind = WorldFormat.RiverKindName(river.Kind),
            Points = [.. river.Points
                .Select(p => (double[]?)[p.LatitudeDegrees, p.LongitudeDegrees])],
            Width = river.WidthKm == 1 ? null : river.WidthKm,
        };
    }

    // Checked fully afterwards by WaterRules.Problem (through River.Problem).
    private static River ToRiver(RiverDocument? document)
    {
        Require(document?.Points is not null, "a river is incomplete");
        Require(document!.Points.All(p => p is { Length: 2 } && IsOnGlobe(p[0], p[1])),
            "a river's course is invalid");
        return new River
        {
            Id = document.Id,
            BodyId = document.Body,
            Name = document.Name ?? "",
            Kind = WorldFormat.ParseRiverKind(document.Kind),
            Points = [.. document.Points.Select(p => new GeoCoordinate(p![0], p[1]))],
            WidthKm = document.Width ?? 1,
        };
    }

    private static bool IsOnGlobe(double latitude, double longitude) =>
        double.IsFinite(latitude) && latitude is >= -90 and <= 90
        && double.IsFinite(longitude) && longitude is >= -180 and <= 180;

    private static LakeDocument ToDocument(Lake lake)
    {
        return new LakeDocument
        {
            Id = lake.Id,
            Body = lake.BodyId,
            Name = lake.Name,
            Latitude = lake.Spot.LatitudeDegrees,
            Longitude = lake.Spot.LongitudeDegrees,
            Level = lake.LevelMeters,
            FlowsOut = lake.FlowsOut ? true : null,
        };
    }

    // Checked fully afterwards by WaterRules.Problem (through Lake.Problem).
    private static Lake ToLake(LakeDocument? document)
    {
        Require(document is not null, "a lake is empty");
        Require(IsOnGlobe(document!.Latitude, document.Longitude), "a lake's spot is invalid");
        return new Lake
        {
            Id = document.Id,
            BodyId = document.Body,
            Name = document.Name ?? "",
            Spot = new GeoCoordinate(document.Latitude, document.Longitude),
            LevelMeters = document.Level,
            FlowsOut = document.FlowsOut ?? false,
        };
    }

    private static RegionDocument ToDocument(Region region)
    {
        return new RegionDocument
        {
            Id = region.Id,
            Body = region.BodyId,
            Name = region.Name,
            Notes = region.Notes.Length == 0 ? null : region.Notes,
            Color = region.Color.ToHex(),
            Corners = [.. region.Corners
                .Select(c => new[] { c.LatitudeDegrees, c.LongitudeDegrees })],
        };
    }

    // Checked fully afterwards by LoreRules.Problem (through Region.Problem).
    private static Region ToRegion(RegionDocument document)
    {
        Require(document?.Corners is not null, "a region is incomplete");
        Require(RgbColor.TryParseHex(document!.Color, out RgbColor color),
            $"invalid region color '{document.Color}'");
        Require(document.Corners!.All(c => c is { Length: 2 }
                && double.IsFinite(c[0]) && c[0] is >= -90 and <= 90
                && double.IsFinite(c[1]) && c[1] is >= -180 and <= 180),
            "a region's outline is invalid");
        return new Region
        {
            Id = document.Id,
            BodyId = document.Body,
            Name = document.Name ?? "",
            Notes = document.Notes ?? "",
            Color = color,
            Corners = [.. document.Corners.Select(c => new GeoCoordinate(c[0], c[1]))],
        };
    }

    private static JournalEntryDocument ToDocument(JournalEntry entry)
    {
        return new JournalEntryDocument
        {
            Id = entry.Id,
            Title = entry.Title,
            Text = entry.Text.Length == 0 ? null : entry.Text,
            Kind = entry.Kind is LoreKind kind ? WorldFormat.LoreKindName(kind) : null,
            Location = entry.Location is LoreLocation location ? ToDocument(location) : null,
            CreatedUtc = entry.CreatedUtc,
            EditedUtc = entry.EditedUtc,
        };
    }

    // Checked fully afterwards by LoreRules.Problem (through JournalEntry.Problem).
    private static JournalEntry ToEntry(JournalEntryDocument document)
    {
        Require(document is not null, "a journal entry is empty");
        return new JournalEntry
        {
            Id = document!.Id,
            Title = document.Title ?? "",
            Text = document.Text ?? "",
            Kind = document.Kind is null ? null : WorldFormat.ParseLoreKind(document.Kind),
            Location = document.Location is LocationDocument location
                ? ToLocation(location)
                : null,
            CreatedUtc = document.CreatedUtc,
            EditedUtc = document.EditedUtc,
        };
    }

    private static TimelineDocument ToDocument(Timeline timeline)
    {
        return new TimelineDocument
        {
            Id = timeline.Id,
            Name = timeline.Name,
            Color = timeline.Color.ToHex(),
            Hidden = timeline.Hidden ? true : null,
        };
    }

    private static Timeline ToTimeline(TimelineDocument document)
    {
        Require(document is not null, "a timeline is empty");
        Require(RgbColor.TryParseHex(document!.Color, out RgbColor color),
            $"invalid timeline color '{document.Color}'");
        return new Timeline
        {
            Id = document.Id,
            Name = document.Name ?? "",
            Color = color,
            Hidden = document.Hidden ?? false,
        };
    }

    private static EventDocument ToDocument(TimelineEvent timelineEvent)
    {
        return new EventDocument
        {
            Id = timelineEvent.Id,
            Timeline = timelineEvent.TimelineId,
            Title = timelineEvent.Title,
            Description = timelineEvent.Description.Length == 0
                ? null
                : timelineEvent.Description,
            Start = timelineEvent.StartDays,
            End = timelineEvent.EndDays,
            Location = timelineEvent.Location is LoreLocation location
                ? ToDocument(location)
                : null,
            Entries = timelineEvent.EntryIds.Count == 0 ? null : [.. timelineEvent.EntryIds],
        };
    }

    private static TimelineEvent ToEvent(EventDocument document)
    {
        Require(document is not null, "a timeline event is empty");
        return new TimelineEvent
        {
            Id = document!.Id,
            TimelineId = document.Timeline,
            Title = document.Title ?? "",
            Description = document.Description ?? "",
            StartDays = document.Start,
            EndDays = document.End,
            Location = document.Location is LocationDocument location
                ? ToLocation(location)
                : null,
            EntryIds = document.Entries is null ? [] : [.. document.Entries],
        };
    }

    private static RelationshipDocument ToDocument(Relationship relationship)
    {
        return new RelationshipDocument
        {
            Id = relationship.Id,
            From = relationship.FromEntryId,
            To = relationship.ToEntryId,
            Kind = WorldFormat.RelationshipKindName(relationship.Kind),
            Label = relationship.Label.Length == 0 ? null : relationship.Label,
            Start = relationship.StartDays,
            End = relationship.EndDays,
        };
    }

    // Checked fully afterwards by LoreRules.Problem (through Relationship.Problem).
    private static Relationship ToRelationship(RelationshipDocument document)
    {
        Require(document is not null, "a relationship is empty");
        return new Relationship
        {
            Id = document!.Id,
            FromEntryId = document.From,
            ToEntryId = document.To,
            Kind = WorldFormat.ParseRelationshipKind(document.Kind),
            Label = document.Label ?? "",
            StartDays = document.Start,
            EndDays = document.End,
        };
    }

    private static DiagramDocument ToDocument(LoreDiagram diagram)
    {
        return new DiagramDocument
        {
            Id = diagram.Id,
            Name = diagram.Name,
            Entries = diagram.Placements.Count == 0
                ? null
                : [.. diagram.Placements.Select(p => new PlacementDocument
                {
                    Entry = p.EntryId,
                    X = p.X,
                    Y = p.Y,
                })],
        };
    }

    // Checked fully afterwards by LoreRules.Problem (through LoreDiagram.Problem).
    private static LoreDiagram ToDiagram(DiagramDocument document)
    {
        Require(document is not null, "a diagram is empty");
        Require((document!.Entries ?? []).All(p => p is not null), "a diagram's entry is empty");
        return new LoreDiagram
        {
            Id = document.Id,
            Name = document.Name ?? "",
            Placements = [.. (document.Entries ?? [])
                .Select(p => new DiagramPlacement(p!.Entry, p.X, p.Y))],
        };
    }

    private static LocationDocument ToDocument(LoreLocation location)
    {
        return new LocationDocument
        {
            Body = location.BodyId,
            Region = location.RegionId,
            Latitude = location.Pin?.LatitudeDegrees,
            Longitude = location.Pin?.LongitudeDegrees,
        };
    }

    private static LoreLocation ToLocation(LocationDocument document)
    {
        if (document.Latitude is null && document.Longitude is null)
        {
            return new LoreLocation(document.Body, RegionId: document.Region);
        }

        Require(document.Latitude is double latitude && double.IsFinite(latitude)
                && latitude is >= -90 and <= 90
                && document.Longitude is double longitude && double.IsFinite(longitude)
                && longitude is >= -180 and <= 180,
            "a pinned location is invalid");
        return new LoreLocation(document.Body,
            new GeoCoordinate(document.Latitude!.Value, document.Longitude!.Value),
            document.Region);
    }

    private static BodyDocument ToDocument(Body body)
    {
        SurfaceMap? map = body.Surface.Map;
        return new BodyDocument
        {
            Id = body.Id,
            Name = body.Name,
            Kind = WorldFormat.BodyKindName(body.Kind),
            Shape = body.Shape == BodyShape.Sphere ? null : WorldFormat.ShapeName(body.Shape),
            RadiusKm = body.RadiusKm,
            DayLengthHours = body.DayLengthHours,
            AxialTilt = body.AxialTiltDegrees,
            AxialTiltDirection = body.AxialTiltDirectionDegrees,
            AverageTemperature = body.AverageTemperatureC,
            Atmosphere = body.HasSurface ? body.HasAtmosphere : null,
            WaterLevel = body.WaterLevelMeters,
            Orbit = body.Orbit is Orbit orbit ? ToDocument(orbit) : null,
            Calendar = body.Calendar is Calendar calendar ? ToDocument(calendar) : null,
            Appearance = body.Kind == BodyKind.Star
                ? new AppearanceDocument
                {
                    StarType = WorldFormat.StarTypeName(body.Appearance.StarType),
                }
                : new AppearanceDocument
                {
                    Color = body.Appearance.Color.ToHex(),
                    Pattern = WorldFormat.PatternName(body.Appearance.Pattern),
                },
            Branch = body.Branch,
            Density = body.DensityGramsPerCm3,
            Tree = body.Tree is WorldTreeLook tree
                ? new TreeDocument
                {
                    Branches = tree.Branches,
                    Spread = tree.Spread,
                    Seed = tree.Seed,
                    Bark = tree.Bark.ToHex(),
                    Leaves = tree.Leaves.ToHex(),
                    Glow = tree.Glow.ToHex(),
                    GlowStrength = tree.GlowStrength,
                }
                : null,
            Belts = body.Belts.Count == 0
                ? null
                : [.. body.Belts.Select(belt => new BeltDocument
                {
                    Id = belt.Id,
                    Name = belt.Name,
                    InnerKm = belt.InnerKm,
                    OuterKm = belt.OuterKm,
                    Thickness = belt.ThicknessDegrees,
                    Density = belt.Density,
                    Color = belt.Color.ToHex(),
                })],
            Rings = body.Rings is PlanetRings rings
                ? new RingsDocument
                {
                    Inner = rings.InnerRadii,
                    Outer = rings.OuterRadii,
                    Color = rings.Color.ToHex(),
                }
                : null,
            Surface = new SurfaceDocument
            {
                Map = map is null ? null : new MapDocument
                {
                    Asset = map.AssetName,
                    Projection = WorldFormat.ProjectionName(map.Projection),
                    Calibration = map.Calibration is null ? null : ToDocument(map.Calibration),
                },
                Pieces = body.Surface.Pieces.Count == 0
                    ? null
                    : body.Surface.Pieces.Select(ToDocument).ToList(),
                FillColor = body.Surface.FillColor.ToHex(),
                Terrain = body.Surface.Terrain.IsEmpty
                    ? null
                    : WorldFormat.TerrainEntryName(body.Id),
                Heights = body.Surface.Heights.IsEmpty
                    ? null
                    : WorldFormat.HeightsEntryName(body.Id),
                Shapes = body.Surface.Shapes.Count == 0
                    ? null
                    : [.. body.Surface.Shapes.Select(ToDocument)],
            },
        };
    }

    private static ShapeDocument? ToDocument(ShapeEdit shape) => new()
    {
        Id = shape.Id,
        Kind = WorldFormat.ShapeKindName(shape.Kind),
        Operation = WorldFormat.ShapeOperationName(shape.Operation),
        Latitude = shape.Spot.LatitudeDegrees,
        Longitude = shape.Spot.LongitudeDegrees,
        DepthKm = shape.DepthKm,
        WidthKm = shape.WidthKm,
        HeightKm = shape.HeightKm,
        LengthKm = shape.LengthKm,
        Turn = NullIfZero(shape.TurnDegrees),
    };

    // Checked fully afterwards, with the body (Body.Problem).
    private static ShapeEdit ToShape(ShapeDocument? document)
    {
        Require(document is not null, "a shape is missing");
        Require(document!.Latitude is >= -90 and <= 90 && double.IsFinite(document.Longitude),
            "a shape's place is out of range");
        return new ShapeEdit(document.Id, WorldFormat.ParseShapeKind(document.Kind),
            WorldFormat.ParseShapeOperation(document.Operation),
            new GeoCoordinate(document.Latitude, document.Longitude), document.DepthKm,
            document.WidthKm, document.HeightKm, document.LengthKm, document.Turn ?? 0);
    }

    private static PieceDocument ToDocument(MapPiece piece)
    {
        return new PieceDocument
        {
            Id = piece.Id,
            Name = piece.Name,
            Asset = piece.AssetName,
            Outline = new OutlineDocument
            {
                SourceAspectRatio = piece.Outline.SourceAspectRatio,
                Points = piece.Outline.Points.Select(p => new[] { p.U, p.V }).ToList(),
            },
            Latitude = piece.Center.LatitudeDegrees,
            Longitude = piece.Center.LongitudeDegrees,
            Rotation = piece.RotationDegrees,
            Width = piece.WidthDegrees,
            Warp = piece.WarpedPoints?.Select(p => new[] { p.U, p.V }).ToList(),
        };
    }

    private static MapPiece ToPiece(PieceDocument document)
    {
        Require(document?.Outline?.Points is not null, "a map piece is incomplete");
        Require(WorldFormat.IsValidAssetName(document!.Asset),
            $"invalid map piece image name '{document.Asset}'");
        Require(document.Outline!.Points!.All(p => p is { Length: 2 }),
            "a map piece's outline is invalid");
        Require(double.IsFinite(document.Rotation) && double.IsFinite(document.Width)
                && document.Width is >= PieceProjection.MinimumWidthDegrees
                    and <= PieceProjection.MaximumWidthDegrees,
            "a map piece's size or rotation is invalid");

        PieceOutline outline;
        GeoCoordinate center;
        IReadOnlyList<ImagePoint>? warp = null;
        try
        {
            outline = PieceOutline.Create(
                document.Outline.Points.Select(p => new ImagePoint(p[0], p[1])),
                document.Outline.SourceAspectRatio);
            center = new GeoCoordinate(document.Latitude, document.Longitude);
            if (document.Warp is List<double[]> warpPoints)
            {
                warp = ToWarp(warpPoints, outline);
            }
        }
        catch (ArgumentException error)
        {
            throw new WorldFileException($"The world data is damaged: {error.Message}");
        }

        return new MapPiece
        {
            Id = document.Id,
            Name = RequireText(document.Name, "map piece name"),
            AssetName = document.Asset,
            Outline = outline,
            Center = center,
            RotationDegrees = document.Rotation,
            WidthDegrees = document.Width,
            WarpedPoints = warp,
        };
    }

    // A piece's warp: one finite position per outline point. Positions may lie well outside the
    // box (a small piece stretched far), so only absurd values are refused.
    private static IReadOnlyList<ImagePoint> ToWarp(List<double[]> points, PieceOutline outline)
    {
        const double limit = 1e6;
        Require(points.All(p => p is { Length: 2 } && p.All(
                value => double.IsFinite(value) && Math.Abs(value) <= limit)),
            "a map piece's warp is invalid");
        ImagePoint[] warp = [.. points.Select(p => new ImagePoint(p[0], p[1]))];
        _ = new PieceWarp(outline, warp);  // Checks there's one position per outline point.
        return warp;
    }

    private static CalendarDocument ToDocument(Calendar calendar)
    {
        return new CalendarDocument
        {
            Months = [.. calendar.Months.Select(m => new MonthDocument
            {
                Name = m.Name,
                Days = m.Days,
            })],
            Weekdays = calendar.Weekdays.Count == 0 ? null : [.. calendar.Weekdays],
            FirstYear = calendar.FirstYear,
            Era = calendar.Era,
            Start = new CalendarStartDocument
            {
                Month = calendar.StartMonth,
                Day = calendar.StartDay,
                Weekday = calendar.StartWeekday,
            },
            Fit = WorldFormat.CalendarFitName(calendar.Fit),
            MonthMoon = calendar.MonthMoonId,
            Leap = calendar.Leap is LeapRule leap
                ? new LeapDocument
                {
                    Every = leap.Every,
                    Except = leap.Except,
                    ExceptAgain = leap.ExceptAgain,
                    Month = leap.Month,
                    Days = leap.Days,
                }
                : null,
        };
    }

    // Checked fully afterwards by Body.Problem (through Calendar.Problem).
    private static Calendar ToCalendar(CalendarDocument document)
    {
        Require(document.Months is not null && document.Start is not null
                && document.Months.All(m => m is not null),
            "a calendar is incomplete");
        return new Calendar
        {
            Months = [.. document.Months!.Select(m => new CalendarMonth(m.Name, m.Days))],
            Weekdays = document.Weekdays is null ? [] : [.. document.Weekdays],
            FirstYear = document.FirstYear,
            Era = document.Era,
            StartMonth = document.Start!.Month,
            StartDay = document.Start.Day,
            StartWeekday = document.Start.Weekday,
            Fit = WorldFormat.ParseCalendarFit(document.Fit),
            MonthMoonId = document.MonthMoon,
            Leap = document.Leap is LeapDocument leap
                ? new LeapRule(leap.Every, leap.Except, leap.ExceptAgain, leap.Month, leap.Days)
                : null,
        };
    }

    private static OrbitDocument ToDocument(Orbit orbit)
    {
        return new OrbitDocument
        {
            Parent = orbit.ParentId,
            DistanceKm = orbit.DistanceKm,
            PeriodDays = orbit.PeriodDays,
            StartAngle = orbit.StartAngleDegrees,
            Eccentricity = NullIfZero(orbit.Eccentricity),
            ClosestApproach = NullIfZero(orbit.ClosestApproachDegrees),
            Tilt = NullIfZero(orbit.TiltDegrees),
            TiltDirection = NullIfZero(orbit.TiltDirectionDegrees),
            Height = NullIfZero(orbit.HeightKm),
        };
    }

    private static Orbit ToOrbit(OrbitDocument document)
    {
        return new Orbit
        {
            ParentId = document.Parent,
            DistanceKm = document.DistanceKm,
            PeriodDays = document.PeriodDays,
            StartAngleDegrees = document.StartAngle,
            Eccentricity = document.Eccentricity ?? 0,
            ClosestApproachDegrees = document.ClosestApproach ?? 0,
            TiltDegrees = document.Tilt ?? 0,
            TiltDirectionDegrees = document.TiltDirection ?? 0,
            HeightKm = document.Height ?? 0,
        };
    }

    // The optional orbit extras are left out of the file when they're 0 (a flat circle).
    private static double? NullIfZero(double value) => value == 0 ? null : value;

    private static CalibrationDocument ToDocument(MapCalibration calibration)
    {
        return new CalibrationDocument
        {
            Latitudes = calibration.Latitudes
                .Select(g => new LatitudeGuideDocument
                {
                    Latitude = g.Degrees,
                    DrawnAs = g.DrawnAsDegrees,
                })
                .ToList(),
            Longitudes = calibration.Longitudes
                .Select(g => new LongitudeGuideDocument
                {
                    Longitude = g.Degrees,
                    DrawnAs = g.DrawnAsDegrees,
                })
                .ToList(),
        };
    }

    private static ViewDocument ToDocument(CameraView view)
    {
        return new ViewDocument
        {
            Latitude = view.LatitudeDegrees,
            Longitude = view.LongitudeDegrees,
            Altitude = view.Altitude,
            FocusOffset = [view.FocusOffsetX, view.FocusOffsetY, view.FocusOffsetZ],
        };
    }

    private static Body ToBody(BodyDocument document, Func<string, TerrainGrid> readTerrain,
        Func<string, HeightGrid> readHeights)
    {
        Require(document is not null, "a planet entry is empty");
        Require(document!.Surface is not null, "a planet has no surface data");
        Require(
            RgbColor.TryParseHex(document.Surface!.FillColor, out RgbColor fillColor),
            $"invalid fill color '{document.Surface.FillColor}'");

        var body = new Body
        {
            Id = document.Id,
            Name = RequireText(document.Name, "planet name"),
            Kind = WorldFormat.ParseBodyKind(document.Kind),
            Shape = document.Shape is null
                ? BodyShape.Sphere
                : WorldFormat.ParseShape(document.Shape),
            RadiusKm = document.RadiusKm,
            DayLengthHours = document.DayLengthHours,
            AxialTiltDegrees = document.AxialTilt,
            AxialTiltDirectionDegrees = document.AxialTiltDirection,
            AverageTemperatureC = document.AverageTemperature,
            Orbit = document.Orbit is OrbitDocument orbit ? ToOrbit(orbit) : null,
            Calendar = document.Calendar is CalendarDocument calendar
                ? ToCalendar(calendar)
                : null,
            Rings = document.Rings is RingsDocument rings ? ToRings(rings) : null,
            Belts = [.. (document.Belts ?? []).Select(ToBelt)],
            Tree = document.Tree is TreeDocument tree ? ToTree(tree) : null,
            Branch = document.Branch,
            DensityGramsPerCm3 = document.Density,
            WaterLevelMeters = document.WaterLevel,
        };
        RequireNoProblem(body.Problem());
        Require(body.HasSurface || document.Atmosphere is null,
            "only planets and moons can have an atmosphere");

        if (body.HasSurface)
        {
            // Missing in files before version 26: planets have air, moons don't (owner's
            // choice).
            body.HasAtmosphere = document.Atmosphere ?? body.Kind == BodyKind.Planet;
        }

        body.Appearance = ToAppearance(document.Appearance, body.Kind);
        body.Surface.FillColor = fillColor;
        if (document.Surface.Terrain is string terrain)
        {
            Require(WorldFormat.IsValidTerrainName(terrain),
                $"invalid terrain image name '{terrain}'");
            body.Surface.Terrain = readTerrain(terrain);
        }

        if (document.Surface.Heights is string heights)
        {
            Require(WorldFormat.IsValidHeightsName(heights),
                $"invalid height image name '{heights}'");
            Require(body.HasSurface, "only planets and moons can be sculpted");
            body.Surface.Heights = readHeights(heights);
        }

        body.Surface.Shapes.AddRange((document.Surface.Shapes ?? []).Select(ToShape));
        RequireNoProblem(body.Problem());

        if (document.Surface.Pieces is List<PieceDocument> pieces)
        {
            body.Surface.Pieces.AddRange(pieces.Select(ToPiece));
        }

        if (document.Surface.Map is MapDocument map)
        {
            Require(
                WorldFormat.IsValidAssetName(map.Asset), $"invalid map image name '{map.Asset}'");
            body.Surface.Map = new SurfaceMap
            {
                AssetName = map.Asset,
                Projection = WorldFormat.ParseProjection(map.Projection),
                Calibration = map.Calibration is null ? null : ToCalibration(map.Calibration),
            };
        }

        return body;
    }

    private static WorldTreeLook ToTree(TreeDocument document)
    {
        Require(RgbColor.TryParseHex(document.Bark, out RgbColor bark)
                & RgbColor.TryParseHex(document.Leaves, out RgbColor leaves)
                & RgbColor.TryParseHex(document.Glow, out RgbColor glow),
            "a world tree's colors are invalid");
        return new WorldTreeLook(document.Branches, document.Spread, document.Seed, bark, leaves,
            glow, document.GlowStrength);
    }

    private static AsteroidBelt ToBelt(BeltDocument? document)
    {
        Require(document is not null, "an asteroid belt is empty");
        Require(RgbColor.TryParseHex(document!.Color, out RgbColor color),
            $"invalid belt color '{document.Color}'");
        return new AsteroidBelt(document.Id, document.Name ?? "", document.InnerKm,
            document.OuterKm, document.Thickness, document.Density, color);
    }

    private static Constellation ToConstellation(ConstellationDocument? document)
    {
        Require(document is { Lines: not null }, "a constellation is empty");
        Require(document!.Lines.All(line => line is { Length: 2 }),
            "a constellation's line doesn't join two stars");
        return new Constellation
        {
            Id = document.Id,
            Name = document.Name ?? "",
            Lines = [.. document.Lines.Select(line => new StarLink(line![0], line[1]))],
        };
    }

    private static Nebula ToNebula(NebulaDocument? document)
    {
        Require(document is not null, "a nebula is empty");
        Require(RgbColor.TryParseHex(document!.Color, out RgbColor color)
                && RgbColor.TryParseHex(document.SecondColor, out _),
            "a nebula's colors are invalid");
        RgbColor.TryParseHex(document.SecondColor, out RgbColor second);
        return new Nebula(document.Id, document.Name ?? "", document.Latitude,
            document.Longitude, document.Size, document.Brightness, color, second);
    }

    private static PlanetRings ToRings(RingsDocument document)
    {
        Require(RgbColor.TryParseHex(document.Color, out RgbColor color),
            $"invalid ring color '{document.Color}'");
        return new PlanetRings(document.Inner, document.Outer, color);
    }

    // A star needs its type; a planet or moon its color and pattern. What the other kinds use
    // keeps the defaults.
    private static BodyAppearance ToAppearance(AppearanceDocument? document, BodyKind kind)
    {
        Require(document is not null, "a body has no appearance");
        BodyAppearance appearance = BodyAppearance.DefaultFor(kind);
        if (kind == BodyKind.Star)
        {
            return appearance with { StarType = WorldFormat.ParseStarType(document!.StarType) };
        }

        Require(RgbColor.TryParseHex(document!.Color, out RgbColor color),
            $"invalid body color '{document.Color}'");
        return appearance with
        {
            Color = color,
            Pattern = WorldFormat.ParsePattern(document.Pattern),
        };
    }

    private static MapCalibration ToCalibration(CalibrationDocument document)
    {
        Require(
            document.Latitudes is not null && document.Longitudes is not null
                && document.Latitudes.All(g => g is not null)
                && document.Longitudes.All(g => g is not null),
            "the map calibration is incomplete");
        try
        {
            return MapCalibration.Create(
                document.Latitudes!.Select(g => new CalibrationGuide(g.Latitude, g.DrawnAs)),
                document.Longitudes!.Select(g => new CalibrationGuide(g.Longitude, g.DrawnAs)));
        }
        catch (ArgumentException error)
        {
            throw new WorldFileException($"The world data is damaged: {error.Message}");
        }
    }

    private static CameraView ToView(ViewDocument document)
    {
        double[] offset = document.FocusOffset ?? [0, 0, 0];
        Require(offset.Length == 3, "the saved camera view is invalid");
        double[] numbers =
            [document.Latitude, document.Longitude, document.Altitude, .. offset];
        Require(numbers.All(double.IsFinite), "the saved camera view is invalid");

        return new CameraView(
            document.Latitude, document.Longitude, document.Altitude,
            offset[0], offset[1], offset[2]);
    }

    private static string RequireText(string? text, string what)
    {
        Require(!string.IsNullOrWhiteSpace(text), $"the {what} is missing");
        return text!;
    }

    private static void RequireNoProblem(string? problem)
    {
        if (problem is not null)
        {
            Require(false, problem);
        }
    }

    private static void Require(bool condition, string problem)
    {
        if (!condition)
        {
            throw new WorldFileException($"The world data is damaged: {problem}.");
        }
    }
}
