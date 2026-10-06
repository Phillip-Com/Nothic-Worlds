namespace NothicWorlds.Core.Maps;

/// <summary>
/// The cut-out shape of a map piece (VISION.md MAP-02): a closed outline of points on the source
/// image, each 0–1 from its top-left. A rectangle cut is four points, an ellipse many points
/// around it, a regular shape one per corner, and a freeform cut however many the user clicked.
/// Immutable.
/// </summary>
public sealed class PieceOutline
{
    /// <summary>The fewest points an outline can have.</summary>
    public const int MinimumPoints = 3;

    /// <summary>The most points an outline can have (keeps files and editing manageable).</summary>
    public const int MaximumPoints = 1000;

    /// <summary>How many points go around an ellipse cut: smooth even when it's large.</summary>
    public const int EllipsePoints = 96;

    /// <summary>The fewest sides a regular shape can have (a triangle).</summary>
    public const int MinimumSides = 3;

    /// <summary>The most sides a regular shape can have.</summary>
    public const int MaximumSides = 12;

    private PieceOutline(IReadOnlyList<ImagePoint> points, double sourceAspectRatio)
    {
        Points = points;
        SourceAspectRatio = sourceAspectRatio;
        MinU = points.Min(p => p.U);
        MaxU = points.Max(p => p.U);
        MinV = points.Min(p => p.V);
        MaxV = points.Max(p => p.V);
    }

    /// <summary>The outline's points, in order (the last connects back to the first).</summary>
    public IReadOnlyList<ImagePoint> Points { get; }

    /// <summary>The source image's width divided by its height (needed to know the true
    /// shape of the cut, since points are stored as fractions of the image).</summary>
    public double SourceAspectRatio { get; }

    /// <summary>
    /// Left edge of the outline's bounding box, as a fraction of the image width.
    /// </summary>
    public double MinU { get; }

    /// <summary>Right edge of the bounding box.</summary>
    public double MaxU { get; }

    /// <summary>Top edge of the bounding box, as a fraction of the image height.</summary>
    public double MinV { get; }

    /// <summary>Bottom edge of the bounding box.</summary>
    public double MaxV { get; }

    /// <summary>The bounding box's true width divided by its height.</summary>
    public double BoxAspectRatio => (MaxU - MinU) / (MaxV - MinV) * SourceAspectRatio;

    /// <summary>Creates a rectangular cut from two opposite corners.</summary>
    /// <exception cref="ArgumentException">The rectangle is empty or outside the image.</exception>
    public static PieceOutline Rectangle(
        ImagePoint corner, ImagePoint oppositeCorner, double sourceAspectRatio)
    {
        double left = Math.Min(corner.U, oppositeCorner.U);
        double right = Math.Max(corner.U, oppositeCorner.U);
        double top = Math.Min(corner.V, oppositeCorner.V);
        double bottom = Math.Max(corner.V, oppositeCorner.V);
        return Create(
            [new(left, top), new(right, top), new(right, bottom), new(left, bottom)],
            sourceAspectRatio);
    }

    /// <summary>
    /// Creates an elliptical cut filling the box between two opposite corners (see
    /// <see cref="SquaredCorner"/> for a circle).
    /// </summary>
    /// <exception cref="ArgumentException">The box is empty.</exception>
    public static PieceOutline Ellipse(
        ImagePoint corner, ImagePoint oppositeCorner, double sourceAspectRatio)
    {
        double centerU = (corner.U + oppositeCorner.U) / 2;
        double centerV = (corner.V + oppositeCorner.V) / 2;
        double radiusU = Math.Abs(oppositeCorner.U - corner.U) / 2;
        double radiusV = Math.Abs(oppositeCorner.V - corner.V) / 2;
        return Create(
            Enumerable.Range(0, EllipsePoints).Select(i =>
            {
                double angle = 2 * Math.PI * i / EllipsePoints;
                return new ImagePoint(
                    Math.Clamp(centerU + radiusU * Math.Cos(angle), 0, 1),
                    Math.Clamp(centerV + radiusV * Math.Sin(angle), 0, 1));
            }),
            sourceAspectRatio);
    }

    /// <summary>
    /// Creates a regular shape (equal sides and angles, as seen on the image) around
    /// <paramref name="center"/>, with one corner at <paramref name="corner"/>. Corners past the
    /// image's edge are pulled back onto it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Too few or many sides, or the shape has no size.
    /// </exception>
    public static PieceOutline RegularShape(
        ImagePoint center, ImagePoint corner, int sides, double sourceAspectRatio)
    {
        Require(sides is >= MinimumSides and <= MaximumSides,
            $"a regular shape needs {MinimumSides} to {MaximumSides} sides");
        Require(double.IsFinite(sourceAspectRatio) && sourceAspectRatio > 0,
            "the image's aspect ratio must be a positive number");

        // Worked out in the image's true proportions, so the shape isn't squashed on a wide one.
        double across = (corner.U - center.U) * sourceAspectRatio;
        double down = corner.V - center.V;
        double radius = Math.Sqrt(across * across + down * down);
        double firstAngle = Math.Atan2(down, across);
        return Create(
            Enumerable.Range(0, sides).Select(i =>
            {
                double angle = firstAngle + 2 * Math.PI * i / sides;
                return new ImagePoint(
                    Math.Clamp(center.U + radius * Math.Cos(angle) / sourceAspectRatio, 0, 1),
                    Math.Clamp(center.V + radius * Math.Sin(angle), 0, 1));
            }),
            sourceAspectRatio);
    }

    /// <summary>
    /// The corner opposite <paramref name="corner"/> that makes the box square on the image
    /// (as wide as it is tall in pixels), toward <paramref name="toward"/> and no bigger than
    /// the box to it: for drawing squares and circles.
    /// </summary>
    public static ImagePoint SquaredCorner(
        ImagePoint corner, ImagePoint toward, double sourceAspectRatio)
    {
        double across = (toward.U - corner.U) * sourceAspectRatio;
        double down = toward.V - corner.V;
        double side = Math.Min(Math.Abs(across), Math.Abs(down));
        return new ImagePoint(
            corner.U + Math.Sign(across) * side / sourceAspectRatio,
            corner.V + Math.Sign(down) * side);
    }

    /// <summary>Creates a cut from an outline, checking that it's usable.</summary>
    /// <exception cref="ArgumentException">
    /// Too few or too many points, points outside the image, no area, or an invalid aspect ratio.
    /// </exception>
    public static PieceOutline Create(IEnumerable<ImagePoint> points, double sourceAspectRatio)
    {
        List<ImagePoint> list = [.. points];
        Require(double.IsFinite(sourceAspectRatio) && sourceAspectRatio > 0,
            "the image's aspect ratio must be a positive number");
        Require(list.Count is >= MinimumPoints and <= MaximumPoints,
            $"an outline needs {MinimumPoints} to {MaximumPoints} points");
        Require(list.All(p => double.IsFinite(p.U) && double.IsFinite(p.V)
                && p.U is >= 0 and <= 1 && p.V is >= 0 and <= 1),
            "outline points must be within the image");
        Require(Math.Abs(SignedArea(list)) > 1e-8, "the outline must enclose an area");
        Require(list.Max(p => p.U) - list.Min(p => p.U) > 1e-6
                && list.Max(p => p.V) - list.Min(p => p.V) > 1e-6,
            "the outline must have width and height");
        return new PieceOutline(list, sourceAspectRatio);
    }

    /// <summary>
    /// Draws the cut-out mask for the bounding box at <paramref name="width"/> ×
    /// <paramref name="height"/> pixels: 255 inside, 0 outside, with smooth (anti-aliased) edges.
    /// Row-major from the top-left.
    /// </summary>
    public byte[] RasterizeMask(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        // Sample each pixel on a 4×4 grid (16 samples) for smooth edges, filling each sample
        // row with the even-odd scanline method: it's fast, even for long freeform outlines.
        const int grid = 4;
        int[] coverage = new int[width * height];
        var crossings = new List<double>();
        double boxWidth = MaxU - MinU;
        double boxHeight = MaxV - MinV;

        for (int sampleRow = 0; sampleRow < height * grid; sampleRow++)
        {
            double v = MinV + (sampleRow + 0.5) / (height * grid) * boxHeight;
            crossings.Clear();
            for (int i = 0; i < Points.Count; i++)
            {
                ImagePoint a = Points[i];
                ImagePoint b = Points[(i + 1) % Points.Count];
                if ((a.V <= v) != (b.V <= v))
                {
                    crossings.Add(a.U + (v - a.V) / (b.V - a.V) * (b.U - a.U));
                }
            }

            crossings.Sort();
            int pixelRow = sampleRow / grid;
            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                // Sample columns whose centers fall inside [start, end).
                double start = (crossings[i] - MinU) / boxWidth * width * grid - 0.5;
                double end = (crossings[i + 1] - MinU) / boxWidth * width * grid - 0.5;
                int first = Math.Max(0, (int)Math.Ceiling(start));
                int last = Math.Min(width * grid - 1, (int)Math.Ceiling(end) - 1);
                for (int column = first; column <= last; column++)
                {
                    coverage[pixelRow * width + column / grid]++;
                }
            }
        }

        byte[] mask = new byte[width * height];
        for (int i = 0; i < mask.Length; i++)
        {
            mask[i] = (byte)Math.Round(coverage[i] * 255.0 / (grid * grid));
        }

        return mask;
    }

    /// <summary>
    /// True if a point on the source image is inside the cut (even-odd rule, the same one the
    /// mask uses).
    /// </summary>
    public bool Contains(ImagePoint point)
    {
        bool inside = false;
        for (int i = 0, j = Points.Count - 1; i < Points.Count; j = i++)
        {
            ImagePoint a = Points[i];
            ImagePoint b = Points[j];
            if ((a.V > point.V) != (b.V > point.V)
                && point.U < a.U + (point.V - a.V) / (b.V - a.V) * (b.U - a.U))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double SignedArea(List<ImagePoint> points)
    {
        double sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            ImagePoint a = points[i];
            ImagePoint b = points[(i + 1) % points.Count];
            sum += a.U * b.V - b.U * a.V;
        }

        return sum / 2;
    }

    private static void Require(bool condition, string problem)
    {
        if (!condition)
        {
            throw new ArgumentException($"Invalid piece outline: {problem}.");
        }
    }
}
