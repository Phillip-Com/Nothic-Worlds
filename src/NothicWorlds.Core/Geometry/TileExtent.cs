namespace NothicWorlds.Core.Geometry;

/// <summary>
/// The space a <see cref="GroundTile"/>'s ground takes up: a ball around it (in the body's own
/// space, in radii) and the lowest and highest of its heights (as its
/// <see cref="ITileSurface"/> measures them).
/// </summary>
/// <param name="Center">The ball's middle.</param>
/// <param name="Radius">The ball's radius.</param>
/// <param name="Lowest">The tile's lowest height.</param>
/// <param name="Highest">The tile's highest height.</param>
public readonly record struct TileExtent(Vector3D Center, double Radius, double Lowest,
    double Highest)
{
    /// <summary>How far a point is from the ball (0 inside it).</summary>
    public double DistanceFrom(Vector3D point) =>
        Math.Max(0, (point - Center).Length - Radius);

    /// <summary>The ball around some points, with the heights they were placed at.</summary>
    public static TileExtent Around(IReadOnlyList<Vector3D> points, double lowest,
        double highest)
    {
        Vector3D sum = Vector3D.Zero;
        foreach (Vector3D point in points)
        {
            sum += point;
        }

        Vector3D center = sum * (1.0 / points.Count);
        double radius = 0;
        foreach (Vector3D point in points)
        {
            radius = Math.Max(radius, (point - center).Length);
        }

        return new TileExtent(center, radius, lowest, highest);
    }
}
