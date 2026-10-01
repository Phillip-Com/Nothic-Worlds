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
            Bodies = world.Bodies.Select(ToDocument).ToList(),
            View = world.View is null ? null : ToDocument(world.View),
        };
    }

    /// <exception cref="WorldFileException">The document has missing or invalid data.</exception>
    public static World ToWorld(WorldDocument document)
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
        };
        world.Bodies.AddRange(document.Bodies.Select(ToBody));
        RequireNoProblem(Simulation.SystemHierarchy.Problem(world.Bodies));
        return world;
    }

    private static BodyDocument ToDocument(Body body)
    {
        SurfaceMap? map = body.Surface.Map;
        return new BodyDocument
        {
            Id = body.Id,
            Name = body.Name,
            Kind = WorldFormat.BodyKindName(body.Kind),
            RadiusKm = body.RadiusKm,
            DayLengthHours = body.DayLengthHours,
            AxialTilt = body.AxialTiltDegrees,
            AxialTiltDirection = body.AxialTiltDirectionDegrees,
            Orbit = body.Orbit is Orbit orbit ? ToDocument(orbit) : null,
            Calendar = body.Calendar is Calendar calendar ? ToDocument(calendar) : null,
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
            },
        };
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

    private static Body ToBody(BodyDocument document)
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
            RadiusKm = document.RadiusKm,
            DayLengthHours = document.DayLengthHours,
            AxialTiltDegrees = document.AxialTilt,
            AxialTiltDirectionDegrees = document.AxialTiltDirection,
            Orbit = document.Orbit is OrbitDocument orbit ? ToOrbit(orbit) : null,
            Calendar = document.Calendar is CalendarDocument calendar
                ? ToCalendar(calendar)
                : null,
        };
        RequireNoProblem(body.Problem());
        body.Surface.FillColor = fillColor;
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
