using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Maps;

/// <summary>
/// Finds the globe position shown at a point in a map image: the reverse of
/// <see cref="MapProjections.ToImagePosition"/>, for any map type (VISION.md MAP-05 uses it to
/// turn mouse drags on the image into latitudes and longitudes). Some map types have no neat
/// reverse formula, so it works numerically: a coarse lattice of image positions is computed
/// once, then the nearest lattice point is refined.
/// </summary>
/// <remarks>Create one per map type and image shape, and reuse it.</remarks>
public sealed class MapProjectionInverter
{
    private const double LatticeStepDegrees = 2.0;
    private const int RefineSteps = 25;

    // How close (in image fractions) a found position must be to the point to count as on the
    // map; points outside the map's outline (e.g. Robinson's corners) don't match anything.
    private const double MatchTolerance = 1e-4;

    private readonly MapProjection _projection;
    private readonly double _aspectRatio;
    private readonly List<(double U, double V, double Latitude, double Longitude)> _lattice = [];

    /// <param name="projection">The map type.</param>
    /// <param name="aspectRatio">Image width divided by height (used by Flat maps).</param>
    public MapProjectionInverter(MapProjection projection, double aspectRatio)
    {
        _projection = projection;
        _aspectRatio = aspectRatio;
        for (double lat = -90.0; lat <= 90.0; lat += LatticeStepDegrees)
        {
            for (double lon = -180.0; lon < 180.0; lon += LatticeStepDegrees)
            {
                MapImagePosition p = Project(lat, lon);
                if (!p.IsOutsideMap)
                {
                    _lattice.Add((p.U, p.V, lat, lon));
                }
            }
        }
    }

    /// <summary>
    /// Returns the globe position drawn at image position (<paramref name="u"/>,
    /// <paramref name="v"/>), each 0 to 1 from the top-left, or null if that point isn't part of
    /// the map (outside its outline, or a region the map doesn't cover).
    /// </summary>
    public GeoCoordinate? Invert(double u, double v)
    {
        (double U, double V, double Latitude, double Longitude) start =
            _lattice.MinBy(p => Square(p.U - u) + Square(p.V - v));
        double lat = start.Latitude;
        double lon = start.Longitude;

        // Newton steps using a numerically measured slope (how the image position changes
        // with latitude and longitude near the current guess).
        const double h = 1e-4;
        for (int i = 0; i < RefineSteps; i++)
        {
            MapImagePosition p = Project(lat, lon);
            double du = u - p.U;
            double dv = v - p.V;
            if (Square(du) + Square(dv) < 1e-18)
            {
                break;
            }

            double stepLat = lat + h > 90.0 ? -h : h;
            MapImagePosition pLat = Project(lat + stepLat, lon);
            MapImagePosition pLon = Project(lat, lon + h);
            double a = (pLat.U - p.U) / stepLat;
            double b = (pLon.U - p.U) / h;
            double c = (pLat.V - p.V) / stepLat;
            double d = (pLon.V - p.V) / h;
            double determinant = a * d - b * c;
            if (Math.Abs(determinant) < 1e-12)
            {
                break;  // At a pole or map edge the slope is undefined; keep the guess.
            }

            // Solve [a b; c d] · [dLat dLon] = [du dv], limiting each step to stay stable.
            double dLat = Math.Clamp((d * du - b * dv) / determinant, -5.0, 5.0);
            double dLon = Math.Clamp((a * dv - c * du) / determinant, -5.0, 5.0);
            lat = Math.Clamp(lat + dLat, -90.0, 90.0);
            lon += dLon;
        }

        MapImagePosition final = Project(lat, lon);
        bool matches = !final.IsOutsideMap
            && Square(final.U - u) + Square(final.V - v) < Square(MatchTolerance);
        return matches ? new GeoCoordinate(lat, lon) : null;
    }

    private MapImagePosition Project(double latitude, double longitude)
    {
        return MapProjections.ToImagePosition(
            new GeoCoordinate(latitude, longitude), _projection, _aspectRatio);
    }

    private static double Square(double x) => x * x;
}
