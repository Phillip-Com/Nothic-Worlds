namespace NothicWorlds.Core.Model;

/// <summary>
/// The shapes a body can be built up or carved with (VISION.md BOD-04; owner's choice: these
/// four). Which of a shape's sizes each uses is in <see cref="ShapeEdit"/>.
/// </summary>
public enum ShapeKind
{
    /// <summary>A ball: domes, craters, and (cut from inside) hollow worlds.</summary>
    Sphere,

    /// <summary>A block: walls, buildings, quarries, straight canyons.</summary>
    Box,

    /// <summary>A round column: towers, pits, holes through a world.</summary>
    Cylinder,

    /// <summary>A cone, point up: volcano-like peaks or funnel pits.</summary>
    Cone,
}
