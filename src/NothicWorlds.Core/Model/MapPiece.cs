using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;

namespace NothicWorlds.Core.Model;

/// <summary>
/// A piece cut out of an image and laid onto the globe (VISION.md MAP-02), like a sticker: its
/// center sits at <see cref="Center"/>, turned by <see cref="RotationDegrees"/>, spanning
/// <see cref="WidthDegrees"/> of the globe. Pieces sit on top of the planet's map, later
/// pieces on top of earlier ones.
/// </summary>
public sealed class MapPiece
{
    /// <summary>Stable identity, kept across saves.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The piece's display name (e.g. "Northern Isles").</summary>
    public string Name { get; set; } = "Piece";

    /// <summary>The source image inside the world file, e.g. <c>assets/1a2b….png</c>.</summary>
    public required string AssetName { get; init; }

    /// <summary>
    /// The cut-out shape, as points on the source image (0–1 from its top-left). A rectangle
    /// is four points; a freeform cut has as many as the user clicked. Immutable.
    /// </summary>
    public required PieceOutline Outline { get; init; }

    /// <summary>Where the center of the piece's bounding box sits on the globe.</summary>
    public GeoCoordinate Center { get; set; }

    /// <summary>Clockwise turn from "up = north", in degrees.</summary>
    public double RotationDegrees { get; set; }

    /// <summary>
    /// How much of the globe the piece's bounding box spans, left to right, in degrees of arc
    /// (e.g. 20 = about a fifth of the way from equator to pole). Its height follows from the
    /// cut's shape.
    /// </summary>
    public double WidthDegrees { get; set; } = 30.0;

    /// <summary>
    /// Where each point of the cut has been dragged to (Edit Points), in the piece's box: 0–1
    /// from its top-left before warping, and possibly beyond. One per outline point, in the same
    /// order. Null if the piece isn't warped. Replace the whole list to change it; it's never
    /// edited in place, so copies can share it.
    /// </summary>
    public IReadOnlyList<ImagePoint>? WarpedPoints { get; set; }

    /// <summary>Returns an independent copy of this piece.</summary>
    public MapPiece Clone()
    {
        return new MapPiece
        {
            Id = Id,
            Name = Name,
            AssetName = AssetName,
            Outline = Outline,  // Immutable, safe to share.
            Center = Center,
            RotationDegrees = RotationDegrees,
            WidthDegrees = WidthDegrees,
            WarpedPoints = WarpedPoints,  // Never edited in place, safe to share.
        };
    }
}
