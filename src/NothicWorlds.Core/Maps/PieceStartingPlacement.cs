using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Maps;

/// <summary>
/// Where a new piece starts on the globe (VISION.md MAP-02). A piece cut from the planet's own
/// map starts exactly where that region already appears, taking the map type and any calibration
/// into account, so the user moves it from there instead of hunting for it.
/// </summary>
public static class PieceStartingPlacement
{
    /// <summary>
    /// Finds where a cut from the planet's main map currently sits: the true globe position of
    /// its bounding box's center, and the arc from its left edge to its right edge. Returns null
    /// if the cut's center isn't on the map (e.g. a Robinson map's empty corner).
    /// </summary>
    /// <param name="outline">The cut, made on the main map image.</param>
    /// <param name="projection">The main map's type.</param>
    /// <param name="mapAspectRatio">The main map image's width divided by height.</param>
    /// <param name="calibration">The main map's calibration, if any.</param>
    public static (GeoCoordinate Center, double WidthDegrees)? FromMainMap(
        PieceOutline outline,
        MapProjection projection,
        double mapAspectRatio,
        MapCalibration? calibration)
    {
        var inverter = new MapProjectionInverter(projection, mapAspectRatio);
        double middleV = (outline.MinV + outline.MaxV) / 2.0;
        GeoCoordinate? center = ToTrue(
            inverter.Invert((outline.MinU + outline.MaxU) / 2.0, middleV), calibration);
        if (center is not GeoCoordinate middle)
        {
            return null;
        }

        // Measure out to each side edge; if one lies off the map (a curved outline), mirror the
        // other.
        double? left = ArcDegrees(
            middle, ToTrue(inverter.Invert(outline.MinU, middleV), calibration));
        double? right = ArcDegrees(
            middle, ToTrue(inverter.Invert(outline.MaxU, middleV), calibration));
        double width = (left, right) switch
        {
            (double l, double r) => l + r,
            (double l, null) => 2.0 * l,
            (null, double r) => 2.0 * r,
            _ => 30.0,
        };

        return (middle, Math.Clamp(
            width, PieceProjection.MinimumWidthDegrees, PieceProjection.MaximumWidthDegrees));
    }

    // The main map's drawn position → the true position, undoing any calibration.
    private static GeoCoordinate? ToTrue(GeoCoordinate? drawn, MapCalibration? calibration)
    {
        if (drawn is not GeoCoordinate position || calibration is null)
        {
            return drawn;
        }

        return new GeoCoordinate(
            calibration.TrueLatitude(position.LatitudeDegrees),
            calibration.TrueLongitude(position.LongitudeDegrees));
    }

    private static double? ArcDegrees(GeoCoordinate from, GeoCoordinate? to)
    {
        return to is GeoCoordinate end ? SphericalCoordinates.ArcDegrees(from, end) : null;
    }
}
