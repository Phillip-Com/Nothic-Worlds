namespace NothicWorlds.Rendering;

/// <summary>
/// Reads a value off the rings of mesh built around a first-person eye
/// (<see cref="FirstPersonGround"/>, <see cref="FlatPatch"/>), exactly as the mesh's flat
/// triangles carry it between their points: the middle point first, then each ring's points
/// going round, the rings spaced evenly in log distance from <c>inner</c> to <c>outer</c>.
/// </summary>
internal static class RingGrid
{
    /// <summary>
    /// The value at <paramref name="distance"/> from the middle, <paramref name="around"/>
    /// turns round from the first point of each ring (0 to 1), or null past the outer ring.
    /// </summary>
    public static double? ValueAt(IReadOnlyList<double> values, int rings, int segments,
        double inner, double outer, double distance, double around)
    {
        if (values.Count == 0 || distance >= outer)
        {
            return null;
        }

        double turn = (around % 1 + 1) % 1 * segments;
        int segment = (int)turn % segments, next = (segment + 1) % segments;
        double u = turn - Math.Floor(turn);
        if (distance < inner)
        {
            // In the fan around the middle.
            double outward = distance / inner;
            return values[0] * (1 - outward)
                + outward * (values[1 + segment] * (1 - u) + values[1 + next] * u);
        }

        int ring = Math.Clamp((int)(Math.Log(distance / inner) / Math.Log(outer / inner)
            * (rings - 1)), 0, rings - 2);
        double near = RingDistance(ring), far = RingDistance(ring + 1);
        double v = Math.Clamp((distance - near) / (far - near), 0, 1);
        int first = 1 + ring * segments, second = first + segments;
        double a = values[first + segment], b = values[second + segment];
        double c = values[first + next], d = values[second + next];

        // The quad's two triangles meet along the line from b to c.
        return u + v <= 1
            ? a + (c - a) * u + (b - a) * v
            : d + (b - d) * (1 - u) + (c - d) * (1 - v);

        double RingDistance(int index) =>
            inner * Math.Pow(outer / inner, index / (rings - 1.0));
    }
}
