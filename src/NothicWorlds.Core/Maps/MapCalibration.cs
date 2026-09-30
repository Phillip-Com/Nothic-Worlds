namespace NothicWorlds.Core.Maps;

/// <summary>
/// Grid calibration for imprecise maps (VISION.md MAP-05). Guide lines say where true latitudes
/// and longitudes are actually drawn on the image, in terms of the map type's own grid. The map is
/// then read from those positions. It works the same way for every map type, because it changes
/// which latitude/longitude the map type reads rather than bending the image.
/// </summary>
/// <remarks>
/// <para>Between guides, a smooth curve that never reverses keeps the lines in order. The poles
/// stay fixed. Longitude wraps around: the gap between the last guide and the first one (across
/// 180°) is handled like any other gap.</para>
/// <para>Immutable: every edit returns a new calibration.</para>
/// </remarks>
public sealed class MapCalibration
{
    /// <summary>The smallest gap allowed between neighboring guides, in degrees.</summary>
    public const double MinimumGapDegrees = 0.5;

    private readonly MonotoneCurve _latitudeCurve;
    private readonly MonotoneCurve _longitudeCurve;

    private MapCalibration(
        IReadOnlyList<CalibrationGuide> latitudes, IReadOnlyList<CalibrationGuide> longitudes)
    {
        Latitudes = latitudes;
        Longitudes = longitudes;
        _latitudeCurve = BuildLatitudeCurve(latitudes);
        _longitudeCurve = BuildLongitudeCurve(longitudes);
    }

    /// <summary>Latitude guides, south to north (the poles are fixed and not listed).</summary>
    public IReadOnlyList<CalibrationGuide> Latitudes { get; }

    /// <summary>Longitude guides, west to east, within [-180, 180).</summary>
    public IReadOnlyList<CalibrationGuide> Longitudes { get; }

    /// <summary>
    /// True if no guide has been moved, so the map is read exactly as its type says.
    /// </summary>
    public bool IsIdentity =>
        Latitudes.All(IsUnmoved) && Longitudes.All(IsUnmoved);

    /// <summary>
    /// The starting calibration (owner decision): guides every 30° of latitude (60°S to 60°N)
    /// and every 60° of longitude, none moved yet.
    /// </summary>
    public static MapCalibration CreateDefault()
    {
        return new MapCalibration(
            [.. new[] { -60.0, -30.0, 0.0, 30.0, 60.0 }.Select(Unmoved)],
            [.. new[] { -180.0, -120.0, -60.0, 0.0, 60.0, 120.0 }.Select(Unmoved)]);
    }

    /// <summary>Creates a calibration from saved guides, checking that they're valid.</summary>
    /// <exception cref="ArgumentException">The guides are out of range or out of order.</exception>
    public static MapCalibration Create(
        IEnumerable<CalibrationGuide> latitudes, IEnumerable<CalibrationGuide> longitudes)
    {
        List<CalibrationGuide> lat = [.. latitudes.OrderBy(guide => guide.Degrees)];
        List<CalibrationGuide> lon = [.. longitudes.OrderBy(guide => guide.Degrees)];
        ValidateLatitudes(lat);
        ValidateLongitudes(lon);
        return new MapCalibration(lat, lon);
    }

    /// <summary>Where the map type's grid has the true <paramref name="latitude"/> drawn.</summary>
    public double DrawnLatitude(double latitude)
    {
        return Math.Clamp(_latitudeCurve.Evaluate(Math.Clamp(latitude, -90.0, 90.0)), -90.0, 90.0);
    }

    /// <summary>
    /// Where the map type's grid has the true <paramref name="longitude"/> drawn, within
    /// [-180, 180).
    /// </summary>
    public double DrawnLongitude(double longitude)
    {
        double wrapped = Geometry.SphericalCoordinates.WrapLongitude(longitude);
        return Geometry.SphericalCoordinates.WrapLongitude(_longitudeCurve.Evaluate(wrapped));
    }

    /// <summary>
    /// The reverse of <see cref="DrawnLatitude"/>: which true latitude is drawn where the map
    /// type puts <paramref name="drawnLatitude"/>.
    /// </summary>
    public double TrueLatitude(double drawnLatitude)
    {
        return SolveIncreasing(DrawnLatitude, Math.Clamp(drawnLatitude, -90.0, 90.0), -90.0, 90.0);
    }

    /// <summary>
    /// The reverse of <see cref="DrawnLongitude"/>: which true longitude is drawn where the map
    /// type puts <paramref name="drawnLongitude"/>. Returned within [-180, 180).
    /// </summary>
    public double TrueLongitude(double drawnLongitude)
    {
        // Search one turn around the unwrapped curve, starting where the target could first
        // appear, so the answer is continuous with the guides.
        double target = Geometry.SphericalCoordinates.WrapLongitude(drawnLongitude);
        double start = _longitudeCurve.Evaluate(-180.0);
        while (target < start)
        {
            target += 360.0;
        }

        while (target >= start + 360.0)
        {
            target -= 360.0;
        }

        double result = SolveIncreasing(_longitudeCurve.Evaluate, target, -180.0, 180.0);
        return Geometry.SphericalCoordinates.WrapLongitude(result);
    }

    // Finds x in [low, high] where the increasing function f(x) equals target (bisection).
    private static double SolveIncreasing(
        Func<double, double> f, double target, double low, double high)
    {
        for (int i = 0; i < 60; i++)
        {
            double middle = (low + high) / 2.0;
            if (f(middle) < target)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return (low + high) / 2.0;
    }

    /// <summary>
    /// Moves latitude guide <paramref name="index"/> to be drawn at
    /// <paramref name="drawnAsDegrees"/>. The value is kept between its neighbors (and the
    /// poles), so lines can never cross.
    /// </summary>
    public MapCalibration WithLatitudeDrawnAs(int index, double drawnAsDegrees)
    {
        double below = index > 0 ? Latitudes[index - 1].DrawnAsDegrees : -90.0;
        double above = index < Latitudes.Count - 1 ? Latitudes[index + 1].DrawnAsDegrees : 90.0;
        double clamped = Math.Clamp(
            drawnAsDegrees, below + MinimumGapDegrees, above - MinimumGapDegrees);
        return new MapCalibration(Replace(Latitudes, index, clamped), Longitudes);
    }

    /// <summary>
    /// Moves longitude guide <paramref name="index"/> to be drawn at
    /// <paramref name="drawnAsDegrees"/>, kept between its neighbors (wrapping around 180°).
    /// </summary>
    public MapCalibration WithLongitudeDrawnAs(int index, double drawnAsDegrees)
    {
        CalibrationGuide guide = Longitudes[index];

        // Saved drawn values run in order around the globe (the first one, plus a turn, comes
        // after the last), so the neighbors across 180° are just shifted by a turn.
        double before = index > 0
            ? Longitudes[index - 1].DrawnAsDegrees
            : Longitudes[^1].DrawnAsDegrees - 360.0;
        double after = index < Longitudes.Count - 1
            ? Longitudes[index + 1].DrawnAsDegrees
            : Longitudes[0].DrawnAsDegrees + 360.0;

        // Take the requested value nearest the guide's current one (it may wrap past ±180°),
        // keep it between its neighbors, and less than half a turn from its true longitude.
        double requested = guide.DrawnAsDegrees
            + Geometry.SphericalCoordinates.LongitudeDelta(guide.DrawnAsDegrees, drawnAsDegrees);
        double low = Math.Max(before + MinimumGapDegrees, guide.Degrees - 179.0);
        double high = Math.Min(after - MinimumGapDegrees, guide.Degrees + 179.0);
        double clamped = Math.Clamp(requested, low, Math.Max(low, high));
        return new MapCalibration(Latitudes, Replace(Longitudes, index, clamped));
    }

    /// <summary>
    /// Adds a latitude guide at <paramref name="degrees"/>. It starts where that latitude is
    /// currently drawn, so the map barely changes (the smooth curve between guides is re-fitted,
    /// which can shift it slightly). Does nothing if there's already a guide within
    /// <see cref="MinimumGapDegrees"/>, or at the poles.
    /// </summary>
    public MapCalibration AddLatitude(double degrees)
    {
        if (Math.Abs(degrees) >= 90.0 - MinimumGapDegrees || IsNear(Latitudes, degrees))
        {
            return this;
        }

        var guide = new CalibrationGuide(degrees, DrawnLatitude(degrees));
        return new MapCalibration(
            [.. Latitudes.Append(guide).OrderBy(g => g.Degrees)], Longitudes);
    }

    /// <summary>Adds a longitude guide at <paramref name="degrees"/> (see
    /// <see cref="AddLatitude"/>).</summary>
    public MapCalibration AddLongitude(double degrees)
    {
        double wrapped = Geometry.SphericalCoordinates.WrapLongitude(degrees);
        if (IsNear(Longitudes, wrapped, periodic: true))
        {
            return this;
        }

        // Keep the drawn value continuous with its neighbors (it may lie beyond ±180°).
        var guide = new CalibrationGuide(wrapped, _longitudeCurve.Evaluate(wrapped));
        return new MapCalibration(
            Latitudes, [.. Longitudes.Append(guide).OrderBy(g => g.Degrees)]);
    }

    /// <summary>Removes latitude guide <paramref name="index"/>.</summary>
    public MapCalibration RemoveLatitude(int index)
    {
        return new MapCalibration([.. Latitudes.Where((_, i) => i != index)], Longitudes);
    }

    /// <summary>
    /// Removes longitude guide <paramref name="index"/>. The last longitude guide can't be removed
    /// (at least one is needed to anchor the wrap-around).
    /// </summary>
    public MapCalibration RemoveLongitude(int index)
    {
        return Longitudes.Count <= 1
            ? this
            : new MapCalibration(Latitudes, [.. Longitudes.Where((_, i) => i != index)]);
    }

    /// <summary>
    /// Samples <see cref="DrawnLatitude"/> at <paramref name="samples"/> evenly spaced
    /// latitudes from -90° to 90°, for the shader's lookup table.
    /// </summary>
    public float[] BakeLatitudeTable(int samples)
    {
        return Bake(samples, -90.0, 180.0, DrawnLatitude);
    }

    /// <summary>
    /// Samples the drawn longitude at <paramref name="samples"/> evenly spaced longitudes from
    /// -180° to 180°, for the shader's lookup table. Values aren't wrapped, so the table stays
    /// smooth for the GPU to blend; the shader wraps them.
    /// </summary>
    public float[] BakeLongitudeTable(int samples)
    {
        return Bake(samples, -180.0, 360.0, _longitudeCurve.Evaluate);
    }

    private static float[] Bake(int samples, double start, double span, Func<double, double> map)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 2);
        float[] table = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            table[i] = (float)map(start + span * i / (samples - 1));
        }

        return table;
    }

    private static MonotoneCurve BuildLatitudeCurve(IReadOnlyList<CalibrationGuide> guides)
    {
        // The poles are fixed anchors at both ends.
        List<CalibrationGuide> points =
            [new(-90, -90), .. guides, new(90, 90)];
        return new MonotoneCurve(
            [.. points.Select(p => p.Degrees)], [.. points.Select(p => p.DrawnAsDegrees)]);
    }

    private static MonotoneCurve BuildLongitudeCurve(IReadOnlyList<CalibrationGuide> guides)
    {
        if (guides.Count == 0)
        {
            return new MonotoneCurve([-540, 540], [-540, 540]);
        }

        // Repeat the guides one turn west and one turn east, so the curve wraps smoothly
        // across 180° and covers any longitude.
        List<CalibrationGuide> points =
        [
            .. guides.Select(g => new CalibrationGuide(g.Degrees - 360, g.DrawnAsDegrees - 360)),
            .. guides,
            .. guides.Select(g => new CalibrationGuide(g.Degrees + 360, g.DrawnAsDegrees + 360)),
        ];
        return new MonotoneCurve(
            [.. points.Select(p => p.Degrees)], [.. points.Select(p => p.DrawnAsDegrees)]);
    }

    private static void ValidateLatitudes(List<CalibrationGuide> guides)
    {
        double previousTrue = -90.0;
        double previousDrawn = -90.0;
        foreach (CalibrationGuide guide in guides.Append(new CalibrationGuide(90, 90)))
        {
            Require(double.IsFinite(guide.Degrees) && double.IsFinite(guide.DrawnAsDegrees),
                "calibration values must be numbers");
            Require(guide.Degrees > previousTrue && guide.DrawnAsDegrees > previousDrawn,
                "latitude guides must be between the poles and in order");
            previousTrue = guide.Degrees;
            previousDrawn = guide.DrawnAsDegrees;
        }
    }

    private static void ValidateLongitudes(List<CalibrationGuide> guides)
    {
        for (int i = 0; i < guides.Count; i++)
        {
            CalibrationGuide guide = guides[i];
            Require(double.IsFinite(guide.Degrees) && double.IsFinite(guide.DrawnAsDegrees),
                "calibration values must be numbers");
            Require(guide.Degrees >= -180.0 && guide.Degrees < 180.0,
                "longitude guides must be from -180° up to 180°");
            Require(Math.Abs(guide.DrawnAsDegrees - guide.Degrees) < 180.0,
                "a longitude guide can't be moved half a turn or more");
            if (i > 0)
            {
                Require(guide.DrawnAsDegrees > guides[i - 1].DrawnAsDegrees,
                    "longitude guides must stay in order");
            }
        }

        if (guides.Count > 1)
        {
            // Across 180°: the first guide, one turn later, must still come after the last.
            Require(guides[0].DrawnAsDegrees + 360.0 > guides[^1].DrawnAsDegrees,
                "longitude guides must stay in order across 180°");
        }
    }

    private static bool IsUnmoved(CalibrationGuide guide)
    {
        return Math.Abs(guide.Degrees - guide.DrawnAsDegrees) < 1e-9;
    }

    private static CalibrationGuide Unmoved(double degrees) => new(degrees, degrees);

    private static List<CalibrationGuide> Replace(
        IReadOnlyList<CalibrationGuide> guides, int index, double drawnAs)
    {
        List<CalibrationGuide> copy = [.. guides];
        copy[index] = copy[index] with { DrawnAsDegrees = drawnAs };
        return copy;
    }

    private static bool IsNear(
        IReadOnlyList<CalibrationGuide> guides, double degrees, bool periodic = false)
    {
        return guides.Any(guide => Math.Abs(periodic
            ? Geometry.SphericalCoordinates.LongitudeDelta(guide.Degrees, degrees)
            : guide.Degrees - degrees) < MinimumGapDegrees);
    }

    private static void Require(bool condition, string problem)
    {
        if (!condition)
        {
            throw new ArgumentException($"Invalid map calibration: {problem}.");
        }
    }
}
