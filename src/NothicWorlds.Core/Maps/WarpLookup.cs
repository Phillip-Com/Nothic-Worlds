namespace NothicWorlds.Core.Maps;

/// <summary>
/// A warped piece's reverse lookup (see <see cref="PieceWarp.BakeLookup"/>): a small grid over
/// the warped piece's extent, each cell holding which spot of the unwarped box belongs there, or
/// nothing. The shader reads it as a texture and blends it exactly like <see cref="Sample"/>, so
/// what's drawn and what's clickable always agree.
/// </summary>
public sealed class WarpLookup
{
    /// <summary>Cells across and down.</summary>
    public const int Size = 64;

    private readonly float[] _values = new float[Size * Size * 4];

    internal WarpLookup(double minU, double minV, double maxU, double maxV)
    {
        MinU = minU;
        MinV = minV;
        MaxU = maxU;
        MaxV = maxV;
    }

    /// <summary>Left edge of the warped piece, in its box (may be below 0).</summary>
    public double MinU { get; }

    /// <summary>Top edge of the warped piece, in its box.</summary>
    public double MinV { get; }

    /// <summary>Right edge of the warped piece, in its box (may be above 1).</summary>
    public double MaxU { get; }

    /// <summary>Bottom edge of the warped piece, in its box.</summary>
    public double MaxV { get; }

    /// <summary>
    /// The grid, row by row from the top: four floats per cell (unwarped U, unwarped V,
    /// 1 if covered or 0 if not, unused). Ready to upload as an RGBA float texture.
    /// </summary>
    public ReadOnlySpan<float> Values => _values;

    /// <summary>
    /// How far the warped piece reaches from its center, in radians of arc, for a piece of the
    /// given size. Nothing of it lies farther away.
    /// </summary>
    public double ReachRadians(double widthRadians, double heightRadians)
    {
        double reach = 0;
        foreach (double u in new[] { MinU, MaxU })
        {
            foreach (double v in new[] { MinV, MaxV })
            {
                double x = (u - 0.5) * widthRadians;
                double y = (0.5 - v) * heightRadians;
                reach = Math.Max(reach, Math.Sqrt(x * x + y * y));
            }
        }

        return reach;
    }

    /// <summary>
    /// Which spot of the unwarped box is drawn at a spot of the warped piece, or null if
    /// nothing of the piece is there. Blends the four nearest cells, ignoring empty ones. The
    /// lookup reaches a little past the piece, so its edge is cut exactly where the box ends,
    /// not along the lookup's cells.
    /// </summary>
    public ImagePoint? Sample(ImagePoint warped)
    {
        double x = (warped.U - MinU) / (MaxU - MinU) * Size - 0.5;
        double y = (warped.V - MinV) / (MaxV - MinV) * Size - 0.5;
        if (x < -1 || y < -1 || x > Size || y > Size)
        {
            return null;
        }

        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        double fx = x - x0;
        double fy = y - y0;
        double covered = 0, u = 0, v = 0;
        for (int dy = 0; dy <= 1; dy++)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                int cx = Math.Clamp(x0 + dx, 0, Size - 1);
                int cy = Math.Clamp(y0 + dy, 0, Size - 1);
                int i = (cy * Size + cx) * 4;
                double weight = (dx == 0 ? 1 - fx : fx) * (dy == 0 ? 1 - fy : fy)
                    * _values[i + 2];
                covered += weight;
                u += weight * _values[i];
                v += weight * _values[i + 1];
            }
        }

        if (covered < 0.5)
        {
            return null;
        }

        var unwarped = new ImagePoint(u / covered, v / covered);
        return unwarped.U is < 0 or > 1 || unwarped.V is < 0 or > 1 ? null : unwarped;
    }

    // Draws one triangle of the warped mesh: every cell whose center it covers gets the
    // unwarped position there, blended across the triangle.
    internal void Fill(
        ImagePoint a, ImagePoint b, ImagePoint c,
        ImagePoint sourceA, ImagePoint sourceB, ImagePoint sourceC)
    {
        (double ax, double ay) = ToCells(a);
        (double bx, double by) = ToCells(b);
        (double cx, double cy) = ToCells(c);
        double area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
        if (Math.Abs(area) < 1e-12)
        {
            return;  // Squashed flat: covers nothing.
        }

        int left = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
        int right = Math.Min(Size - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
        int top = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
        int bottom = Math.Min(Size - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
        const double edgeTolerance = -1e-9;
        for (int row = top; row <= bottom; row++)
        {
            for (int column = left; column <= right; column++)
            {
                double px = column + 0.5;
                double py = row + 0.5;
                double wa = ((bx - px) * (cy - py) - (cx - px) * (by - py)) / area;
                double wb = ((cx - px) * (ay - py) - (ax - px) * (cy - py)) / area;
                double wc = 1 - wa - wb;
                if (wa < edgeTolerance || wb < edgeTolerance || wc < edgeTolerance)
                {
                    continue;
                }

                int i = (row * Size + column) * 4;
                _values[i] = (float)(wa * sourceA.U + wb * sourceB.U + wc * sourceC.U);
                _values[i + 1] = (float)(wa * sourceA.V + wb * sourceB.V + wc * sourceC.V);
                _values[i + 2] = 1;
            }
        }
    }

    private (double X, double Y) ToCells(ImagePoint point)
    {
        return ((point.U - MinU) / (MaxU - MinU) * Size, (point.V - MinV) / (MaxV - MinV) * Size);
    }
}
