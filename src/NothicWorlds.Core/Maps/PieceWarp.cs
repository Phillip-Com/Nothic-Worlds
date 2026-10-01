namespace NothicWorlds.Core.Maps;

/// <summary>
/// Stretches a map piece by moving the points of its cut (VISION.md MAP-02, Edit Points). Each
/// point of the outline is a handle; dragging it moves that spot of the image, and everything
/// inside follows smoothly. Immutable.
/// </summary>
/// <remarks>
/// Positions are in the piece's box (0–1 from its top-left, before warping), so a warp moves,
/// turns, and resizes with its piece. The stretch uses mean value coordinates (Floater,
/// Hormann): every position is a smooth weighted blend of the outline points, so moving one point
/// pulls the area near it most and leaves the other points where they are, with no creases. It
/// works for any outline shape and stays fast for outlines with hundreds of points.
/// </remarks>
public sealed class PieceWarp
{
    // Mesh cells across the box when baking: fine enough that the stretch looks smooth.
    private const int MeshCells = 48;

    // The mesh reaches one cell past each side of the box, so the lookup also covers just past
    // the piece's edge; the edge itself is then cut exactly where the image ends (see
    // WarpLookup.Sample), rather than following the lookup's cells.
    private const int GridCells = MeshCells + 2;

    private readonly ImagePoint[] _cage;
    private readonly ImagePoint[] _targets;
    private readonly double _aspect;

    /// <param name="outline">The piece's cut.</param>
    /// <param name="targets">
    /// Where each point of the outline now sits, in the piece's box (one per outline point).
    /// </param>
    /// <exception cref="ArgumentException">
    /// The number of positions doesn't match the outline, or one isn't finite.
    /// </exception>
    public PieceWarp(PieceOutline outline, IReadOnlyList<ImagePoint> targets)
    {
        if (targets.Count != outline.Points.Count
            || targets.Any(p => !double.IsFinite(p.U) || !double.IsFinite(p.V)))
        {
            throw new ArgumentException(
                "a warp needs one finite position for each point of the cut");
        }

        _cage = [.. UnwarpedPoints(outline)];
        _targets = [.. targets];

        // Angles are measured at the box's true shape, so the stretch isn't skewed.
        _aspect = outline.BoxAspectRatio;
    }

    /// <summary>The piece's warp, or null if it has none.</summary>
    public static PieceWarp? For(Model.MapPiece piece)
    {
        return piece.WarpedPoints is IReadOnlyList<ImagePoint> targets
            ? new PieceWarp(piece.Outline, targets)
            : null;
    }

    /// <summary>
    /// Where each point of the outline sits in the piece's box before any warping: the starting
    /// positions for Edit Points.
    /// </summary>
    public static IReadOnlyList<ImagePoint> UnwarpedPoints(PieceOutline outline)
    {
        double width = outline.MaxU - outline.MinU;
        double height = outline.MaxV - outline.MinV;
        return [.. outline.Points.Select(p =>
            new ImagePoint((p.U - outline.MinU) / width, (p.V - outline.MinV) / height))];
    }

    /// <summary>Where a position in the unwarped box ends up after warping.</summary>
    public ImagePoint Forward(ImagePoint box)
    {
        int count = _cage.Length;
        Span<double> sx = count <= 256 ? stackalloc double[count] : new double[count];
        Span<double> sy = count <= 256 ? stackalloc double[count] : new double[count];
        Span<double> r = count <= 256 ? stackalloc double[count] : new double[count];
        for (int i = 0; i < count; i++)
        {
            sx[i] = (_cage[i].U - box.U) * _aspect;
            sy[i] = _cage[i].V - box.V;
            r[i] = Math.Sqrt(sx[i] * sx[i] + sy[i] * sy[i]);
            if (r[i] < 1e-12)
            {
                return _targets[i];  // Exactly on a point.
            }
        }

        // tan(half the angle) each edge spans, seen from the position.
        Span<double> halfTan = count <= 256 ? stackalloc double[count] : new double[count];
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            double cross = sx[i] * sy[next] - sx[next] * sy[i];
            double dot = sx[i] * sx[next] + sy[i] * sy[next];
            if (Math.Abs(cross) < 1e-12 * r[i] * r[next])
            {
                if (dot < 0)
                {
                    // Exactly on this edge: blend its two ends.
                    double t = r[i] / (r[i] + r[next]);
                    return Lerp(_targets[i], _targets[next], t);
                }

                halfTan[i] = 0;  // In line with the edge, outside it: it spans no angle.
                continue;
            }

            halfTan[i] = (r[i] * r[next] - dot) / cross;
        }

        double sumWeights = 0, u = 0, v = 0;
        for (int i = 0; i < count; i++)
        {
            int previous = (i + count - 1) % count;
            double weight = (halfTan[previous] + halfTan[i]) / r[i];
            sumWeights += weight;
            u += weight * _targets[i].U;
            v += weight * _targets[i].V;
        }

        return Math.Abs(sumWeights) < 1e-15 ? box : new ImagePoint(u / sumWeights, v / sumWeights);
    }

    /// <summary>
    /// Bakes the reverse lookup used to draw the warped piece (and to find it under the mouse):
    /// for each spot of the warped piece, which spot of the original image belongs there.
    /// </summary>
    public WarpLookup BakeLookup()
    {
        const int vertices = GridCells + 1;
        var source = new ImagePoint[vertices * vertices];
        var moved = new ImagePoint[vertices * vertices];
        var inside = new bool[vertices * vertices];
        for (int row = 0; row < vertices; row++)
        {
            for (int column = 0; column < vertices; column++)
            {
                int index = row * vertices + column;
                source[index] = new ImagePoint(
                    (column - 1.0) / MeshCells, (row - 1.0) / MeshCells);
                moved[index] = Forward(source[index]);
                inside[index] = CageContains(source[index]);
            }
        }

        bool[] cells = CellsToDraw(inside);
        (double minU, double minV, double maxU, double maxV) = Extent(cells, moved);
        var lookup = new WarpLookup(minU, minV, maxU, maxV);
        for (int row = 0; row < GridCells; row++)
        {
            for (int column = 0; column < GridCells; column++)
            {
                if (!cells[row * GridCells + column])
                {
                    continue;
                }

                int a = row * vertices + column;
                int b = a + 1;
                int c = a + vertices;
                int d = c + 1;
                lookup.Fill(moved[a], moved[b], moved[d], source[a], source[b], source[d]);
                lookup.Fill(moved[a], moved[d], moved[c], source[a], source[d], source[c]);
            }
        }

        return lookup;
    }

    // Cells touching the cut, plus their neighbors, so a thin part of the cut that crosses a cell
    // without covering any of its corners isn't lost. The rest of the box is transparent anyway.
    private static bool[] CellsToDraw(bool[] insideVertex)
    {
        const int vertices = GridCells + 1;
        var touches = new bool[GridCells * GridCells];
        for (int row = 0; row < GridCells; row++)
        {
            for (int column = 0; column < GridCells; column++)
            {
                int a = row * vertices + column;
                touches[row * GridCells + column] = insideVertex[a] || insideVertex[a + 1]
                    || insideVertex[a + vertices] || insideVertex[a + vertices + 1];
            }
        }

        var draw = new bool[touches.Length];
        for (int row = 0; row < GridCells; row++)
        {
            for (int column = 0; column < GridCells; column++)
            {
                for (int dr = -1; dr <= 1 && !draw[row * GridCells + column]; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int r = row + dr;
                        int c = column + dc;
                        if (r >= 0 && r < GridCells && c >= 0 && c < GridCells
                            && touches[r * GridCells + c])
                        {
                            draw[row * GridCells + column] = true;
                            break;
                        }
                    }
                }
            }
        }

        return draw;
    }

    private static (double MinU, double MinV, double MaxU, double MaxV) Extent(
        bool[] cells, ImagePoint[] moved)
    {
        const int vertices = GridCells + 1;
        double minU = double.MaxValue, minV = double.MaxValue;
        double maxU = double.MinValue, maxV = double.MinValue;
        for (int row = 0; row < GridCells; row++)
        {
            for (int column = 0; column < GridCells; column++)
            {
                if (!cells[row * GridCells + column])
                {
                    continue;
                }

                int a = row * vertices + column;
                foreach (int corner in new[] { a, a + 1, a + vertices, a + vertices + 1 })
                {
                    minU = Math.Min(minU, moved[corner].U);
                    minV = Math.Min(minV, moved[corner].V);
                    maxU = Math.Max(maxU, moved[corner].U);
                    maxV = Math.Max(maxV, moved[corner].V);
                }
            }
        }

        // Never empty, even if every point was dragged onto one spot.
        return (minU, minV, Math.Max(maxU, minU + 1e-6), Math.Max(maxV, minV + 1e-6));
    }

    private bool CageContains(ImagePoint point)
    {
        bool inside = false;
        for (int i = 0, j = _cage.Length - 1; i < _cage.Length; j = i++)
        {
            ImagePoint a = _cage[i];
            ImagePoint b = _cage[j];
            if ((a.V > point.V) != (b.V > point.V)
                && point.U < a.U + (point.V - a.V) / (b.V - a.V) * (b.U - a.U))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static ImagePoint Lerp(ImagePoint a, ImagePoint b, double t)
    {
        return new ImagePoint(a.U + (b.U - a.U) * t, a.V + (b.V - a.V) * t);
    }
}
