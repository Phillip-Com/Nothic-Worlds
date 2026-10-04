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
        return At(bodies, SystemPositions.At(bodies, timeDays), scale);
    }

    /// <summary>
    /// The display positions and radii of the bodies in <paramref name="truePositions"/> (in
    /// true km), such as physics mode's (VISION.md SIM-03), where bodies may have left their
    /// designed orbits or merged away. Each is drawn relative to its nearest remaining parent
    /// up its designed chain (or the system's center), as designed positions are.
    /// </summary>
    public static Dictionary<Guid, DisplayBody> At(IReadOnlyList<Body> bodies,
        IReadOnlyDictionary<Guid, Vector3D> truePositions, SystemScale scale)
    {
        var byId = bodies.ToDictionary(body => body.Id);
        var layout = new Dictionary<Guid, DisplayBody>();
        foreach (Body body in bodies.Where(b => truePositions.ContainsKey(b.Id)))
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

        double displayDistance =
            DisplayDistance(distanceKm, parentDisplayRadius, childDisplayRadius, scale);
        return trueOffsetKm * (displayDistance / distanceKm);
    }

    /// <summary>
    /// How far from its parent's center a body at a true distance (in km) is drawn, in display
    /// units, as <see cref="DisplayOffset"/> draws it (e.g. for the orbit guide's rings).
    /// </summary>
    public static double DisplayDistance(double distanceKm, double parentDisplayRadius,
        double childDisplayRadius, SystemScale scale) =>
        scale == SystemScale.True || distanceKm == 0
            ? distanceKm / DisplayUnitKm
            : parentDisplayRadius + childDisplayRadius
                + Math.Pow(distanceKm / DisplayUnitKm, ReadableExponent);

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

        Vector3D offset = OrbitMath.OffsetFromParent(orbit, timeDays);
        double parentRadius = DisplayRadius(parent.RadiusKm, scale);
        double bodyRadius = DisplayRadius(body.RadiusKm, scale);
        return body.Branch is not null && parent.Tree is not null
            ? OnBranch(offset, parent, parentRadius, bodyRadius)
            : DisplayOffset(offset, parentRadius, bodyRadius, scale);
    }

    // A realm is drawn at its branch tip, scaled with its tree (whose drawn size the readable
    // view squeezes), and nudged out by its own radius so it rests on the tip.
    private static Vector3D OnBranch(
        Vector3D trueOffset, Body tree, double treeDisplayRadius, double realmDisplayRadius)
    {
        Vector3D tip = trueOffset * (treeDisplayRadius / tree.RadiusKm);
        double length = tip.Length;
        return length > 0 ? tip * ((length + realmDisplayRadius) / length) : tip;
    }

    private static DisplayBody Place(
        Body body,
        Dictionary<Guid, Body> byId,
        IReadOnlyDictionary<Guid, Vector3D> truePositions,
        SystemScale scale,
        Dictionary<Guid, DisplayBody> layout)
    {
        if (layout.TryGetValue(body.Id, out DisplayBody known))
        {
            return known;
        }

        double radius = DisplayRadius(body.RadiusKm, scale);
        Vector3D position = Vector3D.Zero;
        if (body.Orbit is not null && Anchor(body, byId, truePositions) is Body parent)
        {
            DisplayBody parentPlace = Place(parent, byId, truePositions, scale, layout);
            Vector3D trueOffset = truePositions[body.Id] - truePositions[parent.Id];
            position = parentPlace.Position + (body.Branch is not null && parent.Tree is not null
                ? OnBranch(trueOffset, parent, parentPlace.Radius, radius)
                : DisplayOffset(trueOffset, parentPlace.Radius, radius, scale));
        }
        else if (body.Orbit is not null)
        {
            // Everything it circled has merged away: draw it from the system's center.
            position = DisplayOffset(truePositions[body.Id], 0, radius, scale);
        }

        var place = new DisplayBody(position, radius);
        layout[body.Id] = place;
        return place;
    }

    // The body's nearest parent up its chain that has a position (all of them, for designed
    // positions), or null to draw it from the system's center.
    private static Body? Anchor(Body body, Dictionary<Guid, Body> byId,
        IReadOnlyDictionary<Guid, Vector3D> truePositions)
    {
        for (Body current = body; current.Orbit is Orbit orbit;)
        {
            current = byId[orbit.ParentId];
            if (truePositions.ContainsKey(current.Id))
            {
                return current;
            }
        }

        return null;
    }
}
