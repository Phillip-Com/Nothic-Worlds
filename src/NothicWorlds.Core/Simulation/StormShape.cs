using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// One storm's clouds, rain, and winds around it (VISION.md WTH-02). A cyclone is a wide
/// spiral with a thick middle and a front trailing toward the equator and west; a tropical
/// storm is a tight spiral around a clear eye. Both turn counterclockwise in the north and
/// clockwise in the south.
/// </summary>
internal readonly struct StormShape
{
    // How far out a storm is looked at, in its radii (its front trails farthest).
    private const double Reach = 2.6;

    // Rain at a storm's thickest cloud at full strength, in mm per standard day.
    private const double CycloneRainMm = 25.0;
    private const double TropicalRainMm = 100.0;

    // The strongest winds, in m/s, and how far out they blow strongest, in radii.
    private const double CycloneWindMs = 20.0;
    private const double CycloneWindRadius = 0.45;
    private const double TropicalWindMs = 50.0;
    private const double TropicalWindRadius = 0.15;

    private readonly Vector3D _center;
    private readonly Vector3D _east;
    private readonly Vector3D _north;
    private readonly double _radiusKm;
    private readonly double _bodyRadiusKm;
    private readonly double _cosReach;
    private readonly int _hemisphere;

    public StormShape(Storm storm, double bodyRadiusKm)
    {
        Storm = storm;
        _center = SphericalPolygon.ToUnit(storm.Center);
        (_east, _north) = Tangents(_center);
        _radiusKm = storm.RadiusKm;
        _bodyRadiusKm = bodyRadiusKm;
        _cosReach = Math.Cos(Math.Min(Math.PI, Reach * storm.RadiusKm / bodyRadiusKm));
        _hemisphere = storm.Center.LatitudeDegrees >= 0 ? 1 : -1;
    }

    public Storm Storm { get; }

    /// <summary>
    /// The storm at a unit direction: its cloud (0 to 1), its rain (mm per standard day), and
    /// its winds east and north (m/s).
    /// </summary>
    public (double Cloud, double RainMm, double WindEastMs, double WindNorthMs) At(
        Vector3D direction)
    {
        if (direction.Dot(_center) < _cosReach || Storm.Strength <= 0)
        {
            return default;
        }

        // Kilometers east and north of the middle, on the plane touching it: close enough
        // across a storm.
        double x = direction.Dot(_east) * _bodyRadiusKm;
        double y = direction.Dot(_north) * _bodyRadiusKm;
        double r = Math.Sqrt(x * x + y * y) / _radiusKm;
        double turn = _hemisphere * Math.Atan2(y, x);  // Counterclockwise in the north
        bool tropical = Storm.Kind == StormKind.Tropical;

        // Even a middling storm is overcast, so its cloud thickens faster than its strength.
        double shape = tropical ? TropicalCloud(r, turn) : CycloneCloud(x, y, r, turn);
        double cloud = Math.Sqrt(Storm.Strength) * shape;
        double rain = shape * shape * Storm.Strength * (tropical ? TropicalRainMm : CycloneRainMm);
        (double windEast, double windNorth) = Wind(x, y, r, tropical);
        return (cloud, rain, windEast, windNorth);
    }

    // A cyclone's cloud at (x, y) km, r radii out, `turn` radians around.
    private double CycloneCloud(double x, double y, double r, double turn)
    {
        double core = Bell(r, 0.5);
        double arm = 0.5 + 0.5 * Math.Cos(turn + 2.5 * Math.Log(r + 0.08));
        double spiral = Math.Pow(arm, 1.5) * Bell(r, 1.15);

        // The front trails toward the equator and the west, as a narrow band.
        double frontEast = -0.45, frontNorth = -0.89 * _hemisphere;
        double along = (x * frontEast + y * frontNorth) / _radiusKm;
        double across = Math.Abs(x * frontNorth - y * frontEast) / _radiusKm;
        double front = Bell(across, 0.14) * SmoothStep(0.1, 0.4, along)
            * (1 - SmoothStep(1.4, 2.4, along));
        return Math.Max(core, Math.Max(0.9 * spiral, 0.85 * front));
    }

    // A tropical storm's cloud, r radii out, `turn` radians around: tight bands around an eye.
    private static double TropicalCloud(double r, double turn)
    {
        double eye = SmoothStep(0.04, 0.1, r);
        double arm = 0.5 + 0.5 * Math.Cos(2 * turn + 5 * Math.Log(r + 0.05));
        return eye * Math.Max(Bell(r, 0.45), 0.9 * arm * Bell(r, 0.9));
    }

    // The storm's winds: round its middle (counterclockwise in the north), strongest a little
    // way out, and drawn a little inward.
    private (double East, double North) Wind(double x, double y, double r, bool tropical)
    {
        double distance = Math.Sqrt(x * x + y * y);
        if (distance < 1e-6)
        {
            return default;
        }

        double strongest = (tropical ? TropicalWindMs : CycloneWindMs) * Storm.Strength;
        double peakAt = tropical ? TropicalWindRadius : CycloneWindRadius;
        double speed = strongest * (r / peakAt) * Math.Exp(1 - r / peakAt);
        double aroundEast = -y / distance * _hemisphere, aroundNorth = x / distance * _hemisphere;
        double inEast = -x / distance, inNorth = -y / distance;
        return (speed * (aroundEast + 0.3 * inEast), speed * (aroundNorth + 0.3 * inNorth));
    }

    // East and north along the ground at a point (at a pole, any pair at right angles).
    internal static (Vector3D East, Vector3D North) Tangents(Vector3D up)
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

    private static double Bell(double offset, double width) =>
        Math.Exp(-(offset / width) * (offset / width));

    private static double SmoothStep(double from, double to, double value)
    {
        double t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
