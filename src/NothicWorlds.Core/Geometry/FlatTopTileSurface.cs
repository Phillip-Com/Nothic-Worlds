namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A flat world's top face (VISION.md BOD-10), tiled for the ground around a first-person eye
/// (VISION.md REN-06): one root square over the whole disc, x across and z down it, base points
/// on the face (pulled back onto the rim past it), and heights as how far above the face.
/// </summary>
public sealed class FlatTopTileSurface : ITileSurface
{
    private static readonly Vector3D _up = new(0, 1, 0);

    /// <inheritdoc/>
    public int RootCount => 1;

    /// <inheritdoc/>
    public double RootWidth => 2 * FlatDisc.Radius;

    /// <inheritdoc/>
    public Vector3D BasePoint(int root, double u, double v)
    {
        double x = (u * 2 - 1) * FlatDisc.Radius, z = (v * 2 - 1) * FlatDisc.Radius;
        double across = Math.Sqrt(x * x + z * z);
        double scale = across > FlatDisc.Radius ? FlatDisc.Radius / across : 1;
        return new Vector3D(x * scale, FlatDisc.HalfThickness, z * scale);
    }

    /// <inheritdoc/>
    public Vector3D Up(Vector3D basePoint) => _up;

    /// <inheritdoc/>
    public Vector3D Place(Vector3D basePoint, double height) =>
        basePoint + new Vector3D(0, height, 0);

    /// <inheritdoc/>
    public (int Root, double U, double V)? Locate(Vector3D point)
    {
        double u = (point.X / FlatDisc.Radius + 1) / 2, v = (point.Z / FlatDisc.Radius + 1) / 2;
        return u is >= 0 and <= 1 && v is >= 0 and <= 1 ? (0, u, v) : null;
    }

    /// <inheritdoc/>
    public double Across(Vector3D a, Vector3D b)
    {
        double dx = a.X - b.X, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    /// <inheritdoc/>
    public bool Covers(GroundTile tile)
    {
        // The square's nearest point to the disc's middle.
        double low = -FlatDisc.Radius, width = RootWidth * tile.Share;
        double left = low + tile.X * width, top = low + tile.Y * width;
        double x = Math.Clamp(0, left, left + width), z = Math.Clamp(0, top, top + width);
        return x * x + z * z < FlatDisc.Radius * FlatDisc.Radius;
    }
}
