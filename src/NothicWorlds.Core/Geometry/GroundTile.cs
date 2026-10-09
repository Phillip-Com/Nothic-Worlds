namespace NothicWorlds.Core.Geometry;

/// <summary>
/// A square of the ground drawn around a first-person eye (VISION.md REN-06): one of a
/// surface's root squares (a globe's six cube faces, a flat world's top face), or a quarter of
/// its parent, <see cref="Level"/> halvings down. Tiles stay put on the ground, so a tile always
/// has the same points however the eye moves, and the ground doesn't swim as it walks.
/// </summary>
/// <param name="Root">Which root square it's in.</param>
/// <param name="Level">How many times the root was halved to get it (0: the root itself).</param>
/// <param name="X">Its column among its level's tiles across the root, from 0.</param>
/// <param name="Y">Its row among them, from 0 (down the root).</param>
public readonly record struct GroundTile(int Root, int Level, int X, int Y)
{
    /// <summary>The most halvings a tile can have.</summary>
    public const int MaxLevel = 30;

    /// <summary>Its width, as a share of its root's.</summary>
    public double Share => 1.0 / (1L << Level);

    /// <summary>The tile it's a quarter of (a root is its own).</summary>
    public GroundTile Parent() => Level == 0 ? this : new(Root, Level - 1, X / 2, Y / 2);

    /// <summary>Its four quarters, row by row.</summary>
    public GroundTile[] Children() =>
    [
        new(Root, Level + 1, X * 2, Y * 2),
        new(Root, Level + 1, X * 2 + 1, Y * 2),
        new(Root, Level + 1, X * 2, Y * 2 + 1),
        new(Root, Level + 1, X * 2 + 1, Y * 2 + 1),
    ];

    /// <summary>
    /// Where a point <paramref name="across"/> and <paramref name="down"/> the tile (0 to 1 each)
    /// falls on its root (0 to 1 each).
    /// </summary>
    public (double U, double V) OnRoot(double across, double down) =>
        ((X + across) * Share, (Y + down) * Share);

    /// <summary>
    /// Where a point on the root (<paramref name="u"/>, <paramref name="v"/>, 0 to 1 each) falls
    /// across and down this tile: 0 to 1 each if it's on it.
    /// </summary>
    public (double Across, double Down) FromRoot(double u, double v) =>
        (u / Share - X, v / Share - Y);
}
