using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The sky from a spot on a globe at a moment (VISION.md REN-06; owner's choice: true sizes
/// and places, with moon phases): every other body's direction above the horizon, how big it
/// looks, how much of it is lit, and how high the body's own star stands (which sets the sky's
/// colors). Deterministic, like everything in the simulation.
/// </summary>
/// <remarks>
/// Directions are worked out in true km from the bodies' real positions and the body's spin and
/// tilt at that moment (<see cref="BodyOrientation.ToSystem"/>), so sunrise, noon, and moonrise
/// fall where the seasons and eclipses say. Light bending in the air (which lifts the sun at
/// the horizon by about half a degree on Earth) isn't modeled.
/// </remarks>
public sealed class SkyView
{
    private SkyView(IReadOnlyList<SkyBody> bodies, SkyBody? star)
    {
        Bodies = bodies;
        Star = star;
    }

    /// <summary>Every other body, nearest first.</summary>
    public IReadOnlyList<SkyBody> Bodies { get; }

    /// <summary>The body's own star (its sun), or null if it has none.</summary>
    public SkyBody? Star { get; }

    /// <summary>
    /// The sky from <paramref name="spot"/> on <paramref name="observer"/> (a globe),
    /// <paramref name="heightKm"/> above its radius, at <paramref name="timeDays"/>; or null
    /// if the observer has no surface or is a flat world (whose sky isn't worked out yet).
    /// </summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static SkyView? From(IReadOnlyList<Body> bodies, Body observer, GeoCoordinate spot,
        double heightKm, double timeDays)
    {
        if (!observer.HasSurface || observer.Shape == BodyShape.FlatDisc)
        {
            return null;
        }

        Dictionary<Guid, Vector3D> positions = SystemPositions.At(bodies, timeDays);
        Vector3D localUp = SphericalPolygon.ToUnit(spot);
        Vector3D up = BodyOrientation.ToSystem(observer, timeDays, localUp);
        (Vector3D localEast, Vector3D localNorth) = Tangents(localUp);
        Vector3D east = BodyOrientation.ToSystem(observer, timeDays, localEast);
        Vector3D north = BodyOrientation.ToSystem(observer, timeDays, localNorth);
        Vector3D eye = positions[observer.Id] + up * (observer.RadiusKm + heightKm);
        Body? ownStar = Seasons.StarFor(bodies, observer);

        var seen = new List<SkyBody>();
        SkyBody? star = null;
        foreach (Body body in bodies)
        {
            if (body.Id == observer.Id || !positions.TryGetValue(body.Id, out Vector3D at))
            {
                continue;
            }

            SkyBody sky = Seen(body, at, eye, east, north, up,
                LitFraction(bodies, body, at, eye, positions));
            seen.Add(sky);
            if (body.Id == ownStar?.Id)
            {
                star = sky;
            }
        }

        return new SkyView([.. seen.OrderBy(b => b.DistanceKm)], star);
    }

    private static SkyBody Seen(Body body, Vector3D at, Vector3D eye, Vector3D east,
        Vector3D north, Vector3D up, double lit)
    {
        Vector3D toward = at - eye;
        double distance = toward.Length;
        Vector3D unit = toward * (1 / distance);
        double e = unit.Dot(east), n = unit.Dot(north), u = unit.Dot(up);
        double altitude = double.RadiansToDegrees(Math.Asin(Math.Clamp(u, -1, 1)));
        double azimuth = (double.RadiansToDegrees(Math.Atan2(e, n)) + 360) % 360;
        double diameter = 2 * double.RadiansToDegrees(
            Math.Asin(Math.Min(1, body.RadiusKm / Math.Max(distance, body.RadiusKm))));
        return new SkyBody(body.Id, body.Name, body.Kind, altitude, azimuth, diameter, distance,
            lit, e, n, u);
    }

    // How much of a body's face, seen from the eye, its star lights: (1 + cos(phase angle)) / 2,
    // the phase angle being the one at the body between its star and the eye. Stars, and bodies
    // without a star, count as fully lit.
    private static double LitFraction(IReadOnlyList<Body> bodies, Body body, Vector3D at,
        Vector3D eye, Dictionary<Guid, Vector3D> positions)
    {
        if (body.GivesLight || Seasons.StarFor(bodies, body) is not Body star
            || !positions.TryGetValue(star.Id, out Vector3D starAt))
        {
            return 1;
        }

        Vector3D toStar = starAt - at, toEye = eye - at;
        double cos = toStar.Dot(toEye) / (toStar.Length * toEye.Length);
        return (1 + Math.Clamp(cos, -1, 1)) / 2;
    }

    // East and north along the ground at a point on the body (its own frame); at a pole, any
    // pair at right angles.
    private static (Vector3D East, Vector3D North) Tangents(Vector3D up)
    {
        var east = new Vector3D(up.Z, 0, -up.X);
        if (east.Length < 1e-9)
        {
            east = new Vector3D(1, 0, 0);
        }

        east *= 1 / east.Length;
        var north = new Vector3D(
            up.Y * east.Z - up.Z * east.Y,
            up.Z * east.X - up.X * east.Z,
            up.X * east.Y - up.Y * east.X);
        return (east, north);
    }
}
