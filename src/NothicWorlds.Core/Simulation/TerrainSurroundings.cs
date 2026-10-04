using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The painted terrain at and around a spot, as it matters for the weather (VISION.md WTH-03;
/// owner's choice: water counts across the area around, other kinds where the spot is).
/// </summary>
/// <param name="Here">
/// The climate kind of the terrain at the spot, or null where it's unpainted (or its type is
/// unknown).
/// </param>
/// <param name="WaterShare">
/// How much of the area around is water, 0 to 1. Unpainted ground counts as not water, so an
/// unpainted planet gives 0.
/// </param>
/// <param name="RadiusKm">How far around the spot was looked at.</param>
public sealed record TerrainSurroundings(ClimateKind? Here, double WaterShare, double RadiusKm)
{
    /// <summary>
    /// How far around a spot water counts on a big enough body (about the reach of sea air).
    /// </summary>
    public const double WaterReachKm = 500;

    // On small bodies the area shrinks: at most this share of the radius.
    private const double MaxReachOfRadius = 0.3;

    // Rings of points looked at, evenly spaced out to the edge, and points per ring. The spot
    // itself is one more. Inner rings have as many points as outer ones, so nearer ground
    // counts for more.
    private const int Rings = 4;
    private const int PointsPerRing = 16;

    /// <summary>How much the weather at this spot is moderated by water, 0 to 1.</summary>
    public double Maritime => Here == ClimateKind.Water ? 1 : WaterShare;

    /// <summary>
    /// Looks at the terrain painted on a body at and around a spot. Deterministic.
    /// </summary>
    public static TerrainSurroundings At(
        Body body, IReadOnlyList<TerrainType> types, GeoCoordinate spot)
    {
        var kinds = types.ToDictionary(type => type.Code, type => type.Climate);
        TerrainGrid terrain = body.Surface.Terrain;
        double radiusKm = Math.Min(WaterReachKm, body.RadiusKm * MaxReachOfRadius);
        double radius = radiusKm / body.RadiusKm;  // Radians of arc
        Vector3D centre = SphericalPolygon.ToUnit(spot);

        ClimateKind? KindAt(Vector3D direction) =>
            kinds.TryGetValue(terrain.CodeAt(direction), out ClimateKind kind) ? kind : null;

        ClimateKind? here = KindAt(centre);
        int water = here == ClimateKind.Water ? 1 : 0;
        int looked = 1;
        bool flat = body.Shape == BodyShape.FlatDisc;
        Vector3D onFace = FlatDisc.TopPointFor(centre);
        (Vector3D east, Vector3D north) = Tangents(centre);
        for (int ring = 1; ring <= Rings; ring++)
        {
            double distance = radius * ring / Rings;
            for (int i = 0; i < PointsPerRing; i++)
            {
                double bearing = 2 * Math.PI * (i + 0.5 * (ring % 2)) / PointsPerRing;
                Vector3D? point = flat
                    ? AcrossFace(onFace, bearing, distance)
                    : centre * Math.Cos(distance)
                        + (east * Math.Sin(bearing) + north * Math.Cos(bearing))
                            * Math.Sin(distance);
                if (point is not Vector3D direction)
                {
                    continue;
                }

                water += KindAt(direction) == ClimateKind.Water ? 1 : 0;
                looked++;
            }
        }

        return new TerrainSurroundings(here, (double)water / looked, radiusKm);
    }

    // On a flat world the ground around is straight across the face (VISION.md BOD-02): the
    // point `distance` (in the globe's radii) away in a direction, as a globe direction, or null
    // past the rim.
    private static Vector3D? AcrossFace(Vector3D onFace, double bearing, double distance)
    {
        var point = new Vector3D(onFace.X + Math.Sin(bearing) * distance, onFace.Y,
            onFace.Z + Math.Cos(bearing) * distance);
        return Math.Sqrt(point.X * point.X + point.Z * point.Z) > FlatDisc.Radius
            ? null
            : FlatDisc.DirectionFor(point);
    }

    // Two directions along the ground at a point, at right angles (east and north, except at
    // the poles, where any pair will do).
    private static (Vector3D East, Vector3D North) Tangents(Vector3D up)
    {
        Vector3D east = new(up.Z, 0, -up.X);
        if (east.Length < 1e-9)
        {
            east = new Vector3D(1, 0, 0);
        }

        east = east * (1 / east.Length);
        Vector3D north = new(
            up.Y * east.Z - up.Z * east.Y,
            up.Z * east.X - up.X * east.Z,
            up.X * east.Y - up.Y * east.X);
        return (east, north);
    }
}
