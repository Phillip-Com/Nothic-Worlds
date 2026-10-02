namespace NothicWorlds.Core.Simulation;

/// <summary>
/// Refines a moment found by stepping through time (a solstice, an eclipse) to full precision.
/// </summary>
internal static class TimeSearch
{
    /// <summary>How closely moments are pinned down by default, in days (about 0.1 ms).</summary>
    public const double Precision = 1e-9;

    // A cap on refining steps: far from time 0, doubles can't resolve the precision above.
    private const int MaxSteps = 200;

    /// <summary>
    /// Narrows down where a smooth function crosses zero between two times (bisection).
    /// <paramref name="rising"/> says whether it goes from below zero to above.
    /// </summary>
    public static double Crossing(Func<double, double> f, double low, double high, bool rising)
    {
        for (int i = 0; i < MaxSteps && high - low > Precision; i++)
        {
            double middle = (low + high) / 2;
            if ((f(middle) < 0) == rising)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return (low + high) / 2;
    }

    /// <summary>
    /// Narrows down where a smooth function peaks (or, with <paramref name="peak"/> false,
    /// bottoms out) between two times (golden-section search), to within
    /// <paramref name="precision"/> days.
    /// </summary>
    public static double Extreme(Func<double, double> f, double low, double high, bool peak,
        double precision = Precision)
    {
        double ratio = (Math.Sqrt(5) - 1) / 2;
        double a = high - ratio * (high - low);
        double b = low + ratio * (high - low);
        double fa = f(a);
        double fb = f(b);
        for (int i = 0; i < MaxSteps && high - low > precision; i++)
        {
            if ((fa > fb) == peak)
            {
                high = b;
                b = a;
                fb = fa;
                a = high - ratio * (high - low);
                fa = f(a);
            }
            else
            {
                low = a;
                a = b;
                fa = fb;
                b = low + ratio * (high - low);
                fb = f(b);
            }
        }

        return (low + high) / 2;
    }
}
