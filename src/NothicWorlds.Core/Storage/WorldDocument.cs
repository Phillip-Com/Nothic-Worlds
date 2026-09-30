namespace NothicWorlds.Core.Storage;

// The exact shape of world.json, format version 1 (docs/world-format.md). Kept separate from
// the Model classes so the file format only changes on purpose. Property names are written in
// camelCase.

internal sealed class WorldDocument
{
    public required int FormatVersion { get; init; }
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset ModifiedUtc { get; init; }
    public required List<BodyDocument> Bodies { get; init; }
    public ViewDocument? View { get; init; }
}

internal sealed class BodyDocument
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required SurfaceDocument Surface { get; init; }
}

internal sealed class SurfaceDocument
{
    public MapDocument? Map { get; init; }
    public required string FillColor { get; init; }
}

internal sealed class MapDocument
{
    public required string Asset { get; init; }
    public required string Projection { get; init; }
}

internal sealed class ViewDocument
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double Altitude { get; init; }
    public double[]? FocusOffset { get; init; }
}
