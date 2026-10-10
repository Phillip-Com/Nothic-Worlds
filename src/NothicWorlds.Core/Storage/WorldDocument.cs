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
    public string? Style { get; init; }  // Added in format version 25
    public required List<BodyDocument> Bodies { get; init; }
    public List<TerrainTypeDocument>? TerrainTypes { get; init; }  // Added in format version 10
    public List<RegionDocument>? Regions { get; init; }  // Added in format version 8
    public List<WeatherPinDocument>? WeatherPins { get; init; }  // Added in format version 9
    public List<RiverDocument?>? Rivers { get; init; }  // Added in format version 31
    public List<LakeDocument?>? Lakes { get; init; }  // Added in format version 31
    public List<JournalEntryDocument>? Journal { get; init; }  // Added in format version 7
    public List<TimelineDocument>? Timelines { get; init; }  // Added in format version 7
    public List<EventDocument>? Events { get; init; }  // Added in format version 7
    public List<RelationshipDocument>? Relationships { get; init; }  // Added in version 27
    public List<DiagramDocument>? Diagrams { get; init; }  // Added in format version 27
    public List<NebulaDocument?>? Nebulas { get; init; }  // Added in format version 19
    public required int StarSeed { get; init; }  // Added in format version 28
    public bool? TerrainShapesGround { get; init; }  // Added in format version 29; omitted if off
    public List<ConstellationDocument?>? Constellations { get; init; }  // Added in version 28
    public ViewDocument? View { get; init; }
}

internal sealed class ConstellationDocument
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required List<int[]?> Lines { get; init; }  // Each a pair of star ids
}

internal sealed class NebulaDocument
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double Size { get; init; }
    public required double Brightness { get; init; }
    public string? Color { get; init; }
    public string? SecondColor { get; init; }
}

internal sealed class TerrainTypeDocument
{
    public required int Code { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; }
    public required string Climate { get; init; }  // Added in format version 12
    public int? Height { get; init; }  // Added in format version 29; omitted for 0
    public double? Edge { get; init; }  // Added in format version 29; omitted for 0
    public int? Variation { get; init; }  // Added in format version 30; omitted for 0
    public double? FeatureSize { get; init; }  // Added in version 30, km; omitted for 50
    public double? Roughness { get; init; }  // Added in format version 33; omitted for 0
    public string? Ground { get; init; }  // Added in format version 34; refused if missing
}

internal sealed class JournalEntryDocument
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Text { get; init; }  // Omitted when empty
    public string? Kind { get; init; }  // Added in format version 27; omitted for none
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

internal sealed class RiverDocument
{
    public required Guid Id { get; init; }
    public required Guid Body { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required List<double[]?> Points { get; init; }  // [latitude, longitude] each
    public double? Width { get; init; }  // Omitted when 1 km
    public double? Depth { get; init; }  // Added in format version 32; omitted for Auto
    public double? DepthVariation { get; init; }  // Version 32; omitted for 0
    public double? DepthSpacing { get; init; }  // Version 32; omitted when 1 km
    public double? DepthSmoothness { get; init; }  // Version 32; omitted when 1
}

internal sealed class LakeDocument
{
    public required Guid Id { get; init; }
    public required Guid Body { get; init; }
    public required string Name { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required int Level { get; init; }
    public bool? FlowsOut { get; init; }  // Omitted when it doesn't
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

internal sealed class RelationshipDocument
{
    public required Guid Id { get; init; }
    public required Guid From { get; init; }
    public required Guid To { get; init; }
    public required string Kind { get; init; }
    public string? Label { get; init; }  // Omitted when empty
    public double? Start { get; init; }  // Omitted for always
    public double? End { get; init; }  // Omitted for never
}

internal sealed class DiagramDocument
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public List<PlacementDocument?>? Entries { get; init; }  // Omitted when empty
}

internal sealed class PlacementDocument
{
    public required Guid Entry { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
}

internal sealed class BodyDocument
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public string? Shape { get; init; }  // Added in format version 16; omitted for spheres
    public required double RadiusKm { get; init; }  // Added in format version 5
    public required double DayLengthHours { get; init; }  // Added in format version 5
    public required double AxialTilt { get; init; }  // Added in format version 5
    public required double AxialTiltDirection { get; init; }  // Added in format version 6
    public required double AverageTemperature { get; init; }  // Added in format version 9
    public bool? Atmosphere { get; init; }  // Added in format version 26; planets and moons
    public int? WaterLevel { get; init; }  // Added in format version 30; omitted for no water
    public double? Density { get; init; }  // Added in format version 22; omitted when typical
    public OrbitDocument? Orbit { get; init; }  // Added in format version 5
    public CalendarDocument? Calendar { get; init; }  // Added in format version 6
    public required AppearanceDocument Appearance { get; init; }  // Added in format version 13
    public RingsDocument? Rings { get; init; }  // Added in format version 17
    public List<BeltDocument?>? Belts { get; init; }  // Added in format version 18; stars only
    public TreeDocument? Tree { get; init; }  // Added in format version 20; world trees only
    public int? Branch { get; init; }  // Added in format version 21; realms only
    public required SurfaceDocument Surface { get; init; }
}

// Planets and moons have a color and pattern; stars a star type.
internal sealed class TreeDocument
{
    public required int Branches { get; init; }
    public required double Spread { get; init; }
    public required int Seed { get; init; }
    public string? Bark { get; init; }
    public string? Leaves { get; init; }
    public string? Glow { get; init; }
    public required double GlowStrength { get; init; }
}

internal sealed class BeltDocument
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public required double InnerKm { get; init; }
    public required double OuterKm { get; init; }
    public required double Thickness { get; init; }
    public required double Density { get; init; }
    public string? Color { get; init; }
}

internal sealed class RingsDocument
{
    public required double Inner { get; init; }
    public required double Outer { get; init; }
    public string? Color { get; init; }
}

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
    public double? Height { get; init; }  // Added in format version 21
}

internal sealed class SurfaceDocument
{
    public MapDocument? Map { get; init; }
    public List<PieceDocument>? Pieces { get; init; }  // Added in format version 3
    public required string FillColor { get; init; }
    public string? Terrain { get; init; }  // Added in format version 10
    public string? Heights { get; init; }  // Added in format version 23
    public List<ShapeDocument?>? Shapes { get; init; }  // Added in format version 24
}

internal sealed class ShapeDocument
{
    public required Guid Id { get; init; }
    public required string Kind { get; init; }
    public required string Operation { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double DepthKm { get; init; }
    public required double WidthKm { get; init; }
    public required double HeightKm { get; init; }
    public required double LengthKm { get; init; }
    public double? Turn { get; init; }  // Omitted when 0
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
