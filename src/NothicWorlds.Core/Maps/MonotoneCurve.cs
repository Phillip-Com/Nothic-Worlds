namespace NothicWorlds.Core.Maps;

/// <summary>
/// A smooth curve through points that only ever goes up (monotone cubic interpolation, the
/// Fritsch–Carlson method). Unlike an ordinary smooth curve, it never overshoots between points,
/// so the order of calibration guide lines is always preserved. Outside the first and last
/// points, it continues in a straight line.
/// </summary>
internal sealed class MonotoneCurve
{
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _slopes;

    /// <param name="x">Strictly increasing positions (at least two).</param>
    /// <param name="y">Strictly increasing values at those positions.</param>
    public MonotoneCurve(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        _x = [.. x];
        _y = [.. y];
        _slopes = ComputeSlopes(_x, _y);
    }

    public double Evaluate(double x)
    {
        if (x <= _x[0])
        {
            return _y[0] + _slopes[0] * (x - _x[0]);
        }

        int last = _x.Length - 1;
        if (x >= _x[last])
        {
            return _y[last] + _slopes[last] * (x - _x[last]);
        }

        int i = Array.BinarySearch(_x, x);
        if (i >= 0)
        {
            return _y[i];
        }

        i = ~i - 1;  // The segment [i, i + 1] containing x.
        double h = _x[i + 1] - _x[i];
        double t = (x - _x[i]) / h;
        double t2 = t * t;
        double t3 = t2 * t;

        // Cubic Hermite basis: matches the values and slopes at both ends of the segment.
        return (2 * t3 - 3 * t2 + 1) * _y[i]
            + (t3 - 2 * t2 + t) * h * _slopes[i]
            + (-2 * t3 + 3 * t2) * _y[i + 1]
            + (t3 - t2) * h * _slopes[i + 1];
    }

    private static double[] ComputeSlopes(double[] x, double[] y)
    {
        int n = x.Length;
        double[] secants = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            secants[i] = (y[i + 1] - y[i]) / (x[i + 1] - x[i]);
        }

        double[] slopes = new double[n];
        slopes[0] = secants[0];
        slopes[n - 1] = secants[n - 2];
        for (int i = 1; i < n - 1; i++)
        {
            // Weighted harmonic mean of the neighboring secants keeps the curve monotone.
            double w1 = 2 * (x[i + 1] - x[i]) + (x[i] - x[i - 1]);
            double w2 = (x[i + 1] - x[i]) + 2 * (x[i] - x[i - 1]);
            slopes[i] = (w1 + w2) / (w1 / secants[i - 1] + w2 / secants[i]);
        }

        return slopes;
    }
}
