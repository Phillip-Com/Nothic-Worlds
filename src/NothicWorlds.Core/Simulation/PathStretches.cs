using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Splits a way into stretches by how far along it they are: those within given spans (a
/// highway's tunnels, drawn dashed on the minimap; VISION.md BOD-13) and those between.
/// </summary>
public static class PathStretches
{
    /// <summary>
    /// <paramref name="path"/> (unit directions, first first, on a body
    /// <paramref name="radiusMeters"/> in radius) in its stretches in order, each with whether
    /// it lies within one of <paramref name="spans"/> (meters from the path's first point,
    /// first first, apart), cut where a span starts or ends. With no spans, the whole path.
    /// </summary>
    public static List<(List<Vector3D> Points, bool InSpan)> Split(
        IReadOnlyList<Vector3D> path, double radiusMeters,
        IReadOnlyList<(double From, double To)> spans)
    {
        var stretches = new List<(List<Vector3D>, bool)>();
        if (path.Count == 0)
        {
            return stretches;
        }

        double[] along = RoadProfile.AlongOf(path, radiusMeters);
        List<double> cuts = [.. spans.SelectMany(span => new[] { span.From, span.To })
            .Where(meters => meters > 0 && meters < along[^1])];
        bool Within(double meters) =>
            spans.Any(span => meters >= span.From && meters <= span.To);

        // Each stretch is within a span or not as its middle is (it's cut at every span's ends).
        List<Vector3D> stretch = [path[0]];
        double start = 0;
        int cut = 0;
        for (int i = 1; i < path.Count; i++)
        {
            for (; cut < cuts.Count && cuts[cut] < along[i]; cut++)
            {
                if (cuts[cut] <= start)
                {
                    continue;
                }

                double t = (cuts[cut] - along[i - 1]) / (along[i] - along[i - 1]);
                Vector3D at = path[i - 1] * (1 - t) + path[i] * t;
                at *= 1 / at.Length;
                stretch.Add(at);
                stretches.Add((stretch, Within((start + cuts[cut]) / 2)));
                (stretch, start) = ([at], cuts[cut]);
            }

            stretch.Add(path[i]);
        }

        stretches.Add((stretch, Within((start + along[^1]) / 2)));
        return stretches;
    }
}
