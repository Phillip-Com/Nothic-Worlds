using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

// Ground for the water tests, made from a rule for each cell's height.
internal static class WaterGround
{
    // Every cell's middle, worked out once for all the tests.
    private static readonly Lazy<Vector3D[]> _centers = new(() =>
    {
        var centers = new Vector3D[WaterCells.Count];
        Parallel.For(0, WaterCells.Count, cell => centers[cell] = WaterCells.Center(cell));
        return centers;
    });

    public static Vector3D CenterOf(int cell) => _centers.Value[cell];

    public static Vector3D At(double latitude, double longitude) =>
        SphericalPolygon.ToUnit(new GeoCoordinate(latitude, longitude));

    // The angle from a direction to a cell's middle, in degrees.
    public static double Degrees(Vector3D from, int cell) =>
        double.RadiansToDegrees(Math.Acos(Math.Clamp(from.Dot(CenterOf(cell)), -1, 1)));

    // Ground whose height at each cell is given by its middle.
    public static HeightGrid Make(Func<Vector3D, double> height) => Make((_, at) => height(at));

    // Ground whose height at each cell is given by its index and middle.
    public static HeightGrid Make(Func<int, Vector3D, double> height)
    {
        var cells = new short[WaterCells.Count];
        Vector3D[] centers = _centers.Value;
        Parallel.For(0, WaterCells.Count,
            cell => cells[cell] = (short)Math.Round(height(cell, centers[cell])));
        return HeightGrid.FromCells(cells);
    }

    // The cells within a number of degrees of a direction.
    public static HashSet<int> Within(Vector3D from, double degrees)
    {
        double limit = Math.Cos(double.DegreesToRadians(degrees));
        Vector3D[] centers = _centers.Value;
        return [.. Enumerable.Range(0, WaterCells.Count)
            .Where(cell => centers[cell].Dot(from) > limit)];
    }
}
