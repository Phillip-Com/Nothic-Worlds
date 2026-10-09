namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A surface the ground around a first-person eye is tiled over (VISION.md REN-06): its root
/// squares, where their points are on the bare surface, and how a height lifts them. All in
/// the body's own space, in radii.
/// </summary>
public interface ITileSurface
{
    /// <summary>How many root squares it has.</summary>
    int RootCount { get; }

    /// <summary>How wide a root square is along the surface.</summary>
    double RootWidth { get; }

    /// <summary>
    /// The point on the bare surface at <paramref name="u"/>, <paramref name="v"/> (0 to 1
    /// each) across and down a root square.
    /// </summary>
    Vector3D BasePoint(int root, double u, double v);

    /// <summary>A base point lifted to a height (the surface's own measure of height).</summary>
    Vector3D Place(Vector3D basePoint, double height);

    /// <summary>
    /// The root square a point on or over the surface is over, and where on it; null if none.
    /// </summary>
    (int Root, double U, double V)? Locate(Vector3D point);

    /// <summary>How far apart two base points are along the surface.</summary>
    double Across(Vector3D a, Vector3D b);

    /// <summary>Whether any of a tile lies on the surface (not all of a square need).</summary>
    bool Covers(GroundTile tile);
}
