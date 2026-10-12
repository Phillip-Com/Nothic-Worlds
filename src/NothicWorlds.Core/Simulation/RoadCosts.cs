using NothicWorlds.Core.Model;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// How hard a road of one style finds each step from cell to cell (VISION.md BOD-13; owner's
/// choices: dirt paths go around steep ground and thick plants, a stone highway keeps a
/// straight, even course and bridges what it meets, and later roads favor the ways of earlier
/// ones). A step costs its length times how hard the going is, plus a bridge where it crosses
/// into a river. The sea and lakes can't be crossed.
/// </summary>
public sealed class RoadCosts
{
    private readonly HeightGrid _ground;
    private readonly double _radiusMeters;
    private readonly Func<int, bool> _isWater;
    private readonly Func<int, PlantCover> _plantsAt;
    private readonly IReadOnlyDictionary<int, double> _riverWidthKm;
    private readonly bool[] _worn;
    private readonly RoadStyle _style;

    /// <summary>
    /// The costs for a road of <paramref name="style"/> over ground of these heights (meters)
    /// on a body <paramref name="radiusKm"/> in radius, where <paramref name="isWater"/> marks
    /// the sea and lakes, <paramref name="plantsAt"/> gives what grows in each cell,
    /// <paramref name="riverWidthKm"/> the widest river through each cell it runs through, and
    /// <paramref name="worn"/> the cells earlier roads run through.
    /// </summary>
    public RoadCosts(HeightGrid ground, double radiusKm, Func<int, bool> isWater,
        Func<int, PlantCover> plantsAt, IReadOnlyDictionary<int, double> riverWidthKm,
        bool[] worn, RoadStyle style)
    {
        (_ground, _radiusMeters, _isWater, _plantsAt, _riverWidthKm, _worn, _style) =
            (ground, radiusKm * 1000, isWater, plantsAt, riverWidthKm, worn, style);
    }

    /// <summary>
    /// The least a step can cost for each radian it covers (on another road's way, level and
    /// bare): what the search may count on for the distance still to go.
    /// </summary>
    public double LeastPerRadian => WornShare(_style);

    /// <summary>Whether a road can go into a cell at all (not into the sea or a lake).</summary>
    public bool IsOpen(int cell) => !_isWater(cell);

    /// <summary>
    /// What the step from <paramref name="from"/> to its neighbor <paramref name="to"/> costs,
    /// <paramref name="radians"/> apart.
    /// </summary>
    public double Step(int from, int to, double radians)
    {
        double meters = Math.Max(radians * _radiusMeters, 1);
        double grade = Math.Abs(HeightOf(to) - HeightOf(from)) / meters;
        double going = SlopeFactor(_style, grade) + PlantFactor(_style, _plantsAt(to));
        if (_worn[to])
        {
            going *= WornShare(_style);
        }

        double cost = radians * going;
        if (_riverWidthKm.TryGetValue(to, out double widthKm)
            && !_riverWidthKm.ContainsKey(from))
        {
            cost += radians * BridgeFactor(_style, widthKm);
        }

        return cost;
    }

    private short HeightOf(int cell) => _ground.HeightAt(WaterCells.CellOf(cell));

    // How much harder a slope makes the going. A highway's crews cut, bank, and tunnel to an
    // even grade, so it barely minds; carts and walkers feel steep ground more and more.
    private static double SlopeFactor(RoadStyle style, double grade) => style switch
    {
        RoadStyle.DirtPaths => 1 + Square(grade / 0.12),
        RoadStyle.CartTrack => 1 + Square(grade / 0.08),
        _ => 1 + 3 * grade,
    };

    // How much harder thick plants make the going: walkers go around woods and marsh; a
    // highway's crews clear its way, minding only marsh (they must build it up).
    private static double PlantFactor(RoadStyle style, PlantCover cover)
    {
        double thickness = cover switch
        {
            PlantCover.Woodland or PlantCover.Scrub => 0.3,
            PlantCover.Rocky => 0.5,
            PlantCover.Forest or PlantCover.ConiferForest => 0.8,
            PlantCover.Marsh => 1.0,
            PlantCover.Jungle => 1.5,
            _ => 0,
        };
        return style switch
        {
            RoadStyle.DirtPaths => thickness,
            RoadStyle.CartTrack => thickness * 0.8,
            _ => cover == PlantCover.Marsh ? 0.3 : 0,
        };
    }

    // How much of the going is left on a way earlier roads take: people take the worn way.
    private static double WornShare(RoadStyle style) => style switch
    {
        RoadStyle.DirtPaths => 0.6,
        RoadStyle.CartTrack => 0.7,
        _ => 0.85,
    };

    // What crossing a river costs, in steps' worth: walkers look harder for a narrow place
    // to cross (a wooden bridge or a ford); a stone bridge is built where the road needs it.
    private static double BridgeFactor(RoadStyle style, double widthKm) => style switch
    {
        RoadStyle.DirtPaths or RoadStyle.CartTrack => 6 + 20 * widthKm,
        _ => 2 + 4 * widthKm,
    };

    private static double Square(double value) => value * value;
}
