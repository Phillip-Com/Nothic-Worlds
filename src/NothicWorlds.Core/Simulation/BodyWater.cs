using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// All the standing and running water on one body, worked out from its ground (VISION.md
/// BOD-11, BOD-09): its sea (below its water level, and its water terrain), where each lake
/// lies, and every river's course, the lakes' outflows included. Worked out again whenever the
/// ground, the lakes, or the rivers change.
/// </summary>
public sealed class BodyWater
{
    private readonly bool[] _sea;
    private readonly bool[] _lake;

    private BodyWater(bool[] sea, bool[] lake, Dictionary<Guid, LakeShape> lakes,
        HeightGrid lakeLevels, IReadOnlyList<RiverCourseShown> rivers)
    {
        (_sea, _lake, Lakes, LakeLevels, Rivers) = (sea, lake, lakes, lakeLevels, rivers);
    }

    /// <summary>Each lake's shape, by its id.</summary>
    public IReadOnlyDictionary<Guid, LakeShape> Lakes { get; }

    /// <summary>
    /// The lakes' surface heights for drawing them: each lake's cells and those just around
    /// them hold its level (so the shore follows the ground between), the rest
    /// <see cref="HeightGrid.MinHeightMeters"/>.
    /// </summary>
    public HeightGrid LakeLevels { get; }

    /// <summary>Every river's course, the lakes' outflows last.</summary>
    public IReadOnlyList<RiverCourseShown> Rivers { get; }

    /// <summary>Whether a cell is in the sea.</summary>
    public bool IsSea(int cell) => _sea[cell];

    /// <summary>Whether a cell is under water: the sea or a lake.</summary>
    public bool IsWater(int cell) => _sea[cell] || _lake[cell];

    /// <summary>
    /// Works out the water on <paramref name="body"/>, whose ground (terrain-shaped and
    /// sculpted, meters) is <paramref name="ground"/>, with the world's lakes, rivers, and
    /// terrain types (those on other bodies are left out).
    /// </summary>
    public static BodyWater For(Body body, HeightGrid ground, IReadOnlyList<Lake> lakes,
        IReadOnlyList<River> rivers, IReadOnlyList<TerrainType> types)
    {
        var sea = new bool[WaterCells.Count];
        if (SeaRule(body, ground, types) is Func<int, bool> isSea)
        {
            Parallel.For(0, WaterCells.Count, cell => sea[cell] = isSea(cell));
        }

        var lake = new bool[WaterCells.Count];
        var levels = new short[WaterCells.Count];
        Array.Fill(levels, HeightGrid.MinHeightMeters);
        var shapes = new Dictionary<Guid, LakeShape>();
        Span<int> around = stackalloc int[WaterCells.MaxNeighbors];
        foreach (Lake each in lakes.Where(l => l.BodyId == body.Id))
        {
            LakeShape shape = LakeFill.Fill(ground, SphericalPolygon.ToUnit(each.Spot),
                each.LevelMeters, cell => sea[cell]);
            shapes[each.Id] = shape;
            short surface = (short)each.LevelMeters;
            foreach (int cell in shape.Cells)
            {
                lake[cell] = true;
                levels[cell] = Math.Max(levels[cell], surface);
                foreach (int next in around[..WaterCells.Neighbors(cell, around)])
                {
                    if (!sea[next])
                    {
                        levels[next] = Math.Max(levels[next], surface);
                    }
                }
            }
        }

        bool IsWater(int cell) => sea[cell] || lake[cell];
        var courses = new List<RiverCourseShown>();
        foreach (River river in rivers.Where(r => r.BodyId == body.Id))
        {
            courses.Add(river.Kind == RiverKind.Drawn
                ? new RiverCourseShown(river.Id, null, RiverKind.Drawn, river.WidthKm,
                    [.. river.Points.Select(SphericalPolygon.ToUnit)], ReachesWater: true)
                : Natural(river.Id, null, river.WidthKm,
                    RiverCourse.Trace(ground, SphericalPolygon.ToUnit(river.Points[0]),
                        IsWater)));
        }

        foreach (Lake each in lakes.Where(l => l.BodyId == body.Id && l.FlowsOut))
        {
            if (shapes[each.Id] is { Outflow: int outflow, Cells: var cells })
            {
                courses.Add(Natural(null, each.Id, OutflowWidthKm,
                    RiverCourse.Trace(ground, WaterCells.Center(outflow), IsWater,
                        cells.Contains)));
            }
        }

        HeightGrid lakeLevels = shapes.Count == 0
            ? HeightGrid.Empty
            : HeightGrid.FromCells(levels);
        return new BodyWater(sea, lake, shapes, lakeLevels, courses);
    }

    /// <summary>
    /// Which cells of <paramref name="body"/> are in its sea, on ground of these heights: those
    /// below its water level, and those painted with a type whose climate is water. Null if it
    /// has no sea.
    /// </summary>
    public static Func<int, bool>? SeaRule(Body body, HeightGrid ground,
        IReadOnlyList<TerrainType> types)
    {
        var waterCodes = new HashSet<byte>(types
            .Where(type => type.Climate == ClimateKind.Water)
            .Select(type => type.Code));
        TerrainGrid terrain = body.Surface.Terrain;
        bool painted = !terrain.IsEmpty && waterCodes.Count > 0;
        int? level = body.WaterLevelMeters;
        if (!painted && level is null)
        {
            return null;
        }

        return cell =>
        {
            CubeCell at = WaterCells.CellOf(cell);
            return (level is int surface && ground.HeightAt(at) < surface)
                || (painted && waterCodes.Contains(terrain.CodeAt(at)));
        };
    }

    /// <summary>How wide a lake's outflow is at its mouth, in km.</summary>
    public const double OutflowWidthKm = 1;

    private static RiverCourseShown Natural(Guid? riverId, Guid? lakeId, double widthKm,
        RiverPath path) =>
        new(riverId, lakeId, RiverKind.Natural, widthKm, path.Points, path.ReachesWater);
}
