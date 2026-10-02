using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Where each body is drawn, and how big, in the system view (VISION.md REN-02; owner's choice:
/// a readable view by default, with a true-scale toggle). Display units are Earth radii, so an
/// Earth-sized planet is 1 unit across its radius in both modes.
/// </summary>
/// <remarks>
/// <para><b>True scale</b> divides everything by Earth's radius: honest, but planets become
/// specks far apart.</para>
/// <para><b>Readable</b> compresses sizes and distances with a power curve (big things shrink
/// more than small ones) and keeps every orbit clear of the bodies at each end. Directions are
/// never changed, so the system's arrangement stays true; only the spacing is compressed.</para>
/// Only drawing uses this. The simulation always works in real km.
/// </remarks>
public static class SystemLayout
{
    /// <summary>One display unit, in km: Earth's radius.</summary>
    public const double DisplayUnitKm = 6371.0;

    // How strongly the readable view compresses: 1 would be true scale, 0 would make everything
    // the same. 0.4 shows the Sun about 6.5 times Earth's size (instead of 109), and Earth's
    // orbit about 10 Sun-radii out (instead of 215).
    private const double ReadableExponent = 0.4;

    /// <summary>Every body's display position and radius at <paramref name="timeDays"/>.</summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static Dictionary<Guid, DisplayBody> At(
        IReadOnlyList<Body> bodies, double timeDays, SystemScale scale)
    {
        Dictionary<Guid, Vector3D> truePositions = SystemPositions.At(bodies, timeDays);
        var byId = bodies.ToDictionary(body => body.Id);
        var layout = new Dictionary<Guid, DisplayBody>();
        foreach (Body body in bodies)
        {
            Place(body, byId, truePositions, scale, layout);
        }

        return layout;
    }

    /// <summary>How big a body of this radius is drawn, in display units.</summary>
    public static double DisplayRadius(double radiusKm, SystemScale scale)
    {
        double units = radiusKm / DisplayUnitKm;
        return scale == SystemScale.True ? units : Math.Pow(units, ReadableExponent);
    }

    /// <summary>
    /// Where a body is drawn relative to its parent, for a true offset in km. In the readable
    /// view the distance is compressed, but never so far that the two bodies would touch.
    /// </summary>
    public static Vector3D DisplayOffset(
        Vector3D trueOffsetKm, double parentDisplayRadius, double childDisplayRadius,
        SystemScale scale)
    {
        double distanceKm = trueOffsetKm.Length;
        if (scale == SystemScale.True || distanceKm == 0)
        {
            return trueOffsetKm * (1.0 / DisplayUnitKm);
        }

        double displayDistance = parentDisplayRadius + childDisplayRadius
            + Math.Pow(distanceKm / DisplayUnitKm, ReadableExponent);
        return trueOffsetKm * (displayDistance / distanceKm);
    }

    /// <summary>
    /// The shape of a body's orbit as drawn, relative to its parent's display position: points
    /// around one full period, for drawing orbit lines.
    /// </summary>
    public static IReadOnlyList<Vector3D> OrbitPath(
        Body body, Body parent, SystemScale scale, int samples = 256)
    {
        if (body.Orbit is not Orbit orbit)
        {
            return [];
        }

        // Spread evenly along the curve, so very elongated orbits stay smooth where the body
        // moves fastest.
        return [.. OrbitMath.EvenlySpacedTimes(orbit, samples)
            .Select(time => OrbitPoint(body, parent, time, scale))];
    }

    /// <summary>
    /// Where on its drawn orbit a body is at a time, relative to its parent's display position
    /// (e.g. to mark an event on the orbit line). Zero for a body without an orbit.
    /// </summary>
    public static Vector3D OrbitPoint(Body body, Body parent, double timeDays, SystemScale scale)
    {
        if (body.Orbit is not Orbit orbit)
        {
            return Vector3D.Zero;
        }

        return DisplayOffset(OrbitMath.OffsetFromParent(orbit, timeDays),
            DisplayRadius(parent.RadiusKm, scale), DisplayRadius(body.RadiusKm, scale), scale);
    }

    private static DisplayBody Place(
        Body body,
        Dictionary<Guid, Body> byId,
        Dictionary<Guid, Vector3D> truePositions,
        SystemScale scale,
        Dictionary<Guid, DisplayBody> layout)
    {
        if (layout.TryGetValue(body.Id, out DisplayBody known))
        {
            return known;
        }

        double radius = DisplayRadius(body.RadiusKm, scale);
        Vector3D position = Vector3D.Zero;
        if (body.Orbit is Orbit orbit)
        {
            Body parent = byId[orbit.ParentId];
            DisplayBody parentPlace = Place(parent, byId, truePositions, scale, layout);
            Vector3D trueOffset = truePositions[body.Id] - truePositions[parent.Id];
            position = parentPlace.Position
                + DisplayOffset(trueOffset, parentPlace.Radius, radius, scale);
        }

        var place = new DisplayBody(position, radius);
        layout[body.Id] = place;
        return place;
    }
}
