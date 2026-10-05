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
    private SkyView(IReadOnlyList<SkyBody> bodies, SkyBody? star, double? solarTimeHours)
    {
        Bodies = bodies;
        Star = star;
        SolarTimeHours = solarTimeHours;
    }

    /// <summary>Every other body, nearest first.</summary>
    public IReadOnlyList<SkyBody> Bodies { get; }

    /// <summary>The body's own star (its sun), or null if it has none.</summary>
    public SkyBody? Star { get; }

    /// <summary>
    /// The time of day the sun shows at the spot, in hours of a 24-hour clock running over the
    /// body's own day: 12 when the star crosses the meridian (at its highest), 6 and 18 about
    /// sunrise and sunset at an equinox. Null without a star, or right at a pole, where the
    /// sun's time has no meaning.
    /// </summary>
    public double? SolarTimeHours { get; }

    /// <summary>
    /// The body seen at a direction (its shares east, north, and up, a unit vector), or null:
    /// one whose disc, drawn at least <paramref name="minRadiusDegrees"/> in radius, lies
    /// within <paramref name="toleranceDegrees"/> of it. Where discs overlap (a moon in front of
    /// the sun), the nearest body is the one seen.
    /// </summary>
    public SkyBody? BodyAt(double east, double north, double up, double minRadiusDegrees,
        double toleranceDegrees) => Bodies
        .Where(body =>
        {
            double cos = body.East * east + body.North * north + body.Up * up;
            double apart = double.RadiansToDegrees(Math.Acos(Math.Clamp(cos, -1, 1)));
            double radius = Math.Max(body.AngularDiameterDegrees / 2, minRadiusDegrees);
            return apart <= radius + toleranceDegrees;
        })
        .MinBy(body => body.DistanceKm);

    /// <summary>
    /// The sky from <paramref name="spot"/> on <paramref name="observer"/> (a globe),
    /// <paramref name="heightKm"/> above its radius, at <paramref name="timeDays"/>; or null
    /// if the observer has no surface or is a flat world (see <see cref="FromFlat"/>).
    /// </summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static SkyView? From(IReadOnlyList<Body> bodies, Body observer, GeoCoordinate spot,
        double heightKm, double timeDays)
    {
        if (!observer.HasSurface || observer.Shape == BodyShape.FlatDisc)
        {
            return null;
        }

        Vector3D localUp = SphericalPolygon.ToUnit(spot);
        (Vector3D localEast, Vector3D localNorth) = Tangents(localUp);
        return Seen(bodies, observer, localUp * (1 + heightKm / observer.RadiusKm),
            (localEast, localNorth, localUp), timeDays, solarTime: true);
    }

    /// <summary>
    /// The sky from <paramref name="spot"/> on a flat world <paramref name="observer"/>,
    /// <paramref name="heightKm"/> off its surface, at <paramref name="timeDays"/> (owner's
    /// choice: the same sky across the disc, as its light and seasons already are): on the top
    /// face, the sun's height is its angle above the disc, so day and night come everywhere at
    /// once; on the underside, the sky is the other way up. No solar time (the sun doesn't
    /// cross a meridian over a disc). Null unless the observer is a flat world.
    /// </summary>
    /// <exception cref="ArgumentException">The bodies' orbits are invalid.</exception>
    public static SkyView? FromFlat(IReadOnlyList<Body> bodies, Body observer, FlatSpot spot,
        double heightKm, double timeDays)
    {
        if (!observer.HasSurface || observer.Shape != BodyShape.FlatDisc)
        {
            return null;
        }

        return Seen(bodies, observer, FlatWalk.Point(spot, heightKm / observer.RadiusKm),
            FlatWalk.Frame(spot), timeDays, solarTime: false);
    }

    // The sky from an eye at a point in the space the body is drawn in (its radii; a flat
    // world's disc space), with the given east, north, and up there (in that space too).
    private static SkyView Seen(IReadOnlyList<Body> bodies, Body observer, Vector3D eyeLocal,
        (Vector3D East, Vector3D North, Vector3D Up) local, double timeDays, bool solarTime)
    {
        Dictionary<Guid, Vector3D> positions = SystemPositions.At(bodies, timeDays);
        Vector3D up = BodyOrientation.ShapeToSystem(observer, timeDays, local.Up);
        Vector3D east = BodyOrientation.ShapeToSystem(observer, timeDays, local.East);
        Vector3D north = BodyOrientation.ShapeToSystem(observer, timeDays, local.North);
        Vector3D eye = positions[observer.Id]
            + BodyOrientation.ShapeToSystem(observer, timeDays, eyeLocal) * observer.RadiusKm;
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

        double? time = !solarTime || star is null ? null
            : SolarTime(BodyOrientation.NorthPole(observer), up, east, star);
        return new SkyView([.. seen.OrderBy(b => b.DistanceKm)], star, time);
    }

    // The star's hour angle at the spot (how far west of the meridian it has turned, around
    // the body's axis) as a time of day: 12 + one hour for every 15 degrees.
    private static double? SolarTime(Vector3D pole, Vector3D up, Vector3D east, SkyBody star)
    {
        Vector3D meridian = up - pole * up.Dot(pole);  // The spot, seen down the axis
        if (meridian.Length < 1e-9)
        {
            return null;
        }

        meridian *= 1 / meridian.Length;
        // The star's direction in the system's frame, from its shares in the spot's frame.
        Vector3D north = Cross(up, east);
        Vector3D toStar = east * star.East + north * star.North + up * star.Up;
        double hourAngle = Math.Atan2(-toStar.Dot(east), toStar.Dot(meridian));
        return ((12 + double.RadiansToDegrees(hourAngle) / 15) % 24 + 24) % 24;
    }

    private static Vector3D Cross(Vector3D a, Vector3D b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

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
