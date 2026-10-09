namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A globe, tiled for the ground around a first-person eye (VISION.md REN-06): the six faces of
/// <see cref="CubeSphere"/>, with base points as unit directions and heights as how far out
/// the ground is (in radii, 1 at the base).
/// </summary>
public sealed class GlobeTileSurface : ITileSurface
{
    /// <inheritdoc/>
    public int RootCount => CubeSphere.FaceCount;

    /// <inheritdoc/>
    public double RootWidth => Math.PI / 2;

    /// <inheritdoc/>
    public Vector3D BasePoint(int root, double u, double v) => CubeSphere.Direction(root, u, v);

    /// <inheritdoc/>
    public Vector3D Up(Vector3D basePoint) => basePoint;

    /// <inheritdoc/>
    public Vector3D Place(Vector3D basePoint, double height) => basePoint * height;

    /// <inheritdoc/>
    public (int Root, double U, double V)? Locate(Vector3D point)
    {
        double length = point.Length;
        if (!(length > 0) || !double.IsFinite(length))
        {
            return null;
        }

        int face = CubeSphere.FaceOf(point);
        (double u, double v) = CubeSphere.FacePosition(face, point);
        return (face, Math.Clamp(u, 0, 1), Math.Clamp(v, 0, 1));
    }

    /// <inheritdoc/>
    public double Across(Vector3D a, Vector3D b) =>
        Math.Acos(Math.Clamp(a.Dot(b) / (a.Length * b.Length), -1, 1));

    /// <inheritdoc/>
    public bool Covers(GroundTile tile) => true;
}
