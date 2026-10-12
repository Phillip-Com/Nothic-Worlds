using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public sealed class PathStretchesTests
{
    private const double RadiusMeters = RoadProfileTests.RadiusKm * 1000;

    // A way east along the equator from longitude 0, with points every 100 m, 1 km long.
    private static readonly List<Vector3D> _way = [.. Enumerable.Range(0, 11)
        .Select(i => WaterGround.At(0, i * 100 / RoadProfileTests.MetersPerDegree))];

    [Fact]
    public void WithNoSpans_TheWholeWayIsOneStretch()
    {
        (List<Vector3D> points, bool inSpan) = Assert.Single(
            PathStretches.Split(_way, RadiusMeters, []));

        Assert.Equal(_way, points);
        Assert.False(inSpan);
    }

    [Fact]
    public void ItsCutWhereEachSpanStartsAndEnds_IntoStretchesInAndOutOfThem()
    {
        List<(List<Vector3D> Points, bool InSpan)> stretches = PathStretches.Split(_way,
            RadiusMeters, [(250, 420), (700, 1_000)]);

        Assert.Equal([false, true, false, true], stretches.Select(s => s.InSpan));
        double[] ends = [.. stretches.Select(s => RoadProfileTests.East(s.Points[^1]))];
        Assert.Equal([250, 420, 700, 1_000], ends.Select(e => Math.Round(e, 3)));

        // Each stretch starts where the one before ends.
        for (int i = 1; i < stretches.Count; i++)
        {
            Assert.Equal(stretches[i - 1].Points[^1], stretches[i].Points[0]);
        }
    }
}
