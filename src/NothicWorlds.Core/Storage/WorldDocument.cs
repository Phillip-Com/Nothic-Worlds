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
    public List<TerrainTypeDocument>? TerrainTypes { get; init; }  // Added in format version 10
    public List<RegionDocument>? Regions { get; init; }  // Added in format version 8
    public List<WeatherPinDocument>? WeatherPins { get; init; }  // Added in format version 9
    public List<JournalEntryDocument>? Journal { get; init; }  // Added in format version 7
    public List<TimelineDocument>? Timelines { get; init; }  // Added in format version 7
    public List<EventDocument>? Events { get; init; }  // Added in format version 7
    public ViewDocument? View { get; init; }
}

internal sealed class TerrainTypeDocument
{
    public required int Code { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public required string Climate { get; init; }  // Added in format version 12
}

internal sealed class JournalEntryDocument
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Text { get; init; }  // Omitted when empty
    public LocationDocument? Location { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset EditedUtc { get; init; }
}

internal sealed class LocationDocument
{
    public required Guid Body { get; init; }
    public Guid? Region { get; init; }  // Added in format version 8
    public double? Latitude { get; init; }  // Both or neither: the pin
    public double? Longitude { get; init; }
}

internal sealed class WeatherPinDocument
{
    public required Guid Id { get; init; }
    public required Guid Body { get; init; }
    public required string Name { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
}

internal sealed class RegionDocument
{
    public required Guid Id { get; init; }
    public required Guid Body { get; init; }
    public required string Name { get; init; }
    public string? Notes { get; init; }  // Omitted when empty
    public required string Color { get; init; }
    public required List<double[]> Corners { get; init; }  // [latitude, longitude] each
}

internal sealed class TimelineDocument
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public bool? Hidden { get; init; }  // Omitted when shown
}

internal sealed class EventDocument
{
    public required Guid Id { get; init; }
    public required Guid Timeline { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }  // Omitted when empty
    public required double Start { get; init; }
    public double? End { get; init; }  // Omitted for a moment
    public LocationDocument? Location { get; init; }
    public List<Guid>? Entries { get; init; }  // Omitted when it links to none
}

internal sealed class BodyDocument
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required double RadiusKm { get; init; }  // Added in format version 5
    public required double DayLengthHours { get; init; }  // Added in format version 5
    public required double AxialTilt { get; init; }  // Added in format version 5
    public required double AxialTiltDirection { get; init; }  // Added in format version 6
    public required double AverageTemperature { get; init; }  // Added in format version 9
    public OrbitDocument? Orbit { get; init; }  // Added in format version 5
    public CalendarDocument? Calendar { get; init; }  // Added in format version 6
    public required AppearanceDocument Appearance { get; init; }  // Added in format version 13
    public required SurfaceDocument Surface { get; init; }
}

// Planets and moons have a color and pattern; stars a star type.
internal sealed class AppearanceDocument
{
    public string? Color { get; init; }
    public string? Pattern { get; init; }
    public string? StarType { get; init; }
}

internal sealed class CalendarDocument
{
    public required List<MonthDocument> Months { get; init; }
    public List<string>? Weekdays { get; init; }  // Omitted for a calendar without weeks
    public required long FirstYear { get; init; }
    public string? Era { get; init; }
    public required CalendarStartDocument Start { get; init; }
    public string? Fit { get; init; }  // Added in format version 11; omitted when not fitted
    public Guid? MonthMoon { get; init; }  // Added in format version 11
    public LeapDocument? Leap { get; init; }  // Added in format version 14
}

internal sealed class LeapDocument
{
    public required int Every { get; init; }
    public int? Except { get; init; }
    public int? ExceptAgain { get; init; }
    public required int Month { get; init; }
    public required int Days { get; init; }
}

internal sealed class MonthDocument
{
    public required string Name { get; init; }
    public required int Days { get; init; }
}

internal sealed class CalendarStartDocument
{
    public required int Month { get; init; }
    public required int Day { get; init; }
    public int Weekday { get; init; }
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
    public string? Terrain { get; init; }  // Added in format version 10
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
