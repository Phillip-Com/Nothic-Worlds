namespace NothicWorlds.Core.Storage;

// The exact shape of world.json, current format version (docs/world-format.md). Kept separate
// from the Model classes so the file format only changes on purpose. Property names are written
// in camelCase.

internal sealed class WorldDocument
{
    public required int FormatVersion { get; init; }
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset ModifiedUtc { get; init; }
    public double? TimeDays { get; init; }  // Added in format version 5
    public required List<BodyDocument> Bodies { get; init; }
    public ViewDocument? View { get; init; }
}

internal sealed class BodyDocument
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required double RadiusKm { get; init; }  // Added in format version 5
    public required double DayLengthHours { get; init; }  // Added in format version 5
    public required double AxialTilt { get; init; }  // Added in format version 5
    public OrbitDocument? Orbit { get; init; }  // Added in format version 5
    public required SurfaceDocument Surface { get; init; }
}

internal sealed class OrbitDocument
{
    public required Guid Parent { get; init; }
    public required double DistanceKm { get; init; }
    public required double PeriodDays { get; init; }
    public required double StartAngle { get; init; }

    // Optional extras, omitted when 0 (a flat circle).
    public double? Eccentricity { get; init; }
    public double? ClosestApproach { get; init; }
    public double? Tilt { get; init; }
    public double? TiltDirection { get; init; }
}

internal sealed class SurfaceDocument
{
    public MapDocument? Map { get; init; }
    public List<PieceDocument>? Pieces { get; init; }  // Added in format version 3
    public required string FillColor { get; init; }
}

internal sealed class PieceDocument
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Asset { get; init; }
    public required OutlineDocument Outline { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double Rotation { get; init; }
    public required double Width { get; init; }
    public List<double[]>? Warp { get; init; }  // Added in format version 4
}

internal sealed class OutlineDocument
{
    public required double SourceAspectRatio { get; init; }
    public required List<double[]> Points { get; init; }
}

internal sealed class MapDocument
{
    public required string Asset { get; init; }
    public required string Projection { get; init; }
    public CalibrationDocument? Calibration { get; init; }  // Added in format version 2
}

internal sealed class CalibrationDocument
{
    public required List<LatitudeGuideDocument> Latitudes { get; init; }
    public required List<LongitudeGuideDocument> Longitudes { get; init; }
}

internal sealed class LatitudeGuideDocument
{
    public required double Latitude { get; init; }
    public required double DrawnAs { get; init; }
}

internal sealed class LongitudeGuideDocument
{
    public required double Longitude { get; init; }
    public required double DrawnAs { get; init; }
}

internal sealed class ViewDocument
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double Altitude { get; init; }
    public double[]? FocusOffset { get; init; }
}
